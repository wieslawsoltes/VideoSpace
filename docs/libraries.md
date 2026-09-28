# Reusable libraries

The Core, Editing, Timeline, Effects, Audio and Documents packages target .NET 10 without Uno. Rendering adds Skia; Media/Controls/Workbench integrate the browser/native Uno hosts. All ten projects are packable; public-feed publication is a separate release operation.

## Transactional editing and prepared frames

```csharp
using VideoSpace.Core;
using VideoSpace.Editing;
using VideoSpace.Effects;

var session = new EditorSession(SampleProject.Create());
session.Execute("Dissolve", project =>
    TransitionEdits.Add(project, "ridge-cut", TransitionKind.CrossDissolve, 48));

var planner = new PreparedFramePlanner(session.Index);
FramePlan frame = planner.Evaluate(240);
// Reuse planner until session.Index changes. Do not mutate its project directly.
session.Undo();
```

`SourceIn` is seconds; timeline `Start`/`Duration` use sequence frames. Imported sound belongs on explicit audio clips. Link related clip IDs through `TimelineEdits.Link`. Mutations inside `Execute` validate and either commit atomically or restore the previous root document.

## Nesting and camera sources

```csharp
string[] nested = [];
session.Execute("Nest selection", p => nested = SequenceEdits.Nest(
    p, p.Tracks.SelectMany(t => t.Clips).Select(c => c.Id), "Compound"));
session.OpenSequence(session.Project.Clip(nested[0]).AssetId);
// Edits target the child; RootProject remains the persistence authority.
session.NavigateUp();
```

`SequenceEdits.CreateMulticam` accepts `CameraAngle` records with source identifiers and offsets. `SwitchCamera` writes held angle keys at the playhead. Source-handle and audio-policy validation is part of the model, not just the workbench.

## Audio

```csharp
using VideoSpace.Audio;

var mixer = new PreparedAudioMixer(session.Project);
var stereo = new float[960 * 2];
mixer.Mix(new Dictionary<string, PcmAudio>(), firstSample: 48000,
    sampleRate: 48000, stereo);
```

The empty dictionary supports generated tone sources only. Supply decoded `PcmAudio` for imported sources; missing active PCM rejects rather than silently producing silence. Reuse the compiled mixer for unchanged projects. Caller owns the PCM arrays.

## Uno controls and Skia rendering

Merge `VideoSpace.Controls.StudioTheme` after `XamlControlsResources`, then embed `StudioWorkbench` with an `EditorSession` and `MediaServices`. Include all `VideoSpace.Media/Web/*.js` as browser embedded resources, as the App project demonstrates. Dispose host resources on teardown. Individual timelines, monitors, panels, bins, numeric/timecode fields and icons can be used independently.

`FrameRenderer.Draw` renders into a host-owned Skia canvas. `SetImage` supplies image data; `Png` encodes a plan. Source identifiers alone are not a video decoder. Low-level focus, accessibility and text input continue to use Uno primitives.

## JavaScript packet index and decoder

```javascript
const index = new VideoSpaceWebMIndex.Index(new Uint8Array(webmBytes));
const decoder = await VideoSpaceIndexedVideo.Decoder.open(index);
try {
  const lease = await decoder.frame(1.25, abortSignal);
  try {
    context2d.drawImage(lease.source, 0, 0);
    console.log(lease.timestamp); // Selected source presentation time, microseconds.
  } finally {
    lease.release();
  }
} finally {
  decoder.dispose();
}
```

Load WebMIndex.js before creating an IndexedVideo decoder. The parser itself has no DOM dependency. Keep the indexed bytes alive while the index/decoder is used, serialize requests per decoder, and release every returned lease. Unsupported indexed formats have a distinct error type. The export-scoped `Sources` pool provides bounded local-Blob fallback decisions.

The compositor accepts origin-clean decoded inputs and recursive plans. Hosts can use `VideoSpaceGPU.Compositor` without Uno; `draw` does not transfer ownership of source images. Await submitted GPU work before releasing transient input leases.

## Packaging

```sh
dotnet pack src/VideoSpace.Core -c Release -o artifacts/packages
dotnet pack src/VideoSpace.Controls -c Release -o artifacts/packages
```

Multi-target Uno packages need the browser workload. `VideoSpaceDesktopOnly=true` is a development convenience, not a complete multi-target release build. Retain upstream notices when distributing packages.
