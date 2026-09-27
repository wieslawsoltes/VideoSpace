using SkiaSharp;

namespace VideoSpace.Rendering;

/// <summary>Deterministic vector landscapes used as self-contained, editable sample footage.</summary>
public static class ProceduralFootage
{
    public static void Draw(SKCanvas c, float w, float h, string scene, double time)
    {
        var top = scene == "dunes" ? "#26353C" : scene == "coast" ? "#738C91" : "#899FA8";
        var bottom = scene == "dunes" ? "#E4AB78" : scene == "coast" ? "#D8DDD0" : "#D7D4C4";
        using (var p = new SKPaint { Shader = SKShader.CreateLinearGradient(new(w / 2, 0), new(w / 2, h), [Palette.Parse(top), Palette.Parse(bottom)], null, SKShaderTileMode.Clamp) }) c.DrawRect(0, 0, w, h, p);
        float drift = (float)Math.Sin(time * .08) * .025f;
        using (var sun = Palette.Paint(scene == "dunes" ? "#F7DAB0" : "#DEDCCC")) c.DrawCircle(w * .74f, h * .29f, h * .065f, sun);
        string[] colors = scene == "dunes" ? ["#BA8D69", "#A27250", "#D3A276", "#805E49", "#352E2C"] : scene == "coast" ? ["#758D8E", "#4D757A", "#335B61", "#29484B", "#1D3337"] : ["#7C9397", "#60787E", "#465E66", "#324A50", "#1B333A"];
        for (int layer = 0; layer < colors.Length; layer++)
        {
            using var path = new SKPath(); path.MoveTo(0, h);
            for (int i = 0; i <= 64; i++)
            {
                float x = i / 64f;
                double height = scene == "dunes" ? Math.Sin(x * 4.6 + layer * 1.5 + drift) * .1 + Math.Sin(x * 2.1 + layer) * .08 : scene == "coast" ? Math.Sin(x * 5 + layer * 1.4 + drift) * .07 + Math.Cos(x * 12 + layer) * .022 : Math.Abs(Math.Sin(x * 5.5 + layer * 1.6 + drift)) * -.19 + Math.Sin(x * 19 + layer) * .017;
                path.LineTo(x * w, h * (float)(.56 + layer * .10 + height));
            }
            path.LineTo(w, h); path.Close(); using var p = Palette.Paint(colors[layer]); c.DrawPath(path, p);
        }
        if (scene == "coast")
        {
            using var wave = Palette.Paint("#B4C7BF", true, h * .0016f);
            for (int j = 0; j < 14; j++)
            {
                float y = h * (.66f + j * .016f); float x = w * (.15f + j * .017f + (float)Math.Sin(time * .5 + j) * .012f);
                c.DrawLine(x, y, x + w * (.25f - j * .006f), y - h * .007f, wave);
            }
        }
    }
}
