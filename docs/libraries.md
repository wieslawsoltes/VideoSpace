# Reusable libraries

## Headless editing

Core, Editing, Timeline, Effects, Audio and Documents target .NET 10 without Uno dependencies.

```csharp
using VideoSpace.Core;
using VideoSpace.Editing;
using VideoSpace.Effects;

var session = new EditorSession(SampleProject.Create());
session.Execute("Split opening shot", p =>
    TimelineEdits.Split(p, ["ridge-cut"], frame: 96));
FramePlan plan = FramePlanner.Evaluate(session.Project, frame: 120);
session.Undo();
```

Use transactions for user-visible edits; direct model construction is appropriate before session creation. `Start`/`Duration` use sequence frames and `SourceIn` uses seconds. Video sound lives on explicit audio clips linked with `TimelineEdits.Link`. Asset identifiers alone do not decode media: the host supplies frames.

## Native rendering and Uno embedding

`FrameRenderer.Draw` targets a host-owned Skia canvas; `Png` encodes a frame; `SetImage` supplies an image. The host owns graphics-context lifecycle.

Merge `VideoSpace.Controls.StudioTheme` after `XamlControlsResources`, reference Workbench, and create:

```csharp
window.Content = new StudioWorkbench(
    new EditorSession(SampleProject.Create()),
    new MediaServices());
```

Include `VideoSpace.Media/Web/*.js` as browser embedded resources as demonstrated by the App project. Dispose the workbench on teardown. Supply a suitable font/typeface before constructing controls.

Controls can be reused individually: PanelHost, SplitHandle, TimelineView, MonitorView, ProjectBinView, ValueField, TimecodeField, AudioMeterView, AssetThumbnail and StudioButton. They use Uno primitives for input, focus, accessibility and text editing; custom templates/composition do not replace the entire WinUI stack.

## Browser compositor

```javascript
const compositor = new VideoSpaceGPU.Compositor(canvas, console.warn);
await compositor.initialize();
compositor.draw(framePlan, [
  { layer: framePlan.layers[0], source: decodedVideoElement }
]);
compositor.dispose();
```

The plan is camelCase JSON matching the C# record. Sources must be decoded and origin-clean. Media.js supplies local files, decoders and audio; Export.js supplies bounded real-time recording. These are not a general offline codec framework.

## Packaging

```sh
dotnet pack src/VideoSpace.Core -c Release -o artifacts/packages
dotnet pack src/VideoSpace.Controls -c Release -o artifacts/packages
```

All ten libraries are packable. Uno packages build browser/native targets and need the browser workload. Desktop-only development builds are not complete multi-target release packages. Release artifacts are distinct from public-feed publication; NuGet.org publishing is not automatic.
