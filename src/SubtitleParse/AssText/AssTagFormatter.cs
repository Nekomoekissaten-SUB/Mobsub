using System.Globalization;

namespace Mobsub.SubtitleParse.AssText;

public static class AssTagFormatter
{
    public static string FormatMove(double sx, double sy, double ex, double ey)
        => "\\move(" +
           sx.ToString("0.###", CultureInfo.InvariantCulture) + "," +
           sy.ToString("0.###", CultureInfo.InvariantCulture) + "," +
           ex.ToString("0.###", CultureInfo.InvariantCulture) + "," +
           ey.ToString("0.###", CultureInfo.InvariantCulture) + ")";

    public static string FormatClipQuad(
        (double X, double Y) p1,
        (double X, double Y) p2,
        (double X, double Y) p3,
        (double X, double Y) p4)
    {
        static int R(double v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);
        int x1 = R(p1.X), y1 = R(p1.Y);
        int x2 = R(p2.X), y2 = R(p2.Y);
        int x3 = R(p3.X), y3 = R(p3.Y);
        int x4 = R(p4.X), y4 = R(p4.Y);
        return "\\clip(m " +
               x1 + " " + y1 + " l " +
               x2 + " " + y2 + " l " +
               x3 + " " + y3 + " l " +
               x4 + " " + y4 + " l " +
               x1 + " " + y1 + ")";
    }
}
