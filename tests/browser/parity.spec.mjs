import { test, expect } from '@playwright/test';
import { mkdir, writeFile, readFile } from 'node:fs/promises';

const state = page => page.evaluate(() => window.videoSpaceState);
async function click(page, id) {
  await page.waitForFunction(id => window.videoSpaceControls?.[id]?.width > 0, id);
  const r = await page.evaluate(id => window.videoSpaceControls[id], id);
  await page.mouse.click(r.x + r.width / 2, r.y + r.height / 2);
}
async function menu(page, title, item) { await click(page, 'Menu ' + title); await click(page, item); }
async function select(page, id) {
  const s = await state(page), c = s.timeline.clips.find(c => c.id === id);
  expect(c, id).toBeTruthy();
  await page.mouse.click(Math.max(s.timeline.x + s.timeline.headerWidth + 12, c.x + Math.min(40, c.width / 2)), c.y + 23);
}
async function seek(page, frame) {
  const t = (await state(page)).timeline;
  await page.mouse.click(t.x + t.headerWidth + (frame - t.scrollFrame) * t.pixelsPerFrame, t.y + 28);
  await expect.poll(async () => (await state(page)).playhead).toBe(frame);
}
async function fill(page, id, text) { await click(page,id); await page.keyboard.press('Control+a'); await page.keyboard.type(text); await page.keyboard.press('Tab'); }

test.beforeEach(async ({ page }) => {
  await page.goto('./', {waitUntil:'domcontentloaded'});
  await page.waitForFunction(() => window.videoSpaceState?.ready && window.VideoSpaceMedia?.diagnostics?.backend?.program, null, {timeout: 100000});
  await page.waitForTimeout(400);
});
test.afterEach(async ({page},info)=>{
  await mkdir('artifacts/screenshots',{recursive:true});
  await page.screenshot({path:`artifacts/screenshots/${info.title.replace(/[^a-z0-9]+/gi,'-')}.png`});
  await writeFile(info.outputPath('state.json'),JSON.stringify({state:await state(page),media:await page.evaluate(()=>VideoSpaceMedia.diagnostics)},null,2));
});

test('transition dialog edits real compositing with undo and recovery',async({page})=>{
  await select(page,'ridge-cut');
  await menu(page,'Clip','Add / edit transition…');
  await fill(page,'Transition duration','48');
  await click(page,'Apply transition');
  await expect.poll(async()=>(await state(page)).transitionCount).toBe(1);
  await seek(page,240);
  await expect.poll(async()=>(await state(page)).framePlan.layers[0]?.transition?.kind).toBe('CrossDissolve');
  await select(page,'ridge-cut');await menu(page,'Clip','Add / edit transition…');
  await click(page,'Transition WipeLeft');await click(page,'Apply transition');
  await expect.poll(async()=>(await state(page)).framePlan.layers[0]?.transition?.kind).toBe('WipeLeft');
  await page.keyboard.press('Control+z');
  await expect.poll(async()=>(await state(page)).framePlan.layers[0]?.transition?.kind).toBe('CrossDissolve');
  await page.waitForTimeout(1800);await page.reload();await page.waitForFunction(()=>window.videoSpaceState?.ready);
  await expect.poll(async()=>(await state(page)).transitionCount).toBe(1);
});

test('nested sequence navigation saves root edits and unnests losslessly',async({page})=>{
  await select(page,'ridge-cut');await page.keyboard.press('Control+a');
  await menu(page,'Sequence','Nest selection…');await click(page,'Nest clips');
  await expect.poll(async()=>(await state(page)).clipCount).toBe(2);
  await expect.poll(async()=>(await state(page)).framePlan.layers[0]?.nested?.layers?.length).toBe(3);
  await menu(page,'Sequence','Open nested sequence');
  await expect.poll(async()=>(await state(page)).sequencePath.length).toBe(1);
  await expect.poll(async()=>(await state(page)).clipCount).toBe(7);
  await seek(page,96);await select(page,'ridge-cut');await page.keyboard.press('Control+k');
  await expect.poll(async()=>(await state(page)).clipCount).toBe(8);
  const promise=page.waitForEvent('download');await menu(page,'File','Save project…');
  const download=await promise;await download.saveAs('artifacts/nested-edited.videospace');
  const saved=JSON.parse(await readFile('artifacts/nested-edited.videospace','utf8'));
  expect(saved.assets.find(a=>a.kind==='Sequence').sequence.tracks.flatMap(t=>t.clips).length).toBe(8);
  await menu(page,'Sequence','Return to parent sequence');
  await expect.poll(async()=>(await state(page)).sequencePath.length).toBe(0);
  await page.keyboard.press('Control+z');
  await select(page,(await state(page)).timeline.clips.find(c=>c.track==='v1').id);
  await menu(page,'Sequence','Unnest selection');
  await expect.poll(async()=>(await state(page)).clipCount).toBe(7);
  await page.keyboard.press('Control+z');await expect.poll(async()=>(await state(page)).clipCount).toBe(2);
});

test('manual multicamera sources switch by actual keyboard cuts',async({page})=>{
  await menu(page,'Sequence','Create multicamera source…');await click(page,'Create camera group');
  await expect.poll(async()=>(await state(page)).assetCount).toBe(7);
  await seek(page,0);await menu(page,'Clip','Overwrite source');
  await seek(page,48);
  const s=await state(page);const camera=s.timeline.clips.find(c=>c.track==='v1'&&c.start===0);await select(page,camera.id);
  await page.keyboard.press('2');
  await expect.poll(async()=>(await state(page)).framePlan.layers[0]?.assetId).toBe('coast');
  await seek(page,47);await expect.poll(async()=>(await state(page)).framePlan.layers[0]?.assetId).toBe('ridge');
  await seek(page,48);await select(page,camera.id);await page.keyboard.press('Control+z');
  await expect.poll(async()=>(await state(page)).framePlan.layers[0]?.assetId).toBe('ridge');
});

test('paused preview performs no redundant uploads or frame evaluations',async({page})=>{
  await page.waitForTimeout(1800);
  const stats=()=>page.evaluate(()=>({plans:videoSpaceState.presentedPlans,evaluations:videoSpaceState.plannerEvaluations,frames:VideoSpaceMedia.diagnostics.frames,uploads:VideoSpaceMedia.diagnostics.compositor.program.uploads,allocations:VideoSpaceMedia.diagnostics.compositor.program.allocations}));
  const before=await stats();await page.waitForTimeout(1500);const after=await stats();
  await writeFile('artifacts/idle.json',JSON.stringify({intervalMs:1500,before,after},null,2));
  expect(after).toEqual(before);
  await click(page,'program Next frame');
  await expect.poll(async()=>(await stats()).frames).toBeGreaterThan(before.frames);
  expect((await state(page)).playhead).toBe(97);
});
