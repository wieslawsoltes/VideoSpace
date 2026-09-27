namespace VideoSpace.Controls;

/// <summary>A compact tabbed tool/document panel. Host applications own workspace persistence and docking policy.</summary>
public sealed class PanelHost : Grid
{
    private readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal, Spacing = 3 };
    private readonly ContentControl _body = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private readonly Dictionary<string, (StudioButton Button, Func<UIElement> Factory)> _items = [];
    private readonly Dictionary<string, UIElement> _cache = [];
    public string Selected { get; private set; } = "";
    public event Action<string>? SelectedChanged;
    public PanelHost()
    {
        Background = Studio.Brush(Studio.Panel); RowDefinitions.Add(new() { Height = new(32) }); RowDefinitions.Add(new() { Height = Studio.Star() });
        var header = new Border { Background = Studio.Brush("#202124"), BorderBrush = Studio.Brush(Studio.Line), BorderThickness = new(0, 0, 0, 1), Padding = new(8, 0, 6, 0), Child = new ScrollViewer { Content = _tabs, HorizontalScrollMode = ScrollMode.Enabled, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollMode = ScrollMode.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled } };
        Studio.At(this, header); Studio.At(this, _body, 1);
    }
    public void Add(string name, Func<UIElement> factory)
    {
        var button = new StudioButton(name, () => Select(name), commandId: "Panel " + name) { Height = 30, Padding = new(7, 0, 7, 0) };
        _items.Add(name, (button, factory)); _tabs.Children.Add(button); if (Selected.Length == 0) Select(name);
    }
    public void Select(string name)
    {
        if (!_items.TryGetValue(name, out var item)) return;
        if (!_cache.TryGetValue(name, out var body)) { body = item.Factory(); _cache.Add(name, body); }
        Selected = name; _body.Content = body;
        foreach (var (key, entry) in _items) { entry.Button.SetActive(key == name); entry.Button.BorderBrush = Studio.Brush(key == name ? Studio.Accent : "#00000000"); entry.Button.BorderThickness = new(0, 0, 0, key == name ? 2 : 0); }
        SelectedChanged?.Invoke(name);
    }
    public void InvalidatePanel(string name) { if (_cache.Remove(name) && Selected == name) Select(name); }
}
