using SkiaSharp;
using VideoSpace.Core;
using VideoSpace.Editing;
using VideoSpace.Rendering;
using VideoSpace.Timeline;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace VideoSpace.Controls;

/// <summary>Virtualized, frame-addressed timeline with direct editing. Gestures preview until a validated transaction commits.</summary>
public sealed class TimelineView : UserControl
{
    private readonly EditorSession _session;
    private readonly DrawingSurface _canvas = new();
    private TimelineHit? _press;
    private Point _pressPoint;
    private long _dragFrame;
    private double _originalScroll;
    private bool _dragging, _scrubbing;
    public TimelineGeometry Geometry { get; } = new();
    public string TargetTrackId { get; set; } = "v1";
    public event Action<string>? Error;
    public event Action<string, string, long>? AssetDropped;
    public event Action<string>? TrackSelected;
    public TimelineView(EditorSession session)
    {
        _session = session; Content = _canvas; Background = Studio.Brush("#202124"); IsTabStop = true; AllowDrop = true;
        _canvas.Draw = Draw; Studio.Name(this, "Sequence timeline");
        PointerPressed += Pressed; PointerMoved += Moved; PointerReleased += Released;
        PointerCaptureLost += (_, _) => { _dragging = _scrubbing = false; _press = null; Invalidate(); };
        PointerWheelChanged += Wheel;
        DragOver += (_, e) => { if (e.DataView.Contains(StandardDataFormats.Text)) { e.AcceptedOperation = DataPackageOperation.Copy; e.Handled = true; } };
        Drop += async (_, e) =>
        {
            try
            {
                string text = await e.DataView.GetTextAsync(); if (!text.StartsWith("videospace-asset:", StringComparison.Ordinal)) return;
                var p = e.GetPosition(this); var hit = Geometry.Hit(_session.Project, p.X, p.Y);
                AssetDropped?.Invoke(text[17..], hit.TrackId ?? TargetTrackId, hit.Frame); e.Handled = true;
            }
            catch (Exception ex) { Error?.Invoke(ex.Message); }
        };
        SizeChanged += (_, _) => Invalidate();
    }
    public void Invalidate() { Geometry.Prepared = _session.Index; _canvas.Invalidate(); }
    public void Fit()
    {
        Geometry.PixelsPerFrame = Math.Clamp((Math.Max(200, ActualWidth) - Geometry.HeaderWidth - 30) / Math.Max(240, _session.Index.Duration), .02, 24); Geometry.ScrollFrame = 0; Invalidate();
    }
    public void Zoom(double factor) { Geometry.ZoomAt(factor, Geometry.X(_session.Playhead)); Invalidate(); }
    public void EnsurePlayheadVisible()
    {
        double x = Geometry.X(_session.Playhead);
        if (x > ActualWidth - 20) Geometry.ScrollFrame = Math.Max(0, _session.Playhead - (ActualWidth - Geometry.HeaderWidth) * .2 / Geometry.PixelsPerFrame);
        else if (x < Geometry.HeaderWidth) Geometry.ScrollFrame = _session.Playhead;
        Invalidate();
    }
    public object Diagnostics()
    {
        var bounds = Studio.Bounds(this); var tracks = Geometry.Display(_session.Project);
        return new
        {
            x = bounds.X, y = bounds.Y, width = bounds.Width, height = bounds.Height,
            headerWidth = Geometry.HeaderWidth, rulerHeight = Geometry.RulerHeight, trackHeight = Geometry.TrackHeight,
            pixelsPerFrame = Geometry.PixelsPerFrame, scrollFrame = Geometry.ScrollFrame,
            clips = tracks.SelectMany((t, i) => DiagnosticClips(t).Select(c => new { c.Id, c.Start, c.Duration, c.SourceIn, c.Speed, track = t.Id, x = bounds.X + Geometry.X(c.Start), y = bounds.Y + Geometry.Y(i) + 4, width = c.Duration * Geometry.PixelsPerFrame, height = Geometry.TrackHeight - 8 })).ToArray()
        };
    }
    private IEnumerable<TimelineClip> DiagnosticClips(Track track)
    {
        if (_session.Index.Clips.Count <= 1000) return track.Clips;
        var ti = _session.Index.Tracks.First(t => t.Track.Id == track.Id);
        return ti.Visible(Geometry.ScrollFrame, Geometry.ScrollFrame + Math.Max(1, (ActualWidth - Geometry.HeaderWidth) / Geometry.PixelsPerFrame)).ToArray();
    }
    private void TryEdit(string name, Action<VideoProject> edit)
    {
        try { _session.Execute(name, edit); } catch (Exception ex) { Error?.Invoke(ex.Message); } Invalidate();
    }
    private void Pressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Focus(FocusState.Pointer); _session.Playing = false;
        _pressPoint = e.GetCurrentPoint(this).Position; _press = Geometry.Hit(_session.Project, _pressPoint.X, _pressPoint.Y); _dragFrame = _press.Frame; _originalScroll = Geometry.ScrollFrame;
        if (_press.Part == HitPart.Track && _press.TrackId is { } trackId)
        {
            TargetTrackId = trackId; TrackSelected?.Invoke(trackId);
            var x = _pressPoint.X;
            if (x >= 54 && x < 80) TryEdit("Toggle track lock", p => p.Tracks.First(t => t.Id == trackId).Locked ^= true);
            else if (x >= 80 && x < 108) TryEdit("Toggle track mute", p => p.Tracks.First(t => t.Id == trackId).Muted ^= true);
            else if (x >= 108 && x < 135) TryEdit("Toggle track solo", p => { var t = p.Tracks.First(t => t.Id == trackId); if (t.Kind == TrackKind.Audio) t.Solo ^= true; else t.SyncLock ^= true; });
            _session.Notify(); _press = null; e.Handled = true; return;
        }
        if (_session.Tool == EditTool.Hand) { _dragging = CapturePointer(e.Pointer); e.Handled = true; return; }
        if (_press.Part == HitPart.Ruler || _press.ClipId is null)
        {
            if (_pressPoint.X >= Geometry.HeaderWidth) { _session.Seek(_press.Frame); _scrubbing = CapturePointer(e.Pointer); }
            if (_press.Part != HitPart.Ruler && !e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift)) _session.Select(null);
            e.Handled = true; return;
        }
        var id = _press.ClipId;
        if (!_session.Selection.Contains(id) || e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift)) _session.Select(id, e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift));
        if (_session.Tool == EditTool.Razor)
        {
            TryEdit("Razor cut", p => TimelineEdits.Split(p, _session.Selection.ToArray(), _press.Frame)); _press = null;
        }
        else _dragging = CapturePointer(e.Pointer);
        Invalidate(); e.Handled = true;
    }
    private void Moved(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this).Position;
        if (_scrubbing) { _session.Seek(Geometry.Frame(point.X)); Invalidate(); e.Handled = true; return; }
        if (!_dragging || _press is null) return;
        if (_session.Tool == EditTool.Hand) { Geometry.ScrollFrame = Math.Max(0, _originalScroll - (point.X - _pressPoint.X) / Geometry.PixelsPerFrame); Invalidate(); e.Handled = true; return; }
        _dragFrame = Geometry.Frame(point.X);
        if (_session.Snapping && !e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control))
        {
            var clip = _session.Index.Clips[_press.ClipId!].Clip;
            long candidate = _press.Part == HitPart.Body ? clip.Start + _dragFrame - _press.Frame : _dragFrame;
            long snapped = Geometry.Snap(_session.Project, candidate, _session.Selection, _session.Playhead);
            _dragFrame += snapped - candidate;
        }
        Invalidate(); e.Handled = true;
    }
    private void Released(object sender, PointerRoutedEventArgs e)
    {
        if (_scrubbing) { _scrubbing = false; ReleasePointerCapture(e.Pointer); _press = null; e.Handled = true; return; }
        if (!_dragging || _press is null) return;
        var press = _press; var point = e.GetCurrentPoint(this).Position;
        _dragging = false; _press = null; ReleasePointerCapture(e.Pointer);
        if (_session.Tool == EditTool.Hand || press.ClipId is null || Math.Abs(point.X - _pressPoint.X) < 3) { Invalidate(); return; }
        var id = press.ClipId; long frame = _dragFrame; long delta = frame - press.Frame;
        var target = Geometry.Hit(_session.Project, point.X, point.Y).TrackId;
        var selection = _session.Selection.ToArray();
        if (_session.Tool == EditTool.Slip) TryEdit("Slip clip", p => TimelineEdits.Slip(p, id, delta));
        else if (_session.Tool == EditTool.Rolling) TryEdit("Rolling edit", p => TimelineEdits.Roll(p, id, frame));
        else if (_session.Tool == EditTool.RateStretch) TryEdit("Rate stretch", p => TimelineEdits.RateStretch(p, id, frame));
        else if (press.Part is HitPart.Head or HitPart.Tail) TryEdit(_session.Tool == EditTool.Ripple ? "Ripple trim" : "Trim clip", p => TimelineEdits.Trim(p, id, frame, press.Part == HitPart.Head, _session.Tool == EditTool.Ripple, _session.LinkedSelection));
        else TryEdit("Move clips", p => TimelineEdits.Move(p, selection, delta, target));
        e.Handled = true;
    }
    private void Wheel(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this); double delta = point.Properties.MouseWheelDelta;
        if (e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control)) Geometry.ZoomAt(Math.Pow(1.2, delta / 120), point.Position.X);
        else if (e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift)) Geometry.ScrollY = Math.Clamp(Geometry.ScrollY - delta / 3, 0, Math.Max(0, _session.Project.Tracks.Count * Geometry.TrackHeight - ActualHeight + Geometry.RulerHeight));
        else Geometry.ScrollFrame = Math.Max(0, Geometry.ScrollFrame - delta / (Geometry.PixelsPerFrame * 3));
        Invalidate(); e.Handled = true;
    }
    private void Draw(SKCanvas canvas, Size area)
    {
        float width = (float)area.Width, height = (float)area.Height; canvas.Clear(Palette.Parse("#202124"));
        var p = _session.Project; var tracks = Geometry.Display(p); float header = (float)Geometry.HeaderWidth, ruler = (float)Geometry.RulerHeight;
        using var font = new SKFont(Studio.Typeface, 11); using var small = new SKFont(Studio.Typeface, 10); using var text = Palette.Paint(Studio.Ink); using var muted = Palette.Paint(Studio.Muted); using var line = Palette.Paint("#36383D", true); using var blue = Palette.Paint(Studio.Accent);
        canvas.Save(); canvas.ClipRect(new(header, 0, width, height));
        using (var band = Palette.Paint("#333D4B")) { var end = p.OutPoint ?? _session.Index.Duration; canvas.DrawRect((float)Geometry.X(p.InPoint), 0, (float)((end - p.InPoint) * Geometry.PixelsPerFrame), 8, band); }
        long[] ticks = [1, 2, 4, 6, 12, 24, 48, 96, 240, 480, 960, 2400, 4800, 9600, 24000, 48000];
        long step = ticks.FirstOrDefault(t => t * Geometry.PixelsPerFrame >= 95, 48000);
        long start = Math.Max(0, (long)(Geometry.ScrollFrame / step) * step);
        for (long frame = start; Geometry.X(frame) < width + 100; frame += step)
        {
            float x = (float)Geometry.X(frame); canvas.DrawLine(x, 34, x, ruler, line); canvas.DrawText(Timecode.Format(frame, p.FrameRate), x + 5, 31, small, muted);
            for (int sub = 1; sub < 4; sub++) { float sx = x + (float)(step * Geometry.PixelsPerFrame * sub / 4); canvas.DrawLine(sx, ruler - 7, sx, ruler, line); }
        }
        foreach (var marker in p.Markers)
        {
            float x = (float)Geometry.X(marker.Frame); if (x < header - 10 || x > width + 10) continue;
            using var mp = Palette.Paint(marker.Color); using var shape = new SKPath(); shape.MoveTo(x - 4, 9); shape.LineTo(x + 4, 9); shape.LineTo(x + 4, 15); shape.LineTo(x, 19); shape.LineTo(x - 4, 15); shape.Close(); canvas.DrawPath(shape, mp);
        }
        canvas.Restore();
        for (int i = 0; i < tracks.Length; i++)
        {
            var track = tracks[i]; float y = (float)Geometry.Y(i), th = (float)Geometry.TrackHeight;
            if (y + th < ruler || y > height) continue;
            canvas.Save(); canvas.ClipRect(new(0, ruler, width, height));
            using (var row = Palette.Paint(track.Kind == TrackKind.Audio ? "#23262A" : "#25262A")) canvas.DrawRect(0, y, width, th, row);
            canvas.DrawLine(0, y + th, width, y + th, line);
            canvas.Save(); canvas.ClipRect(new(header, ruler, width, height));
            var ti = _session.Index.Tracks.First(t => t.Track.Id == track.Id);
            foreach (var clip in ti.Visible(Geometry.ScrollFrame, Geometry.ScrollFrame + Math.Max(1, (width - header) / Geometry.PixelsPerFrame)))
            {
                float x = (float)Geometry.X(clip.Start), cw = (float)(clip.Duration * Geometry.PixelsPerFrame);
                if (x + cw < header || x > width) continue;
                var asset = _session.Index.Assets[clip.AssetId]; var color = Palette.Parse(asset.Color); if (track.Muted || !clip.Enabled) color = color.WithAlpha(80);
                var rect = new SKRect(x + 1, y + 4, x + Math.Max(3, cw) - 1, y + th - 4);
                using var fill = new SKPaint { Color = color }; canvas.DrawRoundRect(rect, 2, 2, fill);
                canvas.Save(); canvas.ClipRect(rect);
                using (var strip = new SKPaint { Color = SKColors.Black.WithAlpha(38) }) canvas.DrawRect(rect.Left, rect.Top, rect.Width, 17, strip);
                using var ink = Palette.Paint("#161D26"); if (rect.Width > 30) canvas.DrawText(clip.Name, Math.Max(rect.Left + 7, header + 4), y + 17, font, ink);
                if (track.Kind == TrackKind.Audio && asset.Peaks.Length > 0)
                {
                    using var wave = new SKPaint { Color = Palette.Parse("#285444"), StrokeWidth = 1.2f };
                    double firstX = Math.Max(rect.Left, header), endX = Math.Min(rect.Right, width);
                    for (double sx = firstX; sx < endX; sx += 2)
                    {
                        double local = (sx - x) / Geometry.PixelsPerFrame; double source = clip.SourceIn + p.FrameRate.Seconds((long)local) * clip.Speed;
                        int index = Math.Clamp((int)(source / asset.DurationSeconds * asset.Peaks.Length), 0, asset.Peaks.Length - 1);
                        float peak = asset.Peaks[index] * (th - 27) * .43f; float center = y + th * .64f;
                        canvas.DrawLine((float)sx, center - peak, (float)sx, center + peak, wave);
                    }
                }
                else if (track.Kind != TrackKind.Audio && cw > 25)
                {
                    using var tint = new SKPaint { Color = SKColors.White.WithAlpha(15) }; canvas.DrawRect(x + 5, y + 25, Math.Max(1, cw - 10), th - 34, tint);
                    if (clip.Effects.Values.Any(v => v.Keys.Count > 0)) { using var key = Palette.Paint("#E7E9FF"); canvas.DrawCircle(rect.Right - 9, y + th - 13, 2.5f, key); }
                    if (Math.Abs(clip.Speed - 1) > .001 && cw > 95) canvas.DrawText($"{clip.Speed:0.##}x", rect.Right - 37, y + th - 12, small, ink);
                }
                if (clip.Effects.FadeIn > 0 || clip.Effects.FadeOut > 0)
                {
                    using var fade = Palette.Paint("#F3E2B6", true, 1.2f);
                    float fy = y + th - 6;
                    canvas.DrawLine(x, fy, x + (float)(Math.Min(clip.Duration, clip.Effects.FadeIn) * Geometry.PixelsPerFrame), y + 23, fade);
                    canvas.DrawLine(x + cw - (float)(Math.Min(clip.Duration, clip.Effects.FadeOut) * Geometry.PixelsPerFrame), y + 23, x + cw, fy, fade);
                }
                canvas.Restore();
                if (_session.Selection.Contains(clip.Id)) { using var selected = Palette.Paint("#F4F6FD", true, 1.6f); canvas.DrawRoundRect(rect, 2, 2, selected); }
                if (track.Locked) { using var hatch = new SKPaint { Color = SKColors.Black.WithAlpha(55), StrokeWidth = 1 }; canvas.Save(); canvas.ClipRect(rect); for (float hx = Math.Max(rect.Left - th, header - th); hx < Math.Min(rect.Right, width); hx += 10) canvas.DrawLine(hx, rect.Bottom, hx + th, rect.Top, hatch); canvas.Restore(); }
            }
            foreach (var transition in ti.Transitions)
            {
                float tx = (float)Geometry.X(transition.Range.Start), tw = (float)(transition.Range.Duration * Geometry.PixelsPerFrame);
                if (tx + tw < header || tx > width) continue;
                using var fill = Palette.Paint("#D1A857"); canvas.DrawRect(tx, y + th - 15, Math.Max(3, tw), 9, fill);
                using var edge = Palette.Paint("#5B4B30", true); canvas.DrawLine(tx, y + th - 7, tx + tw, y + th - 14, edge);
            }
            canvas.Restore();
            using (var head = Palette.Paint("#292B30")) canvas.DrawRect(0, y, header - 1, th, head);
            using (var patch = Palette.Paint(track.Id == TargetTrackId ? "#3B6595" : "#35373D")) canvas.DrawRect(25, y + 9, 27, 25, patch);
            canvas.DrawText(track.Name, 31, y + 26, font, text);
            DrawHeaderFlag(canvas, track.Locked ? "L" : "·", 66, y + 25, track.Locked, font);
            DrawHeaderFlag(canvas, track.Kind == TrackKind.Audio ? "M" : "V", 93, y + 25, track.Muted, font);
            DrawHeaderFlag(canvas, track.Kind == TrackKind.Audio ? "S" : "↔", 120, y + 25, track.Kind == TrackKind.Audio ? track.Solo : track.SyncLock, font);
            canvas.DrawLine(header - 1, y, header - 1, y + th, line); canvas.Restore();
        }
        using (var headerPaint = Palette.Paint("#202124")) canvas.DrawRect(0, 0, header, ruler, headerPaint);
        using var timeFont = new SKFont(Studio.Typeface, 16); canvas.DrawText(Timecode.Format(_session.Playhead, p.FrameRate), 14, 24, timeFont, blue);
        canvas.DrawText($"{p.FrameRate.Value:0.###} fps", 14, 43, small, muted);
        canvas.DrawLine(0, ruler, width, ruler, line);
        float playX = (float)Geometry.X(_session.Playhead);
        if (playX >= header && playX <= width)
        {
            using var playLine = Palette.Paint(Studio.Accent, true, 1.3f); canvas.DrawLine(playX, 39, playX, height, playLine);
            using var triangle = new SKPath(); triangle.MoveTo(playX - 5, 37); triangle.LineTo(playX + 5, 37); triangle.LineTo(playX + 5, 43); triangle.LineTo(playX, 48); triangle.LineTo(playX - 5, 43); triangle.Close(); canvas.DrawPath(triangle, blue);
        }
        if (_dragging && _press?.ClipId is { } clipId && _session.Tool != EditTool.Hand)
        {
            var clip = _session.Index.Clips[clipId].Clip; int index = Array.FindIndex(tracks, t => t.Id == _session.Index.Clips[clipId].Track.Id);
            long from = _press.Part == HitPart.Head ? _dragFrame : _press.Part == HitPart.Body ? clip.Start + _dragFrame - _press.Frame : clip.Start;
            long to = _press.Part == HitPart.Tail ? _dragFrame : from + clip.Duration;
            using var ghost = new SKPaint { Color = Palette.Parse(Studio.Accent).WithAlpha(60) };
            using var outline = Palette.Paint(Studio.Accent, true, 1.5f);
            var rect = new SKRect((float)Geometry.X(from), (float)Geometry.Y(index) + 3, (float)Geometry.X(to), (float)Geometry.Y(index) + (float)Geometry.TrackHeight - 3);
            canvas.DrawRect(rect, ghost); canvas.DrawRect(rect, outline);
        }
    }
    private static void DrawHeaderFlag(SKCanvas canvas, string label, float x, float y, bool active, SKFont font)
    {
        using var ink = Palette.Paint(active ? Studio.Accent : Studio.Muted); canvas.DrawText(label, x, y, SKTextAlign.Center, font, ink);
    }
}
