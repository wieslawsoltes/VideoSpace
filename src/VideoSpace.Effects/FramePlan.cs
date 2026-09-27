namespace VideoSpace.Effects;

public sealed record LayerPlan(
    string ClipId, string AssetId, string Kind, string Source, string Text, string Color,
    double SourceTime, double Speed, double X, double Y, double Scale, double Rotation,
    double Opacity, double Exposure, double Contrast, double Saturation, double Temperature,
    double Vignette, double CropLeft, double CropRight, double CropTop, double CropBottom);
public sealed record AudioPlan(string ClipId, string AssetId, string Source, double SourceTime, double Speed, double Gain, double Pan);
public sealed record FramePlan(long Frame, double Seconds, int Width, int Height, double Fps, LayerPlan[] Layers, AudioPlan[] Audio, string[] Captions);
