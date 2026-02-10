﻿using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.SubtitleParse.AssText;

public interface IAssEventTextProcessor
{
    void OnTag(AssTagSpan tag, AssTagDescriptor desc);
    void OnText(ReadOnlySpan<byte> text);
    void Process(AssEvent ev);
    object? GetResults();
}
