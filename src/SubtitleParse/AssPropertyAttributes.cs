namespace Mobsub.SubtitleParse;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
internal class AssPropertyAttribute : Attribute
{
    public string[]? InvalidatesProperties { get; set; }
    public bool SyncToModel { get; set; } = true;
    public string? Description { get; set; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
internal class AssModelAttribute : Attribute
{
    public bool GenerateBatchUpdate { get; set; } = true;
    public bool AutoPropertyGeneration { get; set; } = false;
}

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
internal class AssCachedPropertyAttribute : Attribute
{
    public string[]? DependsOn { get; set; }
    public string? CalculationMethod { get; set; }
}
