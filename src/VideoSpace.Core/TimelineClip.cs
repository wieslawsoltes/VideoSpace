namespace VideoSpace.Core;

public sealed class TimelineClip
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string AssetId { get; set; } = "";
    public string Name { get; set; } = "Clip";
    public long Start { get; set; }
    public long Duration { get; set; } = 240;
    /// <summary>Source offset in seconds. Different source frame rates do not change timeline arithmetic.</summary>
    public double SourceIn { get; set; }
    public double Speed { get; set; } = 1;
    public string? LinkId { get; set; }
    public bool Enabled { get; set; } = true;
    public ClipEffects Effects { get; set; } = new();
    public long End => checked(Start + Duration);
    public bool Contains(long frame) => frame >= Start && frame < End;
    public double SourceTime(long frame, FrameRate rate) => SourceIn + rate.Seconds(frame - Start) * Speed;
}
