using System.Text.Json.Serialization;

namespace VideoSpace.Core;

public sealed record Marker(long Frame, string Name, string Color = "#61CDA0");
public sealed record Caption(long Start, long End, string Text);

public sealed class VideoProject
{
    public int SchemaVersion { get; set; } = 1;
    public string Name { get; set; } = "Untitled project";
    public string SequenceName { get; set; } = "Sequence 01";
    public FrameRate FrameRate { get; set; } = FrameRate.Film;
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public List<MediaAsset> Assets { get; set; } = [];
    /// <summary>Video tracks are stored bottom to top. UI reverses video tracks; audio remains top to bottom.</summary>
    public List<Track> Tracks { get; set; } = [];
    public List<Marker> Markers { get; set; } = [];
    public List<Caption> Captions { get; set; } = [];
    public long InPoint { get; set; }
    public long? OutPoint { get; set; }
    [JsonIgnore] public long Duration => Math.Max(1, Tracks.SelectMany(t => t.Clips).Select(c => c.End).DefaultIfEmpty(1).Max());
    public MediaAsset Asset(string id) => Assets.FirstOrDefault(a => a.Id == id) ?? throw new InvalidOperationException("Media reference not found: " + id);
    public Track TrackFor(string clipId) => Tracks.FirstOrDefault(t => t.Clips.Any(c => c.Id == clipId)) ?? throw new InvalidOperationException("Clip not found.");
    public TimelineClip Clip(string id) => TrackFor(id).Clips.First(c => c.Id == id);
}
