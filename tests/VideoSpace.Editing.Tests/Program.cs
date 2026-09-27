using VideoSpace.Core;
using VideoSpace.Editing;

int passed = 0;
void Test(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
void Check(bool value) { if (!value) throw new Exception("Assertion failed"); }
void Reject(Action action) { try { action(); } catch { return; } throw new Exception("Expected rejection"); }
Test("clipboard preserves effects and receives fresh identities", () =>
{
    var session = new EditorSession(SampleProject.Create()); var clipboard = new TimelineClipboard();
    session.Project.Clip("ridge-cut").Effects.Exposure.Value = .4;
    clipboard.Copy(session.Project, ["ridge-cut"]);
    string[] ids = []; session.Execute("Paste", p => ids = clipboard.Paste(p, 1000));
    Check(ids.Length == 1 && ids[0] != "ridge-cut");
    Check(session.Project.Clip(ids[0]).Start == 1000 && session.Project.Clip(ids[0]).Effects.Exposure.Value == .4);
    session.Undo(); Check(session.Project.Tracks.SelectMany(t => t.Clips).All(c => c.Id != ids[0]));
    session.Redo(); Check(session.Project.Clip(ids[0]).Start == 1000);
});
Test("clipboard preserves relative starts and remaps links", () =>
{
    var session = new EditorSession(SampleProject.Create()); var clipboard = new TimelineClipboard();
    TimelineEdits.Link(session.Project, ["title-cut", "credit-cut"], false);
    clipboard.Copy(session.Project, ["title-cut", "credit-cut"]);
    string[] ids = []; session.Execute("Paste", p => ids = clipboard.Paste(p, 1000));
    Check(ids.Select(id => session.Project.Clip(id).Start).Order().SequenceEqual([1000L, 1012L]));
    Check(ids.Select(id => session.Project.Clip(id).LinkId).Distinct().Count() == 1);
    Check(session.Project.Clip(ids[0]).LinkId != session.Project.Clip("title-cut").LinkId);
});
Test("clipboard is value-owned", () =>
{
    var p = SampleProject.Create(); var clipboard = new TimelineClipboard(); clipboard.Copy(p, ["ridge-cut"]);
    p.Clip("ridge-cut").Name = "changed"; var ids = clipboard.Paste(p, 1000);
    Check(p.Clip(ids[0]).Name == "The ridgeline");
});
Test("clipboard restores assets and tracks in another project", () =>
{
    var clipboard = new TimelineClipboard(); clipboard.Copy(SampleProject.Create(), ["title-cut", "credit-cut"]);
    var session = new EditorSession(new VideoProject()); string[] ids = [];
    session.Execute("Paste", p => ids = clipboard.Paste(p, 0));
    Check(ids.Length == 2 && session.Project.Assets.Count == 2 && session.Project.Tracks.Count == 3);
});
Test("locked paste rolls back all metadata and edits", () =>
{
    var session = new EditorSession(SampleProject.Create()); var clipboard = new TimelineClipboard(); clipboard.Copy(session.Project, ["ridge-cut"]);
    session.Project.Tracks[0].Locked = true; string before = ProjectSnapshot.Write(session.Project);
    Reject(() => session.Execute("Paste", p => clipboard.Paste(p, 1000)));
    Check(before == ProjectSnapshot.Write(session.Project) && !session.CanUndo);
});
Test("cross-timebase paste is rejected explicitly", () =>
{
    var clipboard = new TimelineClipboard(); clipboard.Copy(SampleProject.Create(), ["ridge-cut"]);
    Reject(() => clipboard.Paste(new VideoProject { FrameRate = FrameRate.Ntsc }, 0));
});
Console.WriteLine($"{passed}/{passed} extended editing tests passed.");
