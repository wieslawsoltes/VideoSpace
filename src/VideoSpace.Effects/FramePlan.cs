using System.Text.Json.Serialization;

namespace VideoSpace.Effects;

public sealed record LayerPlan(
    string ClipId, string AssetId, string Kind, string Source, string Text, string Color,
    double SourceTime, double Speed, double X, double Y, double Scale, double Rotation,
    double Opacity, double Exposure, double Contrast, double Saturation, double Temperature,
    double Vignette, double CropLeft, double CropRight, double CropTop, double CropBottom)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FramePlan? Nested { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TransitionPlan? Transition { get; init; }
}

/// <summary>Premultiplied two-input compositing, isolated at the track's layer position.</summary>
public sealed record TransitionPlan(string Kind, double Progress, LayerPlan From, LayerPlan To);
public sealed record AudioPlan(string ClipId, string AssetId, string Source, double SourceTime, double Speed, double Gain, double Pan);
public sealed record FramePlan(long Frame, double Seconds, int Width, int Height, double Fps, LayerPlan[] Layers, AudioPlan[] Audio, string[] Captions);
