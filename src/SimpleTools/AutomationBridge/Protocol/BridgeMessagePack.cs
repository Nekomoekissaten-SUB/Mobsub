using System.Buffers;
using System.Runtime.InteropServices;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using Mobsub.AutomationBridge.Common;

namespace Mobsub.AutomationBridge.Protocol;

internal static class BridgeMessagePack
{
    public const int SchemaVersion = 1;

    private static readonly MessagePackSerializerOptions Options = CreateOptions();

    public static byte[] SerializeRequest(BridgeRequest request)
        => MessagePackSerializer.Serialize(request, Options);

    public static unsafe bool TryPeekSchemaVersion(ReadOnlySpan<byte> payload, out int schemaVersion, out string? error)
    {
        try
        {
            fixed (byte* p = payload)
            {
                using var mgr = new UnmanagedMemoryManager(p, payload.Length);
                var reader = new MessagePackReader(new ReadOnlySequence<byte>(mgr.Memory));
                int n = reader.ReadArrayHeader();
                if (n < 1)
                    throw new MessagePackSerializationException("Request must be a non-empty array.");
                schemaVersion = reader.ReadInt32();
            }

            error = null;
            return true;
        }
        catch (Exception ex)
        {
            schemaVersion = 0;
            error = FormatDecodeError(ex);
            return false;
        }
    }

    public static unsafe bool TryDeserializeRequest(ReadOnlySpan<byte> payload, out BridgeRequest? request, out string? error)
    {
        try
        {
            fixed (byte* p = payload)
            {
                using var mgr = new UnmanagedMemoryManager(p, payload.Length);
                request = MessagePackSerializer.Deserialize<BridgeRequest>(mgr.Memory, Options);
            }
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            request = null;
            error = FormatDecodeError(ex);
            return false;
        }
    }

    public static unsafe bool TryDeserializeResponse(ReadOnlySpan<byte> payload, out BridgeResponse? response, out string? error)
    {
        try
        {
            fixed (byte* p = payload)
            {
                using var mgr = new UnmanagedMemoryManager(p, payload.Length);
                response = MessagePackSerializer.Deserialize<BridgeResponse>(mgr.Memory, Options);
            }
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            response = null;
            error = FormatDecodeError(ex);
            return false;
        }
    }

    public static byte[] SerializeResponse(BridgeResponse response)
        => MessagePackSerializer.Serialize(response, Options);

    private static MessagePackSerializerOptions CreateOptions()
    {
        // AOT-friendly: prefer source-generated formatters and avoid runtime codegen.
        //
        // NOTE: Avoid DynamicGenericResolver/StandardResolver (reflection-based). Register the few generic collection
        // formatters we need explicitly.
        var resolver = CompositeResolver.Create(
            formatters:
            [
                new DictionaryFormatter<string, string>(),
                new DictionaryFormatter<string, StyleInfo>(),
            ],
            resolvers:
            [
                SourceGeneratedFormatterResolver.Instance,
                BuiltinResolver.Instance,
            ]);

        return MessagePackSerializerOptions.Standard
            .WithResolver(resolver)
            .WithSecurity(MessagePackSecurity.UntrustedData);
    }

    private static string FormatDecodeError(Exception ex)
    {
        // Keep it short enough for Aegisub dialog boxes, but include the key discriminator + inner exception message.
        var msg = $"MessagePack decode failed: {ex.GetType().Name}: {ex.Message}";
        if (ex.InnerException is not null)
            msg += $" | Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
        return msg;
    }

    private sealed unsafe class UnmanagedMemoryManager : MemoryManager<byte>
    {
        private readonly byte* _pointer;
        private readonly int _length;

        public UnmanagedMemoryManager(byte* pointer, int length)
        {
            _pointer = pointer;
            _length = length;
        }

        public override Span<byte> GetSpan()
            => new(_pointer, _length);

        public override MemoryHandle Pin(int elementIndex = 0)
        {
            if ((uint)elementIndex > (uint)_length)
                throw new ArgumentOutOfRangeException(nameof(elementIndex));
            return new MemoryHandle(_pointer + elementIndex);
        }

        public override void Unpin()
        {
        }

        protected override void Dispose(bool disposing)
        {
        }
    }
}
