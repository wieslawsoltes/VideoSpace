using VideoSpace.Core;
using VideoSpace.Effects;

namespace VideoSpace.Audio;

/// <summary>Block-oriented deterministic stereo mixer. Host supplies decoded PCM; no codec licenses are pulled into the engine.</summary>
public static class AudioMixer
{
    public static void Mix(VideoProject project, IReadOnlyDictionary<string, PcmAudio> sources, long firstSample, int sampleRate, Span<float> stereo)
        => new PreparedAudioMixer(project).Mix(sources, firstSample, sampleRate, stereo);
    public static float Tone(double seconds) => (float)((Math.Sin(seconds * 2 * Math.PI * 110) + .5 * Math.Sin(seconds * 2 * Math.PI * 164.81) + .25 * Math.Sin(seconds * 2 * Math.PI * 220)) * .15 * (.75 + .25 * Math.Sin(seconds * .7)));
}
