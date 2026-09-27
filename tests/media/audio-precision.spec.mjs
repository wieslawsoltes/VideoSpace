import { test, expect } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import { execFileSync } from 'node:child_process';

test('Opus pre-skip and end padding preserve exact sample count and zero origin',async({page})=>{
  await page.goto('/VideoSpace/tests/media/harness.html?gpu=off');
  const p=JSON.parse(await readFile('artifacts/fixtures/short.videospace','utf8'));
  await page.evaluate(async()=>await VideoSpaceMedia.init());
  const downloads=[];page.on('download',d=>downloads.push(d));
  const result=await page.evaluate(async p=>{await VideoSpaceOffline.run(p,180);return JSON.parse(VideoSpaceMedia.drain());},p);
  expect(result.some(m=>m.text.startsWith('Export failed:'))).toBe(false);
  await expect.poll(()=>downloads.length).toBe(1);
  const file='artifacts/offline-audio-precision.webm';await downloads[0].saveAs(file);
  const frames=JSON.parse(execFileSync('ffprobe',['-v','error','-select_streams','a','-show_frames','-of','json',file],{encoding:'utf8'})).frames;
  const expected=Math.round((p.outPoint-p.inPoint)*p.frameRate.denominator/p.frameRate.numerator*48000);
  expect(frames.reduce((n,f)=>n+f.nb_samples,0)).toBe(expected);
  expect(Number(frames[0].pts_time)).toBeCloseTo(0,6);
  for(let i=1;i<frames.length;i++) expect(Number(frames[i].pts_time)).toBeGreaterThan(Number(frames[i-1].pts_time));
  console.log(JSON.stringify({expectedSamples:expected,decodedSamples:frames.reduce((n,f)=>n+f.nb_samples,0),firstTimestamp:frames[0].pts_time}));
});
