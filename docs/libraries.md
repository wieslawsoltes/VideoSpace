# Reusable libraries

## Headless C# editing

Core, Editing, Timeline, Effects, Audio and Documents target .NET 10 without Uno dependencies.

```csharp
using VideoSpace.Core;
using VideoSpace.Editing;
using VideoSpace.Effects;

var session = new EditorSession(SampleProject.Create());
session.Execute("Split", project =>
    TimelineEdits.Split(project, ["ridge-cut"], frame: 96));

var clipboard = new TimelineClipboard();
clipboard.Copy(session.Project, ["title-cut", "credit-cut"]);
session.Execute("Paste graphics", project => clipboard.Paste(project, at: 500));

FramePlan frame = FramePlanner.Evaluate(session.Project, frame: 550);
session.Undo();
```

Use transactions for user-visible edits. Direct construction is appropriate before creating a session. Start/Duration are sequence frames; SourceIn is seconds. Audio belongs on explicit audio-track clips, with related clip IDs linked through TimelineEdits.Link. Clipboard data contains metadata, not media bytes. Cross-timebase pastes reject silent rounding.

## Uno embedding and native drawing

Merge StudioTheme after XamlControlsResources and reference VideoSpace.Workbench:

```csharp
using VideoSpace.Media;
using VideoSpace.Workbench;

window.Content = new StudioWorkbench(
    new EditorSession(SampleProject.Create()),
    new MediaServices());
```

Include all `VideoSpace.Media/Web/*.js` as browser embedded resources as demonstrated by the App project. The bridge uses explicit JSON-only named JavaScript imports. Dispose the workbench during teardown. Supply a suitable font/typeface before constructing controls.

Controls can be reused individually: PanelHost, SplitHandle, TimelineView, MonitorView, ProjectBinView, ValueField, TimecodeField, AudioMeterView, AssetThumbnail and StudioButton. PanelHost separates stable tab keys from captions through SetCaption. Low-level input, focus, accessibility and text editing still use Uno primitives.

FrameRenderer.Draw targets a host-owned Skia canvas. Png encodes a evaluated composition; SetImage supplies image data. A media identifier alone is not a decoder. The host owns the native graphics context; native video decode/encode and live audio adapters are not bundled.

## Standalone browser graphics and export

```javascript
const compositor = new VideoSpaceGPU.Compositor(canvas, console.warn);
await compositor.initialize();
compositor.draw(framePlan, [
  { layer: framePlan.layers[0], source: decodedVideoElement }
]);
compositor.dispose();
```

Plans use camelCase JSON matching the C# records. Sources must be decoded and origin-clean. Automatic selection prefers hardware WebGPU and selects WebGL2 for reported software adapters. Canvas 2D is reduced preview only.

WebM.js exposes `VideoSpaceWebM.Muxer`, accepting encoded VP8/VP9 and Opus chunks. OfflineExport.js exposes `VideoSpaceOffline.mix`, `inputs`, `run` and `cancel`. The mixer consumes host-decoded AudioBuffers; run combines the project evaluator, local media adapter, compositor and WebCodecs, emitting progress and downloading a completed WebM. Media.js supplies browser files/decoders/storage. These modules are MIT licensed and independent of the Uno workbench, but they are not a universal professional codec suite.

An offline output frame schedule does not remove browser source-seek limitations. Keep source decoding, encoding capability checks, cancellation and memory budgets explicit in another host.

## Packaging

```sh
dotnet pack src/VideoSpace.Core -c Release -o artifacts/packages
dotnet pack src/VideoSpace.Controls -c Release -o artifacts/packages
```

All ten libraries are configured for packaging. Uno packages target browser/native and require the browser workload. Desktop-only development builds are not complete multi-target packages. Public-feed publication is a separate operation from generating release artifacts.
