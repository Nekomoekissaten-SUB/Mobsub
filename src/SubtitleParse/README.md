# Mobsub.SubtitleParse

Parse common subtitles. Now support ass and srt.

## Simple use

```csharp
using Mobsub.SubtitleParse;
using Mobsub.SubtitleParse.AssTypes;

var ass = new AssData() { };
ass.ReadAssFile(YourAssFile);
ass.WriteAssFile(YourNewAssFile);

var srt = new SubRipText();
srt.FromAss(ass);
srt.WriteSrtFile(YourNewSrtFile, false);
```