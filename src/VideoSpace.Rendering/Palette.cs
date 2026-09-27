using SkiaSharp;

namespace VideoSpace.Rendering;

public static class Palette
{
    public static SKColor Parse(string value)
    {
        try { return SKColor.Parse(value); } catch { return new(127, 127, 127); }
    }
    public static SKPaint Paint(string color, bool stroke = false, float width = 1) => new() { Color = Parse(color), IsAntialias = true, Style = stroke ? SKPaintStyle.Stroke : SKPaintStyle.Fill, StrokeWidth = width };
}
