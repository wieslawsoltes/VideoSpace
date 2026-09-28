/* MIT. Offline fixed-cadence WebCodecs export and sample-addressed stereo mixing. */
(function (g) {
  'use strict';
  const E = g.VideoSpaceOffline = {};
  const clamp = (x, lo, hi) => Math.min(hi, Math.max(lo, x));
  const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
  let active;
  E.cancel = () => active?.abort();
  E.mix = (project, pcm, firstSample, frameCount, sampleRate = 48000) => g.VideoSpaceExport.prepareAudio(project).mix(pcm, firstSample, frameCount, sampleRate);
  async function sourceReady(layer, context, signal) {
    const M = g.VideoSpaceMedia, deadline = performance.now() + 20000;
    if (!['Video', 'Audio'].includes(layer.kind)) {
      if (layer.kind === 'Image' && !M.assetAvailable(layer.assetId)) throw new Error('Relink offline image: ' + layer.assetId);
      return M.getSource(layer, context + ':' + layer.clipId, false, 1);
    }
    if (!M.assetAvailable(layer.assetId)) throw new Error('Relink offline media: ' + layer.assetId);
    for (;;) {
      signal.throwIfAborted();
      const source = M.getSource(layer, context + ':' + layer.clipId, false, 1);
      if (source instanceof HTMLVideoElement && source.readyState >= 2 && !source.seeking) return source;
      if (performance.now() > deadline) throw new Error('Decoder did not provide the requested frame: ' + layer.assetId);
      await sleep(8);
    }
  }
  E.inputs = async (plan, context, signal, budget = { nodes: 0 }) => {
    const inputs = [];
    for (const layer of plan.layers) {
      if (++budget.nodes > 4096) throw new Error('Expanded render graph exceeds 4096 nodes.');
      if (['Generator', 'Title'].includes(layer.kind) && (budget.generated = (budget.generated || 0) + 1) > 48) throw new Error('Active generated layers exceed the canvas memory budget.');
      if (layer.transition) {
        const t = layer.transition;
        const from = await E.inputs({ ...plan, layers: [t.from], captions: [] }, context + ':from:' + layer.clipId, signal, budget);
        const to = await E.inputs({ ...plan, layers: [t.to], captions: [] }, context + ':to:' + layer.clipId, signal, budget);
        inputs.push({ layer, transition: { from, to } });
      } else if (layer.nested) inputs.push({ layer, nested: await E.inputs(layer.nested, context + ':nested:' + layer.clipId, signal, budget) });
      else inputs.push({ layer, source: await sourceReady(layer, context, signal) });
    }
    if (plan.captions.length) inputs.push(...g.VideoSpaceMedia.inputs({ ...plan, layers: [] }, context, false, 1, budget));
    return inputs;
  };
  E.run = async function (project, height = 720) {
    const M = g.VideoSpaceMedia;
    if (active) { M.emit('error', 'An offline export is already running.'); return; }
    const controller = active = new AbortController(), signal = controller.signal;
    let renderer, video, audio, decoderContext, failure;
    const pcm = new Map();
    try {
      if (!g.VideoEncoder || !g.VideoFrame || !g.AudioEncoder || !g.AudioData) throw new Error('This browser does not expose the required WebCodecs encoders. Project and still-image export remain available.');
      const fps = project.frameRate.numerator / project.frameRate.denominator;
      let duration = 1;
      for (const t of project.tracks) for (const c of t.clips) duration = Math.max(duration, c.start + c.duration);
      const from = clamp(project.inPoint || 0, 0, duration - 1), to = clamp(project.outPoint ?? duration, from + 1, duration), count = to - from;
      const durationUs = Math.round(count / fps * 1e6);
      if (!Number.isInteger(count) || durationUs > 600e6) throw new Error('Offline export is limited to a ten-minute In/Out range.');
      height = Math.round(clamp(height, 180, 2160) / 2) * 2;
      const width = Math.round(height * project.width / project.height / 2) * 2;
      if (width < 2 || width > 8192) throw new Error('Export width is outside the supported range.');
      let config;
      for (const codec of ['vp09.00.10.08', 'vp8']) {
        const candidate = { codec, width, height, bitrate: height >= 1080 ? 12000000 : 6000000, framerate: fps, latencyMode: 'quality' };
        if ((await VideoEncoder.isConfigSupported(candidate)).supported) { config = candidate; break; }
      }
      if (!config) throw new Error('No VP9/VP8 WebCodecs encoder is available.');
      const audioConfig = { codec: 'opus', sampleRate: 48000, numberOfChannels: 2, bitrate: 192000 };
      if (!(await AudioEncoder.isConfigSupported(audioConfig)).supported) throw new Error('No Opus encoder is available.');
      const planner = g.VideoSpaceExport.prepare(project), mixer = g.VideoSpaceExport.prepareAudio(project);
      decoderContext = new OfflineAudioContext(2, 1, 48000);
      let decodedBytes = 0;
      for (const asset of mixer.sources) {
        const id = asset.id; if (asset.source === 'tone') continue;
        M.emit('progress', 'Decoding audio · ' + asset.name); signal.throwIfAborted();
        if (asset.durationSeconds * 48000 * 8 > 256 * 1024 * 1024 || asset.byteLength > 128 * 1024 * 1024) throw new Error('Source exceeds the offline PCM decode budget: ' + asset.name);
        const element = await sourceReady({ clipId: 'audio-' + id, assetId: id, kind: 'Audio', sourceTime: 0, speed: 1 }, 'offline', signal);
        const response = await fetch(element.currentSrc || element.src, { signal });
        if (!response.ok) throw new Error('Could not read local audio source: ' + asset.name);
        const bytes = await response.arrayBuffer();
        if (bytes.byteLength > 128 * 1024 * 1024) throw new Error('Compressed audio exceeds the decode budget.');
        const buffer = await decoderContext.decodeAudioData(bytes);
        decodedBytes += buffer.length * buffer.numberOfChannels * 4;
        if (decodedBytes > 256 * 1024 * 1024) throw new Error('Decoded sources exceed the 256 MB audio budget.');
        pcm.set(id, buffer);
      }
      const canvas = document.createElement('canvas'); canvas.width = width; canvas.height = height;
      renderer = new g.VideoSpaceGPU.Compositor(canvas, M.report); await renderer.initialize();
      if (renderer.backend.startsWith('Canvas')) throw new Error('Offline export requires WebGPU or WebGL2 to retain all grading effects.');
      const mux = new g.VideoSpaceWebM.Muxer({ width, height, fps, durationUs, codec: config.codec === 'vp8' ? 'V_VP8' : 'V_VP9' });
      const encoderError = error => { failure = error; controller.abort(error); };
      video = new VideoEncoder({ output: (chunk, metadata) => { try { mux.add(1, chunk, metadata); } catch (e) { encoderError(e); } }, error: encoderError }); video.configure(config);
      audio = new AudioEncoder({ output: (chunk, metadata) => { try { mux.add(2, chunk, metadata); } catch (e) { encoderError(e); } }, error: encoderError }); audio.configure(audioConfig);
      const firstSample = Math.round(from / fps * 48000), totalSamples = Math.round(count / fps * 48000);
      let encodedSamples = 0;
      for (let i = 0; i < count; i++) {
        signal.throwIfAborted();
        const plan = planner.evaluate(from + i), inputs = await E.inputs(plan, 'offline', signal);
        renderer.draw(plan, inputs);
        if (renderer.lost) throw new Error('Graphics device was lost during export.');
        if (renderer.device) await renderer.device.queue.onSubmittedWorkDone();
        const timestamp = Math.round(i / fps * 1e6), end = Math.round((i + 1) / fps * 1e6);
        const frame = new VideoFrame(renderer.canvas, { timestamp, duration: end - timestamp });
        try { video.encode(frame, { keyFrame: i % Math.max(1, Math.round(fps * 2)) === 0 }); } finally { frame.close(); }
        const sampleTarget = Math.min(totalSamples, Math.round((i + 1) / fps * 48000));
        while (encodedSamples < sampleTarget) {
          const n = Math.min(960, totalSamples - encodedSamples), data = mixer.mix(pcm, firstSample + encodedSamples, n);
          const block = new AudioData({ format: 'f32-planar', sampleRate: 48000, numberOfFrames: n, numberOfChannels: 2, timestamp: Math.round(encodedSamples / 48000 * 1e6), data });
          try { audio.encode(block); } finally { block.close(); }
          encodedSamples += n;
        }
        const deadline = performance.now() + 60000;
        while (video.encodeQueueSize >= 8 || audio.encodeQueueSize >= 16) {
          signal.throwIfAborted(); if (performance.now() > deadline) throw new Error('Encoder queue did not make progress.'); await sleep(1);
        }
        if (i % 4 === 0) { M.emit('progress', `Offline export · ${i + 1}/${count} frames · ${Math.floor((i + 1) / count * 100)}%`); await sleep(0); }
      }
      await video.flush(); await audio.flush(); if (failure) throw failure; signal.throwIfAborted();
      const blob = mux.finalize();
      M.downloadBlob(blob, (project.name || 'VideoSpace').replace(/[\\/:*?"<>|]/g, '-') + '.webm'); M.diagnostics.exports++;
      M.diagnostics.lastExport = { mode: 'offline', backend: renderer.backend, frames: count, samples: totalSamples, durationUs, width, height, bytes: blob.size, codec: config.codec };
      M.emit('exportDone', `Exported ${count} frames and ${totalSamples} stereo samples · ${(blob.size / 1048576).toFixed(1)} MB WebM`);
    } catch (error) {
      M.emit('exportDone', failure ? 'Export failed: ' + failure.message : signal.aborted ? 'Export cancelled.' : 'Export failed: ' + error.message);
    } finally {
      for (const encoder of [video, audio]) if (encoder && encoder.state !== 'closed') encoder.close();
      renderer?.dispose(); M.releaseContext('offline'); pcm.clear(); decoderContext = null; active = null;
    }
  };
})(globalThis);
