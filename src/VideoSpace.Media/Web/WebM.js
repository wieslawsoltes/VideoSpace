/* MIT. Bounded VP8/VP9 + Opus WebM muxer. Packet times are integer microseconds. */
(function (g) {
  'use strict';
  const utf8 = s => new TextEncoder().encode(s);
  const join = parts => { const result = new Uint8Array(parts.reduce((n, p) => n + p.length, 0)); let i = 0; for (const p of parts) { result.set(p, i); i += p.length; } return result; };
  function uint(value, width = 0) {
    let v = BigInt(value);
    if (v < 0n) throw new RangeError('Negative unsigned EBML value');
    if (!width) { width = 1; while (v >= (1n << BigInt(width * 8))) width++; }
    if (width > 8 || v >= (1n << BigInt(width * 8))) throw new RangeError('EBML integer overflow');
    const a = new Uint8Array(width);
    for (let i = width - 1; i >= 0; i--) { a[i] = Number(v & 255n); v >>= 8n; }
    return a;
  }
  function size(n) {
    let width = 1;
    while (BigInt(n) >= (1n << BigInt(7 * width)) - 1n) width++;
    return uint(BigInt(n) | (1n << BigInt(7 * width)), width);
  }
  const element = (id, bytes) => join([uint(id), size(bytes.length), bytes]);
  const number = (id, n) => element(id, uint(n));
  const text = (id, s) => element(id, utf8(s));
  const master = (id, parts) => element(id, join(parts));
  const real = (id, n) => { const b = new Uint8Array(8); new DataView(b.buffer).setFloat64(0, n); return element(id, b); };
  class Muxer {
    constructor({ width, height, fps, durationUs, codec = 'V_VP9', audio = true, limit = 256 * 1024 * 1024 }) {
      if (![width, height, fps, durationUs].every(Number.isFinite) || width < 1 || height < 1 || fps <= 0 || durationUs <= 0) throw new RangeError('Invalid WebM configuration');
      if (!['V_VP9', 'V_VP8'].includes(codec)) throw new TypeError('Only VP8/VP9 is supported');
      this.config = { width, height, fps, durationUs, codec, audio };
      this.limit = limit; this.bytes = 0; this.packets = []; this.closed = false; this.opusHead = null;
    }
    add(track, chunk, metadata) {
      if (this.closed) throw new Error('Muxer is finalized');
      if (track !== 1 && track !== 2) throw new RangeError('Invalid track');
      if (!Number.isSafeInteger(chunk.timestamp) || !Number.isFinite(chunk.duration ?? 0)) throw new RangeError('Invalid packet time');
      this.bytes += chunk.byteLength;
      if (this.bytes > this.limit || this.packets.length >= 200000) throw new RangeError('Export exceeds the muxer budget; export a shorter In/Out range');
      const data = new Uint8Array(chunk.byteLength); chunk.copyTo(data);
      this.packets.push({ track, time: chunk.timestamp, duration: chunk.duration || 0, key: chunk.type === 'key', data });
      if (track === 2 && metadata?.decoderConfig?.description) this.opusHead = new Uint8Array(metadata.decoderConfig.description).slice();
    }
    finalize() {
      if (this.closed) throw new Error('Muxer is finalized');
      this.closed = true;
      const c = this.config;
      if (!this.packets.some(p => p.track === 1)) throw new Error('Encoder produced no video');
      if (c.audio && !this.packets.some(p => p.track === 2)) throw new Error('Encoder produced no audio');
      const header = master(0x1A45DFA3, [number(0x4286, 1), number(0x42F7, 1), number(0x42F2, 4), number(0x42F3, 8), text(0x4282, 'webm'), number(0x4287, 4), number(0x4285, 2)]);
      const info = master(0x1549A966, [number(0x2AD7B1, 1000), text(0x4D80, 'VideoSpace'), text(0x5741, 'VideoSpace'), real(0x4489, c.durationUs)]);
      const tracks = [master(0xAE, [number(0xD7, 1), number(0x73C5, 1), number(0x83, 1), number(0x9C, 0), text(0x86, c.codec), number(0x23E383, Math.round(1e9 / c.fps)), master(0xE0, [number(0xB0, c.width), number(0xBA, c.height)])])];
      let delayUs = 0, audioOrigin = 0;
      if (c.audio) {
        const head = this.opusHead;
        if (!head || head.length < 19 || new TextDecoder().decode(head.subarray(0, 8)) !== 'OpusHead') throw new Error('Opus encoder omitted a valid codec header');
        delayUs = Math.round(new DataView(head.buffer, head.byteOffset).getUint16(10, true) / 48000 * 1e6);
        audioOrigin = this.packets.reduce((origin, p) => p.track === 2 ? Math.min(origin, p.time) : origin, Infinity);
        tracks.push(master(0xAE, [number(0xD7, 2), number(0x73C5, 2), number(0x83, 2), number(0x9C, 0), text(0x86, 'A_OPUS'), element(0x63A2, head), number(0x56AA, delayUs * 1000), number(0x56BB, 80000000), master(0xE1, [real(0xB5, 48000), number(0x9F, 2)])]));
      }
      const trackData = master(0x1654AE6B, tracks), chunks = [info, trackData], cues = [];
      // The first Opus block starts at zero. CodecDelay moves its decode timestamp
      // backward; Opus pre-skip then places the first audible sample exactly at zero.
      // Adding delay here as well would shift audio and discard valid tail samples.
      for (const p of this.packets) p.time = Math.max(0, p.time - (p.track === 2 ? audioOrigin : 0));
      this.packets.sort((a, b) => a.time - b.time || a.track - b.track);
      let blocks = [], clusterStart = -1, clusterOffset = info.length + trackData.length, offset = clusterOffset;
      const flush = () => {
        if (!blocks.length) return;
        const chunk = master(0x1F43B675, [number(0xE7, clusterStart), ...blocks]);
        chunks.push(chunk); offset += chunk.length; blocks = [];
      };
      for (const p of this.packets) {
        if (clusterStart < 0 || p.time - clusterStart > 30000 || (p.track === 1 && p.key && p.time !== clusterStart)) { flush(); clusterStart = p.time; clusterOffset = offset; }
        const prefix = new Uint8Array(4); prefix[0] = 0x80 | p.track;
        new DataView(prefix.buffer).setInt16(1, p.time - clusterStart); prefix[3] = p.key ? 0x80 : 0;
        const discard = p.track === 2 ? Math.max(0, Math.round((p.time + p.duration - (c.durationUs + delayUs)) * 1000)) : 0;
        if (discard) { prefix[3] = 0; blocks.push(master(0xA0, [element(0xA1, join([prefix, p.data])), element(0x75A2, uint(discard, 8))])); }
        else blocks.push(element(0xA3, join([prefix, p.data])));
        if (p.track === 1 && p.key) cues.push(master(0xBB, [number(0xB3, p.time), master(0xB7, [number(0xF7, 1), number(0xF1, clusterOffset)])]));
      }
      flush(); chunks.push(master(0x1C53BB6B, cues));
      const total = chunks.reduce((n, p) => n + p.length, 0); this.packets = [];
      return new Blob([header, uint(0x18538067), size(total), ...chunks], { type: 'video/webm' });
    }
  }
  g.VideoSpaceWebM = { Muxer, uint, size };
})(globalThis);
