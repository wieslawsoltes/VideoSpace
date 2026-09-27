using SkiaSharp;
using Uno.WinUI.Graphics2DSK;

namespace VideoSpace.Controls;

public sealed class DrawingSurface : SKCanvasElement
{
    public Action<SKCanvas, Size>? Draw { get; set; }
    protected override void RenderOverride(SKCanvas canvas, Size area) => Draw?.Invoke(canvas, area);
}
