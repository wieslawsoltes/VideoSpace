using SkiaSharp;
using VideoSpace.Core;
using VideoSpace.Effects;
using VideoSpace.Media;

namespace VideoSpace.Controls;

public sealed class MonitorView : Grid, IDisposable
{
    private readonly DrawingSurface _surface = new();
    private readonly TextBlock _duration = Studio.Text("00:00:00:00", 11, Studio.Muted);
    private readonly TextBlock _info = Studio.Text("1920 × 1080", 10, Studio.Muted);
    private readonly TimecodeField _time;
    private readonly StudioButton _play;
    private readonly VideoSpace.Rendering.FrameRenderer _renderer;
    private readonly MediaServices _media;
    private FramePlan? _plan;
    private FrameRate _rate;
    public bool Guides { get; private set; }
    public string MonitorId { get; }
    public event Action<string>? Command;
    public event Action<long>? Seek;
    public MonitorView(string id, MediaServices media, VideoSpace.Rendering.FrameRenderer renderer)
    {
        MonitorId = id; _media = media; _renderer = renderer; Background = Studio.Brush(Studio.Panel);
        RowDefinitions.Add(new() { Height = Studio.Star() }); RowDefinitions.Add(new() { Height = new(32) }); RowDefinitions.Add(new() { Height = new(36) });
        _surface.Draw = (canvas, size) => { if (_plan is not null) _renderer.Draw(canvas, new(0, 0, (float)size.Width, (float)size.Height), _plan, Guides); else canvas.Clear(SKColors.Black); };
        var surfaceHost = new Grid { Background = Studio.Brush("#0B0C0D"), Margin = new(10, 8, 10, 0) }; surfaceHost.Children.Add(_surface); Studio.At(this, surfaceHost);
        _surface.PointerPressed += (_, e) => { var p = e.GetCurrentPoint(_surface).Position; if (_plan is not null && _surface.ActualWidth > 0) Seek?.Invoke((long)(p.X / _surface.ActualWidth * Math.Max(1, TotalFrames))); e.Handled = true; };
        var info = Studio.Columns(new(118), Studio.Star(), new(100)); info.Margin = new(8, 0, 10, 0);
        _time = new TimecodeField(id + " timecode"); _time.Committed += frame => Seek?.Invoke(frame); Studio.At(info, _time); _info.HorizontalAlignment = HorizontalAlignment.Center; Studio.At(info, _info, column: 1); _duration.HorizontalAlignment = HorizontalAlignment.Right; Studio.At(info, _duration, column: 2); Studio.At(this, info, 1);
        var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 4 };
        foreach (var (name, icon) in new[] { ("Mark in", "in"), ("Mark out", "out"), ("Previous frame", "previous") }) controls.Children.Add(new StudioButton("", () => Command?.Invoke(name), icon, id + " " + name));
        _play = new StudioButton("", () => Command?.Invoke("Play"), "play", id + " Play"); controls.Children.Add(_play);
        controls.Children.Add(new StudioButton("", () => Command?.Invoke("Next frame"), "next", id + " Next frame"));
        controls.Children.Add(new StudioButton("", () => Command?.Invoke("Add marker"), "marker", id + " Add marker"));
        controls.Children.Add(new StudioButton("", () => { Guides = !Guides; _surface.Invalidate(); }, "guide", id + " Safe margins"));
        controls.Children.Add(new StudioButton("", () => Command?.Invoke("Snapshot"), "camera", id + " Export frame"));
        Studio.At(this, controls, 2);
    }
    public long TotalFrames { get; private set; }
    public void Update(FramePlan plan, long total, FrameRate rate, bool playing, double playbackRate, bool visible = true)
    {
        bool changed = !ReferenceEquals(_plan, plan);
        if (_plan is null || _plan.Frame != plan.Frame || _rate != rate) { _time.Rate = rate; _time.Update(plan.Frame); }
        if (_plan is null || total != TotalFrames || _rate != rate) _duration.Text = Timecode.Format(total, rate);
        if (_plan is null || plan.Width != _plan.Width || plan.Height != _plan.Height) _info.Text = $"Fit  ·  {plan.Width} × {plan.Height}";
        _plan = plan; TotalFrames = total; _rate = rate; _play.SetIcon(playing ? "pause" : "play");
        if (!_media.Browser && changed) _surface.Invalidate();
        if (_media.Browser && visible && Visibility == Visibility.Visible && ActualWidth > 2)
        {
            var b = Studio.Bounds(_surface); _media.Present(MonitorId, plan, b.X, b.Y, b.Width, b.Height, playing, playbackRate, Guides);
        }
    }
    public void Dispose() { _surface.Draw = null; }
}
