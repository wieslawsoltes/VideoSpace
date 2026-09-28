import test from 'node:test';
import assert from 'node:assert/strict';
import '../../src/VideoSpace.Media/Web/WebM.js';
import '../../src/VideoSpace.Media/Web/WebMIndex.js';
const {Index}=VideoSpaceWebMIndex;
const {uint,size}=VideoSpaceWebM;
const join=parts=>Uint8Array.from(parts.flatMap(p=>[...p]));
const el=(id,data)=>join([uint(id),size(data.length),data]);
const num=(id,n)=>el(id,uint(n));
const text=(id,s)=>el(id,new TextEncoder().encode(s));
const master=(id,parts)=>el(id,join(parts));
const header=master(0x1a45dfa3,[text(0x4282,'webm')]);
function synthetic({unknown=false,group=false,lace=false,unsupported=false}={}){
 const track=master(0xae,[num(0xd7,1),num(0x83,1),text(0x86,unsupported?'V_MPEG4/ISO/AVC':'V_VP9'),num(0x23e383,40000000),master(0xe0,[num(0xb0,160),num(0xba,90)])]);
 const block=(time,key=true)=>{const data=join([new Uint8Array([0x81,(time>>8)&255,time&255,(key?128:0)|(lace?2:0)]),new Uint8Array([1,2,3])]);return group?master(0xa0,[el(0xa1,data),num(0x9b,40),...(key?[]:[num(0xfb,0)])]):el(0xa3,data);};
 const payload=join([num(0xe7,100),block(-100),block(-60,false),block(0),block(70,false)]);
 const cluster=unknown?join([uint(0x1f43b675),new Uint8Array([255]),payload]):el(0x1f43b675,payload);
 const data=join([master(0x1549a966,[num(0x2ad7b1,1000000)]),master(0x1654ae6b,[track]),cluster,master(0x1c53bb6b,[])]);
 return join([header,uint(0x18538067),unknown?new Uint8Array([255]):size(data.length),data]);
}
test('variable timestamps use presentation intervals, not nominal FPS',()=>{const x=new Index(synthetic());assert.deepEqual(x.packets.map(p=>p.timestamp),[0,40000,100000,170000]);assert.equal(x.frameAt(.099),1);assert.equal(x.frameAt(.100),2);assert.equal(x.frameAt(-1),0);assert.equal(x.frameAt(99),3);assert.equal(x.keyAt(3),2);assert.equal(x.durationUs,210000);});
test('indexed chunks reference source bytes without copying',()=>{const data=synthetic(),x=new Index(data);assert.equal(x.chunk(0).data.buffer,data.buffer);assert.equal(x.chunk(1).type,'delta');});
test('unknown Segment and Cluster sizes stop at their next sibling',()=>assert.equal(new Index(synthetic({unknown:true})).frames.length,4));
test('BlockGroup references determine independent keyframes',()=>{const x=new Index(synthetic({group:true}));assert.deepEqual(x.keyframes,[0,2]);assert.equal(x.packets[1].duration,40000);});
test('unsupported codec rejects explicitly',()=>assert.throws(()=>new Index(synthetic({unsupported:true})),/VP8\/VP9/));
test('laced video rejects without assuming packet sizes',()=>assert.throws(()=>new Index(synthetic({lace:true})),/Laced/));
test('every truncation of a known-size stream is rejected',()=>{const data=synthetic();for(let n=0;n<data.length;n++)assert.throws(()=>new Index(data.subarray(0,n)));});
test('compressed and element budgets reject early',()=>{assert.throws(()=>new Index(synthetic(),{maxBytes:10}),/budget/);assert.throws(()=>new Index(synthetic(),{maxElements:3}),/budget/);});
test('subarray input respects byte offsets',()=>{const data=synthetic(),wrapped=new Uint8Array(data.length+17);wrapped.set(data,7);assert.equal(new Index(wrapped.subarray(7,7+data.length)).packets.length,4);});
test('invalid source time rejects',()=>assert.throws(()=>new Index(synthetic()).frameAt(NaN),/source time/));
test('own muxer timestamps roundtrip exactly',async()=>{
 const m=new VideoSpaceWebM.Muxer({width:160,height:90,fps:24,durationUs:125000,audio:false});
 for(let i=0;i<3;i++)m.add(1,{timestamp:Math.round(i*1e6/24),duration:41667,type:i===0?'key':'delta',byteLength:3,copyTo:data=>data.set([1,2,3])});
 const x=new Index(await m.finalize().arrayBuffer());assert.deepEqual(x.packets.map(p=>p.timestamp),[0,41667,83333]);assert.equal(x.frameAt(1/24),1);
});
test('invalid resource limits reject before parsing',()=>assert.throws(()=>new Index(synthetic(),{maxElements:0}),/limits/));
