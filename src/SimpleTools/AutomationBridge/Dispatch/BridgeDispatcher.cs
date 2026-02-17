using Mobsub.AutomationBridge.Protocol;
using Mobsub.AutomationBridge.Scripts;

namespace Mobsub.AutomationBridge.Dispatch;

internal static class BridgeDispatcher
{
    internal readonly record struct InvokeResult(int Code, byte[] ResponseBytes);

    public static InvokeResult Invoke(ReadOnlySpan<byte> requestBytes)
    {
        if (!BridgeEnvelope.TryUnwrap(
                requestBytes,
                out var payload,
                out var envelopeError))
        {
            return EncodeErrorResponse(
                BridgeErrorCodes.ErrDecode,
                envelopeError ?? "Invalid envelope.");
        }

        if (payload.Length > BridgeProtocolLimits.MaxPayloadBytes)
        {
            return EncodeErrorResponse(
                BridgeErrorCodes.ErrDecode,
                $"Request too large: {payload.Length} bytes (max {BridgeProtocolLimits.MaxPayloadBytes}).");
        }

        if (!BridgeMessagePack.TryPeekSchemaVersion(payload, out int schemaVersion, out var peekError))
        {
            return EncodeErrorResponse(
                BridgeErrorCodes.ErrDecode,
                peekError ?? "Decode failed.");
        }

        var logs = new List<string>(capacity: 8);

        if (schemaVersion != BridgeMessagePack.SchemaVersion)
        {
            return EncodeErrorResponse(
                BridgeErrorCodes.ErrDecode,
                $"Unsupported schema_version: {schemaVersion} (expected {BridgeMessagePack.SchemaVersion}).");
        }

        if (!BridgeMessagePack.TryDeserializeRequest(payload, out var request, out var decodeError))
        {
            return EncodeErrorResponse(
                BridgeErrorCodes.ErrDecode,
                decodeError ?? "Decode failed.");
        }

        if (request is null)
            return EncodeErrorResponse(BridgeErrorCodes.ErrDecode, "Request is null.");

        if (request.SchemaVersion != BridgeMessagePack.SchemaVersion)
        {
            return EncodeErrorResponse(
                BridgeErrorCodes.ErrDecode,
                $"Unsupported schema_version: {request.SchemaVersion} (expected {BridgeMessagePack.SchemaVersion}).");
        }

        var call = request.Call;
        if (call is null)
            return EncodeErrorResponse(BridgeErrorCodes.ErrDecode, "call is required.");

        try
        {
            var result = BridgeScriptCatalog.Dispatch(call, logs);
            return EncodeResponse(result.Code, result.Response);
        }
        catch (Exception ex)
        {
            logs.Add("handler_error: " + ex.Message);
            var err = new BridgeResponse(false, ex.ToString(), logs.ToArray(), Patch: null, Result: null, Methods: null);
            return EncodeResponse(BridgeErrorCodes.ErrHandler, err);
        }

        // unreachable
    }


    private static InvokeResult EncodeErrorResponse(int code, string errorMessage)
    {
        var resp = new BridgeResponse(
            Ok: false,
            Error: errorMessage,
            Logs: null,
            Patch: null,
            Result: null,
            Methods: null);

        return EncodeResponse(code, resp);
    }

    private static InvokeResult EncodeResponse(int code, BridgeResponse response)
    {
        byte[] payload = BridgeMessagePack.SerializeResponse(response);
        return new InvokeResult(code, BridgeEnvelope.Wrap(payload));
    }
}
