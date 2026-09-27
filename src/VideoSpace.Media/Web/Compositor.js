/* VideoSpace MIT. Standalone GPU layer composition with explicit hardware/software selection. */
(function (global) {
  'use strict';
  const ns = global.VideoSpaceGPU = global.VideoSpaceGPU || {};
  const wgsl = `
struct P { transform: vec4f, grade: vec4f, extra: vec4f, crop: vec4f };
@group(0) @binding(0) var image: texture_2d<f32>;
@group(0) @binding(1) var imageSampler: sampler;
@group(0) @binding(2) var<uniform> p: P;
struct V { @builtin(position) position: vec4f, @location(0) uv: vec2f };
@vertex fn vs(@builtin(vertex_index) i: u32) -> V {
  let xy = array<vec2f,6>(vec2f(-1,-1),vec2f(1,-1),vec2f(-1,1),vec2f(-1,1),vec2f(1,-1),vec2f(1,1));
  let pos = xy[i]; let q = pos * p.transform.z * p.extra.zw;
  let co = cos(p.transform.w); let si = sin(p.transform.w);
  var o: V; o.position = vec4f(q.x*co+q.y*si+p.transform.x*2, -q.x*si+q.y*co-p.transform.y*2, 0, 1);
  o.uv = vec2f((pos.x+1)*0.5,(1-pos.y)*0.5); return o;
}
@fragment fn fs(v: V) -> @location(0) vec4f {
  if (v.uv.x < p.crop.x || v.uv.x > 1-p.crop.y || v.uv.y < p.crop.z || v.uv.y > 1-p.crop.w) { discard; }
  let sample = textureSample(image,imageSampler,v.uv); var rgb = sample.rgb * p.grade.y;
  let lum = dot(rgb,vec3f(0.2126,0.7152,0.0722)); rgb = mix(vec3f(lum),rgb,p.grade.w);
  rgb = (rgb-vec3f(0.5))*p.grade.z+vec3f(0.5); rgb += vec3f(p.extra.x*0.08,0,-p.extra.x*0.08);
  let vig = smoothstep(0.2,0.75,distance(v.uv,vec2f(0.5))) * p.extra.y; rgb *= 1-vig;
  return vec4f(clamp(rgb,vec3f(0),vec3f(1)),sample.a*p.grade.x);
}`;
  const glVertex = `#version 300 es
precision highp float;
uniform vec4 tr; uniform vec4 ex; out vec2 uv;
void main(){vec2 xy[6]=vec2[6](vec2(-1,-1),vec2(1,-1),vec2(-1,1),vec2(-1,1),vec2(1,-1),vec2(1,1));vec2 p=xy[gl_VertexID];vec2 q=p*tr.z*ex.zw;float c=cos(tr.w),s=sin(tr.w);gl_Position=vec4(q.x*c+q.y*s+tr.x*2.,-q.x*s+q.y*c-tr.y*2.,0,1);uv=vec2((p.x+1.)*.5,(1.-p.y)*.5);}`;
  const glFragment = `#version 300 es
precision highp float;
uniform sampler2D image;uniform vec4 gr;uniform vec4 ex;uniform vec4 crop;in vec2 uv;out vec4 color;
void main(){if(uv.x<crop.x||uv.x>1.-crop.y||uv.y<crop.z||uv.y>1.-crop.w)discard;vec4 s=texture(image,uv);vec3 rgb=s.rgb*gr.y;float l=dot(rgb,vec3(.2126,.7152,.0722));rgb=mix(vec3(l),rgb,gr.w);rgb=(rgb-.5)*gr.z+.5;rgb+=vec3(ex.x*.08,0,-ex.x*.08);rgb*=1.-smoothstep(.2,.75,distance(uv,vec2(.5)))*ex.y;color=vec4(clamp(rgb,0.,1.),s.a*gr.x);}`;
  class Compositor {
    constructor(canvas, report = () => {}) {
      this.canvas = canvas; this.report = report; this.resources = new Map(); this.backend = 'initializing'; this.lost = false;
    }
    async initialize() {
      const preference = new URLSearchParams(location.search).get('gpu');
      if (navigator.gpu && preference !== 'off') {
        try {
          this.gpu = navigator.gpu;
          const adapter = this.adapter = await this.gpu.requestAdapter({ powerPreference: 'high-performance' });
          const info = adapter?.info;
          this.adapterInfo = info ? { vendor: info.vendor, architecture: info.architecture, description: info.description, isFallbackAdapter: info.isFallbackAdapter } : null;
          const software = adapter?.isFallbackAdapter || info?.isFallbackAdapter || /swiftshader|llvmpipe|software/i.test([info?.vendor, info?.architecture, info?.description].join(' '));
          // Software WebGPU does not provide hardware acceleration. Prefer WebGL2 on
          // software adapters; an explicit gpu=force query is available for diagnosis.
          if (software && preference !== 'force') {
            this.fallbackReason = 'Software WebGPU adapter; using WebGL2';
          } else if (adapter) {
            this.device = await adapter.requestDevice(); this.context = this.canvas.getContext('webgpu');
            if (!this.context) throw new Error('WebGPU canvas unavailable');
            const d = this.device, format = this.gpu.getPreferredCanvasFormat();
            this.context.configure({ device: d, format, alphaMode: 'opaque' });
            const shader = d.createShaderModule({ label: 'VideoSpace transform and grade', code: wgsl });
            const compilation = await shader.getCompilationInfo();
            const errors = compilation.messages.filter(m => m.type === 'error');
            if (errors.length) throw new Error(errors.map(m => m.message).join('\n'));
            this.pipeline = d.createRenderPipeline({ label: 'VideoSpace layers', layout: 'auto', vertex: { module: shader, entryPoint: 'vs' }, fragment: { module: shader, entryPoint: 'fs', targets: [{ format, blend: { color: { srcFactor: 'src-alpha', dstFactor: 'one-minus-src-alpha', operation: 'add' }, alpha: { srcFactor: 'one', dstFactor: 'one-minus-src-alpha', operation: 'add' } } }] }, primitive: { topology: 'triangle-list' } });
            this.sampler = d.createSampler({ magFilter: 'linear', minFilter: 'linear' });
            d.lost.then(info => { if (!this.disposed) { this.lost = true; this.report('GPU device lost: ' + info.message); } });
            d.addEventListener('uncapturederror', e => this.report('GPU: ' + e.error.message));
            this.backend = 'WebGPU'; return;
          }
        } catch (error) {
          this.fallbackReason = error.message; this.report('WebGPU unavailable: ' + error.message);
          this.context?.unconfigure(); this.device?.destroy(); this.device = null; this.context = null;
          this.replaceCanvas();
        }
      }
      const gl = this.canvas.getContext('webgl2', { alpha: false, premultipliedAlpha: false, preserveDrawingBuffer: true });
      if (gl) {
        try {
          this.gl = gl;
          const compile = (type, code) => { const s = gl.createShader(type); gl.shaderSource(s, code); gl.compileShader(s); if (!gl.getShaderParameter(s, gl.COMPILE_STATUS)) throw new Error(gl.getShaderInfoLog(s)); return s; };
          const vs = compile(gl.VERTEX_SHADER, glVertex), fs = compile(gl.FRAGMENT_SHADER, glFragment);
          const program = this.program = gl.createProgram(); gl.attachShader(program, vs); gl.attachShader(program, fs); gl.linkProgram(program); gl.deleteShader(vs); gl.deleteShader(fs);
          if (!gl.getProgramParameter(program, gl.LINK_STATUS)) throw new Error(gl.getProgramInfoLog(program));
          this.uniforms = Object.fromEntries(['tr', 'gr', 'ex', 'crop', 'image'].map(n => [n, gl.getUniformLocation(program, n)]));
          this.backend = 'WebGL2'; this.canvas.addEventListener('webglcontextlost', e => { e.preventDefault(); this.lost = true; this.report('WebGL context lost. Reload to recover.'); }); return;
        } catch (error) { this.report('WebGL unavailable: ' + error.message); this.gl = null; this.replaceCanvas(); }
      }
      this.ctx2d = this.canvas.getContext('2d', { alpha: false }); this.backend = 'Canvas 2D (reduced grading)';
      if (!this.ctx2d) throw new Error('No drawing backend is available.');
    }
    replaceCanvas() {
      const old = this.canvas; const canvas = document.createElement('canvas'); canvas.width = old.width; canvas.height = old.height; canvas.id = old.id; canvas.style.cssText = old.style.cssText;
      if (old.parentNode) old.replaceWith(canvas); this.canvas = canvas;
    }
    parameters(layer, source, plan) {
      const sw = source.videoWidth || source.naturalWidth || source.width || plan.width, sh = source.videoHeight || source.naturalHeight || source.height || plan.height;
      const aspect = sw / sh, target = plan.width / plan.height;
      const fitX = Math.min(1, aspect / target), fitY = Math.min(1, target / aspect);
      return new Float32Array([layer.x || 0, layer.y || 0, layer.scale ?? 1, (layer.rotation || 0) * Math.PI / 180, layer.opacity ?? 1, Math.pow(2, layer.exposure || 0), layer.contrast ?? 1, layer.saturation ?? 1, layer.temperature || 0, layer.vignette || 0, fitX, fitY, layer.cropLeft || 0, layer.cropRight || 0, layer.cropTop || 0, layer.cropBottom || 0]);
    }
    draw(plan, inputs) {
      if (this.disposed) throw new Error('Compositor is disposed');
      if (this.lost) throw new Error('Graphics device was lost; reload the editor before exporting');
      const used = new Set();
      if (this.backend === 'WebGPU') {
        const d = this.device; const encoder = d.createCommandEncoder(); const draws = [];
        for (const { layer, source } of inputs) {
          if (!source || (source instanceof HTMLVideoElement && source.readyState < 2)) continue;
          const width = source.videoWidth || source.naturalWidth || source.width, height = source.videoHeight || source.naturalHeight || source.height;
          if (!(width > 0 && height > 0) || width > d.limits.maxTextureDimension2D || height > d.limits.maxTextureDimension2D) throw new RangeError('Source dimensions exceed the graphics device limits');
          const key = layer.clipId; used.add(key); let r = this.resources.get(key);
          if (!r || r.width !== width || r.height !== height) {
            r?.texture.destroy(); r?.uniform.destroy();
            const texture = d.createTexture({ size: [width, height], format: 'rgba8unorm', usage: GPUTextureUsage.TEXTURE_BINDING | GPUTextureUsage.COPY_DST | GPUTextureUsage.RENDER_ATTACHMENT });
            const uniform = d.createBuffer({ size: 64, usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST });
            r = { width, height, texture, uniform, bind: d.createBindGroup({ layout: this.pipeline.getBindGroupLayout(0), entries: [{ binding: 0, resource: texture.createView() }, { binding: 1, resource: this.sampler }, { binding: 2, resource: { buffer: uniform } }] }) }; this.resources.set(key, r);
          }
          d.queue.copyExternalImageToTexture({ source }, { texture: r.texture }, [width, height]); d.queue.writeBuffer(r.uniform, 0, this.parameters(layer, source, plan)); draws.push(r);
        }
        const pass = encoder.beginRenderPass({ colorAttachments: [{ view: this.context.getCurrentTexture().createView(), clearValue: { r: 0, g: 0, b: 0, a: 1 }, loadOp: 'clear', storeOp: 'store' }] });
        pass.setPipeline(this.pipeline); for (const r of draws) { pass.setBindGroup(0, r.bind); pass.draw(6); } pass.end(); d.queue.submit([encoder.finish()]);
        for (const [key, r] of this.resources) if (!used.has(key)) { r.texture.destroy(); r.uniform.destroy(); this.resources.delete(key); }
      } else if (this.backend === 'WebGL2') {
        const gl = this.gl; gl.viewport(0, 0, this.canvas.width, this.canvas.height); gl.clearColor(0, 0, 0, 1); gl.clear(gl.COLOR_BUFFER_BIT); gl.useProgram(this.program); gl.enable(gl.BLEND); gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA); gl.uniform1i(this.uniforms.image, 0);
        for (const { layer, source } of inputs) {
          if (!source || (source instanceof HTMLVideoElement && source.readyState < 2)) continue;
          const key = layer.clipId; used.add(key); let texture = this.resources.get(key);
          if (!texture) { texture = gl.createTexture(); this.resources.set(key, texture); }
          gl.activeTexture(gl.TEXTURE0); gl.bindTexture(gl.TEXTURE_2D, texture); gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false);
          gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR); gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR); gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE); gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
          gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, source);
          const p = this.parameters(layer, source, plan); gl.uniform4fv(this.uniforms.tr, p.subarray(0, 4)); gl.uniform4fv(this.uniforms.gr, p.subarray(4, 8)); gl.uniform4fv(this.uniforms.ex, p.subarray(8, 12)); gl.uniform4fv(this.uniforms.crop, p.subarray(12, 16)); gl.drawArrays(gl.TRIANGLES, 0, 6);
        }
        for (const [key, texture] of this.resources) if (!used.has(key)) { gl.deleteTexture(texture); this.resources.delete(key); }
      } else {
        const c = this.ctx2d, w = this.canvas.width, h = this.canvas.height; c.fillStyle = '#000'; c.fillRect(0, 0, w, h);
        for (const { layer: l, source } of inputs) {
          if (!source || (source instanceof HTMLVideoElement && source.readyState < 2)) continue;
          const p = this.parameters(l, source, plan); c.save(); c.translate(w * (.5 + p[0]), h * (.5 + p[1])); c.rotate(p[3]); c.scale(p[2], p[2]); c.globalAlpha = p[4]; c.filter = `brightness(${p[5]}) contrast(${p[6]}) saturate(${p[7]})`;
          const dw = w * p[10], dh = h * p[11]; c.beginPath(); c.rect(-dw / 2 + dw * p[12], -dh / 2 + dh * p[14], dw * (1 - p[12] - p[13]), dh * (1 - p[14] - p[15])); c.clip(); c.drawImage(source, -dw / 2, -dh / 2, dw, dh); c.restore();
        }
      }
    }
    dispose() {
      if (this.disposed) return; this.disposed = true;
      if (this.backend === 'WebGPU') { for (const r of this.resources.values()) { r.texture.destroy(); r.uniform.destroy(); } this.context.unconfigure(); this.device.destroy(); }
      if (this.gl) { for (const r of this.resources.values()) this.gl.deleteTexture(r); this.gl.deleteProgram(this.program); }
      this.resources.clear(); this.adapter = this.gpu = null;
    }
  }
  ns.Compositor = Compositor; ns.wgsl = wgsl;
})(globalThis);
