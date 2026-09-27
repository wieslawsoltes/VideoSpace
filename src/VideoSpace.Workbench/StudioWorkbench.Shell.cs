using VideoSpace.Documents;
using VideoSpace.Effects;
using Windows.System;

namespace VideoSpace.Workbench;

public sealed partial class StudioWorkbench
{
    private StudioButton Command(string text, string id, Action action, string? icon = null)
    {
        var button = new StudioButton(text, () => Run(action), icon, id); _commands[id] = button; return button;
    }
    private UIElement BuildMenu()
    {
        var menu = Studio.Row(); menu.Spacing = 0; menu.Margin = new(6, 0, 0, 0);
        menu.Children.Add(Command("File", "Menu File", () => ShowMenu("File", [("New demo project", () => ConfirmNew()), ("Open project / captions…", () => RunAsync(_media.OpenAsync)), ("Import media…", () => RunAsync(_media.ImportAsync)), ("Save project…", SaveProject), ("Export…", ShowExport)])));
        menu.Children.Add(Command("Edit", "Menu Edit", () => ShowMenu("Edit", [("Undo", () => Session.Undo()), ("Redo", () => Session.Redo()), ("Add edit at playhead", Split), ("Delete", () => Delete(false)), ("Ripple delete", () => Delete(true)), ("Link selected clips", () => Edit("Link clips", p => TimelineEdits.Link(p, Session.Selection, false))), ("Unlink selected clips", () => Edit("Unlink clips", p => TimelineEdits.Link(p, Session.Selection, true)))])));
        menu.Children.Add(Command("Clip", "Menu Clip", () => ShowMenu("Clip", [("Insert source", () => InsertSource(false)), ("Overwrite source", () => InsertSource(true)), ("Create title…", NewTitle), ("Apply default fade", () => ApplyPreset("Fade in / out")), ("Enable / disable selected", () => Edit("Toggle clips", p => { foreach (var id in Session.Selection) p.Clip(id).Enabled ^= true; }))])));
        menu.Children.Add(Command("Sequence", "Menu Sequence", () => ShowMenu("Sequence", [("Sequence settings…", SequenceSettings), ("Add video track", () => AddTrack(TrackKind.Video)), ("Add audio track", () => AddTrack(TrackKind.Audio)), ("Add marker…", AddMarker), ("Clear In / Out", () => Edit("Clear sequence range", p => { p.InPoint = 0; p.OutPoint = null; })), ("Fit timeline", TimelineFit)])));
        menu.Children.Add(Command("Window", "Menu Window", () => ShowMenu("Window", [("Editing workspace", () => SetWorkspace("Editing")), ("Color workspace", () => SetWorkspace("Color")), ("Audio workspace", () => SetWorkspace("Audio")), ("Captions workspace", () => SetWorkspace("Captions")), ("History panel", () => _projectPanel.Select("History"))])));
        menu.Children.Add(Command("Help", "Help", ShowHelp));
        return menu;
    }
    private UIElement BuildNavigation()
    {
        var row = Studio.Columns(new(300), Studio.Star(), new(376)); row.Margin = new(12, 0, 12, 0);
        var brand = new Border { Background = Studio.Brush("#292951"), BorderBrush = Studio.Brush("#8582CD"), BorderThickness = new(1), Width = 28, Height = 28, CornerRadius = new(4), Child = new TextBlock { Text = "Vs", FontSize = 16, FontFamily = Studio.Font, Foreground = Studio.Brush("#C8C5FF"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        var left = Studio.Row(brand, Command("Import", "Import", () => RunAsync(_media.ImportAsync)), Command("Edit", "Edit workspace", () => SetWorkspace("Editing")), Command("Export", "Export", ShowExport)); left.Spacing = 12; Studio.At(row, left);
        _projectTitle.HorizontalAlignment = HorizontalAlignment.Center; Studio.At(row, _projectTitle, column: 1);
        var workspaces = Studio.Row(); workspaces.HorizontalAlignment = HorizontalAlignment.Right;
        foreach (var name in new[] { "Editing", "Color", "Effects", "Audio", "Captions" }) workspaces.Children.Add(Command(name, "Workspace " + name, () => SetWorkspace(name)));
        Studio.At(row, workspaces, column: 2); return row;
    }
    private UIElement BuildTools()
    {
        var stack = new StackPanel { Spacing = 1, Margin = new(3, 6, 2, 0) };
        var icons = new Dictionary<EditTool, string> { [EditTool.Selection] = "select", [EditTool.Razor] = "razor", [EditTool.Ripple] = "ripple", [EditTool.Rolling] = "roll", [EditTool.Slip] = "slip", [EditTool.RateStretch] = "stretch", [EditTool.Hand] = "hand" };
        foreach (var (tool, icon) in icons) stack.Children.Add(Command("", "Tool " + tool, () => { Session.Tool = tool; Session.Notify(); }, icon));
        stack.Children.Add(new Border { Height = 1, Background = Studio.Brush("#45484E"), Margin = new(5, 6, 5, 5) });
        stack.Children.Add(Command("", "Snapping", () => { Session.Snapping ^= true; Session.Notify(); }, "snap"));
        stack.Children.Add(Command("", "Linked selection", () => { Session.LinkedSelection ^= true; Session.Notify(); }, "link"));
        stack.Children.Add(Command("", "Zoom in", () => Timeline.Zoom(1.35), "plus"));
        stack.Children.Add(Command("", "Zoom out", () => Timeline.Zoom(1 / 1.35), "minus"));
        return new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }
    private void SetWorkspace(string workspace)
    {
        _workspace = workspace;
        foreach (var name in new[] { "Editing", "Color", "Effects", "Audio", "Captions" }) if (_commands.TryGetValue("Workspace " + name, out var b)) b.SetActive(name == workspace);
        if (workspace == "Editing") { _sourcePanel.Select("Source"); _projectPanel.Select("Project"); }
        else if (workspace == "Captions") { _sourcePanel.Select("Source"); _projectPanel.Select("Captions"); }
        else { _sourcePanel.Select("Effect Controls"); _projectPanel.Select(workspace == "Effects" || workspace == "Color" ? "Effects" : "Project"); }
        BuildInspector(); ShowStatus(workspace + " workspace");
    }
    private void Edit(string name, Action<VideoProject> edit) => Run(() => { Session.Execute(name, edit); ShowStatus(name); });
    private void Seek(long frame) { Session.Playing = false; Session.Seek(frame); _playPosition = Session.Playhead; }
    private void TogglePlayback()
    {
        if (_dialogOpen || _exporting) return; _media.UnlockAudio();
        if (!Session.Playing && Session.Playhead >= Session.Project.Duration - 1) { Session.Playhead = 0; _playPosition = 0; }
        Session.Playing ^= true; Session.PlaybackRate = 1; _playPosition = Session.Playhead; _lastTime = _clock.Elapsed.TotalSeconds; _sourcePlaying = false; RefreshFrames();
    }
    private void ProgramCommand(string command)
    {
        switch (command)
        {
            case "Play": TogglePlayback(); break;
            case "Previous frame": Seek(Session.Playhead - 1); break;
            case "Next frame": Seek(Session.Playhead + 1); break;
            case "Mark in": Edit("Mark sequence In", p => { p.InPoint = Session.Playhead; if (p.OutPoint <= p.InPoint) p.OutPoint = null; }); break;
            case "Mark out": Edit("Mark sequence Out", p => p.OutPoint = Math.Max(p.InPoint + 1, Session.Playhead + 1)); break;
            case "Add marker": AddMarker(); break;
            case "Snapshot": ExportStill(); break;
        }
    }
    private void SourceCommand(string command)
    {
        switch (command)
        {
            case "Play": _sourcePlaying ^= true; Session.Playing = false; _lastTime = _clock.Elapsed.TotalSeconds; break;
            case "Previous frame": _sourcePosition = Math.Max(0, _sourcePosition - 1); _sourcePlaying = false; break;
            case "Next frame": _sourcePosition = Math.Min(SourceLength - 1, _sourcePosition + 1); _sourcePlaying = false; break;
            case "Mark in": _sourceIn = (long)_sourcePosition; _sourceOut = Math.Max(_sourceIn + 1, _sourceOut); ShowStatus("Source In: " + Timecode.Format(_sourceIn, Session.Project.FrameRate)); break;
            case "Mark out": _sourceOut = Math.Max(_sourceIn + 1, (long)_sourcePosition + 1); ShowStatus("Source Out: " + Timecode.Format(_sourceOut, Session.Project.FrameRate)); break;
            case "Add marker": AddMarker(); break;
            case "Snapshot": ExportStill(); break;
        }
        RefreshFrames();
    }
    private void OpenSource(string id)
    {
        if (!Session.Project.Assets.Any(a => a.Id == id)) return;
        _sourceAsset = id; _sourcePosition = 0; _sourceIn = 0; _sourceOut = Math.Min(SourceLength, Session.Project.FrameRate.Frames(10)); _sourcePlaying = false; _sourcePanel.Select("Source"); RefreshFrames();
    }
    private void TimelineFit() => Timeline.Fit();
    private void SaveProject() => RunAsync(() => _media.SaveTextAsync(ProjectFile.Save(Session.Project), SafeName(Session.Project.Name) + ProjectFile.Extension));
    private static string SafeName(string name) => string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c));
    private void Split()
    {
        var ids = Session.Selection.Count > 0 ? Session.Selection.ToArray() : Session.Project.Tracks.Where(t => !t.Locked).SelectMany(t => t.Clips).Where(c => c.Contains(Session.Playhead)).Select(c => c.Id).ToArray();
        Edit("Add edit", p => TimelineEdits.Split(p, ids, Session.Playhead));
    }
    private void Delete(bool ripple) { var ids = Session.Selection.ToArray(); if (ids.Length > 0) Edit(ripple ? "Ripple delete" : "Delete clips", p => TimelineEdits.Delete(p, ids, ripple)); }
    private void ApplyPreset(string name)
    {
        if (Session.Selection.Count == 0) { ShowStatus("Select a timeline clip first.", true); return; }
        Edit(name, p => { foreach (var id in Session.Selection) { TimelineEdits.RequireUnlocked(p.TrackFor(id)); EffectPresets.Apply(p.Clip(id), name, p.FrameRate); } });
    }
    private void InsertSource(bool overwrite) => InsertAsset(_sourceAsset, Timeline.TargetTrackId, Session.Playhead, overwrite, true);
    private void InsertAsset(string assetId, string trackId, long frame, bool overwrite, bool sourceRange = false)
    {
        Run(() =>
        {
            var asset = Session.Project.Asset(assetId); var chosen = Session.Project.Tracks.FirstOrDefault(t => t.Id == trackId);
            bool audioOnly = asset.Kind == MediaKind.Audio;
            if (chosen is null || (audioOnly && chosen.Kind != TrackKind.Audio) || (!audioOnly && chosen.Kind == TrackKind.Audio)) chosen = Session.Project.Tracks.FirstOrDefault(t => t.Kind == (audioOnly ? TrackKind.Audio : TrackKind.Video) && !t.Locked);
            if (chosen is null) throw new InvalidOperationException("Add an unlocked compatible track first.");
            long available = Session.Project.FrameRate.Frames(asset.DurationSeconds), from = sourceRange ? _sourceIn : 0;
            long duration = Math.Max(1, Math.Min(available - from, sourceRange ? _sourceOut - _sourceIn : Session.Project.FrameRate.Frames(Math.Min(10, asset.DurationSeconds))));
            string? inserted = null;
            Session.Execute(overwrite ? "Overwrite source" : "Insert source", p =>
            {
                inserted = TimelineEdits.Insert(p, assetId, chosen.Id, Math.Max(0, frame), p.FrameRate.Seconds(from), duration, overwrite);
                if (!audioOnly && asset.Kind == MediaKind.Video && asset.HasAudio)
                {
                    var audioTrack = p.Tracks.FirstOrDefault(t => t.Kind == TrackKind.Audio && !t.Locked && !t.Clips.Any(c => c.Start < frame + duration && c.End > frame));
                    if (audioTrack is null) { audioTrack = new Track { Name = "A" + (p.Tracks.Count(t => t.Kind == TrackKind.Audio) + 1), Kind = TrackKind.Audio }; p.Tracks.Add(audioTrack); }
                    string audio = TimelineEdits.Insert(p, assetId, audioTrack.Id, Math.Max(0, frame), p.FrameRate.Seconds(from), duration, true);
                    TimelineEdits.Link(p, [inserted, audio], false);
                }
            });
            Session.Select(inserted); Timeline.EnsurePlayheadVisible();
        });
    }
    private void AddTrack(TrackKind kind) => Edit("Add " + kind + " track", p => p.Tracks.Add(new Track { Name = (kind == TrackKind.Audio ? "A" : "V") + (p.Tracks.Count(t => t.Kind == kind) + 1), Kind = kind }));
    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.OriginalSource is TextBox || (_root.XamlRoot is not null && FocusManager.GetFocusedElement(_root.XamlRoot) is TextBox)) return;
        if (_dialogOpen) { if (e.Key == VirtualKey.Escape && !_exporting) CloseOverlay(); return; }
        bool ctrl = IsDown(VirtualKey.Control) || IsDown(VirtualKey.LeftWindows) || IsDown(VirtualKey.RightWindows), shift = IsDown(VirtualKey.Shift);
        bool handled = true;
        Run(() =>
        {
            if (ctrl)
            {
                switch (e.Key)
                {
                    case VirtualKey.S: SaveProject(); break;
                    case VirtualKey.O: RunAsync(_media.OpenAsync); break;
                    case VirtualKey.I: RunAsync(_media.ImportAsync); break;
                    case VirtualKey.Z: if (shift) Session.Redo(); else Session.Undo(); break;
                    case VirtualKey.Y: Session.Redo(); break;
                    case VirtualKey.K: Split(); break;
                    case VirtualKey.A: Session.Selection.Clear(); foreach (var c in Session.Project.Tracks.Where(t => !t.Locked).SelectMany(t => t.Clips)) Session.Selection.Add(c.Id); Session.Notify(); break;
                    default: handled = false; break;
                }
            }
            else
            {
                switch (e.Key)
                {
                    case VirtualKey.Space: TogglePlayback(); break;
                    case VirtualKey.V: Session.Tool = EditTool.Selection; break;
                    case VirtualKey.C: Session.Tool = EditTool.Razor; break;
                    case VirtualKey.B: Session.Tool = EditTool.Ripple; break;
                    case VirtualKey.N: Session.Tool = EditTool.Rolling; break;
                    case VirtualKey.Y: Session.Tool = EditTool.Slip; break;
                    case VirtualKey.R: Session.Tool = EditTool.RateStretch; break;
                    case VirtualKey.H: Session.Tool = EditTool.Hand; break;
                    case VirtualKey.S: Session.Snapping ^= true; break;
                    case VirtualKey.J: _media.UnlockAudio(); Session.PlaybackRate = Session.Playing && Session.PlaybackRate < 0 ? Math.Max(-8, Session.PlaybackRate * 2) : -1; Session.Playing = true; _playPosition = Session.Playhead; break;
                    case VirtualKey.K: Session.Playing = false; break;
                    case VirtualKey.L: _media.UnlockAudio(); Session.PlaybackRate = Session.Playing && Session.PlaybackRate > 0 ? Math.Min(8, Session.PlaybackRate * 2) : 1; Session.Playing = true; _playPosition = Session.Playhead; break;
                    case VirtualKey.Left: Seek(Session.Playhead - (shift ? 10 : 1)); break;
                    case VirtualKey.Right: Seek(Session.Playhead + (shift ? 10 : 1)); break;
                    case VirtualKey.Home: Seek(0); break;
                    case VirtualKey.End: Seek(Session.Project.Duration - 1); break;
                    case VirtualKey.Delete: case VirtualKey.Back: Delete(shift); break;
                    case VirtualKey.I: ProgramCommand("Mark in"); break;
                    case VirtualKey.O: ProgramCommand("Mark out"); break;
                    case VirtualKey.M: AddMarker(); break;
                    case VirtualKey.Add: Timeline.Zoom(1.35); break;
                    case VirtualKey.Subtract: Timeline.Zoom(1 / 1.35); break;
                    case VirtualKey.Escape: Session.Tool = EditTool.Selection; Session.Select(null); break;
                    default:
                        if ((int)e.Key == 188) InsertSource(false);
                        else if ((int)e.Key == 190) InsertSource(true);
                        else if ((int)e.Key == 187) Timeline.Zoom(1.35);
                        else if ((int)e.Key == 189) Timeline.Zoom(1 / 1.35);
                        else handled = false;
                        break;
                }
            }
        });
        if (handled) { RefreshPanels(); e.Handled = true; }
    }
    private static bool IsDown(VirtualKey key) => (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
}
