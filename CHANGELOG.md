# Changelog

## 0.3.0-alpha.1

- Finish and merge the previously pending transition/nesting/multicamera implementation and its UI, native-pixel, sample-clock and performance tests.
- Add explicit cross dissolve, dips, wipes and audio crossfade objects with alignment, handle validation, clipboard remapping and transactional structural edits.
- Add editable nested sequences, root-document history/recovery, parent/child navigation and guarded unnesting.
- Add manually synchronized 2–16 camera groups, held camera cuts and fixed/follow-video audio.
- Add prepared timeline indexes, cached frame plans, interval-based audio graphs and single-context premultiplied GPU graph rendering.
- Skip unchanged presentation plans, texture uploads and idle render work; bound graph expansion, GPU resources and generated-source caches.
- Preserve animation curves through split/trim using keyframe time origins.
- Fix stale read-only control rectangles when menus are reconstructed, allowing actual-input acceptance to target the current instance.
- Add a bounded zero-copy WebM/Matroska VP8/VP9 packet index and offline VideoDecoder source path with exact presentation-timestamp matching, keyframe restarts, cancellation and independent frame leases.
- Decode only audio sources intersecting export In/Out. Record unsupported indexed-source fallbacks instead of claiming arbitrary codec precision.
- Extend independent tests for variable-timestamp decoded colors, backward requests, frame ownership, parser corruption/limits and indexed exported output.
- Retain application/media deployment gates and repeat full application acceptance on the public Pages commit.

Full Adobe project compatibility, native media backends, professional color/audio, automatic multicamera synchronization and other advanced NLE features remain outside this alpha; see the capability ledger.

## 0.2.0-alpha.1

- Repair the Uno WebAssembly splash manifest and preserve reflection-based JSON schema during trimmed publish.
- Prefer hardware WebGPU and use WebGL2 for reported software adapters.
- Add offline WebCodecs output, sample-clock PCM, bounded encoder queues and original WebM muxing with exact Opus trimming.
- Add clip copy/cut/paste and blank-project creation with rational timebase.
- Replace centered menu dialogs with anchored command menus.
- Gate Pages deployment on actual application/media tests and verify the deployed commit.
