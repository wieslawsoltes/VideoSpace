using SkiaSharp;
using Uno.WinUI.Graphics2DSK;

namespace VideoSpace.Controls;

public sealed class IconView : SKCanvasElement
{
    public string Glyph { get; set; } = "select";
    public string Color { get; set; } = Studio.Ink;
    public IconView() { Width = Height = 16; IsHitTestVisible = false; }
    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        canvas.Save(); canvas.Scale((float)area.Width / 24, (float)area.Height / 24);
        using var path = SKPath.ParseSvgPathData(Paths.GetValueOrDefault(Glyph) ?? Paths["select"]);
        using var paint = new SKPaint { Color = VideoSpace.Rendering.Palette.Parse(Color), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.6f, StrokeJoin = SKStrokeJoin.Round, StrokeCap = SKStrokeCap.Round };
        canvas.DrawPath(path, paint); canvas.Restore();
    }
    public static IReadOnlyDictionary<string, string> Paths { get; } = new Dictionary<string, string>
    {
        ["select"]="M5 3L19 13L12 14L9 21Z M12 14L17 21",
        ["razor"]="M3 15L15 3L21 9L9 21Z M7 11L13 17 M12 6L18 12",
        ["ripple"]="M12 3V21 M2 12H10 M5 8L9 12L5 16 M22 12H14 M19 8L15 12L19 16",
        ["roll"]="M10 3V21 M14 3V21 M2 12H8 M5 9L8 12L5 15 M22 12H16 M19 9L16 12L19 15",
        ["slip"]="M5 4V20 M19 4V20 M8 12H16 M10 9L7 12L10 15 M14 9L17 12L14 15",
        ["stretch"]="M4 5V19 M20 5V19 M7 12H17 M10 8L6 12L10 16 M14 8L18 12L14 16",
        ["hand"]="M6 13V7Q6 4 9 6V12V4Q12 1 12 5V12V5Q15 3 15 7V12V9Q18 7 18 11V16Q18 22 11 22Q7 22 4 17L2 13Q3 10 5 13Z",
        ["play"]="M7 4L20 12L7 20Z", ["pause"]="M7 4V20 M17 4V20", ["stop"]="M5 5H19V19H5Z",
        ["previous"]="M5 4V20 M19 5L7 12L19 19Z", ["next"]="M19 4V20 M5 5L17 12L5 19Z",
        ["in"]="M15 4H8V20H15 M3 12H17 M13 8L17 12L13 16",
        ["out"]="M9 4H16V20H9 M7 12H21 M11 8L7 12L11 16",
        ["marker"]="M7 3H17V12L12 18L7 12Z", ["plus"]="M12 4V20 M4 12H20", ["minus"]="M4 12H20",
        ["import"]="M12 3V16 M7 11L12 16L17 11 M4 18V21H20V18", ["export"]="M12 16V3 M7 8L12 3L17 8 M4 18V21H20V18",
        ["folder"]="M3 6H10L13 9H21V20H3Z", ["save"]="M4 3H17L21 7V21H3V3Z M7 3V10H17V3 M7 21V14H17V21",
        ["undo"]="M8 4L3 9L8 14 M3 9H14Q21 9 21 16V20", ["redo"]="M16 4L21 9L16 14 M21 9H10Q3 9 3 16V20",
        ["text"]="M4 5V3H20V5 M12 3V21 M8 21H16", ["link"]="M8 16L16 8 M7 13L4 16Q1 21 7 22L12 17 M12 7L17 2Q23 2 22 8L17 13",
        ["snap"]="M5 4V14Q5 22 12 22Q19 22 19 14V4H14V14Q14 17 12 17Q10 17 10 14V4Z M5 9H10 M14 9H19",
        ["lock"]="M5 10H19V21H5Z M8 10V6A4 4 0 0 1 16 6V10", ["eye"]="M2 12Q12 0 22 12Q12 24 2 12Z M15 12A3 3 0 1 1 9 12A3 3 0 1 1 15 12Z",
        ["grid"]="M3 3H9V9H3Z M15 3H21V9H15Z M3 15H9V21H3Z M15 15H21V21H15Z",
        ["list"]="M3 5H5 M9 5H21 M3 12H5 M9 12H21 M3 19H5 M9 19H21",
        ["search"]="M17 10A7 7 0 1 1 3 10A7 7 0 1 1 17 10Z M15 15L21 21",
        ["more"]="M4 12H5 M11 12H12 M18 12H19", ["close"]="M6 6L18 18 M18 6L6 18",
        ["key"]="M12 3L21 12L12 21L3 12Z", ["chevron"]="M7 9L12 14L17 9",
        ["guide"]="M3 3H21V21H3Z M7 7H17V17H7Z", ["camera"]="M3 7H7L9 4H15L17 7H21V20H3Z M16 13A4 4 0 1 1 8 13A4 4 0 1 1 16 13Z",
        ["trash"]="M3 6H21 M9 6V3H15V6 M6 6L7 21H17L18 6 M10 10V17 M14 10V17",
        ["audio"]="M4 9H8L14 4V20L8 15H4Z M17 8Q22 12 17 16",
        ["video"]="M3 4H21V20H3Z M3 8H21 M3 16H21 M7 4V8 M13 4V8 M19 4V8 M7 16V20 M13 16V20 M19 16V20",
        ["help"]="M21 12A9 9 0 1 1 3 12A9 9 0 1 1 21 12Z M9 8Q9 4 13 6Q17 8 12 12V14 M12 18H12.1"
    };
}
