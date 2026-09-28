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
        var panel = Studio.Rows(new(50), Studio.Star(), new(56)); panel.Background = Studio.Brush("#25272C"); panel.Width = Math.Min(640, Math.Max(320, ActualWidth - 40)); panel.MaxHeight = Math.Max(250, ActualHeight - 70); panel.VerticalAlignment = VerticalAlignment.Center; panel.HorizontalAlignment = HorizontalAlignment.Center;
        var header = Studio.Columns(Studio.Star(), new(36)); header.Margin = new(20, 8, 12, 8); Studio.At(header, Studio.Text(title, 17)); Studio.At(header, new StudioButton("", () => { if (_exporting) _media.CancelExport(); else CloseOverlay(); }, "close", "Close dialog"), column: 1); Studio.At(panel, header);
        Studio.At(panel, new ScrollViewer { Content = body, Padding = new(20, 6, 20, 10), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }, 1);
        var footer = Studio.Row(); footer.HorizontalAlignment = HorizontalAlignment.Right; footer.Margin = new(16, 10, 16, 12);
        foreach (var (label, action) in actions)
        {
            var button = new StudioButton(label, () => Run(action), commandId: label) { Padding = new(16, 6, 16, 6), BorderBrush = Studio.Brush("#535861"), BorderThickness = new(1), MinWidth = 90 };
            _commands["Dialog " + label] = button; footer.Children.Add(button);
        }
        Studio.At(panel, footer, 2);
        _overlay = new Border { Background = Studio.Brush("#CD0B0C10"), Child = new Border { BorderBrush = Studio.Brush("#565B65"), BorderThickness = new(1), CornerRadius = new(5), Child = panel, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        Studio.At(_root, _overlay, rowSpan: 4); Canvas.SetZIndex(_overlay, 100); PublishDiagnostics();
    }
    private void CloseOverlay()
    {
        if (_overlay is not null) _root.Children.Remove(_overlay); _overlay = null; _dialogOpen = false; _exportProgress = null;
        foreach (var key in _commands.Keys.Where(k => k.StartsWith("Dialog ", StringComparison.Ordinal)).ToArray()) _commands.Remove(key);
        _media.HideMonitors(false); BrowserStateVisible();
        if (!_disposed && _root is not null) Focus(FocusState.Programmatic);
    }
    private void BrowserStateVisible()
    {
        VideoSpace.Media.BrowserState.Visible("source", _sourcePanel.Selected == "Source"); VideoSpace.Media.BrowserState.Visible("program", true);
    }
    private static StackPanel Form(params UIElement[] children)
    {
        var panel = new StackPanel { Spacing = 10 }; foreach (var child in children) panel.Children.Add(child); return panel;
    }
    private static TextBlock Paragraph(string text)
    {
        var label = Studio.Text(text, 12, Studio.Muted); label.TextWrapping = TextWrapping.Wrap; label.LineHeight = 19; return label;
    }
    private void ShowMenu(string title, (string Label, Action Action)[] items)
    {
        CloseOverlay(); Session.Playing = _sourcePlaying = false; _dialogOpen = true;
        _media.HideMonitors(true);
        double left = _commands.TryGetValue("Menu " + title, out var anchor) ? Studio.Bounds(anchor).X : 8;
        var list = new StackPanel { Spacing = 1, Padding = new(5) };
        foreach (var (label, action) in items)
            list.Children.Add(new StudioButton(label, () => { CloseOverlay(); Run(action); }) { HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Height = 30, Padding = new(10, 4, 18, 4) });
        var popup = new Border { Child = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, Width = 286, Height = items.Length * 31 + 12,
            MaxHeight = Math.Max(120, ActualHeight - 60), Background = Studio.Brush("#292C32"), BorderBrush = Studio.Brush("#555A65"), BorderThickness = new(1), CornerRadius = new(3),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new(Math.Clamp(left, 5, Math.Max(5, ActualWidth - 295)), 26, 0, 0) };
        var layer = new Grid(); var dismiss = new Border { Background = Studio.Brush("#01000000") };
        dismiss.PointerPressed += (_, e) => { CloseOverlay(); e.Handled = true; }; layer.Children.Add(dismiss); layer.Children.Add(popup);
        _overlay = new Border { Child = layer }; Studio.At(_root, _overlay, rowSpan: 4); Canvas.SetZIndex(_overlay, 100); PublishDiagnostics();
    }
    private void ConfirmNew()
    {
        ShowOverlay("New demo project", Paragraph("Replace the current project with the original NORTH demo? Save the current project first to keep a separate copy. Local imported media remains stored on this device."), ("Cancel", CloseOverlay), ("New project", () => { CloseOverlay(); Session.Replace(SampleProject.Create()); _sourceAsset = "ridge"; _sourceIn = 0; _sourceOut = 240; _sourcePosition = 96; Timeline.TargetTrackId = "v1"; Timeline.Fit(); Session.Select("ridge-cut"); }));
    }
    private void NewTitle()
    {
        TextBox name = Studio.Input("New title"), text = Studio.Input("YOUR TITLE"); text.AcceptsReturn = true; text.Height = 74;
        ShowOverlay("New graphic title", Form(Studio.Text("Name"), name, Studio.Text("Text"), text, Paragraph("Creates a reusable title asset and places a five-second clip above the video tracks at the playhead.")), ("Cancel", CloseOverlay), ("Create title", () =>
        {
            if (string.IsNullOrWhiteSpace(text.Text)) throw new InvalidOperationException("Enter title text."); string? created = null;
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
        ShowOverlay("Sequence marker", Form(Paragraph(Timecode.Format(frame, Session.Project.FrameRate)), Studio.Text("Name"), name), ("Cancel", CloseOverlay), ("Add marker", () => { Edit("Add marker", p => p.Markers.Add(new(frame, name.Text))); CloseOverlay(); }));
    }
    private void AddCaption() => EditCaption(new(Session.Playhead, Math.Min(Session.Project.Duration + 72, Session.Playhead + 72), ""), true);
    private void EditCaption(Caption caption, bool create = false)
    {
        TextBox start = Studio.Input(Timecode.Format(caption.Start, Session.Project.FrameRate)), end = Studio.Input(Timecode.Format(caption.End, Session.Project.FrameRate)); var text = Studio.Input(caption.Text); text.AcceptsReturn = true; text.Height = 90;
        ShowOverlay(create ? "Add caption" : "Edit caption", Form(Studio.Text("Start timecode"), start, Studio.Text("End timecode"), end, Studio.Text("Caption text"), text), ("Cancel", CloseOverlay), ("Save caption", () =>
        {
            var updated = new Caption(Timecode.Parse(start.Text, Session.Project.FrameRate), Timecode.Parse(end.Text, Session.Project.FrameRate), text.Text);
            if (updated.End <= updated.Start || string.IsNullOrWhiteSpace(updated.Text)) throw new InvalidOperationException("Enter text and a positive caption range.");
            Session.Execute(create ? "Add caption" : "Edit caption", p => { if (!create) p.Captions.RemoveAll(c => c == caption); p.Captions.Add(updated); }); CloseOverlay();
        }));
    }
    private void SequenceSettings()
    {
        var p = Session.Project; var name = Studio.Input(p.SequenceName); var projectName = Studio.Input(p.Name); TextBox width = Studio.Input(p.Width.ToString(CultureInfo.InvariantCulture)), height = Studio.Input(p.Height.ToString(CultureInfo.InvariantCulture));
        ShowOverlay("Sequence settings", Form(Studio.Text("Project name"), projectName, Studio.Text("Sequence name"), name, Studio.Text("Width / height in pixels"), Studio.Row(width, height), Paragraph($"Timebase: {p.FrameRate} fps. This version preserves the existing sequence timebase to avoid silently retiming edits. Create a blank project to choose a new timebase.")), ("Cancel", CloseOverlay), ("Save settings", () =>
        {
            int w = int.Parse(width.Text, CultureInfo.InvariantCulture), h = int.Parse(height.Text, CultureInfo.InvariantCulture);
            Session.Execute("Sequence settings", project => { project.Name = projectName.Text; project.SequenceName = name.Text; project.Width = w; project.Height = h; }); CloseOverlay();
        }));
    }
    private void ExportStill()
    {
        if (_media.Browser) _media.ExportStill(); else RunAsync(() => _media.SaveBytesAsync(_renderer.Png(Planner.Evaluate(Session.Playhead)), "VideoSpace-frame.png"));
    }
    private void ShowExport()
    {
        var p = Session.Project;
        var description = Paragraph($"{p.SequenceName}\n{p.Width} × {p.Height} · {p.FrameRate.Value:0.###} fps · {Timecode.Format(p.Duration, p.FrameRate)}\nSequence In/Out defines the video export range.");
        var options = new StackPanel { Spacing = 8 };
        void Choice(string title, string detail, Action action)
        {
            var row = new StackPanel { Spacing = 3, Margin = new(10) }; row.Children.Add(new StudioButton(title, action) { HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left }); row.Children.Add(Paragraph(detail)); options.Children.Add(new Border { Background = Studio.Brush("#2E3137"), Child = row });
        }
        Choice("Export video · WebM 720p", "Offline WebCodecs VP9/VP8 + Opus with explicit output cadence and sample-addressed audio. Source-frame selection still depends on browser seeking. Maximum ten-minute range and 256 MB encoded payload.", () => StartVideoExport(720));
        Choice("Export video · WebM 1080p", "Offline export at 1080p with nested sequences, transitions and camera cuts. Capability probing selects an available encoder.", () => StartVideoExport(1080));
        Choice("Save native project", "Lossless root editing manifest, including nested edits. Media files remain local and are not embedded.", () => { CloseOverlay(); SaveProject(); });
        Choice("Export still frame · PNG", "Current Program composition at sequence dimensions.", () => { CloseOverlay(); ExportStill(); });
        Choice("Export captions · SRT", "Timed captions from the active sequence in SubRip format.", () => { CloseOverlay(); RunAsync(() => _media.SaveTextAsync(SubRip.Write(p.Captions, p.FrameRate), SafeName(p.Name) + ".srt")); });
        Choice("Export edit decisions · EDL", "First-video-track cuts only. Transitions, nested sequences, camera groups and speed changes are rejected rather than silently flattened.", () => { CloseOverlay(); RunAsync(() => _media.SaveTextAsync(EdlWriter.Write(p), SafeName(p.Name) + ".edl")); });
        Choice("Export synthesized demo audio · WAV", "Managed PCM rendering of generated tone sources, including nested mixes. Imported PCM is not decoded by this export path; use offline WebM for imported audio.", ExportWav);
        ShowOverlay("Export", Form(description, options), ("Cancel", CloseOverlay));
    }
    private void StartVideoExport(int height)
    {
        if (!_media.Browser) { ShowStatus("The native host does not yet include a video encoder. Open the browser version for WebM export.", true); return; }
        var text = Paragraph("Preparing source decoders, offline PCM and WebCodecs…");
        ShowOverlay("Exporting video", Form(text, Paragraph("Each output frame is rendered explicitly. Cancellation discards the current output without changing edits.")), ("Cancel export", () => _media.CancelExport()));
        _exportProgress = text; _exporting = true; _media.ExportVideo(Session.Project, height);
    }
    private void ExportWav()
    {
        Run(() =>
        {
            var p = Session.Project; var mixer = new VideoSpace.Audio.PreparedAudioMixer(p);
            if (mixer.Sources.Any(a => a.Source != "tone")) throw new InvalidOperationException("This PCM export only supports generated demo audio. Imported audio must be exported through browser video export.");
            int sampleRate = 48000; long count = (long)Math.Ceiling(p.FrameRate.Seconds(p.Duration) * sampleRate);
            if (count > sampleRate * 300L) throw new InvalidOperationException("Managed WAV export is limited to five minutes.");
            var samples = new float[checked((int)count * 2)]; mixer.Mix(new Dictionary<string, VideoSpace.Audio.PcmAudio>(), 0, sampleRate, samples);
            byte[] bytes = VideoSpace.Audio.WavWriter.Write(new(sampleRate, 2, samples)); CloseOverlay(); RunAsync(() => _media.SaveBytesAsync(bytes, SafeName(p.Name) + ".wav"));
        });
    }
    private void ShowHelp()
    {
        var body = new StackPanel { Spacing = 12 };
        body.Children.Add(Paragraph("VideoSpace is an independent local-first nonlinear editor. It implements an expanding editing subset, not complete Adobe Premiere Pro feature or project-format compatibility."));
        body.Children.Add(Studio.Text("Start editing", 15));
        body.Children.Add(Paragraph("Import video, audio or images, or use NORTH. Select a Project asset to open Source; mark source In/Out, then insert/overwrite from Clip or drag to a compatible track. Imported video receives linked audio. File → New project creates a blank sequence with an explicit rational timebase."));
        body.Children.Add(Studio.Text("Timeline and keyboard", 15));
        body.Children.Add(Paragraph("V Selection · C Razor · B Ripple · N Rolling · Y Slip · R Rate stretch · H Hand. Drag bodies to move and edges to trim. Ctrl bypasses snapping. Ctrl+wheel zooms; wheel pans; Shift+wheel scrolls tracks. Track L locks; V/M toggles visibility/mute; S solos audio."));
        body.Children.Add(Paragraph("Space play/pause · J/K/L shuttle/stop · Left/Right one frame · Shift+Left/Right ten frames · Home/End limits · I/O range · M marker · comma/period insert/overwrite · Ctrl+K split · Delete lift · Shift+Delete guarded ripple delete · Ctrl+C/X/V copy/cut/paste · Ctrl+Z undo · Ctrl+Shift+Z redo · Ctrl+N new · Ctrl+S save · Ctrl+O open · Ctrl+I import."));
        body.Children.Add(Studio.Text("Effects and transitions", 15));
        body.Children.Add(Paragraph("Select a clip and open Effect Controls. Type values or scrub labels. Diamonds add/remove keys at the live playhead. Clip → Add / edit transition creates two-source dissolves, dips, wipes or audio crossfades with strict source-handle validation. No frame freezing hides exhausted handles. The complete keyframe graph editor remains unimplemented."));
        body.Children.Add(Studio.Text("Nested sequences and multicamera", 15));
        body.Children.Add(Paragraph("Sequence → Nest selection wraps a complete temporal selection while preserving editable tracks, transitions, captions and audio. Open nested sequence edits the child; Return to parent leaves it. Undo and saving always retain the root document. Unnest rejects outer transforms or trim that would change the result. Create multicamera source selects 2–16 sources with manual offsets and an audio policy. Insert the group, then use Effect Controls or keys 1–9 to record camera cuts."));
        body.Children.Add(Studio.Text("Offline video export", 15));
        body.Children.Add(Paragraph("Output uses WebCodecs and the same render graph as preview, with explicit per-frame timestamps and sample-clock PCM mixing. Queues and memory are bounded. This is not the older real-time recording path. Source selection still uses browser seeking, so VFR/sample-accurate source decoding is not universally guaranteed. Inspect output before delivery."));
        body.Children.Add(Studio.Text("Privacy and recovery", 15));
        body.Children.Add(Paragraph("Media stays in browser IndexedDB, never uploaded. Recovery depends on quota/storage policy and is not a backup. Save native project files and keep source media. Reimport matching filenames/lengths to relink. Native recovery retains manifests but not imported image bytes."));
        body.Children.Add(Studio.Text("Remaining compatibility boundary", 15));
        body.Children.Add(Paragraph("Native hosts share editing, image/title/generated footage rendering and document exports. Native video codecs and live audio output are absent. No .prproj/AAF, HDR/OCIO, professional plug-ins, tracking, transcription or collaboration is claimed. Manual multicamera cuts and editable nested sequences are implemented; automatic synchronization and simultaneous angle grids are not."));
        ShowOverlay("VideoSpace · User guide", body, ("Close", CloseOverlay));
    }
}
