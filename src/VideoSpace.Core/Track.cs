namespace VideoSpace.Core;

public enum TrackKind { Video, Audio, Caption }
public sealed class Track
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "V1";
    public TrackKind Kind { get; set; }
    public bool Locked { get; set; }
    public bool Muted { get; set; }
    public bool Solo { get; set; }
    public bool SyncLock { get; set; } = true;
    public double Gain { get; set; } = 1;
    public List<TimelineClip> Clips { get; set; } = [];
    public List<TimelineTransition> Transitions { get; set; } = [];
}
