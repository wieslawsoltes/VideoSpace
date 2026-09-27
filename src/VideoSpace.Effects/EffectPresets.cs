using VideoSpace.Core;

namespace VideoSpace.Effects;

public static class EffectPresets
{
    public static string[] Names => ["Reset effects", "Cinematic cool", "Golden hour", "Black & white", "Soft contrast", "Fade in / out", "Slow push in"];
    public static void Apply(TimelineClip clip, string name, FrameRate rate)
    {
        var e = clip.Effects;
        switch (name)
        {
            case "Reset effects": clip.Effects = new(); break;
            case "Cinematic cool": e.Temperature.Value = -.24; e.Contrast.Value = 1.12; e.Saturation.Value = .76; e.Vignette.Value = .25; break;
            case "Golden hour": e.Temperature.Value = .32; e.Exposure.Value = .18; e.Saturation.Value = 1.12; break;
            case "Black & white": e.Saturation.Value = 0; e.Contrast.Value = 1.16; break;
            case "Soft contrast": e.Contrast.Value = .82; e.Exposure.Value = .1; break;
            case "Fade in / out": e.FadeIn = e.FadeOut = Math.Min(clip.Duration / 2, rate.Frames(.75)); break;
            case "Slow push in": e.Scale.SetKey(0, 1); e.Scale.SetKey(clip.Duration - 1, 1.12, Interpolation.Smooth); break;
            default: throw new ArgumentException("Unknown effect preset.", nameof(name));
        }
    }
}
