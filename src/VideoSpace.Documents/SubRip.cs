using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using VideoSpace.Core;

namespace VideoSpace.Documents;

public static partial class SubRip
{
    [GeneratedRegex(@"^(\d{2,}):(\d{2}):(\d{2})[,.](\d{3})$")]
    private static partial Regex Timestamp();
    public static List<Caption> Read(string input, FrameRate rate)
    {
        if (input.Length > 4 * 1024 * 1024) throw new InvalidDataException("Caption file is too large.");
        var captions = new List<Caption>();
        foreach (string block in Regex.Split(input.Trim().Replace("\r", ""), "\n[ \t]*\n"))
        {
            var lines = block.Split('\n'); int index = Array.FindIndex(lines, l => l.Contains("-->"));
            if (index < 0 || index + 1 >= lines.Length) continue;
            var times = lines[index].Split("-->");
            long start = rate.Frames(Parse(times[0].Trim())), end = rate.Frames(Parse(times[1].Trim().Split(' ')[0]));
            if (end <= start) throw new InvalidDataException("Caption end must follow its start.");
            captions.Add(new(start, end, string.Join("\n", lines.Skip(index + 1))));
        }
        return captions.OrderBy(c => c.Start).ToList();
    }
    public static string Write(IEnumerable<Caption> captions, FrameRate rate)
    {
        var output = new StringBuilder(); int i = 0;
        foreach (var c in captions.OrderBy(c => c.Start)) output.AppendLine((++i).ToString(CultureInfo.InvariantCulture)).Append(Format(rate.Seconds(c.Start))).Append(" --> ").AppendLine(Format(rate.Seconds(c.End))).AppendLine(c.Text).AppendLine();
        return output.ToString();
    }
    private static double Parse(string text)
    {
        var m = Timestamp().Match(text);
        if (!m.Success) throw new FormatException("Invalid SRT time: " + text);
        int h = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), min = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), sec = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture), ms = int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture);
        if (min > 59 || sec > 59) throw new FormatException("Invalid SRT timestamp range.");
        return h * 3600 + min * 60 + sec + ms / 1000d;
    }
    private static string Format(double seconds)
    {
        long ms = (long)Math.Round(seconds * 1000);
        return $"{ms / 3600000:00}:{ms / 60000 % 60:00}:{ms / 1000 % 60:00},{ms % 1000:000}";
    }
}
