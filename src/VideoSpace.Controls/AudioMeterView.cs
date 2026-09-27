using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using VideoSpace.Rendering;

namespace VideoSpace.Controls;

/// <summary>Peak/RMS display. Values must come from the audio host; silence is shown until actual metering is available.</summary>
public sealed class AudioMeterView : SKCanvasElement
{
    public double Left { get; set; }
    public double Right { get; set; }
    protected override void RenderOverride(SKCanvas c, Size area)
    {
        c.Clear(Palette.Parse("#202124")); float h = (float)area.Height, w = (float)area.Width; using var font = new SKFont(Studio.Typeface, 8); using var text = Palette.Paint(Studio.Muted);
        float top = 31, bottom = h - 21, usable = Math.Max(10, bottom - top);
        for (int db = 0; db >= -60; db -= 6) { float y = top + -db / 60f * usable; c.DrawText(db.ToString(), w - 16, y + 3, font, text); }
        double[] levels = [Left, Right];
        for (int channel = 0; channel < 2; channel++)
        {
            float x = 6 + channel * 12; double db = levels[channel] > 0 ? Math.Clamp(20 * Math.Log10(levels[channel]), -60, 0) : -60;
            for (int segment = 0; segment < 40; segment++)
            {
                double threshold = -60 + segment * 1.5; string color = threshold > -6 ? "#E76D61" : threshold > -15 ? "#D8C96B" : "#60AC7B";
                using var paint = Palette.Paint(threshold < db ? color : "#30383A"); float y = bottom - (segment + 1) / 40f * usable; c.DrawRect(x, y, 8, Math.Max(1, usable / 40 - 1), paint);
            }
        }
        c.DrawText("L", 7, h - 7, font, text); c.DrawText("R", 19, h - 7, font, text);
    }
}
