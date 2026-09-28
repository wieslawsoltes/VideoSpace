using System.Text.Json;
using VideoSpace.Core;
using VideoSpace.Effects;
using VideoSpace.Editing;
using VideoSpace.Audio;

var p = SampleProject.Create();
p.Clip("ridge-cut").Effects.Scale.SetKey(0, 1); p.Clip("ridge-cut").Effects.Scale.SetKey(120, 1.5, Interpolation.Smooth); p.Clip("ridge-cut").Effects.Scale.SetKey(239, 1.2);
p.Clip("coast-cut").Effects.Opacity.SetKey(0, .8, Interpolation.Hold); p.Clip("coast-cut").Effects.Opacity.SetKey(100, .5);
p.Clip("dunes-cut").Effects.Exposure.Value = .4; p.Clip("dunes-cut").Effects.Temperature.Value = .2; p.Clip("dunes-cut").Effects.Vignette.Value = .3;
ProjectValidation.Validate(p);
var cases = Enumerable.Range(0, 865).Where(f => f % 13 == 0 || new[] { 0, 35, 36, 47, 48, 72, 119, 120, 191, 192, 239, 240, 340, 431, 432, 671, 672, 863, 864 }.Contains(f)).Select(frame => new { frame, plan = FramePlanner.Evaluate(p, frame) }).ToArray();
string directory = args.Length > 0 ? args[0] : "artifacts/fixtures"; Directory.CreateDirectory(directory);
File.WriteAllText(Path.Combine(directory, "frame-plans.json"), JsonSerializer.Serialize(new { project = p, cases }, ProjectSnapshot.Options));
var shortProject = SampleProject.Create(); shortProject.InPoint = 72; shortProject.OutPoint = 108;
File.WriteAllText(Path.Combine(directory, "short.videospace"), ProjectSnapshot.Write(shortProject));
Console.WriteLine($"Wrote {cases.Length} independent C# frame-plan fixtures.");
var graphs = new List<object>();
void Graph(string name, VideoProject project, double[] frames, long[] audioStarts)
{
    ProjectValidation.Validate(project);
    var planner = new PreparedFramePlanner(project);
    var mixer = new PreparedAudioMixer(project);
    var pcm = new Dictionary<string, PcmAudio>();
    graphs.Add(new { name, project,
        cases = frames.Select(frame => new { frame, plan = planner.Evaluate(frame) }).ToArray(),
        audio = audioStarts.Select(firstSample => { var data = new float[256]; mixer.Mix(pcm, firstSample, 48000, data); return new { firstSample, sampleRate = 48000, count = 128, data }; }).ToArray() });
}
foreach (var kind in new[] { TransitionKind.CrossDissolve, TransitionKind.DipToBlack, TransitionKind.DipToWhite, TransitionKind.WipeLeft, TransitionKind.WipeRight })
{
    var project = ProjectSnapshot.Clone(p); TransitionEdits.Add(project, "ridge-cut", kind, 48);
    TimelineEdits.Split(project, ["score-cut"], 432); TransitionEdits.Add(project, "score-cut", TransitionKind.EqualPowerAudio, 48);
    Graph(kind.ToString(), project, [215,216,220.5,239,239.5,240,263,264,408,431.5,455], [432000,863900,864000]);
}
var nested = ProjectSnapshot.Clone(p);
TransitionEdits.Add(nested, "ridge-cut", TransitionKind.CrossDissolve, 48);
var nestedIds = SequenceEdits.Nest(nested, nested.Tracks.SelectMany(t => t.Clips).Select(c => c.Id), "Nested fixture");
nested.Clip(nestedIds[0]).Effects.Scale.Value = .75;
nested.Clip(nestedIds[0]).Effects.Rotation.Value = 10;
nested.Clip(nestedIds[1]).Effects.Pan = -.3;
nested.Clip(nestedIds[1]).Effects.Gain.Value = .6;
Graph("Nested", nested, [0,36,96,216,239.5,263,432], [96000,431900]);
var multicam = ProjectSnapshot.Clone(p);
string group = SequenceEdits.CreateMulticam(multicam, "Two cameras", [new("ridge","A",1),new("coast","B",2)], true);
multicam.Clip("ridge-cut").AssetId = group;
SequenceEdits.SwitchCamera(multicam, ["ridge-cut"], 1, 100);
TimelineEdits.Split(multicam, ["ridge-cut"], 75);
Graph("Multicam and shifted key origin", multicam, [0,74,75,99,100,101,239], [96000]);
File.WriteAllText(Path.Combine(directory,"graph-plans.json"), JsonSerializer.Serialize(graphs, ProjectSnapshot.Options));
var nestedShort = SampleProject.Create();
TransitionEdits.Add(nestedShort,"ridge-cut",TransitionKind.CrossDissolve,48);
SequenceEdits.Nest(nestedShort,nestedShort.Tracks.SelectMany(t=>t.Clips).Select(c=>c.Id),"Export nest");
nestedShort.InPoint=216;nestedShort.OutPoint=252;
File.WriteAllText(Path.Combine(directory,"nested.videospace"),ProjectSnapshot.Write(nestedShort));
Console.WriteLine("Wrote recursive composition and sample-clock equivalence fixtures.");
