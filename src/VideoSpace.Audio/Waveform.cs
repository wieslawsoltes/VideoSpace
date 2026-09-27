namespace VideoSpace.Audio;

public static class Waveform
{
    public static float[] Reduce(PcmAudio pcm, int buckets)
    {
        if (buckets < 1 || buckets > 100000) throw new ArgumentOutOfRangeException(nameof(buckets));
        var result = new float[Math.Min(buckets, pcm.FrameCount)];
        for (int b = 0; b < result.Length; b++)
        {
            int start = (int)((long)b * pcm.FrameCount / result.Length), end = (int)((long)(b + 1) * pcm.FrameCount / result.Length);
            float peak = 0;
            for (int i = start * pcm.Channels; i < end * pcm.Channels; i++) peak = Math.Max(peak, Math.Abs(pcm.Samples[i]));
            result[b] = peak;
        }
        return result;
    }
    public static double Decibels(double amplitude) => amplitude <= 0 ? -120 : 20 * Math.Log10(amplitude);
    public static double Rms(ReadOnlySpan<float> samples)
    {
        if (samples.IsEmpty) return 0;
        double sum = 0; foreach (float sample in samples) sum += sample * sample;
        return Math.Sqrt(sum / samples.Length);
    }
}
