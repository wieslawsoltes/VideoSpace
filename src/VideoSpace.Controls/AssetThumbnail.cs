using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using VideoSpace.Core;
using VideoSpace.Rendering;

namespace VideoSpace.Controls;

public sealed class AssetThumbnail : SKCanvasElement
{
    public MediaAsset? Asset { get; set; }
    public SKImage? Image { get; set; }
    public AssetThumbnail() { Height = 66; }
    protected override void RenderOverride(SKCanvas c, Size area)
    {
        float w = (float)area.Width, h = (float)area.Height; c.Clear(Palette.Parse("#15191D")); if (Asset is not { } a) return;
        if (Image is { } image) { float fit = Math.Min(w / image.Width, h / image.Height); c.DrawImage(image, SKRect.Create((w - image.Width * fit) / 2, (h - image.Height * fit) / 2, image.Width * fit, image.Height * fit)); }
        else if (a.Kind == MediaKind.Generator) ProceduralFootage.Draw(c, w, h, a.Source, 4);
        else if (a.Kind == MediaKind.Title) { using var font = new SKFont(Studio.Typeface, Math.Min(24, h * .24f)); using var ink = Palette.Paint("#E9E7E1"); c.DrawText(a.Text, w / 2, h * .55f, SKTextAlign.Center, font, ink); }
        else if (a.Kind == MediaKind.Audio && a.Peaks.Length > 0)
        {
            using var wave = Palette.Paint("#73B598", true, 1.3f); for (int x = 4; x < w - 4; x += 2) { int i = Math.Clamp((int)(x / w * a.Peaks.Length), 0, a.Peaks.Length - 1); float peak = a.Peaks[i] * h * .42f; c.DrawLine(x, h / 2 - peak, x, h / 2 + peak, wave); }
        }
        else { using var font = new SKFont(Studio.Typeface, 12); using var ink = Palette.Paint(Studio.Muted); c.DrawText(a.Kind.ToString().ToUpperInvariant(), w / 2, h * .55f, SKTextAlign.Center, font, ink); }
    }
}
