/* MIT. Bounded zero-copy EBML/WebM VP8/VP9 packet index. No DOM or decoder dependency. */
(function (g) {
  'use strict';
  class UnsupportedWebMError extends Error { constructor(message) { super(message); this.name = 'UnsupportedWebMError'; } }
  const fail = message => { throw new UnsupportedWebMError(message); };
  const topIds = new Set([0x114D9B74,0x1549A966,0x1654AE6B,0x1F43B675,0x1C53BB6B,0x1941A469,0x1043A770,0x1254C367]);
  class Index {
    constructor(input, { maxBytes = 128 * 1024 * 1024, maxPackets = 250000, maxElements = 2000000 } = {}) {
      if (![maxBytes,maxPackets,maxElements].every(n => Number.isSafeInteger(n) && n > 0)) throw new RangeError('Invalid index limits.');
      this.bytes = input instanceof Uint8Array ? input : new Uint8Array(input);
      if (this.bytes.byteLength > maxBytes) throw new RangeError('Indexed source exceeds the compressed-memory budget.');
      this.view = new DataView(this.bytes.buffer,this.bytes.byteOffset,this.bytes.byteLength);
      this.maxPackets = maxPackets; this.maxElements = maxElements; this.elements = 0;
      this.timeScale = 1000000; this.durationTicks = 0; this.tracks = []; this.raw = [];
      const header = this.element(0,this.bytes.length);
      if (header.id !== 0x1A45DFA3 || header.unknown) fail('Source is not an EBML document.');
      let docType;
      this.children(header, e => { if(e.id===0x4282)docType=this.text(e); });
      if (!['webm','matroska'].includes(docType)) fail('Source is not a WebM/Matroska document.');
      let offset=header.end, segment;
      while(offset<this.bytes.length) { const e=this.element(offset,this.bytes.length); if(e.id===0x18538067){segment=e;break;} if(e.unknown)fail('Unknown-size element before Segment.'); offset=e.end; }
      if(!segment) fail('No media segment.');
      offset=segment.data;
      while(offset<segment.end) {
        const e=this.element(offset,segment.end);
        if(e.id===0x1F43B675) offset=this.cluster(e);
        else { if(e.unknown)fail('Unknown-sized segment metadata.'); if(e.id===0x1549A966)this.info(e); if(e.id===0x1654AE6B)this.children(e,t=>{if(t.id===0xAE)this.track(t);}); offset=e.end; }
      }
      const videos=this.tracks.filter(t=>t.type===1);
      if(videos.length!==1)fail('Indexed decoding requires exactly one video track.');
      this.track=videos[0]; const t=this.track;
      if(!['V_VP8','V_VP9'].includes(t.codec))fail('Indexed decoding supports VP8/VP9 only.');
      if(t.unsupported)fail(t.unsupported);
      if(!Number.isInteger(t.width)||!Number.isInteger(t.height)||t.width<1||t.height<1||t.width>8192||t.height>8192)fail('Invalid indexed video dimensions.');
      if(this.durationTicks < 0)fail('Invalid container duration.');
      if(this.timeScale<=0||!Number.isFinite(this.timeScale))fail('Invalid timestamp scale.');
      this.packets=[];
      for(const block of this.raw) {
        const v=this.vint(block.data,block.end), offset=v.next;
        if(v.unknown||v.value<1)fail('Invalid block track number.');
        if(v.value!==t.number)continue;
        if(block.additional)fail('Additional video planes require another adapter.');
        if(offset+3>=block.end)fail('Truncated video block.');
        const flags=this.bytes[offset+2];
        if(flags&6)fail('Laced video requires a different demux adapter.');
        const timestamp=Math.round((block.cluster+this.view.getInt16(offset))*this.timeScale/1000);
        if(!Number.isSafeInteger(timestamp)||Math.abs(timestamp)>86400e6)fail('Video timestamp exceeds supported range.');
        const last=this.packets.at(-1);
        if(last&&timestamp<last.timestamp)fail('Reordered container timestamps are not supported by this adapter.');
        this.packets.push({timestamp,duration:block.duration?Math.round(block.duration*this.timeScale/1000):0,
          key:block.key??!!(flags&128),invisible:!!(flags&8),offset:offset+3,length:block.end-offset-3});
        if(this.packets.length>maxPackets)throw new RangeError('Indexed source packet budget exceeded.');
      }
      this.raw=null;
      if(!this.packets.length||!this.packets[0].key)fail('No decodable initial keyframe.');
      this.frames=[];this.keyframes=[];
      for(let i=0;i<this.packets.length;i++) { const p=this.packets[i]; if(p.key)this.keyframes.push(i); if(!p.invisible){if(this.frames.length&&this.packets[this.frames.at(-1)].timestamp===p.timestamp)fail('Duplicate visible timestamps require another adapter.');this.frames.push(i);} }
      if(!this.frames.length)fail('No visible video frames.');
      this.durationUs=Math.round(this.durationTicks*this.timeScale/1000);
      for(let n=0;n<this.frames.length;n++) {
        const p=this.packets[this.frames[n]],next=this.packets[this.frames[n+1]];
        if(!p.duration)p.duration=next?next.timestamp-p.timestamp:t.defaultDuration?Math.round(t.defaultDuration/1000):this.durationUs-p.timestamp;
        if(!(p.duration>0))fail('Final frame duration is unavailable.');
      }
      const last=this.packets[this.frames.at(-1)]; this.durationUs=Math.max(this.durationUs,last.timestamp+last.duration);
      this.config={codec:t.codec==='V_VP8'?'vp8':'vp09.00.10.08',codedWidth:t.width,codedHeight:t.height};
      if(t.displayWidth||t.displayHeight){this.config.displayAspectWidth=t.displayWidth||t.width;this.config.displayAspectHeight=t.displayHeight||t.height;}
    }
    vint(offset,end,id=false) {
      if(offset>=end||!this.bytes[offset])fail('Truncated or invalid EBML variable integer.');
      let width=1,mask=128;while(!(this.bytes[offset]&mask)){mask>>=1;width++;}
      if(width>(id?4:8)||offset+width>end)fail('Invalid EBML integer width.');
      let value=BigInt(id?this.bytes[offset]:this.bytes[offset]&(mask-1));
      for(let i=1;i<width;i++)value=(value<<8n)|BigInt(this.bytes[offset+i]);
      const unknown=!id&&value===(1n<<BigInt(7*width))-1n;
      if(!unknown&&value>BigInt(Number.MAX_SAFE_INTEGER))fail('EBML integer exceeds exact range.');
      return {value:unknown?0:Number(value),unknown,next:offset+width};
    }
    element(offset,end) {
      if(++this.elements>this.maxElements)throw new RangeError('EBML element budget exceeded.');
      const id=this.vint(offset,end,true),size=this.vint(id.next,end);
      const stop=size.unknown?end:size.next+size.value;
      if(stop>end||stop<size.next)fail('EBML element extends beyond its parent.');
      return {id:id.value,data:size.next,end:stop,unknown:size.unknown};
    }
    children(parent,visitor) { let at=parent.data;while(at<parent.end){const e=this.element(at,parent.end);if(e.unknown)fail('Unexpected unknown-sized metadata element.');visitor(e);at=e.end;} }
    uint(e) { if(e.end-e.data<1||e.end-e.data>8)fail('Invalid integer element.');let v=0n;for(let i=e.data;i<e.end;i++)v=(v<<8n)|BigInt(this.bytes[i]);if(v>BigInt(Number.MAX_SAFE_INTEGER))fail('Integer exceeds exact range.');return Number(v); }
    real(e) { const n=e.end-e.data;if(n!==4&&n!==8)fail('Invalid floating-point element.');const v=n===4?this.view.getFloat32(e.data):this.view.getFloat64(e.data);if(!Number.isFinite(v))fail('Non-finite metadata.');return v; }
    text(e) { if(e.end-e.data>4096)fail('Metadata string exceeds limit.');return new TextDecoder('utf-8',{fatal:true}).decode(this.bytes.subarray(e.data,e.end)).replace(/\0+$/,''); }
    info(e) { this.children(e,x=>{if(x.id===0x2AD7B1)this.timeScale=this.uint(x);if(x.id===0x4489)this.durationTicks=this.real(x);}); }
    track(e) {
      const t={number:0,type:0,codec:'',width:0,height:0,defaultDuration:0};
      this.children(e,x=>{
        if(x.id===0xD7)t.number=this.uint(x);else if(x.id===0x83)t.type=this.uint(x);else if(x.id===0x86)t.codec=this.text(x);
        else if(x.id===0x23E383)t.defaultDuration=this.uint(x);
        else if(x.id===0x6D80)t.unsupported='Encrypted/compressed track content is not supported.';
        else if(x.id===0x23314F&&this.real(x)!==1)t.unsupported='Non-unit track timestamp scale.';
        else if(x.id===0x56AA&&this.uint(x)!==0)t.unsupported='Video codec delay requires another adapter.';
        else if(x.id===0xE0)this.children(x,v=>{
          if(v.id===0xB0)t.width=this.uint(v);else if(v.id===0xBA)t.height=this.uint(v);
          else if(v.id===0x54B0)t.displayWidth=this.uint(v);else if(v.id===0x54BA)t.displayHeight=this.uint(v);
          else if([0x54B2,0x53C0,0x54AA,0x54BB,0x54CC,0x54DD].includes(v.id)&&this.uint(v)!==0)t.unsupported='Non-pixel display units, alpha or crop require another adapter.';
        });
      });
      if(t.number<1||this.tracks.some(x=>x.number===t.number))fail('Invalid or duplicate track number.');this.tracks.push(t);
      if(this.tracks.length>128)fail('Track limit exceeded.');
    }
    cluster(e) {
      let offset=e.data,time=0;const blocks=[];
      while(offset<e.end) {
        const x=this.element(offset,e.end);
        if(e.unknown&&topIds.has(x.id))break;
        if(x.unknown)fail('Unexpected unknown-sized cluster child.');
        if(x.id===0xE7)time=this.uint(x);
        else if(x.id===0xA3)blocks.push({data:x.data,end:x.end});
        else if(x.id===0xA0){let block,duration=0,reference=false,additional=false;this.children(x,b=>{
          if(b.id===0xA1){if(block)fail('Multiple blocks in one group.');block=b;}else if(b.id===0x9B)duration=this.uint(b);else if(b.id===0xFB)reference=true;else if(b.id===0x75A1)additional=true;
        });if(block)blocks.push({data:block.data,end:block.end,duration,key:!reference,additional});}
        offset=x.end;
      }
      for(const b of blocks)this.raw.push({...b,cluster:time});
      if(this.raw.length>this.maxPackets*8)throw new RangeError('Container block budget exceeded.');
      return offset;
    }
    frameAt(seconds) {
      if(!Number.isFinite(seconds))throw new RangeError('Invalid source time.');
      const timestamp=Math.max(0,Math.round(seconds*1e6));let lo=0,hi=this.frames.length;
      while(lo<hi){const m=(lo+hi)>>>1;if(this.packets[this.frames[m]].timestamp<=timestamp)lo=m+1;else hi=m;}
      return this.frames[Math.max(0,lo-1)];
    }
    keyAt(packet) {let lo=0,hi=this.keyframes.length;while(lo<hi){const m=(lo+hi)>>>1;if(this.keyframes[m]<=packet)lo=m+1;else hi=m;}return this.keyframes[Math.max(0,lo-1)];}
    chunk(packet) {const p=this.packets[packet];return {type:p.key?'key':'delta',timestamp:p.timestamp,...(p.duration?{duration:p.duration}:{}),data:this.bytes.subarray(p.offset,p.offset+p.length)};}
  }
  g.VideoSpaceWebMIndex={Index,UnsupportedWebMError};
})(globalThis);
