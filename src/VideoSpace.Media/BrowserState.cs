using System.Text.Json;

namespace VideoSpace.Media;

public static class BrowserState
{
    public static double[] Meter()
    {
#if __WASM__
        try { return JsonSerializer.Deserialize<double[]>(global::Uno.Foundation.WebAssemblyRuntime.InvokeJS("JSON.stringify(window.VideoSpaceMedia?.diagnostics?.meter||[0,0])")) ?? [0, 0]; }
        catch { return [0, 0]; }
#else
        return [0, 0];
#endif
    }
    public static void Visible(string id, bool visible)
    {
#if __WASM__
        global::Uno.Foundation.WebAssemblyRuntime.InvokeJS("document.documentElement.toggleAttribute(" + JsonSerializer.Serialize("data-videospace-" + id + "-hidden") + "," + (visible ? "false" : "true") + ");'ok'");
#endif
    }
}
