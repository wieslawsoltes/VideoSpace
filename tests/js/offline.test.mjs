import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import '../../src/VideoSpace.Media/Web/Export.js';
import '../../src/VideoSpace.Media/Web/WebM.js';
import '../../src/VideoSpace.Media/Web/OfflineExport.js';
const original = JSON.parse(await readFile('artifacts/fixtures/short.videospace', 'utf8'));
test('EBML sizes reserve the all-ones marker', () => {
  assert.deepEqual([...VideoSpaceWebM.size(126)], [254]); assert.deepEqual([...VideoSpaceWebM.size(127)], [64, 127]);
});
test('unsigned EBML numbers reject negative values', () => assert.throws(() => VideoSpaceWebM.uint(-1)));
test('bounded muxer rejects output overflow', () => {
  const m = new VideoSpaceWebM.Muxer({width:320,height:180,fps:24,durationUs:1e6,audio:false,limit:1});
  assert.throws(() => m.add(1,{timestamp:0,duration:41667,byteLength:2,copyTo(){},type:'key'}));
});
test('muxer rejects an empty video', () => assert.throws(() => new VideoSpaceWebM.Muxer({width:320,height:180,fps:24,durationUs:1e6}).finalize()));
test('PCM mixer produces actual stereo sound', () => {
  const pcm = VideoSpaceOffline.mix(original,new Map(),48000,960);
  assert.ok(pcm.some(x => Math.abs(x)>.001)); assert.equal(pcm.length,1920);
});
test('PCM mixer respects mute', () => {
  const p = structuredClone(original); p.tracks[3].muted = true;
  assert.ok(VideoSpaceOffline.mix(p,new Map(),48000,960).every(x=>x===0));
});
test('PCM mixer respects solo', () => {
  const p = structuredClone(original); p.tracks[4].solo = true;
  assert.ok(VideoSpaceOffline.mix(p,new Map(),48000,960).every(x=>x===0));
});
test('PCM mixer pans without leaking to the muted channel', () => {
  const p = structuredClone(original); p.tracks[3].clips[0].effects.pan = -1;
  const data=VideoSpaceOffline.mix(p,new Map(),48000,960);
  assert.ok(data.slice(960).every(x=>x===0)); assert.ok(data.slice(0,960).some(x=>Math.abs(x)>.001));
});
test('PCM mixer refuses missing imported audio', () => {
  const p = structuredClone(original); p.assets.find(a=>a.id==='score').source='';
  assert.throws(()=>VideoSpaceOffline.mix(p,new Map(),48000,960));
});
