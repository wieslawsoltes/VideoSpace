namespace VideoSpace.Core;

public enum Interpolation { Linear, Hold, Smooth }
public sealed record Keyframe(long Frame, double Value, Interpolation Interpolation = Interpolation.Linear);

public sealed class AnimatedValue
{
    public double Value { get; set; }
    public List<Keyframe> Keys { get; set; } = [];
    public AnimatedValue() { }
    public AnimatedValue(double value) => Value = value;
    public double At(long frame)
    {
        if (Keys.Count == 0) return Value;
        if (frame <= Keys[0].Frame) return Keys[0].Value;
        for (int i = 1; i < Keys.Count; i++)
        {
            var right = Keys[i];
            if (frame > right.Frame) continue;
            var left = Keys[i - 1];
            if (left.Interpolation == Interpolation.Hold) return frame == right.Frame ? right.Value : left.Value;
            double t = (double)(frame - left.Frame) / (right.Frame - left.Frame);
            if (left.Interpolation == Interpolation.Smooth) t = t * t * (3 - 2 * t);
            return left.Value + (right.Value - left.Value) * t;
        }
        return Keys[^1].Value;
    }
    public void SetKey(long frame, double value, Interpolation interpolation = Interpolation.Linear)
    {
        Keys.RemoveAll(k => k.Frame == frame);
        Keys.Add(new(frame, value, interpolation));
        Keys.Sort((a, b) => a.Frame.CompareTo(b.Frame));
    }
}
