import { test, expect } from '@playwright/test';
import { writeFile } from 'node:fs/promises';

test('indexed decoding selects VFR presentation intervals and supports reverse requests', async ({ page }) => {
  await page.goto('/VideoSpace/tests/media/harness.html?gpu=off');
  const result = await page.evaluate(async () => {
    const times=[0,40000,100000,170000],colors=['#ff0000','#00ff00','#0000ff','#ffff00'];
    const mux=new VideoSpaceWebM.Muxer({width:160,height:90,fps:25,durationUs:220000,codec:'V_VP8',audio:false});
    let error;
    const encoder=new VideoEncoder({output:(chunk,metadata)=>mux.add(1,chunk,metadata),error:e=>{error=e;}});
    encoder.configure({codec:'vp8',width:160,height:90,bitrate:1000000,framerate:25,latencyMode:'realtime'});
    const canvas=document.createElement('canvas');canvas.width=160;canvas.height=90;const c=canvas.getContext('2d');
    for(let i=0;i<times.length;i++){c.fillStyle=colors[i];c.fillRect(0,0,160,90);const f=new VideoFrame(canvas,{timestamp:times[i],duration:(times[i+1]??220000)-times[i]});try{encoder.encode(f,{keyFrame:i===0||i===2});}finally{f.close();}}
    await encoder.flush();encoder.close();if(error)throw error;
    const blob=mux.finalize(),index=new VideoSpaceWebMIndex.Index(await blob.arrayBuffer());
    const decoder=await VideoSpaceIndexedVideo.Decoder.open(index,{cacheFrames:2,cacheBytes:1024*1024});
    const pixel=lease=>{c.clearRect(0,0,160,90);c.drawImage(lease.source,0,0);return [...c.getImageData(80,45,1,1).data];};
    const kept=await decoder.frame(.100);
    const samples=[];
    try{
      for(const time of [.039,.169,.04,.100,.18,0]){const lease=await decoder.frame(time);try{samples.push({time,timestamp:lease.timestamp,pixel:pixel(lease)});}finally{lease.release();}}
      const retained=pixel(kept);
      const controller=new AbortController();controller.abort();let cancelled=false;
      try{await decoder.frame(.1,controller.signal);}catch(e){cancelled=e.name==='AbortError';}
      const stats={...decoder.stats};decoder.dispose();
      return {samples,retained,cancelled,stats,bytesAfterDispose:decoder.stats.bytes,index:index.packets.map(p=>({timestamp:p.timestamp,key:p.key}))};
    }finally{kept.release();decoder.dispose();}
  });
  await writeFile('artifacts/indexed-video.json',JSON.stringify(result,null,2));
  expect(result.samples.map(s=>s.timestamp)).toEqual([0,100000,40000,100000,170000,0]);
  const expected=[[255,0,0],[0,0,255],[0,255,0],[0,0,255],[255,255,0],[255,0,0]];
  result.samples.forEach((s,i)=>expected[i].forEach((v,ch)=>expect(Math.abs(v-s.pixel[ch])).toBeLessThan(12)));
  expect(result.retained[2]).toBeGreaterThan(240);expect(result.cancelled).toBe(true);
  expect(result.stats.bytes).toBeLessThanOrEqual(1024*1024);expect(result.stats.restarts).toBeGreaterThan(1);expect(result.bytesAfterDispose).toBe(0);
});

test('source cache reports unsupported formats and rejects unsafe budgets', async ({page})=>{
  await page.goto('/VideoSpace/tests/media/harness.html?gpu=off');
  const result=await page.evaluate(async()=>{
    let invalid=false;try{new VideoSpaceIndexedVideo.Sources({maxSources:0});}catch(e){invalid=e instanceof RangeError;}
    const pool=new VideoSpaceIndexedVideo.Sources(),url=URL.createObjectURL(new Blob(['not-webm']));
    try{return {invalid,first:await pool.frame('a',url,0),second:await pool.frame('a',url,0),stats:{...pool.stats},reason:pool.unsupported.get('a')};}finally{pool.dispose();URL.revokeObjectURL(url);}
  });
  expect(result.invalid).toBe(true);expect(result.first).toBeNull();expect(result.second).toBeNull();expect(result.stats.fallbackSources).toBe(1);expect(result.reason).toBe('Not an EBML source.');
});
