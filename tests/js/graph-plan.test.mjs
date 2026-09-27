import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import '../../src/VideoSpace.Media/Web/Export.js';
const graphs = JSON.parse(await readFile('artifacts/fixtures/graph-plans.json', 'utf8'));
function compare(actual, expected, path = '') {
  if (typeof expected === 'number') { assert.ok(Math.abs(actual - expected) < 1e-7, `${path}: ${actual} != ${expected}`); return; }
  if (Array.isArray(expected)) { assert.equal(actual.length, expected.length, path); expected.forEach((x,i)=>compare(actual[i],x,`${path}[${i}]`)); return; }
  if (expected && typeof expected === 'object') { for (const [k,v] of Object.entries(expected)) compare(actual[k],v,path+'.'+k); return; }
  assert.equal(actual,expected,path);
}
for (const graph of graphs) {
  const planner = VideoSpaceExport.prepare(graph.project), mixer = VideoSpaceExport.prepareAudio(graph.project);
  for (const item of graph.cases) test(`${graph.name} frame ${item.frame}: C#/JS recursive plans`,()=>compare(planner.evaluate(item.frame),item.plan));
  for (const item of graph.audio) test(`${graph.name} sample ${item.firstSample}: C#/JS PCM`,()=>{
    const planar=mixer.mix(new Map(),item.firstSample,item.count,item.sampleRate);
    for(let i=0;i<item.count;i++){ assert.ok(Math.abs(planar[i]-item.data[i*2])<1e-6); assert.ok(Math.abs(planar[item.count+i]-item.data[i*2+1])<1e-6); }
  });
  test(`${graph.name}: prepared cache returns unchanged frame by identity`,()=>assert.equal(planner.evaluate(96),planner.evaluate(96)));
}
test('expanded graph is bounded before bridge serialization',()=>{
  const p={layers:[],audio:[],captions:[]};
  for(let i=0;i<4097;i++)p.layers.push({});
  assert.throws(()=>VideoSpaceExport.validatePlan(p),/4096/);
});
