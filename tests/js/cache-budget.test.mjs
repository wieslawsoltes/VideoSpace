import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import '../../src/VideoSpace.Media/Web/Export.js';
const fixture = JSON.parse(await readFile('artifacts/fixtures/short.videospace','utf8'));
test('rejected expanded frame does not poison last-result cache',()=>{
  const p=structuredClone(fixture),child=structuredClone(fixture);
  child.tracks=Array.from({length:32},(_,i)=>({...structuredClone(child.tracks[0]),id:'ct'+i,clips:[{...structuredClone(child.tracks[0].clips[0]),id:'cc'+i}]}));child.captions=[];
  p.assets.push({...p.assets[0],id:'nested',kind:'Sequence',sequence:child});
  p.tracks=Array.from({length:128},(_,i)=>({...structuredClone(p.tracks[0]),id:'t'+i,clips:[{...structuredClone(p.tracks[0].clips[0]),id:'c'+i,assetId:'nested',start:100}]}));p.captions=[];
  const prepared=VideoSpaceExport.prepare(p);assert.equal(prepared.evaluate(0).layers.length,0);
  assert.throws(()=>prepared.evaluate(100),/4096/);
  assert.throws(()=>prepared.evaluate(100),/4096/);
  assert.equal(prepared.evaluate(0).layers.length,0);
});
