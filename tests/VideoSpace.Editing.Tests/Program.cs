using VideoSpace.Core;
using VideoSpace.Editing;

int passed = 0;
void Test(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
void Check(bool value) { if (!value) throw new Exception("Assertion failed"); }
void Reject(Action action)
{
    try { action(); } catch (InvalidOperationException) { return; } catch (InvalidDataException) { return; }
    throw new Exception("Expected a supported validation rejection");
}
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
(EditorSession Session, string[] Ids) NestVideo()
{
    var p = new VideoProject
    {
        Assets = [new() { Id = "image", Kind = MediaKind.Image }],
        Tracks = [new() { Id = "v1", Kind = TrackKind.Video, Clips =
        [new() { Id = "first", AssetId = "image", Start = 24, Duration = 48 }, new() { Id = "second", AssetId = "image", Start = 72, Duration = 48 }] }],
        Markers = [new(30, "Preserved marker")]
    };
    var session = new EditorSession(p); string[] ids = [];
    session.Execute("Nest", q => ids = SequenceEdits.Nest(q, ["first", "second"], "Compound"));
    return (session, ids);
}
Test("unnest preserves a leading gap created by deleting child media", () =>
{
    var (s, ids) = NestVideo(); string asset = s.Project.Clip(ids[0]).AssetId;
    s.OpenSequence(asset); s.Execute("Remove opening", p => TimelineEdits.Delete(p, ["first"], false)); s.NavigateUp();
    string[] result = []; s.Execute("Unnest", p => result = SequenceEdits.Unnest(p, ids[0]));
    Check(result.Length == 1 && s.Project.Clip(result[0]).Start == 72 && s.Project.Clip(result[0]).Duration == 48);
    Check(s.Project.Markers.Single().Frame == 30); s.Undo(); Check(s.Project.Clip(ids[0]).Start == 24);
});
Test("unnest rejects parent overlaps without overwriting other media", () =>
{
    var (s, ids) = NestVideo();
    s.Execute("Parent overlay", p => p.Tracks.Add(new() { Id = "v2", Clips = [new() { Id = "unrelated", AssetId = "image", Start = 30, Duration = 20 }] }));
    string before = ProjectSnapshot.Write(s.Project);
    Reject(() => s.Execute("Unnest", p => SequenceEdits.Unnest(p, ids[0])));
    Check(before == ProjectSnapshot.Write(s.Project));
});
Test("unnest does not re-enable a disabled compound clip", () =>
{
    var (s, ids) = NestVideo(); s.Execute("Disable", p => p.Clip(ids[0]).Enabled = false);
    string before = ProjectSnapshot.Write(s.Project); Reject(() => s.Execute("Unnest", p => SequenceEdits.Unnest(p, ids[0])));
    Check(before == ProjectSnapshot.Write(s.Project));
});
Test("unnest rejects changed child geometry", () =>
{
    var (s, ids) = NestVideo(); s.Execute("Child aspect", p => p.Asset(p.Clip(ids[0]).AssetId).Sequence!.Width = 1080);
    Reject(() => s.Execute("Unnest", p => SequenceEdits.Unnest(p, ids[0])));
});
Test("unnest requires linked audio and does not silently add sound", () =>
{
    var s = new EditorSession(SampleProject.Create()); string[] ids = [];
    s.Execute("Nest", p => ids = SequenceEdits.Nest(p, p.Tracks.SelectMany(t => t.Clips).Select(c => c.Id), "Compound"));
    s.Execute("Remove audio representation", p => TimelineEdits.Delete(p, [ids[1]], false));
    Reject(() => s.Execute("Unnest", p => SequenceEdits.Unnest(p, ids[0])));
});
Test("unnest rejects desynchronized linked picture and audio", () =>
{
    var s = new EditorSession(SampleProject.Create()); string[] ids = [];
    s.Execute("Nest", p => ids = SequenceEdits.Nest(p, p.Tracks.SelectMany(t => t.Clips).Select(c => c.Id), "Compound"));
    s.Execute("Offset audio", p => p.Clip(ids[1]).Start = 24);
    Reject(() => s.Execute("Unnest", p => SequenceEdits.Unnest(p, ids[0])));
});
Console.WriteLine($"{passed}/{passed} extended editing tests passed.");
