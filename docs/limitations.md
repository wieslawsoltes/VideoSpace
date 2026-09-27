# Capability and parity ledger

A familiar layout does not imply complete Adobe Premiere Pro compatibility.

| Area | Initial alpha boundary |
| --- | --- |
| UI | Premiere-style conventions, not verified pixel identity; original branding/assets |
| Native media | Images/titles/generators; no native video decode/encode or live audio |
| Browser formats | Browser decoders; no professional codec suite or arbitrary MXF/RAW guarantee |
| Precision | Integer edits; browser seeks/recording not universally frame-accurate |
| Video export | Real-time WebM, 10-minute/512 MB bounds, no offline render queue |
| Color | SDR approximation, no HDR/ACES/ICC/OCIO |
| GPU loss | Errors surfaced; automatic reconstruction absent |
| Canvas fallback | No temperature/vignette; cross-backend color identity not guaranteed |
| Timeline scale | Lists/snapshot history/culling; no persistent interval index |
| Transitions | Clip opacity/audio fades only |
| Interchange | Native JSON/SRT and first-track cuts-only EDL; no `.prproj`/AAF/Final Cut XML |
| Audio | Gain/pan/mute/solo; no buses, plug-ins or native live output |
| WAV | Generated audio only; imported audio rejected |
| Graphics | Basic text titles, not a complete motion-graphics designer |
| Captions | Basic timed text/SRT; no speech recognition or full styling |
| Recovery | Quota-dependent browser media; manifests omit bytes; native image recovery incomplete |
| Docking | Resizable fixed regions/tabs, not arbitrary floating-window docking |
| Advanced NLE | No multicam/nesting/proxies/masks/tracking/stabilization/adjustment layers/collaboration |

Validation is scoped. Browser acceptance uses actual Uno input and original generated media, then independent output decoding. Headless GPU checks may use software adapters. Desktop compilation does not certify all hardware or media paths. Consult Actions for the exact tested commit.

Future priorities include permissively licensed native media adapters, deterministic offline encoding, explicit transitions/nesting, color management, delta-based indexed editing and stronger portable-media identity. Interfaces/buttons alone do not mark these complete.
