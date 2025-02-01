namespace Mobsub.Helper;

public readonly struct UInt24 : IEquatable<UInt24>
{
    private readonly byte b0, b1, b2;

    public const uint MinValue = 0;
    public const uint MaxValue = 0x00FFFFFF;
    public readonly uint Value => (uint)((b0 << 16) | (b1 << 8) | b2);

    public UInt24(uint value)
    {
        b0 = (byte)(value & 0xFF);
        b1 = (byte)(value >> 8 & 0xFF);
        b2 = (byte)(value >> 16 & 0xFF);
    }
    public UInt24(byte value0, byte value1, byte value2)
    {
        b0 = value0; b1 = value1; b2 = value2;
    }
    public UInt24(Span<byte> source, bool isBigEndian)
    {
        if (source.Length < 3)
        {
            throw new ArgumentOutOfRangeException();
        }

        if (isBigEndian)
        {
            b0 = source[0]; b1 = source[1]; b2 = source[2];
        }
        else
        {
            b0 = source[2]; b1 = source[1]; b2 = source[0];
        }
    }
    public override string ToString() => Value.ToString();
    public override bool Equals(object? obj) => (obj is UInt24 @ui24 && Equals(@ui24)) || (obj is uint @ui32 && Equals(ui32));
    public bool Equals(UInt24 other) => b0 == other.b0 && b1 == other.b1 && b2 == other.b2;
    public override int GetHashCode() => HashCode.Combine(b0, b1, b2);
    public static bool operator ==(UInt24 left, UInt24 right) => left.Equals(right);
    public static bool operator !=(UInt24 left, UInt24 right) => !(left == right);
    public static bool operator ==(UInt24 left, uint right) => left.Equals(right);
    public static bool operator !=(UInt24 left, uint right) => !(left == right);

    public static implicit operator UInt24(byte x) => new UInt24((uint)x);
    public static explicit operator byte(UInt24 x) => (byte)x.Value;
    public static implicit operator UInt24(ushort x) => new UInt24((uint)x);
    public static explicit operator ushort(UInt24 x) => (ushort)x.Value;
    public static implicit operator UInt24(short x) => new UInt24((uint)x);
    public static explicit operator short(UInt24 x) => (short)x.Value;
    public static implicit operator UInt24(uint x) => new UInt24(x);
    public static explicit operator uint(UInt24 x) => x.Value;
    public static implicit operator UInt24(int x) => new UInt24((uint)x);
    public static explicit operator int(UInt24 x) => (int)x.Value;
}
