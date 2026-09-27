using System.Text.Json;

namespace VideoSpace.Media;

public static class BrowserState
{
    public static double[] Meter()
    {
#if __WASM__
        try { return JsonSerializer.Deserialize<double[]>(BrowserInterop.Call("meter", "null")) ?? [0, 0]; }
        catch { return [0, 0]; }
#else
        return [0, 0];
#endif
    }
    public static void Visible(string id, bool visible)
    {
#if __WASM__
        BrowserInterop.Call("visibility", JsonSerializer.Serialize(new { id, visible }));
#endif
    }
}
