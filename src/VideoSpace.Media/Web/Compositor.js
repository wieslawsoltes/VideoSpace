/* MIT. Bounded, premultiplied render graph for WebGPU/WebGL2.
   Nested sequences and transitions remain in one device/context. No preview readback. */
(function (g) {
  'use strict';
  const ns = g.VideoSpaceGPU = g.VideoSpaceGPU || {};
  const wgsl = `
struct P { transform: vec4f, grade: vec4f, extra: vec4f, crop: vec4f, options: vec4f };
@group(0) @binding(0) var image: texture_2d<f32>;
@group(0) @binding(1) var imageSampler: sampler;
@group(0) @binding(2) var<uniform> p: P;
struct V { @builtin(position) position: vec4f, @location(0) uv: vec2f };
@vertex fn vs(@builtin(vertex_index) i: u32) -> V {
  let xy = array<vec2f,6>(vec2f(-1,-1),vec2f(1,-1),vec2f(-1,1),vec2f(-1,1),vec2f(1,-1),vec2f(1,1));
  let pos = xy[i]; let aspect = p.options.y;
  let q = pos * p.transform.z * p.extra.zw * vec2f(aspect,1);
  let co = cos(p.transform.w); let si = sin(p.transform.w);
  var o: V; o.position = vec4f((q.x*co+q.y*si)/aspect+p.transform.x*2, -q.x*si+q.y*co-p.transform.y*2, 0, 1);
  o.uv = vec2f((pos.x+1)*0.5,(1-pos.y)*0.5); return o;
}
@fragment fn fs(v: V) -> @location(0) vec4f {
  if (v.uv.x < p.crop.x || v.uv.x > 1-p.crop.y || v.uv.y < p.crop.z || v.uv.y > 1-p.crop.w) { discard; }
  let texel = textureSample(image,imageSampler,v.uv);
  var rgb = texel.rgb; if (p.options.x > 0.5) { rgb /= max(texel.a,0.00001); }
  rgb *= p.grade.y;
  let lum = dot(rgb,vec3f(0.2126,0.7152,0.0722)); rgb = mix(vec3f(lum),rgb,p.grade.w);
  rgb = (rgb-vec3f(0.5))*p.grade.z+vec3f(0.5); rgb += vec3f(p.extra.x*0.08,0,-p.extra.x*0.08);
  rgb *= 1-smoothstep(0.2,0.75,distance(v.uv,vec2f(0.5)))*p.extra.y;
  let alpha = texel.a*p.grade.x; return vec4f(clamp(rgb,vec3f(0),vec3f(1))*alpha,alpha);
}`;
  const mixWgsl = `
@group(0) @binding(0) var a: texture_2d<f32>;
@group(0) @binding(1) var b: texture_2d<f32>;
@group(0) @binding(2) var smp: sampler;
@group(0) @binding(3) var<uniform> settings: vec4f;
struct V { @builtin(position) position: vec4f, @location(0) uv: vec2f };
@vertex fn vs(@builtin(vertex_index) i:u32)->V {
 let xy=array<vec2f,6>(vec2f(-1,-1),vec2f(1,-1),vec2f(-1,1),vec2f(-1,1),vec2f(1,-1),vec2f(1,1));
 var o:V; o.position=vec4f(xy[i],0,1); o.uv=vec2f((xy[i].x+1)*0.5,(1-xy[i].y)*0.5); return o;
}
@fragment fn fs(v:V)->@location(0) vec4f {
 let x=textureSample(a,smp,v.uv); let y=textureSample(b,smp,v.uv); let t=settings.x; let kind=settings.y;
 if (kind<0.5) { return mix(x,y,t); }
 if (kind<2.5) {
   let color=vec4f(vec3f(select(0.0,1.0,kind>1.5)),1);
   if(t<0.5) { return mix(x,color,t*2); } return mix(color,y,t*2-1);
 }
 if(kind<3.5) { return select(x,y,v.uv.x<t); } return select(x,y,v.uv.x>1-t);
}`;
  const glVertex = `#version 300 es
precision highp float;
uniform vec4 tr; uniform vec4 ex; uniform vec4 opts; out vec2 uv;
void main(){vec2 xy[6]=vec2[6](vec2(-1,-1),vec2(1,-1),vec2(-1,1),vec2(-1,1),vec2(1,-1),vec2(1,1));vec2 p=xy[gl_VertexID];vec2 q=p*tr.z*ex.zw*vec2(opts.y,1);float c=cos(tr.w),s=sin(tr.w);gl_Position=vec4((q.x*c+q.y*s)/opts.y+tr.x*2.,-q.x*s+q.y*c-tr.y*2.,0,1);uv=vec2((p.x+1.)*.5,(1.-p.y)*.5);}`;
  const glFragment = `#version 300 es
precision highp float;
uniform sampler2D image;uniform vec4 gr;uniform vec4 ex;uniform vec4 crop;uniform vec4 opts;in vec2 uv;out vec4 color;
void main(){if(uv.x<crop.x||uv.x>1.-crop.y||uv.y<crop.z||uv.y>1.-crop.w)discard;vec2 st=vec2(uv.x,opts.z>.5?1.-uv.y:uv.y);vec4 texel=texture(image,st);vec3 rgb=texel.rgb;if(opts.x>.5)rgb/=max(texel.a,.00001);rgb*=gr.y;float l=dot(rgb,vec3(.2126,.7152,.0722));rgb=mix(vec3(l),rgb,gr.w);rgb=(rgb-.5)*gr.z+.5;rgb+=vec3(ex.x*.08,0,-ex.x*.08);rgb*=1.-smoothstep(.2,.75,distance(uv,vec2(.5)))*ex.y;float alpha=texel.a*gr.x;color=vec4(clamp(rgb,0.,1.)*alpha,alpha);}`;
  const glMixVertex = `#version 300 es
precision highp float;out vec2 uv;
void main(){vec2 xy[6]=vec2[6](vec2(-1,-1),vec2(1,-1),vec2(-1,1),vec2(-1,1),vec2(1,-1),vec2(1,1));vec2 p=xy[gl_VertexID];gl_Position=vec4(p,0,1);uv=vec2((p.x+1.)*.5,(1.-p.y)*.5);}`;
  const glMixFragment = `#version 300 es
precision highp float;uniform sampler2D a;uniform sampler2D b;uniform vec4 settings;in vec2 uv;out vec4 color;
void main(){vec2 st=vec2(uv.x,1.-uv.y);vec4 x=texture(a,st),y=texture(b,st);float t=settings.x,k=settings.y;if(k<.5)color=mix(x,y,t);else if(k<2.5){vec4 c=vec4(vec3(k>1.5?1.:0.),1);color=t<.5?mix(x,c,t*2.):mix(c,y,t*2.-1.);}else color=(k<3.5?uv.x<t:uv.x>1.-t)?y:x;}`;
  const kinds = { CrossDissolve: 0, DipToBlack: 1, DipToWhite: 2, WipeLeft: 3, WipeRight: 4 };
  const sizeOf = source => [source.videoWidth || source.naturalWidth || source.width, source.videoHeight || source.naturalHeight || source.height];
  class Compositor {
    constructor(canvas, report = () => {}, { memoryLimit = 384 * 1024 * 1024 } = {}) {
      this.canvas = canvas; this.report = report; this.backend = 'initializing'; this.lost = false;
      this.textures = new Map(); this.targets = new Map(); this.uniforms = new Map(); this.memoryLimit = memoryLimit; this.bytes = 0;
      this.stats = { frames: 0, passes: 0, uploads: 0, uniformWrites: 0, allocations: 0, bytes: 0 };
    }
    async initialize() {
      this.gpu = navigator.gpu; const preference = new URLSearchParams(location.search).get('gpu');
      if (this.gpu && preference !== 'off') {
        try {
          this.adapter = await this.gpu.requestAdapter({ powerPreference: 'high-performance' });
          const adapter = this.adapter, info = adapter?.info;
          this.adapterInfo = info ? { vendor: info.vendor, architecture: info.architecture, description: info.description, isFallbackAdapter: info.isFallbackAdapter } : null;
          const software = adapter?.isFallbackAdapter || info?.isFallbackAdapter || /swiftshader|llvmpipe|software/i.test([info?.vendor, info?.architecture, info?.description].join(' '));
          if (software && preference !== 'force') this.fallbackReason = 'Software WebGPU adapter; using WebGL2';
          else if (adapter) {
            const d = this.device = await adapter.requestDevice(); this.context = this.canvas.getContext('webgpu');
            if (!this.context) throw new Error('WebGPU canvas unavailable');
            this.format = this.gpu.getPreferredCanvasFormat(); this.context.configure({ device: d, format: this.format, alphaMode: 'opaque' });
            const pipeline = async (code, label) => {
              const module = d.createShaderModule({ label, code }); const compilation = await module.getCompilationInfo();
              const errors = compilation.messages.filter(m => m.type === 'error'); if (errors.length) throw new Error(errors.map(m => m.message).join('\n'));
              return d.createRenderPipeline({ label, layout: 'auto', vertex: { module, entryPoint: 'vs' }, fragment: { module, entryPoint: 'fs', targets: [{ format: this.format, blend: { color: { srcFactor: 'one', dstFactor: 'one-minus-src-alpha', operation: 'add' }, alpha: { srcFactor: 'one', dstFactor: 'one-minus-src-alpha', operation: 'add' } } }] }, primitive: { topology: 'triangle-list' } });
            };
            this.pipeline = await pipeline(wgsl, 'VideoSpace premultiplied layer'); this.mixPipeline = await pipeline(mixWgsl, 'VideoSpace two-input transition');
            this.sampler = d.createSampler({ magFilter: 'linear', minFilter: 'linear' });
            d.lost.then(info => { if (!this.disposed) { this.lost = true; this.report('GPU device lost: ' + info.message); } });
            d.addEventListener('uncapturederror', e => { this.lost = true; this.report('GPU: ' + e.error.message); });
            this.backend = 'WebGPU'; return;
          }
        } catch (error) {
          this.fallbackReason = error.message; this.report('WebGPU unavailable: ' + error.message);
          this.context?.unconfigure(); this.device?.destroy(); this.device = null; this.context = null; this.replaceCanvas();
        }
      }
      const gl = this.canvas.getContext('webgl2', { alpha: false, premultipliedAlpha: false, preserveDrawingBuffer: true });
      if (gl) {
        try {
          this.gl = gl;
          const program = (vs, fs, names) => {
            const shaders = [[gl.VERTEX_SHADER, vs], [gl.FRAGMENT_SHADER, fs]].map(([type, code]) => {
              const s = gl.createShader(type); gl.shaderSource(s, code); gl.compileShader(s); if (!gl.getShaderParameter(s, gl.COMPILE_STATUS)) { const error = gl.getShaderInfoLog(s); gl.deleteShader(s); throw new Error(error); } return s;
            });
            const p = gl.createProgram(); for (const s of shaders) gl.attachShader(p, s); gl.linkProgram(p); for (const s of shaders) gl.deleteShader(s);
            if (!gl.getProgramParameter(p, gl.LINK_STATUS)) throw new Error(gl.getProgramInfoLog(p));
            return { program: p, locations: Object.fromEntries(names.map(n => [n, gl.getUniformLocation(p, n)])) };
          };
          this.normalProgram = program(glVertex, glFragment, ['tr', 'gr', 'ex', 'crop', 'opts', 'image']);
          this.mixProgram = program(glMixVertex, glMixFragment, ['a', 'b', 'settings']);
          this.backend = 'WebGL2'; this.canvas.addEventListener('webglcontextlost', e => { e.preventDefault(); this.lost = true; this.report('WebGL context lost. Reload to recover.'); }); return;
        } catch (error) { this.report('WebGL unavailable: ' + error.message); this.gl = null; this.replaceCanvas(); }
      }
      this.ctx2d = this.canvas.getContext('2d', { alpha: false }); this.backend = 'Canvas 2D (reduced grading)';
      if (!this.ctx2d) throw new Error('No drawing backend is available.');
    }
    replaceCanvas() {
      const old = this.canvas, canvas = document.createElement('canvas'); canvas.width = old.width; canvas.height = old.height; canvas.id = old.id; canvas.style.cssText = old.style.cssText;
      if (old.parentNode) old.replaceWith(canvas); this.canvas = canvas;
    }
    destroy(info) {
      if (!info) return;
      if (this.device) info.texture?.destroy();
      if (this.gl) { if (info.texture) this.gl.deleteTexture(info.texture); if (info.fbo) this.gl.deleteFramebuffer(info.fbo); }
      this.bytes -= info.bytes || 0;
    }
    reserve(bytes) {
      for (const map of [this.textures, this.targets]) for (const [key, value] of map) {
        if (this.bytes + bytes <= this.memoryLimit) break;
        if (!this.used.has(key)) { this.destroy(value); map.delete(key); }
      }
      if (this.bytes + bytes > this.memoryLimit) throw new RangeError('Composition exceeds the graphics memory budget. Reduce media or export resolution.');
      this.bytes += bytes;
    }
    texture(key, width, height, target = false) {
      this.used.add(key); const map = target ? this.targets : this.textures;
      let r = map.get(key); if (r && r.width === width && r.height === height) return r;
      if (r) { this.destroy(r); map.delete(key); }
      const limit = this.device?.limits.maxTextureDimension2D || (this.gl ? this.gl.getParameter(this.gl.MAX_TEXTURE_SIZE) : 8192);
      if (!(width > 0 && height > 0) || width > limit || height > limit) throw new RangeError('Source dimensions exceed graphics limits.');
      const bytes = width * height * 4; this.reserve(bytes); r = { width, height, bytes, premultiplied: target, flipY: target && !!this.gl };
      if (this.device) {
        r.texture = this.device.createTexture({ size: [width, height], format: target ? this.format : 'rgba8unorm', usage: GPUTextureUsage.TEXTURE_BINDING | GPUTextureUsage.COPY_DST | GPUTextureUsage.RENDER_ATTACHMENT }); r.view = r.texture.createView();
      } else if (this.gl) {
        const gl = this.gl; r.texture = gl.createTexture(); gl.bindTexture(gl.TEXTURE_2D, r.texture);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR); gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE); gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, width, height, 0, gl.RGBA, gl.UNSIGNED_BYTE, null);
        if (target) {
          r.fbo = gl.createFramebuffer(); gl.bindFramebuffer(gl.FRAMEBUFFER, r.fbo); gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, r.texture, 0);
          if (gl.checkFramebufferStatus(gl.FRAMEBUFFER) !== gl.FRAMEBUFFER_COMPLETE) throw new Error('Could not allocate composition framebuffer.');
        }
      } else { r.canvas = document.createElement('canvas'); r.canvas.width = width; r.canvas.height = height; r.context = r.canvas.getContext('2d'); }
      map.set(key, r); this.stats.allocations++; return r;
    }
    external(key, input) {
      const source = input.source;
      if (!source || source instanceof HTMLVideoElement && source.readyState < 2) return null;
      const [width, height] = sizeOf(source), r = this.texture(key, width, height);
      const immutable = (typeof ImageBitmap !== 'undefined' && source instanceof ImageBitmap) || source instanceof HTMLImageElement;
      const version = input.version ?? source.__vsVersion ?? (immutable ? (source.currentSrc || 0) : undefined);
      if (r.source !== source || version === undefined || r.version !== version) {
        if (this.device) this.device.queue.copyExternalImageToTexture({ source }, { texture: r.texture, premultipliedAlpha: false }, [width, height]);
        else if (this.gl) { const gl = this.gl; gl.bindTexture(gl.TEXTURE_2D, r.texture); gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false); gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, false); gl.texSubImage2D(gl.TEXTURE_2D, 0, 0, 0, gl.RGBA, gl.UNSIGNED_BYTE, source); }
        else { r.context.clearRect(0, 0, width, height); r.context.drawImage(source, 0, 0); }
        r.source = source; r.version = version; this.stats.uploads++;
      }
      return r;
    }
    parameters(layer, image, plan) {
      const aspect = image.width / image.height, target = plan.width / plan.height;
      return new Float32Array([layer.x || 0, layer.y || 0, layer.scale ?? 1, (layer.rotation || 0) * Math.PI / 180,
        layer.opacity ?? 1, Math.pow(2, layer.exposure || 0), layer.contrast ?? 1, layer.saturation ?? 1,
        layer.temperature || 0, layer.vignette || 0, Math.min(1, aspect / target), Math.min(1, target / aspect),
        layer.cropLeft || 0, layer.cropRight || 0, layer.cropTop || 0, layer.cropBottom || 0, image.premultiplied ? 1 : 0, target, image.flipY ? 1 : 0, 0]);
    }
    uniform(key, data, views, mix) {
      this.usedUniforms.add(key); let r = this.uniforms.get(key);
      if (!r) { r = { buffer: this.device.createBuffer({ size: data.byteLength, usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST }), data: new Float32Array(data.length).fill(NaN) }; this.uniforms.set(key, r); }
      if (data.some((x, i) => x !== r.data[i])) { r.data.set(data); this.device.queue.writeBuffer(r.buffer, 0, r.data); this.stats.uniformWrites++; }
      if (!r.bind || r.a !== views[0] || r.b !== views[1]) {
        const entries = mix ? [{ binding: 0, resource: views[0] }, { binding: 1, resource: views[1] }, { binding: 2, resource: this.sampler }, { binding: 3, resource: { buffer: r.buffer } }] :
          [{ binding: 0, resource: views[0] }, { binding: 1, resource: this.sampler }, { binding: 2, resource: { buffer: r.buffer } }];
        r.bind = this.device.createBindGroup({ layout: (mix ? this.mixPipeline : this.pipeline).getBindGroupLayout(0), entries }); r.a = views[0]; r.b = views[1];
      }
      return r.bind;
    }
    draw(plan, inputs) {
      if (this.disposed) throw new Error('Compositor is disposed.');
      if (this.lost) throw new Error('Graphics device was lost; reload the editor before exporting.');
      this.used = new Set(); this.usedUniforms = new Set(); this.nodeCount = 0;
      const encoder = this.device?.createCommandEncoder();
      this.render(plan, inputs, this.canvas.width, this.canvas.height, 'root', null, encoder, true);
      if (encoder) this.device.queue.submit([encoder.finish()]);
      for (const map of [this.textures, this.targets]) for (const [key, r] of map) if (!this.used.has(key)) { this.destroy(r); map.delete(key); }
      for (const [key, r] of this.uniforms) if (!this.usedUniforms.has(key)) { r.buffer.destroy(); this.uniforms.delete(key); }
      this.stats.frames++; this.stats.bytes = this.bytes;
    }
    render(plan, inputs, width, height, path, target, encoder, opaque) {
      const commands = [];
      for (const input of inputs) {
        if (++this.nodeCount > 4096) throw new RangeError('Expanded render graph exceeds 4096 nodes.');
        const layer = input.layer, key = path + '/' + layer.clipId;
        if (input.transition) {
          const from = this.texture(key + '/from-target', width, height, true), to = this.texture(key + '/to-target', width, height, true);
          this.render(plan, input.transition.from, width, height, key + '/from', from, encoder, false);
          this.render(plan, input.transition.to, width, height, key + '/to', to, encoder, false);
          const kind = kinds[layer.transition.kind]; if (kind === undefined) throw new Error('Unsupported visual transition: ' + layer.transition.kind);
          const data = new Float32Array([layer.transition.progress, kind, 0, 0]);
          commands.push({ mix: true, from, to, data, bind: this.device ? this.uniform(key + '/mix', data, [from.view, to.view], true) : null });
        } else {
          let image;
          if (input.nested) {
            const nested = layer.nested, fit = Math.min(width / nested.width, height / nested.height);
            const nw = Math.max(2, Math.round(nested.width * fit)), nh = Math.max(2, Math.round(nested.height * fit));
            image = this.texture(key + '/nested-target', nw, nh, true);
            this.render(nested, input.nested, nw, nh, key + '/nested', image, encoder, true);
          } else image = this.external(key + '/source', input);
          if (!image) continue;
          const data = this.parameters(layer, image, plan);
          commands.push({ mix: false, image, data, bind: this.device ? this.uniform(key + '/normal', data, [image.view], false) : null });
        }
      }
      this.stats.passes++;
      if (this.device) {
        const pass = encoder.beginRenderPass({ colorAttachments: [{ view: target ? target.view : this.context.getCurrentTexture().createView(), clearValue: { r: 0, g: 0, b: 0, a: opaque ? 1 : 0 }, loadOp: 'clear', storeOp: 'store' }] });
        for (const c of commands) { pass.setPipeline(c.mix ? this.mixPipeline : this.pipeline); pass.setBindGroup(0, c.bind); pass.draw(6); } pass.end();
      } else if (this.gl) {
        const gl = this.gl; gl.bindFramebuffer(gl.FRAMEBUFFER, target?.fbo || null); gl.viewport(0, 0, width, height); gl.clearColor(0, 0, 0, opaque ? 1 : 0); gl.clear(gl.COLOR_BUFFER_BIT);
        gl.enable(gl.BLEND); gl.blendFuncSeparate(gl.ONE, gl.ONE_MINUS_SRC_ALPHA, gl.ONE, gl.ONE_MINUS_SRC_ALPHA);
        for (const c of commands) {
          const p = c.mix ? this.mixProgram : this.normalProgram, u = p.locations; gl.useProgram(p.program);
          if (c.mix) {
            gl.activeTexture(gl.TEXTURE0); gl.bindTexture(gl.TEXTURE_2D, c.from.texture); gl.uniform1i(u.a, 0);
            gl.activeTexture(gl.TEXTURE1); gl.bindTexture(gl.TEXTURE_2D, c.to.texture); gl.uniform1i(u.b, 1); gl.uniform4fv(u.settings, c.data);
          } else {
            gl.activeTexture(gl.TEXTURE0); gl.bindTexture(gl.TEXTURE_2D, c.image.texture); gl.uniform1i(u.image, 0);
            gl.uniform4fv(u.tr, c.data.subarray(0, 4)); gl.uniform4fv(u.gr, c.data.subarray(4, 8)); gl.uniform4fv(u.ex, c.data.subarray(8, 12)); gl.uniform4fv(u.crop, c.data.subarray(12, 16)); gl.uniform4fv(u.opts, c.data.subarray(16, 20));
          }
          gl.drawArrays(gl.TRIANGLES, 0, 6);
        }
      } else {
        const ctx = target?.context || this.ctx2d; ctx.clearRect(0, 0, width, height); if (opaque) { ctx.fillStyle = '#000'; ctx.fillRect(0, 0, width, height); }
        for (const c of commands) this.drawCanvas(ctx, c, width, height);
      }
    }
    drawCanvas(ctx, c, w, h) {
      ctx.save();
      if (c.mix) {
        const t = c.data[0], kind = c.data[1];
        if (kind >= 3) {
          const cut = (kind === 3 ? t : 1 - t) * w;
          const part = (image, left, right) => { ctx.save(); ctx.beginPath(); ctx.rect(left, 0, right - left, h); ctx.clip(); ctx.drawImage(image.canvas, 0, 0, w, h); ctx.restore(); };
          if (kind === 3) { part(c.to, 0, cut); part(c.from, cut, w); } else { part(c.from, 0, cut); part(c.to, cut, w); }
        } else {
          const scratch = document.createElement('canvas'); scratch.width = w; scratch.height = h; const s = scratch.getContext('2d'); s.globalCompositeOperation = 'lighter';
          if (kind === 0) { s.globalAlpha = 1 - t; s.drawImage(c.from.canvas, 0, 0, w, h); s.globalAlpha = t; s.drawImage(c.to.canvas, 0, 0, w, h); }
          else { const a = t < .5 ? 1 - t * 2 : t * 2 - 1; s.globalAlpha = a; s.drawImage(t < .5 ? c.from.canvas : c.to.canvas, 0, 0, w, h); s.globalAlpha = 1 - a; s.fillStyle = kind === 1 ? '#000' : '#fff'; s.fillRect(0, 0, w, h); }
          ctx.drawImage(scratch, 0, 0);
        }
      } else {
        const p = c.data, dw = w * p[10], dh = h * p[11]; ctx.translate(w * (.5 + p[0]), h * (.5 + p[1])); ctx.rotate(p[3]); ctx.scale(p[2], p[2]); ctx.globalAlpha = p[4];
        ctx.filter = `brightness(${p[5]}) contrast(${p[6]}) saturate(${p[7]})`; ctx.beginPath(); ctx.rect(-dw / 2 + dw * p[12], -dh / 2 + dh * p[14], dw * (1 - p[12] - p[13]), dh * (1 - p[14] - p[15])); ctx.clip(); ctx.drawImage(c.image.canvas, -dw / 2, -dh / 2, dw, dh);
      }
      ctx.restore();
    }
    dispose() {
      if (this.disposed) return; this.disposed = true;
      for (const map of [this.textures, this.targets]) { for (const r of map.values()) this.destroy(r); map.clear(); }
      for (const r of this.uniforms.values()) r.buffer.destroy(); this.uniforms.clear();
      if (this.device) { this.context.unconfigure(); this.device.destroy(); }
      if (this.gl) { this.gl.deleteProgram(this.normalProgram.program); this.gl.deleteProgram(this.mixProgram.program); }
      this.adapter = this.gpu = null; this.stats.bytes = 0;
    }
  }
  ns.Compositor = Compositor; ns.wgsl = wgsl; ns.mixWgsl = mixWgsl;
})(globalThis);
