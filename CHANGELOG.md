# Changelog

## 0.2.0-alpha.1

- Repair the Uno WebAssembly splash manifest and preserve reflection-based JSON schema during trimmed publish.
- Select hardware WebGPU automatically and prefer WebGL2 for reported software adapters; preserve explicit diagnostic overrides.
- Add offline WebCodecs video encoding with explicit frame timestamps, bounded encoder queues, imported/generated stereo PCM mixing and an original WebM muxer.
- Replace the supported UI's real-time recorder path with offline export, including cancellation and explicit source/codec/memory failures.
- Add clip copy/cut/paste with relative placement, keyframes, track/asset restoration and fresh clip/link identities.
- Add blank-project creation with dimensions and rational timebase.
- Replace centered menu dialogs with compact anchored command menus.
- Add rendered-pixel, clipboard, blank-project and independently decoded media acceptance checks.
- Gate GitHub Pages on both application and media tests, then verify the public commit and rerun UI tests at the deployed URL.

Native video adapters, professional color management, transitions/nesting and complete Premiere compatibility remain outside this alpha's implemented scope.
