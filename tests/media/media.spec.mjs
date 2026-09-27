import { test, expect } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import { execFileSync } from 'node:child_process';

for (const fallback of [false, true]) test(`offline ${fallback ? 'explicit WebGL2' : 'automatic hardware selection'} encodes exact cadence and audible PCM`, async ({ page }) => {
  const errors = []; page.on('pageerror', e => errors.push(e.message));
  await page.goto('/VideoSpace/tests/media/harness.html' + (fallback ? '?gpu=off' : ''));
  const project = JSON.parse(await readFile('artifacts/fixtures/short.videospace', 'utf8'));
  await page.evaluate(async () => await VideoSpaceMedia.init());
  const downloads = []; page.on('download', d => downloads.push(d));
  const result = await page.evaluate(async project => {
    const canvas = document.createElement('canvas'); const renderer = new VideoSpaceGPU.Compositor(canvas); await renderer.initialize();
    const selected = {backend:renderer.backend, adapter:renderer.adapterInfo, reason:renderer.fallbackReason}; renderer.dispose();
    await VideoSpaceOffline.run(project, 180);
    return { selected, diagnostics: VideoSpaceMedia.diagnostics, messages: JSON.parse(VideoSpaceMedia.drain()) };
  }, project);
  console.log(JSON.stringify(result));
  expect(result.messages.some(m => m.text.startsWith('Export failed:'))).toBe(false);
  if(fallback) expect(result.selected.backend).toBe('WebGL2');
  await expect.poll(()=>downloads.length).toBe(1);
  const file = `artifacts/offline-${fallback ? 'gl' : 'auto'}.webm`; await downloads[0].saveAs(file);
  const probe = JSON.parse(execFileSync('ffprobe', ['-v','error','-count_frames','-show_streams','-show_format','-of','json',file], { encoding: 'utf8' }));
  const video = probe.streams.find(s => s.codec_type === 'video');
  expect(Number(video.nb_read_frames)).toBe(project.outPoint - project.inPoint); expect(video.width).toBe(320); expect(video.height).toBe(180);
  expect(Number(probe.format.duration)).toBeCloseTo(1.5, 2);
  const samples = execFileSync('ffmpeg', ['-v','error','-i',file,'-map','0:a:0','-f','f32le','-acodec','pcm_f32le','-']);
  let energy = 0; for (let i = 0; i + 4 <= samples.length; i += 4) energy += samples.readFloatLE(i) ** 2;
  expect(Math.sqrt(energy / (samples.length / 4))).toBeGreaterThan(.005);
  expect(errors).toEqual([]);
});

test('offline imported video retains the picture, sound and sequence cadence', async ({ page }) => {
  await page.goto('/VideoSpace/tests/media/harness.html');
  const project = JSON.parse(await readFile('artifacts/fixtures/short.videospace', 'utf8'));
  await page.evaluate(async () => await VideoSpaceMedia.init());
  const chooser = page.waitForEvent('filechooser'); await page.evaluate(() => VideoSpaceMedia.pickMedia());
  await (await chooser).setFiles('artifacts/fixtures/local-video.webm');
  await expect.poll(() => page.evaluate(() => VideoSpaceMedia.diagnostics.imports)).toBe(1);
  const messages = await page.evaluate(() => JSON.parse(VideoSpaceMedia.drain()));
  const asset = messages.find(m => m.type === 'asset').asset;
  project.assets.push(asset); project.inPoint = 0; project.outPoint = 36;
  const picture = structuredClone(project.tracks[0].clips[0]); picture.assetId = asset.id; picture.start = 0; picture.duration = 36; picture.effects.fadeIn = picture.effects.fadeOut = 0;
  project.tracks[0].clips = [picture]; project.tracks[1].clips = []; project.tracks[2].clips = [];
  const sound = structuredClone(picture); sound.id = 'imported-audio'; sound.effects.gain.value = 1;
  project.tracks[3].clips = [sound]; project.captions = [];
  const downloads=[];page.on('download',d=>downloads.push(d));
  const result = await page.evaluate(async p => { await VideoSpaceOffline.run(p, 180); return JSON.parse(VideoSpaceMedia.drain()); }, project);
  console.log(result); expect(result.some(m => m.text.startsWith('Export failed:'))).toBe(false);
  await expect.poll(()=>downloads.length).toBe(1);await downloads[0].saveAs('artifacts/offline-import.webm');
  const probe = JSON.parse(execFileSync('ffprobe', ['-v','error','-count_frames','-show_streams','-of','json','artifacts/offline-import.webm'], { encoding: 'utf8' }));
  expect(Number(probe.streams.find(s => s.codec_type === 'video').nb_read_frames)).toBe(36);
  const pixel = execFileSync('ffmpeg',['-v','error','-ss','0.7','-i','artifacts/offline-import.webm','-vf','scale=1:1','-frames:v','1','-f','rawvideo','-pix_fmt','rgb24','-']);
  expect(pixel[0]).toBeGreaterThan(pixel[1] * 1.6); expect(pixel[0]).toBeGreaterThan(120);
});
