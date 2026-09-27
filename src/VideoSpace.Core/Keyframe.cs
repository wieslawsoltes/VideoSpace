namespace VideoSpace.Core;

public enum Interpolation { Linear, Hold, Smooth }
public sealed record Keyframe(long Frame, double Value, Interpolation Interpolation = Interpolation.Linear);

public sealed class AnimatedValue
{
    public double Value { get; set; }
    public long FrameOffset { get; set; }
    public List<Keyframe> Keys { get; set; } = [];
    public AnimatedValue() { }
    public AnimatedValue(double value) => Value = value;
    public double At(long frame) => At((double)frame);
    public double At(double frame)
    {
        frame += FrameOffset;
        if (Keys.Count == 0) return Value;
        if (frame <= Keys[0].Frame) return Keys[0].Value;
        int lo = 0, hi = Keys.Count;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            if (Keys[mid].Frame <= frame) lo = mid + 1; else hi = mid;
        }
        if (lo == Keys.Count) return Keys[^1].Value;
        var left = Keys[lo - 1]; var right = Keys[lo];
        if (left.Interpolation == Interpolation.Hold) return left.Value;
        double t = (frame - left.Frame) / (right.Frame - left.Frame);
        if (left.Interpolation == Interpolation.Smooth) t = t * t * (3 - 2 * t);
        return left.Value + (right.Value - left.Value) * t;
    }
    public bool HasKey(long frame) => Keys.Any(k => k.Frame == frame + FrameOffset);
    public void RemoveKey(long frame) => Keys.RemoveAll(k => k.Frame == frame + FrameOffset);
    public void SetKey(long frame, double value, Interpolation interpolation = Interpolation.Linear)
    {
        if (frame < 0 || !double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(frame));
        frame = checked(frame + FrameOffset);
        if (frame < 0)
        {
            long shift = -frame;
            Keys = Keys.Select(k => k with { Frame = checked(k.Frame + shift) }).ToList();
            FrameOffset += shift; frame = 0;
        }
        Keys.RemoveAll(k => k.Frame == frame);
        Keys.Add(new(frame, value, interpolation));
        Keys.Sort((a, b) => a.Frame.CompareTo(b.Frame));
    }
}
