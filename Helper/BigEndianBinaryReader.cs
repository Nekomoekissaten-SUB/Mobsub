using System.Buffers;
using System.Buffers.Binary;

namespace Mobsub.Helper;

public class BigEndianBinaryReader(Stream input) : BinaryReader(input)
{
    private byte[] buffer = ArrayPool<byte>.Shared.Rent(8);

    private Span<byte> Read(int count)
    {
        var span = buffer.AsSpan(0, count);
        var readCount = BaseStream.Read(span);
        if (readCount != count) { throw new EndOfStreamException(); }
        return span;
    }

    protected override void Dispose(bool disposing)
    {
        ArrayPool<byte>.Shared.Return(buffer);
        base.Dispose(disposing);
    }


    public override ushort ReadUInt16() => BinaryPrimitives.ReadUInt16BigEndian(Read(sizeof(ushort)));
    public override short ReadInt16() => BinaryPrimitives.ReadInt16BigEndian(Read(sizeof(short)));
    public UInt24 ReadUInt24() => new UInt24(Read(3), true);
    public override uint ReadUInt32() => BinaryPrimitives.ReadUInt32BigEndian(Read(sizeof(uint)));
    public override int ReadInt32() => BinaryPrimitives.ReadInt32BigEndian(Read(sizeof(int)));
    public short[] ReadInt16Array(int count)
    {
        var arr = new short[count];
        for (var i = 0; i < count; i++)
        {
            arr[i] = ReadInt16();
        }
        return arr;
    }
    public ushort[] ReadUInt16Array(int count)
    {
        var arr = new ushort[count];
        for (var i = 0; i < count; i++)
        {
            arr[i] = ReadUInt16();
        }
        return arr;
    }
}
