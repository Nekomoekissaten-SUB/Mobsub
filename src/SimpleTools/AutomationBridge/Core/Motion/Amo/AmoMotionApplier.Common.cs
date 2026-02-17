using System.Text;

namespace Mobsub.AutomationBridge.Core.Motion.Amo;

internal static partial class AmoMotionApplier
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);
    private static readonly byte[] RawTagStartPos = "\\pos("u8.ToArray();
    private static readonly byte[] RawTagStartOrg = "\\org("u8.ToArray();
    private static readonly byte[] RawTagStartMove = "\\move("u8.ToArray();
    private static readonly byte[] RawTagStartFade = "\\fade("u8.ToArray();
    private static readonly byte[] RawTagStartClip = "\\clip("u8.ToArray();
    private static readonly byte[] RawTagStartIclip = "\\iclip("u8.ToArray();
}
