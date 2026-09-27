using System.Globalization;
using VideoSpace.Documents;
using VideoSpace.Effects;

namespace VideoSpace.Workbench;

public sealed partial class StudioWorkbench
{
    private TextBlock? _exportProgress;
    private void ShowOverlay(string title, UIElement body, params (string Label, Action Action)[] actions)
    {
        CloseOverlay(); Session.Playing = _sourcePlaying = false; _dialogOpen = true; _media.HideMonitors(true);
        var panel = Studio.Rows(new(50), Studio.Star(), new(56));
        panel.Background = Studio.Brush("#25272C"); panel.Width = Math.Min(640, Math.Max(320, ActualWidth - 40));
        panel.MaxHeight = Math.Max(250, ActualHeight - 70); panel.VerticalAlignment = VerticalAlignment.Center; panel.HorizontalAlignment = HorizontalAlignment.Center;
        var header = Studio.Columns(Studio.Star(), new(36)); header.Margin = new(20, 8, 12, 8);
        Studio.At(header, Studio.Text(title, 17));
        Studio.At(header, new StudioButton("", () => { if (_exporting) _media.CancelExport(); else CloseOverlay(); }, "close", "Close dialog"), column: 1);
        Studio.At(panel, header);
        Studio.At(panel, new ScrollViewer { Content = body, Padding = new(20, 6, 20, 10), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }, 1);
        var footer = Studio.Row(); footer.HorizontalAlignment = HorizontalAlignment.Right; footer.Margin = new(16, 10, 16, 12);
        foreach (var (label, action) in actions)
        {
            var button = new StudioButton(label, () => Run(action), commandId: label) { Padding = new(16, 6, 16, 6), BorderBrush = Studio.Brush("#535861"), BorderThickness = new(1), MinWidth = 90 };
            _commands["Dialog " + label] = button; footer.Children.Add(button);
        }
        Studio.At(panel, footer, 2);
        _overlay = new Border
        {
            Background = Studio.Brush("#CD0B0C10"),
            Child = new Border { BorderBrush = Studio.Brush("#565B65"), BorderThickness = new(1), CornerRadius = new(5), Child = panel, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        Studio.At(_root, _overlay, rowSpan: 4); Canvas.SetZIndex(_overlay, 100); PublishDiagnostics();
    }
    private void CloseOverlay()
    {
        if (_overlay is not null) _root.Children.Remove(_overlay);
        _overlay = null; _dialogOpen = false; _exportProgress = null;
        foreach (var key in _commands.Keys.Where(k => k.StartsWith("Dialog ", StringComparison.Ordinal)).ToArray()) _commands.Remove(key);
        _media.HideMonitors(false); BrowserStateVisible();
        if (!_disposed && _root is not null) Focus(FocusState.Programmatic);
    }
    private void BrowserStateVisible()
    {
        VideoSpace.Media.BrowserState.Visible("source", _sourcePanel.Selected == "Source");
        VideoSpace.Media.BrowserState.Visible("program", true);
    }
    private static StackPanel Form(params UIElement[] children)
    {
        var panel = new StackPanel { Spacing = 10 };
        foreach (var child in children) panel.Children.Add(child);
        return panel;
    }
    private static TextBlock Paragraph(string text)
    {
        var label = Studio.Text(text, 12, Studio.Muted); label.TextWrapping = TextWrapping.Wrap; label.LineHeight = 19; return label;
    }
    private void ShowMenu(string title, (string Label, Action Action)[] items)
    {
        CloseOverlay(); Session.Playing = _sourcePlaying = false; _dialogOpen = true; _media.HideMonitors(true);
        var list = new StackPanel { Spacing = 1, Margin = new(4) };
        foreach (var (label, action) in items)
            list.Children.Add(new StudioButton(label, () => { CloseOverlay(); Run(action); }) { HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Height = 30 });
        var anchor = _commands.TryGetValue("Menu " + title, out var button) ? Studio.Bounds(button) : new Rect(8, 0, 80, 25);
        double x = Math.Clamp(anchor.X, 8, Math.Max(8, ActualWidth - 292));
        double y = Math.Max(0, anchor.Bottom + 2);
        var menu = new Border
        {
            Width = 280, MaxHeight = Math.Max(100, ActualHeight - y - 12), Margin = new(x, y, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
            Background = Studio.Brush("#292B30"), BorderBrush = Studio.Brush("#555B65"), BorderThickness = new(1), CornerRadius = new(3),
            Child = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }
        };
        _overlay = new Border { Background = Studio.Brush("#00000000"), Child = menu };
        _overlay.PointerPressed += (_, e) =>
        {
            var point = e.GetCurrentPoint(menu).Position;
            if (point.X < 0 || point.Y < 0 || point.X > menu.ActualWidth || point.Y > menu.ActualHeight) { CloseOverlay(); e.Handled = true; }
        };
        Studio.At(_root, _overlay, rowSpan: 4); Canvas.SetZIndex(_overlay, 100); PublishDiagnostics();
    }
    private void ConfirmNew()
    {
        ShowOverlay("New demo project", Paragraph("Replace the current project with the original NORTH demo? Save the current project first. Imported source media is not deleted."),
            ("Cancel", CloseOverlay), ("New project", () =>
            {
                CloseOverlay(); Session.Replace(SampleProject.Create()); _sourceAsset = "ridge"; _sourceIn = 0; _sourceOut = 240;
                _sourcePosition = 96; Timeline.TargetTrackId = "v1"; Timeline.Fit(); Session.Select("ridge-cut");
                _programPanel.SetCaption("Program: NORTH / Main edit", "Program: NORTH / Main edit");
                _timelinePanel.SetCaption("NORTH / Main edit", "NORTH / Main edit");
            }));
    }
    private void NewTitle()
    {
        TextBox name = Studio.Input("New title"), text = Studio.Input("YOUR TITLE"); text.AcceptsReturn = true; text.Height = 74;
        ShowOverlay("New graphic title", Form(Studio.Text("Name"), name, Studio.Text("Text"), text, Paragraph("Creates a reusable title asset and a five-second clip above the video tracks at the playhead.")),
            ("Cancel", CloseOverlay), ("Create title", () =>
            {
                if (string.IsNullOrWhiteSpace(text.Text)) throw new InvalidOperationException("Enter title text.");
                string? created = null;
                Session.Execute("Create title", p =>
                {
                    var asset = new MediaAsset { Name = name.Text, Text = text.Text, Kind = MediaKind.Title, Bin = "Graphics", Color = "#BE91C6", DurationSeconds = 3600 }; p.Assets.Add(asset);
                    var track = p.Tracks.LastOrDefault(t => t.Kind == TrackKind.Video && !t.Locked && !t.Clips.Any(c => c.Start < Session.Playhead + p.FrameRate.Frames(5) && c.End > Session.Playhead));
                    if (track is null) { track = new Track { Name = "V" + (p.Tracks.Count(t => t.Kind == TrackKind.Video) + 1), Kind = TrackKind.Video }; p.Tracks.Add(track); }
                    created = TimelineEdits.Insert(p, asset.Id, track.Id, Session.Playhead, 0, p.FrameRate.Frames(5), true);
                });
                CloseOverlay(); Session.Select(created); _sourcePanel.Select("Effect Controls");
            }));
    }
    private void AddMarker()
    {
        long frame = Session.Playhead; var name = Studio.Input("Marker " + (Session.Project.Markers.Count + 1));
        ShowOverlay("Sequence marker", Form(Paragraph(Timecode.Format(frame, Session.Project.FrameRate)), Studio.Text("Name"), name),
            ("Cancel", CloseOverlay), ("Add marker", () => { Edit("Add marker", p => p.Markers.Add(new(frame, name.Text))); CloseOverlay(); }));
    }
    private void AddCaption() => EditCaption(new(Session.Playhead, Math.Min(Session.Project.Duration + 72, Session.Playhead + 72), ""), true);
    private void EditCaption(Caption caption, bool create = false)
    {
        TextBox start = Studio.Input(Timecode.Format(caption.Start, Session.Project.FrameRate)), end = Studio.Input(Timecode.Format(caption.End, Session.Project.FrameRate));
        var text = Studio.Input(caption.Text); text.AcceptsReturn = true; text.Height = 90;
        ShowOverlay(create ? "Add caption" : "Edit caption", Form(Studio.Text("Start timecode"), start, Studio.Text("End timecode"), end, Studio.Text("Caption text"), text),
            ("Cancel", CloseOverlay), ("Save caption", () =>
            {
                var updated = new Caption(Timecode.Parse(start.Text, Session.Project.FrameRate), Timecode.Parse(end.Text, Session.Project.FrameRate), text.Text);
                if (updated.End <= updated.Start || string.IsNullOrWhiteSpace(updated.Text)) throw new InvalidOperationException("Enter text and a positive caption range.");
                Session.Execute(create ? "Add caption" : "Edit caption", p => { if (!create) p.Captions.RemoveAll(c => c == caption); p.Captions.Add(updated); }); CloseOverlay();
            }));
    }
    private void SequenceSettings()
    {
        var p = Session.Project; var name = Studio.Input(p.SequenceName); var projectName = Studio.Input(p.Name);
        TextBox width = Studio.Input(p.Width.ToString(CultureInfo.InvariantCulture)), height = Studio.Input(p.Height.ToString(CultureInfo.InvariantCulture));
        ShowOverlay("Sequence settings", Form(Studio.Text("Project name"), projectName, Studio.Text("Sequence name"), name, Studio.Text("Width / height in pixels"), Studio.Row(width, height),
            Paragraph($"Timebase: {p.FrameRate} fps. Use New project to select another rational timebase without silently retiming existing edits.")),
            ("Cancel", CloseOverlay), ("Save settings", () =>
            {
                int w = int.Parse(width.Text, CultureInfo.InvariantCulture), h = int.Parse(height.Text, CultureInfo.InvariantCulture);
                Session.Execute("Sequence settings", project => { project.Name = projectName.Text; project.SequenceName = name.Text; project.Width = w; project.Height = h; });
                _programPanel.SetCaption("Program: NORTH / Main edit", "Program: " + name.Text);
                _timelinePanel.SetCaption("NORTH / Main edit", name.Text); CloseOverlay();
            }));
    }
    private void ExportStill()
    {
        if (_media.Browser) _media.ExportStill();
        else RunAsync(() => _media.SaveBytesAsync(_renderer.Png(FramePlanner.Evaluate(Session.Project, Session.Playhead)), "VideoSpace-frame.png"));
    }
    private void ShowExport()
    {
        var p = Session.Project;
        var description = Paragraph($"{p.SequenceName}\n{p.Width} × {p.Height} · {p.FrameRate.Value:0.###} fps · {Timecode.Format(p.Duration, p.FrameRate)}\nSequence In/Out defines the video export range.");
        var options = new StackPanel { Spacing = 8 };
        void Choice(string title, string detail, Action action)
        {
            var row = new StackPanel { Spacing = 3, Margin = new(10) };
            row.Children.Add(new StudioButton(title, () => Run(action)) { HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left });
            row.Children.Add(Paragraph(detail)); options.Children.Add(new Border { Background = Studio.Brush("#2E3137"), Child = row });
        }
        Choice("Export video · WebM 720p", "Offline WebCodecs VP9/VP8 + Opus. One output frame per sequence frame, with explicit timestamps and mixed stereo PCM. Source decoding uses browser seeks. Maximum ten minutes; bounded media/muxer memory.", () => StartVideoExport(720));
        Choice("Export video · WebM 1080p", "The same offline frame/sample pipeline at 1080p. Requires browser support for VideoEncoder and AudioEncoder. Hardware WebGPU is preferred; software adapters use WebGL2.", () => StartVideoExport(1080));
        Choice("Save native project", "Lossless editing manifest. Media files remain local and are not embedded; import matching files to relink on another device.", () => { CloseOverlay(); SaveProject(); });
        Choice("Export still frame · PNG", "Current Program composition at sequence dimensions.", () => { CloseOverlay(); ExportStill(); });
        Choice("Export captions · SRT", "Timed captions in standard SubRip format.", () => { CloseOverlay(); RunAsync(() => _media.SaveTextAsync(SubRip.Write(p.Captions, p.FrameRate), SafeName(p.Name) + ".srt")); });
        Choice("Export edit decisions · EDL", "Cuts-only CMX3600 for the first video track. Effects, layers and speed changes are not represented.", () => { CloseOverlay(); RunAsync(() => _media.SaveTextAsync(EdlWriter.Write(p), SafeName(p.Name) + ".edl")); });
        Choice("Export synthesized demo audio · WAV", "Managed PCM export for generated tone tracks. Imported audio is rejected here; it is supported in offline WebM export.", ExportWav);
        ShowOverlay("Export", Form(description, options), ("Cancel", CloseOverlay));
    }
    private void StartVideoExport(int height)
    {
        if (!_media.Browser) { ShowStatus("Native video encoding is not installed. Use the browser version for offline WebM export.", true); return; }
        var text = Paragraph("Preparing decoders, GPU composition and offline encoders…");
        ShowOverlay("Exporting video", Form(text, Paragraph("Every output frame has an explicit timestamp; audio is mixed in sample-addressed blocks. Keep this page open until the download completes. Cancel discards the output without modifying the project.")),
            ("Cancel export", () => _media.CancelExport()));
        _exportProgress = text; _exporting = true; _media.ExportVideo(Session.Project, height);
    }
    private void ExportWav()
    {
        Run(() =>
        {
            var p = Session.Project;
            if (p.Tracks.Where(t => t.Kind == TrackKind.Audio && !t.Muted).SelectMany(t => t.Clips).Any(c => c.Enabled && p.Asset(c.AssetId).Source != "tone")) throw new InvalidOperationException("Managed WAV export supports generated audio. Use offline WebM for imported audio.");
            int sampleRate = 48000; long count = (long)Math.Ceiling(p.FrameRate.Seconds(p.Duration) * sampleRate);
            if (count > sampleRate * 300L) throw new InvalidOperationException("Managed WAV export is limited to five minutes.");
            var samples = new float[checked((int)count * 2)];
            VideoSpace.Audio.AudioMixer.Mix(p, new Dictionary<string, VideoSpace.Audio.PcmAudio>(), 0, sampleRate, samples);
            byte[] bytes = VideoSpace.Audio.WavWriter.Write(new(sampleRate, 2, samples));
            CloseOverlay(); RunAsync(() => _media.SaveBytesAsync(bytes, SafeName(p.Name) + ".wav"));
        });
    }
    private void ShowHelp()
    {
        var body = new StackPanel { Spacing = 12 };
        body.Children.Add(Paragraph("VideoSpace is an independent local-first editor. It implements a working editing subset, not complete Adobe Premiere Pro feature, UI or project-format parity."));
        body.Children.Add(Studio.Text("Create, import and edit", 15));
        body.Children.Add(Paragraph("File → New project creates a blank sequence with your dimensions and rational frame rate. Import video, audio or images, then mark the Source range and use Clip → Insert / Overwrite or drag to a timeline track. Imported video receives linked audio. The NORTH demo contains original generated media."));
        body.Children.Add(Studio.Text("Timeline and clipboard", 15));
        body.Children.Add(Paragraph("V Selection · C Razor · B Ripple trim · N Rolling edit · Y Slip · R Rate stretch · H Hand. Drag clip bodies to move and edges to trim. Ctrl temporarily bypasses snapping. Ctrl+C/X/V copies, cuts and pastes clips, preserving effects, gaps and link groups. Cross-timebase paste is rejected. Clipboard data is session-local and does not include source media bytes."));
        body.Children.Add(Studio.Text("Keyboard", 15));
        body.Children.Add(Paragraph("Space play/pause · J/K/L shuttle · arrows one frame · Shift+arrows ten frames · Home/End bounds · I/O sequence In/Out · M marker · comma/period insert/overwrite · Ctrl+K split · Delete lift · Shift+Delete guarded ripple · Ctrl+Z undo · Ctrl+Shift+Z redo · Ctrl+S save · Ctrl+O open · Ctrl+I import · Ctrl+N new project."));
        body.Children.Add(Studio.Text("Effects and animation", 15));
        body.Children.Add(Paragraph("Select a clip and open Effect Controls. Type values or drag property labels. A diamond toggles a keyframe at the playhead. Presets remain editable. Clip opacity fades reveal lower tracks; dedicated transition objects and a full graph editor are not implemented."));
        body.Children.Add(Studio.Text("Offline video export", 15));
        body.Children.Add(Paragraph("WebCodecs renders each sequence frame with an explicit output timestamp and mixes imported/generated audio to stereo PCM before Opus encoding. Exports are cancellable and bounded to ten minutes, 256 MB compressed output and 256 MB decoded audio. Browser source seeks are not a guarantee of exact source-frame selection for arbitrary variable-rate codecs. Unsupported formats or offline sources fail explicitly."));
        body.Children.Add(Studio.Text("Storage and platform boundaries", 15));
        body.Children.Add(Paragraph("Media stays in local IndexedDB; quota and eviction can affect recovery. Save manifests regularly and retain original sources. Manifests do not embed media. Native hosts currently support images, titles, generated footage and document/still/WAV exports, not native video decode, live audio or video encoding. No .prproj/AAF, multicam, nesting, professional color management, plug-ins, transcription or collaboration is claimed."));
        ShowOverlay("VideoSpace · User guide", body, ("Close", CloseOverlay));
    }
}
