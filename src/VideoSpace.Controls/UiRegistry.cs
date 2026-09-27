using System.Text.Json;

namespace VideoSpace.Controls;

/// <summary>Read-only control bounds for acceptance tests of a Skia-rendered UI. No command execution or project mutation endpoint.</summary>
public static class UiRegistry
{
    private static readonly Dictionary<string, WeakReference<FrameworkElement>> Elements = [];
    private static DispatcherTimer? _timer;
    public static void Register(string id, FrameworkElement element)
    {
        Elements[id] = new(element);
#if __WASM__
        if (_timer is null)
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _timer.Tick += (_, _) =>
            {
                var items = new Dictionary<string, object>();
                foreach (var (key, weak) in Elements.ToArray())
                {
                    if (!weak.TryGetTarget(out var target)) { Elements.Remove(key); continue; }
                    if (!target.IsLoaded || target.ActualWidth <= 0 || target.ActualHeight <= 0) continue;
                    var r = Studio.Bounds(target); items[key] = new { x = r.X, y = r.Y, width = r.Width, height = r.Height, enabled = target is not Control c || c.IsEnabled };
                }
                global::Uno.Foundation.WebAssemblyRuntime.InvokeJS("window.videoSpaceControls=" + JsonSerializer.Serialize(items) + ";'ok'");
            };
            _timer.Start();
        }
#endif
    }
}
