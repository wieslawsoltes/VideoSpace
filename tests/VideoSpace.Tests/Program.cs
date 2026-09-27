using System.Text;
using VideoSpace.Core;
using VideoSpace.Editing;
using VideoSpace.Timeline;
using VideoSpace.Documents;
using VideoSpace.Effects;
using VideoSpace.Audio;
using VideoSpace.Rendering;

var tests = new List<(string Name, Action Run)>();
void Test(string name, Action run) => tests.Add((name, run));
void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}; got {actual}."); }
void True(bool value) { if (!value) throw new Exception("Assertion failed."); }
void Near(double expected, double actual, double epsilon = 1e-6) { if (Math.Abs(expected - actual) > epsilon) throw new Exception($"Expected {expected}; got {actual}."); }
void Throws(Action action) { try { action(); } catch { return; } throw new Exception("Expected rejection."); }
EditorSession Session() => new(SampleProject.Create());
VideoProject Simple() { var p = SampleProject.Create(); p.Tracks = [p.Tracks[0]]; p.Markers.Clear(); p.Captions.Clear(); return p; }

Test("sample validates", () => ProjectValidation.Validate(SampleProject.Create()));
Test("sample duration", () => Equal(864L, SampleProject.Create().Duration));
Test("project manifest round trip", () => { var p = SampleProject.Create(); Equal(ProjectFile.Save(p), ProjectFile.Save(ProjectFile.Open(ProjectFile.Save(p)))); });
Test("frame rate exact rational", () => { Near(1001, FrameRate.Ntsc.Seconds(30000)); Equal(30000L, FrameRate.Ntsc.Frames(1001)); });
Test("invalid frame rate", () => Throws(() => new FrameRate(1, 0).Validate()));
Test("non drop timecode", () => { Equal("01:02:03:12", Timecode.Format(89364, FrameRate.Film)); Equal(89364L, Timecode.Parse("01:02:03:12", FrameRate.Film)); });
Test("drop frame minute boundary", () => { Equal("00:01:00;02", Timecode.Format(1800, FrameRate.Ntsc, true)); Equal(1800L, Timecode.Parse("00:01:00;02", FrameRate.Ntsc)); });
Test("drop frame ten minute boundary", () => Equal("00:10:00;00", Timecode.Format(17982, FrameRate.Ntsc, true)));
Test("drop frame hour boundary", () => Equal("01:00:00;00", Timecode.Format(107892, FrameRate.Ntsc, true)));
Test("skipped timecode rejected", () => Throws(() => Timecode.Parse("00:01:00;00", FrameRate.Ntsc)));
Test("timecode invalid fields", () => { Throws(() => Timecode.Parse("00:60:00:00", FrameRate.Film)); Throws(() => Timecode.Parse("-1:00:00:00", FrameRate.Film)); });
Test("drop frame roundtrip 20000 samples", () => { foreach (var rate in new[] { FrameRate.Ntsc, new FrameRate(60000,1001) }) for(long i=0;i<1000000;i+=100) Equal(i, Timecode.Parse(Timecode.Format(i,rate,true),rate)); });
Test("linear keyframes", () => { var v = new AnimatedValue(3); v.SetKey(0, 2); v.SetKey(10, 8); Near(5, v.At(5)); Near(8, v.At(100)); });
Test("hold keyframes", () => { var v = new AnimatedValue(); v.SetKey(0, 2, Interpolation.Hold); v.SetKey(10, 8); Near(2, v.At(9)); Near(8, v.At(10)); });
Test("unique sorted keyframes", () => { var v = new AnimatedValue(); v.SetKey(10, 3); v.SetKey(0, 4); v.SetKey(10, 7); Equal(2, v.Keys.Count); Near(7, v.At(10)); });
Test("split respects half open range", () => { var s=Session(); s.Execute("Split",p=>TimelineEdits.Split(p,["ridge-cut"],96)); Equal(5,s.Project.Tracks[0].Clips.Count); Equal(96L,s.Project.Clip("ridge-cut").Duration); var r=s.Project.Tracks[0].Clips.Single(c=>c.Start==96); Near(4,r.SourceIn); Equal(144L,r.Duration); });
Test("split at boundary is no op", () => { var s=Session(); True(!s.Execute("Split",p=>TimelineEdits.Split(p,["ridge-cut"],0))); True(!s.CanUndo); });
Test("split preserves animated right boundary", () => { var s=Session(); s.Project.Clip("ridge-cut").Effects.Scale.SetKey(0,1); s.Project.Clip("ridge-cut").Effects.Scale.SetKey(200,3); s.Execute("Split",p=>TimelineEdits.Split(p,["ridge-cut"],100)); var right=s.Project.Tracks[0].Clips.Single(c=>c.Start==100); Near(2,right.Effects.Scale.At(0)); Near(3,right.Effects.Scale.At(100)); });
Test("undo and redo restore exact state", () => { var s=Session(); string before=ProjectFile.Save(s.Project); s.Execute("Split",p=>TimelineEdits.Split(p,["ridge-cut"],96)); string after=ProjectFile.Save(s.Project); True(s.Undo()); Equal(before,ProjectFile.Save(s.Project)); True(s.Redo()); Equal(after,ProjectFile.Save(s.Project)); });
Test("failed edit rolls back", () => { var s=Session(); string before=ProjectFile.Save(s.Project); Throws(()=>s.Execute("Invalid",p=>TimelineEdits.Move(p,["ridge-cut"],-50))); Equal(before,ProjectFile.Save(s.Project)); True(!s.CanUndo); });
Test("overlap rejected atomically", () => { var s=Session(); Throws(()=>s.Execute("Move",p=>TimelineEdits.Move(p,["coast-cut"],-10))); Equal(240L,s.Project.Clip("coast-cut").Start); });
Test("locked track rejection", () => { var s=Session(); s.Project.Tracks[0].Locked=true; Throws(()=>s.Execute("Split",p=>TimelineEdits.Split(p,["ridge-cut"],50))); Equal(4,s.Project.Tracks[0].Clips.Count); });
Test("trim head updates source", () => { var s=Session(); s.Execute("Trim",p=>TimelineEdits.Trim(p,"ridge-cut",24,true)); Equal(24L,s.Project.Clip("ridge-cut").Start); Near(1,s.Project.Clip("ridge-cut").SourceIn); Equal(216L,s.Project.Clip("ridge-cut").Duration); });
Test("trim tail", () => { var s=Session(); s.Execute("Trim",p=>TimelineEdits.Trim(p,"ridge-cut",200,false)); Equal(200L,s.Project.Clip("ridge-cut").Duration); });
Test("rolling edit preserves combined duration", () => { var s=Session(); s.Execute("Roll",p=>TimelineEdits.Roll(p,"ridge-cut",264)); Equal(264L,s.Project.Clip("coast-cut").Start); Equal(168L,s.Project.Clip("coast-cut").Duration); Near(1,s.Project.Clip("coast-cut").SourceIn); });
Test("rate stretch preserves source extent", () => { var s=Session(); s.Execute("Stretch",p=>TimelineEdits.RateStretch(p,"ridge-cut",120)); Near(2,s.Project.Clip("ridge-cut").Speed); Equal(120L,s.Project.Clip("ridge-cut").Duration); });
Test("slip preserves timeline", () => { var s=Session(); s.Execute("Slip",p=>TimelineEdits.Slip(p,"ridge-cut",24)); Near(1,s.Project.Clip("ridge-cut").SourceIn); Equal(0L,s.Project.Clip("ridge-cut").Start); });
Test("ripple delete closes gap", () => { var s=new EditorSession(Simple()); s.Execute("Ripple",p=>TimelineEdits.Delete(p,["coast-cut"],true)); Equal(240L,s.Project.Clip("dunes-cut").Start); Equal(672L,s.Project.Duration); });
Test("ripple protects crossing audio", () => { var s=Session(); Throws(()=>s.Execute("Ripple",p=>TimelineEdits.Delete(p,["coast-cut"],true))); Equal(864L,s.Project.Duration); });
Test("insert splits crossing clip", () => { var s=new EditorSession(Simple()); s.Execute("Insert",p=>TimelineEdits.Insert(p,"dunes","v1",96,0,48,false)); Equal(912L,s.Project.Duration); True(s.Project.Tracks[0].Clips.Any(c=>c.Start==144)); });
Test("overwrite retains handles", () => { var s=new EditorSession(Simple()); s.Execute("Overwrite",p=>TimelineEdits.Insert(p,"dunes","v1",96,0,48,true)); Equal(864L,s.Project.Duration); Equal(6,s.Project.Tracks[0].Clips.Count); var tail=s.Project.Tracks[0].Clips.Single(c=>c.Start==144); Near(6,tail.SourceIn); });
Test("linked selection", () => { var s=Session(); s.Execute("Link",p=>TimelineEdits.Link(p,["ridge-cut","score-cut"],false)); s.Select("ridge-cut"); Equal(2,s.Selection.Count); });
Test("new edit clears redo", () => { var s=Session(); s.Execute("Split",p=>TimelineEdits.Split(p,["ridge-cut"],96)); s.Undo(); s.Execute("Marker",p=>p.Markers.Add(new(100,"Test"))); True(!s.CanRedo); });
Test("delete removes stale selection", () => { var s=Session(); s.Select("title-cut"); s.Execute("Delete",p=>TimelineEdits.Delete(p,s.Selection,false)); Equal(0,s.Selection.Count); });
Test("timeline clip hit", () => { var g=new TimelineGeometry(); var p=SampleProject.Create(); var hit=g.Hit(p,g.X(100),g.Y(2)+20); Equal("ridge-cut",hit.ClipId); Equal(HitPart.Body,hit.Part); });
Test("timeline edge hit", () => { var g=new TimelineGeometry(); Equal(HitPart.Head,g.Hit(SampleProject.Create(),g.X(240)+2,g.Y(2)+10).Part); });
Test("zoom preserves pointer anchor", () => { var g=new TimelineGeometry { ScrollFrame=100 }; var before=g.Frame(400); g.ZoomAt(2,400); Equal(before,g.Frame(400)); });
Test("snap to edit boundary", () => { var g=new TimelineGeometry(); Equal(240L,g.Snap(SampleProject.Create(),245,[],999)); });
Test("frame plan track order", () => { var plan=FramePlanner.Evaluate(SampleProject.Create(),96); Equal("ridge",plan.Layers[0].AssetId); Equal("title",plan.Layers[1].AssetId); Equal("credit",plan.Layers[2].AssetId); Equal(1,plan.Audio.Length); });
Test("muted video not composed", () => { var p=SampleProject.Create(); p.Tracks[0].Muted=true; True(FramePlanner.Evaluate(p,96).Layers.All(l=>l.AssetId!="ridge")); });
Test("solo audio", () => { var p=SampleProject.Create(); p.Tracks[4].Solo=true; Equal(0,FramePlanner.Evaluate(p,96).Audio.Length); });
Test("fade exact endpoints", () => { var c=SampleProject.Create().Clip("title-cut"); Near(0,FramePlanner.Envelope(c,0)); Near(0,FramePlanner.Envelope(c,c.Duration-1)); Near(1,FramePlanner.Envelope(c,80)); });
Test("SRT roundtrip", () => { var p=SampleProject.Create(); var captions=SubRip.Read(SubRip.Write(p.Captions,p.FrameRate),p.FrameRate); Equal(p.Captions.Count,captions.Count); Equal(p.Captions[0],captions[0]); });
Test("SRT invalid timestamp", () => Throws(()=>SubRip.Read("1\n00:70:00,000 --> 00:80:00,000\nBad",FrameRate.Film)));
Test("EDL cut export", () => { var edl=EdlWriter.Write(SampleProject.Create()); True(edl.Contains("001  AX")); True(edl.Contains("00:00:10:00")); });
Test("EDL rejects unrepresented speed", () => { var p=SampleProject.Create(); p.Clip("ridge-cut").Speed=2; Throws(()=>EdlWriter.Write(p)); });
Test("waveform peak reduction", () => { var pcm=new PcmAudio(48000,1,[0,.5f,-.9f,.2f]); var peaks=Waveform.Reduce(pcm,2); Near(.5,peaks[0]); Near(.9,peaks[1]); });
Test("PCM resampling", () => { var pcm=new PcmAudio(8000,1,[0,1,0]); Near(.5,pcm.At(.5/8000,0)); Near(0,pcm.At(-1,0)); });
Test("WAV RIFF header", () => { var bytes=WavWriter.Write(new(48000,2,[0,.5f,-.5f,0])); Equal("RIFF",Encoding.ASCII.GetString(bytes,0,4)); Equal(52,bytes.Length); });
Test("audio sample mixing", () => { var p=SampleProject.Create(); var samples=new float[960]; AudioMixer.Mix(p,new Dictionary<string,PcmAudio>(),48000,48000,samples); True(samples.Any(s=>Math.Abs(s)>.001)); True(samples.All(s=>Math.Abs(s)<=1)); });
Test("mute produces silence", () => { var p=SampleProject.Create(); p.Tracks[3].Muted=true; var samples=new float[960]; AudioMixer.Mix(p,new Dictionary<string,PcmAudio>(),48000,48000,samples); True(samples.All(s=>s==0)); });
Test("effect preset mutates render plan", () => { var p=SampleProject.Create(); EffectPresets.Apply(p.Clip("ridge-cut"),"Black & white",p.FrameRate); Near(0,FramePlanner.Evaluate(p,0).Layers[0].Saturation); });
Test("PNG renders an actual frame", () => { using var renderer=new FrameRenderer(); var p=SampleProject.Create(); p.Width=320; p.Height=180; var bytes=renderer.Png(FramePlanner.Evaluate(p,200)); True(bytes.Length>1000); Equal((byte)137,bytes[0]); });
Test("duplicate ids rejected", () => { var p=SampleProject.Create(); p.Assets[1].Id=p.Assets[0].Id; Throws(()=>ProjectValidation.Validate(p)); });
Test("invalid source extent rejected", () => { var p=SampleProject.Create(); p.Asset("ridge").Kind=MediaKind.Video; p.Asset("ridge").DurationSeconds=1; Throws(()=>ProjectValidation.Validate(p)); });
Test("unknown schema rejected", () => { var p=SampleProject.Create(); p.SchemaVersion=99; Throws(()=>ProjectValidation.Validate(p)); });

int failed=0;
foreach(var (name,run) in tests) { try { run(); Console.WriteLine("PASS "+name); } catch(Exception ex) { failed++; Console.Error.WriteLine("FAIL "+name+"\n"+ex); } }
Console.WriteLine($"{tests.Count-failed}/{tests.Count} tests passed.");
Environment.ExitCode=failed==0?0:1;
