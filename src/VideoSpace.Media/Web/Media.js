/* VideoSpace MIT. Local-only media storage, decoding and presentation. No upload endpoints. */
(function (global) {
  'use strict';
  const M = global.VideoSpaceMedia = global.VideoSpaceMedia || {};
  const queue = [], assets = new Map(), views = new Map(), sessions = new Map(), generated = new Map();
  let database, initialized = false, hidden = false, animation, project, saveTimer, audioContext, master, analyser, meterData;
  const audioNodes = new Map();
  let contentEpoch = 0;
  const generatedLimit = 32;
  // Eviction releases cache ownership; it never mutates canvases retained by an in-flight graph.
  function sourceCanvas(key) {
    let canvas = generated.get(key);
    if (canvas) generated.delete(key);
    else { if (generated.size >= generatedLimit) generated.delete(generated.keys().next().value); canvas = makeCanvas(); }
    generated.set(key, canvas); M.diagnostics.generatedCacheBytes = generated.size * 960 * 540 * 4;
    return canvas;
  }
  const activeSessions = new Map();
  const dirty = () => { contentEpoch++; };
  global.addEventListener('resize', dirty);
  function stopSession(session) {
    const v = session.element; if (session.callback && v.cancelVideoFrameCallback) v.cancelVideoFrameCallback(session.callback);
    session.disposed = true; v.pause(); v.removeAttribute('src'); v.load();
  }
  function disposeAudio(node) {
    node.element?.pause(); if (node.element) { node.element.removeAttribute('src'); node.element.load(); }
    for (const o of node.oscillators || []) o.stop(); node.source?.disconnect(); node.gain.disconnect(); node.pan.disconnect();
  }
  M.diagnostics = { backend: {}, meter: [0, 0], imports: 0, exports: 0, errors: [], frames: 0, generatedFrames: 0, compositor: {} };
  M.emit = (type, text = '', extra = {}) => { queue.push({ type, text, ...extra }); if (queue.length > 200) queue.shift(); };
  M.report = text => { if (M.diagnostics.errors.at(-1) === text) return; M.diagnostics.errors.push(text); if (M.diagnostics.errors.length > 30) M.diagnostics.errors.shift(); M.emit('status', text); };
  M.drain = () => JSON.stringify(queue.splice(0));
  const request = r => new Promise((resolve, reject) => { r.onsuccess = () => resolve(r.result); r.onerror = () => reject(r.error); });
  async function dbGet(store, key) { return database ? request(database.transaction(store).objectStore(store).get(key)) : null; }
  async function dbPut(store, value, key) {
    if (!database) throw new Error('Browser storage is unavailable. Save a project file before closing.');
    await new Promise((resolve, reject) => { const tx = database.transaction(store, 'readwrite'); tx.objectStore(store).put(value, key); tx.oncomplete = resolve; tx.onerror = () => reject(tx.error); tx.onabort = () => reject(tx.error || new Error('Storage transaction aborted')); });
  }
  function waitFor(element, event, timeout = 15000) {
    return new Promise((resolve, reject) => { const timer = setTimeout(() => end(new Error('Media loading timed out')), timeout); const done = () => end(); const fail = () => end(new Error(element.error?.message || 'Unsupported or damaged media')); function end(error) { clearTimeout(timer); element.removeEventListener(event, done); element.removeEventListener('error', fail); error ? reject(error) : resolve(); } element.addEventListener(event, done, { once: true }); element.addEventListener('error', fail, { once: true }); });
  }
  function kindFor(file) {
    if (file.type.startsWith('image/') || /\.(png|jpe?g|webp|bmp|gif)$/i.test(file.name)) return 'Image';
    if (file.type.startsWith('audio/') || /\.(wav|mp3|ogg|m4a|aac|flac)$/i.test(file.name)) return 'Audio';
    return 'Video';
  }
  function makeCanvas(width = 960, height = 540) { const c = document.createElement('canvas'); c.width = width; c.height = height; return c; }
  function generatedSource(layer, key) {
    const canvas = sourceCanvas(key);
    const stamp = JSON.stringify([layer.kind, layer.source, layer.text, layer.kind === 'Generator' ? layer.sourceTime : 0]);
    if (canvas.__vsStamp === stamp) return canvas;
    canvas.__vsStamp = stamp; canvas.__vsVersion = (canvas.__vsVersion || 0) + 1; M.diagnostics.generatedFrames++;
    const c = canvas.getContext('2d'), w = canvas.width, h = canvas.height; c.clearRect(0, 0, w, h);
    if (layer.kind === 'Title') { c.fillStyle = '#F6F1E5'; c.font = `${h * .14}px Inter, Arial, sans-serif`; c.textAlign = 'center'; c.fillText(layer.text || '', w / 2, h * .55); return canvas; }
    if (layer.kind === 'Captions') {
      c.font = `${h * .035}px Inter, Arial, sans-serif`; c.textAlign = 'center'; const tw = Math.min(w * .9, c.measureText(layer.text).width); c.fillStyle = '#000B'; c.fillRect((w - tw) / 2 - 10, h * .88, tw + 20, h * .064); c.fillStyle = 'white'; c.fillText(layer.text, w / 2, h * .925, w * .9); return canvas;
    }
    const scene = layer.source, time = layer.sourceTime;
    const top = scene === 'dunes' ? '#26353C' : scene === 'coast' ? '#738C91' : '#899FA8', bottom = scene === 'dunes' ? '#E4AB78' : scene === 'coast' ? '#D8DDD0' : '#D7D4C4';
    const gradient = c.createLinearGradient(w / 2, 0, w / 2, h); gradient.addColorStop(0, top); gradient.addColorStop(1, bottom); c.fillStyle = gradient; c.fillRect(0, 0, w, h);
    const drift = Math.sin(time * .08) * .025; c.fillStyle = scene === 'dunes' ? '#F7DAB0' : '#DEDCCC'; c.beginPath(); c.arc(w * .74, h * .29, h * .065, 0, Math.PI * 2); c.fill();
    const colors = scene === 'dunes' ? ['#BA8D69','#A27250','#D3A276','#805E49','#352E2C'] : scene === 'coast' ? ['#758D8E','#4D757A','#335B61','#29484B','#1D3337'] : ['#7C9397','#60787E','#465E66','#324A50','#1B333A'];
    for (let layerIndex = 0; layerIndex < colors.length; layerIndex++) {
      c.beginPath(); c.moveTo(0, h);
      for (let i = 0; i <= 64; i++) { const x = i / 64; const y = scene === 'dunes' ? Math.sin(x * 4.6 + layerIndex * 1.5 + drift) * .1 + Math.sin(x * 2.1 + layerIndex) * .08 : scene === 'coast' ? Math.sin(x * 5 + layerIndex * 1.4 + drift) * .07 + Math.cos(x * 12 + layerIndex) * .022 : Math.abs(Math.sin(x * 5.5 + layerIndex * 1.6 + drift)) * -.19 + Math.sin(x * 19 + layerIndex) * .017; c.lineTo(x * w, h * (.56 + layerIndex * .10 + y)); }
      c.lineTo(w, h); c.closePath(); c.fillStyle = colors[layerIndex]; c.fill();
    }
    if (scene === 'coast') { c.strokeStyle = '#B4C7BF'; c.lineWidth = h * .0016; for (let j = 0; j < 14; j++) { const y = h * (.66 + j * .016), x = w * (.15 + j * .017 + Math.sin(time * .5 + j) * .012); c.beginPath(); c.moveTo(x, y); c.lineTo(x + w * (.25 - j * .006), y - h * .007); c.stroke(); } }
    return canvas;
  }
  function offlineSource(name) {
    const canvas = sourceCanvas('offline:' + name);
    if (canvas.__vsVersion) return canvas; canvas.__vsVersion = 1;
    const c = canvas.getContext('2d'); c.fillStyle = '#25282B'; c.fillRect(0, 0, canvas.width, canvas.height); c.fillStyle = '#B7BCC2'; c.font = '18px Arial'; c.textAlign = 'center'; c.fillText('MEDIA OFFLINE — IMPORT TO RELINK', 480, 262); c.font = '13px Arial'; c.fillText(String(name).slice(0, 90), 480, 295); return canvas;
  }
  M.getSource = (layer, key, playing, rate = 1) => {
    if (['Generator', 'Title', 'Captions'].includes(layer.kind)) return generatedSource(layer, key);
    const asset = assets.get(layer.assetId); if (!asset) return offlineSource(layer.assetId);
    if (layer.kind === 'Image') return asset.image || offlineSource(asset.asset.name);
    let session = sessions.get(key);
    if (!session || session.assetId !== layer.assetId) {
      if (session) stopSession(session);
      const video = document.createElement('video'); video.src = asset.url; video.muted = true; video.playsInline = true; video.preload = 'auto'; video.addEventListener('error', () => M.report('Cannot decode ' + asset.asset.name));
      session = { assetId: layer.assetId, element: video }; sessions.set(key, session);
      const changed = () => { video.__vsVersion = (video.__vsVersion || 0) + 1; dirty(); };
      for (const event of ['loadedmetadata', 'loadeddata', 'seeked', 'error']) video.addEventListener(event, changed);
      const next = () => { if (!session.disposed) { changed(); session.callback = video.requestVideoFrameCallback(next); } };
      if (video.requestVideoFrameCallback) session.callback = video.requestVideoFrameCallback(next);
    }
    activeSessions.get(key.split(':')[0])?.add(key);
    const v = session.element; session.used = performance.now();
    const target = Math.max(0, Math.min(layer.sourceTime, Number.isFinite(v.duration) ? Math.max(0, v.duration - .001) : layer.sourceTime));
    if (v.readyState >= 1 && !v.seeking && Math.abs(v.currentTime - target) > (playing && rate > 0 ? .16 : .0008)) { try { v.currentTime = target; } catch { } }
    if (playing && rate > 0) { v.playbackRate = Math.max(.0625, Math.min(16, (layer.speed || 1) * rate)); if (v.paused) v.play().catch(() => {}); } else v.pause();
    return v.readyState >= 2 ? v : offlineSource(asset.asset.name);
  };
  M.inputs = (plan, context, playing, rate, budget = { nodes: 0 }) => {
    const result = [];
    for (const layer of plan.layers) {
      if (++budget.nodes > 4096) throw new Error('Expanded composition exceeds 4096 nodes.');
      if (['Generator', 'Title'].includes(layer.kind) && (budget.generated = (budget.generated || 0) + 1) > 48) throw new Error('Active generated layers exceed the canvas memory budget.');
      const key = context + ':' + layer.clipId;
      if (layer.transition) {
        const t = layer.transition;
        result.push({ layer, transition: {
          from: M.inputs({ ...plan, layers: [t.from], captions: [] }, key + ':from', playing, rate, budget),
          to: M.inputs({ ...plan, layers: [t.to], captions: [] }, key + ':to', playing, rate, budget)
        }});
      } else if (layer.nested) result.push({ layer, nested: M.inputs(layer.nested, key + ':nested', playing, rate * layer.speed, budget) });
      else result.push({ layer, source: M.getSource(layer, key, playing, rate) });
    }
    if (plan.captions?.length) {
      if ((budget.generated = (budget.generated || 0) + 1) > 48) throw new Error('Active generated layers exceed the canvas memory budget.');
      const layer = { clipId: 'captions', kind: 'Captions', text: plan.captions.join('  '), opacity: 1, scale: 1, contrast: 1, saturation: 1 };
      result.push({ layer, source: generatedSource(layer, context + ':captions') });
    }
    return result;
  };
  async function register(file, asset, persist) {
    const old = assets.get(asset.id); if (old) { URL.revokeObjectURL(old.url); old.image?.close(); }
    const item = { file, asset, url: URL.createObjectURL(file) };
    if (asset.kind === 'Image') item.image = await createImageBitmap(file);
    assets.set(asset.id, item); dirty();
    if (persist) { try { await dbPut('assets', { file, asset }, asset.id); } catch (error) { M.emit('error', 'Media is available for this session but could not be persisted: ' + error.message); } }
    return item;
  }
  async function importFiles(files) {
    for (const file of [...files].slice(0, 100)) {
      try {
        if (file.size > 2 * 1024 * 1024 * 1024) throw new Error('Media imports are limited to 2 GB per file.');
        const kind = kindFor(file); const old = project?.assets?.find(a => a.name === file.name && a.byteLength === file.size && !assets.has(a.id));
        const asset = { id: old?.id || crypto.randomUUID().replaceAll('-', ''), name: file.name, bin: 'Imported', kind, durationSeconds: 10, width: 0, height: 0, source: '', text: '', color: kind === 'Audio' ? '#66A58D' : '#8E9CCC', byteLength: file.size, hasAudio: kind !== 'Image', peaks: [] };
        let probe, bitmap, url;
        try {
          if (kind === 'Image') { bitmap = await createImageBitmap(file); asset.width = bitmap.width; asset.height = bitmap.height; }
          else { probe = document.createElement(kind === 'Audio' ? 'audio' : 'video'); probe.preload = 'auto'; probe.muted = true; probe.src = url = URL.createObjectURL(file); await waitFor(probe, 'loadedmetadata'); if (!Number.isFinite(probe.duration) || probe.duration <= 0 || probe.duration > 86400) throw new Error('Unsupported or invalid media duration.'); asset.durationSeconds = probe.duration; asset.width = probe.videoWidth || 0; asset.height = probe.videoHeight || 0; if (kind === 'Video') { probe.currentTime = Math.min(.05, probe.duration / 2); await waitFor(probe, 'seeked').catch(() => {}); } }
          if (asset.width > 16384 || asset.height > 16384) throw new Error('Media dimensions exceed the 16384-pixel limit.');
          let thumbnail = null;
          if (kind !== 'Audio') { const canvas = makeCanvas(192, 108), c = canvas.getContext('2d'); c.fillStyle = '#000'; c.fillRect(0, 0, 192, 108); const src = bitmap || probe; if (bitmap || probe.readyState >= 2) { const fit = Math.min(192 / asset.width, 108 / asset.height); c.drawImage(src, (192 - asset.width * fit) / 2, (108 - asset.height * fit) / 2, asset.width * fit, asset.height * fit); thumbnail = canvas.toDataURL('image/jpeg', .7).split(',')[1]; } }
          if (kind !== 'Image' && file.size <= 32 * 1024 * 1024) {
            let context;
            try { context = new AudioContext(); const audio = await context.decodeAudioData(await file.arrayBuffer()); const data = audio.getChannelData(0), count = 512; asset.peaks = Array.from({ length: count }, (_, b) => { let peak = 0; const start = Math.floor(b * data.length / count), end = Math.floor((b + 1) * data.length / count); for (let i = start; i < end; i++) peak = Math.max(peak, Math.abs(data[i])); return peak; }); } catch { } finally { await context?.close().catch(() => {}); }
          }
          await register(file, asset, true); M.emit('asset', '', { asset, bytes: thumbnail }); M.diagnostics.imports++;
        } finally { bitmap?.close(); if (probe) { probe.pause(); probe.removeAttribute('src'); probe.load(); } if (url) URL.revokeObjectURL(url); }
      } catch (error) { M.emit('error', file.name + ': ' + error.message); }
    }
  }
  function picker(accept, multiple, action) {
    const input = document.createElement('input'); input.type = 'file'; input.accept = accept; input.multiple = multiple; input.id = 'videospace-file-picker'; input.style.display = 'none'; document.body.append(input);
    input.addEventListener('change', async () => { try { await action(input.files || []); } catch (error) { M.emit('error', error.message); } finally { input.remove(); } }, { once: true }); input.addEventListener('cancel', () => input.remove(), { once: true }); input.click();
  }
  M.pickMedia = () => picker('video/*,audio/*,image/*,.mp4,.mov,.webm,.mkv,.wav,.mp3,.m4a', true, importFiles);
  M.pickProject = () => picker('.videospace,.json,.srt', false, async files => { if (!files.length) return; const f = files[0]; if (f.size > 32 * 1024 * 1024) throw new Error('Project is too large.'); M.emit(f.name.toLowerCase().endsWith('.srt') ? 'captions' : 'project', await f.text(), { name: f.name }); });
  M.setProject = p => { project = p; };
  M.autosave = text => { clearTimeout(saveTimer); saveTimer = setTimeout(() => dbPut('project', text, 'last').catch(error => M.emit('error', 'Autosave failed: ' + error.message)), 500); };
  M.downloadBlob = (blob, name) => { const url = URL.createObjectURL(blob), link = document.createElement('a'); link.href = url; link.download = name; document.body.append(link); link.click(); link.remove(); setTimeout(() => URL.revokeObjectURL(url), 60000); };
  M.downloadText = (text, name) => M.downloadBlob(new Blob([text], { type: 'application/octet-stream' }), name);
  M.downloadBase64 = (base64, name) => { const data = atob(base64); const bytes = Uint8Array.from(data, c => c.charCodeAt(0)); M.downloadBlob(new Blob([bytes]), name); };
  M.unlockAudio = () => {
    if (!audioContext) {
      audioContext = new AudioContext(); master = audioContext.createGain(); master.gain.value = .8; analyser = audioContext.createAnalyser(); analyser.fftSize = 256; meterData = new Float32Array(analyser.fftSize); master.connect(analyser); analyser.connect(audioContext.destination);
    }
    audioContext.resume().catch(error => M.report(error.message));
  };
  M.audioContext = () => { M.unlockAudio(); return audioContext; };
  M.syncAudio = (plan, playing, rate = 1, destination = null, prefix = 'preview') => {
    if (!audioContext) return;
    const active = new Set(), now = audioContext.currentTime;
    for (const layer of plan.audio || []) {
      const key = prefix + ':' + layer.clipId; active.add(key); let node = audioNodes.get(key);
      if (node && node.assetId !== layer.assetId) { disposeAudio(node); audioNodes.delete(key); node = null; }
      if (!node && layer.source !== 'tone' && !assets.has(layer.assetId)) continue;
      if (!node) {
        const gain = audioContext.createGain(), pan = audioContext.createStereoPanner(); gain.gain.value = 0; gain.connect(pan); pan.connect(destination || master);
        node = { gain, pan, prefix, assetId: layer.assetId };
        if (layer.source === 'tone') { const a = audioContext.createOscillator(), b = audioContext.createOscillator(); a.type = 'sine'; b.type = 'sine'; a.frequency.value = 110; b.frequency.value = 164.81; const level = audioContext.createGain(); level.gain.value = .12; a.connect(level); b.connect(level); level.connect(gain); a.start(); b.start(); node.oscillators = [a, b]; }
        else {
          const asset = assets.get(layer.assetId);
          const element = document.createElement('audio'); element.src = asset.url; element.preload = 'auto'; const source = audioContext.createMediaElementSource(element); source.connect(gain); node.element = element; node.source = source;
        }
        audioNodes.set(key, node);
      }
      node.used = performance.now(); node.gain.gain.setTargetAtTime(playing && rate > 0 ? Math.min(4, Math.max(0, layer.gain)) : 0, now, .008); node.pan.pan.setValueAtTime(Math.max(-1, Math.min(1, layer.pan)), now);
      const e = node.element;
      if (e) {
        if (e.readyState >= 1 && !e.seeking && Math.abs(e.currentTime - layer.sourceTime) > .15) e.currentTime = Math.max(0, Math.min(layer.sourceTime, e.duration - .001));
        e.playbackRate = Math.max(.0625, Math.min(16, (layer.speed || 1) * Math.max(.0625, rate)));
        if (playing && rate > 0) { if (e.paused) e.play().catch(() => {}); } else e.pause();
      }
    }
    for (const [key, node] of audioNodes) if (node.prefix === prefix && !active.has(key)) { node.gain.gain.setTargetAtTime(0, now, .008); node.element?.pause(); if (performance.now() - (node.used || 0) > 10000) { disposeAudio(node); audioNodes.delete(key); } }
    if (analyser) { analyser.getFloatTimeDomainData(meterData); const rms = Math.sqrt(meterData.reduce((s, v) => s + v * v, 0) / meterData.length); M.diagnostics.meter = [rms, rms]; }
  };
  M.stopAudio = prefix => { for (const [key, node] of audioNodes) if (!prefix || node.prefix === prefix) { disposeAudio(node); audioNodes.delete(key); } };
  M.releaseContext = prefix => {
    for (const [key, session] of sessions) if (key.startsWith(prefix + ':')) { stopSession(session); sessions.delete(key); }
    for (const key of generated.keys()) if (key.startsWith(prefix + ':')) generated.delete(key);
  };
  async function newView(id) {
    const canvas = makeCanvas(); canvas.id = 'videospace-' + id; canvas.setAttribute('aria-hidden', 'true'); canvas.style.cssText = 'position:fixed;pointer-events:none;z-index:20;background:black;display:none'; document.body.append(canvas);
    const view = { id, compositor: new global.VideoSpaceGPU.Compositor(canvas, M.report), ready: false, state: null, revision: 0, painted: -1, epoch: -1 }; views.set(id, view);
    try { await view.compositor.initialize(); view.ready = true; M.diagnostics.backend[id] = view.compositor.backend; M.emit('backend', view.compositor.backend); } catch (error) { M.emit('error', error.message); }
    return view;
  }
  M.present = state => {
    let view = views.get(state.id); if (!view) { newView(state.id).then(v => { if (!v.state) { v.state = state; v.revision++; } dirty(); }); return; }
    view.state = state; view.revision++;
  };
  function tick() {
    if (document.hidden) { animation = requestAnimationFrame(tick); return; }
    for (const view of views.values()) {
      const s = view.state; if (!s || !view.ready) continue;
      const canvas = view.compositor.canvas;
      const invisible = hidden || document.documentElement.hasAttribute('data-videospace-' + view.id + '-hidden');
      canvas.style.display = invisible ? 'none' : 'block';
      if (invisible) { view.painted = -1; continue; }
      const ratio = s.plan.width / s.plan.height; let w = s.width, h = s.height; if (w / h > ratio) w = h * ratio; else h = w / ratio;
      const x = s.x + (s.width - w) / 2, y = s.y + (s.height - h) / 2;
      const dpr = Math.min(devicePixelRatio || 1, 2), pw = Math.max(2, Math.round(w * dpr)), ph = Math.max(2, Math.round(h * dpr));
      const resized = canvas.width !== pw || canvas.height !== ph;
      if (view.painted === view.revision && view.epoch === contentEpoch && !resized) continue;
      canvas.style.left = x + 'px'; canvas.style.top = y + 'px'; canvas.style.width = w + 'px'; canvas.style.height = h + 'px';
      if (resized) { canvas.width = pw; canvas.height = ph; }
      try {
        activeSessions.set(view.id, new Set());
        view.compositor.draw(s.plan, M.inputs(s.plan, view.id, s.playing, s.rate)); M.diagnostics.frames++;
        view.painted = view.revision; view.epoch = contentEpoch;
        M.diagnostics.compositor[view.id] = { ...view.compositor.stats };
        if (view.id === 'program') M.syncAudio(s.plan, s.playing, s.rate);
      } catch (error) { M.report('Composition: ' + error.message); view.painted = view.revision; view.epoch = contentEpoch; }
    }
    const now = performance.now();
    for (const [key, session] of sessions) if (now - (session.used || 0) > 10000 && !activeSessions.get(key.split(':')[0])?.has(key)) { stopSession(session); sessions.delete(key); }
    animation = requestAnimationFrame(tick);
  }
  M.hide = value => {
    hidden = value; dirty();
    for (const view of views.values()) view.compositor.canvas.style.display = value ? 'none' : 'block';
    if (value) { for (const session of sessions.values()) session.element.pause(); M.stopAudio('preview'); }
  };
  M.exportStill = async () => {
    const view = views.get('program'); if (!view?.state || !view.ready) return;
    let compositor;
    try {
      const plan = view.state.plan, canvas = makeCanvas(plan.width, plan.height);
      compositor = new global.VideoSpaceGPU.Compositor(canvas, M.report); await compositor.initialize();
      const inputs = await global.VideoSpaceOffline.inputs(plan, 'still', new AbortController().signal);
      compositor.draw(plan, inputs); if (compositor.device) await compositor.device.queue.onSubmittedWorkDone();
      const blob = await new Promise(resolve => compositor.canvas.toBlob(resolve, 'image/png'));
      if (!blob) throw new Error('Still encoding failed'); M.downloadBlob(blob, 'VideoSpace-frame.png');
    } catch (error) { M.emit('error', error.message); }
    finally { compositor?.dispose(); M.releaseContext('still'); }
  };
  M.init = async () => {
    if (initialized) return; initialized = true;
    try {
      database = await new Promise((resolve, reject) => { const r = indexedDB.open('VideoSpace', 1); r.onupgradeneeded = () => { r.result.createObjectStore('assets'); r.result.createObjectStore('project'); }; r.onsuccess = () => resolve(r.result); r.onerror = () => reject(r.error); });
      const saved = await dbGet('project', 'last'); if (saved) M.emit('project', saved);
      const stored = await request(database.transaction('assets').objectStore('assets').getAll()); for (const entry of stored) { try { await register(entry.file, entry.asset, false); } catch (error) { M.report('Stored media: ' + error.message); } }
    } catch (error) { M.emit('error', 'Browser recovery unavailable: ' + error.message); }
    M.emit('ready'); animation = requestAnimationFrame(tick);
    document.addEventListener('dragover', e => { if (e.dataTransfer?.files?.length || [...(e.dataTransfer?.types || [])].includes('Files')) e.preventDefault(); });
    document.addEventListener('drop', e => { if (e.dataTransfer?.files?.length) { e.preventDefault(); importFiles(e.dataTransfer.files); } });
    document.addEventListener('visibilitychange', () => { if (document.hidden) { for (const session of sessions.values()) session.element.pause(); M.stopAudio('preview'); M.emit('pause', 'Playback paused while the page is hidden.'); } });
  };
  M.dispose = () => { cancelAnimationFrame(animation); clearTimeout(saveTimer); for (const view of views.values()) { view.compositor.dispose(); view.compositor.canvas.remove(); } for (const a of assets.values()) { URL.revokeObjectURL(a.url); a.image?.close(); } M.releaseContext('program'); M.releaseContext('source'); generated.clear(); M.stopAudio(); audioContext?.close(); database?.close(); views.clear(); assets.clear(); };
  M.assetAvailable = id => assets.has(id); M.localSource = id => assets.get(id)?.url ?? null; M.waitFor = waitFor;
})(globalThis);
