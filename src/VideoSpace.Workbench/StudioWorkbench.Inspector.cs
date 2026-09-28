using VideoSpace.Effects;

namespace VideoSpace.Workbench;

public sealed partial class StudioWorkbench
{
    private void BuildInspector()
    {
        _inspector.Children.Clear();
        var p = Session.Project; var id = Session.Selection.FirstOrDefault();
        if (id is null || !p.Tracks.Any(t => t.Clips.Any(c => c.Id == id)))
        {
            var empty = Studio.Text("Select a clip in the timeline to edit its properties.", 12, Studio.Muted); empty.TextWrapping = TextWrapping.Wrap; empty.Margin = new(20); _inspector.Children.Add(empty); return;
        }
        var clip = p.Clip(id); var asset = p.Asset(clip.AssetId); var track = p.TrackFor(id);
        var heading = Studio.Text(clip.Name, 13); heading.Margin = new(12, 10, 12, 3); _inspector.Children.Add(heading);
        var sourceInfo = Studio.Text($"{track.Name}  ·  {asset.Kind}  ·  {Timecode.Format(clip.Duration, p.FrameRate)}", 10, Studio.Muted); sourceInfo.Margin = new(12, 0, 12, 6); _inspector.Children.Add(sourceInfo);
        var actionRow = Studio.Row(Command("Undo", "Undo", () => Session.Undo(), "undo"), Command("Redo", "Redo", () => Session.Redo(), "redo"), Command("Split", "Split", Split, "razor"), Command("Delete", "Delete", () => Delete(false), "trash")); actionRow.Margin = new(6, 2, 6, 8); _inspector.Children.Add(actionRow);
        AddSequenceInspector(clip, asset);
        if (track.Locked) { var warning = Studio.Text("Track is locked. Unlock it before changing clip properties.", 11, "#DCC087"); warning.TextWrapping = TextWrapping.Wrap; warning.Margin = new(12, 5, 12, 8); _inspector.Children.Add(warning); }
        void Scalar(string label, Func<TimelineClip, double> get, Action<TimelineClip, double> set, double min, double max, double step = 1)
        {
            var field = new ValueField(label, get(clip), min, max, step); field.IsEnabled = !track.Locked;
            field.Committed += value => Edit("Set " + label, project => { TimelineEdits.RequireUnlocked(project.TrackFor(id)); set(project.Clip(id), value); }); _inspector.Children.Add(field);
        }
        void Animated(string label, Func<ClipEffects, AnimatedValue> get, double factor, double min, double max, double step)
        {
            long local = Math.Clamp(Session.Playhead - clip.Start, 0, clip.Duration - 1);
            var value = get(clip.Effects); var row = Studio.Columns(Studio.Star(), new(31));
            var field = new ValueField(label, value.At(local) * factor, min, max, step); field.IsEnabled = !track.Locked;
            field.Committed += next => Edit("Set " + label, project =>
            {
                TimelineEdits.RequireUnlocked(project.TrackFor(id)); var effect = get(project.Clip(id).Effects);
                if (effect.Keys.Count > 0) effect.SetKey(Math.Clamp(Session.Playhead - project.Clip(id).Start, 0, project.Clip(id).Duration - 1), next / factor); else effect.Value = next / factor;
            });
            Studio.At(row, field);
            var key = new StudioButton("", () => Edit("Keyframe " + label, project => { TimelineEdits.RequireUnlocked(project.TrackFor(id)); var effect = get(project.Clip(id).Effects); long cursor = Math.Clamp(Session.Playhead - project.Clip(id).Start, 0, project.Clip(id).Duration - 1); if (effect.HasKey(cursor)) effect.RemoveKey(cursor); else effect.SetKey(cursor, effect.At(cursor)); }), "key", "Keyframe " + label) { IsEnabled = !track.Locked, Margin = new(0, 2, 5, 2) };
            key.SetActive(value.HasKey(local)); Studio.At(row, key, column: 1); _inspector.Children.Add(row);
        }
        if (track.Kind != TrackKind.Audio)
        {
            Section(_inspector, "Motion");
            Animated("Position X", e => e.X, p.Width, -p.Width * 2, p.Width * 2, 1);
            Animated("Position Y", e => e.Y, p.Height, -p.Height * 2, p.Height * 2, 1);
            Animated("Scale", e => e.Scale, 100, 1, 2000, .25);
            Animated("Rotation", e => e.Rotation, 1, -3600, 3600, .25);
            Animated("Opacity", e => e.Opacity, 100, 0, 100, .25);
            Section(_inspector, "Color · Basic correction");
            Animated("Exposure", e => e.Exposure, 1, -5, 5, .01);
            Animated("Contrast", e => e.Contrast, 100, 0, 300, .25);
            Animated("Saturation", e => e.Saturation, 100, 0, 300, .25);
            Animated("Temperature", e => e.Temperature, 100, -100, 100, .25);
            Animated("Vignette", e => e.Vignette, 100, 0, 100, .25);
            Section(_inspector, "Crop");
            Scalar("Crop left", c => c.Effects.CropLeft * 100, (c, v) => c.Effects.CropLeft = v / 100, 0, 99, .1);
            Scalar("Crop right", c => c.Effects.CropRight * 100, (c, v) => c.Effects.CropRight = v / 100, 0, 99, .1);
            Scalar("Crop top", c => c.Effects.CropTop * 100, (c, v) => c.Effects.CropTop = v / 100, 0, 99, .1);
            Scalar("Crop bottom", c => c.Effects.CropBottom * 100, (c, v) => c.Effects.CropBottom = v / 100, 0, 99, .1);
        }
        else
        {
            Section(_inspector, "Audio");
            Animated("Volume", e => e.Gain, 100, 0, 400, .5);
            Scalar("Pan", c => c.Effects.Pan * 100, (c, v) => c.Effects.Pan = v / 100, -100, 100, 1);
            var mute = new StudioButton(track.Muted ? "Unmute track" : "Mute track", () => Edit("Track mute", project => project.TrackFor(id).Muted ^= true), "audio"); mute.Margin = new(8); _inspector.Children.Add(mute);
        }
        Section(_inspector, "Timing and fades");
        Scalar("Fade in (frames)", c => c.Effects.FadeIn, (c, v) => c.Effects.FadeIn = (long)v, 0, clip.Duration, 1);
        Scalar("Fade out (frames)", c => c.Effects.FadeOut, (c, v) => c.Effects.FadeOut = (long)v, 0, clip.Duration, 1);
        Scalar("Speed (%)", c => c.Speed * 100, (c, v) => c.Speed = v / 100, 5, 1600, 1);
        Scalar("Source in (s)", c => c.SourceIn, (c, v) => c.SourceIn = v, 0, asset.DurationSeconds, .02);
        if (asset.Kind == MediaKind.Title)
        {
            Section(_inspector, "Graphic text");
            var text = Studio.Input(asset.Text); text.Margin = new(10, 5, 10, 3); _inspector.Children.Add(text);
            _inspector.Children.Add(new StudioButton("Update title text", () => Edit("Edit title", project => project.Asset(asset.Id).Text = text.Text)) { Margin = new(10, 0, 10, 7) });
        }
        var help = Studio.Text("Diamond: add/remove a keyframe at the playhead. Values with keyframes animate during playback. Drag a property label to scrub.", 10, Studio.Muted); help.TextWrapping = TextWrapping.Wrap; help.Margin = new(12, 12, 12, 16); _inspector.Children.Add(help);
    }
    private static void Section(StackPanel panel, string title)
    {
        var label = Studio.Text(title, 11); label.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        panel.Children.Add(new Border { Background = Studio.Brush("#2C2E33"), BorderBrush = Studio.Brush("#181A1D"), BorderThickness = new(0, 1, 0, 1), Padding = new(11, 8, 9, 8), Margin = new(0, 6, 0, 2), Child = label });
    }
    private UIElement BuildEffects()
    {
        var body = new StackPanel { Spacing = 3, Margin = new(10) };
        var search = Studio.Input(placeholder: "Search effects"); body.Children.Add(search);
        var items = new StackPanel { Spacing = 2 }; body.Children.Add(items);
        void Rebuild()
        {
            items.Children.Clear();
            foreach (var name in EffectPresets.Names.Where(n => n.Contains(search.Text ?? "", StringComparison.OrdinalIgnoreCase))) items.Children.Add(new StudioButton(name, () => ApplyPreset(name), name == "Slow push in" ? "key" : "video") { HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Height = 32 });
            var note = Studio.Text("Select a timeline clip, then choose a preset. All values remain editable in Effect Controls.", 11, Studio.Muted); note.TextWrapping = TextWrapping.Wrap; note.Margin = new(8, 16, 8, 5); items.Children.Add(note);
        }
        search.TextChanged += (_, _) => Rebuild(); Rebuild(); return new ScrollViewer { Content = body };
    }
    private UIElement BuildMetadata()
    {
        var body = new StackPanel { Spacing = 7, Margin = new(16) }; var p = Session.Project;
        foreach (var text in new[] { "PROJECT", p.Name, "Sequence: " + p.SequenceName, $"{p.Width} × {p.Height} · {p.FrameRate} fps", $"Duration: {Timecode.Format(p.Duration, p.FrameRate)}", $"{p.Assets.Count} assets · {p.Tracks.Count} tracks", "Storage: local device", "Project format: VideoSpace JSON / schema 1" }) body.Children.Add(Studio.Text(text, 12));
        body.Children.Add(new StudioButton("Sequence settings…", SequenceSettings)); return new ScrollViewer { Content = body };
    }
    private void RebuildHistory()
    {
        _history.Children.Clear(); _history.Children.Add(Studio.Text("History", 13));
        _history.Children.Add(Studio.Row(new StudioButton("Undo", () => Run(() => Session.Undo()), "undo"), new StudioButton("Redo", () => Run(() => Session.Redo()), "redo")));
        foreach (var name in Session.History.Reverse()) { var label = Studio.Text(name, 11, Studio.Muted); label.Margin = new(8, 5, 4, 5); _history.Children.Add(label); }
        if (Session.History.Count == 0) _history.Children.Add(Studio.Text("No edits in this session.", 11, Studio.Muted));
    }
    private void RebuildCaptions()
    {
        _captions.Children.Clear(); _captions.Children.Add(Studio.Row(new StudioButton("Add caption", AddCaption, "plus"), new StudioButton("Import SRT", () => RunAsync(_media.OpenAsync), "import")));
        var p = Session.Project;
        foreach (var caption in p.Captions.OrderBy(c => c.Start))
        {
            var content = new StackPanel { Spacing = 5, Margin = new(8) };
            var heading = new StudioButton(Timecode.Format(caption.Start, p.FrameRate) + " — " + Timecode.Format(caption.End, p.FrameRate), () => Seek(caption.Start)); content.Children.Add(heading);
            var text = Studio.Text(caption.Text, 12); text.TextWrapping = TextWrapping.Wrap; content.Children.Add(text);
            content.Children.Add(Studio.Row(new StudioButton("Edit", () => EditCaption(caption)), new StudioButton("Remove", () => Edit("Remove caption", project => project.Captions.RemoveAll(c => c == caption)), "trash")));
            _captions.Children.Add(new Border { Background = Studio.Brush("#2C2E33"), Child = content });
        }
    }
}
