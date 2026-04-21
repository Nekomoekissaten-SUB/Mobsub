using System.Buffers;
using Mobsub.AutomationBridge.Protocol;
using Mobsub.SubtitleParse.AssText;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.AutomationBridge.Scripts.Hydra;

internal sealed class HydraTagSet : IDisposable
{
    private static readonly AssTextOptions TagOptions = new(Dialect: AssTextDialect.VsFilterMod);

    private byte[]? _mask;
    private readonly int _tagCount;

    public bool Any { get; }

    private HydraTagSet(byte[] mask, int tagCount, bool any)
    {
        _mask = mask;
        _tagCount = tagCount;
        Any = any;
    }

    public static HydraTagSet FromTagsPayload(ReadOnlySpan<byte> tagsUtf8)
    {
        int tagCount = AssTagRegistry.TagCount;
        byte[] mask = ArrayPool<byte>.Shared.Rent(tagCount);
        Array.Clear(mask, 0, tagCount);

        bool any = false;
        var scanner = new AssOverrideTagScanner(tagsUtf8, payloadAbsoluteStartByte: 0, lineBytes: default, TagOptions);
        while (scanner.MoveNext(out var token))
        {
            if (!token.IsKnown)
                continue;

            any = true;
            Mark(mask, token.Tag);
        }

        return new HydraTagSet(mask, tagCount, any);
    }

    public static HydraTagScope NormalizeScope(HydraTagScope scope)
        => scope is HydraTagScope.FirstBlock or HydraTagScope.AllBlocks ? scope : HydraTagScope.FirstBlock;

    public bool Contains(AssTag tag)
    {
        int i = (int)tag;
        var mask = _mask;
        if (mask is null)
            return false;
        if ((uint)i >= (uint)_tagCount)
            return false;
        return mask[i] != 0;
    }

    public void Dispose()
    {
        var mask = _mask;
        if (mask is not null)
        {
            _mask = null;
            ArrayPool<byte>.Shared.Return(mask, clearArray: true);
        }
    }

    private static void Mark(byte[] mask, AssTag tag)
    {
        int tagCount = AssTagRegistry.TagCount;
        void Set(AssTag t)
        {
            int i = (int)t;
            if ((uint)i < (uint)tagCount)
                mask[i] = 1;
        }

        Set(tag);

        // Alias/equivalence groups (best-effort, matching common TS expectations).
        switch (tag)
        {
            case AssTag.ColorPrimary:
            case AssTag.ColorPrimaryAbbreviation:
                Set(AssTag.ColorPrimary);
                Set(AssTag.ColorPrimaryAbbreviation);
                break;

            case AssTag.Alignment:
            case AssTag.AlignmentLegacy:
                Set(AssTag.Alignment);
                Set(AssTag.AlignmentLegacy);
                break;

            case AssTag.FontRotationZ:
            case AssTag.FontRotationZSimple:
                Set(AssTag.FontRotationZ);
                Set(AssTag.FontRotationZSimple);
                break;

            case AssTag.Border:
            case AssTag.BorderX:
            case AssTag.BorderY:
                Set(AssTag.Border);
                Set(AssTag.BorderX);
                Set(AssTag.BorderY);
                break;

            case AssTag.Shadow:
            case AssTag.ShadowX:
            case AssTag.ShadowY:
                Set(AssTag.Shadow);
                Set(AssTag.ShadowX);
                Set(AssTag.ShadowY);
                break;

            case AssTag.FontScale:
            case AssTag.FontScaleX:
            case AssTag.FontScaleY:
                Set(AssTag.FontScale);
                Set(AssTag.FontScaleX);
                Set(AssTag.FontScaleY);
                break;

            case AssTag.Position:
            case AssTag.Movement:
                Set(AssTag.Position);
                Set(AssTag.Movement);
                break;

            case AssTag.Clip:
            case AssTag.InverseClip:
                Set(AssTag.Clip);
                Set(AssTag.InverseClip);
                break;
        }
    }
}
