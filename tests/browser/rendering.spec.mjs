import { test, expect } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { mkdir } from 'node:fs/promises';

test('published Uno shell and monitors contain rendered pixels, not just live model state', async ({ page }) => {
  const errors = []; page.on('pageerror', e => errors.push(e.message));
  await page.goto('./', {waitUntil:'domcontentloaded'});
  await page.waitForFunction(() => window.videoSpaceState?.ready && window.VideoSpaceMedia?.diagnostics?.backend?.program);
  await expect.poll(async () => {
    const png = await page.screenshot();
    const rgb = execFileSync('ffmpeg',['-v','error','-i','pipe:0','-vf','scale=120:75','-frames:v','1','-f','rawvideo','-pix_fmt','rgb24','-'], {input:png});
    const colors = new Set(); for(let i=0;i<rgb.length;i+=3) colors.add(rgb.subarray(i,i+3).toString('hex'));
    return colors.size;
  }, { timeout: 20000 }).toBeGreaterThan(100);
  await mkdir('artifacts/screenshots',{recursive:true});
  await page.screenshot({path:'artifacts/screenshots/rendered-workspace.png'});
  expect(errors).toEqual([]);
  const lost = await page.evaluate(() => VideoSpaceMedia.diagnostics.errors.filter(x=>/device lost|Instance reference/i.test(x)));
  expect(lost).toEqual([]);
});
