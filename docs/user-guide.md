# User guide

## Start editing

NORTH contains original generated landscapes, titles, synthesized audio, markers and captions. Space plays; the timeline ruler seeks. Select a clip and open Effect Controls to change it. File → New project creates a blank sequence with explicit dimensions and rational timebase.

Import local video/audio/images through Import or file drop. Browser format support varies. Select a Project item to open Source, mark In/Out, then use Clip → Insert source / Overwrite source, comma/period, or drag it onto the timeline. Insert shifts sync-locked following material; overwrite replaces the target range. Imported video receives linked audio.

## Timeline and effects

V selects/moves; edges trim; Shift-click extends selection. C razors, B guarded-ripple trims, N rolls an adjacent boundary, Y slips, R rate-stretches, and H pans. Ctrl temporarily bypasses snapping. Track L locks, V/M controls visibility/mute, audio S solos, and video sync-lock governs ripple participation. Invalid overlaps or exhausted source ranges roll back atomically.

Effect values support typing or label scrubbing. Diamonds add/remove keys at the live playhead. Presets remain editable. Split/trim retains the original animation curve's sampling origin. The complete animation graph editor and professional effect catalog are not implemented.

## Transitions

Select the outgoing clip at an adjacent cut and use Clip → Add / edit transition. Choose dissolve, black/white dip, directional wipe or the compatible audio crossfade; enter a duration in sequence frames and select centered/start/end alignment. The gold timeline strip marks its range.

Transitions use source handles on both clips. An error means the selected overlap cannot be decoded from the available source range; trim to expose handles, shorten the transition or change alignment. No automatic freeze frame hides insufficient handles. Copying both endpoints preserves their transition; deleting/moving an endpoint removes a detached transition.

## Nested sequences

Select every clip intersecting the desired temporal range and choose Sequence → Nest selection. Split boundaries first for a shorter range. Nesting retains editable tracks, effects, transitions, captions, markers and audio. Video/audio nests stay linked.

Select the nested clip and choose Open nested sequence. Child edits remain part of the root document; Save always exports that root. Return to parent sequence leaves the child. Undo can restore child edits from the parent. Unnest requires compatible timebase, default outer effects, untrimmed unit-speed placement and compatible track settings; it rejects cases that would alter the result.

## Multicamera editing

Sequence → Create multicamera source combines 2–16 video/image/generated assets. Select cameras and enter manual source offsets. The common usable range is the shortest remaining source. Audio can stay on the first selected angle or follow camera cuts.

Insert the group from Source, select its timeline clip, and press 1–9 at the playhead or choose a camera in Effect Controls. Cuts are held angle keys and are undoable. Automatic waveform/timecode synchronization and a simultaneous angle-preview grid remain absent.

## Saving and exporting

Native `.videospace` files preserve the editing manifest and nested edits, not media bytes. Keep source files and save regularly; quota-dependent IndexedDB recovery is not a backup. Matching offline filenames and lengths relinks browser sources. Native recovery does not yet restore every imported media byte.

Offline WebM export renders each output frame and sample-clock audio. Supported VP8/VP9 WebM/Matroska sources use packet-indexed decoder output. Unsupported indexed features use a diagnosed browser-seeking fallback; arbitrary codec/VFR accuracy is not guaranteed. Cancellation discards only the incomplete export. The ten-minute and memory limits remain explicit.

PNG captures the current Program frame. SRT writes captions from the active sequence. EDL is first-video-track cuts only and rejects transitions, nests, camera groups and speed changes. Generated/nested WAV rejects undecoded imported PCM; browser WebM includes supported imported audio. Native video encoding and live native audio are not implemented.

## Shortcuts

| Keys | Action |
| --- | --- |
| Space; J/K/L | Play/pause; shuttle/stop |
| Left/Right; Shift+Left/Right | One frame; ten frames |
| Home/End; I/O/M | Sequence bounds; In/Out/marker |
| V/C/B/N/Y/R/H | Selection/razor/ripple/roll/slip/stretch/hand |
| 1–9 | Cut selected multicamera clip to angle |
| S; comma/period | Snapping; insert/overwrite Source |
| Ctrl+K; Delete/Shift+Delete | Split; lift/guarded ripple delete |
| Ctrl+C/X/V | Copy/cut/paste clips and compatible internal transitions |
| Ctrl+Z/Ctrl+Shift+Z/Ctrl+Y | Undo/redo/redo |
| Ctrl+N/S/O/I | New/save/open/import |
| Ctrl+wheel; wheel/Shift+wheel | Anchored zoom; horizontal/vertical timeline scroll |

Shortcuts are suspended in text fields. Browser/OS reservations can intercept some combinations; menus remain available.
