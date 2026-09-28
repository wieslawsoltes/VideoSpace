/* MIT. Revision-scoped frame and audio graphs. No DOM dependency: shared by preview,
   offline export and independent Node/C# equivalence tests. */
(function (g) {
  'use strict';
  const E = g.VideoSpaceExport = {};
  const clamp = (x, lo, hi) => Math.max(lo, Math.min(hi, x));
  const upper = (a, x) => { let l = 0, h = a.length; while (l < h) { const m = (l + h) >>> 1; if (a[m] <= x) l = m + 1; else h = m; } return l; };
  function at(value, frame) {
    if (!value) return 0;
    frame += value.frameOffset || 0;
    const keys = value.keys || [];
    if (!keys.length) return value.value;
    if (frame <= keys[0].frame) return keys[0].value;
    let lo = 0, hi = keys.length;
    while (lo < hi) { const m = (lo + hi) >>> 1; if (keys[m].frame <= frame) lo = m + 1; else hi = m; }
    if (lo === keys.length) return keys.at(-1).value;
    const l = keys[lo - 1], r = keys[lo];
    if (l.interpolation === 'Hold') return l.value;
    let t = (frame - l.frame) / (r.frame - l.frame);
    if (l.interpolation === 'Smooth') t = t * t * (3 - 2 * t);
    return l.value + (r.value - l.value) * t;
  }
  const envelope = (clip, local) => {
    const e = clip.effects;
    return (e.fadeIn > 0 ? clamp(local / e.fadeIn, 0, 1) : 1) * (e.fadeOut > 0 ? clamp((clip.duration - 1 - local) / e.fadeOut, 0, 1) : 1);
  };
  const progress = (t, frame) => clamp((frame - t.start) / Math.max(1, t.end - t.start - 1), 0, 1);
  const contains = (t, frame) => t && frame >= t.start && frame < t.end;
  const sourceTime = (c, frame, fps) => c.sourceIn + (frame - c.start) / fps * c.speed;
  function trackIndex(track) {
    const clips = [...track.clips].sort((a, b) => a.start - b.start), ids = new Map(clips.map(c => [c.id, c]));
    const transitions = (track.transitions || []).map(definition => {
      const left = ids.get(definition.leftClipId), right = ids.get(definition.rightClipId);
      if (!left || !right || left.start + left.duration !== right.start) throw new Error('Transition has detached endpoints.');
      const before = definition.alignment === 'StartAtCut' ? 0 : definition.alignment === 'EndAtCut' ? definition.duration : Math.floor(definition.duration / 2);
      const start = right.start - before;
      return { definition, left, right, start, end: start + definition.duration };
    }).sort((a, b) => a.start - b.start);
    return { track, clips, starts: clips.map(c => c.start), transitions, transitionStarts: transitions.map(t => t.start) };
  }
  const emptyLayer = (id, kind) => ({ clipId: id, assetId: '', kind, source: '', text: '', color: '', sourceTime: 0, speed: 1, x: 0, y: 0, scale: 1, rotation: 0, opacity: 1, exposure: 0, contrast: 1, saturation: 1, temperature: 0, vignette: 0, cropLeft: 0, cropRight: 0, cropTop: 0, cropBottom: 0 });
  function validatePlan(plan) {
    let nodes = 0;
    const layer = (l, depth) => {
      if (++nodes > 4096 || depth > 24) throw new Error('Expanded composition exceeds 4096 nodes or 24 levels.');
      if (l.nested) visit(l.nested, depth + 1);
      if (l.transition) { layer(l.transition.from, depth + 1); layer(l.transition.to, depth + 1); }
    };
    const visit = (p, depth) => {
      nodes += p.audio.length + p.captions.length;
      if (nodes > 4096) throw new Error('Expanded composition exceeds 4096 nodes.');
      for (const l of p.layers) layer(l, depth);
    };
    visit(plan, 0);
  }
  class PreparedFramePlanner {
    constructor(project, ancestry = new Set()) {
      if (ancestry.size > 8 || ancestry.has(project)) throw new Error('Cyclic or excessively nested project.');
      ancestry.add(project);
      this.project = project; this.fps = project.frameRate.numerator / project.frameRate.denominator;
      this.assets = new Map(project.assets.map(a => [a.id, a])); this.tracks = project.tracks.map(trackIndex);
      this.children = new Map(); this.solo = project.tracks.some(t => t.kind === 'Audio' && t.solo);
      this.evaluations = 0; this.cacheHits = 0;
      for (const a of project.assets) if (a.kind === 'Sequence') this.children.set(a.id, new PreparedFramePlanner(a.sequence, ancestry));
      ancestry.delete(project);
    }
    layer(clip, frame) {
      let asset = this.assets.get(clip.assetId), source = sourceTime(clip, frame, this.fps);
      const local = clamp(frame - clip.start, 0, clip.duration - 1), e = clip.effects;
      if (asset.kind === 'Multicam') {
        const camera = asset.angles[Math.trunc(at(clip.cameraAngle, local))];
        asset = this.assets.get(camera.assetId); source += camera.offsetSeconds;
      }
      const result = { clipId: clip.id, assetId: asset.id, kind: asset.kind, source: asset.source, text: asset.text, color: asset.color,
        sourceTime: source, speed: clip.speed, x: at(e.x, local), y: at(e.y, local), scale: clamp(at(e.scale, local), .01, 20), rotation: at(e.rotation, local),
        opacity: clip.enabled ? clamp(at(e.opacity, local) * envelope(clip, local), 0, 1) : 0,
        exposure: clamp(at(e.exposure, local), -5, 5), contrast: clamp(at(e.contrast, local), 0, 3), saturation: clamp(at(e.saturation, local), 0, 3),
        temperature: clamp(at(e.temperature, local), -1, 1), vignette: clamp(at(e.vignette, local), 0, 1), cropLeft: e.cropLeft, cropRight: e.cropRight, cropTop: e.cropTop, cropBottom: e.cropBottom };
      if (asset.kind === 'Sequence') result.nested = this.children.get(asset.id).evaluate(source * asset.sequence.frameRate.numerator / asset.sequence.frameRate.denominator);
      return result;
    }
    addAudio(output, clip, track, frame, weight) {
      if (!clip.enabled) return;
      let asset = this.assets.get(clip.assetId), source = sourceTime(clip, frame, this.fps);
      const local = clamp(frame - clip.start, 0, clip.duration - 1), e = clip.effects;
      const gain = clamp(at(e.gain, local) * envelope(clip, local) * track.gain * weight, 0, 16);
      if (asset.kind === 'Multicam') {
        const camera = asset.angles[asset.audioFollowsVideo ? Math.trunc(at(clip.cameraAngle, local)) : asset.audioAngle];
        asset = this.assets.get(camera.assetId); source += camera.offsetSeconds;
        if (!asset.hasAudio) return;
      }
      if (asset.kind !== 'Sequence') { output.push({ clipId: clip.id, assetId: asset.id, source: asset.source, sourceTime: source, speed: clip.speed, gain, pan: e.pan }); return; }
      const child = this.children.get(asset.id).evaluate(source * asset.sequence.frameRate.numerator / asset.sequence.frameRate.denominator);
      for (const a of child.audio) {
        const left = a.gain * Math.sqrt(1 - a.pan) * gain * Math.sqrt(1 - e.pan), right = a.gain * Math.sqrt(1 + a.pan) * gain * Math.sqrt(1 + e.pan), power = left * left + right * right;
        output.push({ ...a, clipId: clip.id + '/' + a.clipId, speed: a.speed * clip.speed, gain: Math.sqrt(power / 2), pan: power > 0 ? (right * right - left * left) / power : 0 });
      }
    }
    evaluate(frame, captions = true) {
      if (!Number.isFinite(frame)) throw new RangeError('Invalid frame.');
      if (this.last && frame === this.lastFrame && captions === this.lastCaptions) { this.cacheHits++; return this.last; }
      const p = this.project, layers = [], audio = [];
      for (const ti of this.tracks) {
        const t = ti.track;
        if (t.muted || t.kind === 'Audio' && this.solo && !t.solo) continue;
        const transition = ti.transitions[upper(ti.transitionStarts, frame) - 1];
        if (contains(transition, frame)) {
          const fraction = progress(transition, frame);
          if (t.kind === 'Audio') {
            const power = transition.definition.kind === 'EqualPowerAudio';
            this.addAudio(audio, transition.left, t, frame, power ? Math.cos(fraction * Math.PI / 2) : 1 - fraction);
            this.addAudio(audio, transition.right, t, frame, power ? Math.sin(fraction * Math.PI / 2) : fraction);
          } else layers.push({ ...emptyLayer(transition.definition.id, 'Transition'), transition: { kind: transition.definition.kind, progress: fraction, from: this.layer(transition.left, frame), to: this.layer(transition.right, frame) } });
          continue;
        }
        const clip = ti.clips[upper(ti.starts, frame) - 1];
        if (!clip?.enabled || frame >= clip.start + clip.duration) continue;
        if (t.kind === 'Audio') this.addAudio(audio, clip, t, frame, 1); else layers.push(this.layer(clip, frame));
      }
      const result = { frame: Math.floor(frame), seconds: frame / this.fps, width: p.width, height: p.height, fps: this.fps, layers, audio,
        captions: captions ? p.captions.filter(c => frame >= c.start && frame < c.end).map(c => c.text) : [] };
      validatePlan(result);
      this.lastFrame = frame; this.lastCaptions = captions; this.evaluations++;
      return this.last = result;
    }
  }
  class PreparedAudioMixer {
    constructor(project) {
      const voices = [], indices = new Map(), ancestry = new Set();
      const visit = (p, scale, offset, from, to, parents) => {
        if (ancestry.size > 8 || ancestry.has(p)) throw new Error('Cyclic or excessively nested audio.');
        ancestry.add(p);
        let index = indices.get(p);
        if (!index) { index = { fps: p.frameRate.numerator / p.frameRate.denominator, assets: new Map(p.assets.map(a => [a.id, a])), tracks: p.tracks.map(trackIndex) }; indices.set(p, index); }
        const solo = p.tracks.some(t => t.kind === 'Audio' && t.solo);
        for (const ti of index.tracks) {
          const t = ti.track; if (t.kind !== 'Audio' || t.muted || solo && !t.solo) continue;
          const incoming = new Map(ti.transitions.map(x => [x.right.id, x])), outgoing = new Map(ti.transitions.map(x => [x.left.id, x]));
          for (const c of ti.clips) {
            if (!c.enabled) continue;
            const head = incoming.get(c.id), tail = outgoing.get(c.id), fps = index.fps;
            const start = Math.max(from, (Math.min(c.start, head?.start ?? c.start) / fps - offset) / scale);
            const end = Math.min(to, (Math.max(c.start + c.duration, tail?.end ?? c.start + c.duration) / fps - offset) / scale);
            if (start >= end) continue;
            const asset = index.assets.get(c.assetId), stage = { clip: c, fps, scale, offset, gain: t.gain, head, tail }, chain = [...parents, stage];
            const nextScale = scale * c.speed, nextOffset = (offset - c.start / fps) * c.speed + c.sourceIn;
            if (asset.kind === 'Sequence') visit(asset.sequence, nextScale, nextOffset, start, end, chain);
            else if (asset.kind === 'Multicam') {
              for (let i = 0; i < asset.angles.length; i++) {
                if (!asset.audioFollowsVideo && i !== asset.audioAngle) continue;
                const camera = asset.angles[i], source = index.assets.get(camera.assetId); if (!source.hasAudio) continue;
                const cameraChain = [...parents, { ...stage, camera: asset.audioFollowsVideo ? i : null }];
                voices.push({ asset: source, start, end, scale: nextScale, offset: nextOffset + camera.offsetSeconds, stages: cameraChain });
              }
            } else voices.push({ asset, start, end, scale: nextScale, offset: nextOffset, stages: chain });
            if (voices.length > 100000) throw new Error('Expanded audio graph exceeds 100,000 voices.');
          }
        }
        ancestry.delete(p);
      };
      visit(project, 1, 0, 0, 86400, []);
      this.voices = voices.sort((a, b) => a.start - b.start); this.maxEnd = new Float64Array(Math.max(1, voices.length * 4));
      const build = (node, lo, hi) => {
        if (lo >= hi) return 0;
        return this.maxEnd[node] = hi - lo === 1 ? voices[lo].end : Math.max(build(node * 2 + 1, lo, (lo + hi) >>> 1), build(node * 2 + 2, (lo + hi) >>> 1, hi));
      };
      build(0, 0, voices.length);
      this.sources = [...new Map(voices.map(v => [v.asset.id, v.asset])).values()];
    }
    mix(pcm, firstSample, count, rate = 48000) {
      if (!Number.isSafeInteger(firstSample) || firstSample < 0 || !Number.isInteger(count) || count < 0 || count > 48000 * 600 || !Number.isInteger(rate) || rate < 8000 || rate > 192000) throw new RangeError('Invalid PCM range.');
      const data = new Float32Array(count * 2), from = firstSample / rate, to = (firstSample + count) / rate;
      const visit = (node, lo, hi) => {
        if (lo >= hi || this.maxEnd[node] <= from || this.voices[lo].start >= to) return;
        if (hi - lo !== 1) { const mid = (lo + hi) >>> 1; visit(node * 2 + 1, lo, mid); visit(node * 2 + 2, mid, hi); return; }
        const v = this.voices[lo], begin = clamp(Math.ceil(v.start * rate - firstSample - 1e-7), 0, count), end = clamp(Math.ceil(v.end * rate - firstSample - 1e-7), 0, count);
        if (end <= begin) return;
        const source = pcm.get(v.asset.id);
        if (!source && v.asset.source !== 'tone') throw new Error('Audio is offline or cannot be decoded: ' + v.asset.name);
        const left = source?.getChannelData(0), right = source?.getChannelData(Math.min(1, source.numberOfChannels - 1));
        for (let i = begin; i < end; i++) {
          const time = (firstSample + i) / rate; let lg = 1, rg = 1;
          for (const s of v.stages) {
            const frame = (time * s.scale + s.offset) * s.fps, local = clamp(frame - s.clip.start, 0, s.clip.duration - 1), e = s.clip.effects;
            if (s.camera != null && Math.trunc(at(s.clip.cameraAngle, local)) !== s.camera) { lg = rg = 0; break; }
            let weight = 1;
            if (contains(s.head, frame)) { const t = progress(s.head, frame); weight = s.head.definition.kind === 'EqualPowerAudio' ? Math.sin(t * Math.PI / 2) : t; }
            else if (contains(s.tail, frame)) { const t = progress(s.tail, frame); weight = s.tail.definition.kind === 'EqualPowerAudio' ? Math.cos(t * Math.PI / 2) : 1 - t; }
            const gain = clamp(at(e.gain, local) * s.gain * envelope(s.clip, local) * weight, 0, 16);
            lg *= gain * Math.sqrt(1 - e.pan); rg *= gain * Math.sqrt(1 + e.pan);
          }
          const timeInSource = time * v.scale + v.offset; let l, r;
          if (source) {
            const x = timeInSource * source.sampleRate, k = Math.floor(x), f = x - k;
            if (k < 0 || k >= source.length) l = r = 0;
            else { const next = Math.min(k + 1, source.length - 1); l = left[k] * (1 - f) + left[next] * f; r = right[k] * (1 - f) + right[next] * f; }
          } else l = r = (Math.sin(timeInSource * 2 * Math.PI * 110) + .5 * Math.sin(timeInSource * 2 * Math.PI * 164.81) + .25 * Math.sin(timeInSource * 2 * Math.PI * 220)) * .15 * (.75 + .25 * Math.sin(timeInSource * .7));
          data[i] += l * lg; data[count + i] += r * rg;
        }
      };
      visit(0, 0, this.voices.length);
      for (let i = 0; i < data.length; i++) data[i] = clamp(data[i], -1, 1);
      return data;
    }
  }
  E.validatePlan = validatePlan; E.at = at; E.envelope = envelope; E.PreparedFramePlanner = PreparedFramePlanner; E.PreparedAudioMixer = PreparedAudioMixer;
  E.prepare = project => new PreparedFramePlanner(project);
  E.evaluate = (project, frame, captions = true) => E.prepare(project).evaluate(frame, captions);
  E.prepareAudio = project => new PreparedAudioMixer(project);
  const media = g.VideoSpaceMedia = g.VideoSpaceMedia || {};
  media.exportVideo = (...args) => g.VideoSpaceOffline.run(...args);
  media.cancelExport = () => g.VideoSpaceOffline?.cancel();
})(globalThis);
