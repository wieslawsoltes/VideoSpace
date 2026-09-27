# User guide

## Project and source preparation

NORTH is an editable demo with original generated landscapes, titles, synthesized audio, markers and captions. Space plays, the ruler seeks, and Effect Controls edits selected timeline clips.

File → New project creates a blank sequence with a name, dimensions and rational timebase such as 24/1, 25/1, 30000/1001 or 60000/1001. Save your current project first. Creating a new project replaces the editing session but does not delete imported browser media.

Import local video/audio/images through Import or file drop. Supported codecs depend on the browser; a successful metadata read does not certify every frame of a damaged or unusual source. Imported bytes remain local. Large or unsupported audio may lack waveform previews.

Select a Project item to open Source. Mark In/Out, then choose Clip → Insert source / Overwrite source, use comma/period, or drag to a timeline track. Insert shifts following sync-locked material; overwrite replaces the target range. Imported video receives linked audio on a compatible track.

## Timeline editing and clipboard

V selects and moves clips. Drag head/tail edges to trim. Shift-click extends selection. Linked selection follows related clips. Snapping uses clip edges, markers, zero and the playhead; Ctrl temporarily bypasses it.

C cuts at the pointer. Ctrl+K splits selection at the playhead, or unlocked active clips with no selection. B performs guarded ripple trims; N rolls adjacent boundaries; Y slips source windows; R rate-stretches; H pans. Locked tracks, invalid overlaps and exhausted source handles reject atomically. Layered composites belong on separate video tracks.

Ctrl+C copies the selected clips, Ctrl+X copies and lifts them, and Ctrl+V overwrites their occupied ranges at the playhead. Relative starts, gaps, track ordinals, effect values, keyframes and links are preserved. Each pasted clip and link group receives new identifiers. Paste can restore required tracks/asset metadata in another project with the same timebase. Media bytes remain host-owned, and the clipboard lasts only for the editor session. Different timebases are rejected rather than silently rounding edits.

Track patches select targets. L locks; V/M toggles visibility/mute; audio S solos; video sync-lock determines ripple participation. Panel dividers adjust the workspace.

## Effects, titles and captions

Type numeric values or drag property labels. One completed gesture creates one undo entry. A diamond adds/removes a keyframe at the playhead; changing an animated value updates that key. The engine supports linear/hold/smooth interpolation; the initial UI is not a full graph editor. Presets remain editable. Opacity fades reveal lower layers, but dedicated transition objects are not implemented.

Titles are reusable text assets editable through Effect Controls. M creates a marker. Captions supports creation, editing, deletion and seeking. SRT import replaces the caption set; SRT export writes it. Inspect typography visually before delivery: this is not a complete multilingual caption styling system.

## Saving and local recovery

Ctrl+S writes a `.videospace` manifest; Ctrl+O opens a project or SRT. Manifests preserve edits but do not embed source media. Keep original files. IndexedDB recovery depends on browser quota and eviction policy, and clearing site data removes that copy. Import matching offline filenames and byte lengths to relink. This policy is not content hashing. Native recovery currently preserves manifests, not imported image bytes.

## Export

The Export panel's 720p/1080p WebM choices use the offline WebCodecs pipeline. The selected In/Out range determines frame count. Each frame is evaluated and encoded with an explicit timestamp; imported/generated audio is mixed to stereo PCM and encoded with Opus. Work proceeds according to decoder/encoder readiness, not wall-clock playback speed.

Keep the page open until the download completes. Cancel closes the codecs and discards the in-progress output without changing edits. Exports are bounded to ten minutes, 256 MB encoded packets, 128 MB per compressed audio source and 256 MB total decoded audio. Use shorter In/Out sections when a budget is exceeded. A browser without the required encoders receives an explicit error; the application does not pretend a video file was saved.

Output cadence is deterministic, but source decoding uses browser media-element seeks. Exact source-frame selection for all variable-frame-rate codecs is not guaranteed. Inspect delivered output. Native video encoding and frame-indexed professional codec ingest remain unimplemented.

Other choices include native project, PNG current frame, SRT captions, cuts-only first-video-track EDL and managed generated-audio WAV. EDL is not a final layered movie. The WAV path rejects imported audio; offline WebM includes it. The older real-time recorder is an internal experimental API, not the supported UI export.

## Shortcuts

| Keys | Action |
| --- | --- |
| Space; J/K/L | Play/pause; reverse/stop/forward shuttle |
| Left/Right; Shift+Left/Right | One frame; ten frames |
| Home/End | Sequence bounds |
| I/O/M | Sequence In/Out/marker |
| V/C/B/N/Y/R/H | Selection/razor/ripple/roll/slip/stretch/hand |
| S | Snapping |
| Comma/period | Insert/overwrite source |
| Ctrl+C/X/V | Copy/cut/paste clips |
| Ctrl+K | Add edit |
| Delete; Shift+Delete | Lift; guarded ripple delete |
| Ctrl+Z; Ctrl+Shift+Z or Ctrl+Y | Undo; redo |
| Ctrl+N/S/O/I | New project/save/open/import |
| Ctrl+wheel | Anchored timeline zoom |
| Wheel; Shift+wheel | Horizontal; vertical timeline scroll |

Shortcuts are suspended inside text fields. Browser/OS reservations can intercept combinations; menu commands remain available. Hardware WebGPU is preferred, while software adapters automatically use WebGL2.
