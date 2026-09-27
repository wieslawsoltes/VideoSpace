using VideoSpace.Core;

namespace VideoSpace.Editing;

public enum EditTool { Selection, Razor, Ripple, Rolling, Slip, RateStretch, Hand }

/// <summary>Atomic root-document history with a navigable sequence view. Index lifetime
/// is one committed revision; failed transactions also invalidate every read index.</summary>
public sealed class EditorSession
{
    private sealed record HistoryEntry(string Name, string Before, string After);
    private readonly List<HistoryEntry> _undo = [];
    private readonly List<HistoryEntry> _redo = [];
    private readonly List<string> _path = [];
    private bool _editing;
    private VideoProject _root;
    private ProjectIndex? _index;
    public VideoProject RootProject => _root;
    public VideoProject Project
    {
        get
        {
            var project = _root;
            foreach (var id in _path) project = project.Asset(id).Sequence ?? throw new InvalidOperationException("Nested sequence was removed.");
            return project;
        }
    }
    public ProjectIndex Index => _index ??= new(Project);
    public IReadOnlyList<string> SequencePath => _path;
    public bool CanNavigateUp => _path.Count != 0;
    public HashSet<string> Selection { get; } = new(StringComparer.Ordinal);
    public long Playhead { get; set; } = 96;
    public bool Playing { get; set; }
    public double PlaybackRate { get; set; } = 1;
    public EditTool Tool { get; set; }
    public bool Snapping { get; set; } = true;
    public bool LinkedSelection { get; set; } = true;
    public long Revision { get; private set; }
    public string Status { get; set; } = "Ready";
    public IReadOnlyList<string> History => _undo.Select(h => h.Name).ToArray();
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public event Action? Changed;
    public EditorSession(VideoProject project) { ProjectValidation.Validate(project); _root = project; }
    public void Notify() => Changed?.Invoke();
    public void Select(string? id, bool additive = false)
    {
        if (!additive) Selection.Clear();
        if (id is not null)
        {
            if (!Index.Clips.TryGetValue(id, out var target)) throw new InvalidOperationException("Clip not found.");
            if (additive && Selection.Contains(id)) Selection.Remove(id); else Selection.Add(id);
            if (LinkedSelection && Selection.Contains(id) && target.Clip.LinkId is { } link)
                foreach (var c in Index.Clips.Values) if (c.Clip.LinkId == link) Selection.Add(c.Clip.Id);
        }
        Notify();
    }
    public bool Execute(string name, Action<VideoProject> edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        if (_editing) throw new InvalidOperationException("Nested editing transactions are not supported.");
        string before = ProjectSnapshot.Write(_root);
        if (_undo.Count > 0 && _undo[^1].After == before) before = _undo[^1].After;
        _editing = true; _index = null;
        try
        {
            edit(Project);
            TransitionEdits.PruneDetached(_root);
            ProjectValidation.Validate(_root);
            string after = ProjectSnapshot.Write(_root);
            if (before == after) return false;
            _undo.Add(new(name, before, after)); _redo.Clear();
            long bytes = _undo.Sum(h => 2L * (h.Before.Length + h.After.Length));
            while (_undo.Count > 100 || (_undo.Count > 1 && bytes > 32 * 1024 * 1024))
            { bytes -= 2L * (_undo[0].Before.Length + _undo[0].After.Length); _undo.RemoveAt(0); }
            Revision++; Status = name; _index = null; CleanSelection(); return true;
        }
        catch { _root = ProjectSnapshot.Read(before); _index = null; RepairPath(); throw; }
        finally { _editing = false; _index = null; Notify(); }
    }
    public bool Undo()
    {
        if (_undo.Count == 0) return false;
        var entry = _undo[^1]; _undo.RemoveAt(_undo.Count - 1); _redo.Add(entry);
        _root = ProjectSnapshot.Read(entry.Before); ResetReadState(); Status = "Undo " + entry.Name; Notify(); return true;
    }
    public bool Redo()
    {
        if (_redo.Count == 0) return false;
        var entry = _redo[^1]; _redo.RemoveAt(_redo.Count - 1); _undo.Add(entry);
        _root = ProjectSnapshot.Read(entry.After); ResetReadState(); Status = "Redo " + entry.Name; Notify(); return true;
    }
    public void Replace(VideoProject project)
    {
        ProjectValidation.Validate(project); _root = project; _path.Clear(); _index = null;
        _undo.Clear(); _redo.Clear(); Selection.Clear(); Playhead = 0; Playing = false; Revision++; Notify();
    }
    public void OpenSequence(string assetId)
    {
        var asset = Project.Asset(assetId);
        if (asset.Kind != MediaKind.Sequence || asset.Sequence is null) throw new InvalidOperationException("Select a nested sequence clip.");
        if (_path.Count >= ProjectValidation.MaximumNestingDepth) throw new InvalidOperationException("Maximum nesting depth reached.");
        _path.Add(assetId); _index = null; Selection.Clear(); Playing = false; Playhead = 0; Revision++; Notify();
    }
    public void NavigateUp()
    {
        if (!CanNavigateUp) return;
        _path.RemoveAt(_path.Count - 1); _index = null; Selection.Clear(); Playing = false; Playhead = 0; Revision++; Notify();
    }
    public void Seek(long frame) { Playhead = Math.Clamp(frame, 0, Math.Max(0, Index.Duration - 1)); Notify(); }
    private void ResetReadState() { _index = null; RepairPath(); Revision++; Playing = false; CleanSelection(); Playhead = Math.Min(Playhead, Index.Duration - 1); }
    private void CleanSelection() => Selection.RemoveWhere(id => !Index.Clips.ContainsKey(id));
    private void RepairPath()
    {
        var p = _root;
        for (int i = 0; i < _path.Count; i++)
        {
            var next = p.Assets.FirstOrDefault(a => a.Id == _path[i])?.Sequence;
            if (next is null) { _path.RemoveRange(i, _path.Count - i); break; }
            p = next;
        }
    }
}
