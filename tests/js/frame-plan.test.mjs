import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import '../../src/VideoSpace.Media/Web/Export.js';
const fixture = JSON.parse(await readFile('artifacts/fixtures/frame-plans.json', 'utf8'));
function compare(actual, expected, path = '') {
  if (typeof expected === 'number') { assert.ok(Math.abs(actual - expected) < 1e-8, `${path}: ${actual} != ${expected}`); return; }
  if (Array.isArray(expected)) { assert.equal(actual.length, expected.length, path); expected.forEach((item, i) => compare(actual[i], item, `${path}[${i}]`)); return; }
  if (expected && typeof expected === 'object') { for (const [key, value] of Object.entries(expected)) compare(actual[key], value, path + '.' + key); return; }
  assert.equal(actual, expected, path);
}
for (const sample of fixture.cases) test(`browser export evaluation matches C# at frame ${sample.frame}`, () => compare(globalThis.VideoSpaceExport.evaluate(fixture.project, sample.frame), sample.plan));
