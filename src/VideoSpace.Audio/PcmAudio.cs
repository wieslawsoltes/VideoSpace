namespace VideoSpace.Audio;

public sealed record PcmAudio(int SampleRate, int Channels, float[] Samples)
{
    public int FrameCount => Samples.Length / Channels;
    public double Duration => (double)FrameCount / SampleRate;
    public float At(double seconds, int channel)
    {
        double x = seconds * SampleRate; int i = (int)Math.Floor(x);
        if (i < 0 || i >= FrameCount) return 0;
        int c = Math.Clamp(channel, 0, Channels - 1), next = Math.Min(i + 1, FrameCount - 1);
        return (float)(Samples[i * Channels + c] * (1 - (x - i)) + Samples[next * Channels + c] * (x - i));
    }
}
