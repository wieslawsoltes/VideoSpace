import { chromium } from '@playwright/test';
import { writeFile, mkdir } from 'node:fs/promises';
const common = ['--enable-unsafe-webgpu','--enable-unsafe-swiftshader','--autoplay-policy=no-user-gesture-required'];
const variants = [
 {name:'shell-angle',args:[...common,'--use-angle=swiftshader']},
 {name:'full-angle',channel:'chromium',args:[...common,'--use-angle=swiftshader']},
 {name:'full-swift-webgpu',channel:'chromium',args:[...common,'--use-angle=swiftshader','--use-webgpu-adapter=swiftshader']},
 {name:'full-vulkan',channel:'chromium',args:[...common,'--enable-features=Vulkan','--use-angle=vulkan','--use-vulkan=swiftshader','--use-webgpu-adapter=swiftshader','--disable-vulkan-surface']},
 {name:'chrome-angle',channel:'chrome',args:[...common,'--use-angle=swiftshader','--use-webgpu-adapter=swiftshader']},
 {name:'full-headed',channel:'chromium',headless:false,args:[...common,'--use-angle=swiftshader','--use-webgpu-adapter=swiftshader']}
];
const results=[];
for(const {name,...options} of variants){
 let browser; const messages=[]; const result={name};
 try{
  browser=await chromium.launch(options); result.version=browser.version(); const page=await browser.newPage();
  page.on('console',m=>messages.push(m.type()+': '+m.text())); page.on('pageerror',e=>messages.push(e.stack));
  await page.goto('http://127.0.0.1:4174/VideoSpace/tests/media/harness.html');
  result.probe=await page.evaluate(async()=>{
   const state={phase:'adapter',errors:[]};
   try{
    window.gpuRoot=navigator.gpu; window.adapterRoot=await gpuRoot.requestAdapter(); if(!adapterRoot)throw new Error('No adapter');
    state.adapter=adapterRoot.info; state.phase='device'; window.deviceRoot=await adapterRoot.requestDevice();
    deviceRoot.lost.then(i=>state.errors.push('lost '+i.reason+' '+i.message));
    state.phase='empty submit'; deviceRoot.queue.submit([]); await deviceRoot.queue.onSubmittedWorkDone();
    state.phase='canvas clear'; const c=document.createElement('canvas'); c.width=c.height=64;document.body.append(c);
    const ctx=c.getContext('webgpu');ctx.configure({device:deviceRoot,format:gpuRoot.getPreferredCanvasFormat()});
    const enc=deviceRoot.createCommandEncoder();const pass=enc.beginRenderPass({colorAttachments:[{view:ctx.getCurrentTexture().createView(),clearValue:{r:1,g:0,b:0,a:1},loadOp:'clear',storeOp:'store'}]});pass.end();deviceRoot.queue.submit([enc.finish()]);await deviceRoot.queue.onSubmittedWorkDone();
    state.phase='compositor';const output=document.createElement('canvas');output.width=320;output.height=180; const r=new VideoSpaceGPU.Compositor(output,t=>state.errors.push(t)); window.rendererRoot=r;await r.initialize();
    const source=document.createElement('canvas');source.width=320;source.height=180;const s=source.getContext('2d');s.fillStyle='red';s.fillRect(0,0,320,180);
    state.phase='compositor draw';r.draw({width:320,height:180},[{layer:{clipId:'a',opacity:1,scale:1,contrast:1,saturation:1},source}]);if(r.device)await r.device.queue.onSubmittedWorkDone();
    state.phase='capture';const f=new VideoFrame(r.canvas,{timestamp:0,duration:41667});f.close();state.backend=r.backend;state.phase='ok';
   }catch(e){state.error=e.message;state.stack=e.stack;}
   return state;
  });
  result.messages=messages;
 }catch(e){result.error=e.message;}finally{await browser?.close();}
 results.push(result);console.log(JSON.stringify(result));
}
await mkdir('artifacts',{recursive:true});await writeFile('artifacts/gpu-probes.json',JSON.stringify(results,null,2));
