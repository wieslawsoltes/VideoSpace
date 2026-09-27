# Third-party notices

Original C#, JavaScript, shaders, icons and procedural footage are MIT licensed. Dependencies retain their licenses.

| Component | Role | License |
| --- | --- | --- |
| Uno Platform/SDK/WinUI | App/XAML/input/host integration | Apache-2.0 |
| .NET | Runtime/libraries | MIT and upstream notices |
| SkiaSharp | Graphics binding | MIT |
| Skia | Native graphics | BSD-style and upstream notices |
| Inter | Font | SIL OFL 1.1 |
| Playwright | Testing | Apache-2.0 |

Inter is fetched from google/fonts/ofl/inter with reviewed hashes and its OFL notice included in builds. No Adobe fonts/artwork/footage/icons are bundled.

WebGPU/WebGL/Web Audio/media decoding/MediaRecorder are browser APIs. Codec implementations and patent obligations are not supplied or relicensed by VideoSpace. Native rendering uses Uno/Skia, not native WebGPU.

FFmpeg/ffprobe generate CI fixtures and independently inspect exports. They are not application dependencies or shipped components; their licenses still apply to test environments. Preserve exact transitive dependency notices when distributing releases.
