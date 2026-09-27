using System.Globalization;

namespace VideoSpace.Workbench;

public sealed partial class StudioWorkbench
{
    private readonly TimelineClipboard _clipboard = new();

    private void CopySelection()
    {
        if (Session.Selection.Count == 0) { ShowStatus("Select clips to copy.", true); return; }
        _clipboard.Copy(Session.Project, Session.Selection);
        ShowStatus($"Copied {_clipboard.Count} clips · source media remains on this device");
    }

    private void CutSelection()
    {
        if (Session.Selection.Count == 0) return;
        var ids = Session.Selection.ToArray();
        foreach (var id in ids) TimelineEdits.RequireUnlocked(Session.Project.TrackFor(id));
        _clipboard.Copy(Session.Project, ids);
        Session.Execute("Cut clips", p => TimelineEdits.Delete(p, ids, false));
    }

    private void PasteSelection()
    {
        if (_clipboard.Count == 0) { ShowStatus("The clip clipboard is empty.", true); return; }
        string[] pasted = [];
        Session.Execute("Paste clips", p => pasted = _clipboard.Paste(p, Session.Playhead));
        Session.Selection.Clear();
        foreach (var id in pasted) Session.Selection.Add(id);
        Session.Notify(); Timeline.EnsurePlayheadVisible();
        ShowStatus($"Pasted {pasted.Length} clips at {Timecode.Format(Session.Playhead, Session.Project.FrameRate)}");
    }

    private void NewProject()
    {
        var name = Studio.Input("Untitled project");
        var sequence = Studio.Input("Sequence 01");
        var width = Studio.Input("1920"); var height = Studio.Input("1080");
        var numerator = Studio.Input("24"); var denominator = Studio.Input("1");
        Studio.Name(name, "New project name"); Studio.Name(sequence, "New sequence name");
        UiRegistry.Register("New project name", name); UiRegistry.Register("New sequence name", sequence);
        UiRegistry.Register("New frame numerator", numerator); UiRegistry.Register("New frame denominator", denominator);
        var body = Form(Paragraph("Create an empty sequence. Save your current project first; creating a new project replaces the current editing session, but does not delete imported media."),
            Studio.Text("Project name"), name, Studio.Text("Sequence name"), sequence,
            Studio.Text("Sequence dimensions"), Studio.Row(width, Studio.Text("×"), height),
            Studio.Text("Frame rate · numerator / denominator"), Studio.Row(numerator, Studio.Text("/"), denominator),
            Paragraph("Examples: 24/1, 25/1, 30/1, 30000/1001, 60000/1001. Timeline coordinates remain exact integer frames."));
        ShowOverlay("New project", body, ("Cancel", CloseOverlay), ("Create project", () =>
        {
            if (string.IsNullOrWhiteSpace(name.Text) || string.IsNullOrWhiteSpace(sequence.Text)) throw new InvalidOperationException("Enter project and sequence names.");
            var project = new VideoProject
            {
                Name = name.Text.Trim(), SequenceName = sequence.Text.Trim(),
                Width = int.Parse(width.Text, CultureInfo.InvariantCulture), Height = int.Parse(height.Text, CultureInfo.InvariantCulture),
                FrameRate = new FrameRate(int.Parse(numerator.Text, CultureInfo.InvariantCulture), int.Parse(denominator.Text, CultureInfo.InvariantCulture)),
                Tracks =
                [
                    new Track { Id = "v1", Name = "V1", Kind = TrackKind.Video },
                    new Track { Id = "v2", Name = "V2", Kind = TrackKind.Video },
                    new Track { Id = "a1", Name = "A1", Kind = TrackKind.Audio },
                    new Track { Id = "a2", Name = "A2", Kind = TrackKind.Audio }
                ]
            };
            ProjectValidation.Validate(project);
            CloseOverlay(); Session.Replace(project); _sourceAsset = ""; _sourceIn = 0; _sourceOut = 1;
            _sourcePosition = _playPosition = 0; Timeline.TargetTrackId = "v1"; Timeline.Fit();
            _media.SetProject(project); _sourcePanel.Select("Effect Controls"); _projectPanel.Select("Project");
            _programPanel.SetCaption("Program: NORTH / Main edit", "Program: " + project.SequenceName);
            _timelinePanel.SetCaption("NORTH / Main edit", project.SequenceName);
            ShowStatus("Empty sequence created · import media to begin");
        }));
    }
}
