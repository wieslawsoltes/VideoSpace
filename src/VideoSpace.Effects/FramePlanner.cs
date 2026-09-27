using VideoSpace.Core;

namespace VideoSpace.Effects;

public static class FramePlanner
{
    public static FramePlan Evaluate(VideoProject project, long frame, bool captions = true)
    {
        var visual = new List<LayerPlan>(); var audio = new List<AudioPlan>();
        bool solo = project.Tracks.Any(t => t.Kind == TrackKind.Audio && t.Solo);
        foreach (var track in project.Tracks)
        {
            if (track.Muted || (track.Kind == TrackKind.Audio && solo && !track.Solo)) continue;
            foreach (var clip in track.Clips.Where(c => c.Enabled && c.Contains(frame)))
            {
                var asset = project.Asset(clip.AssetId); long local = frame - clip.Start;
                var e = clip.Effects; double fade = Envelope(clip, local);
                if (track.Kind == TrackKind.Audio)
                    audio.Add(new(clip.Id, asset.Id, asset.Source, clip.SourceTime(frame, project.FrameRate), clip.Speed, Math.Clamp(e.Gain.At(local) * fade * track.Gain, 0, 16), e.Pan));
                else
                    visual.Add(new(clip.Id, asset.Id, asset.Kind.ToString(), asset.Source, asset.Text, asset.Color,
                        clip.SourceTime(frame, project.FrameRate), clip.Speed, e.X.At(local), e.Y.At(local), Math.Clamp(e.Scale.At(local), .01, 20), e.Rotation.At(local),
                        Math.Clamp(e.Opacity.At(local) * fade, 0, 1), Math.Clamp(e.Exposure.At(local), -5, 5), Math.Clamp(e.Contrast.At(local), 0, 3), Math.Clamp(e.Saturation.At(local), 0, 3), Math.Clamp(e.Temperature.At(local), -1, 1),
                        Math.Clamp(e.Vignette.At(local), 0, 1), e.CropLeft, e.CropRight, e.CropTop, e.CropBottom));
            }
        }
        return new(frame, project.FrameRate.Seconds(frame), project.Width, project.Height, project.FrameRate.Value, visual.ToArray(), audio.ToArray(), captions ? project.Captions.Where(c => frame >= c.Start && frame < c.End).Select(c => c.Text).ToArray() : []);
    }
    public static double Envelope(TimelineClip clip, long local)
    {
        double fade = 1;
        if (clip.Effects.FadeIn > 0) fade *= Math.Clamp((double)local / clip.Effects.FadeIn, 0, 1);
        if (clip.Effects.FadeOut > 0) fade *= Math.Clamp((double)(clip.Duration - 1 - local) / clip.Effects.FadeOut, 0, 1);
        return fade;
    }
}
