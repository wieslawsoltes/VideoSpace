/* MIT. Serial, bounded VP8/VP9 WebCodecs seeking with keyframe restart and frame leases. */
(function(g){
  'use strict';
  const delay=ms=>new Promise(resolve=>setTimeout(resolve,ms));
  class Decoder {
    static async open(index,options){
      if(!g.VideoDecoder)throw new g.VideoSpaceWebMIndex.UnsupportedWebMError('VideoDecoder is unavailable.');
      const config={...index.config,optimizeForLatency:true};
      if(!(await VideoDecoder.isConfigSupported(config)).supported)throw new g.VideoSpaceWebMIndex.UnsupportedWebMError('Indexed source codec is unavailable.');
      return new Decoder(index,config,options);
    }
    constructor(index,config,{cacheBytes=64*1024*1024,cacheFrames=12,timeoutMs=15000}={}){
      if (!Number.isSafeInteger(cacheBytes) || cacheBytes < 1 || !Number.isInteger(cacheFrames) || cacheFrames < 1 || cacheFrames > 256 || !Number.isFinite(timeoutMs) || timeoutMs < 1) throw new RangeError('Invalid decoder limits.');
      this.index=index;this.config=config;this.cacheBytes=cacheBytes;this.cacheFrames=cacheFrames;this.timeoutMs=timeoutMs;
      this.cache=new Map();this.bytes=0;this.next=0;this.resetRequired=true;this.busy=false;this.disposed=false;
      this.stats={decoded:0,cacheHits:0,restarts:0,bytes:0};
      this.decoder=new VideoDecoder({output:frame=>{
        const bytes=frame.codedWidth*frame.codedHeight*4;
        if(this.disposed||bytes>this.cacheBytes){frame.close();if(!this.disposed)this.failure=new Error('Decoded frame exceeds the cache budget.');return;}
        const old=this.cache.get(frame.timestamp);if(old){old.frame.close();this.bytes-=old.bytes;this.cache.delete(frame.timestamp);}
        this.cache.set(frame.timestamp,{frame,bytes});this.bytes+=bytes;this.stats.decoded++;
        while(this.bytes>this.cacheBytes||this.cache.size>this.cacheFrames){
          const key=[...this.cache.keys()].find(k=>k!==this.target);if(key===undefined)break;
          const entry=this.cache.get(key);entry.frame.close();this.bytes-=entry.bytes;this.cache.delete(key);
        }
        this.stats.bytes=this.bytes;
      },error:error=>{this.failure=error;}});
      this.decoder.configure(config);
    }
    async frame(seconds,signal){
      if(this.disposed)throw new Error('Indexed decoder is disposed.');
      if(this.busy)throw new Error('Concurrent requests must use separate decoder sessions.');
      this.busy=true;
      try{
        signal?.throwIfAborted();const index=this.index,packet=index.frameAt(seconds),target=index.packets[packet].timestamp;this.target=target;
        let cached=this.cache.get(target);
        if(cached)this.stats.cacheHits++;
        else{
          if(this.failure)throw this.failure;
          if(this.resetRequired||packet<this.next){
            this.decoder.reset();this.decoder.configure(this.config);this.next=index.keyAt(packet);this.resetRequired=false;this.stats.restarts++;
          }
          // Lookahead lets implementations release buffered output without flushing each frame.
          const end=Math.min(index.packets.length,packet+3),deadline=performance.now()+this.timeoutMs;
          for(;this.next<end;this.next++){
            signal?.throwIfAborted();if(this.failure)throw this.failure;
            this.decoder.decode(new EncodedVideoChunk(index.chunk(this.next)));
            while(this.decoder.decodeQueueSize>8){
              signal?.throwIfAborted();if(this.failure)throw this.failure;
              if(performance.now()>deadline)throw new Error('Source decoder queue stalled.');await delay(1);
            }
          }
          // After flush the WebCodecs contract requires a key chunk on the next miss.
          const grace=Math.min(deadline,performance.now()+40);
          while(!this.cache.has(target)&&performance.now()<grace){signal?.throwIfAborted();if(this.failure)throw this.failure;await delay(1);}
          if(!this.cache.has(target)){
            let timer,abort;
            try{
              await Promise.race([this.decoder.flush(),new Promise((_,reject)=>{
                timer=setTimeout(()=>reject(new Error('Source decoder flush timed out.')),Math.max(1,deadline-performance.now()));
                abort=()=>reject(signal.reason||new DOMException('Cancelled','AbortError'));signal?.addEventListener('abort',abort,{once:true});
              })]);
            }finally{clearTimeout(timer);if(abort)signal?.removeEventListener('abort',abort);this.resetRequired=true;}
          }
          if(this.failure)throw this.failure;
          cached=this.cache.get(target);if(!cached)throw new Error('Decoder did not output the indexed presentation timestamp '+target+'.');
        }
        signal?.throwIfAborted();
        // The independent bitmap remains valid if a later input evicts this decoded
        // frame while the same transition/composition retains its earlier input.
        const source=await createImageBitmap(cached.frame);
        if(signal?.aborted){source.close();signal.throwIfAborted();}
        return {source,version:target,release:()=>source.close(),timestamp:target};
      }catch(error){
        if(this.decoder.state!=='closed'){this.decoder.reset();this.decoder.configure(this.config);}this.resetRequired=true;throw error;
      }finally{this.busy=false;}
    }
    dispose(){if(this.disposed)return;this.disposed=true;if(this.decoder.state!=='closed')this.decoder.close();for(const entry of this.cache.values())entry.frame.close();this.cache.clear();this.bytes=this.stats.bytes=0;this.index=null;}
  }
  // Export-scoped LRU. Compressed bytes and decoded surfaces have independent budgets.
  class Sources {
    constructor({maxSources=2,maxSourceBytes=96*1024*1024,cacheBytes=32*1024*1024}={}){
      if (!Number.isInteger(maxSources) || maxSources < 1 || maxSources > 8 || !Number.isSafeInteger(maxSourceBytes) || maxSourceBytes < 4 || !Number.isSafeInteger(cacheBytes) || cacheBytes < 1) throw new RangeError('Invalid source cache limits.');
      this.maxSources=maxSources;this.maxSourceBytes=maxSourceBytes;this.cacheBytes=cacheBytes;this.entries=new Map();this.unsupported=new Map();this.indexedIds=new Set();this.stats={indexedSources:0,fallbackSources:0,requests:0,cacheHits:0};
    }
    async frame(id,url,seconds,signal){
      if(this.unsupported.has(id))return null;
      let entry=this.entries.get(id);
      if(entry){this.entries.delete(id);this.entries.set(id,entry);}
      else{
        if(!url.startsWith('blob:'))throw new Error('Indexed decoding accepts local Blob URLs only.');
        while(this.entries.size>=this.maxSources){const key=this.entries.keys().next().value;this.entries.get(key).dispose();this.entries.delete(key);}
        const response=await fetch(url,{signal});if(!response.ok)throw new Error('Could not read local source.');
        const blob=await response.blob();
        if(blob.size>this.maxSourceBytes){this.unsupported.set(id,'Compressed source exceeds index budget.');this.stats.fallbackSources++;return null;}
        const magic=new Uint8Array(await blob.slice(0,4).arrayBuffer());
        if(magic.length!==4||magic[0]!==0x1a||magic[1]!==0x45||magic[2]!==0xdf||magic[3]!==0xa3){this.unsupported.set(id,'Not an EBML source.');this.stats.fallbackSources++;return null;}
        try{
          const index=new g.VideoSpaceWebMIndex.Index(await blob.arrayBuffer(),{maxBytes:this.maxSourceBytes});
          entry=await Decoder.open(index,{cacheBytes:this.cacheBytes});this.entries.set(id,entry);this.indexedIds.add(id);this.stats.indexedSources=this.indexedIds.size;
        }catch(error){
          if(!(error instanceof g.VideoSpaceWebMIndex.UnsupportedWebMError))throw error;
          this.unsupported.set(id,error.message);this.stats.fallbackSources++;return null;
        }
      }
      this.stats.requests++;const hits=entry.stats.cacheHits;const result=await entry.frame(seconds,signal);this.stats.cacheHits+=entry.stats.cacheHits-hits;return result;
    }
    dispose(){for(const entry of this.entries.values())entry.dispose();this.entries.clear();this.unsupported.clear();}
  }
  g.VideoSpaceIndexedVideo={Decoder,Sources};
})(globalThis);
