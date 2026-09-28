using System.Text.Json;
using VideoSpace.Core;

namespace VideoSpace.Editing;

/// <summary>A value-owned selection. Paste preserves occupied ranges, gaps and effects,
/// restores asset metadata, and assigns fresh clip and link identifiers.</summary>
public sealed class TimelineClipboard
{
    private sealed record Entry(TrackKind Kind, int TrackOrdinal, TimelineClip Clip);
    private Entry[] _entries = [];
    private MediaAsset[] _assets = [];
    private FrameRate _rate;
    private TimelineTransition[] _transitions = [];
    public int Count => _entries.Length;
    public long Duration { get; private set; }
    public void Copy(VideoProject project, IEnumerable<string> selection)
    {
        ArgumentNullException.ThrowIfNull(project); ArgumentNullException.ThrowIfNull(selection);
        var selected = selection.ToHashSet(StringComparer.Ordinal);
        var clips = project.Tracks.SelectMany(t => t.Clips).Where(c => selected.Contains(c.Id)).ToArray();
        if (clips.Length == 0) return;
        long origin = clips.Min(c => c.Start); Duration = clips.Max(c => c.End) - origin; _rate = project.FrameRate;
        _entries = project.Tracks.SelectMany(track => track.Clips.Where(c => selected.Contains(c.Id)).Select(clip =>
        {
            var copy = ProjectSnapshot.CloneClip(clip); copy.Start -= origin;
            int ordinal = project.Tracks.Where(t => t.Kind == track.Kind).ToList().IndexOf(track);
            return new Entry(track.Kind, ordinal, copy);
        })).ToArray();
        var ids = clips.Select(c => c.AssetId).ToHashSet(StringComparer.Ordinal);
        foreach (var asset in project.Assets.Where(a => ids.Contains(a.Id)).ToArray()) foreach (var angle in asset.Angles) ids.Add(angle.AssetId);
        _assets = project.Assets.Where(a => ids.Contains(a.Id)).Select(CloneAsset).ToArray();
        _transitions = project.Tracks.SelectMany(t => t.Transitions).Where(t => selected.Contains(t.LeftClipId) && selected.Contains(t.RightClipId))
            .Select(t => new TimelineTransition { LeftClipId = t.LeftClipId, RightClipId = t.RightClipId, Kind = t.Kind, Alignment = t.Alignment, Duration = t.Duration }).ToArray();
    }
    /// <summary>Call inside EditorSession.Execute. Cross-timebase pastes reject rather
    /// than silently rounding edit/keyframe coordinates. Media bytes remain host-owned.</summary>
    public string[] Paste(VideoProject project, long at)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (Count == 0) return [];
        if (at < 0) throw new ArgumentOutOfRangeException(nameof(at));
        if (project.FrameRate != _rate) throw new InvalidOperationException("Paste requires the same sequence timebase. Conform the sequence before pasting.");
        var assetIds = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in _assets)
        {
            var existing = project.Assets.FirstOrDefault(a => a.Id == source.Id);
            if (existing is not null && JsonSerializer.Serialize(existing, ProjectSnapshot.Options) == JsonSerializer.Serialize(source, ProjectSnapshot.Options))
            { assetIds[source.Id] = source.Id; continue; }
            var copy = CloneAsset(source); if (existing is not null) copy.Id = Guid.NewGuid().ToString("N");
            project.Assets.Add(copy); assetIds[source.Id] = copy.Id;
        }
        foreach (var original in _assets.Where(a => a.Kind == MediaKind.Multicam))
        {
            var copied = project.Assets.First(a => a.Id == assetIds[original.Id]);
            bool remapped = original.Angles.Any(a => assetIds[a.AssetId] != a.AssetId);
            if (remapped && copied.Id == original.Id)
            {
                copied = CloneAsset(original); copied.Id = Guid.NewGuid().ToString("N");
                project.Assets.Add(copied); assetIds[original.Id] = copied.Id;
            }
            copied.Angles = original.Angles.Select(a => a with { AssetId = assetIds[a.AssetId] }).ToList();
        }
        var clipIds = new Dictionary<string, string>(StringComparer.Ordinal);
        var links = new Dictionary<string, string>(StringComparer.Ordinal); var result = new List<string>();
        foreach (var entry in _entries.OrderBy(e => e.Clip.Start))
        {
            var tracks = project.Tracks.Where(t => t.Kind == entry.Kind).ToList();
            while (tracks.Count <= entry.TrackOrdinal)
            {
                var track = new Track { Kind = entry.Kind, Name = (entry.Kind == TrackKind.Audio ? "A" : "V") + (tracks.Count + 1) };
                tracks.Add(track); project.Tracks.Add(track);
            }
            var target = tracks[entry.TrackOrdinal]; TimelineEdits.RequireUnlocked(target);
            var copy = ProjectSnapshot.CloneClip(entry.Clip); copy.AssetId = assetIds[copy.AssetId]; copy.Start = checked(at + copy.Start);
            string id = TimelineEdits.Insert(project, copy.AssetId, target.Id, copy.Start, copy.SourceIn, copy.Duration, true);
            clipIds[entry.Clip.Id] = id; copy.Id = id;
            if (copy.LinkId is { } sourceLink)
            {
                if (!links.TryGetValue(sourceLink, out var mapped)) links.Add(sourceLink, mapped = Guid.NewGuid().ToString("N"));
                copy.LinkId = mapped;
            }
            target.Clips[target.Clips.FindIndex(c => c.Id == id)] = copy; result.Add(id);
        }
        foreach (var transition in _transitions)
        {
            var left = clipIds[transition.LeftClipId];
            project.TrackFor(left).Transitions.Add(new TimelineTransition { LeftClipId = left, RightClipId = clipIds[transition.RightClipId], Kind = transition.Kind, Duration = transition.Duration, Alignment = transition.Alignment });
        }
        return result.ToArray();
    }
    private static MediaAsset CloneAsset(MediaAsset asset) =>
        JsonSerializer.Deserialize<MediaAsset>(JsonSerializer.Serialize(asset, ProjectSnapshot.Options), ProjectSnapshot.Options)
        ?? throw new InvalidDataException("Could not copy asset metadata.");
}
