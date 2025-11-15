using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using static Mobsub.Helper.ColorConv;

namespace Mobsub.Helper;

public sealed class SimpleBitmap : IDisposable
{
    private readonly int width;
    private readonly int height;
    private readonly int stride;
    private byte[] pixelData;
    private bool disposed;
    public SimpleBitmap(int w, int h)
    {
        width = w;
        height = h;
        stride = width * 4;
        pixelData = ArrayPool<byte>.Shared.Rent(stride * height);
    }
    
    public int GetWidth() => width;
    public int GetHeight() => height;
    public int GetStride() => stride;
    public byte[]? GetPixelData() => pixelData;
    public Span<byte> GetPixelSpan() => pixelData.AsSpan();
    public Span<uint> GetPixelSpanUInt() => MemoryMarshal.Cast<byte, uint>(GetPixelSpan());

    public void Dispose()
    {
        if (!disposed)
        {
            ArrayPool<byte>.Shared.Return(pixelData);
            disposed = true;
        }
    }

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

        Span<byte> dataSpan = GetPixelSpan();
        for (int y = height - 1; y >= 0; y--)
        {
            //writer.Write(pixelData, y * stride, stride);
            writer.Write(dataSpan.Slice(y * stride, stride));
        }
    }
    public void Save2(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream);

        int imageSize = stride * height;

        // BITMAPFILEHEADER (14 bytes)
        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(54 + imageSize);
        writer.Write(0);
        writer.Write(54);

        // BITMAPINFOHEADER (40 bytes)
        writer.Write(40);
        writer.Write(width);
        writer.Write(height);
        writer.Write((short)1);
        writer.Write((short)32);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);

        Span<uint> span = GetPixelSpanUInt();
        for (int y = height - 1; y >= 0; y--)
        {
            int rowStart = y * width;
            ReadOnlySpan<byte> rowBytes = MemoryMarshal.AsBytes(span.Slice(rowStart, width));
            writer.Write(rowBytes);
        }
    }

    public unsafe void Binarize(byte threshold = 128)
    {
        fixed (byte* p = pixelData)
        {
            var pixel = p;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var r = pixel[0];
                    var g = pixel[1];
                    var b = pixel[2];

                    // byte gray = (byte)(0.2989 * r + 0.587 * g + 0.114 * b);
                    var gray = (r * 299 + g * 587 + b * 114 + 500) / 1000;

                    var binary = gray > threshold ? (byte)255 : (byte)0;

                    pixel[0] = binary;
                    pixel[1] = binary;
                    pixel[2] = binary;
                    pixel[3] = 255;

                    pixel += 4;
                }
            }
        }
    }
    public void Binarize2(uint threshold = 128)
    {
        Span<uint> span = GetPixelSpanUInt();
        int pixelCount = width * height;

        for (int i = 0; i < pixelCount; i++)
        {
            uint pixel = span[i];

            // ARGB 格式：0xAARRGGBB
            byte b = (byte)(pixel & 0xFF);
            byte g = (byte)((pixel >> 8) & 0xFF);
            byte r = (byte)((pixel >> 16) & 0xFF);

            int gray = (r * 77 + g * 150 + b * 29) >> 8;
            byte binary = (byte)(gray > threshold ? 255 : 0);

            span[i] = (0xFFu << 24) | ((uint)binary << 16) | ((uint)binary << 8) | binary;
        }
    }

    public void BinarizeVetor(byte threshold = 128)
    {
        Span<byte> span = pixelData.AsSpan();
        int pixelCount = width * height;
        int vecSize = Vector<byte>.Count; 
        for (int i = 0; i < pixelCount; i++)
        {
            int idx = i * 4;
            byte r = span[idx + 2];
            byte g = span[idx + 1];
            byte b = span[idx + 0];

            int gray = (r * 77 + g * 150 + b * 29) >> 8;
            byte binary = (byte)(gray > threshold ? 255 : 0);

            span[idx + 0] = binary;
            span[idx + 1] = binary;
            span[idx + 2] = binary;
            span[idx + 3] = 255;
        }
    }
    public void BinarizeVector2(byte threshold = 128)
    {
        Span<uint> span = GetPixelSpanUInt();
        int pixelCount = width * height;

        for (int i = 0; i < pixelCount; i++)
        {
            uint pixel = span[i];

            // ARGB: 0xAARRGGBB
            byte b = (byte)(pixel & 0xFF);
            byte g = (byte)((pixel >> 8) & 0xFF);
            byte r = (byte)((pixel >> 16) & 0xFF);

            int gray = (r * 77 + g * 150 + b * 29) >> 8;
            byte binary = (byte)(gray > threshold ? 255 : 0);

            span[i] = (0xFFu << 24) | ((uint)binary << 16) | ((uint)binary << 8) | binary;
        }
    }

    public unsafe SimpleBitmap ResizeNearest(int scale)
    {
        var newImages = new SimpleBitmap(width * scale, height * scale);

        fixed (byte* pSrc = pixelData, pDst = newImages.pixelData)
        {
            for (var origY = 0; origY < height; origY++)
            {
                var srcLine = pSrc + origY * stride;
                var newYStart = origY * scale;
                
                for (var dy = 0; dy < scale; dy++)
                {
                    var dstLine = pDst + (newYStart + dy) * newImages.stride;
                    
                    for (var origX = 0; origX < width; origX++)
                    {
                        var pixel = *(uint*)(srcLine + origX * 4);
                        var newXStart = origX * scale;
                        
                        var dstPixel = (uint*)(dstLine + newXStart * 4);
                        for (var dx = 0; dx < scale; dx++)
                        {
                            dstPixel[dx] = pixel;
                        }
                    }
                }
            }
        }

        return newImages;
    }
    public SimpleBitmap ResizeNearest2(int scale)
    {
        var newImage = new SimpleBitmap(width * scale, height * scale);

        Span<uint> srcSpan = GetPixelSpanUInt();
        Span<uint> dstSpan = newImage.GetPixelSpanUInt();

        int newWidth = newImage.GetWidth();

        for (int origY = 0; origY < height; origY++)
        {
            int newYStart = origY * scale;
            for (int dy = 0; dy < scale; dy++)
            {
                int dstRowStart = (newYStart + dy) * newWidth;
                for (int origX = 0; origX < width; origX++)
                {
                    uint pixel = srcSpan[origY * width + origX];
                    int newXStart = origX * scale;

                    for (int dx = 0; dx < scale; dx++)
                    {
                        dstSpan[dstRowStart + newXStart + dx] = pixel;
                    }
                }
            }
        }

        return newImage;
    }


    public unsafe SimpleBitmap ResizeNearestVector(int scale)
    {
        var newImages = new SimpleBitmap(width * scale, height * scale);

        Span<uint> srcSpan = MemoryMarshal.Cast<byte, uint>(pixelData.AsSpan());
        Span<uint> dstSpan = MemoryMarshal.Cast<byte, uint>(newImages.pixelData.AsSpan());

        for (int origY = 0; origY < height; origY++)
        {
            int newYStart = origY * scale;
            for (int dy = 0; dy < scale; dy++)
            {
                int dstRowStart = (newYStart + dy) * newImages.GetWidth();
                for (int origX = 0; origX < width; origX++)
                {
                    uint pixel = srcSpan[origY * width + origX];
                    int newXStart = origX * scale;

                    var vecPixel = new Vector<uint>(pixel);
                    int step = Vector<uint>.Count;
                    int i = 0;
                    for (; i + step <= scale; i += step)
                    {
                        vecPixel.CopyTo(dstSpan.Slice(dstRowStart + newXStart + i, step));
                    }
                    for (; i < scale; i++)
                    {
                        dstSpan[dstRowStart + newXStart + i] = pixel;
                    }
                }
            }
        }
        return newImages;
    }
    public SimpleBitmap ResizeNearestVector2(int scale)
    {
        var newImage = new SimpleBitmap(width * scale, height * scale);

        Span<uint> srcSpan = GetPixelSpanUInt();
        Span<uint> dstSpan = newImage.GetPixelSpanUInt();

        int newWidth = newImage.GetWidth();

        for (int origY = 0; origY < height; origY++)
        {
            int newYStart = origY * scale;
            for (int dy = 0; dy < scale; dy++)
            {
                int dstRowStart = (newYStart + dy) * newWidth;
                for (int origX = 0; origX < width; origX++)
                {
                    uint pixel = srcSpan[origY * width + origX];
                    int newXStart = origX * scale;

                    var vecPixel = new Vector<uint>(pixel);
                    int step = Vector<uint>.Count;
                    int i = 0;

                    for (; i + step <= scale; i += step)
                    {
                        vecPixel.CopyTo(dstSpan.Slice(dstRowStart + newXStart + i, step));
                    }
                    for (; i < scale; i++)
                    {
                        dstSpan[dstRowStart + newXStart + i] = pixel;
                    }
                }
            }
        }

        return newImage;
    }
}
