using Mobsub.SubtitleParseNT2.AssTypes;

namespace Mobsub.SubtitleParseNT2.AssUtils;

public interface IAssTagProcessor
{
    void OnTag(AssTagSpan tag, AssTagDescriptor desc);
    void OnText(ReadOnlySpan<byte> text);
}
