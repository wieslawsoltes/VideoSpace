using System.Diagnostics;
using System.Text.Json;
using VideoSpace.Core;
using VideoSpace.Editing;
using VideoSpace.Effects;
using VideoSpace.Audio;

var tests = new List<(string, Action)>();
void Test(string name, Action action) => tests.Add((name, action));
void Check(bool value, string text = "Assertion failed") { if (!value) throw new Exception(text); }
void Near(double a, double b, double tolerance = 1e-8) => Check(Math.Abs(a - b) <= tolerance, $"{a} != {b}");
void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } catch (InvalidOperationException) { return; } throw new Exception("Expected rejection"); }
EditorSession Session() => new(SampleProject.Create());
VideoProject Transition(TransitionKind kind = TransitionKind.CrossDissolve, long duration = 48)
{
    var p = SampleProject.Create(); TransitionEdits.Add(p, "ridge-cut", kind, duration); ProjectValidation.Validate(p); return p;
}
Test("indexed lookup matches linear clip lookup at every boundary", () =>
{
    var p = Transition(); var index = new ProjectIndex(p);
    foreach (var t in index.Tracks) for (int f = -1; f <= 865; f++) Check(ReferenceEquals(t.At(f), t.Track.Clips.FirstOrDefault(c => c.Contains(f))));
});
Test("indexed visible ranges retain half-open semantics", () =>
{
    var index = new ProjectIndex(Transition()).Tracks[0];
    Check(index.Visible(0, 240).Length == 1); Check(index.Visible(240, 432).Length == 1);
    Check(index.Visible(864, 1000).Length == 0); Check(index.Visible(0, 0).Length == 0);
    Check(index.Visible(230, 231, true).Length == 2); Check(index.Visible(230, 231).Length == 1);
});
Test("prepared frame evaluation reuses the exact paused result", () =>
{
    var prepared = new PreparedFramePlanner(SampleProject.Create()); var a = prepared.Evaluate(96);
    Check(ReferenceEquals(a, prepared.Evaluate(96))); Check(prepared.Evaluations == 1 && prepared.CacheHits == 1);
    Check(!ReferenceEquals(a, prepared.Evaluate(97)));
});
Test("session index changes after edits, rollback and undo", () =>
{
    var s = Session(); var a = s.Index;
    s.Execute("Marker", p => p.Markers.Add(new(3, "M"))); Check(!ReferenceEquals(a, s.Index)); a = s.Index;
    Reject(() => s.Execute("Bad", p => p.Clip("ridge-cut").Start = -1)); Check(!ReferenceEquals(a, s.Index)); a = s.Index;
    s.Undo(); Check(!ReferenceEquals(a, s.Index));
});
Test("transition reaches both exact endpoint images", () =>
{
    var p = Transition(); var planner = new PreparedFramePlanner(p);
    Near(0, planner.Evaluate(216).Layers[0].Transition!.Progress); Near(1, planner.Evaluate(263).Layers[0].Transition!.Progress);
    Check(planner.Evaluate(215).Layers[0].Transition is null); Check(planner.Evaluate(264).Layers[0].Transition is null);
});
Test("transition reads actual source handles", () =>
{
    var p = Transition(); p.Asset("ridge").Kind = MediaKind.Video; p.Asset("coast").Kind = MediaKind.Video;
    p.Clip("coast-cut").SourceIn = 2; ProjectValidation.Validate(p);
    var transition = new PreparedFramePlanner(p).Evaluate(216).Layers[0].Transition!;
    Near(9, transition.From.SourceTime); Near(1, transition.To.SourceTime);
});
Test("missing incoming handles reject atomically", () =>
{
    var s = Session(); s.Project.Asset("coast").Kind = MediaKind.Video; string before = ProjectSnapshot.Write(s.Project);
    Reject(() => s.Execute("Transition", p => TransitionEdits.Add(p, "ridge-cut", TransitionKind.CrossDissolve, 48)));
    Check(before == ProjectSnapshot.Write(s.Project));
});
Test("missing outgoing handles reject", () =>
{
    var p = Transition(); p.Asset("ridge").Kind = MediaKind.Video; p.Asset("ridge").DurationSeconds = 10;
    Reject(() => ProjectValidation.Validate(p));
});
Test("start-aligned transition needs no incoming preroll", () =>
{
    var p = SampleProject.Create(); p.Asset("coast").Kind = MediaKind.Video;
    TransitionEdits.Add(p, "ridge-cut", TransitionKind.CrossDissolve, 24, TransitionAlignment.StartAtCut); ProjectValidation.Validate(p);
    Near(0, new PreparedFramePlanner(p).Evaluate(240).Layers[0].Transition!.To.SourceTime);
});
Test("end-aligned transition needs no outgoing postroll", () =>
{
    var p = SampleProject.Create(); p.Asset("ridge").Kind = MediaKind.Video; p.Asset("ridge").DurationSeconds = 10;
    TransitionEdits.Add(p, "ridge-cut", TransitionKind.CrossDissolve, 24, TransitionAlignment.EndAtCut); ProjectValidation.Validate(p);
});
Test("overlapping transitions reject", () =>
{
    var p = SampleProject.Create(); TransitionEdits.Add(p, "ridge-cut", TransitionKind.CrossDissolve, 300);
    TransitionEdits.Add(p, "coast-cut", TransitionKind.CrossDissolve, 300); Reject(() => ProjectValidation.Validate(p));
});
Test("transition kind matches track", () =>
{
    var p = SampleProject.Create(); TransitionEdits.Add(p, "ridge-cut", TransitionKind.EqualPowerAudio, 24); Reject(() => ProjectValidation.Validate(p));
});
Test("deleting endpoint removes only detached transitions", () =>
{
    var s = new EditorSession(Transition()); s.Execute("Delete", p => TimelineEdits.Delete(p, ["coast-cut"], false));
    Check(s.Project.Tracks[0].Transitions.Count == 0); s.Undo(); Check(s.Project.Tracks[0].Transitions.Count == 1);
});
Test("splitting outgoing endpoint transfers its transition", () =>
{
    var s = new EditorSession(Transition()); s.Execute("Split", p => TimelineEdits.Split(p, ["ridge-cut"], 100));
    var t = s.Project.Tracks[0].Transitions.Single(); Check(t.LeftClipId != "ridge-cut"); Check(s.Project.Clip(t.LeftClipId).End == 240);
});
Test("clipboard preserves internal transition identity mapping", () =>
{
    var s = new EditorSession(Transition()); var clipboard = new TimelineClipboard(); clipboard.Copy(s.Project, ["ridge-cut", "coast-cut"]);
    s.Execute("Paste", p => clipboard.Paste(p, 1000)); Check(s.Project.Tracks[0].Transitions.Count == 2);
    var t = s.Project.Tracks[0].Transitions.Last(); Check(t.LeftClipId != "ridge-cut" && t.RightClipId != "coast-cut");
});
Test("partial clipboard does not create detached transitions", () =>
{
    var s = new EditorSession(Transition()); var clipboard = new TimelineClipboard(); clipboard.Copy(s.Project, ["ridge-cut"]);
    s.Execute("Paste", p => clipboard.Paste(p, 1000)); Check(s.Project.Tracks[0].Transitions.Count == 1);
});
Test("split preserves smooth curve samples without rebaking", () =>
{
    var s = Session(); s.Project.Clip("ridge-cut").Effects.Scale.SetKey(0, 1, Interpolation.Smooth); s.Project.Clip("ridge-cut").Effects.Scale.SetKey(239, 3);
    var values = Enumerable.Range(0, 240).Select(f => s.Project.Clip("ridge-cut").Effects.Scale.At(f)).ToArray();
    s.Execute("Split", p => TimelineEdits.Split(p, ["ridge-cut"], 75)); var right = s.Project.Tracks[0].Clips.Single(c => c.Start == 75);
    for (int i = 0; i < right.Duration; i++) Near(values[i + 75], right.Effects.Scale.At(i));
});
Test("trim preserves smooth curve samples", () =>
{
    var s = Session(); var value = s.Project.Clip("ridge-cut").Effects.X; value.SetKey(0, 0, Interpolation.Smooth); value.SetKey(239, .5);
    double before = value.At(130); s.Execute("Trim", p => TimelineEdits.Trim(p, "ridge-cut", 50, true));
    Near(before, s.Project.Clip("ridge-cut").Effects.X.At(80));
});
Test("key insertion rebases an extended negative origin", () =>
{
    var v = new AnimatedValue(1) { FrameOffset = -10 }; v.SetKey(0, 2); v.SetKey(20, 4); Near(2, v.At(0)); Near(3, v.At(10));
});
Test("hidden animation cursors survive project roundtrip", () =>
{
    var p = SampleProject.Create(); p.Clip("ridge-cut").Effects.Scale.FrameOffset = 40;
    Check(ProjectSnapshot.Clone(p).Clip("ridge-cut").Effects.Scale.FrameOffset == 40);
});
Test("nested selection preserves visual and audio frames", () =>
{
    var s = Session(); var before = new PreparedFramePlanner(s.Project).Evaluate(96);
    string[] nested = []; s.Execute("Nest", p => nested = SequenceEdits.Nest(p, p.Tracks.SelectMany(t => t.Clips).Select(c => c.Id), "Nested"));
    Check(nested.Length == 2); var plan = new PreparedFramePlanner(s.Project).Evaluate(96);
    Check(plan.Layers.Single().Nested!.Layers.Length == before.Layers.Length); Check(plan.Audio.Length == 1); Near(before.Audio[0].Gain, plan.Audio[0].Gain);
});
Test("nest carries markers, captions and transitions", () =>
{
    var s = new EditorSession(Transition()); s.Execute("Nest", p => SequenceEdits.Nest(p, p.Tracks.SelectMany(t => t.Clips).Select(c => c.Id), "Nested"));
    var child = s.Project.Assets.Single(a => a.Kind == MediaKind.Sequence).Sequence!;
    Check(child.Tracks[0].Transitions.Count == 1 && child.Captions.Count == 2 && child.Markers.Count == 4);
    Check(s.Project.Captions.Count == 0 && s.Project.Markers.Count == 0);
});
Test("partial temporal nesting rejects instead of changing layer order", () =>
{
    var s = Session(); Reject(() => s.Execute("Nest", p => SequenceEdits.Nest(p, ["ridge-cut"], "Bad"))); Check(s.Project.Tracks[0].Clips.Count == 4);
});
Test("editing nested content is undoable from parent", () =>
{
    var s = Session(); s.Execute("Nest", p => SequenceEdits.Nest(p, p.Tracks.SelectMany(t => t.Clips).Select(c => c.Id), "Nested"));
    string id = s.Project.Assets.Single(a => a.Kind == MediaKind.Sequence).Id; s.OpenSequence(id);
    s.Execute("Child title", p => p.Asset("title").Text = "CHANGED"); s.NavigateUp();
    Check(s.RootProject.Asset(id).Sequence!.Asset("title").Text == "CHANGED"); s.Undo(); Check(s.RootProject.Asset(id).Sequence!.Asset("title").Text == "NORTH");
});
Test("root serialization retains active child changes", () =>
{
    var s = Session(); s.Execute("Nest", p => SequenceEdits.Nest(p, p.Tracks.SelectMany(t => t.Clips).Select(c => c.Id), "Nested"));
    s.OpenSequence(s.Project.Assets.Single(a => a.Kind == MediaKind.Sequence).Id); s.Execute("Marker", p => p.Markers.Add(new(12, "CHILD")));
    var root = ProjectSnapshot.Clone(s.RootProject); Check(root.Assets.Single(a => a.Kind == MediaKind.Sequence).Sequence!.Markers.Count == 5);
});
Test("undoing nest repairs active sequence path", () =>
{
    var s = Session(); s.Execute("Nest", p => SequenceEdits.Nest(p, p.Tracks.SelectMany(t => t.Clips).Select(c => c.Id), "Nested"));
    s.OpenSequence(s.Project.Assets.Single(a => a.Kind == MediaKind.Sequence).Id); s.Undo(); Check(!s.CanNavigateUp && s.Project.Tracks[0].Clips.Count == 4);
});
Test("unnest restores editable source tracks and transition references", () =>
{
    var s = new EditorSession(Transition()); string[] ids = [];
    s.Execute("Nest", p => ids = SequenceEdits.Nest(p, p.Tracks.SelectMany(t => t.Clips).Select(c => c.Id), "Nested"));
    s.Execute("Unnest", p => SequenceEdits.Unnest(p, ids[0]));
    Check(s.Project.Tracks.Sum(t => t.Clips.Count) == 7); Check(s.Project.Tracks[0].Transitions.Count == 1); Check(s.Project.Captions.Count == 2);
});
Test("unnest rejects transformed outer composition", () =>
{
    var s = Session(); string[] ids = []; s.Execute("Nest", p => ids = SequenceEdits.Nest(p, p.Tracks.SelectMany(t => t.Clips).Select(c => c.Id), "Nested"));
    s.Execute("Scale", p => p.Clip(ids[0]).Effects.Scale.Value = .8); Reject(() => s.Execute("Unnest", p => SequenceEdits.Unnest(p, ids[0])));
});
Test("cycle validation is bounded", () =>
{
    var p = SampleProject.Create(); p.Assets.Add(new MediaAsset { Kind = MediaKind.Sequence, Sequence = p }); Reject(() => ProjectValidation.Validate(p));
});
Test("excessive nesting is rejected", () =>
{
    var p = SampleProject.Create(); var current = p;
    for (int i = 0; i < 9; i++) { var child = new VideoProject(); current.Assets.Add(new() { Kind = MediaKind.Sequence, Sequence = child }); current = child; }
    Reject(() => ProjectValidation.Validate(p));
});
Test("multicamera cuts use hold interpolation and source offsets", () =>
{
    var s = Session(); string id = ""; s.Execute("Group", p => id = SequenceEdits.CreateMulticam(p, "Cameras", [new("ridge", "A", 1), new("coast", "B", 2)]));
    s.Execute("Place", p => p.Clip("ridge-cut").AssetId = id);
    s.Execute("Switch", p => SequenceEdits.SwitchCamera(p, ["ridge-cut"], 1, 100)); var planner = new PreparedFramePlanner(s.Project);
    Check(planner.Evaluate(99).Layers[0].AssetId == "ridge"); Check(planner.Evaluate(100).Layers[0].AssetId == "coast"); Near(100 / 24d + 2, planner.Evaluate(100).Layers[0].SourceTime);
});
Test("multicam rejects fractional and smoothly interpolated angles", () =>
{
    var p = SampleProject.Create(); var id = SequenceEdits.CreateMulticam(p, "Cameras", [new("ridge", "A"), new("coast", "B")]); p.Clip("ridge-cut").AssetId = id;
    p.Clip("ridge-cut").CameraAngle.SetKey(0, 1, Interpolation.Linear); Reject(() => ProjectValidation.Validate(p));
});
Test("multicam camera dependencies survive clipboard", () =>
{
    var p = SampleProject.Create(); var id = SequenceEdits.CreateMulticam(p, "Cameras", [new("ridge", "A"), new("coast", "B")]); p.Clip("ridge-cut").AssetId = id;
    var copy = new TimelineClipboard(); copy.Copy(p, ["ridge-cut"]); var target = new EditorSession(new VideoProject()); target.Execute("Paste", q => copy.Paste(q, 0));
    Check(target.Project.Assets.Count == 3); Check(new PreparedFramePlanner(target.Project).Evaluate(0).Layers[0].AssetId == "ridge");
});
Test("nested audio is sample-identical before and after nesting", () =>
{
    var p = SampleProject.Create(); var samples = new float[1024]; new PreparedAudioMixer(p).Mix(new Dictionary<string, PcmAudio>(), 96000, 48000, samples);
    SequenceEdits.Nest(p, p.Tracks.SelectMany(t => t.Clips).Select(c => c.Id), "Nested"); var after = new float[1024]; new PreparedAudioMixer(p).Mix(new Dictionary<string, PcmAudio>(), 96000, 48000, after);
    for (int i = 0; i < samples.Length; i++) Near(samples[i], after[i], 1e-6);
});
Test("nested pan and gain multiply rather than replacing child automation", () =>
{
    var p = SampleProject.Create(); var ids = SequenceEdits.Nest(p, p.Tracks.SelectMany(t => t.Clips).Select(c => c.Id), "Nested");
    p.Clip(ids[1]).Effects.Pan = -1; p.Clip(ids[1]).Effects.Gain.Value = .5;
    var samples = new float[512]; new PreparedAudioMixer(p).Mix(new Dictionary<string, PcmAudio>(), 96000, 48000, samples);
    Check(samples.Where((x, i) => i % 2 == 1).All(x => x == 0)); Check(samples.Any(x => Math.Abs(x) > .001));
});
Test("audio transition power weights sum to one", () =>
{
    var p = SampleProject.Create(); TimelineEdits.Split(p, ["score-cut"], 432); var t = p.Tracks.First(t => t.Id == "a1"); var right = t.Clips.Single(c => c.Start == 432);
    TransitionEdits.Add(p, "score-cut", TransitionKind.EqualPowerAudio, 48);
    p.Clip("score-cut").Effects.Gain.Value = 1; right.Effects.Gain.Value = 1;
    var audio = new PreparedFramePlanner(p).Evaluate(431.5).Audio;
    Check(audio.Length == 2); Near(1, audio[0].Gain * audio[0].Gain + audio[1].Gain * audio[1].Gain);
});
Test("prepared mixer rejects missing source rather than rendering silence", () =>
{
    var p = SampleProject.Create(); p.Asset("score").Source = ""; var mixer = new PreparedAudioMixer(p);
    Reject(() => { var samples = new float[32]; mixer.Mix(new Dictionary<string, PcmAudio>(), 96000, 48000, samples); });
});
Test("strict schema rejects unknown media and transition enums", () =>
{
    var p = SampleProject.Create(); p.Assets[0].Kind = (MediaKind)200; Reject(() => ProjectValidation.Validate(p));
    p = Transition(); p.Tracks[0].Transitions[0].Kind = (TransitionKind)200; Reject(() => ProjectValidation.Validate(p));
});
Test("nesting applies existing track gain and solo only once", () =>
{
    var p = SampleProject.Create(); p.Tracks[3].Gain = .4; p.Tracks[3].Solo = true;
    var before = new PreparedFramePlanner(p).Evaluate(96).Audio.Single();
    SequenceEdits.Nest(p, p.Tracks.SelectMany(t => t.Clips).Select(c => c.Id), "Nested");
    Near(before.Gain, new PreparedFramePlanner(p).Evaluate(96).Audio.Single().Gain);
});
Test("clipboard camera collisions do not mutate an existing group", () =>
{
    var p = SampleProject.Create(); var id = SequenceEdits.CreateMulticam(p, "Group", [new("ridge", "A"), new("coast", "B")]); p.Clip("ridge-cut").AssetId = id;
    var clipboard = new TimelineClipboard(); clipboard.Copy(p, ["ridge-cut"]);
    var target = ProjectSnapshot.Clone(p); target.Asset("ridge").Name = "Different media";
    var s = new EditorSession(target); s.Execute("Paste", q => clipboard.Paste(q, 1000));
    Check(s.Project.Asset(id).Angles[0].AssetId == "ridge"); Check(s.Project.Assets.Count(a => a.Kind == MediaKind.Multicam) == 2);
});
int failures = 0;
foreach (var (name, run) in tests) { try { run(); Console.WriteLine("PASS " + name); } catch (Exception e) { failures++; Console.Error.WriteLine("FAIL " + name + "\n" + e); } }
Console.WriteLine($"{tests.Count - failures}/{tests.Count} parity regressions passed.");
if (failures != 0) { Environment.ExitCode = 1; return; }

