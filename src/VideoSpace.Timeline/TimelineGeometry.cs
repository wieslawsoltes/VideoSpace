using VideoSpace.Core;

namespace VideoSpace.Timeline;

public enum HitPart { None, Ruler, Track, Body, Head, Tail }
public sealed record TimelineHit(HitPart Part, long Frame, string? TrackId = null, string? ClipId = null);
public sealed class TimelineGeometry
{
    private ProjectIndex? _prepared;
    private Track[] _display = [];
    private Dictionary<string, TrackIndex> _tracks = [];
    private HashSet<long> _markers = [];
    public ProjectIndex? Prepared
    {
        get => _prepared;
        set { if (ReferenceEquals(value, _prepared)) return; _prepared = value; _display = value is null ? [] : DisplayTracks(value.Project); _tracks = value?.Tracks.ToDictionary(t => t.Track.Id) ?? []; _markers = value?.Project.Markers.Select(m => m.Frame).ToHashSet() ?? []; }
    }
    public Track[] Display(VideoProject project) => ReferenceEquals(Prepared?.Project, project) ? _display : DisplayTracks(project);
    public double PixelsPerFrame { get; set; } = 1;
    public double ScrollFrame { get; set; }
    public double ScrollY { get; set; }
    public double HeaderWidth { get; set; } = 148;
    public double RulerHeight { get; set; } = 58;
    public double TrackHeight { get; set; } = 52;
    public double X(long frame) => HeaderWidth + (frame - ScrollFrame) * PixelsPerFrame;
    public long Frame(double x) => Math.Max(0, (long)Math.Round(ScrollFrame + (x - HeaderWidth) / PixelsPerFrame));
    public double Y(int track) => RulerHeight + track * TrackHeight - ScrollY;
    public static Track[] DisplayTracks(VideoProject project) => project.Tracks.Where(t => t.Kind != TrackKind.Audio).Reverse().Concat(project.Tracks.Where(t => t.Kind == TrackKind.Audio)).ToArray();
    public TimelineHit Hit(VideoProject project, double x, double y)
    {
        long frame = Frame(x);
        if (y < RulerHeight) return new(HitPart.Ruler, frame);
        var tracks = Display(project); int index = (int)Math.Floor((y - RulerHeight + ScrollY) / TrackHeight);
        if (index < 0 || index >= tracks.Length) return new(HitPart.None, frame);
        var track = tracks[index]; if (x < HeaderWidth) return new(HitPart.Track, frame, track.Id);
        var clip = ReferenceEquals(Prepared?.Project, project) ? _tracks[track.Id].At(ScrollFrame + (x - HeaderWidth) / PixelsPerFrame) : track.Clips.FirstOrDefault(c => x >= X(c.Start) && x < X(c.End));
        if (clip is null) return new(HitPart.None, frame, track.Id);
        var part = Math.Abs(x - X(clip.Start)) < 7 ? HitPart.Head : Math.Abs(x - X(clip.End)) < 7 ? HitPart.Tail : HitPart.Body;
        return new(part, frame, track.Id, clip.Id);
    }
    public long Snap(VideoProject p, long frame, IEnumerable<string> exclude, long playhead, double tolerancePixels = 9)
    {
        var ids = exclude.ToHashSet();
        if (ReferenceEquals(Prepared?.Project, p))
        {
            double tolerance = tolerancePixels / PixelsPerFrame, best = tolerance; long found = frame;
            void Consider(long point) { double distance = Math.Abs(point - frame); if (distance < best) { best = distance; found = point; } }
            Consider(playhead); Consider(0);
            var points = Prepared.EditPoints;
            int first = TrackIndex.LowerBound(points, frame - tolerance), last = TrackIndex.UpperBound(points, frame + tolerance);
            for (int i = first; i < last; i++)
            {
                long point = points[i]; bool allowed = _markers.Contains(point);
                foreach (var track in Prepared.Tracks)
                {
                    if (allowed) break;
                    var right = track.At(point); var left = track.At(point - 1);
                    allowed = right is not null && right.Start == point && !ids.Contains(right.Id) || left is not null && left.End == point && !ids.Contains(left.Id);
                }
                if (allowed) Consider(point);
            }
            return found;
        }
        var candidates = p.Tracks.SelectMany(t => t.Clips).Where(c => !ids.Contains(c.Id)).SelectMany(c => new[] { c.Start, c.End }).Concat(p.Markers.Select(m => m.Frame)).Append(playhead).Append(0);
        long closest = frame; double distance = tolerancePixels / PixelsPerFrame;
        foreach (long candidate in candidates) if (Math.Abs(candidate - frame) < distance) { closest = candidate; distance = Math.Abs(candidate - frame); }
        return closest;
    }
    public void ZoomAt(double factor, double anchorX)
    {
        double frame = ScrollFrame + (anchorX - HeaderWidth) / PixelsPerFrame;
        PixelsPerFrame = Math.Clamp(PixelsPerFrame * factor, .02, 24);
        ScrollFrame = Math.Max(0, frame - (anchorX - HeaderWidth) / PixelsPerFrame);
    }
}
