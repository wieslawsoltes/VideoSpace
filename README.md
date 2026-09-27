<div align="center">

<img src="src/VideoSpace.App/Assets/Icons/icon.svg" width="88" height="88" alt="VideoSpace" />

# VideoSpace

**Local-first nonlinear video editing with Uno Platform and GPU composition.**

[Browser editor](https://wieslawsoltes.github.io/VideoSpace/) · [User guide](docs/user-guide.md) · [Architecture](docs/architecture.md) · [Libraries](docs/libraries.md)

[![Build](https://github.com/wieslawsoltes/VideoSpace/actions/workflows/build.yml/badge.svg)](https://github.com/wieslawsoltes/VideoSpace/actions/workflows/build.yml)
[![Desktop](https://github.com/wieslawsoltes/VideoSpace/actions/workflows/desktop.yml/badge.svg)](https://github.com/wieslawsoltes/VideoSpace/actions/workflows/desktop.yml)
[![Engine](https://github.com/wieslawsoltes/VideoSpace/actions/workflows/engine.yml/badge.svg)](https://github.com/wieslawsoltes/VideoSpace/actions/workflows/engine.yml)
[![MIT](https://img.shields.io/badge/license-MIT-8582CD)](LICENSE)

</div>

---

VideoSpace brings a familiar professional editing workspace to a frame-addressed C# engine, custom Uno controls, and a browser media pipeline that keeps source files on the user's device. It is a **real Uno WebAssembly/native application**, not an HTML mock-up around disconnected controls.

**Status: `0.1.0-alpha.1`.** This is an initial working editing subset, not complete or pixel-identical Adobe Premiere Pro parity. Native video decoding, live native audio and native video encoding are not implemented. Read the [capability ledger](docs/limitations.md) before adopting it for delivery work. Pages serves the latest build that passes its deployment gates; consult Actions during initial deployment.

## Editing workspace

The compact dark workspace contains Source and Program monitors, project bins, a multitrack timeline, a tool palette, Effect Controls, color presets, history, captions and audio meters. Panel dividers resize directly. Editing, Color, Effects, Audio and Captions workspaces reveal the corresponding tools. Branding, vector icons and the original **NORTH** procedural sample are independently created.

Editing operations are real model transactions: selection and linked selection, snapping, movement, razor cuts, head/tail trim, guarded ripple editing, rolling edits, slip, rate stretch, source-range insert/overwrite, track lock/mute/solo/sync lock, markers, and bounded undo/redo. Invalid edits roll back instead of leaving overlaps or broken source references.

Effects include position, scale, rotation, opacity, exposure, contrast, saturation, temperature, vignette, crop, volume, pan and clip fades. Numeric fields support typed values and drag-to-scrub. Animated properties support keyframes; presets remain editable instead of baking pixels.

## Platform matrix

| Capability | Browser | Native desktop |
| --- | --- | --- |
| Shared Uno UI and C# editing engine | Yes | Yes |
| Generated footage, titles and images | Yes | Yes |
| Local video/audio import and playback | Browser-supported formats | Not yet |
| Composition | WebGPU → WebGL2 → reduced Canvas 2D | Host-owned Skia canvas |
| Audio routing and live metering | Web Audio | Not yet |
| Project, SRT and cuts-only EDL | Yes | Yes |
| PNG frame export | Yes | Yes |
| Generated-audio PCM WAV export | Yes | Yes |
| Video export | Real-time WebM recording | Not yet |
| Recovery | IndexedDB manifest and local media | Application-storage manifest |

WebM export shares preview composition and routes audio into the recorder. It is **real-time recording, not frame-accurate offline encoding**. Keep the tab visible, inspect the output, and observe the initial 10-minute / 512 MB limits. Codec availability and acceleration depend on the browser. The reduced Canvas 2D fallback omits temperature and vignette grading.

## Start editing

Open the browser editor and press Space to play NORTH. Select a clip and open Effect Controls or Color. Import your own media through Import or by dropping files into the window. Select a Project item to open Source, mark its In/Out range, then choose Clip → Insert source / Overwrite source, use comma/period, or drag it to the timeline. Imported video receives a linked audio clip.

Save `.videospace` files regularly: browser recovery is not a backup. The Help menu contains an in-application guide and explicit platform boundaries.

## Build

| Component | Version |
| --- | --- |
| .NET SDK | 10.0.401 |
| Uno SDK | 6.7.30, stable |
| Uno WinUI | 6.7.135, selected by Uno SDK |
| SkiaSharp | 3.119.2, matched managed/native ABI |
| Playwright | 1.63.0 |

```sh
git clone https://github.com/wieslawsoltes/VideoSpace.git
cd VideoSpace
python3 scripts/fetch-assets.py
dotnet run --project tests/VideoSpace.Tests -c Release

dotnet workload install wasm-tools --skip-manifest-update
dotnet publish src/VideoSpace.App -f net10.0-browserwasm -c Release \
  -o artifacts/publish -p:WasmShellWebAppBasePath=/VideoSpace/
python3 scripts/collect-site.py artifacts/publish artifacts/site
python3 scripts/serve-site.py --directory artifacts/site --port 4173
```

Open `http://localhost:4173/VideoSpace/`. WebGPU requires a secure context; localhost is suitable for development. Append `?gpu=off` to exercise WebGL2.

```sh
# Native editing host
dotnet run --project src/VideoSpace.App -f net10.0-desktop \
  -p:VideoSpaceDesktopOnly=true
```

Linux needs a graphical session and Uno's Skia/X11 native dependencies. Desktop CI compiles Windows, macOS and Linux; compilation does not certify every GPU/windowing configuration.

## Reusable libraries

| Library | Responsibility |
| --- | --- |
| `VideoSpace.Core` | Project/media model, rational time, timecode, validation, snapshots |
| `VideoSpace.Editing` | Atomic nonlinear edits, linked operations, undo/redo |
| `VideoSpace.Timeline` | Geometry, culling, hit testing, snapping, anchored zoom |
| `VideoSpace.Effects` | Keyframes, envelopes, backend-independent frame plans |
| `VideoSpace.Audio` | PCM mixing/resampling, waveform reduction, metering math, WAV |
| `VideoSpace.Documents` | Manifests, SubRip captions, CMX3600 cuts-only export |
| `VideoSpace.Rendering` | Skia composition, generated footage, images, PNG |
| `VideoSpace.Media` | Host storage, browser decoders, audio and GPU integration |
| `VideoSpace.Controls` | Custom Uno panels, timeline, monitors, bins and edit fields |
| `VideoSpace.Workbench` | Embeddable editing workspace and workflows |

The first six libraries are independent of Uno. All ten are configured for NuGet packaging. The browser modules are reusable without the workbench; see [integration examples](docs/libraries.md). Package configuration does not imply publication to a public feed.

## Validation and delivery

Engine regressions cover timecode, timeline semantics, rollback, audio, documents and rendering. C#-generated frame plans are compared field-by-field with the browser export evaluator. Browser acceptance sends actual pointer/keyboard input into the published Uno application, including local video import and independent decoding of a recorded WebM.

Build gates GitHub Pages on browser acceptance. Artifacts retain source, site, screenshots, diagnostics and exact build/test logs. Desktop compiles all three native hosts. Release produces full-target NuGet packages and application archives; it does not automatically publish to NuGet.org. See [development](docs/development.md).

## Explicit boundaries

No `.prproj`/AAF import, multicam, nested sequences, complete transition system, tracking, professional codec suite, HDR/OCIO pipeline, plug-in ecosystem, transcription, collaboration or deterministic offline video encoder is implemented. These gaps are recorded in the [capability ledger](docs/limitations.md), not disguised as completed controls.

## License and independence

VideoSpace source is MIT licensed. Dependencies retain their own licenses; see [third-party notices](THIRD_PARTY_NOTICES.md). Inter is OFL-licensed and checked against reviewed content hashes at build time.

VideoSpace is independent and not affiliated with, sponsored by or endorsed by Adobe. Adobe Premiere Pro is a trademark of Adobe. No Adobe code, icons, fonts, footage or proprietary project-format implementation is included.
