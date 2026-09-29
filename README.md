<div align="center">

<img src="src/VideoSpace.App/Assets/Icons/icon.svg" width="88" height="88" alt="VideoSpace" />

# VideoSpace

**Local-first nonlinear editing. Custom Uno controls. GPU composition.**

[Open the editor](https://wieslawsoltes.github.io/VideoSpace/) · [User guide](docs/user-guide.md) · [Architecture](docs/architecture.md) · [Libraries](docs/libraries.md)

[![Build](https://github.com/wieslawsoltes/VideoSpace/actions/workflows/build.yml/badge.svg)](https://github.com/wieslawsoltes/VideoSpace/actions/workflows/build.yml)
[![Desktop](https://github.com/wieslawsoltes/VideoSpace/actions/workflows/desktop.yml/badge.svg)](https://github.com/wieslawsoltes/VideoSpace/actions/workflows/desktop.yml)
[![Engine](https://github.com/wieslawsoltes/VideoSpace/actions/workflows/engine.yml/badge.svg)](https://github.com/wieslawsoltes/VideoSpace/actions/workflows/engine.yml)
[![MIT](https://img.shields.io/badge/license-MIT-8582CD)](LICENSE)
[![NuGet](https://img.shields.io/nuget/vpre/VideoSpace.Core.svg?label=NuGet)](https://www.nuget.org/packages/VideoSpace.Core)
[![Downloads](https://img.shields.io/nuget/dt/VideoSpace.Core.svg)](https://www.nuget.org/packages/VideoSpace.Core)

</div>

---

VideoSpace is a real Uno WebAssembly and native desktop application: a frame-addressed C# editing engine, reusable custom controls, and a local browser media pipeline. No media upload service is required. The NORTH sample, vector icons and branding are original.

**Version `0.3.0-alpha.1`** adds two-source transitions, editable nested sequences, manually synchronized multicamera sources, prepared timeline/render/audio graphs, and indexed VP8/VP9 source decoding for offline WebM export. It is not complete or pixel-identical Adobe Premiere Pro parity. Read the [capability ledger](docs/limitations.md) for explicit platform and format boundaries.

## Edit, compose, deliver

The compact workspace contains Source and Program monitors, project bins, a multitrack timeline, Effect Controls, color presets, captions, history and metering. Resize panel dividers directly; switch among Editing, Color, Effects, Audio and Captions layouts. The controls are composed and styled for this application on Uno's input, focus and accessibility infrastructure.

Actual editing transactions support linked selection, snapping, movement, razor cuts, trimming, guarded ripple, roll, slip, rate stretch, insert/overwrite, clipboard operations, track controls and undo/redo. Invalid operations roll back rather than leaving partial edits. Smooth keyframe curves retain their original sample positions when clips are split or trimmed.

**Transitions** include cross dissolve, dips to black/white, directional wipes and constant-gain/power audio crossfades. They use real source handles and support centered, start-at-cut and end-at-cut alignment. Exhausted handles and overlapping transitions reject explicitly.

**Nested sequences** preserve editable tracks, effects, transitions, captions, markers and audio. Navigate into the child and return to the parent; saving and undo always retain the root document. Unnesting is guarded when outer transforms, trim or track settings would change the result.

**Multicamera sources** combine 2–16 sources with manual offsets and either fixed-angle audio or audio-follow-video. Insert the group and record camera cuts with keys 1–9 or Effect Controls. Automatic synchronization and a simultaneous camera grid are not implemented.

## Performance architecture

Prepared per-revision indexes replace repeated full-timeline scans for active clips, hit tests and visible ranges. Frame evaluation memoizes the paused result; the Uno/browser bridge avoids retransmitting unchanged presentation plans. The GPU graph renders nested sequences and transitions within one context, reuses targets/textures/uniforms, and skips unchanged source uploads. Audio compiles time/gain/pan/camera/transition chains once and queries only voices intersecting each sample block.

Tests measure an 80,000-clip active-query workload separately from index construction. Browser checks assert no additional evaluations, uploads or presentation frames while the editor is idle. These are scoped CPU/cache measurements, not a claim about all hardware or end-to-end export speed. See [performance and resource ownership](docs/performance.md).

## Platform matrix

| Capability | Browser | Native desktop |
| --- | --- | --- |
| Shared Uno editing workspace and transactions | Yes | Yes |
| Nested/camera/transition model and controls | Yes | Yes |
| Images, titles and generated footage | Yes | Yes |
| Imported video/audio playback | Browser-supported sources | Not yet |
| Composition | Hardware WebGPU → WebGL2 → reduced Canvas preview | Host-owned Skia canvas |
| Video export | Offline VP9/VP8 + Opus WebM | Not yet |
| Indexed source selection | Supported single-video VP8/VP9 WebM/Matroska | Not yet |
| PNG, native manifests, SRT, cuts-only EDL | Yes | Yes |
| Generated/nested PCM WAV | Yes | Yes |
| Live audio output | Web Audio | Not yet |
| Recovery | IndexedDB manifest and media | Manifest in application storage |

Output frames and stereo PCM have explicit timestamps. For supported indexed sources, packet presentation intervals and decoder output timestamps determine the source frame, including variable intervals and backward requests. Unsupported containers/features use a diagnosed browser-seeking fallback; arbitrary VFR/professional codec accuracy is not claimed. Limits and capability failures are explicit, not hidden frame freezes.

## Try it

Open the browser editor and press Space to play NORTH. Import local files, select an asset to open Source, mark its range, then use Clip → Insert/Overwrite or drag it onto a compatible track. Imported video receives linked audio. Save `.videospace` manifests regularly and keep the source files: IndexedDB recovery is not a backup.

Use Clip → Add / edit transition on an outgoing clip. Use Sequence → Nest selection for a complete temporal selection, or Create multicamera source for manually synchronized angles. The Help menu includes shortcuts, these workflows and export boundaries.

## Build

| Toolchain | Pinned version |
| --- | --- |
| .NET SDK | 10.0.401 |
| Uno SDK / WinUI | 6.7.30 / 6.7.135 |
| SkiaSharp | 3.119.2, matched managed/native ABI |
| Node / Playwright | 22 / 1.63.0 |

```sh
git clone https://github.com/wieslawsoltes/VideoSpace.git
cd VideoSpace
python3 scripts/fetch-assets.py
dotnet run --project tests/VideoSpace.Tests -c Release
dotnet run --project tests/VideoSpace.Parity.Tests -c Release

dotnet workload install wasm-tools --skip-manifest-update
dotnet publish src/VideoSpace.App -f net10.0-browserwasm -c Release \
  -o artifacts/publish -p:WasmShellWebAppBasePath=/VideoSpace/
python3 scripts/collect-site.py artifacts/publish artifacts/site
python3 scripts/serve-site.py --directory artifacts/site --port 4173
```

Open `http://localhost:4173/VideoSpace/`. Production browser GPU/media APIs need a secure context. Append `?gpu=off` to exercise WebGL2.

```sh
dotnet run --project src/VideoSpace.App -f net10.0-desktop \
  -p:VideoSpaceDesktopOnly=true
```

Linux requires a graphical session and Uno's native dependencies. Desktop CI compiles Windows, macOS and Linux; compilation is not native-media certification.

## Download

Every [release](https://github.com/wieslawsoltes/VideoSpace/releases/latest) ships a self-contained, single-file desktop app — no .NET install needed:

| OS | x64 | Arm64 |
| --- | --- | --- |
| Windows | `VideoSpace-<version>-win-x64.zip` | `VideoSpace-<version>-win-arm64.zip` |
| macOS | `VideoSpace-<version>-osx-x64.tar.gz` | `VideoSpace-<version>-osx-arm64.tar.gz` |
| Linux | `VideoSpace-<version>-linux-x64.tar.gz` | `VideoSpace-<version>-linux-arm64.tar.gz` |

Extract and run `VideoSpace` (`VideoSpace.exe` on Windows). Builds are not code-signed yet: on macOS clear the quarantine flag with `xattr -d com.apple.quarantine VideoSpace`; on Windows choose **More info → Run anyway** in SmartScreen. Verify downloads against `SHA256SUMS`. Releases also include the browser build and a source archive.

## NuGet packages

The editor is built from ten MIT-licensed packages that are versioned and released together. Seven of them (`Core` through `Rendering`) target plain `net10.0` and have no UI dependency; only `VideoSpace.Rendering` pulls in SkiaSharp. `Media`, `Controls` and `Workbench` are Uno Platform libraries targeting `net10.0-desktop` and `net10.0-browserwasm`. Every package ships symbols to NuGet.org (`.snupkg`) with SourceLink.

```sh
dotnet add package VideoSpace.Core --prerelease
```

| Package | Version | Downloads | Description |
| --- | --- | --- | --- |
| [VideoSpace.Core](https://www.nuget.org/packages/VideoSpace.Core) | [![NuGet](https://img.shields.io/nuget/vpre/VideoSpace.Core.svg)](https://www.nuget.org/packages/VideoSpace.Core) | [![Downloads](https://img.shields.io/nuget/dt/VideoSpace.Core.svg)](https://www.nuget.org/packages/VideoSpace.Core) | Frame-based project model, rational time, timecode, validation and prepared indexes |
| [VideoSpace.Editing](https://www.nuget.org/packages/VideoSpace.Editing) | [![NuGet](https://img.shields.io/nuget/vpre/VideoSpace.Editing.svg)](https://www.nuget.org/packages/VideoSpace.Editing) | [![Downloads](https://img.shields.io/nuget/dt/VideoSpace.Editing.svg)](https://www.nuget.org/packages/VideoSpace.Editing) | Transactional edits, transitions, nesting, camera cuts, clipboard and undo/redo |
| [VideoSpace.Timeline](https://www.nuget.org/packages/VideoSpace.Timeline) | [![NuGet](https://img.shields.io/nuget/vpre/VideoSpace.Timeline.svg)](https://www.nuget.org/packages/VideoSpace.Timeline) | [![Downloads](https://img.shields.io/nuget/dt/VideoSpace.Timeline.svg)](https://www.nuget.org/packages/VideoSpace.Timeline) | Indexed timeline geometry, hit testing, frame snapping and anchored zoom |
| [VideoSpace.Effects](https://www.nuget.org/packages/VideoSpace.Effects) | [![NuGet](https://img.shields.io/nuget/vpre/VideoSpace.Effects.svg)](https://www.nuget.org/packages/VideoSpace.Effects) | [![Downloads](https://img.shields.io/nuget/dt/VideoSpace.Effects.svg)](https://www.nuget.org/packages/VideoSpace.Effects) | Keyframed evaluation and backend-independent frame/audio composition plans |
| [VideoSpace.Audio](https://www.nuget.org/packages/VideoSpace.Audio) | [![NuGet](https://img.shields.io/nuget/vpre/VideoSpace.Audio.svg)](https://www.nuget.org/packages/VideoSpace.Audio) | [![Downloads](https://img.shields.io/nuget/dt/VideoSpace.Audio.svg)](https://www.nuget.org/packages/VideoSpace.Audio) | Sample-clock PCM mixing, waveform reduction, metering and WAV export |
| [VideoSpace.Documents](https://www.nuget.org/packages/VideoSpace.Documents) | [![NuGet](https://img.shields.io/nuget/vpre/VideoSpace.Documents.svg)](https://www.nuget.org/packages/VideoSpace.Documents) | [![Downloads](https://img.shields.io/nuget/dt/VideoSpace.Documents.svg)](https://www.nuget.org/packages/VideoSpace.Documents) | Validated `.videospace` manifests, SRT captions and cuts-only CMX3600 EDL |
| [VideoSpace.Rendering](https://www.nuget.org/packages/VideoSpace.Rendering) | [![NuGet](https://img.shields.io/nuget/vpre/VideoSpace.Rendering.svg)](https://www.nuget.org/packages/VideoSpace.Rendering) | [![Downloads](https://img.shields.io/nuget/dt/VideoSpace.Rendering.svg)](https://www.nuget.org/packages/VideoSpace.Rendering) | Skia frame composition, transitions, procedural footage and PNG export |
| [VideoSpace.Media](https://www.nuget.org/packages/VideoSpace.Media) | [![NuGet](https://img.shields.io/nuget/vpre/VideoSpace.Media.svg)](https://www.nuget.org/packages/VideoSpace.Media) | [![Downloads](https://img.shields.io/nuget/dt/VideoSpace.Media.svg)](https://www.nuget.org/packages/VideoSpace.Media) | Uno platform boundary: local media, browser GPU presentation, audio and storage |
| [VideoSpace.Controls](https://www.nuget.org/packages/VideoSpace.Controls) | [![NuGet](https://img.shields.io/nuget/vpre/VideoSpace.Controls.svg)](https://www.nuget.org/packages/VideoSpace.Controls) | [![Downloads](https://img.shields.io/nuget/dt/VideoSpace.Controls.svg)](https://www.nuget.org/packages/VideoSpace.Controls) | Custom Uno timeline, monitors, bins, panels, icons and editing fields |
| [VideoSpace.Workbench](https://www.nuget.org/packages/VideoSpace.Workbench) | [![NuGet](https://img.shields.io/nuget/vpre/VideoSpace.Workbench.svg)](https://www.nuget.org/packages/VideoSpace.Workbench) | [![Downloads](https://img.shields.io/nuget/dt/VideoSpace.Workbench.svg)](https://www.nuget.org/packages/VideoSpace.Workbench) | Embeddable Uno editing workspace, effects inspector and project workflows |

Dependencies (from project references):

```text
Core ← Editing, Timeline, Documents, Effects
Effects ← Audio, Rendering (+ SkiaSharp), Media (Uno)
Editing + Timeline + Rendering + Media ← Controls
Controls + Documents + Audio ← Workbench
```

The browser media pipeline also contains standalone JavaScript modules (packet indexing, decoding, GPU compositing) that are not NuGet packages; [docs/libraries.md](docs/libraries.md) explains their ownership and revision boundaries.

### VideoSpace.Core

The document model every other package builds on: `VideoProject`, assets, tracks, clips, transitions, keyframes and captions, with exact rational frame rates and SMPTE timecode. Use it alone to generate, inspect or validate projects. No dependencies beyond .NET; no UI.

```sh
dotnet add package VideoSpace.Core --prerelease
```

**Key types**

- `VideoProject` / `Track` / `TimelineClip` / `MediaAsset` — the serializable sequence model (timeline coordinates are integer frames).
- `FrameRate` — exact `Numerator/Denominator` rate with `Seconds`/`Frames` conversion.
- `Timecode` — `Format`/`Parse`, including 29.97/59.94 drop-frame.
- `ProjectIndex` / `TrackIndex` — prepared per-revision indexes for `At`, `TransitionAt` and `Visible` queries.
- `ProjectValidation.Validate`, `ProjectSnapshot.Write/Read/Clone` — limits, invariants and JSON manifests.

**Usage**

```csharp
using VideoSpace.Core;

var project = SampleProject.Create();                 // validated NORTH sample, 24 fps
FrameRate rate = project.FrameRate;
string tc = Timecode.Format(240, rate);               // "00:00:10:00"
long frame = Timecode.Parse("00:00:10:00", rate);     // 240

var index = new ProjectIndex(project);                // read index for this revision
TimelineClip? clip = index.Tracks[0].At(300);         // O(log n) active clip on V1
Console.WriteLine($"{tc}: {clip?.Name}, sequence ends at frame {index.Duration}");

project.Markers.Add(new Marker(frame, "Coast"));       // mutate, then re-validate
ProjectValidation.Validate(project);                  // throws InvalidDataException
string json = ProjectSnapshot.Write(project);
```

### VideoSpace.Editing

Atomic editing on top of Core: every `EditorSession.Execute` call validates the whole document and either commits one undo step or restores the previous state. Includes insert/overwrite, razor, ripple, roll, slip, rate stretch, transitions, nesting and multicamera cuts. Depends on `VideoSpace.Core`; no UI.

```sh
dotnet add package VideoSpace.Editing --prerelease
```

**Key types**

- `EditorSession` — `Execute`, `Undo`/`Redo`, `Selection`, `Playhead`, `Index`, `OpenSequence`/`NavigateUp`, `Changed` event.
- `TimelineEdits` — `Insert`, `Split`, `Move`, `Trim`, `Delete`, `Slip`, `Roll`, `RateStretch`, `Link`.
- `TransitionEdits` — `Add`/`Remove` two-sided transitions with `TransitionAlignment`.
- `SequenceEdits` — `Nest`/`Unnest`, `CreateMulticam`, `SwitchCamera`.
- `TimelineClipboard` — value-owned `Copy`/`Paste` preserving gaps, links and transitions.

**Usage**

```csharp
using VideoSpace.Core;
using VideoSpace.Editing;

var session = new EditorSession(SampleProject.Create());
session.Execute("Dissolve", p => TransitionEdits.Add(p, "ridge-cut", TransitionKind.CrossDissolve, 48));
session.Execute("Razor", p => TimelineEdits.Split(p, ["dunes-cut"], 540));

string[] nested = [];
session.Execute("Nest", p => nested = SequenceEdits.Nest(
    p, p.Tracks.SelectMany(t => t.Clips).Select(c => c.Id), "Main edit"));
session.OpenSequence(session.Project.Clip(nested[0]).AssetId); // edits now target the child
session.NavigateUp();                                          // RootProject stays authoritative

session.Undo();
Console.WriteLine(string.Join(", ", session.History));         // Dissolve, Razor
```

### VideoSpace.Timeline

Pixel/frame mapping for building your own timeline UI: indexed hit testing of heads, tails and bodies, snapping to edit points and markers, and zoom anchored under the pointer. Depends on `VideoSpace.Core`; renderer-agnostic, no UI.

```sh
dotnet add package VideoSpace.Timeline --prerelease
```

**Key types**

- `TimelineGeometry` — `X`/`Frame`/`Y` mapping, `PixelsPerFrame`, `ScrollFrame`, `HeaderWidth`, `TrackHeight`.
- `TimelineGeometry.Prepared` — attach a `ProjectIndex` for logarithmic hit tests and snapping.
- `TimelineGeometry.Hit`, `Snap`, `ZoomAt`, `DisplayTracks`.
- `TimelineHit` / `HitPart` — `Ruler`, `Track`, `Body`, `Head`, `Tail` results with frame, track and clip IDs.

**Usage**

```csharp
using VideoSpace.Core;
using VideoSpace.Timeline;

var project = SampleProject.Create();
var geometry = new TimelineGeometry { PixelsPerFrame = 2, HeaderWidth = 148, TrackHeight = 52 };
geometry.Prepared = new ProjectIndex(project);

TimelineHit hit = geometry.Hit(project, x: 700, y: 90);
if (hit.Part is HitPart.Head or HitPart.Tail)
    Console.WriteLine($"Trim handle of {hit.ClipId} on {hit.TrackId}");

long snapped = geometry.Snap(project, geometry.Frame(705), exclude: [], playhead: 96);
geometry.ZoomAt(1.25, anchorX: 705);   // keeps the frame under the pointer fixed
double x = geometry.X(snapped);
```

### VideoSpace.Effects

Evaluates a project at a frame into a `FramePlan`: resolved layer transforms, color, crop, opacity envelopes, transitions, nested-sequence subplans and audio voices. Plans are plain records, so any backend (Skia, WebGPU, WebGL2) can draw them. Depends on `VideoSpace.Core`; no UI.

```sh
dotnet add package VideoSpace.Effects --prerelease
```

**Key types**

- `PreparedFramePlanner` — per-revision planner with a memoized last frame; `Evaluate(frame)`.
- `FramePlanner.Evaluate` — one-shot convenience wrapper.
- `FramePlan`, `LayerPlan`, `TransitionPlan`, `AudioPlan` — the backend-independent plan records.
- `EffectPresets` — `Names` and `Apply` for built-in looks and animations.
- `FramePlanBudget` — bounds expanded graphs before serialization or GPU allocation.

**Usage**

```csharp
using VideoSpace.Core;
using VideoSpace.Effects;

var project = SampleProject.Create();
var clip = project.Clip("coast-cut");
EffectPresets.Apply(clip, "Golden hour", project.FrameRate);
clip.Effects.Opacity.SetKey(0, 0);
clip.Effects.Opacity.SetKey(24, 1, Interpolation.Smooth);

var planner = new PreparedFramePlanner(project);   // reuse until the project changes
FramePlan plan = planner.Evaluate(250);
foreach (LayerPlan layer in plan.Layers)
    Console.WriteLine($"{layer.ClipId}: {layer.Kind} at {layer.SourceTime:0.00}s, opacity {layer.Opacity:0.00}");
foreach (AudioPlan voice in plan.Audio)
    Console.WriteLine($"{voice.ClipId}: gain {voice.Gain:0.00}");
```

### VideoSpace.Audio

A deterministic stereo mixer that compiles gain, pan, fades, transitions, nesting and camera audio into an interval-indexed graph, then mixes arbitrary sample blocks. The host supplies decoded PCM, so no codec is pulled in. Depends on `VideoSpace.Effects`; no UI.

```sh
dotnet add package VideoSpace.Audio --prerelease
```

**Key types**

- `PreparedAudioMixer` — compile once per revision; `Mix(sources, firstSample, sampleRate, stereo)`.
- `PcmAudio` — caller-owned interleaved float samples with interpolated `At`.
- `Waveform` — `Reduce` to peaks, `Rms`, `Decibels`.
- `WavWriter.Write` — 16-bit PCM WAV bytes.

**Usage**

```csharp
using VideoSpace.Audio;
using VideoSpace.Core;

var project = SampleProject.Create();
var mixer = new PreparedAudioMixer(project);
const int rate = 48000;
var stereo = new float[rate * 2 * 5];              // five seconds, interleaved L/R

// The sample score is a generated tone. For imported media, pass decoded PcmAudio keyed by asset ID.
mixer.Mix(new Dictionary<string, PcmAudio>(), firstSample: 0, sampleRate: rate, stereo);

var pcm = new PcmAudio(rate, 2, stereo);
float[] peaks = Waveform.Reduce(pcm, buckets: 512);
double level = Waveform.Decibels(Waveform.Rms(stereo));
File.WriteAllBytes("mix.wav", WavWriter.Write(pcm));
```

### VideoSpace.Documents

File interchange: validated `.videospace` manifests, SubRip captions and a guarded cuts-only CMX3600 EDL writer that rejects what it cannot represent (transitions, nests, multicam, speed changes). Depends on `VideoSpace.Core`; no UI.

```sh
dotnet add package VideoSpace.Documents --prerelease
```

**Key types**

- `ProjectFile` — `Save`/`Open` and the `.videospace` `Extension`.
- `SubRip` — `Read`/`Write` SRT captions at a `FrameRate`.
- `EdlWriter.Write` — CMX3600 export of one video track.

**Usage**

```csharp
using VideoSpace.Core;
using VideoSpace.Documents;

var project = SampleProject.Create();
string manifest = ProjectFile.Save(project);             // validates before writing
File.WriteAllText("north" + ProjectFile.Extension, manifest);
VideoProject reopened = ProjectFile.Open(manifest);     // validates on read

string srt = SubRip.Write(project.Captions, project.FrameRate);
List<Caption> captions = SubRip.Read(srt, project.FrameRate);
string edl = EdlWriter.Write(project, trackId: "v1");
```

### VideoSpace.Rendering

Draws a `FramePlan` into a host-owned `SKCanvas` (GPU or raster): transforms, color matrix, crop, vignette, dissolves/dips/wipes, nested plans, titles, captions and the procedural sample footage. Use it for native previews, thumbnails or PNG stills. Depends on `VideoSpace.Effects` and SkiaSharp; no UI framework.

```sh
dotnet add package VideoSpace.Rendering --prerelease
```

**Key types**

- `FrameRenderer` — `Draw(canvas, area, plan, safeAreas)`, `Png(plan)`, `SetImage`, `SetTypeface`; `IDisposable`.
- `ProceduralFootage.Draw` — the deterministic NORTH landscape generator.
- `Palette` — hex color parsing and paint helpers.

**Usage**

```csharp
using SkiaSharp;
using VideoSpace.Core;
using VideoSpace.Effects;
using VideoSpace.Rendering;

FramePlan plan = FramePlanner.Evaluate(SampleProject.Create(), frame: 96);

using var renderer = new FrameRenderer();
// renderer.SetImage(assetId, File.ReadAllBytes("still.png")) supplies image assets.
File.WriteAllBytes("frame-96.png", renderer.Png(plan));

using var surface = SKSurface.Create(new SKImageInfo(1280, 720));
renderer.Draw(surface.Canvas, new SKRect(0, 0, 1280, 720), plan, safeAreas: true);
```

### VideoSpace.Media

The platform boundary used by the controls: local import and project pickers, recovery storage, browser GPU presentation, Web Audio output and WebM export. In the browser it drives the bundled JavaScript modules; on desktop it provides image import, file pickers and recovery in application storage. Depends on `VideoSpace.Effects`; requires Uno Platform.

```sh
dotnet add package VideoSpace.Media --prerelease
```

**Key types**

- `MediaServices` — `InitializeAsync`, `Poll`, `ImportAsync`, `OpenAsync`, `SaveTextAsync`, `ExportVideo`, `SetProject`; `IDisposable`.
- `MediaMessage` — queued host events (`Type`, `Text`, `Asset`, `Bytes`).
- `BrowserState` — browser meter levels and monitor visibility.

**Usage**

```csharp
using VideoSpace.Core;
using VideoSpace.Media;

var project = SampleProject.Create();
var media = new MediaServices();
await media.InitializeAsync();          // restores recovery and starts browser services
await media.ImportAsync();              // opens the platform picker

// Results arrive asynchronously; poll from a DispatcherTimer tick.
foreach (MediaMessage message in media.Poll())
{
    if (message.Type == "asset" && message.Asset is { } asset) project.Assets.Add(asset);
    else if (message.Type == "error") Console.Error.WriteLine(message.Text);
}
media.SetProject(project);              // browser: keep the media pipeline in sync
```

In a browser head, include every `VideoSpace.Media/Web/*.js` file as an embedded `WasmScripts` resource, as `src/VideoSpace.App/VideoSpace.App.csproj` does.

### VideoSpace.Controls

The custom editing controls — timeline, Source/Program monitors, project bin, dockable panels, split handles, timecode/value fields, meters and vector icons — usable individually in any Uno app. Depends on `Editing`, `Timeline`, `Rendering` and `Media`; requires Uno Platform (Skia renderer).

```sh
dotnet add package VideoSpace.Controls --prerelease
```

**Key types**

- `StudioTheme` — resource dictionary to merge after `XamlControlsResources`.
- `TimelineView` — multitrack timeline bound to an `EditorSession`; `Geometry`, `Fit`, `Zoom`, `AssetDropped`.
- `MonitorView` — frame monitor; `Update(plan, total, rate, playing, playbackRate)`, `Seek`, `Command`.
- `ProjectBinView` — asset bin; `Update(project)`, `AssetSelected`, `ImportRequested`.
- `PanelHost`, `SplitHandle`, `TimecodeField`, `AudioMeterView`, `Studio` layout/brush helpers.

**Usage**

```xml
<ResourceDictionary.MergedDictionaries>
  <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" />
  <studio:StudioTheme xmlns:studio="using:VideoSpace.Controls" />
</ResourceDictionary.MergedDictionaries>
```

```csharp
using VideoSpace.Controls;
using VideoSpace.Core;
using VideoSpace.Editing;
using VideoSpace.Effects;
using VideoSpace.Media;
using VideoSpace.Rendering;

var session = new EditorSession(SampleProject.Create());
var timeline = new TimelineView(session);
var program = new MonitorView("program", new MediaServices(), new FrameRenderer());
var planner = new PreparedFramePlanner(session.Index);

var layout = Studio.Rows(Studio.Star(), Studio.Star());
Studio.At(layout, program);
Studio.At(layout, timeline, row: 1);
window.Content = layout;                 // your Uno Window

program.Update(planner.Evaluate(session.Playhead), session.Index.Duration,
    session.Project.FrameRate, playing: false, playbackRate: 1);
```

### VideoSpace.Workbench

The complete embeddable editor — menus, workspaces, Source/Program monitors, bins, timeline, Effect Controls, captions, history, playback and project/export workflows — as one `UserControl`. Depends on `Controls`, `Documents` and `Audio`; requires Uno Platform (Skia renderer).

```sh
dotnet add package VideoSpace.Workbench --prerelease
```

**Key types**

- `StudioWorkbench(EditorSession, MediaServices)` — the workspace control; `IDisposable`.
- `StudioWorkbench.Session` / `Timeline` — the shared session and timeline control.
- `SetTypeface`, `ShowStatus` — host customization.

**Usage**

```csharp
using VideoSpace.Core;
using VideoSpace.Editing;
using VideoSpace.Media;
using VideoSpace.Workbench;

protected override void OnLaunched(LaunchActivatedEventArgs args)
{
    var window = new Window { Title = "VideoSpace" };
    var workbench = new StudioWorkbench(new EditorSession(SampleProject.Create()), new MediaServices());
    window.Content = workbench;
    window.Closed += (_, _) => workbench.Dispose();
    window.Activate();
}
```

Merge `StudioTheme` into the application resources as shown for Controls, and embed the Media scripts in the browser head.

## Validation and delivery

Build runs engine, editing, parity, native pixel and cross-runtime evaluation tests; publishes the real Uno browser output; and exercises actual pointer/keyboard interactions. Independent media tests inspect decoded export frames/audio and transition pixels. Indexed-source tests encode variable-timestamp color frames and verify forward/backward presentation selection and eviction-safe ownership.

**Release** runs for `v*` tags or a supplied manual version. It runs engine, fixture and frame-plan gates, publishes self-contained single-file desktop executables for Windows, macOS and Linux (x64 and arm64), builds the browser and source archives, packs all ten versioned libraries with symbols and emits `SHA256SUMS`. Tags attach all assets to a GitHub Release and publish the packages to NuGet.org with [Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing) (OIDC, no stored API key) from the protected `nuget` environment. Manual runs are dry runs: they build and upload every asset as workflow artifacts but publish nothing.

Pages deployment requires application and media acceptance. The public verification job checks the exact deployed commit, then repeats application tests on the public URL. Source, logs, screenshots, benchmark JSON and encoded samples are retained as Actions artifacts. See [development](docs/development.md).

## License and independence

Original source, shaders, muxing/indexing code, icons and sample content are MIT licensed. Dependencies retain their own licenses; [third-party notices](THIRD_PARTY_NOTICES.md) include Uno, Skia, .NET and the OFL-licensed font. No Adobe source, artwork, font, footage or proprietary project-format implementation is included.

VideoSpace is independent and not affiliated with, sponsored by or endorsed by Adobe. Adobe Premiere Pro is a trademark of Adobe.
