using VideoSpace.Core;
using VideoSpace.Effects;

namespace VideoSpace.Audio;

/// <summary>Revision-scoped interval-indexed audio graph. Nested time maps, camera cuts,
/// gain, pan and transitions are evaluated at the sample clock. Do not mutate its
/// source project while rendering; decoded PCM remains caller-owned.</summary>
public sealed class PreparedAudioMixer
{
    private sealed record Stage(TimelineClip Clip, double Fps, double Scale, double Offset,
        double TrackGain, IndexedTransition? Incoming, IndexedTransition? Outgoing,
        MediaAsset? Camera = null, int CameraIndex = -1);
    private sealed record Voice(MediaAsset Asset, double Start, double End,
        double SourceScale, double SourceOffset, Stage[] Stages);
    private readonly Voice[] _voices;
    private readonly double[] _maxEnd;
    public IReadOnlyList<MediaAsset> Sources { get; }
    public int VoiceCount => _voices.Length;
    public PreparedAudioMixer(VideoProject project)
    {
        ProjectValidation.Validate(project);
        var voices = new List<Voice>();
        var indices = new Dictionary<VideoProject, ProjectIndex>(ReferenceEqualityComparer.Instance);
        void Visit(VideoProject p, double scale, double offset, double from, double to, Stage[] parents, int depth)
        {
            if (depth > ProjectValidation.MaximumNestingDepth) throw new InvalidDataException("Audio graph exceeds nesting limit.");
            if (!indices.TryGetValue(p, out var index)) indices.Add(p, index = new(p));
            bool solo = p.Tracks.Any(t => t.Kind == TrackKind.Audio && t.Solo);
            foreach (var ti in index.Tracks)
            {
                var track = ti.Track;
                if (track.Kind != TrackKind.Audio || track.Muted || solo && !track.Solo) continue;
                var incoming = ti.Transitions.ToDictionary(t => t.Right.Id);
                var outgoing = ti.Transitions.ToDictionary(t => t.Left.Id);
                foreach (var clip in ti.Clips)
                {
                    if (!clip.Enabled) continue;
                    incoming.TryGetValue(clip.Id, out var head); outgoing.TryGetValue(clip.Id, out var tail);
                    double start = Math.Max(from, (Math.Min(clip.Start, head?.Range.Start ?? clip.Start) / p.FrameRate.Value - offset) / scale);
                    double end = Math.Min(to, (Math.Max(clip.End, tail?.Range.End ?? clip.End) / p.FrameRate.Value - offset) / scale);
                    if (start >= end) continue;
                    var asset = index.Assets[clip.AssetId];
                    var stage = new Stage(clip, p.FrameRate.Value, scale, offset, track.Gain, head, tail);
                    Stage[] chain = [..parents, stage];
                    double nextScale = scale * clip.Speed, nextOffset = (offset - clip.Start / p.FrameRate.Value) * clip.Speed + clip.SourceIn;
                    if (asset.Kind == MediaKind.Sequence) Visit(asset.Sequence!, nextScale, nextOffset, start, end, chain, depth + 1);
                    else if (asset.Kind == MediaKind.Multicam)
                    {
                        for (int i = 0; i < asset.Angles.Count; i++)
                        {
                            if (!asset.AudioFollowsVideo && i != asset.AudioAngle) continue;
                            var camera = asset.Angles[i]; var source = index.Assets[camera.AssetId];
                            if (!source.HasAudio) continue;
                            var cameraChain = (Stage[])chain.Clone();
                            cameraChain[^1] = stage with { Camera = asset.AudioFollowsVideo ? asset : null, CameraIndex = i };
                            voices.Add(new(source, start, end, nextScale, nextOffset + camera.OffsetSeconds, cameraChain));
                        }
                    }
                    else voices.Add(new(asset, start, end, nextScale, nextOffset, chain));
                    if (voices.Count > 100000) throw new InvalidDataException("Expanded audio graph exceeds 100,000 voices.");
                }
            }
        }
        Visit(project, 1, 0, 0, 86400, [], 0);
        _voices = voices.OrderBy(v => v.Start).ToArray();
        _maxEnd = new double[Math.Max(1, _voices.Length * 4)];
        double Build(int node, int lo, int hi)
        {
            if (lo >= hi) return 0;
            return _maxEnd[node] = hi - lo == 1 ? _voices[lo].End : Math.Max(Build(node * 2 + 1, lo, (lo + hi) / 2), Build(node * 2 + 2, (lo + hi) / 2, hi));
        }
        Build(0, 0, _voices.Length);
        Sources = _voices.Select(v => v.Asset).DistinctBy(a => a.Id).ToArray();
    }
    public void Mix(IReadOnlyDictionary<string, PcmAudio> sources, long firstSample, int sampleRate, Span<float> stereo)
    {
        if (firstSample < 0 || sampleRate is < 8000 or > 192000 || stereo.Length % 2 != 0) throw new ArgumentException("Invalid PCM format or output range.");
        stereo.Clear();
        double from = firstSample / (double)sampleRate, to = (firstSample + stereo.Length / 2d) / sampleRate;
        MixNode(0, 0, _voices.Length, from, to, sources, firstSample, sampleRate, stereo);
        for (int i = 0; i < stereo.Length; i++) stereo[i] = Math.Clamp(stereo[i], -1, 1);
    }
    private void MixNode(int node, int lo, int hi, double from, double to,
        IReadOnlyDictionary<string, PcmAudio> sources, long firstSample, int rate, Span<float> stereo)
    {
        if (lo >= hi || _maxEnd[node] <= from || _voices[lo].Start >= to) return;
        if (hi - lo != 1)
        {
            int mid = (lo + hi) / 2;
            MixNode(node * 2 + 1, lo, mid, from, to, sources, firstSample, rate, stereo);
            MixNode(node * 2 + 2, mid, hi, from, to, sources, firstSample, rate, stereo); return;
        }
        var voice = _voices[lo];
        int begin = (int)Math.Clamp(Math.Ceiling(voice.Start * rate - firstSample - 1e-7), 0, stereo.Length / 2);
        int end = (int)Math.Clamp(Math.Ceiling(voice.End * rate - firstSample - 1e-7), 0, stereo.Length / 2);
        if (end <= begin) return;
        sources.TryGetValue(voice.Asset.Id, out var pcm);
        if (pcm is null && voice.Asset.Source != "tone") throw new InvalidDataException("Audio is offline or undecoded: " + voice.Asset.Name);
        for (int i = begin; i < end; i++)
        {
            double time = (firstSample + i) / (double)rate, lg = 1, rg = 1;
            foreach (var s in voice.Stages)
            {
                double frame = (time * s.Scale + s.Offset) * s.Fps;
                double local = Math.Clamp(frame - s.Clip.Start, 0, s.Clip.Duration - 1);
                if (s.Camera is not null && (int)s.Clip.CameraAngle.At(local) != s.CameraIndex) { lg = rg = 0; break; }
                double weight = 1;
                if (s.Incoming is { } head && head.Range.Contains(frame))
                {
                    double progress = head.Range.Progress(frame);
                    weight = head.Definition.Kind == TransitionKind.EqualPowerAudio ? Math.Sin(progress * Math.PI / 2) : progress;
                }
                else if (s.Outgoing is { } tail && tail.Range.Contains(frame))
                {
                    double progress = tail.Range.Progress(frame);
                    weight = tail.Definition.Kind == TransitionKind.EqualPowerAudio ? Math.Cos(progress * Math.PI / 2) : 1 - progress;
                }
                double gain = Math.Clamp(s.Clip.Effects.Gain.At(local) * s.TrackGain * FramePlanner.Envelope(s.Clip, local) * weight, 0, 16);
                lg *= gain * Math.Sqrt(1 - s.Clip.Effects.Pan); rg *= gain * Math.Sqrt(1 + s.Clip.Effects.Pan);
            }
            double sourceTime = time * voice.SourceScale + voice.SourceOffset;
            float left = pcm?.At(sourceTime, 0) ?? AudioMixer.Tone(sourceTime);
            float right = pcm?.At(sourceTime, 1) ?? left;
            stereo[i * 2] += (float)(left * lg); stereo[i * 2 + 1] += (float)(right * rg);
        }
    }
}
