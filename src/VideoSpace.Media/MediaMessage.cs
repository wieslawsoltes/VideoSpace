using VideoSpace.Core;

namespace VideoSpace.Media;

public sealed class MediaMessage
{
    public string Type { get; set; } = "status";
    public string Text { get; set; } = "";
    public string Name { get; set; } = "";
    public MediaAsset? Asset { get; set; }
    public byte[]? Bytes { get; set; }
}
