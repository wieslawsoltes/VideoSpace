using System.Text;
using VideoSpace.Core;

namespace VideoSpace.Documents;

/// <summary>CMX3600 cuts-only export of one video track. Refuses speed changes rather than silently flattening them.</summary>
public static class EdlWriter
{
    public static string Write(VideoProject project, string? trackId = null)
    {
        var track = trackId is null ? project.Tracks.FirstOrDefault(t => t.Kind == TrackKind.Video) : project.Tracks.FirstOrDefault(t => t.Id == trackId);
        if (track is null) throw new InvalidOperationException("No video track to export.");
        var output = new StringBuilder().Append("TITLE: ").AppendLine(Clean(project.SequenceName)).AppendLine("FCM: NON-DROP FRAME").AppendLine();
        int index = 0;
        foreach (var clip in track.Clips.OrderBy(c => c.Start))
        {
            if (Math.Abs(clip.Speed - 1) > .000001) throw new InvalidOperationException("Cuts-only EDL cannot represent speed changes. Export the native project instead.");
            if (++index > 999) throw new InvalidOperationException("CMX3600 export is limited to 999 edits.");
            long source = project.FrameRate.Frames(clip.SourceIn);
            output.AppendLine($"{index:000}  AX       V     C        {Timecode.Format(source, project.FrameRate)} {Timecode.Format(source + clip.Duration, project.FrameRate)} {Timecode.Format(clip.Start, project.FrameRate)} {Timecode.Format(clip.End, project.FrameRate)}");
            output.Append("* FROM CLIP NAME: ").AppendLine(Clean(clip.Name)).AppendLine();
        }
        return output.ToString();
    }
    private static string Clean(string text) => text.Replace('\r', ' ').Replace('\n', ' ');
}
