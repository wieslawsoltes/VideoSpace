using VideoSpace.Core;

namespace VideoSpace.Effects;

/// <summary>O(track count × log clip count + active content) evaluation, prepared once
/// for a validated revision. Holds one memoized result per sequence, not a growing frame cache.</summary>
public sealed class PreparedFramePlanner
{
    private readonly Dictionary<string, PreparedFramePlanner> _children = new(StringComparer.Ordinal);
    private readonly bool _solo;
    private FramePlan? _last;
    private double _lastFrame = double.NaN;
    private bool _lastCaptions;
    public ProjectIndex Index { get; }
    public long Evaluations { get; private set; }
    public long CacheHits { get; private set; }
    public PreparedFramePlanner(VideoProject project) : this(Validate(project)) { }
    public PreparedFramePlanner(ProjectIndex index)
    {
        Index = index;
        _solo = index.Tracks.Any(t => t.Track.Kind == TrackKind.Audio && t.Track.Solo);
        foreach (var a in index.Assets.Values.Where(a => a.Kind == MediaKind.Sequence))
            _children.Add(a.Id, new PreparedFramePlanner(new ProjectIndex(a.Sequence!)));
    }
    private static ProjectIndex Validate(VideoProject p) { ProjectValidation.Validate(p); return new(p); }
    public FramePlan Evaluate(double frame, bool captions = true)
    {
        if (!double.IsFinite(frame)) throw new ArgumentOutOfRangeException(nameof(frame));
        if (_last is not null && frame == _lastFrame && captions == _lastCaptions) { CacheHits++; return _last; }
        var p = Index.Project; var visual = new List<LayerPlan>(); var audio = new List<AudioPlan>();
        foreach (var ti in Index.Tracks)
        {
            var track = ti.Track;
            if (track.Muted || track.Kind == TrackKind.Audio && _solo && !track.Solo) continue;
            if (ti.TransitionAt(frame) is { } transition)
            {
                double progress = transition.Range.Progress(frame);
                if (track.Kind == TrackKind.Audio)
                {
                    double left = transition.Definition.Kind == TransitionKind.EqualPowerAudio ? Math.Cos(progress * Math.PI / 2) : 1 - progress;
                    double right = transition.Definition.Kind == TransitionKind.EqualPowerAudio ? Math.Sin(progress * Math.PI / 2) : progress;
                    AddAudio(audio, transition.Left, track, frame, left); AddAudio(audio, transition.Right, track, frame, right);
                }
                else
                {
                    var from = Layer(transition.Left, frame); var to = Layer(transition.Right, frame);
                    visual.Add(new LayerPlan(transition.Definition.Id, "", "Transition", "", "", "", 0, 1, 0, 0, 1, 0, 1, 0, 1, 1, 0, 0, 0, 0, 0, 0)
                    { Transition = new(transition.Definition.Kind.ToString(), progress, from, to) });
                }
                continue;
            }
            if (ti.At(frame) is not { Enabled: true } clip) continue;
            if (track.Kind == TrackKind.Audio) AddAudio(audio, clip, track, frame, 1);
            else visual.Add(Layer(clip, frame));
        }
        var result = new FramePlan((long)Math.Floor(frame), frame / p.FrameRate.Value, p.Width, p.Height, p.FrameRate.Value,
            visual.ToArray(), audio.ToArray(), captions ? p.Captions.Where(c => frame >= c.Start && frame < c.End).Select(c => c.Text).ToArray() : []);
        FramePlanBudget.Validate(result);
        _lastFrame = frame; _lastCaptions = captions; Evaluations++;
        return _last = result;
    }
    public LayerPlan Layer(TimelineClip clip, double frame)
    {
        var p = Index.Project; var asset = Index.Assets[clip.AssetId];
        double local = Math.Clamp(frame - clip.Start, 0, clip.Duration - 1);
        double source = clip.SourceTime(frame, p.FrameRate);
        if (asset.Kind == MediaKind.Multicam)
        {
            int n = (int)clip.CameraAngle.At(local); var camera = asset.Angles[n];
            asset = Index.Assets[camera.AssetId]; source += camera.OffsetSeconds;
        }
        var e = clip.Effects;
        var layer = new LayerPlan(clip.Id, asset.Id, asset.Kind.ToString(), asset.Source, asset.Text, asset.Color, source, clip.Speed,
            e.X.At(local), e.Y.At(local), Math.Clamp(e.Scale.At(local), .01, 20), e.Rotation.At(local),
            clip.Enabled ? Math.Clamp(e.Opacity.At(local) * FramePlanner.Envelope(clip, local), 0, 1) : 0,
            Math.Clamp(e.Exposure.At(local), -5, 5), Math.Clamp(e.Contrast.At(local), 0, 3), Math.Clamp(e.Saturation.At(local), 0, 3),
            Math.Clamp(e.Temperature.At(local), -1, 1), Math.Clamp(e.Vignette.At(local), 0, 1), e.CropLeft, e.CropRight, e.CropTop, e.CropBottom);
        return asset.Kind == MediaKind.Sequence ? layer with { Nested = _children[asset.Id].Evaluate(source * asset.Sequence!.FrameRate.Value) } : layer;
    }
    private void AddAudio(List<AudioPlan> output, TimelineClip clip, Track track, double frame, double weight)
    {
        if (!clip.Enabled) return;
        var p = Index.Project; var asset = Index.Assets[clip.AssetId]; double source = clip.SourceTime(frame, p.FrameRate);
        double local = Math.Clamp(frame - clip.Start, 0, clip.Duration - 1);
        double gain = Math.Clamp(clip.Effects.Gain.At(local) * FramePlanner.Envelope(clip, local) * track.Gain * weight, 0, 16);
        if (asset.Kind == MediaKind.Multicam)
        {
            var camera = asset.Angles[asset.AudioFollowsVideo ? (int)clip.CameraAngle.At(local) : asset.AudioAngle];
            asset = Index.Assets[camera.AssetId]; source += camera.OffsetSeconds;
            if (!asset.HasAudio) return;
        }
        if (asset.Kind != MediaKind.Sequence)
        {
            output.Add(new(clip.Id, asset.Id, asset.Source, source, clip.Speed, gain, clip.Effects.Pan)); return;
        }
        foreach (var child in _children[asset.Id].Evaluate(source * asset.Sequence!.FrameRate.Value).Audio)
        {
            double left = child.Gain * Math.Sqrt(1 - child.Pan) * gain * Math.Sqrt(1 - clip.Effects.Pan);
            double right = child.Gain * Math.Sqrt(1 + child.Pan) * gain * Math.Sqrt(1 + clip.Effects.Pan);
            double power = left * left + right * right;
            output.Add(child with { ClipId = clip.Id + "/" + child.ClipId, Speed = child.Speed * clip.Speed,
                Gain = Math.Sqrt(power / 2), Pan = power > 0 ? (right * right - left * left) / power : 0 });
        }
    }
}
