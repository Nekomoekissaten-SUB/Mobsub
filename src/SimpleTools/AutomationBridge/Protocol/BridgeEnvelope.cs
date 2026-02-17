using System.Buffers.Binary;

namespace Mobsub.AutomationBridge.Protocol;

internal static class BridgeEnvelope
{
    // ASCII "MSB1"
    private static readonly uint Magic = 0x3142534Du;

    public static bool TryUnwrap(
        ReadOnlySpan<byte> request,
        out ReadOnlySpan<byte> payload,
        out string? error)
    {
        payload = default;
        error = null;

        if (request.Length < 4)
        {
            error = "Missing MSB1 envelope.";
            return false;
        }

        if (BinaryPrimitives.ReadUInt32LittleEndian(request) != Magic)
        {
            error = "Missing MSB1 envelope.";
            return false;
        }

        payload = request.Slice(4);
        return true;
    }

    public static byte[] Wrap(ReadOnlySpan<byte> payload)
    {
        var bytes = new byte[4 + payload.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0, 4), Magic);
        payload.CopyTo(bytes.AsSpan(4));
        return bytes;
    }
}
