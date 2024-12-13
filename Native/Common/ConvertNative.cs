using System.Runtime.InteropServices;
using System.Text;

namespace Mobsub.Native.Common;

public unsafe class ConvertNative
{
    public static string? StringFromPtr(sbyte* ptr)
    {
        if (ptr == null)
            return null;

        return Marshal.PtrToStringUTF8((IntPtr)ptr);
    }

    public static sbyte* StringToPtr(string str)
    {
        var bytes = Encoding.UTF8.GetBytes(str);

        sbyte* sb;
        fixed (byte* p = bytes)
        {
            sb = (sbyte*)p;
        }
        return sb;
    }

    public static T[] CopyToManaged<T>(nint ptr, int length)
    {
        var arr = new T[length];
        new Span<T>((void*)ptr, length).CopyTo(new Span<T>(arr, 0, length));
        return arr;
    }
    public static IntPtr CopyToNative<T>(T[] source)
    {
        var length = source.Length;
        var sizeOfT = Marshal.SizeOf<T>();
        var totalSize = length * sizeOfT;
        var destination = Marshal.AllocHGlobal(totalSize);
        new Span<T>(source, 0, length).CopyTo(new Span<T>((void*)destination, length));
        return destination;
    }
}