// Warm query timing excludes preparation, which is reported separately. No speed threshold is asserted.
const int clipCount = 80000, queries = 600;
var dense = new VideoProject { Width = 320, Height = 180, Assets = [new() { Id = "g", Kind = MediaKind.Generator, Source = "ridge", DurationSeconds = 86400 }],
    Tracks = [new() { Id = "v", Clips = Enumerable.Range(0, clipCount).Select(i => new TimelineClip { Id = "c" + i, AssetId = "g", Start = i * 12L, Duration = 12 }).ToList() }] };
long allocated = GC.GetAllocatedBytesForCurrentThread(); var clock = Stopwatch.StartNew(); var preparedIndex = new ProjectIndex(dense); clock.Stop();
double preparationMs = clock.Elapsed.TotalMilliseconds; long preparationBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
long[] frames = Enumerable.Range(0, queries).Select(i => (i * 15485863L) % (clipCount * 12)).ToArray();
long Scan() { long total = 0; foreach (var f in frames) foreach (var t in dense.Tracks) foreach (var c in t.Clips) if (c.Contains(f)) total += c.Start; return total; }
long Indexed() { long total = 0; foreach (var f in frames) foreach (var t in preparedIndex.Tracks) if (t.At(f) is { } c) total += c.Start; return total; }
Check(Scan() == Indexed()); _ = Indexed(); _ = Scan();
var linear = new List<double>(); var indexed = new List<double>();
for (int repeat = 0; repeat < 5; repeat++) { clock.Restart(); _ = Scan(); linear.Add(clock.Elapsed.TotalMilliseconds); clock.Restart(); _ = Indexed(); indexed.Add(clock.Elapsed.TotalMilliseconds); }
linear.Sort(); indexed.Sort();
var benchmark = new { runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
    clips = clipCount, queries, preparationMs, preparationBytes, linearMedianMs = linear[2], indexedMedianMs = indexed[2], querySpeedup = linear[2] / indexed[2], methodology = "600 exact active-clip queries; five warmed repeats, medians; original linear scan versus prepared binary search. CPU lookup only, not end-to-end GPU frame time." };
Directory.CreateDirectory("artifacts/benchmarks"); File.WriteAllText("artifacts/benchmarks/timeline.json", JsonSerializer.Serialize(benchmark, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(benchmark));
