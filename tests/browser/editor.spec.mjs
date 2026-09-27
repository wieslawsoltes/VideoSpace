import { test, expect } from '@playwright/test';
import { mkdir, readFile, stat, writeFile } from 'node:fs/promises';
import { execFileSync } from 'node:child_process';
import path from 'node:path';

async function state(page) { return page.evaluate(() => window.videoSpaceState); }
async function control(page, id) {
  await page.waitForFunction(id => window.videoSpaceControls?.[id]?.width > 0, id);
  return page.evaluate(id => window.videoSpaceControls[id], id);
}
async function click(page, id) {
  const r = await control(page, id); expect(r.enabled, id).not.toBe(false);
  await page.mouse.click(r.x + r.width / 2, r.y + r.height / 2);
}
async function clip(page, id) {
  return (await state(page)).timeline.clips.find(c => c.id === id);
}
async function selectClip(page, id) {
  const c = await clip(page, id); const t = (await state(page)).timeline;
  const x = Math.max(t.x + t.headerWidth + 10, c.x + Math.min(c.width / 2, 45));
  await page.mouse.click(x, c.y + c.height / 2);
}
async function openProject(page, content) {
  await click(page, 'Menu File');
  const chooser = page.waitForEvent('filechooser'); await click(page, 'Open project / captions…');
  await (await chooser).setFiles({ name: 'acceptance.videospace', mimeType: 'application/json', buffer: Buffer.from(content) });
  await expect.poll(async () => (await state(page))?.playhead).toBe(0);
  await expect.poll(async () => (await state(page))?.modal).toBe(false);
}

test.beforeEach(async ({ page }) => {
  const errors = []; page.on('pageerror', e => errors.push(e.message)); page.on('console', msg => { if (msg.type() === 'error') errors.push(msg.text()); });
  page.__errors = errors;
  await page.goto('./', { waitUntil: 'domcontentloaded' });
  try { await page.waitForFunction(() => window.videoSpaceState?.ready && window.VideoSpaceMedia?.diagnostics?.backend?.program, null, { timeout: 150000 }); }
  catch (error) { console.error('Startup errors:', errors); console.error(await page.locator('body').innerText()); throw error; }
  await page.waitForTimeout(500);
});

test.afterEach(async ({ page }, info) => {
  await mkdir('artifacts/screenshots', { recursive: true });
  await page.screenshot({ path: `artifacts/screenshots/${info.title.replace(/[^a-z0-9]+/gi, '-')}.png`, fullPage: true });
  await writeFile(info.outputPath('diagnostics.json'), JSON.stringify({ state: await state(page), media: await page.evaluate(() => window.VideoSpaceMedia?.diagnostics), errors: page.__errors }, null, 2));
});

test('Uno workbench performs edits, history and playback', async ({ page }) => {
  const initial = await state(page); expect(initial.clipCount).toBe(7); expect(initial.assetCount).toBe(6);
  const runtime = await page.evaluate(() => performance.getEntriesByType('resource').some(r => /\.wasm(?:\?|$)/.test(r.name))); expect(runtime).toBe(true);
  const backend = await page.evaluate(() => window.VideoSpaceMedia.diagnostics.backend.program); expect(['WebGPU', 'WebGL2', 'Canvas 2D (reduced grading)']).toContain(backend);
  await page.screenshot({ path: 'artifacts/screenshots/editor-initial.png' });
  await selectClip(page, 'ridge-cut'); await page.keyboard.press('Control+k');
  await expect.poll(async () => (await state(page)).clipCount).toBe(8);
  await page.keyboard.press('Control+z'); await expect.poll(async () => (await state(page)).clipCount).toBe(7);
  await page.keyboard.press('Control+Shift+z'); await expect.poll(async () => (await state(page)).clipCount).toBe(8);
  await page.keyboard.press('Control+z');
  await click(page, 'program Play');
  const before = (await state(page)).playhead; await expect.poll(async () => (await state(page)).playhead).toBeGreaterThan(before + 8);
  await click(page, 'program Play'); await expect.poll(async () => (await state(page)).playing).toBe(false);
  const download = page.waitForEvent('download'); await click(page, 'program Export frame'); const file = await download; await file.saveAs('artifacts/frame.png'); expect((await stat('artifacts/frame.png')).size).toBeGreaterThan(1000);
  const serious = page.__errors.filter(e => !/favicon|AudioContext|WebGPU unavailable/i.test(e)); expect(serious).toEqual([]);
});

