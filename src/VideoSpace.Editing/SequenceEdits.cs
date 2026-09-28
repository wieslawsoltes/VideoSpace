using System.Text.Json;
using VideoSpace.Core;

namespace VideoSpace.Editing;

public static class SequenceEdits
{
    /// <summary>Nest a complete temporal selection, retaining editable source tracks,
    /// markers, captions, transitions and linked audio. Rejects ambiguous partial spans.</summary>
    public static string[] Nest(VideoProject p, IEnumerable<string> selection, string name)
    {
        var ids = selection.ToHashSet(StringComparer.Ordinal);
        var all = p.Tracks.SelectMany(t => t.Clips).ToArray();
        var chosen = all.Where(c => ids.Contains(c.Id)).ToArray();
        if (chosen.Length == 0) throw new InvalidOperationException("Select clips to nest.");
        long from = chosen.Min(c => c.Start), to = chosen.Max(c => c.End);
        if (all.Any(c => c.Start < to && c.End > from && !ids.Contains(c.Id)))
            throw new InvalidOperationException("Select every clip crossing the nested range, or razor its boundaries first. Partial overlapping selections cannot be nested losslessly.");
        foreach (var track in p.Tracks.Where(t => t.Clips.Any(c => ids.Contains(c.Id)))) TimelineEdits.RequireUnlocked(track);
        var nested = ProjectSnapshot.Clone(p); nested.Name = name; nested.SequenceName = name;
        nested.InPoint = 0; nested.OutPoint = null;
        foreach (var track in nested.Tracks)
        {
            track.Clips.RemoveAll(c => !ids.Contains(c.Id));
            foreach (var clip in track.Clips) clip.Start -= from;
            track.Transitions.RemoveAll(t => !ids.Contains(t.LeftClipId) || !ids.Contains(t.RightClipId));
        }
        var used = nested.Tracks.SelectMany(t => t.Clips).Select(c => c.AssetId).ToHashSet(StringComparer.Ordinal);
        foreach (var a in nested.Assets.Where(a => used.Contains(a.Id)).ToArray())
            foreach (var camera in a.Angles) used.Add(camera.AssetId);
        nested.Assets.RemoveAll(a => !used.Contains(a.Id));
        nested.Markers = p.Markers.Where(m => m.Frame >= from && m.Frame < to).Select(m => m with { Frame = m.Frame - from }).ToList();
        nested.Captions = p.Captions.Where(c => c.Start < to && c.End > from).Select(c => c with { Start = Math.Max(c.Start, from) - from, End = Math.Min(c.End, to) - from }).ToList();
        p.Markers.RemoveAll(m => m.Frame >= from && m.Frame < to);
        var remaining = new List<Caption>();
        foreach (var c in p.Captions)
        {
            if (c.End <= from || c.Start >= to) remaining.Add(c);
            else { if (c.Start < from) remaining.Add(c with { End = from }); if (c.End > to) remaining.Add(c with { Start = to }); }
        }
        p.Captions = remaining;
        bool hasVideo = nested.Tracks.Any(t => t.Kind != TrackKind.Audio && t.Clips.Count != 0);
        bool hasAudio = nested.Tracks.Any(t => t.Kind == TrackKind.Audio && t.Clips.Count != 0);
        var asset = new MediaAsset { Name = name, Kind = MediaKind.Sequence, Sequence = nested, DurationSeconds = p.FrameRate.Seconds(to - from), Width = p.Width, Height = p.Height, HasAudio = hasAudio, Bin = "Sequences", Color = "#A999CE" };
        p.Assets.Add(asset);
        var targets = p.Tracks.Where(t => t.Clips.Any(c => ids.Contains(c.Id))).ToArray();
        foreach (var t in p.Tracks) { t.Clips.RemoveAll(c => ids.Contains(c.Id)); t.Transitions.RemoveAll(x => ids.Contains(x.LeftClipId) || ids.Contains(x.RightClipId)); }
        var result = new List<string>(); string link = Guid.NewGuid().ToString("N");
        foreach (var kind in new[] { TrackKind.Video, TrackKind.Audio })
        {
            if (kind == TrackKind.Video && !hasVideo || kind == TrackKind.Audio && !hasAudio) continue;
            bool solo = kind == TrackKind.Audio && p.Tracks.Any(t => t.Kind == TrackKind.Audio && t.Solo);
            var track = targets.FirstOrDefault(t => t.Kind == kind && t.Gain == 1 && !t.Muted && (!solo || t.Solo));
            if (track is null)
            {
                track = new Track { Name = (kind == TrackKind.Video ? "V" : "A") + (p.Tracks.Count(t => t.Kind == kind) + 1), Kind = kind, Solo = solo };
                p.Tracks.Add(track);
            }
            var clip = new TimelineClip { AssetId = asset.Id, Name = name, Start = from, Duration = to - from, LinkId = link };
            track.Clips.Add(clip); result.Add(clip.Id);
        }
        return result.ToArray();
    }
    public static string[] Unnest(VideoProject p, string clipId)
    {
        var clip = p.Clip(clipId); var asset = p.Asset(clip.AssetId);
        var child = asset.Sequence ?? throw new InvalidOperationException("Select a nested sequence clip.");
        var siblings = p.Tracks.SelectMany(t => t.Clips).Where(c => c.Id == clipId || clip.LinkId is not null && c.LinkId == clip.LinkId && c.AssetId == asset.Id).ToArray();
        if (child.FrameRate != p.FrameRate || siblings.Any(c => c.SourceIn != 0 || c.Speed != 1 || c.Duration != child.Duration || !Identity(c.Effects)))
            throw new InvalidOperationException("Unnest requires an untrimmed, unit-speed sequence with default outer effects and the same timebase. Keep the nest to preserve its composition otherwise.");
        foreach (var c in siblings) TimelineEdits.RequireUnlocked(p.TrackFor(c.Id));
        foreach (var sourceTrack in child.Tracks.Where(t => t.Clips.Count > 0))
        {
            int ordinal = child.Tracks.Where(t => t.Kind == sourceTrack.Kind).ToList().IndexOf(sourceTrack);
            var target = p.Tracks.Where(t => t.Kind == sourceTrack.Kind).ElementAtOrDefault(ordinal);
            if ((target?.Gain ?? 1) != sourceTrack.Gain || (target?.Muted ?? false) != sourceTrack.Muted || (target?.Solo ?? false) != sourceTrack.Solo)
                throw new InvalidOperationException("Unnest requires matching parent/child track gain, mute and solo settings. Keep the nest to preserve the mix.");
        }
        var clipboard = new TimelineClipboard(); clipboard.Copy(child, child.Tracks.SelectMany(t => t.Clips).Select(c => c.Id));
        foreach (var c in siblings) p.TrackFor(c.Id).Clips.Remove(c);
        var result = clipboard.Paste(p, clip.Start);
        p.Markers.AddRange(child.Markers.Select(m => m with { Frame = m.Frame + clip.Start }));
        p.Captions.AddRange(child.Captions.Select(c => c with { Start = c.Start + clip.Start, End = c.End + clip.Start }));
        return result;
    }
    private static bool Identity(ClipEffects effects) => JsonSerializer.Serialize(effects, ProjectSnapshot.Options) == JsonSerializer.Serialize(new ClipEffects(), ProjectSnapshot.Options);

