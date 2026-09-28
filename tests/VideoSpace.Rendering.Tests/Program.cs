using SkiaSharp;
using VideoSpace.Effects;
using VideoSpace.Rendering;

using var renderer = new FrameRenderer();
void Image(string id, SKColor color)
{
    using var surface = SKSurface.Create(new SKImageInfo(160, 90)); surface.Canvas.Clear(color);
    using var image = surface.Snapshot(); using var bytes = image.Encode(SKEncodedImageFormat.Png, 100); renderer.SetImage(id, bytes.ToArray());
}
Image("red", SKColors.Red); Image("blue", SKColors.Blue); Image("green", SKColors.Lime);
LayerPlan Layer(string id, double opacity = 1) => new(id,id,"Image","","","",0,1,0,0,1,0,opacity,0,1,1,0,0,0,0,0,0);
FramePlan Plan(params LayerPlan[] layers) => new(0,0,160,90,24,layers,[],[]);
SKColor Pixel(FramePlan plan, int x = 80, int y = 45)
{
    using var bitmap = SKBitmap.Decode(renderer.Png(plan)); return bitmap.GetPixel(x,y);
}
void Near(SKColor actual, SKColor expected)
{
    if (Math.Abs(actual.Red-expected.Red)>2 || Math.Abs(actual.Green-expected.Green)>2 || Math.Abs(actual.Blue-expected.Blue)>2 || Math.Abs(actual.Alpha-expected.Alpha)>2)
        throw new Exception($"Expected {expected}, got {actual}");
}
int tests=0;
foreach(string kind in new[]{"CrossDissolve","DipToBlack","DipToWhite","WipeLeft","WipeRight"})
{
    foreach(double t in new[]{0d,1d}) { var l=Layer("mix") with { Transition=new(kind,t,Layer("red"),Layer("blue")) }; Near(Pixel(Plan(l)),t==0?SKColors.Red:SKColors.Blue); tests++; }
}
Near(Pixel(Plan(Layer("mix") with {Transition=new("CrossDissolve",.5,Layer("red"),Layer("blue"))})),new(128,0,128));tests++;
Near(Pixel(Plan(Layer("mix") with {Transition=new("DipToBlack",.5,Layer("red"),Layer("blue"))})),SKColors.Black);tests++;
Near(Pixel(Plan(Layer("mix") with {Transition=new("DipToWhite",.5,Layer("red"),Layer("blue"))})),SKColors.White);tests++;
Near(Pixel(Plan(Layer("green"),Layer("mix") with {Transition=new("CrossDissolve",.5,Layer("red",.5),Layer("blue",.5))})),new(64,127,64));tests++;
Near(Pixel(Plan(Layer("nested") with {Nested=Plan(Layer("blue"))})),SKColors.Blue);tests++;
using(var surface=SKSurface.Create(new SKImageInfo(160,90)))
{
    int before=surface.Canvas.SaveCount;
    renderer.Draw(surface.Canvas,new(0,0,160,90),Plan(Layer("blue")));
    if(surface.Canvas.SaveCount!=before)throw new Exception("Renderer leaked canvas save state.");tests++;
}
Console.WriteLine($"{tests}/{tests} native Skia graph pixel checks passed.");