test('Direct manipulation, effects and recovery remain transactional', async ({ page }) => {
  await click(page, 'Snapping');
  const before = await clip(page, 'title-cut'); const geometry = (await state(page)).timeline;
  await page.mouse.move(before.x + before.width / 2, before.y + 25); await page.mouse.down(); await page.mouse.move(before.x + before.width / 2 + 24 * geometry.pixelsPerFrame, before.y + 25, { steps: 12 }); await page.mouse.up();
  await expect.poll(async () => (await clip(page, 'title-cut')).start).toBe(60);
  const moved = await clip(page, 'title-cut');
  await page.mouse.move(moved.x + moved.width - 3, moved.y + 25); await page.mouse.down(); await page.mouse.move(moved.x + moved.width - 3 - 24 * geometry.pixelsPerFrame, moved.y + 25, { steps: 12 }); await page.mouse.up();
  await expect.poll(async () => (await clip(page, 'title-cut')).duration).toBeLessThan(before.duration);
  await selectClip(page, 'ridge-cut'); await click(page, 'Workspace Effects'); await click(page, 'Black & white');
  await expect.poll(async () => (await state(page)).framePlan.layers.find(l => l.assetId === 'ridge')?.saturation).toBe(0);
  const download = page.waitForEvent('download'); await click(page, 'Menu File'); await click(page, 'Save project…'); const saved = await download; await saved.saveAs('artifacts/edited.videospace');
  const project = JSON.parse(await readFile('artifacts/edited.videospace', 'utf8')); expect(project.tracks.flatMap(t => t.clips).find(c => c.id === 'title-cut').start).toBe(60);
  await page.waitForTimeout(2500); await page.reload(); await page.waitForFunction(() => window.videoSpaceState?.ready); await expect.poll(async () => (await clip(page, 'title-cut')).start).toBe(60);
});

test('Local video import, linked insert and playable WebM export', async ({ page }) => {
  const fixture = JSON.parse(await readFile('artifacts/fixtures/short.videospace', 'utf8')); fixture.inPoint = 0; fixture.outPoint = 36;
  await openProject(page, JSON.stringify(fixture));
  const chooser = page.waitForEvent('filechooser'); await click(page, 'Import'); await (await chooser).setFiles('artifacts/fixtures/local-video.webm');
  await expect.poll(async () => (await state(page)).assetCount).toBe(7);
  await click(page, 'Menu Clip'); await click(page, 'Overwrite source');
  await expect.poll(async () => (await state(page)).framePlan.layers.some(l => l.kind === 'Video')).toBe(true);
  await expect.poll(async () => (await state(page)).selected.length).toBe(2);
  await page.waitForTimeout(1200);
  await page.screenshot({ path: 'artifacts/screenshots/local-video.png' });
  const download = page.waitForEvent('download', { timeout: 60000 });
  await click(page, 'Export'); await click(page, 'Export video · WebM 720p');
  const exported = await download; await exported.saveAs('artifacts/exported.webm');
  expect((await stat('artifacts/exported.webm')).size).toBeGreaterThan(3000);
  const probe = JSON.parse(execFileSync('ffprobe', ['-v', 'error', '-count_frames', '-show_streams', '-of', 'json', 'artifacts/exported.webm'], { encoding: 'utf8' }));
  const video = probe.streams.find(s => s.codec_type === 'video'); expect(video).toBeTruthy(); expect(video.width).toBe(1280); expect(video.height).toBe(720); expect(Number(video.nb_read_frames)).toBeGreaterThan(15);
  expect(probe.streams.some(s => s.codec_type === 'audio')).toBe(true);
  const rgb = execFileSync('ffmpeg', ['-v', 'error', '-ss', '0.7', '-i', 'artifacts/exported.webm', '-vf', 'scale=1:1', '-frames:v', '1', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-']);
  expect(rgb.length).toBe(3); expect(rgb[0] + rgb[1] + rgb[2]).toBeGreaterThan(40);
  await expect.poll(async () => (await state(page)).exporting).toBe(false);
});