    public static string CreateMulticam(VideoProject p, string name, IReadOnlyList<CameraAngle> angles, bool audioFollowsVideo = false)
    {
        if (angles.Count is < 2 or > 16) throw new InvalidOperationException("Choose between two and sixteen cameras.");
        var first = p.Asset(angles[0].AssetId);
        double duration = angles.Min(a => p.Asset(a.AssetId).DurationSeconds - a.OffsetSeconds);
        var asset = new MediaAsset { Name = name, Kind = MediaKind.Multicam, Angles = angles.ToList(), Width = first.Width, Height = first.Height, DurationSeconds = duration, HasAudio = audioFollowsVideo ? angles.Any(a => p.Asset(a.AssetId).HasAudio) : first.HasAudio, AudioFollowsVideo = audioFollowsVideo, Bin = "Multicam", Color = "#B89A69" };
        p.Assets.Add(asset); return asset.Id;
    }
    public static void SwitchCamera(VideoProject p, IEnumerable<string> selection, int angle, long frame)
    {
        var ids = selection.ToHashSet(StringComparer.Ordinal);
        var links = p.Tracks.SelectMany(t => t.Clips).Where(c => ids.Contains(c.Id) && c.LinkId is not null).Select(c => c.LinkId).ToHashSet();
        bool any = false;
        foreach (var track in p.Tracks)
        foreach (var clip in track.Clips.Where(c => ids.Contains(c.Id) || c.LinkId is not null && links.Contains(c.LinkId)))
        {
            var asset = p.Asset(clip.AssetId);
            if (asset.Kind != MediaKind.Multicam) continue;
            TimelineEdits.RequireUnlocked(track);
            if (angle < 0 || angle >= asset.Angles.Count) throw new ArgumentOutOfRangeException(nameof(angle));
            long local = Math.Clamp(frame - clip.Start, 0, clip.Duration - 1);
            if (local > 0 && clip.CameraAngle.Keys.Count == 0) clip.CameraAngle.SetKey(0, clip.CameraAngle.Value, Interpolation.Hold);
            clip.CameraAngle.SetKey(local, angle, Interpolation.Hold); any = true;
        }
        if (!any) throw new InvalidOperationException("Select a multicamera clip first.");
    }
}
