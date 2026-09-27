# Capability and parity ledger

Version 0.2.0-alpha.1 is a working nonlinear editing subset, not complete Adobe Premiere Pro compatibility.

| Area | Current boundary |
| --- | --- |
| UI | Custom Premiere-style workbench, not verified pixel identity; original branding/assets |
| Native media | Images/titles/generators; no native video decode/encode or live audio |
| Browser formats | Delegated to browser decoders; no arbitrary MXF/RAW/professional codec guarantee |
| Offline output | Explicit per-frame WebCodecs timestamps and sample-addressed stereo mixing; VP9/VP8 + Opus WebM |
| Source-frame precision | Media-element seeks, not full demux/sample-indexed decoding; VFR source selection remains browser-dependent |
| Export limits | Ten-minute range; 256 MB encoded packets; 128 MB compressed audio per input; 256 MB decoded audio total |
| Older recording path | Experimental real-time MediaRecorder module, not the supported Export-panel implementation |
| Color | SDR approximation, not HDR/ACES/ICC/OCIO |
| GPU selection | Hardware WebGPU preferred; reported software adapters use WebGL2; forced software WebGPU is diagnostic only |
| GPU device loss | Surfaced rather than silently exporting blank frames; automatic reconstruction is not complete |
| Canvas fallback | Reduced preview; offline export rejects it to avoid silently dropping grading |
| Clipboard | Value-owned clips/effects/gaps/links; session-local; matching timebase required; media bytes not copied |
| Project creation | Blank dimensions/rational rate supported; retiming a populated sequence is not implicit |
| Timeline scale | Lists, snapshot history and draw-time culling; no persistent interval index |
| Transitions | Clip opacity/audio fades only, no complete transition-object system |
| Interchange | Native JSON, SRT and first-track cuts-only EDL; no .prproj/AAF/Final Cut XML |
| Audio | Gain/pan/mute/solo and offline stereo mix; no buses, plug-ins, loudness conform or native live output |
| WAV | Managed generated audio only; imported audio is included in offline WebM instead |
| Graphics/captions | Basic titles and timed text; no complete motion-graphics, typography or transcription system |
| Recovery | Quota-dependent browser media; manifests omit bytes; native image-byte recovery remains incomplete |
| Docking | Adjustable fixed regions/tabs; no arbitrary floating/multi-window docking |
| Advanced NLE | No multicam, nesting, proxies, masks, tracking, stabilization, adjustment layers or collaboration |

## Validation scope

The engine/evaluator checks are deterministic. Browser acceptance uses actual Uno input and rendered-pixel assertions; media checks decode original synthetic fixtures and encoded output with independent tools. Pages verification checks the deployed commit and reruns UI acceptance on the public URL. Desktop builds test compilation, not a complete native media workflow.

A software WebGPU device on the Linux CI environment failed even a minimal canvas clear across multiple Chrome configurations. Automatic selection therefore chooses the supported WebGL2 path for reported software adapters. Hardware WebGPU remains implemented but is not certified by those software-runner tests. Do not describe a WebGL2 test as a physical WebGPU test.

Future work includes permissively licensed native media adapters, frame-indexed demux/decode, transitions/nesting, color management, indexed/delta editing and portable media packaging. An interface or menu item alone does not mark these features complete.
