using VideoSpace.Core;

namespace VideoSpace.Editing;

public static class TransitionEdits
{
    /// <summary>Call inside EditorSession.Execute. Strict handle validation occurs at commit.</summary>
    public static string Add(VideoProject project, string leftClipId, TransitionKind kind,
        long duration, TransitionAlignment alignment = TransitionAlignment.Center)
    {
        var track = project.TrackFor(leftClipId); TimelineEdits.RequireUnlocked(track);
        var left = project.Clip(leftClipId);
        var right = track.Clips.FirstOrDefault(c => c.Start == left.End) ?? throw new InvalidOperationException("A transition requires a clip immediately after the selected clip.");
        track.Transitions.RemoveAll(t => t.LeftClipId == left.Id && t.RightClipId == right.Id);
        var transition = new TimelineTransition { LeftClipId = left.Id, RightClipId = right.Id, Kind = kind, Duration = duration, Alignment = alignment };
        track.Transitions.Add(transition); return transition.Id;
    }
    public static void Remove(VideoProject project, string transitionId)
    {
        var track = project.Tracks.FirstOrDefault(t => t.Transitions.Any(x => x.Id == transitionId)) ?? throw new InvalidOperationException("Transition not found.");
        TimelineEdits.RequireUnlocked(track); track.Transitions.RemoveAll(t => t.Id == transitionId);
    }
    /// <summary>Structural edits remove transitions whose cut no longer exists. Other
    /// invalid transitions (exhausted handles, overlaps) reject the enclosing transaction.</summary>
    public static void PruneDetached(VideoProject project) => Prune(project, new(ReferenceEqualityComparer.Instance), 0);
    private static void Prune(VideoProject p, HashSet<VideoProject> visited, int depth)
    {
        if (depth > ProjectValidation.MaximumNestingDepth || !visited.Add(p)) throw new InvalidDataException("Cyclic or excessively nested project.");
        foreach (var track in p.Tracks)
        {
            var clips = track.Clips.ToDictionary(c => c.Id, StringComparer.Ordinal);
            track.Transitions.RemoveAll(t => !clips.TryGetValue(t.LeftClipId, out var a) || !clips.TryGetValue(t.RightClipId, out var b) || a.End != b.Start);
        }
        foreach (var asset in p.Assets.Where(a => a.Sequence is not null))
        {
            Prune(asset.Sequence!, visited, depth + 1);
            asset.Width = asset.Sequence!.Width; asset.Height = asset.Sequence.Height;
            asset.DurationSeconds = Math.Max(asset.DurationSeconds, asset.Sequence.FrameRate.Seconds(asset.Sequence.Duration));
        }
        visited.Remove(p);
    }
}
