/* MIT. Offline fixed-cadence WebCodecs export and sample-addressed stereo mixing. */
(function (g) {
  'use strict';
  const E = g.VideoSpaceOffline = {};
  const clamp = (x, lo, hi) => Math.min(hi, Math.max(lo, x));
  const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
  let active;
  E.cancel = () => active?.abort();
  E.mix = function (project, pcm, firstSample, frameCount, sampleRate = 48000) {
    const data = new Float32Array(frameCount * 2), fps = project.frameRate.numerator / project.frameRate.denominator;
    const solo = project.tracks.some(t => t.kind === 'Audio' && t.solo), at = g.VideoSpaceExport.at;
    for (const track of project.tracks) {
      if (track.kind !== 'Audio' || track.muted || (solo && !track.solo)) continue;
      for (const clip of track.clips) {
        if (!clip.enabled) continue;
        const start = Math.max(0, Math.ceil(clip.start / fps * sampleRate - firstSample));
        const end = Math.min(frameCount, Math.ceil((clip.start + clip.duration) / fps * sampleRate - firstSample));
        if (start >= end) continue;
        const source = pcm.get(clip.assetId), asset = project.assets.find(a => a.id === clip.assetId), e = clip.effects;
        if (!source && asset.source !== 'tone') throw new Error('Audio is offline or cannot be decoded: ' + asset.name);
        const pan = clamp(e.pan, -1, 1), lpan = Math.sqrt(1 - pan), rpan = Math.sqrt(1 + pan);
        for (let i = start; i < end; i++) {
          const relative = (firstSample + i) / sampleRate - clip.start / fps, local = Math.floor(relative * fps + 1e-9);
          const t = clip.sourceIn + relative * clip.speed;
          let fade = 1;
          if (e.fadeIn > 0) fade *= clamp(local / e.fadeIn, 0, 1);
          if (e.fadeOut > 0) fade *= clamp((clip.duration - 1 - local) / e.fadeOut, 0, 1);
          const gain = clamp(at(e.gain, local) * track.gain * fade, 0, 16);
          let l, r;
          if (source) {
            const x = t * source.sampleRate, k = Math.floor(x), fraction = x - k;
            const sample = channel => {
              if (k < 0 || k >= source.length) return 0;
              const samples = source.getChannelData(Math.min(channel, source.numberOfChannels - 1));
              return samples[k] * (1 - fraction) + samples[Math.min(k + 1, source.length - 1)] * fraction;
            };
            l = sample(0); r = sample(1);
          } else l = r = (Math.sin(t * 2 * Math.PI * 110) + .5 * Math.sin(t * 2 * Math.PI * 164.81) + .25 * Math.sin(t * 2 * Math.PI * 220)) * .15 * (.75 + .25 * Math.sin(t * .7));
          data[i] += l * gain * lpan; data[frameCount + i] += r * gain * rpan;
        }
      }
    }
    for (let i = 0; i < data.length; i++) data[i] = clamp(data[i], -1, 1);
    return data;
  };
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
  E.inputs = async (plan, context, signal) => {
    const inputs = [];
    for (const layer of plan.layers) inputs.push({ layer, source: await sourceReady(layer, context, signal) });
    if (plan.captions.length) inputs.push(...g.VideoSpaceMedia.inputs({ ...plan, layers: [] }, context, false, 1));
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
      const solo = project.tracks.some(t => t.kind === 'Audio' && t.solo);
      const audioAssets = new Set(project.tracks.filter(t => t.kind === 'Audio' && !t.muted && (!solo || t.solo)).flatMap(t => t.clips.filter(c => c.enabled && c.start < to && c.start + c.duration > from).map(c => c.assetId)));
      decoderContext = new OfflineAudioContext(2, 1, 48000);
      let decodedBytes = 0;
      for (const id of audioAssets) {
        const asset = project.assets.find(a => a.id === id);
        if (asset.source === 'tone') continue;
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
        const plan = g.VideoSpaceExport.evaluate(project, from + i), inputs = await E.inputs(plan, 'offline', signal);
        renderer.draw(plan, inputs);
        if (renderer.lost) throw new Error('Graphics device was lost during export.');
        if (renderer.device) await renderer.device.queue.onSubmittedWorkDone();
        const timestamp = Math.round(i / fps * 1e6), end = Math.round((i + 1) / fps * 1e6);
        const frame = new VideoFrame(renderer.canvas, { timestamp, duration: end - timestamp });
        try { video.encode(frame, { keyFrame: i % Math.max(1, Math.round(fps * 2)) === 0 }); } finally { frame.close(); }
        const sampleTarget = Math.min(totalSamples, Math.round((i + 1) / fps * 48000));
        while (encodedSamples < sampleTarget) {
          const n = Math.min(960, totalSamples - encodedSamples), data = E.mix(project, pcm, firstSample + encodedSamples, n);
          const block = new AudioData({ format: 'f32-planar', sampleRate: 48000, numberOfFrames: n, numberOfChannels: 2, timestamp: Math.round(encodedSamples / 48000 * 1e6), data });
          try { audio.encode(block); } finally { block.close(); }
          encodedSamples += n;
        }
        // Do not flush Opus mid-stream: that pads a partial codec packet. Preserve
        // packetization state and use bounded queue backpressure instead.
        const deadline = performance.now() + 60000;
        while (video.encodeQueueSize >= 8 || audio.encodeQueueSize >= 16) {
          signal.throwIfAborted();
          if (performance.now() > deadline) throw new Error('Encoder queue did not make progress.');
          await sleep(1);
        }
        if (i % 4 === 0) { M.emit('progress', `Offline export · ${i + 1}/${count} frames · ${Math.floor((i + 1) / count * 100)}%`); await sleep(0); }
      }
      await video.flush(); await audio.flush();
      if (failure) throw failure;
      signal.throwIfAborted();
      const blob = mux.finalize();
      M.downloadBlob(blob, (project.name || 'VideoSpace').replace(/[\\/:*?"<>|]/g, '-') + '.webm');
      M.diagnostics.exports++;
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
