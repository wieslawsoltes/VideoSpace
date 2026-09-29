# Development and validation

Use global.json and upgrade Uno/Skia managed/native ABI together. Font assets have reviewed SHA-256 checks and upstream notices.

```sh
dotnet run --project tests/VideoSpace.Tests -c Release
dotnet run --project tests/VideoSpace.Editing.Tests -c Release
dotnet run --project tests/VideoSpace.Parity.Tests -c Release
dotnet run --project tests/VideoSpace.Rendering.Tests -c Release
dotnet run --project tests/VideoSpace.Fixtures -c Release -- artifacts/fixtures
npm ci
npm run test:plans
```

C# emits independently evaluated recursive frame/sample fixtures. Node compares JavaScript results with numerical tolerances and tests EBML indexes, keyframe/presentation lookup, limits and malformed input. Native Skia tests check real nested/transition pixels. The dense timeline benchmark reports preparation and warmed lookup separately in `artifacts/benchmarks/timeline.json`.

## Browser and media acceptance

Publish/serve the Uno application per README. Install Playwright and generate original media:

```sh
npx playwright install --with-deps chromium
ffmpeg -y -f lavfi -i 'color=c=0xA94637:s=320x180:r=24:d=2' \
  -f lavfi -i 'sine=frequency=440:sample_rate=48000:duration=2' \
  -c:v libvpx-vp9 -b:v 250k -c:a libopus -shortest \
  artifacts/fixtures/local-video.webm
npm run test:browser
```

For isolated media tests, serve the repository on port 4174 with `scripts/serve-site.py --directory . --port 4174` and run `npx playwright test -c playwright.media.config.mjs`. These tests encode variable-timestamp color sources through real WebCodecs, read indexed decoded pixels, retain leases across eviction, inspect transition blending, and independently decode exported movie/audio with ffprobe/FFmpeg. Those binaries are test tools only, not runtime dependencies.

UI tests use read-only bounds to send actual pointer/keyboard events. Reconstructed controls immediately invalidate stale rectangles; no test-only editing endpoint exists. Screenshots, logs, traces and diagnostics are retained. `VIDEOSPACE_URL` targets the public application for the same tests.

## Delivery gates

Build requires core/editing/parity/native-render tests, cross-runtime/packet-index tests, full Uno acceptance and independent media acceptance. Only a successful main build deploys its tested site. `verify-pages.py` checks the exact public commit before repeating UI acceptance. Desktop independently compiles Windows, macOS and Linux. Release creates multi-target packages, single-file desktop executables for six runtimes and browser/source archives; tags publish packages to NuGet.org via Trusted Publishing, while manual runs are dry runs.

Do not equate desktop compilation with unavailable native video support, software-adapter rendering with physical WebGPU validation, or microbenchmarks with end-to-end FPS. Keep mutations transactional, release media leases deterministically, and document the support boundary when adding codecs or interchange formats.
