using System.Globalization;

namespace VideoSpace.Core;

public static class Timecode
{
    public static string Format(long frame, FrameRate rate, bool dropFrame = false)
    {
        rate.Validate();
        if (frame < 0) throw new ArgumentOutOfRangeException(nameof(frame));
        int nominal = rate.Nominal;
        if (dropFrame)
        {
            int drop = DropCount(rate);
            long tenMinutes = nominal * 600L - drop * 9L;
            long minute = nominal * 60L - drop;
            long remainder = frame % tenMinutes;
            frame += drop * 9L * (frame / tenMinutes) + (remainder >= drop ? drop * ((remainder - drop) / minute) : 0);
        }
        return string.Create(CultureInfo.InvariantCulture, $"{frame / (nominal * 3600):00}:{frame / (nominal * 60) % 60:00}:{frame / nominal % 60:00}{(dropFrame ? ';' : ':')}{frame % nominal:00}");
    }

    public static long Parse(string text, FrameRate rate)
    {
        rate.Validate();
        bool drop = text.Contains(';');
        var parts = text.Replace(';', ':').Split(':');
        if (parts.Length != 4 || parts.Any(p => !int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
            throw new FormatException("Expected HH:MM:SS:FF timecode.");
        var n = parts.Select(p => int.Parse(p, CultureInfo.InvariantCulture)).ToArray();
        if (n[1] > 59 || n[2] > 59 || n[3] >= rate.Nominal) throw new FormatException("Timecode field is outside its range.");
        long result = checked(((n[0] * 3600L) + n[1] * 60L + n[2]) * rate.Nominal + n[3]);
        if (drop)
        {
            int count = DropCount(rate);
            if (n[1] % 10 != 0 && n[2] == 0 && n[3] < count) throw new FormatException("This timecode is skipped in drop-frame numbering.");
            long minutes = n[0] * 60L + n[1];
            result -= count * (minutes - minutes / 10);
        }
        return result;
    }

    private static int DropCount(FrameRate rate) => rate switch
    {
        { Numerator: 30000, Denominator: 1001 } => 2,
        { Numerator: 60000, Denominator: 1001 } => 4,
        _ => throw new ArgumentException("Drop frame requires 30000/1001 or 60000/1001.")
    };
}
