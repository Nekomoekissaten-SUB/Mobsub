﻿using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.SubtitleParse.AssUtils;

public interface IAssTagProcessor
{
    void OnTag(AssTagSpan tag, AssTagDescriptor desc);
    void OnText(ReadOnlySpan<byte> text);
    void Process(AssEvent ev);
    object? GetResults();
}
