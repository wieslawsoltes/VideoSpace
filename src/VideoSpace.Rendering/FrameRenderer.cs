using SkiaSharp;
using VideoSpace.Effects;

namespace VideoSpace.Rendering;

/// <summary>Renders into a host-owned Skia canvas. The host determines the native GPU or software backend.</summary>
public sealed class FrameRenderer : IDisposable
{
    private readonly Dictionary<string, SKImage> _images = [];
    private SKTypeface? _typeface;
    public void SetTypeface(SKTypeface typeface) { _typeface?.Dispose(); _typeface = typeface; }
    public void SetImage(string id, ReadOnlySpan<byte> bytes)
    {
        using var data = SKData.CreateCopy(bytes); var image = SKImage.FromEncodedData(data) ?? throw new InvalidDataException("Unsupported image.");
        if (image.Width > 16384 || image.Height > 16384) { image.Dispose(); throw new InvalidDataException("Image is too large."); }
        if (_images.Remove(id, out var old)) old.Dispose(); _images[id] = image;
    }
    public void Draw(SKCanvas canvas, SKRect area, FramePlan plan, bool safeAreas = false)
    {
        int budget = 0; DrawPlan(canvas, area, plan, safeAreas, 0, ref budget);
    }
    private void DrawPlan(SKCanvas canvas, SKRect area, FramePlan plan, bool safeAreas, int depth, ref int budget)
    {
        if (depth > 16) throw new InvalidOperationException("Composition nesting exceeds the rendering limit.");
        canvas.Save(); canvas.ClipRect(area); canvas.DrawColor(SKColors.Black);
        float scale = Math.Min(area.Width / plan.Width, area.Height / plan.Height);
        float w = plan.Width, h = plan.Height;
        canvas.Translate(area.MidX - w * scale / 2, area.MidY - h * scale / 2); canvas.Scale(scale);
        canvas.ClipRect(new(0, 0, w, h));
        foreach (var layer in plan.Layers) DrawLayer(canvas, w, h, layer, depth, ref budget);
        if (plan.Captions.Length > 0)
        {
            using var font = new SKFont(_typeface ?? SKTypeface.Default, h * .035f); using var ink = Palette.Paint("#FFFFFF"); using var back = new SKPaint { Color = SKColors.Black.WithAlpha(185) };
            var text = string.Join("  ", plan.Captions); float tw = font.MeasureText(text); canvas.DrawRect((w - tw) / 2 - 20, h * .88f, tw + 40, h * .063f, back);
            canvas.DrawText(text, w / 2, h * .925f, SKTextAlign.Center, font, ink);
        }
        if (safeAreas)
        {
            using var line = new SKPaint { Color = SKColors.White.WithAlpha(120), StrokeWidth = 1 / scale, Style = SKPaintStyle.Stroke };
            canvas.DrawRect(w * .05f, h * .05f, w * .9f, h * .9f, line); canvas.DrawRect(w * .1f, h * .1f, w * .8f, h * .8f, line);
        }
        canvas.Restore();
    }
    private void DrawLayer(SKCanvas canvas, float w, float h, LayerPlan layer, int depth, ref int budget)
    {
        if (++budget > 4096 || depth > 16) throw new InvalidOperationException("Expanded composition exceeds rendering limits.");
        if (layer.Transition is { } transition) { DrawTransition(canvas, w, h, transition, depth + 1, ref budget); return; }
        canvas.Save(); canvas.Translate(w * .5f + (float)layer.X * w, h * .5f + (float)layer.Y * h);
        canvas.RotateDegrees((float)layer.Rotation); canvas.Scale((float)layer.Scale); canvas.Translate(-w / 2, -h / 2);
        using var paint = new SKPaint { Color = SKColors.White.WithAlpha((byte)(layer.Opacity * 255)), ColorFilter = ColorFilter(layer) };
        canvas.SaveLayer(paint);
        canvas.ClipRect(new((float)layer.CropLeft * w, (float)layer.CropTop * h, w * (1 - (float)layer.CropRight), h * (1 - (float)layer.CropBottom)));
        if (layer.Nested is { } nested) DrawPlan(canvas, new(0, 0, w, h), nested, false, depth + 1, ref budget);
        else if (layer.Kind == "Generator") ProceduralFootage.Draw(canvas, w, h, layer.Source, layer.SourceTime);
        else if (layer.Kind == "Title")
        {
            using var font = new SKFont(_typeface ?? SKTypeface.Default, h * .14f); using var text = Palette.Paint("#F6F1E5");
            canvas.DrawText(layer.Text, w / 2, h * .55f, SKTextAlign.Center, font, text);
        }
        else if (_images.TryGetValue(layer.AssetId, out var image))
        {
            float fit = Math.Min(w / image.Width, h / image.Height); var dest = SKRect.Create((w - image.Width * fit) / 2, (h - image.Height * fit) / 2, image.Width * fit, image.Height * fit);
            canvas.DrawImage(image, dest);
        }
        else
        {
            using var bg = Palette.Paint("#25282B"); canvas.DrawRect(0, 0, w, h, bg);
            using var font = new SKFont(_typeface ?? SKTypeface.Default, h * .035f); using var ink = Palette.Paint("#B7BCC2");
            canvas.DrawText("MEDIA OFFLINE — RELINK IN THE PROJECT PANEL", w / 2, h / 2, SKTextAlign.Center, font, ink);
        }
        if (layer.Vignette > 0)
        {
            using var vignette = new SKPaint { Shader = SKShader.CreateRadialGradient(new(w / 2, h / 2), w * .65f, [SKColors.Transparent, SKColors.Black.WithAlpha((byte)(layer.Vignette * 220))], [0.3f, 1f], SKShaderTileMode.Clamp) };
            canvas.DrawRect(0, 0, w, h, vignette);
        }
        canvas.Restore(); canvas.Restore();
    }
    private void DrawTransition(SKCanvas canvas, float w, float h, TransitionPlan t, int depth, ref int budget)
    {
        double progress = Math.Clamp(t.Progress, 0, 1);
        canvas.SaveLayer();
        if (t.Kind is "WipeLeft" or "WipeRight")
        {
            float boundary = w * (float)(t.Kind == "WipeLeft" ? progress : 1 - progress);
            canvas.Save(); canvas.ClipRect(t.Kind == "WipeLeft" ? new(boundary, 0, w, h) : new(0, 0, boundary, h));
            DrawLayer(canvas, w, h, t.From, depth, ref budget); canvas.Restore();
            canvas.Save(); canvas.ClipRect(t.Kind == "WipeLeft" ? new(0, 0, boundary, h) : new(boundary, 0, w, h));
            DrawLayer(canvas, w, h, t.To, depth, ref budget); canvas.Restore();
        }
        else if (t.Kind is "DipToBlack" or "DipToWhite")
        {
            double picture = Math.Abs(2 * progress - 1);
            WeightedLayer(canvas, w, h, progress < .5 ? t.From : t.To, picture, depth, ref budget);
            using var paint = new SKPaint { Color = (t.Kind == "DipToWhite" ? SKColors.White : SKColors.Black).WithAlpha((byte)Math.Round((1-picture)*255)), BlendMode = SKBlendMode.Plus };
            canvas.DrawRect(0, 0, w, h, paint);
        }
        else
        {
            WeightedLayer(canvas, w, h, t.From, 1 - progress, depth, ref budget);
            WeightedLayer(canvas, w, h, t.To, progress, depth, ref budget);
        }
        canvas.Restore();
    }
    private void WeightedLayer(SKCanvas canvas, float w, float h, LayerPlan layer, double opacity, int depth, ref int budget)
    {
        using var weight = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(opacity * 255)), BlendMode = SKBlendMode.Plus };
        canvas.SaveLayer(weight); DrawLayer(canvas, w, h, layer, depth, ref budget); canvas.Restore();
    }
    public byte[] Png(FramePlan plan)
    {
        using var surface = SKSurface.Create(new SKImageInfo(plan.Width, plan.Height)) ?? throw new InvalidOperationException("Could not allocate output surface.");
        Draw(surface.Canvas, new(0, 0, plan.Width, plan.Height), plan);
        using var image = surface.Snapshot(); using var bytes = image.Encode(SKEncodedImageFormat.Png, 100); return bytes.ToArray();
    }
    private static SKColorFilter ColorFilter(LayerPlan l)
    {
        float s = (float)l.Saturation, c = (float)(l.Contrast * Math.Pow(2, l.Exposure));
        float r = .2126f * (1 - s), g = .7152f * (1 - s), b = .0722f * (1 - s);
        float offset = .5f * (1 - (float)l.Contrast);
        return SKColorFilter.CreateColorMatrix([
            (r+s)*c,g*c,b*c,0,offset+(float)l.Temperature*.08f,
            r*c,(g+s)*c,b*c,0,offset,
            r*c,g*c,(b+s)*c,0,offset-(float)l.Temperature*.08f,
            0,0,0,1,0]);
    }
    public void Dispose() { foreach (var image in _images.Values) image.Dispose(); _images.Clear(); _typeface?.Dispose(); }
}
