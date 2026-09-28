using System.Text.Json;

namespace VideoSpace.Controls;

/// <summary>Read-only bounds for real-input tests, not an editing-command endpoint.</summary>
public static class UiRegistry
{
    private static readonly Dictionary<string, WeakReference<FrameworkElement>> Elements = [];
#if __WASM__
    private static DispatcherTimer? _timer;
#endif
    public static void Register(string id, FrameworkElement element)
    {
        Elements[id] = new(element);
#if __WASM__
        // A reconstructed menu must not expose the old instance's rectangle before
        // the new control has loaded and been arranged. Tests still use real input.
        Invalidate(id);
        element.Unloaded += (_, _) =>
        {
            if (Elements.TryGetValue(id, out var weak) && weak.TryGetTarget(out var current) && ReferenceEquals(current, element)) Invalidate(id);
        };
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
                global::VideoSpace.Media.BrowserInterop.Call("controls", JsonSerializer.Serialize(items));
            };
            _timer.Start();
        }
#endif
    }
#if __WASM__
    private static void Invalidate(string id) => global::VideoSpace.Media.BrowserInterop.Call("controlInvalidated", JsonSerializer.Serialize(id));
#endif
}
