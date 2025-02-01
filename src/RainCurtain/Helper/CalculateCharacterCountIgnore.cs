using System;

namespace Mobsub.RainCurtain.Helper;

[Flags]
public enum CalculateCharacterCountIgnore
{
    Punctuation = 1 << 0,
    WhiteSpace = 1 << 1,
}