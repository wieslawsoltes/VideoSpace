namespace VideoSpace.Core;

public sealed class ClipEffects
{
    public AnimatedValue X { get; set; } = new(0);
    public AnimatedValue Y { get; set; } = new(0);
    public AnimatedValue Scale { get; set; } = new(1);
    public AnimatedValue Rotation { get; set; } = new(0);
    public AnimatedValue Opacity { get; set; } = new(1);
    public AnimatedValue Exposure { get; set; } = new(0);
    public AnimatedValue Contrast { get; set; } = new(1);
    public AnimatedValue Saturation { get; set; } = new(1);
    public AnimatedValue Temperature { get; set; } = new(0);
    public AnimatedValue Vignette { get; set; } = new(0);
    public AnimatedValue Gain { get; set; } = new(1);
    public double Pan { get; set; }
    public long FadeIn { get; set; }
    public long FadeOut { get; set; }
    public double CropLeft { get; set; }
    public double CropRight { get; set; }
    public double CropTop { get; set; }
    public double CropBottom { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public IEnumerable<AnimatedValue> Values => [X, Y, Scale, Rotation, Opacity, Exposure, Contrast, Saturation, Temperature, Vignette, Gain];
}
