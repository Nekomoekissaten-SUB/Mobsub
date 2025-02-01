global using Mobsub.Native.Common;
using System.Runtime.InteropServices;

namespace Mobsub.Native.VapoursynthBinding.Native.API;

// ReSharper disable InconsistentNaming
public enum VSColorFamily : int
{
    // api3
    cmGray = 1000000,
    cmRGB = 2000000,
    cmYUV = 3000000,
    cmYCoCg = 4000000,
    cmCompat = 9000000,

    // api4
    cfUndefined = 0,
    cfGray = 1,
    cfRGB = 2,
    cfYUV = 3,
}

// public enum VSPropertyType : int
// {
//     // api3
//     ptUnset3 = (sbyte)'u',
//     ptInt3 = (sbyte)'i',
//     ptFloat3 = (sbyte) 'f',
//     ptData3 = (sbyte)'s',
//     ptNode3 = (sbyte)'c',
//     ptFrame3 = (sbyte)'v',
//     ptFunction3 = (sbyte)'m',
//
//     // api4
//     ptUnset = 0,
//     ptInt = 1,
//     ptFloat = 2,
//     ptData = 3,
//     ptFunction = 4,
//     ptVideoNode = 5,
//     ptAudioNode = 6,
//     ptVideoFrame = 7,
//     ptAudioFrame = 8,
// }

public static unsafe partial class Methods
{
    [DllImport(Library.VapoursynthDll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const VSAPI *")]
    public static extern VSAPI* getVapourSynthAPI(int version);
}
