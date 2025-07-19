namespace Mobsub.SubtitleParse.AssTypes;

[Flags]
public enum AssParseOption
{
    None = 0,
    FixStyleName = 1 << 0,
    DropDuplicateStyle = 1 << 1,
}
