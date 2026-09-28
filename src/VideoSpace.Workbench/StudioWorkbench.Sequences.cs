using System.Globalization;

namespace VideoSpace.Workbench;

public sealed partial class StudioWorkbench
{
    private string SelectedClipId() => Session.Selection.FirstOrDefault(id => Session.Index.Clips[id].Track.Kind != TrackKind.Audio)
        ?? Session.Selection.FirstOrDefault() ?? throw new InvalidOperationException("Select a timeline clip first.");

    private void EditTransition()
    {
        var id = SelectedClipId(); var track = Session.Project.TrackFor(id);
        TimelineEdits.RequireUnlocked(track);
        var existing = track.Transitions.FirstOrDefault(t => t.LeftClipId == id) ?? track.Transitions.FirstOrDefault(t => t.RightClipId == id);
        if (existing is not null) id = existing.LeftClipId;
        var clip = Session.Project.Clip(id);
        if (!track.Clips.Any(c => c.Start == clip.End)) throw new InvalidOperationException("Select the outgoing clip of an adjacent edit point.");
        bool audio = track.Kind == TrackKind.Audio;
        var kind = existing?.Kind ?? (audio ? TransitionKind.EqualPowerAudio : TransitionKind.CrossDissolve);
        var alignment = existing?.Alignment ?? TransitionAlignment.Center;
        var duration = Studio.Input((existing?.Duration ?? 24).ToString(CultureInfo.InvariantCulture));
        Studio.Name(duration, "Transition duration"); UiRegistry.Register("Transition duration", duration);
        var kinds = new StackPanel { Spacing = 3 }; var buttons = new List<(TransitionKind Kind, StudioButton Button)>();
        foreach (var value in Enum.GetValues<TransitionKind>().Where(k => (k is TransitionKind.LinearAudio or TransitionKind.EqualPowerAudio) == audio))
        {
            var button = new StudioButton(TransitionLabel(value), () => { kind = value; foreach (var item in buttons) item.Button.SetActive(item.Kind == kind); }, "video", "Transition " + value) { HorizontalContentAlignment = HorizontalAlignment.Left, HorizontalAlignment = HorizontalAlignment.Stretch, Height = 29 };
            buttons.Add((value, button)); button.SetActive(kind == value); kinds.Children.Add(button);
        }
        var alignmentRow = Studio.Row(); var aligns = new List<(TransitionAlignment Kind, StudioButton Button)>();
        foreach (var value in Enum.GetValues<TransitionAlignment>())
        {
            var button = new StudioButton(value switch { TransitionAlignment.Center => "Centered", TransitionAlignment.StartAtCut => "Start at cut", _ => "End at cut" }, () => { alignment = value; foreach (var item in aligns) item.Button.SetActive(item.Kind == alignment); });
            aligns.Add((value, button)); button.SetActive(value == alignment); alignmentRow.Children.Add(button);
        }
        var body = Form(Paragraph($"{clip.Name} → {track.Clips.First(c => c.Start == clip.End).Name}\nEdit point: {Timecode.Format(clip.End, Session.Project.FrameRate)}"), kinds,
            Studio.Text("Duration in sequence frames"), duration, Studio.Text("Alignment"), alignmentRow,
            Paragraph("Transitions read real source handles on both sides. Exhausted handles reject the edit; no frames are silently frozen or duplicated."));
        ShowOverlay(existing is null ? "Add transition" : "Edit transition", body,
            ("Cancel", CloseOverlay), ("Apply transition", () =>
            {
                long frames = long.Parse(duration.Text, CultureInfo.InvariantCulture);
                Session.Execute("Set " + TransitionLabel(kind), p => TransitionEdits.Add(p, id, kind, frames, alignment));
                CloseOverlay(); Timeline.Invalidate();
            }));
    }
    private static string TransitionLabel(TransitionKind kind) => kind switch
    {
        TransitionKind.CrossDissolve => "Cross dissolve", TransitionKind.DipToBlack => "Dip to black",
        TransitionKind.DipToWhite => "Dip to white", TransitionKind.WipeLeft => "Wipe left",
        TransitionKind.WipeRight => "Wipe right", TransitionKind.LinearAudio => "Constant gain", _ => "Constant power"
    };
    private void RemoveTransition()
    {
        string id = SelectedClipId(); var track = Session.Project.TrackFor(id);
        var transitions = track.Transitions.Where(t => t.LeftClipId == id || t.RightClipId == id).Select(t => t.Id).ToArray();
        Session.Execute("Remove transitions", p => { foreach (var transition in transitions) TransitionEdits.Remove(p, transition); });
    }
    private void NestSelection()
    {
        if (Session.Selection.Count == 0) throw new InvalidOperationException("Select a complete temporal range of clips first.");
        var ids = Session.Selection.ToArray(); var name = Studio.Input("Nested sequence");
        Studio.Name(name, "Nested sequence name"); UiRegistry.Register("Nested sequence name", name);
        ShowOverlay("Nest selected clips", Form(Studio.Text("Sequence name"), name,
            Paragraph("The selection becomes an editable compound sequence with linked picture and audio. Every clip intersecting this temporal range must be selected; split the range boundaries first when necessary.")),
            ("Cancel", CloseOverlay), ("Nest clips", () =>
            {
                if (string.IsNullOrWhiteSpace(name.Text)) throw new InvalidOperationException("Enter a sequence name.");
                string[] created = []; Session.Execute("Nest selection", p => created = SequenceEdits.Nest(p, ids, name.Text));
                CloseOverlay(); Session.Select(created.FirstOrDefault()); Timeline.Fit();
            }));
    }
    private void OpenNestedSequence()
    {
        string id = SelectedClipId(); Session.OpenSequence(Session.Project.Clip(id).AssetId); Timeline.Fit();
        ShowStatus("Editing nested sequence · " + Session.Project.SequenceName + " · Sequence → Return to parent to leave");
    }
    private void ReturnToParent()
    {
        Session.NavigateUp(); Timeline.Fit(); ShowStatus("Sequence · " + Session.Project.SequenceName);
    }
    private void UnnestSelection()
    {
        string id = SelectedClipId(); string[] result = [];
        Session.Execute("Unnest selection", p => result = SequenceEdits.Unnest(p, id));
        Session.Select(null); foreach (var value in result) Session.Selection.Add(value); Session.Notify(); Timeline.Fit();
    }
    private void NewMulticam()
    {
        var sources = Session.Project.Assets.Where(a => a.Kind is MediaKind.Video or MediaKind.Image or MediaKind.Generator).ToArray();
        if (sources.Length < 2) throw new InvalidOperationException("Import at least two video/image sources or use the generated sample footage.");
        var name = Studio.Input("Multicam sequence"); Studio.Name(name, "Multicam name"); UiRegistry.Register("Multicam name", name);
        var selected = new HashSet<string>(sources.Take(2).Select(a => a.Id)); var offsets = sources.ToDictionary(a => a.Id, _ => 0d);
        var rows = new StackPanel { Spacing = 4 };
        foreach (var source in sources)
        {
            var row = Studio.Columns(Studio.Star(), new(180)); StudioButton? choose = null;
            choose = new StudioButton(source.Name, () => { if (!selected.Add(source.Id)) selected.Remove(source.Id); choose!.SetActive(selected.Contains(source.Id)); }, "video", "Include camera " + source.Name) { HorizontalContentAlignment = HorizontalAlignment.Left, HorizontalAlignment = HorizontalAlignment.Stretch };
            choose.SetActive(selected.Contains(source.Id)); Studio.At(row, choose);
            var offset = new ValueField("Offset (s)", 0, 0, Math.Max(0, source.DurationSeconds - .05), .01);
            offset.Committed += value => offsets[source.Id] = value; Studio.At(row, offset, column: 1); rows.Children.Add(row);
        }
        bool follows = false; StudioButton? policy = null;
        policy = new StudioButton("Audio follows video", () => { follows = !follows; policy!.SetActive(follows); });
        ShowOverlay("Create multicamera source", Form(Studio.Text("Name"), name, Paragraph("Select 2–16 cameras. Offsets map group time zero to each source's time; the shared usable range is calculated from the shortest remaining source."), rows, policy,
            Paragraph("With audio-follow disabled, the first selected angle supplies audio. This is manual synchronization; waveform/timecode synchronization and a simultaneous angle-preview grid are not implemented.")),
            ("Cancel", CloseOverlay), ("Create camera group", () =>
            {
                var angles = sources.Where(a => selected.Contains(a.Id)).Select(a => new CameraAngle(a.Id, a.Name, offsets[a.Id])).ToArray();
                string id = ""; Session.Execute("Create multicamera source", p => id = SequenceEdits.CreateMulticam(p, name.Text, angles, follows));
                CloseOverlay(); OpenSource(id); _projectPanel.Select("Project"); ShowStatus("Camera group created · insert or overwrite it from Source");
            }));
    }
    private void SwitchCamera(int angle) => Edit("Cut to camera " + (angle + 1), p => SequenceEdits.SwitchCamera(p, Session.Selection, angle, Session.Playhead));
    private void AddSequenceInspector(TimelineClip clip, MediaAsset asset)
    {
        if (asset.Kind == MediaKind.Sequence)
        {
            Section(_inspector, "Nested sequence");
            _inspector.Children.Add(Studio.Row(new StudioButton("Open sequence", () => Run(OpenNestedSequence), "folder"), new StudioButton("Unnest", () => Run(UnnestSelection))));
        }
        if (asset.Kind == MediaKind.Multicam)
        {
            Section(_inspector, "Multicam · cut at playhead");
            int current = (int)clip.CameraAngle.At(Math.Clamp(Session.Playhead - clip.Start, 0, clip.Duration - 1));
            for (int i = 0; i < asset.Angles.Count; i++)
            {
                int angle = i; var button = new StudioButton($"{i + 1}  {asset.Angles[i].Name}", () => SwitchCamera(angle), "video", "Camera " + (i + 1)) { HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new(8, 0, 8, 0) };
                button.SetActive(i == current); _inspector.Children.Add(button);
            }
            var note = Paragraph("Keys 1–9 cut the selected multicamera clip at the playhead. Camera cuts are hold keyframes and participate in undo/redo."); note.Margin = new(12, 5, 12, 7); _inspector.Children.Add(note);
        }
        var track = Session.Project.TrackFor(clip.Id);
        if (track.Transitions.Any(t => t.LeftClipId == clip.Id || t.RightClipId == clip.Id))
        {
            Section(_inspector, "Transition");
            _inspector.Children.Add(Studio.Row(new StudioButton("Edit transition", () => Run(EditTransition)), new StudioButton("Remove transition", () => Run(RemoveTransition))));
        }
    }
}
