namespace VideoSpace.Core;

public enum MediaKind { Video, Audio, Image, Generator, Title, Sequence, Multicam }

public sealed class MediaAsset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Untitled";
    public string Bin { get; set; } = "Media";
    public MediaKind Kind { get; set; }
    public double DurationSeconds { get; set; } = 10;
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public string Color { get; set; } = "#667CA9";
    public string Text { get; set; } = "";
    public string Source { get; set; } = "";
    public long ByteLength { get; set; }
    public bool HasAudio { get; set; }
    public float[] Peaks { get; set; } = [];
    public VideoProject? Sequence { get; set; }
    public List<CameraAngle> Angles { get; set; } = [];
    public int AudioAngle { get; set; }
    public bool AudioFollowsVideo { get; set; }
}

/// <summary>Time-aligned camera source. OffsetSeconds maps group zero to source time.</summary>
public sealed record CameraAngle(string AssetId, string Name, double OffsetSeconds = 0);
