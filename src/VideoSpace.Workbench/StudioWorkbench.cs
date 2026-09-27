using System.Diagnostics;
using VideoSpace.Effects;
using VideoSpace.Media;
using VideoSpace.Rendering;

namespace VideoSpace.Workbench;

/// <summary>Embeddable editing workspace. The shared C# session is the sole owner of project mutations.</summary>
public sealed partial class StudioWorkbench : UserControl, IDisposable
{
    private readonly MediaServices _media;
    private readonly FrameRenderer _renderer = new();
    private readonly Grid _root;
    private readonly Grid _body;
    private readonly PanelHost _sourcePanel = new(), _projectPanel = new(), _programPanel = new();
    private readonly PanelHost _timelinePanel = new();
    private readonly ProjectBinView _bin = new();
    private readonly MonitorView _source, _program;
    private readonly AudioMeterView _meter = new();
    private readonly StackPanel _inspector = new() { Spacing = 3 };
    private readonly StackPanel _history = new() { Spacing = 2, Margin = new(12) };
    private readonly StackPanel _captions = new() { Spacing = 6, Margin = new(12) };
    private readonly TextBlock _status = Studio.Text("Starting local media services…", 11, Studio.Muted);
    private readonly TextBlock _projectTitle = Studio.Text("", 12);
    private readonly TextBlock _backend = Studio.Text("Skia", 10, Studio.Muted);
    private readonly Dictionary<string, StudioButton> _commands = [];
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private PreparedFramePlanner? _planner, _sourcePlanner;
    private FramePlan? _sourcePlan;
    private string _sourcePlanAsset = "";
    private long _lastPaintedPlayhead = -1;
    private long _lastRevision = -1;
    private long _savedRevision = -1;
    private double _lastTime, _playPosition = 96, _sourcePosition = 96;
    private string _sourceAsset = "ridge", _selectedSignature = "";
    private long _sourceIn, _sourceOut = 240;
    private bool _sourcePlaying, _ready, _disposed, _dialogOpen, _exporting, _updating;
    private int _tickCount;
    private string _workspace = "Editing";
    private Border? _overlay;
    public EditorSession Session { get; }
    public TimelineView Timeline { get; }
    public StudioWorkbench(EditorSession session, MediaServices media)
    {
        Session = session; _media = media; IsTabStop = true; Background = Studio.Brush(Studio.Background);
        _source = new("source", media, _renderer); _program = new("program", media, _renderer); Timeline = new(session);
        _root = Studio.Rows(new(25), new(45), Studio.Star(), new(24)); _root.Background = Studio.Brush(Studio.Background); Content = _root;
        Studio.At(_root, BuildMenu()); Studio.At(_root, BuildNavigation(), 1);
        _body = Studio.Rows(Studio.Star(1.12), new(5), Studio.Star()); _body.Margin = new(5, 0, 5, 0); Studio.At(_root, _body, 2);
        var upper = Studio.Columns(Studio.Star(.9), new(5), Studio.Star(1.35));
        _sourcePanel.Add("Source", () => _source);
        _sourcePanel.Add("Effect Controls", () => new ScrollViewer { Content = _inspector, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        _sourcePanel.Add("Metadata", BuildMetadata);
        _sourcePanel.SelectedChanged += name => { BrowserState.Visible("source", name == "Source"); if (name != "Source") _sourcePlaying = false; RefreshFrames(); };
        _programPanel.Add("Program: NORTH / Main edit", () => _program);
        Studio.At(upper, _sourcePanel); Studio.At(upper, new SplitHandle(upper, 0, true, 260), column: 1); Studio.At(upper, _programPanel, column: 2); Studio.At(_body, upper);
        Studio.At(_body, new SplitHandle(_body, 0, false, 170), 1);
        var lower = Studio.Columns(new(342), new(5), Studio.Star());
        _projectPanel.Add("Project", () => _bin); _projectPanel.Add("Effects", BuildEffects); _projectPanel.Add("History", () => new ScrollViewer { Content = _history }); _projectPanel.Add("Captions", () => new ScrollViewer { Content = _captions });
        Studio.At(lower, _projectPanel); Studio.At(lower, new SplitHandle(lower, 0, true, 250, .5), column: 1);
        var timelineArea = Studio.Columns(new(37), Studio.Star(), new(50));
        Studio.At(timelineArea, BuildTools()); Studio.At(timelineArea, Timeline, column: 1); Studio.At(timelineArea, _meter, column: 2);
        _timelinePanel.Add("NORTH / Main edit", () => timelineArea); Studio.At(lower, _timelinePanel, column: 2); Studio.At(_body, lower, 2);
        var status = Studio.Columns(Studio.Star(), new(220)); status.Margin = new(12, 0, 12, 0); Studio.At(status, _status); _backend.HorizontalAlignment = HorizontalAlignment.Right; Studio.At(status, _backend, column: 1); Studio.At(_root, status, 3);
        _bin.ImportRequested += () => RunAsync(_media.ImportAsync); _bin.NewTitleRequested += NewTitle;
        _bin.AssetSelected += OpenSource; _bin.AssetOpened += OpenSource;
        Timeline.Error += text => ShowStatus(text, true); Timeline.AssetDropped += (id, track, frame) => InsertAsset(id, track, frame, true);
        Timeline.TrackSelected += _ => RefreshPanels();
        _program.Command += ProgramCommand; _program.Seek += Seek;
        _source.Command += SourceCommand; _source.Seek += frame => { _sourcePosition = Math.Clamp(frame, 0, Math.Max(0, SourceLength - 1)); _sourcePlaying = false; RefreshFrames(); };
        Session.Changed += SessionChanged;
        KeyDown += OnKeyDown;
        Loaded += async (_, _) => { try { await _media.InitializeAsync(); } catch (Exception ex) { ShowStatus(ex.Message, true); _ready = true; } _lastTime = _clock.Elapsed.TotalSeconds; _timer.Start(); Timeline.Fit(); Focus(FocusState.Programmatic); };
        _timer.Tick += (_, _) => Tick();
        SizeChanged += (_, _) =>
        {
            if (lower.ColumnDefinitions[0].Width.IsAbsolute && lower.ColumnDefinitions[0].Width.Value > ActualWidth * .42) lower.ColumnDefinitions[0].Width = new(Math.Max(230, ActualWidth * .3));
            Timeline.Geometry.TrackHeight = ActualHeight < 620 ? 42 : 52;
            RefreshFrames();
        };
        Session.Select("ridge-cut"); RefreshPanels(); RefreshFrames();
    }
    private long SourceLength => Session.Index.Assets.TryGetValue(_sourceAsset, out var asset) ? Math.Max(1, Session.Project.FrameRate.Frames(asset.DurationSeconds)) : 1;
    public void SetTypeface(SkiaSharp.SKTypeface typeface) { Studio.Typeface = typeface; _renderer.SetTypeface(typeface); RefreshPanels(); Timeline.Invalidate(); }
    public void ShowStatus(string text, bool error = false)
    {
        Session.Status = text; _status.Text = text; _status.Foreground = Studio.Brush(error ? "#F39E91" : Studio.Muted); ToolTipService.SetToolTip(_status, text);
    }
    private void Run(Action action)
    {
        try { action(); } catch (Exception ex) { ShowStatus(ex.Message, true); } RefreshPanels(); RefreshFrames();
    }
    private async void RunAsync(Func<Task> action)
    {
        try { await action(); } catch (Exception ex) { ShowStatus(ex.Message, true); }
    }
    private void SessionChanged()
    {
        if (_updating) return; _playPosition = Session.Playhead;
        if (!Session.Index.Assets.ContainsKey(_sourceAsset)) { _sourceAsset = Session.Project.Assets.FirstOrDefault()?.Id ?? ""; _sourcePosition = 0; _sourceIn = 0; _sourceOut = Math.Min(SourceLength, Session.Project.FrameRate.Frames(10)); }
        if (!Session.Project.Tracks.Any(t => t.Id == Timeline.TargetTrackId)) Timeline.TargetTrackId = Session.Project.Tracks.FirstOrDefault()?.Id ?? "";
        Timeline.Invalidate(); RefreshPanels(); RefreshFrames();
    }
    private void Tick()
    {
        if (_disposed) return;
        try
        {
            foreach (var message in _media.Poll()) HandleMessage(message);
            double now = _clock.Elapsed.TotalSeconds, delta = Math.Min(.25, Math.Max(0, now - _lastTime)); _lastTime = now;
            if (!_dialogOpen && !_exporting)
            {
                if (Session.Playing)
                {
                    _playPosition += delta * Session.Project.FrameRate.Value * Session.PlaybackRate;
                    if (_playPosition < 0 || _playPosition >= Session.Index.Duration) { _playPosition = Math.Clamp(_playPosition, 0, Session.Index.Duration - 1); Session.Playing = false; }
                    long frame = (long)_playPosition;
                    if (Session.Playhead != frame) { Session.Playhead = frame; Timeline.EnsurePlayheadVisible(); }
                }
                if (_sourcePlaying) { _sourcePosition += delta * Session.Project.FrameRate.Value; if (_sourcePosition >= SourceLength) { _sourcePosition = SourceLength - 1; _sourcePlaying = false; } }
            }
            if (_ready && Session.Revision != _savedRevision && !_dialogOpen && !_exporting && _tickCount % 30 == 0)
            {
                _savedRevision = Session.Revision; _media.SetProject(Session.Project); RunAsync(() => _media.SaveTextAsync(VideoSpace.Documents.ProjectFile.Save(Session.RootProject), "recovery.videospace", true));
            }
            RefreshFrames();
            if (++_tickCount % 6 == 0)
            {
                var levels = BrowserState.Meter(); double left = levels.ElementAtOrDefault(0), right = levels.ElementAtOrDefault(1);
                if (Math.Abs(_meter.Left - left) > .0001 || Math.Abs(_meter.Right - right) > .0001) { _meter.Left = left; _meter.Right = right; _meter.Invalidate(); }
                PublishDiagnostics();
            }
        }
        catch (Exception ex) { ShowStatus("Playback: " + ex.Message, true); Session.Playing = _sourcePlaying = false; }
    }
    private PreparedFramePlanner Planner
    {
        get
        {
            if (_planner is null || !ReferenceEquals(_planner.Index, Session.Index))
            {
                _planner = new(Session.Index); _sourcePlanner = new(Session.Index); _sourcePlan = null;
            }
            return _planner;
        }
    }
    private void RefreshFrames()
    {
        if (_disposed || _program is null || _source is null) return;
        var p = Session.Project; var planner = Planner;
        _program.Update(planner.Evaluate(Session.Playhead), Session.Index.Duration, p.FrameRate, Session.Playing && !_dialogOpen && !_exporting, Session.PlaybackRate, !_dialogOpen && !_exporting);
        long frame = (long)_sourcePosition;
        if (_sourcePlan is null || _sourcePlan.Frame != frame || _sourcePlanAsset != _sourceAsset)
        {
            _sourcePlanAsset = _sourceAsset;
            if (Session.Index.Assets.TryGetValue(_sourceAsset, out var asset))
            {
                var clip = new TimelineClip { Id = "source-preview", AssetId = asset.Id, Duration = SourceLength };
                var layer = _sourcePlanner!.Layer(clip, frame);
                _sourcePlan = new(frame, p.FrameRate.Seconds(frame), asset.Width > 0 ? asset.Width : 1920, asset.Height > 0 ? asset.Height : 1080, p.FrameRate.Value, asset.Kind == MediaKind.Audio ? [] : [layer], [], []);
            }
            else _sourcePlan = new(frame, 0, p.Width, p.Height, p.FrameRate.Value, [], [], []);
        }
        _source.Update(_sourcePlan, SourceLength, p.FrameRate, _sourcePlaying && !_dialogOpen, 1, _sourcePanel.Selected == "Source" && !_dialogOpen && !_exporting);
        if (_lastPaintedPlayhead != Session.Playhead) { _lastPaintedPlayhead = Session.Playhead; Timeline.Invalidate(); }
    }
    private void RefreshPanels()
    {
        if (_updating || _disposed) return; _updating = true;
        try
        {
            var p = Session.Project; string signature = string.Join('|', Session.Selection.Order());
            if (_lastRevision != Session.Revision)
            {
                _bin.Update(p, _sourceAsset); _projectTitle.Text = Session.CanNavigateUp ? Session.RootProject.Name + " / " + p.SequenceName : p.Name;
                _programPanel.SetCaption("Program: NORTH / Main edit", "Program: " + p.SequenceName);
                _timelinePanel.SetCaption("NORTH / Main edit", (Session.CanNavigateUp ? "↳ " : "") + p.SequenceName); RebuildHistory(); RebuildCaptions();
                _sourcePanel.InvalidatePanel("Metadata"); _lastRevision = Session.Revision;
                _selectedSignature = "__invalidate__";
            }
            if (signature != _selectedSignature) { BuildInspector(); _selectedSignature = signature; }
            if (_commands.TryGetValue("Undo", out var undo)) undo.IsEnabled = Session.CanUndo;
            if (_commands.TryGetValue("Redo", out var redo)) redo.IsEnabled = Session.CanRedo;
            foreach (var tool in Enum.GetValues<EditTool>()) if (_commands.TryGetValue("Tool " + tool, out var button)) button.SetActive(Session.Tool == tool);
            if (_commands.TryGetValue("Snapping", out var snap)) snap.SetActive(Session.Snapping);
            if (_commands.TryGetValue("Linked selection", out var link)) link.SetActive(Session.LinkedSelection);
        }
        finally { _updating = false; }
    }
    private void HandleMessage(MediaMessage message)
    {
        switch (message.Type)
        {
            case "ready": _ready = true; _media.SetProject(Session.Project); ShowStatus("Ready  ·  All media stays on this device"); break;
            case "project": Run(() => { var p = VideoSpace.Documents.ProjectFile.Open(message.Text); Session.Replace(p); _sourceAsset = p.Assets.FirstOrDefault()?.Id ?? ""; _sourcePosition = 0; _sourceOut = Math.Min(SourceLength, p.FrameRate.Frames(10)); _sourceIn = 0; Timeline.TargetTrackId = p.Tracks.FirstOrDefault(t => t.Kind == TrackKind.Video)?.Id ?? ""; Timeline.Fit(); _media.SetProject(p); ShowStatus("Project opened: " + p.Name); }); break;
            case "asset":
                if (message.Asset is { } asset) Run(() =>
                {
                    Session.Execute("Import " + asset.Name, p => { int i = p.Assets.FindIndex(a => a.Id == asset.Id); if (i >= 0) p.Assets[i] = asset; else p.Assets.Add(asset); });
                    if (message.Bytes is { Length: > 0 } bytes) { _bin.SetThumbnail(asset.Id, bytes); _renderer.SetImage(asset.Id, bytes); }
                    OpenSource(asset.Id); _bin.Update(Session.Project, asset.Id); ShowStatus("Imported " + asset.Name + " · use Insert, Overwrite, or drag to timeline");
                }); break;
            case "captions": Run(() => { var captions = VideoSpace.Documents.SubRip.Read(message.Text, Session.Project.FrameRate); Session.Execute("Import captions", p => p.Captions = captions); _projectPanel.Select("Captions"); }); break;
            case "backend": _backend.Text = message.Text + "  ·  Local processing"; break;
            case "error": ShowStatus(message.Text, true); break;
            case "pause": Session.Playing = _sourcePlaying = false; ShowStatus(message.Text); break;
            case "progress": ShowStatus(message.Text); if (_exportProgress is not null) _exportProgress.Text = message.Text; break;
            case "exportDone": _exporting = false; CloseOverlay(); ShowStatus(message.Text, message.Text.StartsWith("Export failed", StringComparison.Ordinal)); break;
            default: ShowStatus(message.Text); break;
        }
    }
    private void PublishDiagnostics()
    {
        var p = Session.Project;
        _media.Diagnostics(new
        {
            ready = _ready, revision = Session.Revision, playhead = Session.Playhead, playing = Session.Playing, selected = Session.Selection.ToArray(), tool = Session.Tool.ToString(), clipCount = p.Tracks.Sum(t => t.Clips.Count), assetCount = p.Assets.Count,
            canUndo = Session.CanUndo, canRedo = Session.CanRedo, status = Session.Status, workspace = _workspace, modal = _dialogOpen, exporting = _exporting, sourceAsset = _sourceAsset,
            sourceIn = _sourceIn, sourceOut = _sourceOut, timeline = Timeline.Diagnostics(), framePlan = Planner.Evaluate(Session.Playhead), sequencePath = Session.SequencePath, transitionCount = p.Tracks.Sum(t => t.Transitions.Count), plannerEvaluations = Planner.Evaluations, plannerCacheHits = Planner.CacheHits, presentedPlans = _media.PresentedPlans,
            commands = _commands.ToDictionary(k => k.Key, k => { var r = Studio.Bounds(k.Value); return new { x = r.X, y = r.Y, width = r.Width, height = r.Height, enabled = k.Value.IsEnabled }; })
        });
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true; _timer.Stop(); Session.Changed -= SessionChanged;
        _source.Dispose(); _program.Dispose(); _bin.Dispose(); _renderer.Dispose(); _media.Dispose();
    }
}
