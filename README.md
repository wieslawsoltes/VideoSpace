<div align="center">

<img src="src/VideoSpace.App/Assets/Icons/icon.svg" width="88" height="88" alt="VideoSpace" />

# VideoSpace

**Local-first nonlinear video editing. Real Uno controls. GPU composition. Offline export.**

[Open the editor](https://wieslawsoltes.github.io/VideoSpace/) · [User guide](docs/user-guide.md) · [Architecture](docs/architecture.md) · [Reusable libraries](docs/libraries.md)

[![Build and Pages](https://github.com/wieslawsoltes/VideoSpace/actions/workflows/build.yml/badge.svg)](https://github.com/wieslawsoltes/VideoSpace/actions/workflows/build.yml)
[![Desktop](https://github.com/wieslawsoltes/VideoSpace/actions/workflows/desktop.yml/badge.svg)](https://github.com/wieslawsoltes/VideoSpace/actions/workflows/desktop.yml)
[![Engine](https://github.com/wieslawsoltes/VideoSpace/actions/workflows/engine.yml/badge.svg)](https://github.com/wieslawsoltes/VideoSpace/actions/workflows/engine.yml)
[![MIT](https://img.shields.io/badge/license-MIT-8582CD)](LICENSE)

</div>

---

VideoSpace is an independent, Premiere-style editing application built with **Uno Platform and C#**, supported by a reusable timeline engine and local browser media pipeline. The browser application runs the real Uno WebAssembly workbench—not a separate HTML imitation. Source files remain on the user's device.

**Current version: `0.2.0-alpha.1`.** This is an executable editing subset, not complete or pixel-identical Adobe Premiere Pro parity. Native video decode/encode and live native audio remain unimplemented. The [capability ledger](docs/limitations.md) defines the platform boundaries. The Pages site serves only an artifact that passes its pre-deployment checks.

## An editing workspace, not a static mock-up

Source and Program monitors sit above project bins and a multitrack timeline. Custom Uno components provide compact menus, panel tabs, adjustable dividers, transport controls, timecode fields, a tool palette, Effect Controls, history, captions and audio meters. Editing, Color, Effects, Audio and Captions workspaces expose the corresponding tools. Vector icons, branding and the **NORTH** procedural sample are original.

Timeline operations are transactional: selection/linking, snapping, move, razor cuts, head/tail trim, guarded ripple edits, roll, slip, rate stretch, source-range insert/overwrite, track lock/mute/solo/sync lock, markers and undo/redo. Clip copy/cut/paste preserves effects and relative placement, remaps identities and rejects incompatible timebases. File → New project creates a blank sequence with explicit dimensions and a rational frame rate.

The effects engine evaluates motion, scale, rotation, opacity, crop, SDR exposure/contrast/saturation/temperature/vignette, volume, pan and clip fades. Type numeric values or drag property labels to scrub them. Animated values support keyframes; presets remain editable rather than baking pixels.

## Offline video export

The Export panel uses **WebCodecs**, not wall-clock screen recording. It evaluates every sequence frame, waits for available decoded source images, renders the composition, and supplies explicit timestamps to a VP9/VP8 encoder. Imported and generated audio is mixed to sample-addressed stereo PCM before Opus encoding. An original MIT-licensed WebM muxer writes tracks, timestamps, cue points, codec delay and end padding.

Encoder queues and media memory are bounded. Cancellation closes codecs and releases decoder/render resources without changing the project. The initial export limits are ten minutes, 256 MB encoded packets, 128 MB per compressed audio input and 256 MB total decoded audio. A long or high-bitrate sequence may reach a memory limit before ten minutes; use In/Out to export sections.

**Output cadence is explicit; source precision is still browser-dependent.** Imported source selection currently uses browser media-element seeks, not a complete frame-indexed demux/VideoDecoder pipeline. Do not assume exact source-frame selection for every variable-frame-rate codec or unusual container. Unsupported/offline media and unavailable encoders fail visibly. The older real-time recorder remains an internal experimental module and is not the supported UI export path.

## Platform capabilities

| Capability | Browser | Native desktop |
| --- | --- | --- |
| Uno workspace and C# editing engine | Yes | Yes |
| Generated footage, titles and images | Yes | Yes |
| Local video/audio import and playback | Browser-supported codecs | Not yet |
| Compositing | Hardware WebGPU; WebGL2 fallback | Host-owned Skia canvas |
| Live audio routing and metering | Web Audio | Not yet |
| Project, SRT and cuts-only EDL | Yes | Yes |
| PNG frame export | Yes | Yes |
| Generated-audio PCM WAV | Yes | Yes |
| Offline WebM with mixed imported audio | WebCodecs support required | Not yet |
| Recovery | IndexedDB manifests and imported media | Application-storage manifest |

Automatic selection prefers **hardware WebGPU**. Reported software adapters such as SwiftShader use WebGL2 instead of a software WebGPU device. `?gpu=off` explicitly exercises WebGL2; `?gpu=force` is a diagnostic override. Canvas 2D is a reduced preview fallback and is rejected for offline video export because it omits some grading effects.

## Start editing

Open the [editor](https://wieslawsoltes.github.io/VideoSpace/) and press Space to play NORTH. Select a clip and open Effect Controls. Import your own media, select a Project item to open Source, mark its In/Out range, and choose Clip → Insert source / Overwrite source or drag it to the timeline. Imported video receives a linked audio clip.

Use Ctrl+C/X/V for the session-local clip clipboard. Save `.videospace` manifests regularly and retain original media. Browser storage is useful recovery, not a durable backup. The Help menu includes an in-application guide, shortcuts and supported-platform details.

## Build and run

| Component | Pinned version |
| --- | --- |
| .NET SDK | 10.0.401 |
| Uno SDK | 6.7.30 |
| Uno WinUI | 6.7.135, selected by Uno SDK |
| SkiaSharp | 3.119.2, matched managed/native ABI |
| Playwright | 1.63.0 |

```sh
git clone https://github.com/wieslawsoltes/VideoSpace.git
cd VideoSpace
python3 scripts/fetch-assets.py

dotnet run --project tests/VideoSpace.Tests -c Release
dotnet run --project tests/VideoSpace.Editing.Tests -c Release

dotnet workload install wasm-tools --skip-manifest-update
dotnet publish src/VideoSpace.App -f net10.0-browserwasm -c Release \
  -o artifacts/publish -p:WasmShellWebAppBasePath=/VideoSpace/
python3 scripts/collect-site.py artifacts/publish artifacts/site
python3 scripts/serve-site.py --directory artifacts/site --port 4173
```

Open `http://localhost:4173/VideoSpace/`. Production graphics/codecs require an appropriate secure context; localhost is suitable for development.

```sh
dotnet run --project src/VideoSpace.App -f net10.0-desktop \
  -p:VideoSpaceDesktopOnly=true
```

Linux requires a graphical session and Uno's Skia/X11 dependencies. Desktop CI compiles Windows, macOS and Linux; compilation does not certify native media playback or every graphics/windowing configuration.

## Ten reusable libraries

| Library | Responsibility |
| --- | --- |
| `VideoSpace.Core` | Project/media model, rational time, timecode, validation, snapshots |
| `VideoSpace.Editing` | Transactions, nonlinear edits, clipboard, linking, undo/redo |
| `VideoSpace.Timeline` | Geometry, culling, hit testing, snapping, anchored zoom |
| `VideoSpace.Effects` | Keyframes, envelopes, immutable evaluated frame plans |
| `VideoSpace.Audio` | Managed PCM mixing/resampling, waveform reduction, metering and WAV |
| `VideoSpace.Documents` | Native manifests, SubRip captions, cuts-only CMX3600 |
| `VideoSpace.Rendering` | Skia composition, original footage, images, PNG |
| `VideoSpace.Media` | Local storage, browser decoding, composition and offline encoding |
| `VideoSpace.Controls` | Custom Uno panels, timeline, monitors, bins and edit controls |
| `VideoSpace.Workbench` | Embeddable editor and application workflows |

The first six do not depend on Uno. All ten are configured for NuGet packaging; public-feed publication is a separate operation. The JavaScript compositor, PCM mixer, offline exporter and WebM muxer can be reused without the Uno workbench. See [integration examples](docs/libraries.md).

## Validation and deployment

The Build workflow gates Pages on both application acceptance and independent media acceptance. Tests cover integer-time edits, rollback, clipboard identity, C#/JavaScript plan equivalence, actual rendered workspace pixels, pointer/keyboard editing, recovery, local video import and decoded output. Independent FFmpeg/ffprobe checks examine exported frame count, dimensions, duration, picture values and audio energy.

After deployment, **the same UI tests run against the public Pages URL**, and `build-info.json` must identify the expected source commit. Source, candidate site, screenshots, traces, logs and export samples remain available as workflow artifacts. The Release workflow packages the libraries and browser/native archives; it does not automatically publish to NuGet.org.

Headless runners use software adapters. Successful fallback tests do not constitute validation on physical WebGPU hardware; inspect the recorded adapter/backend information. See [development](docs/development.md) for reproducible commands and [limitations](docs/limitations.md) for what the checks do not certify.

## License and independence

Original VideoSpace source is **MIT licensed**. Uno, SkiaSharp, Skia, Inter and test tools retain their upstream licenses; see [third-party notices](THIRD_PARTY_NOTICES.md). No native codec framework with a conflicting runtime license is silently bundled.

VideoSpace is not affiliated with, sponsored by or endorsed by Adobe. Adobe Premiere Pro is a trademark of Adobe. No Adobe code, icons, fonts, footage or proprietary project-format implementation is included.
