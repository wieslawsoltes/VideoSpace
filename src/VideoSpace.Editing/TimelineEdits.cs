using VideoSpace.Core;

namespace VideoSpace.Editing;

public static class TimelineEdits
{
    public static void RequireUnlocked(Track track) { if (track.Locked) throw new InvalidOperationException($"Track {track.Name} is locked."); }
    public static void Split(VideoProject p, IEnumerable<string> ids, long frame)
    {
        var links = new Dictionary<string, string>();
        foreach (string id in ids.ToArray())
        {
            var track = p.TrackFor(id); RequireUnlocked(track); var clip = p.Clip(id);
            if (frame <= clip.Start || frame >= clip.End) continue;
            var right = ProjectSnapshot.CloneClip(clip); long offset = frame - clip.Start;
            right.Id = Guid.NewGuid().ToString("N"); right.Start = frame; right.Duration -= offset;
            right.SourceIn += p.FrameRate.Seconds(offset) * clip.Speed;
            foreach (var value in right.Effects.Values.Append(right.CameraAngle)) value.FrameOffset += offset;
            foreach (var transition in track.Transitions.Where(t => t.LeftClipId == clip.Id)) transition.LeftClipId = right.Id;
            if (right.LinkId is { } oldLink)
            {
                if (!links.TryGetValue(oldLink, out var newLink)) links.Add(oldLink, newLink = Guid.NewGuid().ToString("N"));
                right.LinkId = newLink;
            }
            clip.Duration = offset; clip.Effects.FadeOut = 0; right.Effects.FadeIn = 0; track.Clips.Add(right);
        }
    }
    public static void Move(VideoProject p, IEnumerable<string> ids, long delta, string? targetTrack = null)
    {
        string[] selected = ids.ToArray();
        foreach (var id in selected) { var t = p.TrackFor(id); RequireUnlocked(t); p.Clip(id).Start += delta; }
        if (targetTrack is not null && selected.Length == 1)
        {
            var source = p.TrackFor(selected[0]); var target = p.Tracks.First(t => t.Id == targetTrack); RequireUnlocked(target);
            if (source.Kind != target.Kind) throw new InvalidOperationException("Use a compatible track.");
            var clip = p.Clip(selected[0]); source.Clips.Remove(clip); target.Clips.Add(clip);
        }
    }
    public static void Trim(VideoProject p, string id, long boundary, bool start, bool ripple = false, bool linked = true)
    {
        var initial = p.Clip(id);
        var ids = linked && initial.LinkId is { } link ? p.Tracks.SelectMany(t => t.Clips).Where(c => c.LinkId == link).Select(c => c.Id).ToArray() : [id];
        long delta = boundary - (start ? initial.Start : initial.End);
        long cut = start ? initial.Start : initial.End;
        foreach (var clipId in ids)
        {
            var track = p.TrackFor(clipId); RequireUnlocked(track); var clip = p.Clip(clipId);
            if (start)
            {
                clip.SourceIn += p.FrameRate.Seconds(delta) * clip.Speed; clip.Duration -= delta;
                if (!ripple) clip.Start += delta;
                ShiftKeys(clip, delta);
            }
            else clip.Duration += delta;
        }
        if (ripple)
        {
            long shift = start ? -delta : delta;
            foreach (var track in p.Tracks.Where(t => t.SyncLock))
            {
                foreach (var clip in track.Clips.Where(c => !ids.Contains(c.Id) && c.Start >= cut)) { RequireUnlocked(track); clip.Start += shift; }
                if (track.Clips.Any(c => !ids.Contains(c.Id) && c.Start < cut && c.End > cut)) throw new InvalidOperationException("Ripple would desynchronize a clip crossing this edit point.");
            }
        }
    }
    public static void Delete(VideoProject p, IEnumerable<string> ids, bool ripple)
    {
        var selected = ids.ToHashSet();
        var clips = p.Tracks.SelectMany(t => t.Clips).Where(c => selected.Contains(c.Id)).ToArray();
        if (clips.Length == 0) return;
        long from = clips.Min(c => c.Start), to = clips.Max(c => c.End);
        foreach (var track in p.Tracks)
        {
            if (track.Clips.Any(c => selected.Contains(c.Id))) { RequireUnlocked(track); track.Clips.RemoveAll(c => selected.Contains(c.Id)); }
        }
        if (!ripple) return;
        foreach (var track in p.Tracks.Where(t => t.SyncLock))
        {
            if (track.Clips.Any(c => c.Start < to && c.End > from)) throw new InvalidOperationException("Ripple deletion would remove unselected media. Select it or disable sync lock on that track.");
            foreach (var clip in track.Clips.Where(c => c.Start >= to)) { RequireUnlocked(track); clip.Start -= to - from; }
        }
    }
    public static string Insert(VideoProject p, string assetId, string trackId, long at, double sourceIn, long duration, bool overwrite)
    {
        var track = p.Tracks.First(t => t.Id == trackId); RequireUnlocked(track); var asset = p.Asset(assetId);
        if (duration < 1) throw new InvalidOperationException("Mark a nonempty source range.");
        if (overwrite) RemoveRange(p, track, at, at + duration);
        else
        {
            foreach (var t in p.Tracks.Where(t => t.Id == trackId || t.SyncLock))
            {
                if (t.Clips.Any(c => c.End > at)) RequireUnlocked(t);
                Split(p, t.Clips.Where(c => c.Start < at && c.End > at).Select(c => c.Id).ToArray(), at);
                foreach (var c in t.Clips.Where(c => c.Start >= at)) c.Start += duration;
            }
        }
        var clip = new TimelineClip { AssetId = assetId, Name = asset.Name, Start = at, Duration = duration, SourceIn = sourceIn };
        track.Clips.Add(clip); return clip.Id;
    }
    public static void Slip(VideoProject p, string id, long frames)
    {
        RequireUnlocked(p.TrackFor(id)); var clip = p.Clip(id); clip.SourceIn += p.FrameRate.Seconds(frames) * clip.Speed;
    }
    public static void Roll(VideoProject p, string leftId, long boundary)
    {
        var track = p.TrackFor(leftId); RequireUnlocked(track); var left = p.Clip(leftId);
        var right = track.Clips.FirstOrDefault(c => c.Start == left.End) ?? throw new InvalidOperationException("Rolling edit needs an adjacent clip.");
        long delta = boundary - left.End; left.Duration += delta; right.Start += delta; right.Duration -= delta;
        right.SourceIn += p.FrameRate.Seconds(delta) * right.Speed; ShiftKeys(right, delta);
    }
    public static void RateStretch(VideoProject p, string id, long end)
    {
        RequireUnlocked(p.TrackFor(id)); var clip = p.Clip(id); long duration = end - clip.Start;
        if (duration < 1) throw new InvalidOperationException("Duration must be positive.");
        clip.Speed *= (double)clip.Duration / duration; clip.Duration = duration;
    }
    public static void Link(VideoProject p, IEnumerable<string> ids, bool unlink)
    {
        var link = unlink ? null : Guid.NewGuid().ToString("N");
        foreach (var id in ids) { RequireUnlocked(p.TrackFor(id)); p.Clip(id).LinkId = link; }
    }
    private static void RemoveRange(VideoProject p, Track track, long from, long to)
    {
        Split(p, track.Clips.Where(c => c.Start < from && c.End > from).Select(c => c.Id).ToArray(), from);
        Split(p, track.Clips.Where(c => c.Start < to && c.End > to).Select(c => c.Id).ToArray(), to);
        track.Clips.RemoveAll(c => c.Start >= from && c.End <= to);
    }
    private static void ShiftKeys(TimelineClip clip, long delta)
    {
        foreach (var value in clip.Effects.Values.Append(clip.CameraAngle)) value.FrameOffset += delta;
    }
}
