using System.Runtime.InteropServices;

using Mobsub.AutomationBridge.Dispatch;
using Mobsub.AutomationBridge.Protocol;

namespace Mobsub.AutomationBridge.Abi;

public static unsafe class Exports
{
    [UnmanagedCallersOnly(EntryPoint = "mobsub_abi_version")]
    public static int AbiVersion()
        => 1;

    [UnmanagedCallersOnly(EntryPoint = "mobsub_free")]
    public static void Free(void* p)
    {
        if (p != null)
            NativeMemory.Free(p);
    }

    [UnmanagedCallersOnly(EntryPoint = "mobsub_invoke")]
    public static int Invoke(byte* req, int reqLen, byte** resp, int* respLen)
    {
        if (resp == null || respLen == null)
            return BridgeErrorCodes.ErrBadArgs;

        *resp = null;
        *respLen = 0;

        if (req == null || reqLen <= 0)
            return WriteErrorResponse(BridgeErrorCodes.ErrBadArgs, "Invalid request buffer.", resp, respLen);

        try
        {
            var reqBytes = new ReadOnlySpan<byte>(req, reqLen);
            var result = BridgeDispatcher.Invoke(reqBytes);
            WriteResponseBytes(result.ResponseBytes, resp, respLen);
            return result.Code;
        }
        catch (Exception ex)
        {
            return WriteErrorResponse(BridgeErrorCodes.ErrHandler, ex.ToString(), resp, respLen);
        }
    }

    private static int WriteErrorResponse(int code, string message, byte** resp, int* respLen)
    {
        var response = new BridgeResponse(
            Ok: false,
            Error: message,
            Logs: null,
            Patch: null,
            Result: null,
            Methods: null);

        // Keep ABI-level error responses consistent with normal dispatcher responses:
        // always return a wrapped (MSB1 envelope) MessagePack payload.
        var bytes = BridgeEnvelope.Wrap(BridgeMessagePack.SerializeResponse(response));
        WriteResponseBytes(bytes, resp, respLen);
        return code;
    }

    private static void WriteResponseBytes(ReadOnlySpan<byte> bytes, byte** resp, int* respLen)
    {
        if (bytes.IsEmpty)
        {
            *resp = null;
            *respLen = 0;
            return;
        }

        var p = (byte*)NativeMemory.Alloc((nuint)bytes.Length);
        bytes.CopyTo(new Span<byte>(p, bytes.Length));
        *resp = p;
        *respLen = bytes.Length;
    }
}
