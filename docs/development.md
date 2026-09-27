# Development and validation

Use global.json and keep Uno/Skia managed/native ABI versions aligned. Font downloads are checked against reviewed SHA-256 hashes. No stock footage is required.

## Engine and evaluation

```sh
dotnet run --project tests/VideoSpace.Tests -c Release
dotnet run --project tests/VideoSpace.Editing.Tests -c Release
dotnet run --project tests/VideoSpace.Fixtures -c Release -- artifacts/fixtures
npm ci
npm run test:plans
```

The engine suite covers rational timecode, edit boundaries, rollback, audio, documents and a real PNG path. The extended editing suite covers clipboard ownership, effects, identity/link remapping, assets/tracks, locked rollback and timebase rejection. C# fixtures independently check the browser evaluator at edits, fades and keyframes. JavaScript unit checks also cover EBML bounds and PCM behavior.

## Browser and media acceptance

Publish and serve the real Uno application as described in README. Generate an original video/audio fixture and install the test browser:

```sh
npx playwright install --with-deps chromium
ffmpeg -y -f lavfi -i 'color=c=0xA94637:s=320x180:r=24:d=2' \
  -f lavfi -i 'sine=frequency=440:sample_rate=48000:duration=2' \
  -c:v libvpx-vp9 -b:v 250k -c:a libopus -shortest \
  artifacts/fixtures/local-video.webm
npm run test:browser
```

For isolated codecs/rendering, serve the repository root at port 4174 in another terminal:

```sh
python3 scripts/serve-site.py --directory . --port 4174
# In another terminal:
npx playwright test -c playwright.media.config.mjs
```

FFmpeg/ffprobe are independent development tools, not application dependencies. Tests inspect decoded frame count, dimensions, duration, picture values and real audio energy. Test sources are synthetic, not private user media.

Application tests read control bounds/state, then send actual pointer/keyboard input. They do not mutate the model through a test-only command interface. A rendered-pixel test rejects a live-but-blank UI. Tests cover history, playback/still export, direct manipulation, effects, recovery, local video/linked insertion, offline movie export, clipboard and blank-project timebase.

## CI and Pages

Build runs reusable media acceptance in parallel with engine/application build and UI acceptance. Both jobs must succeed before Pages deployment. The public verification job then checks `build-info.json` against the exact source commit and runs the same application acceptance suite at the public URL. `VIDEOSPACE_URL` can target any deployed instance manually.

Artifacts retain exact logs, candidate/site output, source snapshots, traces, screenshots and encoded samples. `.nojekyll` preserves Uno bootstrapper filenames. The collector discovers the actual publish root rather than assuming a fixed output directory layout. The build-info version comes from Directory.Build.props.

The Desktop workflow compiles the shared native host on Windows, Linux and macOS. Release packages reusable libraries and browser/native archives. Public NuGet publishing and signed installers are separate release operations, not implied by a configured workflow.

## Graphics test interpretation

The Linux runner reports software graphics. Its WebGPU device failed a minimal canvas clear in multiple Chrome configurations, before application shaders. Production selection therefore prefers WebGL2 on reported software adapters while retaining hardware WebGPU. Automatic-selection and explicit-WebGL tests are labeled accordingly. Do not claim these certify physical WebGPU hardware or all browser/OS configurations.

Keep changes transactional and bounded, document exact platform/format support, and fail explicitly rather than silently losing unsupported effects or media. Update the capability ledger when a feature is executable and tested, not merely when its button exists.
