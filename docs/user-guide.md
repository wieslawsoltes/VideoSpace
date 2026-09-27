# User guide

NORTH contains original generated landscapes, titles, synthesized audio, markers and captions. Space plays; the timeline ruler seeks. Select a clip and open Effect Controls to change it.

Import local media through Import or file drop. Format support depends on the browser; metadata import does not guarantee every frame of damaged/unusual media decodes. Files remain local. Large or unsupported audio may lack waveform previews.

Select a Project item to open Source. Mark In/Out, then use Clip → Insert source / Overwrite source, comma/period, or drag it to the timeline. Insert shifts sync-locked following material. Overwrite replaces the target range. Imported video receives a linked audio clip.

## Timeline tools

V selects/moves clips. Drag either edge to trim. Shift-click extends selection. Linked selection follows associated clips. Snapping uses edges, markers, zero and the playhead; Ctrl temporarily bypasses it.

C razors at the pointer. Ctrl+K splits selection at the playhead, or unlocked active clips when none is selected. B guarded-ripple trims. N rolls adjacent boundaries. Y slips the source window. R changes duration/speed while retaining source extent. H pans. Invalid overlaps and source handles reject atomically. Composites belong on separate video tracks.

Track patches select targets. L locks; V/M toggles visibility/mute; audio S solos; video sync-lock governs ripple participation. Upper video layers display first. Panel dividers resize the workspace.

## Effects and metadata

Type values or drag property labels. One gesture creates one undo entry. A diamond toggles a keyframe at the playhead; changing an animated value creates/updates a key. The engine supports linear/hold/smooth interpolation, but the initial UI has no complete graph editor. Presets remain editable. Opacity fades reveal lower layers; complete transition objects are absent.

Titles are reusable text assets, editable through Effect Controls. M adds markers. Captions supports creation/editing/deletion/seeking; SRT imports replace the caption set and exports write it. Basic caption rendering is not a complete typography system: inspect text before delivery.

## Storage and export

Ctrl+S saves `.videospace`; Ctrl+O opens project/SRT. Manifests preserve editing data, not media. Keep original files. IndexedDB recovery depends on quota and storage policy; clearing site data removes its copy. Matching an offline filename/length relinks an import. Native recovery currently stores manifests, not imported image bytes.

Export native project for editing interchange, PNG for the current frame, SRT for captions, and EDL for first-track cuts. EDL is not a final layered movie. Managed generated-audio WAV rejects imported sources.

Browser WebM records composition/audio in real time. Keep the tab visible. Initial limits are 10 minutes and 512 MB. Cancellation discards recording data, not edits. Timing drift/frame drops are possible; inspect the result. A deterministic offline encoder is not implemented.

## Shortcuts

| Keys | Action |
| --- | --- |
| Space | Play/pause |
| J/K/L | Reverse/stop/forward shuttle |
| Left/Right; Shift+Left/Right | One frame; ten frames |
| Home/End | Sequence bounds |
| I/O/M | Sequence In/Out/marker |
| V/C/B/N/Y/R/H | Selection/razor/ripple/roll/slip/stretch/hand |
| S | Snapping |
| Comma/period | Insert/overwrite source |
| Ctrl+K | Add edit |
| Delete/Shift+Delete | Lift/guarded ripple delete |
| Ctrl+Z/Ctrl+Shift+Z/Ctrl+Y | Undo/redo/redo |
| Ctrl+S/O/I | Save/open/import |
| Ctrl+wheel | Anchored timeline zoom |
| Wheel/Shift+wheel | Horizontal/vertical timeline scroll |

Shortcuts are suspended in text fields. Browser/OS reservations may intercept combinations; menu commands remain available. Native video decoding/encoding and native live audio are not yet available.
