using System.Buffers;
using MessagePack;
using MessagePack.Formatters;

namespace Mobsub.AutomationBridge.Protocol;

internal sealed class Utf8BytesReadOnlyMemoryFormatter : IMessagePackFormatter<ReadOnlyMemory<byte>?>
{
    public static Utf8BytesReadOnlyMemoryFormatter Instance { get; } = new();

    public void Serialize(ref MessagePackWriter writer, ReadOnlyMemory<byte>? value, MessagePackSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNil();
            return;
        }

        var span = value.Value.Span;

        // Always write as bin so Lua can receive it as a raw string (MessagePack.lua maps bin->string).
        writer.WriteBinHeader(span.Length);
        writer.WriteRaw(span);
    }

    public ReadOnlyMemory<byte>? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
            return null;

        static ReadOnlyMemory<byte> CopySequenceToArray(ReadOnlySequence<byte> seq)
        {
            if (seq.IsSingleSegment)
                return seq.First;

            if (seq.Length > int.MaxValue)
                throw new MessagePackSerializationException("Binary/string too large.");

            byte[] arr = new byte[(int)seq.Length];
            seq.CopyTo(arr);
            return arr;
        }

        switch (reader.NextMessagePackType)
        {
            case MessagePackType.Binary:
                {
                    var seq = reader.ReadBytes();
                    if (seq is null)
                        return null;
                    return CopySequenceToArray(seq.Value);
                }

            case MessagePackType.String:
                {
                    ReadOnlySequence<byte>? seq = reader.ReadStringSequence();
                    return seq is null ? ReadOnlyMemory<byte>.Empty : CopySequenceToArray(seq.Value);
                }

            default:
                throw new MessagePackSerializationException($"Expected str/bin for utf8 bytes, got {reader.NextMessagePackType}.");
        }
    }
}
