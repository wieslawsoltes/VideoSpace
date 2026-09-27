import { test, expect } from '@playwright/test';
async function state(page) { return page.evaluate(() => window.videoSpaceState); }
async function click(page,id) {
  await page.waitForFunction(id=>window.videoSpaceControls?.[id]?.width>0,id);
  const r=await page.evaluate(id=>window.videoSpaceControls[id],id);
  await page.mouse.click(r.x+r.width/2,r.y+r.height/2);
}
async function field(page,id,text) { await click(page,id); await page.keyboard.press('Control+a'); await page.keyboard.insertText(text); }
test.beforeEach(async({page})=>{
  await page.goto('./',{waitUntil:'domcontentloaded'});
  await page.waitForFunction(()=>window.videoSpaceState?.ready && window.VideoSpaceMedia?.diagnostics?.backend?.program);
});
test('clip clipboard preserves effects through copy, cut, paste and undo',async({page})=>{
  const before=await state(page), c=before.timeline.clips.find(c=>c.id==='title-cut');
  await page.mouse.click(c.x+c.width/2,c.y+c.height/2);
  await expect.poll(async()=> (await state(page)).selected).toContain('title-cut');
  await page.keyboard.press('Control+c'); await page.keyboard.press('End');
  await expect.poll(async()=> (await state(page)).playhead).toBe(863);
  await page.keyboard.press('Control+v');
  await expect.poll(async()=> (await state(page)).clipCount).toBe(8);
  const pasted=(await state(page)).selected[0]; expect(pasted).not.toBe('title-cut');
  expect((await state(page)).timeline.clips.find(c=>c.id===pasted).start).toBe(863);
  await page.keyboard.press('Control+x');await expect.poll(async()=> (await state(page)).clipCount).toBe(7);
  await page.keyboard.press('Control+z');await expect.poll(async()=> (await state(page)).clipCount).toBe(8);
});
test('blank project accepts an explicit rational sequence timebase',async({page})=>{
  await click(page,'Menu File'); await click(page,'New project…');
  await field(page,'New project name','Acceptance film');
  await field(page,'New frame numerator','30000');await field(page,'New frame denominator','1001');
  await click(page,'Create project');
  await expect.poll(async()=> (await state(page)).modal).toBe(false);
  await expect.poll(async()=> (await state(page)).assetCount).toBe(0);
  expect((await state(page)).clipCount).toBe(0);
  expect((await state(page)).framePlan.fps).toBeCloseTo(30000/1001,8);
});
