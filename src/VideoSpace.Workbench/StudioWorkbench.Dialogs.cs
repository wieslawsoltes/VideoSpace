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
        var list = new StackPanel { Spacing = 2 };
        foreach (var (label, action) in items) list.Children.Add(new StudioButton(label, () => { CloseOverlay(); Run(action); }) { HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Height = 32 });
        ShowOverlay(title, list, ("Close", CloseOverlay));
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
        ShowOverlay("Sequence settings", Form(Studio.Text("Project name"), projectName, Studio.Text("Sequence name"), name, Studio.Text("Width / height in pixels"), Studio.Row(width, height), Paragraph($"Timebase: {p.FrameRate} fps. This version preserves the existing sequence timebase to avoid silently retiming edits. The core model supports rational rates for newly constructed projects.")), ("Cancel", CloseOverlay), ("Save settings", () =>
        {
            int w = int.Parse(width.Text, CultureInfo.InvariantCulture), h = int.Parse(height.Text, CultureInfo.InvariantCulture);
            Session.Execute("Sequence settings", project => { project.Name = projectName.Text; project.SequenceName = name.Text; project.Width = w; project.Height = h; }); CloseOverlay();
        }));
    }
    private void ExportStill()
    {
        if (_media.Browser) _media.ExportStill(); else RunAsync(() => _media.SaveBytesAsync(_renderer.Png(FramePlanner.Evaluate(Session.Project, Session.Playhead)), "VideoSpace-frame.png"));
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
        Choice("Export video · WebM 720p", "Browser GPU composition and browser-supported VP9/VP8 + Opus. Real-time recording; keep this tab visible. Limited to 10 minutes / 512 MB. Timing drift is possible; this is not a frame-accurate offline encoder.", () => StartVideoExport(720));
        Choice("Export video · WebM 1080p", "Same real-time recording path at 1080p. Codec availability and hardware acceleration depend on the browser.", () => StartVideoExport(1080));
        Choice("Save native project", "Lossless editing manifest. Media files remain local and are not embedded; import matching files to relink on another device.", () => { CloseOverlay(); SaveProject(); });
        Choice("Export still frame · PNG", "Current Program composition at sequence dimensions.", () => { CloseOverlay(); ExportStill(); });
        Choice("Export captions · SRT", "Timed captions in standard SubRip format.", () => { CloseOverlay(); RunAsync(() => _media.SaveTextAsync(SubRip.Write(p.Captions, p.FrameRate), SafeName(p.Name) + ".srt")); });
        Choice("Export edit decisions · EDL", "CMX3600 cuts-only export of the first video track. Layer compositing, effects, titles, captions and speed changes are not represented.", () => { CloseOverlay(); RunAsync(() => _media.SaveTextAsync(EdlWriter.Write(p), SafeName(p.Name) + ".edl")); });
        Choice("Export synthesized demo audio · WAV", "Exports generated tone tracks using the managed PCM engine. Imported media is not decoded by this export path and is rejected rather than silently omitted.", ExportWav);
        ShowOverlay("Export", Form(description, options), ("Cancel", CloseOverlay));
    }
    private void StartVideoExport(int height)
    {
        if (!_media.Browser) { ShowStatus("The native host does not yet include a video encoder. Open the browser version for WebM export.", true); return; }
        var text = Paragraph("Preparing local media and the video recorder…");
        ShowOverlay("Exporting video", Form(text, Paragraph("Do not hide this tab. Cancellation discards the current recording without affecting the project.")), ("Cancel export", () => _media.CancelExport()));
        _exportProgress = text; _exporting = true; _media.ExportVideo(Session.Project, height);
    }
    private void ExportWav()
    {
        Run(() =>
        {
            var p = Session.Project;
            if (p.Tracks.Where(t => t.Kind == TrackKind.Audio && !t.Muted).SelectMany(t => t.Clips).Any(c => c.Enabled && p.Asset(c.AssetId).Source != "tone")) throw new InvalidOperationException("This PCM export only supports generated demo audio. Imported audio must be exported through browser video export.");
            int sampleRate = 48000; long count = (long)Math.Ceiling(p.FrameRate.Seconds(p.Duration) * sampleRate);
            if (count > sampleRate * 300L) throw new InvalidOperationException("Managed WAV export is limited to five minutes.");
            var samples = new float[checked((int)count * 2)]; VideoSpace.Audio.AudioMixer.Mix(p, new Dictionary<string, VideoSpace.Audio.PcmAudio>(), 0, sampleRate, samples);
            byte[] bytes = VideoSpace.Audio.WavWriter.Write(new(sampleRate, 2, samples)); CloseOverlay(); RunAsync(() => _media.SaveBytesAsync(bytes, SafeName(p.Name) + ".wav"));
        });
    }
    private void ShowHelp()
    {
        var body = new StackPanel { Spacing = 12 };
        body.Children.Add(Paragraph("VideoSpace is an independent, local-first nonlinear editor. This initial alpha implements a working editing subset, not complete Adobe Premiere Pro feature or project-format compatibility."));
        body.Children.Add(Studio.Text("Start editing", 15));
        body.Children.Add(Paragraph("Import video, audio or images, or use the original NORTH demo. Select an asset in Project to open Source. Mark a source range with its In/Out buttons, then use Clip → Insert / Overwrite or drag the asset to a compatible timeline track. Imported video receives a linked audio clip."));
        body.Children.Add(Studio.Text("Timeline tools", 15));
        body.Children.Add(Paragraph("V Selection · C Razor · B Ripple trim · N Rolling edit · Y Slip · R Rate stretch · H Hand. Drag a clip body to move it. Drag its left/right edge to trim. Ctrl while dragging bypasses snapping. Track headers: L lock, V/M visibility/mute, S solo on audio, sync lock on video. Ctrl+wheel zooms around the pointer; wheel pans; Shift+wheel scrolls tracks."));
        body.Children.Add(Studio.Text("Keyboard", 15));
        body.Children.Add(Paragraph("Space play/pause · J/K/L reverse/stop/forward shuttle · Left/Right one frame · Shift+Left/Right ten frames · Home/End sequence limits · I/O sequence In/Out · M marker · comma insert source · period overwrite source · Ctrl+K split · Delete lift · Shift+Delete ripple delete · Ctrl+Z undo · Ctrl+Shift+Z or Ctrl+Y redo · Ctrl+S save · Ctrl+O open · Ctrl+I import."));
        body.Children.Add(Studio.Text("Effects and animation", 15));
        body.Children.Add(Paragraph("Select a timeline clip and open Effect Controls, Color or Audio. Type numeric values or drag labels to scrub. The diamond adds/removes a keyframe at the playhead. Presets are editable parameter changes, not baked pixels. Video opacity fades reveal lower tracks; true transition objects are not implemented yet."));
        body.Children.Add(Studio.Text("Storage and privacy", 15));
        body.Children.Add(Paragraph("Media remains in this browser's IndexedDB and is never uploaded. Recovery depends on browser quota and storage policies. Save a native project file regularly; it contains metadata but not media bytes. Relink offline media by importing the same name and byte length. Do not rely on browser recovery as your only backup."));
        body.Children.Add(Studio.Text("Platform boundaries", 15));
        body.Children.Add(Paragraph("Browser: real local video/audio decoding, WebGPU or WebGL2 composition, Web Audio playback, real-time WebM export. Desktop: shared editing UI, Skia composition for images/titles/generated footage and document/still/WAV exports. Native video decoding, live native audio output and native video encoding are not included. Codec support is browser-dependent; reverse video seeks are not guaranteed frame-accurate."));
        body.Children.Add(Studio.Text("Known parity gaps", 15));
        body.Children.Add(Paragraph("No .prproj/AAF import, multicam, nested sequences, adjustment layers, full transitions, motion tracking, professional audio plug-ins, HDR/OCIO color pipeline, proxies, collaboration, caption speech recognition, or frame-accurate offline video encoding. The layout follows familiar professional conventions with original icons and sample footage."));
        ShowOverlay("VideoSpace · User guide", body, ("Close", CloseOverlay));
    }
}
