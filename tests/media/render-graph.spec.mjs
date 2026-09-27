import { test, expect } from '@playwright/test';
import { readFile, writeFile } from 'node:fs/promises';
import { execFileSync } from 'node:child_process';

test('premultiplied transitions, nested orientation and upload reuse', async ({ page }) => {
  await page.goto('/VideoSpace/tests/media/harness.html?gpu=off');
  const result = await page.evaluate(async () => {
    const canvas = document.createElement('canvas'); canvas.width = 160; canvas.height = 90;
    const errors = []; const r = new VideoSpaceGPU.Compositor(canvas, x => errors.push(x)); await r.initialize();
    const source = color => { const s = document.createElement('canvas'); s.width = 160; s.height = 90; const c = s.getContext('2d'); c.fillStyle = color; c.fillRect(0,0,160,90); s.__vsVersion=1; return s; };
    const red=source('#ff0000'),blue=source('#0000ff'),green=source('#00ff00');
    const layer=(id,extra={})=>({clipId:id,opacity:1,scale:1,contrast:1,saturation:1,...extra});
    const input=(id,s,extra={})=>({layer:layer(id,extra),source:s});
    const plan={width:160,height:90,layers:[],audio:[],captions:[]};
    const pixel=(x=80,y=45)=>{ const c=document.createElement('canvas'); c.width=160;c.height=90;const ctx=c.getContext('2d');ctx.drawImage(r.canvas,0,0);return [...ctx.getImageData(x,y,1,1).data]; };
    const draw=async(inputs)=>{r.draw(plan,inputs);if(r.device)await r.device.queue.onSubmittedWorkDone();return {center:pixel(),left:pixel(20,45),right:pixel(140,45),top:pixel(80,10),bottom:pixel(80,80)};};
    const cases={};
    for(const kind of ['CrossDissolve','DipToBlack','DipToWhite','WipeLeft','WipeRight']) for(const t of [0,.5,1]) {
      const def={kind,progress:t}; cases[kind+t]=await draw([{layer:layer('mix',{transition:def}),transition:{from:[input('a',red)],to:[input('b',blue)]}}]);
    }
    cases.alpha=await draw([input('green',green),{layer:layer('mix',{transition:{kind:'CrossDissolve',progress:.5}}),transition:{from:[input('a',red,{opacity:.5})],to:[input('b',blue,{opacity:.5})]}}]);
    const striped=source('#ff0000');striped.getContext('2d').fillStyle='#0000ff';striped.getContext('2d').fillRect(0,45,160,45);
    cases.nested=await draw([{layer:layer('nest',{nested:plan}),nested:[input('stripes',striped)]}]);
    await draw([input('static',red)]); const uploads=r.stats.uploads, allocations=r.stats.allocations;
    for(let i=0;i<60;i++)await draw([input('static',red)]);
    const reused={uploads:r.stats.uploads-uploads,allocations:r.stats.allocations-allocations};
    red.__vsVersion++;await draw([input('static',red)]);const dirtyUploads=r.stats.uploads-uploads;
    const stats={...r.stats};r.dispose();return {backend:r.backend,cases,errors,reused,dirtyUploads,stats,disposedBytes:r.stats.bytes};
  });
  await writeFile('artifacts/render-graph.json',JSON.stringify(result,null,2));
  expect(result.backend).toBe('WebGL2');expect(result.errors).toEqual([]);
  const near=(actual,expected)=>expected.forEach((x,i)=>expect(Math.abs(actual[i]-x)).toBeLessThanOrEqual(2));
  for(const kind of ['CrossDissolve','DipToBlack','DipToWhite','WipeLeft','WipeRight']) {near(result.cases[kind+'0'].center,[255,0,0,255]);near(result.cases[kind+'1'].center,[0,0,255,255]);}
  near(result.cases['CrossDissolve0.5'].center,[128,0,128,255]);near(result.cases['DipToBlack0.5'].center,[0,0,0,255]);near(result.cases['DipToWhite0.5'].center,[255,255,255,255]);
  near(result.cases['WipeLeft0.5'].left,[0,0,255,255]);near(result.cases['WipeLeft0.5'].right,[255,0,0,255]);near(result.cases['WipeRight0.5'].right,[0,0,255,255]);
  near(result.cases.alpha.center,[64,127,64,255]);near(result.cases.nested.top,[255,0,0,255]);near(result.cases.nested.bottom,[0,0,255,255]);
  expect(result.reused).toEqual({uploads:0,allocations:0});expect(result.dirtyUploads).toBe(1);expect(result.disposedBytes).toBe(0);
});

test('offline nested transition export retains cadence and exact PCM length', async ({ page }) => {
  await page.goto('/VideoSpace/tests/media/harness.html?gpu=off');
  const project=JSON.parse(await readFile('artifacts/fixtures/nested.videospace','utf8'));
  await page.evaluate(async()=>await VideoSpaceMedia.init());
  const downloads=[];page.on('download',d=>downloads.push(d));
  const result=await page.evaluate(async p=>{await VideoSpaceOffline.run(p,180);return JSON.parse(VideoSpaceMedia.drain());},project);
  expect(result.filter(m=>m.text.startsWith('Export failed:'))).toEqual([]);
  await expect.poll(()=>downloads.length).toBe(1);const file='artifacts/offline-nested.webm';await downloads[0].saveAs(file);
  const probe=JSON.parse(execFileSync('ffprobe',['-v','error','-count_frames','-show_streams','-show_format','-of','json',file],{encoding:'utf8'}));
  expect(Number(probe.streams.find(s=>s.codec_type==='video').nb_read_frames)).toBe(36);expect(Number(probe.format.duration)).toBeCloseTo(1.5,3);
  const audio=execFileSync('ffmpeg',['-v','error','-i',file,'-map','0:a:0','-f','f32le','-acodec','pcm_f32le','-']);expect(audio.length).toBe(72000*2*4);
});
