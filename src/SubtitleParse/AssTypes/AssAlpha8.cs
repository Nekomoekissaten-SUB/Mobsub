namespace Mobsub.SubtitleParse.AssTypes;

/// <summary>
/// ASS/Aegisub alpha semantics: 0 = fully opaque, 255 = fully transparent.
/// </summary>
public readonly struct AssAlpha8 : IEquatable<AssAlpha8>
{
    public AssAlpha8(byte value) => Value = value;

    public byte Value { get; }

    public bool Equals(AssAlpha8 other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is AssAlpha8 other && Equals(other);
    public override int GetHashCode() => Value;

    public static implicit operator AssAlpha8(byte value) => new(value);
    public static implicit operator byte(AssAlpha8 value) => value.Value;
}
