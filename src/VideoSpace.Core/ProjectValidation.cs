namespace VideoSpace.Core;

public static class ProjectValidation
{
    public const int MaximumNestingDepth = 8;
    public static void Validate(VideoProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        int clips = 0, assets = 0;
        Validate(project, new HashSet<VideoProject>(ReferenceEqualityComparer.Instance), 0, ref clips, ref assets);
    }
    private static void Validate(VideoProject p, HashSet<VideoProject> ancestors, int depth, ref int clipCount, ref int assetCount)
    {
        if (depth > MaximumNestingDepth || !ancestors.Add(p)) throw new InvalidDataException("Sequence nesting exceeds eight levels or contains a cycle.");
        try
        {
            if (p.SchemaVersion != 1) throw new InvalidDataException("Unsupported project schema.");
            p.FrameRate.Validate();
            if (p.Width is < 16 or > 8192 || p.Height is < 16 or > 8192) throw new InvalidDataException("Sequence dimensions must be 16–8192 pixels.");
            if (p.Assets is null || p.Tracks is null || p.Markers is null || p.Captions is null) throw new InvalidDataException("Project collections cannot be null.");
            assetCount += p.Assets.Count; clipCount += p.Tracks.Sum(t => t?.Clips?.Count ?? 0);
            if (assetCount > 10000 || p.Tracks.Count > 128 || clipCount > 100000 || p.Markers.Count > 100000 || p.Captions.Count > 100000)
                throw new InvalidDataException("Project exceeds safe editing limits.");
            Unique(p.Assets.Select(a => a?.Id)); Unique(p.Tracks.Select(t => t?.Id));
            var assets = p.Assets.ToDictionary(a => a.Id, StringComparer.Ordinal);
            var clipIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var a in p.Assets)
            {
                if (!Enum.IsDefined(a.Kind) || !double.IsFinite(a.DurationSeconds) || a.DurationSeconds <= 0 || a.DurationSeconds > 86400)
                    throw new InvalidDataException("Invalid media kind or duration.");
                if (a.Name is null || a.Text is null || a.Source is null || a.Bin is null || a.Color is null || a.Peaks is null || a.Angles is null)
                    throw new InvalidDataException("Media fields cannot be null.");
                if (a.Width is < 0 or > 32768 || a.Height is < 0 or > 32768 || a.ByteLength < 0) throw new InvalidDataException("Invalid media dimensions or length.");
                if (a.Peaks.Length > 100000 || a.Peaks.Any(x => !float.IsFinite(x))) throw new InvalidDataException("Invalid audio peaks.");
                if (a.Kind == MediaKind.Sequence)
                {
                    if (a.Sequence is null) throw new InvalidDataException("Nested sequence content is missing.");
                    Validate(a.Sequence, ancestors, depth + 1, ref clipCount, ref assetCount);
                }
                else if (a.Sequence is not null) throw new InvalidDataException("Only sequence assets can contain nested projects.");
                if (a.Kind == MediaKind.Multicam)
                {
                    if (a.Angles.Count is < 2 or > 16 || a.AudioAngle < 0 || a.AudioAngle >= a.Angles.Count) throw new InvalidDataException("Multicam requires 2–16 angles and a valid audio angle.");
                    foreach (var angle in a.Angles)
                    {
                        if (angle is null || !assets.TryGetValue(angle.AssetId, out var source) || source.Kind is not (MediaKind.Video or MediaKind.Image or MediaKind.Generator))
                            throw new InvalidDataException("Multicam angles must reference local video, image or generator assets.");
                        if (!double.IsFinite(angle.OffsetSeconds) || angle.OffsetSeconds < 0 || angle.OffsetSeconds + a.DurationSeconds > source.DurationSeconds + 1e-6)
                            throw new InvalidDataException("A camera angle exceeds the synchronized source range.");
                    }
                }
                else if (a.Angles.Count != 0) throw new InvalidDataException("Only multicam assets can declare camera angles.");
            }
            foreach (var track in p.Tracks)
            {
                if (!Enum.IsDefined(track.Kind) || track.Clips is null || track.Transitions is null) throw new InvalidDataException("Invalid track.");
                if (!double.IsFinite(track.Gain) || track.Gain is < 0 or > 16) throw new InvalidDataException("Invalid track gain.");
                var sorted = track.Clips.OrderBy(c => c?.Start).ToArray(); long end = 0;
                var byId = new Dictionary<string, TimelineClip>(StringComparer.Ordinal);
                foreach (var c in sorted)
                {
                    if (c is null || string.IsNullOrWhiteSpace(c.Id) || !clipIds.Add(c.Id)) throw new InvalidDataException("Clip identifiers must be unique and nonempty.");
                    if (!assets.TryGetValue(c.AssetId, out var a)) throw new InvalidDataException("Media reference not found: " + c.AssetId);
                    if (c.Start < 0 || c.Duration < 1 || c.Duration > p.FrameRate.Frames(86400) || c.Start > p.FrameRate.Frames(86400) - c.Duration || c.Start < end)
                        throw new InvalidDataException("Clips on one track must have positive durations and cannot overlap.");
                    if (!double.IsFinite(c.Speed) || c.Speed is < .05 or > 16 || !double.IsFinite(c.SourceIn) || c.SourceIn < 0) throw new InvalidDataException("Invalid source time or speed.");
                    if (FiniteMedia(a) && c.SourceTime(c.End, p.FrameRate) > a.DurationSeconds + .05) throw new InvalidDataException("The edit exceeds available source media.");
                    if (track.Kind == TrackKind.Audio && a.Kind is MediaKind.Image or MediaKind.Title) throw new InvalidDataException("Still images cannot be placed on audio tracks.");
                    if (c.Effects is null || c.CameraAngle is null) throw new InvalidDataException("Clip effects cannot be null.");
                    foreach (var value in c.Effects.Values.Append(c.CameraAngle)) ValidateValue(value);
                    if (a.Kind == MediaKind.Multicam)
                    {
                        bool Angle(double x) => x >= 0 && x < a.Angles.Count && x == Math.Truncate(x);
                        if (!Angle(c.CameraAngle.Value) || c.CameraAngle.Keys.Any(k => !Angle(k.Value) || k.Interpolation != Interpolation.Hold))
                            throw new InvalidDataException("Camera cuts require integer angles and hold interpolation.");
                    }
                    if (c.Effects.FadeIn < 0 || c.Effects.FadeOut < 0 || !double.IsFinite(c.Effects.Pan) || Math.Abs(c.Effects.Pan) > 1) throw new InvalidDataException("Invalid audio or fade parameters.");
                    double[] crop = [c.Effects.CropLeft, c.Effects.CropRight, c.Effects.CropTop, c.Effects.CropBottom];
                    if (crop.Any(x => !double.IsFinite(x) || x < 0 || x > .99) || crop[0] + crop[1] >= 1 || crop[2] + crop[3] >= 1) throw new InvalidDataException("Invalid crop.");
                    byId.Add(c.Id, c); end = c.End;
                }
                if (track.Transitions.Count > sorted.Length) throw new InvalidDataException("Too many transitions.");
                Unique(track.Transitions.Select(t => t?.Id));
                long transitionEnd = -1;
                var ranges = new List<TransitionRange>();
                foreach (var t in track.Transitions)
                {
                    if (!Enum.IsDefined(t.Kind) || !Enum.IsDefined(t.Alignment) || t.Duration < 2 || t.Duration > p.FrameRate.Frames(600)) throw new InvalidDataException("Invalid transition type or duration.");
                    bool audio = t.Kind is TransitionKind.LinearAudio or TransitionKind.EqualPowerAudio;
                    if (audio != (track.Kind == TrackKind.Audio)) throw new InvalidDataException("Transition kind does not match the track.");
                    if (!byId.TryGetValue(t.LeftClipId, out var left) || !byId.TryGetValue(t.RightClipId, out var right) || left.Id == right.Id || left.End != right.Start)
                        throw new InvalidDataException("A transition requires adjacent clips on the same track.");
                    var range = t.Range(left.End);
                    if (range.Start < left.Start || range.End > right.End) throw new InvalidDataException("Transition exceeds the adjacent clips.");
                    var la = assets[left.AssetId]; var ra = assets[right.AssetId];
                    if (FiniteMedia(la) && left.SourceTime(range.End, p.FrameRate) > la.DurationSeconds + 1e-6 || FiniteMedia(ra) && right.SourceTime(range.Start, p.FrameRate) < -1e-6)
                        throw new InvalidDataException("Insufficient source handles. Shorten or realign the transition, or trim the source clips first.");
                    ranges.Add(range);
                }
                foreach (var range in ranges.OrderBy(r => r.Start))
                {
                    if (range.Start < transitionEnd) throw new InvalidDataException("Transitions on one track cannot overlap.");
                    transitionEnd = range.End;
                }
            }
            if (p.InPoint < 0 || p.OutPoint is < 1 || p.OutPoint <= p.InPoint) throw new InvalidDataException("Invalid sequence In/Out range.");
            if (p.Markers.Any(m => m is null || m.Frame < 0 || m.Name is null) || p.Captions.Any(c => c is null || c.Start < 0 || c.End <= c.Start || c.Text is null))
                throw new InvalidDataException("Invalid marker or caption.");
        }
        finally { ancestors.Remove(p); }
    }
    public static bool FiniteMedia(MediaAsset a) => a.Kind is MediaKind.Video or MediaKind.Audio or MediaKind.Sequence or MediaKind.Multicam;
    private static void ValidateValue(AnimatedValue? value)
    {
        if (value is null || !double.IsFinite(value.Value) || value.Keys is null || value.Keys.Count > 10000 || value.FrameOffset is < -1000000000 or > 1000000000) throw new InvalidDataException("Invalid effect value.");
        long last = -1;
        foreach (var key in value.Keys)
        {
            if (key is null || key.Frame < 0 || key.Frame <= last || !double.IsFinite(key.Value) || !Enum.IsDefined(key.Interpolation)) throw new InvalidDataException("Keyframes must be finite, unique and sorted.");
            last = key.Frame;
        }
    }
    private static void Unique(IEnumerable<string?> values)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values) if (string.IsNullOrWhiteSpace(value) || !ids.Add(value)) throw new InvalidDataException("Identifiers must be nonempty and unique.");
    }
}
