using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace VideoSpace.Media;

/// <summary>Named, JSON-only browser operations. No script evaluation is exposed.</summary>
public static partial class BrowserInterop
{
#if __WASM__
    [JSImport("globalThis.VideoSpaceBridge.call")]
    [SupportedOSPlatform("browser")]
    public static partial string Call(string operation, string payload);
#else
    public static string Call(string operation, string payload) => throw new PlatformNotSupportedException("This operation requires the browser host.");
#endif
}
