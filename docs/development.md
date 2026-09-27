# Development and validation

Use global.json and upgrade Uno/Skia managed/native ABI together. Font assets have reviewed SHA-256 checks.

```sh
dotnet run --project tests/VideoSpace.Tests -c Release
dotnet run --project tests/VideoSpace.Fixtures -c Release -- artifacts/fixtures
npm ci
npm run test:plans
```

C# produces frame plans at edits, fades and keyframes. Node compares the browser evaluator recursively with numeric tolerance. This checks evaluation, not colorimetry/all decoder behavior.

For browser acceptance, publish/serve per README, then:

```sh
npx playwright install --with-deps chromium
ffmpeg -y -f lavfi -i 'color=c=0xA94637:s=320x180:r=24:d=2' \
  -f lavfi -i 'sine=frequency=440:sample_rate=48000:duration=2' \
  -c:v libvpx-vp9 -b:v 250k -c:a libopus -shortest \
  artifacts/fixtures/local-video.webm
npm run test:browser
```

FFmpeg/ffprobe are independent test tools, not application dependencies. Tests read UI bounds/state, then send actual pointer/keyboard input; no mutation endpoint exists. Artifacts retain screenshots, traces, diagnostics and exact logs. `VIDEOSPACE_URL` can target a deployed build.

Engine runs regressions. Build checks engine/evaluator/UI and gates Pages. Desktop compiles three hosts. Release packages all library targets plus application archives; it does not publish to NuGet.org automatically. Pages collects actual Uno output and preserves bootstrapper names with `.nojekyll`; `build-info.json` identifies the commit.

Keep edits transactional, bound media/history/recording memory, reject unsupported interchange explicitly, and update the parity ledger only when features are executable and tested.
