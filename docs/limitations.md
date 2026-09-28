# Capability and parity ledger

Version 0.3.0-alpha.1 is an implemented nonlinear-editing subset, not complete Adobe Premiere Pro compatibility. A familiar layout and working engine do not imply every professional interchange or codec workflow.

| Area | Implemented boundary |
| --- | --- |
| UI | Original custom Premiere-style Uno workspace, not verified pixel identity |
| Native media | Images/titles/generators plus nested/transition rendering; native video decode/encode and live audio remain absent |
| Timeline | Per-revision indexed lookup and visible ranges; edits still mutate lists and history still uses bounded root snapshots |
| Transitions | Cross dissolve, black/white dips, directional wipes, linear/equal-power audio; strict handles/alignment; not the complete Premiere effect catalog |
| Nesting | Editable bounded child sequences, root history/recovery, composed outer effects and nested audio; guarded unnest; partial temporal selections reject |
| Multicam | 2–16 manually offset sources, camera cuts, fixed/follow-video audio; no waveform/timecode auto-sync or simultaneous angle grid |
| Output timing | Explicit offline WebCodecs frame timestamps and sample-clock stereo PCM, VP8/VP9 + Opus WebM |
| Indexed source decoding | Single-video-track VP8/VP9 WebM/Matroska, monotonic container presentation timestamps, bounded source/frame caches and independent frame leases |
| Source fallback | Other codecs/containers, unsupported EBML features or index-budget excess use diagnosed browser seeking; arbitrary source-frame accuracy is not claimed |
| Unsupported indexed features | Laced video, extra alpha planes, encrypted tracks, non-unit track scales, video codec delay, reordered/duplicate visible timestamps and specialized crop/display metadata |
| Source limits | Default two indexed source sessions; at most 96 MiB compressed bytes each; 32 MiB retained decoded-frame estimate each; browser-internal codec allocations are not measured by these limits |
| Export limits | Ten-minute range, 256 MB encoded packets, 128 MB compressed audio per source, 256 MB decoded audio total; audio outside In/Out is not required |
| Audio | Prepared nested sample-clock gain/pan/mute/solo and crossfades; no professional buses/plug-ins, loudness conform or native live output |
| Color | SDR parameter grading, not HDR/ACES/ICC/OCIO or a colorimetrically identical cross-backend pipeline |
| GPU selection | Hardware WebGPU preferred, reported software adapters use WebGL2; forced software WebGPU is diagnostic |
| GPU loss | Reported explicitly; automatic full device reconstruction remains incomplete |
| Canvas fallback | Reduced preview only; offline export rejects it rather than omitting grading silently |
| Clipboard | Value-owned clip/effect/transition/camera/nested metadata with identifier remapping; matching timebase required; media bytes remain host-owned |
| Interchange | Native JSON, SRT and first-track cuts-only EDL; EDL rejects transitions, nests, camera groups and speed changes; no .prproj/AAF/Final Cut XML |
| WAV | Generated/nested managed audio; imported audio is supported in browser WebM instead |
| Graphics/captions | Basic text titles and timed captions; no full motion-graphics/typography/transcription system |
| Recovery | Browser quota-dependent; saved manifests omit media bytes; native imported-image recovery remains incomplete |
| Docking | Resizable fixed regions/tabs, not arbitrary floating/multi-window docking |
| Other advanced features | Proxies, masks, tracking, stabilization, adjustment layers and collaboration remain unimplemented |

## Validation scope

Engine and C#/JavaScript graph/sample tests are deterministic. Native Skia tests check real transition/nesting pixels. Browser acceptance sends actual Uno pointer/keyboard input; read-only diagnostics supply bounds, never editing commands. Indexed-source tests encode color frames at deliberately variable timestamps and check actual decoded pixels, not just metadata. Independent ffprobe/FFmpeg checks inspect movie frame counts and PCM length.

Headless CI can use a software graphics implementation. Successful WebGL2 or software-adapter tests do not certify physical WebGPU hardware. Desktop builds certify compilation, not missing native media adapters. Paused-render and timeline-query results are not end-to-end playback-rate benchmarks.

The remaining native codecs, professional color/audio, broader indexed demuxing, project interchange and advanced editing workflows are tracked as absent rather than described as completed interfaces.
