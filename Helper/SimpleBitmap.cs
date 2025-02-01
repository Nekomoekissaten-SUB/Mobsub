using static Mobsub.Helper.ColorConv;

namespace Mobsub.Helper;

public class SimpleBitmap
{
    private int width;
    private int height;
    private int stride;
    private byte[]? pixelData;

    public SimpleBitmap(int w, int h)
    {
        width = w;
        height = h;
        stride = width * 4;
        pixelData = new byte[stride * height];
    }
    
    public int GetWidth() => width;
    public int GetHeight() => height;
    public int GetStride() => stride;
    public byte[]? GetPixelData() => pixelData;

    public void SetPixel(int x, int y, ARGB8b argb)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
            throw new ArgumentOutOfRangeException();
        SetPixel(x, y, 4, argb);
    }

    public void DrawHorizontalLine(int x0, int y, int x1, ARGB8b argb)
    {
        if (y < 0 || y >= height || x0 >= width || x1 < 0)
            throw new ArgumentOutOfRangeException();

        if (x0 > x1) (x0, x1) = (x1, x0);

        x0 = Math.Max(x0, 0);
        x1 = Math.Min(x1, width - 1);

        var length = (x1 - x0 + 1) * 4;
        SetPixel(x0, y, length, argb);
    }

    private void SetPixel(int x, int y, int length, ARGB8b argb)
    {
        var startIndex = y * stride + x * 4;
        var span = pixelData.AsSpan(startIndex, length);
        for (var i = 0; i < length; i += 4)
        {
            span[i] = argb.Blue;
            span[i + 1] = argb.Green;
            span[i + 2] = argb.Red;
            span[i + 3] = argb.Alpha;
        }
    }

    public void Save(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream);

        // BITMAPFILEHEADER, 14 byte
        // https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-bitmapfileheader
        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(54 + pixelData!.Length);
        writer.Write(0); // Reserved, 2 short
        writer.Write(54); // Offset to pixel data, start from BITMAPFILEHEADER begin

        // BITMAPINFOHEADER, 40 byte (Uncompressed)
        // https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-bitmapinfoheader
        writer.Write(40);
        writer.Write(width);
        writer.Write(height);
        writer.Write((short)1); // biPlanes
        writer.Write((short)32); // biBitCount
        writer.Write(0); // biCompression (BI_RGB)
        writer.Write(0); // biSizeImage
        writer.Write(0); // biXPelsPerMeter, GDI ignored
        writer.Write(0); // biYPelsPerMeter, GDI ignored
        writer.Write(0);
        writer.Write(0);
        // BI_BITFIELDS
        //writer.Write(0x00FF0000); // Red mask
        //writer.Write(0x0000FF00); // Green mask
        //writer.Write(0x000000FF); // Blue mask
        //writer.Write(0xFF000000); // Alpha mask

        for (int y = height - 1; y >= 0; y--)
        {
            writer.Write(pixelData, y * stride, stride);
        }
    }
}
