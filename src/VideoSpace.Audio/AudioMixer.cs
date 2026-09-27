using VideoSpace.Core;
using VideoSpace.Effects;

namespace VideoSpace.Audio;

/// <summary>Block-oriented deterministic stereo mixer. Host supplies decoded PCM; no codec licenses are pulled into the engine.</summary>
public static class AudioMixer
{
    public static void Mix(VideoProject project, IReadOnlyDictionary<string, PcmAudio> sources, long firstSample, int sampleRate, Span<float> stereo)
    {
        if (sampleRate < 8000 || sampleRate > 192000 || stereo.Length % 2 != 0) throw new ArgumentException("Invalid PCM format.");
        stereo.Clear(); bool solo = project.Tracks.Any(t => t.Kind == TrackKind.Audio && t.Solo);
        foreach (var track in project.Tracks.Where(t => t.Kind == TrackKind.Audio && !t.Muted && (!solo || t.Solo)))
        foreach (var clip in track.Clips.Where(c => c.Enabled))
        {
            var asset = project.Asset(clip.AssetId);
            sources.TryGetValue(asset.Id, out var pcm);
            for (int i = 0; i < stereo.Length / 2; i++)
            {
                double time = (firstSample + i) / (double)sampleRate;
                double relative = time - project.FrameRate.Seconds(clip.Start);
                if (relative < 0 || relative >= project.FrameRate.Seconds(clip.Duration)) continue;
                long local = (long)Math.Floor(relative * project.FrameRate.Value);
                double source = clip.SourceIn + relative * clip.Speed;
                double gain = Math.Clamp(clip.Effects.Gain.At(local) * track.Gain * FramePlanner.Envelope(clip, local), 0, 16);
                double pan = Math.Clamp(clip.Effects.Pan, -1, 1);
                float l = pcm?.At(source, 0) ?? (asset.Source == "tone" ? Tone(source) : 0);
                float r = pcm?.At(source, 1) ?? l;
                stereo[i * 2] += (float)(l * gain * Math.Sqrt((1 - pan) / 2) * Math.Sqrt(2));
                stereo[i * 2 + 1] += (float)(r * gain * Math.Sqrt((1 + pan) / 2) * Math.Sqrt(2));
            }
        }
        for (int i = 0; i < stereo.Length; i++) stereo[i] = Math.Clamp(stereo[i], -1, 1);
    }
    public static float Tone(double seconds) => (float)((Math.Sin(seconds * 2 * Math.PI * 110) + .5 * Math.Sin(seconds * 2 * Math.PI * 164.81) + .25 * Math.Sin(seconds * 2 * Math.PI * 220)) * .15 * (.75 + .25 * Math.Sin(seconds * .7)));
}
