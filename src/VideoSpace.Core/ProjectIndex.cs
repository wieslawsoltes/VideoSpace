namespace VideoSpace.Core;

/// <summary>Prepared read index for a single committed project revision. The model remains
/// host-owned: discard this index after any mutation, including a failed transaction.</summary>
public sealed class ProjectIndex
{
    public VideoProject Project { get; }
    public IReadOnlyDictionary<string, MediaAsset> Assets { get; }
    public IReadOnlyDictionary<string, (Track Track, TimelineClip Clip)> Clips { get; }
    public TrackIndex[] Tracks { get; }
    public long Duration { get; }
    public long[] EditPoints { get; }

    public ProjectIndex(VideoProject project)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        Assets = project.Assets.ToDictionary(a => a.Id, StringComparer.Ordinal);
        Clips = project.Tracks.SelectMany(t => t.Clips.Select(c => (t, c)))
            .ToDictionary(x => x.c.Id, x => (x.t, x.c), StringComparer.Ordinal);
        Tracks = project.Tracks.Select(t => new TrackIndex(t)).ToArray();
        Duration = Math.Max(1, Tracks.Select(t => t.End).DefaultIfEmpty(1).Max());
        EditPoints = Clips.Values.SelectMany(c => new[] { c.Clip.Start, c.Clip.End })
            .Concat(project.Markers.Select(m => m.Frame)).Append(0).Distinct().Order().ToArray();
    }
}

public sealed record IndexedTransition(TimelineTransition Definition, TimelineClip Left,
    TimelineClip Right, TransitionRange Range);

public sealed class TrackIndex
{
    private readonly TimelineClip[] _clips;
    private readonly long[] _starts, _ends, _extendedStarts, _extendedEnds;
    private readonly IndexedTransition[] _transitions;
    private readonly long[] _transitionStarts;
    public Track Track { get; }
    public IReadOnlyList<TimelineClip> Clips => _clips;
    public IReadOnlyList<IndexedTransition> Transitions => _transitions;
    public long End => _clips.Length == 0 ? 0 : _ends[^1];

    public TrackIndex(Track track)
    {
        Track = track;
        _clips = track.Clips.OrderBy(c => c.Start).ToArray();
        _starts = _clips.Select(c => c.Start).ToArray();
        _ends = _clips.Select(c => c.End).ToArray();
        _extendedStarts = (long[])_starts.Clone(); _extendedEnds = (long[])_ends.Clone();
        var ids = _clips.Select((c, i) => (c.Id, Index: i)).ToDictionary(x => x.Id, x => x.Index, StringComparer.Ordinal);
        _transitions = track.Transitions.Select(t =>
        {
            int left = ids[t.LeftClipId], right = ids[t.RightClipId];
            var range = t.Range(_clips[left].End);
            _extendedEnds[left] = Math.Max(_ends[left], range.End);
            _extendedStarts[right] = Math.Min(_starts[right], range.Start);
            return new IndexedTransition(t, _clips[left], _clips[right], range);
        }).OrderBy(t => t.Range.Start).ToArray();
        _transitionStarts = _transitions.Select(t => t.Range.Start).ToArray();
    }
    public TimelineClip? At(double frame)
    {
        int i = UpperBound(_starts, frame) - 1;
        return i >= 0 && frame < _ends[i] ? _clips[i] : null;
    }
    public IndexedTransition? TransitionAt(double frame)
    {
        int i = UpperBound(_transitionStarts, frame) - 1;
        return i >= 0 && _transitions[i].Range.Contains(frame) ? _transitions[i] : null;
    }
    public ReadOnlySpan<TimelineClip> Visible(double from, double to, bool includeHandles = false)
    {
        if (to <= from) return [];
        var starts = includeHandles ? _extendedStarts : _starts;
        var ends = includeHandles ? _extendedEnds : _ends;
        int first = UpperBound(ends, from), last = LowerBound(starts, to);
        return _clips.AsSpan(first, Math.Max(0, last - first));
    }
    public static int LowerBound(long[] values, double target)
    {
        int lo = 0, hi = values.Length;
        while (lo < hi) { int m = lo + ((hi - lo) >> 1); if (values[m] < target) lo = m + 1; else hi = m; }
        return lo;
    }
    public static int UpperBound(long[] values, double target)
    {
        int lo = 0, hi = values.Length;
        while (lo < hi) { int m = lo + ((hi - lo) >> 1); if (values[m] <= target) lo = m + 1; else hi = m; }
        return lo;
    }
}
