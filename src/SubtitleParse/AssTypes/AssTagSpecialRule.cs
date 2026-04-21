namespace Mobsub.SubtitleParse.AssTypes;

public enum AssTagSpecialRule : byte
{
    None = 0,

    /// <summary>
    /// \fsc: VSFilter/libass resets scale; VSFilterMod enables \fsc&lt;scale&gt; overload.
    /// </summary>
    FontScaleFsc = 1,

    /// <summary>
    /// Parse integer payload as loose hex (VSFilterMod \rnds).
    /// </summary>
    HexInt32 = 2,

    /// <summary>
    /// \blend: VSFilterMod blend mode keyword/enum validation.
    /// </summary>
    BlendMode = 3,
}

