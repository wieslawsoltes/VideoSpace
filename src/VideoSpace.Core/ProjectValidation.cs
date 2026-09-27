namespace VideoSpace.Core;

public static class ProjectValidation
{
    public static void Validate(VideoProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.SchemaVersion != 1) throw new InvalidDataException("Unsupported project schema.");
        project.FrameRate.Validate();
        if (project.Width < 16 || project.Width > 8192 || project.Height < 16 || project.Height > 8192) throw new InvalidDataException("Sequence dimensions must be 16–8192 pixels.");
        if (project.Assets.Count > 10000 || project.Tracks.Count > 128 || project.Tracks.Sum(t => t.Clips.Count) > 100000) throw new InvalidDataException("Project exceeds safe editing limits.");
        Unique(project.Assets.Select(a => a.Id)); Unique(project.Tracks.Select(t => t.Id)); Unique(project.Tracks.SelectMany(t => t.Clips).Select(c => c.Id));
        foreach (var asset in project.Assets)
        {
            if (!double.IsFinite(asset.DurationSeconds) || asset.DurationSeconds <= 0 || asset.DurationSeconds > 86400) throw new InvalidDataException("Invalid media duration.");
            if (asset.Width < 0 || asset.Height < 0 || asset.Width > 32768 || asset.Height > 32768) throw new InvalidDataException("Invalid media dimensions.");
            if (asset.Peaks.Length > 100000 || asset.Peaks.Any(p => !float.IsFinite(p))) throw new InvalidDataException("Invalid audio peaks.");
        }
        foreach (var track in project.Tracks)
        {
            if (!double.IsFinite(track.Gain) || track.Gain < 0 || track.Gain > 16) throw new InvalidDataException("Invalid track gain.");
            long end = 0;
            foreach (var clip in track.Clips.OrderBy(c => c.Start))
            {
                var asset = project.Asset(clip.AssetId);
                if (clip.Start < 0 || clip.Duration < 1 || clip.End > project.FrameRate.Frames(86400) || clip.Start < end) throw new InvalidDataException("Clips on one track must have positive durations and cannot overlap.");
                if (!double.IsFinite(clip.Speed) || clip.Speed < .05 || clip.Speed > 16 || !double.IsFinite(clip.SourceIn) || clip.SourceIn < 0) throw new InvalidDataException("Invalid source time or speed.");
                if (asset.Kind is MediaKind.Video or MediaKind.Audio && clip.SourceIn + project.FrameRate.Seconds(clip.Duration) * clip.Speed > asset.DurationSeconds + .05) throw new InvalidDataException("The edit exceeds the available source media.");
                if (track.Kind == TrackKind.Audio && asset.Kind is MediaKind.Image or MediaKind.Title) throw new InvalidDataException("Still images cannot be placed on audio tracks.");
                foreach (var value in clip.Effects.Values)
                {
                    if (!double.IsFinite(value.Value) || value.Keys.Count > 10000) throw new InvalidDataException("Invalid effect value.");
                    long last = -1;
                    foreach (var key in value.Keys)
                    {
                        if (key.Frame < 0 || key.Frame <= last || !double.IsFinite(key.Value)) throw new InvalidDataException("Keyframes must be finite, unique and sorted.");
                        last = key.Frame;
                    }
                }
                if (clip.Effects.FadeIn < 0 || clip.Effects.FadeOut < 0 || !double.IsFinite(clip.Effects.Pan) || Math.Abs(clip.Effects.Pan) > 1) throw new InvalidDataException("Invalid audio or fade parameters.");
                var crop = new[] { clip.Effects.CropLeft, clip.Effects.CropRight, clip.Effects.CropTop, clip.Effects.CropBottom };
                if (crop.Any(v => !double.IsFinite(v) || v < 0 || v > .99) || crop[0] + crop[1] >= 1 || crop[2] + crop[3] >= 1) throw new InvalidDataException("Invalid crop.");
                end = clip.End;
            }
        }
        if (project.InPoint < 0 || project.OutPoint is < 1 || project.OutPoint <= project.InPoint) throw new InvalidDataException("Invalid sequence in/out range.");
        if (project.Markers.Any(m => m.Frame < 0) || project.Captions.Any(c => c.Start < 0 || c.End <= c.Start)) throw new InvalidDataException("Invalid marker or caption range.");
    }
    private static void Unique(IEnumerable<string> values)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values) if (string.IsNullOrWhiteSpace(value) || !ids.Add(value)) throw new InvalidDataException("Identifiers must be nonempty and unique.");
    }
}
