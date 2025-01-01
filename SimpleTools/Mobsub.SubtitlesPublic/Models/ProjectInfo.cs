namespace Mobsub.SubtitlesPublic.Models;

public class ProjectInfo
{
    public string? DirectoryName { get; set; }
    internal string? DirectoryPath { get; set; }
    public string? ProjectNameEng { get; set; }
    public string? ProjectNameJpn { get; set; }
    public SubtitlesLanguage? Language { get; set; }
    public TimelineType? TimelineType { get; set; }
    public int Episodes { get; set; }
    public string? PackageName { get; set; }
    public string? UsedFonts { get; set; }
    public string? MergeConfigure { get; set; }
    public string? PackageEffectName { get; set; }
}

public enum SubtitlesLanguage
{
    Single = 0,
    Dual = 1,
}

public enum TimelineType
{
    Web = 0,
    BluRay = 1,
}