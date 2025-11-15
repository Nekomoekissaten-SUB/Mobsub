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

    public static ARGB8b YCbCr2RGB_Int(YCbCr8b yuv, Matrix mat, ColorRange inRange, byte alpha = 255)
    {
        int y = yuv.Y, cb = yuv.U, cr = yuv.V;

        if (inRange == ColorRange.limited)
        {
            y = y - 16;
            cb = cb - 128;
            cr = cr - 128;
        }

        // bt709 fixed-point (scale 1024)
        // R = 1.164*y + 1.793*cr
        // G = 1.164*y - 0.213*cr - 0.533*cb
        // B = 1.164*y + 2.112*cb
        int Yk = (int)(1.164 * 1024);  // 1191
        int Rcr = (int)(1.793 * 1024); // 1836
        int Gcr = (int)(0.213 * 1024); // 218
        int Gcb = (int)(0.533 * 1024); // 546
        int Bcb = (int)(2.112 * 1024); // 2161

        int R = (Yk * y + Rcr * cr) >> 10;
        int G = (Yk * y - Gcr * cr - Gcb * cb) >> 10;
        int B = (Yk * y + Bcb * cb) >> 10;

        R = Math.Clamp(R, 0, 255);
        G = Math.Clamp(G, 0, 255);
        B = Math.Clamp(B, 0, 255);

        return new ARGB8b((byte)R, (byte)G, (byte)B, alpha);
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
