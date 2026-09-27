namespace VideoSpace.Core;

public sealed class TimelineClip
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string AssetId { get; set; } = "";
    public string Name { get; set; } = "Clip";
    public long Start { get; set; }
    public long Duration { get; set; } = 240;
    public double SourceIn { get; set; }
    public double Speed { get; set; } = 1;
    public string? LinkId { get; set; }
    public bool Enabled { get; set; } = true;
    public ClipEffects Effects { get; set; } = new();
    public AnimatedValue CameraAngle { get; set; } = new(0);
    [System.Text.Json.Serialization.JsonIgnore]
    public long End => checked(Start + Duration);
    public bool Contains(long frame) => frame >= Start && frame < End;
    public double SourceTime(long frame, FrameRate rate) => SourceTime((double)frame, rate);
    public double SourceTime(double frame, FrameRate rate) => SourceIn + (frame - Start) / rate.Value * Speed;
}
