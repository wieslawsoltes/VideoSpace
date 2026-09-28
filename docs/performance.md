# Performance, lifetime and resource contracts

## Prepared revisions

`EditorSession.Index` is valid for one committed active-sequence revision. Any transaction, rollback, undo or navigation invalidates it. `PreparedFramePlanner` uses sorted track ranges, binary active-clip/transition search and dictionary media lookup. Reuse the planner until the index changes; do not construct it once per frame or mutate its source model behind its back.

Repeated evaluation of the same frame returns the same plan object. `MediaServices.Present` compares the plan and presentation state before serializing. The browser presents only on changed plans, decoder output or layout/visibility changes. This does not claim a sleeping process: lightweight event-loop and diagnostic polling still exist. The browser acceptance test measures unchanged evaluation, presentation, upload and allocation counters across an idle interval.

## Timeline measurements

`VideoSpace.Parity.Tests` builds 80,000 nonoverlapping clips and executes 600 deterministic active-clip queries. It first verifies that linear and indexed paths select the same clips, then reports index construction/allocation separately from five warmed query repetitions. The JSON artifact contains median times, framework and OS information.

This microbenchmark measures lookup only. It excludes full project editing, JSON snapshots, UI layout, source decoding, GPU composition and encoding. Do not multiply its speedup into a claim about application FPS. History remains bounded snapshot-based, so very large edits still have serialization cost.

## Render graph and caches

Nested sequences and transitions render to pooled intermediate targets in the same GPU context. Transition inputs use premultiplied alpha so a dissolve does not introduce an unintended black hole or destroy lower tracks. Targets, source textures, bind groups and uniform buffers have explicit owners. Unchanged immutable/versioned sources skip uploads; graph resources not visited by the current frame retire.

Default texture/target allocation is limited to 384 MiB per compositor. Expanded frame plans stop at 4,096 nodes/24 levels. Generated preview canvases retain 32 LRU entries, with a separate active-generation limit. These are application-owned allocations, not a promise to bound all browser/driver memory.

## Indexed source decoding

`WebMIndex.Index` parses EBML metadata and block boundaries once. Packet payloads are subarrays into the original source buffer, not copied packet-by-packet. Frame lookup uses presentation intervals; keyframe lookup locates a valid restart point. Nominal FPS is not substituted for variable source timestamps.

`IndexedVideo.Decoder` serializes requests, caps queued decode work and retains bounded decoded frames. Forward requests can consume lookahead/cached output. Backward misses restart from an indexed keyframe. Flush follows the WebCodecs key-chunk requirement. Every output is matched to its indexed timestamp before use.

The returned ImageBitmap is a separate lease. This is deliberate: another source request may evict the decoded frame while a transition still needs its earlier input. The compositor caller releases every lease after queued uploads complete. This introduces an implementation-dependent bitmap conversion cost; no zero-copy end-to-end decode claim is made.

An export owns an LRU of two indexed sources, each bounded to 96 MiB compressed bytes and a 32 MiB retained-frame estimate. Sources exceeding the indexed adapter's support/budget use a recorded browser-seeking fallback. Decoder failures after selection fail the export rather than silently substituting a different frame. Browser-internal codec surfaces and active bitmap leases are separate from the retained-frame estimate.

## Audio and cancellation

Prepared audio voices contain composed time transforms, gain/pan stages, transition weights and camera selectors. An interval tree visits only voices overlapping a sample block. Audio inputs outside the requested In/Out range are not decoded. Remaining imported inputs still use bounded whole-source PCM decoding; streaming audio decode is not implemented.

Output video/audio queues have backpressure. Cancellation aborts reads and decoder waits, closes VideoFrame/AudioData objects, disposes source sessions and GPU resources, and discards incomplete muxed output without modifying edits.
