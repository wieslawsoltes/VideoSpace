using SkiaSharp;
using VideoSpace.Core;
using Windows.ApplicationModel.DataTransfer;

namespace VideoSpace.Controls;

public sealed class ProjectBinView : Grid, IDisposable
{
    private readonly TextBox _search = Studio.Input(placeholder: "Search project");
    private readonly StackPanel _list = new() { Spacing = 2 };
    private readonly TextBlock _count = Studio.Text("", 10, Studio.Muted);
    private readonly Dictionary<string, SKImage> _thumbnails = [];
    private VideoProject? _project;
    private string? _selected;
    private bool _listMode;
    public event Action<string>? AssetSelected;
    public event Action<string>? AssetOpened;
    public event Action? ImportRequested;
    public event Action? NewTitleRequested;
    public ProjectBinView()
    {
        RowDefinitions.Add(new() { Height = new(37) }); RowDefinitions.Add(new() { Height = Studio.Star() }); RowDefinitions.Add(new() { Height = new(30) });
        var tools = Studio.Columns(Studio.Star(), new(32), new(32)); tools.Margin = new(9, 5, 7, 4);
        Studio.At(tools, _search); Studio.At(tools, new StudioButton("", () => { _listMode = false; Rebuild(); }, "grid", "Project icon view"), column: 1); Studio.At(tools, new StudioButton("", () => { _listMode = true; Rebuild(); }, "list", "Project list view"), column: 2); Studio.At(this, tools);
        _search.TextChanged += (_, _) => Rebuild();
        Studio.At(this, new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new(8, 3, 8, 8) }, 1);
        var footer = Studio.Columns(Studio.Star(), new(32), new(32)); footer.Margin = new(12, 0, 6, 0); Studio.At(footer, _count); Studio.At(footer, new StudioButton("", () => ImportRequested?.Invoke(), "import", "Import media"), column: 1); Studio.At(footer, new StudioButton("", () => NewTitleRequested?.Invoke(), "plus", "New title"), column: 2); Studio.At(this, footer, 2);
    }
    public void SetThumbnail(string id, byte[] data)
    {
        using var bytes = SKData.CreateCopy(data); var image = SKImage.FromEncodedData(bytes); if (image is null) return;
        if (_thumbnails.Remove(id, out var old)) old.Dispose(); _thumbnails.Add(id, image);
    }
    public void Update(VideoProject project, string? selected = null) { _project = project; if (selected is not null) _selected = selected; Rebuild(); }
    private void Rebuild()
    {
        _list.Children.Clear(); if (_project is null) return;
        var assets = _project.Assets.Where(a => a.Name.Contains(_search.Text ?? "", StringComparison.OrdinalIgnoreCase) || a.Bin.Contains(_search.Text ?? "", StringComparison.OrdinalIgnoreCase)).ToArray();
        _count.Text = $"{assets.Length} items  ·  Local project";
        foreach (var bin in assets.GroupBy(a => a.Bin))
        {
            var title = Studio.Row(new IconView { Glyph = "folder", Color = Studio.Muted, Width = 14, Height = 14 }, Studio.Text(bin.Key, 11, Studio.Muted)); title.Margin = new(3, 6, 0, 4); _list.Children.Add(title);
            if (_listMode) { foreach (var asset in bin) _list.Children.Add(Card(asset, true)); }
            else
            {
                Grid? row = null; int index = 0;
                foreach (var asset in bin)
                {
                    if (index % 2 == 0) { row = Studio.Columns(Studio.Star(), Studio.Star()); row.ColumnSpacing = 7; row.Margin = new(0, 0, 0, 7); _list.Children.Add(row); }
                    Studio.At(row!, Card(asset, false), column: index++ % 2);
                }
            }
        }
    }
    private UIElement Card(MediaAsset asset, bool list)
    {
        var body = new StackPanel { Spacing = 4 };
        if (!list) body.Children.Add(new AssetThumbnail { Asset = asset, Image = _thumbnails.GetValueOrDefault(asset.Id), HorizontalAlignment = HorizontalAlignment.Stretch });
        var name = Studio.Text(asset.Name, 11); name.Margin = new(4, 1, 4, 0); body.Children.Add(name);
        if (!list) { var info = Studio.Text($"{asset.Kind}  ·  {asset.DurationSeconds:0.0}s", 10, Studio.Muted); info.Margin = new(4, 0, 4, 4); body.Children.Add(info); }
        var card = new Border { Child = body, Background = Studio.Brush(asset.Id == _selected ? "#35445B" : "#292B2F"), BorderBrush = Studio.Brush(asset.Id == _selected ? Studio.Accent : "#36383D"), BorderThickness = new(1), Padding = new(2), CanDrag = true };
        Studio.Name(card, asset.Name); ToolTipService.SetToolTip(card, $"{asset.Name}\n{asset.Kind} · {asset.Width} × {asset.Height}\nDouble-click to open in Source. Drag to timeline to overwrite.");
        card.Tapped += (_, e) => { _selected = asset.Id; AssetSelected?.Invoke(asset.Id); Rebuild(); e.Handled = true; };
        card.DoubleTapped += (_, e) => { _selected = asset.Id; AssetOpened?.Invoke(asset.Id); e.Handled = true; };
        card.DragStarting += (_, e) => { e.Data.SetText("videospace-asset:" + asset.Id); e.Data.RequestedOperation = DataPackageOperation.Copy; };
        return card;
    }
    public void Dispose() { foreach (var image in _thumbnails.Values) image.Dispose(); _thumbnails.Clear(); }
}
