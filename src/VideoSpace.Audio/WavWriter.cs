using System.Text;

namespace VideoSpace.Audio;

public static class WavWriter
{
    public static byte[] Write(PcmAudio audio)
    {
        if (audio.Channels is < 1 or > 8 || audio.SampleRate is < 8000 or > 192000 || audio.Samples.Length % audio.Channels != 0) throw new ArgumentException("Invalid audio format.");
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.ASCII, true);
        int bytes = checked(audio.Samples.Length * 2);
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + bytes); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16); writer.Write((short)1); writer.Write((short)audio.Channels); writer.Write(audio.SampleRate);
        writer.Write(audio.SampleRate * audio.Channels * 2); writer.Write((short)(audio.Channels * 2)); writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(bytes);
        foreach (float sample in audio.Samples) writer.Write((short)Math.Round(Math.Clamp(sample, -1, 1) * 32767));
        writer.Flush(); return stream.ToArray();
    }
}
