using VideoSpace.Core;

namespace VideoSpace.Editing;

public enum EditTool { Selection, Razor, Ripple, Rolling, Slip, RateStretch, Hand }

/// <summary>Atomic project mutations with validation/rollback and bounded undo memory.</summary>
public sealed class EditorSession
{
    private sealed record HistoryEntry(string Name, string Before, string After);
    private readonly List<HistoryEntry> _undo = [];
    private readonly List<HistoryEntry> _redo = [];
    private bool _editing;
    public VideoProject Project { get; private set; }
    public HashSet<string> Selection { get; } = [];
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
    public EditorSession(VideoProject project) { ProjectValidation.Validate(project); Project = project; }
    public void Notify() => Changed?.Invoke();
    public void Select(string? id, bool additive = false)
    {
        if (!additive) Selection.Clear();
        if (id is not null)
        {
            if (additive && Selection.Contains(id)) Selection.Remove(id); else Selection.Add(id);
            if (LinkedSelection && Selection.Contains(id) && Project.Clip(id).LinkId is { } link)
                foreach (var c in Project.Tracks.SelectMany(t => t.Clips).Where(c => c.LinkId == link)) Selection.Add(c.Id);
        }
        Notify();
    }
    public bool Execute(string name, Action<VideoProject> edit)
    {
        if (_editing) throw new InvalidOperationException("Nested editing transactions are not supported.");
        string before = ProjectSnapshot.Write(Project);
        _editing = true;
        try
        {
            edit(Project); ProjectValidation.Validate(Project);
            string after = ProjectSnapshot.Write(Project);
            if (before == after) return false;
            _undo.Add(new(name, before, after)); _redo.Clear();
            long size = _undo.Sum(h => (long)h.Before.Length + h.After.Length);
            while (_undo.Count > 100 || (_undo.Count > 1 && size > 32 * 1024 * 1024)) { size -= _undo[0].Before.Length + _undo[0].After.Length; _undo.RemoveAt(0); }
            Revision++; Status = name; CleanSelection();
            return true;
        }
        catch { Project = ProjectSnapshot.Read(before); throw; }
        finally { _editing = false; Notify(); }
    }
    public bool Undo()
    {
        if (_undo.Count == 0) return false;
        var entry = _undo[^1]; _undo.RemoveAt(_undo.Count - 1); _redo.Add(entry);
        Project = ProjectSnapshot.Read(entry.Before); Revision++; Status = "Undo " + entry.Name; CleanSelection(); Notify(); return true;
    }
    public bool Redo()
    {
        if (_redo.Count == 0) return false;
        var entry = _redo[^1]; _redo.RemoveAt(_redo.Count - 1); _undo.Add(entry);
        Project = ProjectSnapshot.Read(entry.After); Revision++; Status = "Redo " + entry.Name; CleanSelection(); Notify(); return true;
    }
    public void Replace(VideoProject project)
    {
        ProjectValidation.Validate(project); Project = project; _undo.Clear(); _redo.Clear(); Selection.Clear(); Playhead = 0; Playing = false; Revision++; Notify();
    }
    public void Seek(long frame) { Playhead = Math.Clamp(frame, 0, Math.Max(0, Project.Duration - 1)); Notify(); }
    private void CleanSelection() => Selection.RemoveWhere(id => !Project.Tracks.Any(t => t.Clips.Any(c => c.Id == id)));
}
