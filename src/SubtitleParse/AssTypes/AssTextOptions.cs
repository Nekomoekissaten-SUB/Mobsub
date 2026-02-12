namespace Mobsub.SubtitleParse.AssTypes;

public enum AssTextDialect : byte
{
    /// <summary>
    /// VSFilter-compatible baseline (default).
    /// </summary>
    VsFilter = 0,

    /// <summary>
    /// VSFilterMod enabled: recognizes Mod tags and overloads.
    /// </summary>
    VsFilterMod = 1,

    /// <summary>
    /// “Spec-first” dialect placeholder (currently treated as VSFilter).
    /// </summary>
    Ass = 2,
}

public enum AssRendererProfile : byte
{
    VsFilter = 0,
    LibAss_0_17_4 = 1,
}

public enum AssValidationStrictness : byte
{
    Normal = 0,
    Compat = 1,
    Strict = 2,
}

public readonly record struct AssTextOptions(
    AssTextDialect Dialect = AssTextDialect.VsFilter,
    AssRendererProfile RendererProfile = AssRendererProfile.VsFilter,
    AssValidationStrictness Strictness = AssValidationStrictness.Normal)
{
    public bool ModMode => Dialect == AssTextDialect.VsFilterMod;
}

