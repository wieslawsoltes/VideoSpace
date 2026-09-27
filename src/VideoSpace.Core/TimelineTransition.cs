namespace VideoSpace.Core;

public enum TransitionKind { CrossDissolve, DipToBlack, DipToWhite, WipeLeft, WipeRight, LinearAudio, EqualPowerAudio }
public enum TransitionAlignment { Center, StartAtCut, EndAtCut }

/// <summary>A two-sided edit-point transition. Clip ranges remain nonoverlapping;
/// source handles, not duplicated timeline clips, supply the overlap.</summary>
public sealed class TimelineTransition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string LeftClipId { get; set; } = "";
    public string RightClipId { get; set; } = "";
    public TransitionKind Kind { get; set; }
    public TransitionAlignment Alignment { get; set; }
    public long Duration { get; set; } = 24;
    public TransitionRange Range(long cut)
    {
        long before = Alignment switch
        {
            TransitionAlignment.StartAtCut => 0,
            TransitionAlignment.EndAtCut => Duration,
            _ => Duration / 2
        };
        return new(checked(cut - before), checked(cut - before + Duration));
    }
}

public readonly record struct TransitionRange(long Start, long End)
{
    public long Duration => End - Start;
    public bool Contains(double frame) => frame >= Start && frame < End;
    public double Progress(double frame) => Math.Clamp((frame - Start) / Math.Max(1, Duration - 1), 0, 1);
}
