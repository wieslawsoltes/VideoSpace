using VideoSpace.Core;

namespace VideoSpace.Effects;

public static class FramePlanner
{
    /// <summary>One-shot evaluation. Interactive hosts should reuse PreparedFramePlanner per revision.</summary>
    public static FramePlan Evaluate(VideoProject project, long frame, bool captions = true) => new PreparedFramePlanner(project).Evaluate(frame, captions);
    public static double Envelope(TimelineClip clip, long local) => Envelope(clip, (double)local);
    public static double Envelope(TimelineClip clip, double local)
    {
        double fade = 1;
        if (clip.Effects.FadeIn > 0) fade *= Math.Clamp(local / clip.Effects.FadeIn, 0, 1);
        if (clip.Effects.FadeOut > 0) fade *= Math.Clamp((clip.Duration - 1 - local) / clip.Effects.FadeOut, 0, 1);
        return fade;
    }
}
