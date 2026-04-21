﻿using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.SubtitleParse.AssText;

public interface IAssEventTextProcessor
{
    void OnTag(AssTagSpan tag);
    void OnText(ReadOnlySpan<byte> text);
    void Process(AssEvent ev);
    object? GetResults();
}
