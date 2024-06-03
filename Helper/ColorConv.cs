namespace Mobsub.Helper;

public class ColorConv
{
    public enum Matrix : byte
    {
        rgb = 0,
        bt709 = 1,
    }

    public enum Transfer : byte
    {
        rec709 = 1,
    }

    public enum Primary : byte
    {
        rec709 = 1,
    }

    public enum ColorRange : byte
    {
        limited = 0,
        full = 1,
    }

    public static ARGB8b YCbCr2RGB(YCbCr8b yuv, Matrix mat, ColorRange inRange, byte alpha = 255)
    {
        double R = 0, G = 0, B = 0;
        int y = 0, u = 0, v = 0;
        if (inRange == ColorRange.limited)
        {
            y = yuv.Y - 16;
            u = yuv.Cb - 128;
            v = yuv.Cr - 128;
        }

        if (mat == Matrix.bt709)
        {
            R = 1.164 * y + 1.793 * u;
            G = 1.164 * y - 0.213 * u - 0.533 * v;
            B = 1.164 * y + 2.112 * u;
        }

        R = Math.Clamp(R, 0, 255);
        G = Math.Clamp(G, 0, 255);
        B = Math.Clamp(B, 0, 255);

        return new ARGB8b(R, G, B, alpha);
    }

    public record struct ARGB8b
    {
        public byte Red, Green, Blue, Alpha;

        public ARGB8b(byte R, byte G, byte B, byte A = 255)
        {
            Red = R; Green = G; Blue = B; Alpha = A;
        }

        public ARGB8b(double R, double G, double B, byte A = 255)
        {
            Red = (byte)Math.Round(R); Green = (byte)Math.Round(G); Blue = (byte)Math.Round(B); Alpha = A;
        }
    }

    public record struct YCbCr8b(byte y, byte Cb, byte Cr)
    {
        public byte Y = y, U = Cb, V = Cr;
    }
}
