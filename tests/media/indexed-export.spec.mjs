import { test, expect } from '@playwright/test';
import { readFile, writeFile } from 'node:fs/promises';
import { execFileSync } from 'node:child_process';

test('offline export uses indexed VP9 source frames rather than fallback seeks', async ({page})=>{
  await page.goto('/VideoSpace/tests/media/harness.html?gpu=off');
  const project=JSON.parse(await readFile('artifacts/fixtures/short.videospace','utf8'));
  await page.evaluate(async()=>await VideoSpaceMedia.init());
  const choose=page.waitForEvent('filechooser');await page.evaluate(()=>VideoSpaceMedia.pickMedia());await(await choose).setFiles('artifacts/fixtures/local-video.webm');
  await expect.poll(()=>page.evaluate(()=>VideoSpaceMedia.diagnostics.imports)).toBe(1);
  const asset=await page.evaluate(()=>JSON.parse(VideoSpaceMedia.drain()).find(m=>m.type==='asset').asset);
  project.assets.push(asset);project.inPoint=0;project.outPoint=36;project.captions=[];
  const clip=structuredClone(project.tracks[0].clips[0]);clip.assetId=asset.id;clip.duration=36;clip.start=0;clip.sourceIn=0;clip.effects.fadeIn=clip.effects.fadeOut=0;
  project.tracks.forEach(t=>{t.clips=[];t.transitions=[];});project.tracks[0].clips=[clip];
  project.tracks[3].clips=[{...structuredClone(clip),id:'sound'}];
  const download=page.waitForEvent('download');
  const result=await page.evaluate(async p=>{await VideoSpaceOffline.run(p,180);return {export:VideoSpaceMedia.diagnostics.lastExport,messages:JSON.parse(VideoSpaceMedia.drain())};},project);
  expect(result.messages.some(m=>m.text.startsWith('Export failed:'))).toBe(false);
  expect(result.export.indexedDecode.indexedSources).toBe(1);expect(result.export.indexedDecode.fallbackSources).toBe(0);expect(result.export.indexedDecode.requests).toBe(36);
  await(await download).saveAs('artifacts/offline-indexed.webm');
  const probe=JSON.parse(execFileSync('ffprobe',['-v','error','-count_frames','-show_streams','-of','json','artifacts/offline-indexed.webm'],{encoding:'utf8'}));
  expect(Number(probe.streams.find(s=>s.codec_type==='video').nb_read_frames)).toBe(36);
  const audio=execFileSync('ffmpeg',['-v','error','-i','artifacts/offline-indexed.webm','-map','0:a:0','-f','f32le','-acodec','pcm_f32le','-']);expect(audio.length).toBe(72000*2*4);
  await writeFile('artifacts/indexed-export.json',JSON.stringify(result,null,2));
});

test('offline audio outside the export range is not decoded or required', async({page})=>{
  await page.goto('/VideoSpace/tests/media/harness.html?gpu=off');
  const project=JSON.parse(await readFile('artifacts/fixtures/short.videospace','utf8'));
  const asset={...structuredClone(project.assets.find(a=>a.kind==='Audio')),id:'offline',name:'Outside-range.wav',source:'',durationSeconds:10};project.assets.push(asset);
  const clip=structuredClone(project.tracks[3].clips[0]);clip.id='outside-range';clip.assetId=asset.id;clip.start=480;clip.duration=120;
  project.tracks[4].clips=[clip];
  await page.evaluate(async()=>await VideoSpaceMedia.init());
  const download=page.waitForEvent('download');
  const result=await page.evaluate(async p=>{await VideoSpaceOffline.run(p,180);return {export:VideoSpaceMedia.diagnostics.lastExport,messages:JSON.parse(VideoSpaceMedia.drain())};},project);
  expect(result.messages.some(m=>m.text.startsWith('Export failed:'))).toBe(false);expect(result.export.frames).toBe(36);
  await(await download).saveAs('artifacts/offline-range.webm');
});
