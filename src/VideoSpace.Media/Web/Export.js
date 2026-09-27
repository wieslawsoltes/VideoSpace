/* VideoSpace MIT. Real-time WebM export uses the same composition/grade implementation as preview. */
(function (global) {
  'use strict';
  const E = global.VideoSpaceExport = global.VideoSpaceExport || {};
  let current;
  const clamp = (x, a, b) => Math.max(a, Math.min(b, x));
  function at(value, frame) {
    if (!value) return 0; const keys = value.keys || [];
    if (!keys.length) return value.value;
    if (frame <= keys[0].frame) return keys[0].value;
    for (let i = 1; i < keys.length; i++) { const r = keys[i], l = keys[i - 1]; if (frame > r.frame) continue; if (l.interpolation === 'Hold') return frame === r.frame ? r.value : l.value; let t = (frame - l.frame) / (r.frame - l.frame); if (l.interpolation === 'Smooth') t = t * t * (3 - 2 * t); return l.value + (r.value - l.value) * t; }
    return keys.at(-1).value;
  }
  E.evaluate = (project, frame) => {
    const fps = project.frameRate.numerator / project.frameRate.denominator, layers = [], audio = []; const solo = project.tracks.some(t => t.kind === 'Audio' && t.solo), assets = new Map(project.assets.map(a => [a.id, a]));
    for (const track of project.tracks) {
      if (track.muted || (track.kind === 'Audio' && solo && !track.solo)) continue;
      for (const clip of track.clips) {
        if (!clip.enabled || frame < clip.start || frame >= clip.start + clip.duration) continue;
        const a = assets.get(clip.assetId); if (!a) throw new Error('Missing asset reference');
        const local = frame - clip.start, e = clip.effects;
        let fade = 1; if (e.fadeIn > 0) fade *= clamp(local / e.fadeIn, 0, 1); if (e.fadeOut > 0) fade *= clamp((clip.duration - 1 - local) / e.fadeOut, 0, 1);
        const sourceTime = clip.sourceIn + local / fps * clip.speed;
        if (track.kind === 'Audio') audio.push({ clipId: clip.id, assetId: a.id, source: a.source, sourceTime, speed: clip.speed, gain: clamp(at(e.gain, local) * fade * track.gain, 0, 16), pan: e.pan });
        else layers.push({ clipId: clip.id, assetId: a.id, kind: a.kind, source: a.source, text: a.text, color: a.color, sourceTime, speed: clip.speed, x: at(e.x, local), y: at(e.y, local), scale: clamp(at(e.scale, local), .01, 20), rotation: at(e.rotation, local), opacity: clamp(at(e.opacity, local) * fade, 0, 1), exposure: clamp(at(e.exposure, local), -5, 5), contrast: clamp(at(e.contrast, local), 0, 3), saturation: clamp(at(e.saturation, local), 0, 3), temperature: clamp(at(e.temperature, local), -1, 1), vignette: clamp(at(e.vignette, local), 0, 1), cropLeft: e.cropLeft, cropRight: e.cropRight, cropTop: e.cropTop, cropBottom: e.cropBottom });
      }
    }
    return { frame, seconds: frame / fps, width: project.width, height: project.height, fps, layers, audio, captions: project.captions.filter(c => frame >= c.start && frame < c.end).map(c => c.text) };
  };
  E.at = at;
  function sleep(ms) { return new Promise(resolve => setTimeout(resolve, ms)); }
  async function prepare(plan, context) {
    for (const input of global.VideoSpaceMedia.inputs(plan, context, false, 1)) {
      const source = input.source;
      if (source instanceof HTMLVideoElement && source.seeking) await global.VideoSpaceMedia.waitFor(source, 'seeked').catch(() => {});
    }
  }
  E.run = async (project, height = 720) => {
    const M = global.VideoSpaceMedia;
    if (current) { M.emit('error', 'An export is already running.'); return; }
    if (typeof MediaRecorder === 'undefined' || !HTMLCanvasElement.prototype.captureStream) { M.emit('error', 'This browser does not support canvas video recording.'); return; }
    const mime = ['video/webm;codecs=vp9,opus', 'video/webm;codecs=vp8,opus', 'video/webm'].find(t => MediaRecorder.isTypeSupported(t));
    if (!mime) { M.emit('error', 'This browser cannot encode WebM. Use a browser with WebM MediaRecorder support.'); return; }
    let compositor, recorder, stream, destination, canvas; const chunks = []; current = { cancelled: false }; const state = current;
    try {
      const fps = project.frameRate.numerator / project.frameRate.denominator;
      const duration = Math.max(1, ...project.tracks.flatMap(t => t.clips.map(c => c.start + c.duration)));
      const from = clamp(project.inPoint || 0, 0, duration - 1), to = clamp(project.outPoint ?? duration, from + 1, duration), seconds = (to - from) / fps;
      if (seconds > 600) throw new Error('This initial real-time export is limited to 10 minutes. Use sequence In/Out to export sections.');
      for (const t of project.tracks) for (const clip of t.clips) { const asset = project.assets.find(a => a.id === clip.assetId); if (clip.enabled && !t.muted && clip.start < to && clip.start + clip.duration > from && !['Generator', 'Title'].includes(asset.kind) && asset.source !== 'tone' && !M.assetAvailable(asset.id)) throw new Error('Relink offline media before export: ' + asset.name); }
      canvas = document.createElement('canvas'); canvas.height = clamp(height, 180, 1080); canvas.width = Math.round(canvas.height * project.width / project.height / 2) * 2; canvas.style.cssText = 'position:fixed;left:-20000px;top:0;pointer-events:none'; document.body.append(canvas);
      compositor = new global.VideoSpaceGPU.Compositor(canvas, M.report); await compositor.initialize(); canvas = compositor.canvas;
      const context = M.audioContext(); await context.resume(); destination = context.createMediaStreamDestination();
      const initial = E.evaluate(project, from); M.inputs(initial, 'export', false, 1); await sleep(150); await prepare(initial, 'export'); compositor.draw(initial, M.inputs(initial, 'export', false, 1));
      stream = canvas.captureStream(Math.min(60, fps)); for (const track of destination.stream.getAudioTracks()) stream.addTrack(track);
      recorder = new MediaRecorder(stream, { mimeType: mime, videoBitsPerSecond: canvas.height >= 1080 ? 12000000 : 6000000, audioBitsPerSecond: 192000 });
      let byteCount = 0, recordingError;
      recorder.ondataavailable = e => { if (e.data.size) { chunks.push(e.data); byteCount += e.data.size; if (byteCount > 512 * 1024 * 1024) { state.cancelled = true; recordingError = new Error('Export exceeded the 512 MB in-memory recording limit.'); } } };
      recorder.onerror = e => { recordingError = e.error || new Error('Recorder failed'); state.cancelled = true; };
      const stopped = new Promise(resolve => recorder.addEventListener('stop', resolve, { once: true })); recorder.start(1000); const start = performance.now(); let lastProgress = -1;
      while (!state.cancelled) {
        if (document.hidden) throw new Error('Export stopped because the page was hidden. Keep the tab visible during real-time export.');
        const elapsed = (performance.now() - start) / 1000; if (elapsed >= seconds) break;
        const frame = Math.min(to - 1, from + Math.floor(elapsed * fps)), plan = E.evaluate(project, frame);
        compositor.draw(plan, M.inputs(plan, 'export', true, 1)); M.syncAudio(plan, true, 1, destination, 'export');
        const progress = Math.floor(elapsed / seconds * 100); if (progress !== lastProgress) { M.emit('progress', `Exporting ${progress}% · ${mime} · real-time`); lastProgress = progress; }
        await sleep(Math.max(4, 1000 / Math.min(60, fps)));
      }
      M.stopAudio('export'); if (recorder.state !== 'inactive') recorder.stop(); await stopped;
      if (recordingError) throw recordingError;
      if (state.cancelled) { M.emit('exportDone', 'Export cancelled.'); return; }
      const blob = new Blob(chunks, { type: mime }); if (blob.size < 100) throw new Error('The browser produced an empty recording.');
      M.downloadBlob(blob, (project.name || 'VideoSpace').replace(/[\\/:*?"<>|]/g, '-') + '.webm'); M.diagnostics.exports++; M.emit('exportDone', `Exported ${(blob.size / 1048576).toFixed(1)} MB WebM. Real-time recording may contain timing drift; inspect output before delivery.`);
    } catch (error) { M.emit('exportDone', 'Export failed: ' + error.message); }
    finally { if (recorder?.state === 'recording') recorder.stop(); M.stopAudio('export'); M.releaseContext('export'); for (const track of stream?.getTracks() || []) track.stop(); for (const track of destination?.stream.getTracks() || []) track.stop(); compositor?.dispose(); canvas?.remove(); chunks.length = 0; current = null; }
  };
  const media = global.VideoSpaceMedia = global.VideoSpaceMedia || {};
  media.exportVideo = (...args) => E.run(...args);
  media.cancelExport = () => { if (current) current.cancelled = true; };
})(globalThis);
