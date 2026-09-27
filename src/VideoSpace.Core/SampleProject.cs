namespace VideoSpace.Core;

/// <summary>Original procedural footage. No Adobe assets, proprietary fonts or third-party media are bundled.</summary>
public static class SampleProject
{
    public static VideoProject Create()
    {
        var p = new VideoProject { Name = "NORTH — A field film", SequenceName = "NORTH / Main edit" };
        p.Assets = [
            new() { Id = "ridge", Name = "01 — The ridgeline", Kind = MediaKind.Generator, Source = "ridge", DurationSeconds = 30, Color = "#8E9CCC", Bin = "01  Footage" },
            new() { Id = "coast", Name = "02 — Quiet coastline", Kind = MediaKind.Generator, Source = "coast", DurationSeconds = 30, Color = "#8E9CCC", Bin = "01  Footage" },
            new() { Id = "dunes", Name = "03 — Desert light", Kind = MediaKind.Generator, Source = "dunes", DurationSeconds = 30, Color = "#8E9CCC", Bin = "01  Footage" },
            new() { Id = "title", Name = "NORTH / Opening title", Kind = MediaKind.Title, Text = "NORTH", DurationSeconds = 30, Color = "#BE91C6", Bin = "02  Graphics" },
            new() { Id = "credit", Name = "An independent field film", Kind = MediaKind.Title, Text = "A FIELD FILM", DurationSeconds = 30, Color = "#BE91C6", Bin = "02  Graphics" },
            new() { Id = "score", Name = "Ambient score · synthesized", Kind = MediaKind.Audio, Source = "tone", DurationSeconds = 60, Width = 0, Height = 0, HasAudio = true, Color = "#66A58D", Bin = "03  Audio", Peaks = Enumerable.Range(0, 256).Select(i => (float)(.15 + .22 * Math.Abs(Math.Sin(i * .12)) + .1 * Math.Abs(Math.Sin(i * .73)))).ToArray() }
        ];
        var v1 = new Track { Id = "v1", Name = "V1", Kind = TrackKind.Video };
        var v2 = new Track { Id = "v2", Name = "V2", Kind = TrackKind.Video };
        var v3 = new Track { Id = "v3", Name = "V3", Kind = TrackKind.Video };
        var a1 = new Track { Id = "a1", Name = "A1", Kind = TrackKind.Audio };
        var a2 = new Track { Id = "a2", Name = "A2", Kind = TrackKind.Audio };
        v1.Clips = [Clip("ridge-cut", "ridge", "The ridgeline", 0, 240), Clip("coast-cut", "coast", "Quiet coastline", 240, 192), Clip("dunes-cut", "dunes", "Desert light", 432, 240), Clip("ridge-close", "ridge", "Return to the mountains", 672, 192)];
        var title = Clip("title-cut", "title", "NORTH", 36, 156); title.Effects.FadeIn = 18; title.Effects.FadeOut = 18;
        var credit = Clip("credit-cut", "credit", "A FIELD FILM", 48, 144); credit.Effects.Y.Value = .16; credit.Effects.Scale.Value = .24; credit.Effects.FadeIn = 18; credit.Effects.FadeOut = 18;
        v2.Clips.Add(title); v3.Clips.Add(credit);
        var score = Clip("score-cut", "score", "Ambient score", 0, 864); score.Effects.FadeIn = 48; score.Effects.FadeOut = 72; score.Effects.Gain.Value = .35; a1.Clips.Add(score);
        p.Tracks = [v1, v2, v3, a1, a2];
        p.Markers = [new(0, "OPEN"), new(240, "COAST"), new(432, "DESERT"), new(672, "RETURN")];
        p.Captions = [new(72, 160, "A little further. A little quieter."), new(288, 396, "Where the land meets the sea.")];
        ProjectValidation.Validate(p); return p;
    }
    public static TimelineClip Clip(string id, string asset, string name, long start, long duration) => new() { Id = id, AssetId = asset, Name = name, Start = start, Duration = duration };
}
