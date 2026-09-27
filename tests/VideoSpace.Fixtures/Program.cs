using System.Text.Json;
using VideoSpace.Core;
using VideoSpace.Effects;

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
