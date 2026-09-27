namespace VideoSpace.Core;

/// <summary>Exact rational frame rate. Timeline coordinates are integer frames, never wall-clock doubles.</summary>
public readonly record struct FrameRate(int Numerator, int Denominator)
{
    public static FrameRate Film => new(24, 1);
    public static FrameRate Ntsc => new(30000, 1001);
    public double Value => (double)Numerator / Denominator;
    public int Nominal => (int)Math.Round(Value);
    public double Seconds(long frames) => frames * (double)Denominator / Numerator;
    public long Frames(double seconds) => checked((long)Math.Round(seconds * Value, MidpointRounding.AwayFromZero));
    public void Validate()
    {
        if (Numerator <= 0 || Denominator <= 0 || Value < 1 || Value > 240)
            throw new ArgumentException("Frame rate must be between 1 and 240 fps.");
    }
    public override string ToString() => Denominator == 1 ? Numerator.ToString() : $"{Numerator}/{Denominator}";
}
