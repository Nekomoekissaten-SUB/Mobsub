using Mobsub.SubtitleProcess;
using Mobsub.SubtitleParse.AssTypes;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;

namespace Mobsub.Ikkoku.CommandLine;

internal partial class MergeCmd
{
    private static Command BuildNijigasaki(Argument<FileSystemInfo> path, Option<FileSystemInfo> optPath, Option<string[]> confVar)
    {
        var mergeTarget = new Option<string>("--target")
        {
            Description = "Target output. Can be m2ts, all, main (default).",
            DefaultValueFactory = _ => "main"
        }.AcceptOnlyFromAmong("m2ts", "all", "main");

        // m2ts: split prev, keep main and end
        // main: split prev, only merge Yu version main and end
        // all:  split prev, merge main and end

        var cmd = new Command("nijigasaki", "Internal command, only merge or split Nijigasaki movie subtitles.")
        {
            path, optPath, confVar, mergeTarget
        };

        cmd.SetAction(result =>
        {
            ExecuteNijigasaki(result.GetValue(path)!, result.GetValue(optPath)!, result.GetValue(confVar), result.GetValue(mergeTarget)!);
        });

        return cmd;
    }

    private static void ExecuteNijigasaki(FileSystemInfo path, FileSystemInfo optPath, string[]? confVar, string target)
    {
        if (path is not DirectoryInfo)
        {
            throw new Exception($"{path.FullName} must be directory");
        }
        if (optPath is not DirectoryInfo)
        {
            throw new Exception($"{optPath.FullName} must be directory");
        }
        if (confVar is null || confVar.Length < 2)
        {
            throw new ArgumentException("Missing --config-var values.");
        }

        var d = (DirectoryInfo)path;
        var assFiles = d.GetFiles($"*.{confVar[1]}.ass");

        if (SplitFirstConfigVariable(confVar[0], out var _epStart, out var _epEnd, out var _length))
        {
            for (var i = _epStart; i <= _epEnd; i++)
            {
                var ep = i.ToString($"D{_length}");
                var files = assFiles.Where(f => f.Name.Contains($"[{ep}]")).ToArray();
                MergeNijigasaki(files, (DirectoryInfo)optPath, target);
            }
        }
        else
        {
            var ep = confVar[0];
            var files = assFiles.Where(f => f.Name.Contains($"[{ep}]")).ToArray();
            MergeNijigasaki(files, (DirectoryInfo)optPath, target);
        }
    }

    private static void MergeNijigasaki(FileInfo[]? files, DirectoryInfo optDir, string target)
    {
        if (files is null || files.Length == 0)
        {
            return;
        }

        var cleanAssArgs = new Clean.CleanAssArgs
        {
            keepComment = false,
            renameTitle = true,
            addLayoutRes = true,
            dropUnusedStyles = true,
            processEvents = true,
            rmMotionGarbage = true,
            deleteFanhuaji = true,
            dropDuplicateStyles = true,
            fixStyleName = true,
        };

        // read main ass
        var mainAssData = new AssData();
        var mainFile = files.FirstOrDefault(f => f.Name.Contains("Main"));
        if (mainFile is null)
        {
            throw new FileNotFoundException("Main ass not found.");
        }
        mainAssData.ReadAssFile(mainFile.FullName);
        var mainEvents = mainAssData.Events ?? throw new InvalidDataException($"ASS events missing in {mainFile.FullName}.");
        var filePrefix = mainFile.Name.AsSpan(0, mainFile.Name.AsSpan().IndexOf($"][") + 1);
        var fileSuffix = mainFile.Name.AsSpan(mainFile.Name.AsSpan().LastIndexOf($"][") + 1);
        if (mainEvents.Collection.Count == 0)
        {
            throw new InvalidDataException($"No events in {mainFile.FullName}.");
        }
        _ = NijigasakiSplitValue(mainEvents.Collection[0].Text, out _, out var mainFrameLength);

        // read end ass
        var endAssData = new AssData();
        var endFile = files.FirstOrDefault(f => f.Name.Contains("End"));
        if (endFile is null)
        {
            throw new FileNotFoundException("End ass not found.");
        }
        endAssData.ReadAssFile(endFile.FullName);
        var endEvents = endAssData.Events ?? throw new InvalidDataException($"ASS events missing in {endFile.FullName}.");

        // Previous part
        var assData = new AssData();
        var prevDict = new Dictionary<string, EventPart>();
        var prevNonYuFile = files.FirstOrDefault(f => f.Name.Contains("Prev") && f.Name.Contains("Non-Yu"));
        var preYuFile = files.FirstOrDefault(f => f.Name.Contains("Prev") && !f.Name.Contains("Non-Yu"));
        if (prevNonYuFile is null)
        {
            throw new FileNotFoundException("Prev Non-Yu ass not found.");
        }
        if (preYuFile is null)
        {
            throw new FileNotFoundException("Prev Yu ass not found.");
        }

        assData.ReadAssFile(prevNonYuFile.FullName);
        var prevNonYuEvents = assData.Events ?? throw new InvalidDataException($"ASS events missing in {prevNonYuFile.FullName}.");
        var prevNonYuAss = prevNonYuEvents.Collection;
        var partStart = 0;
        var partName = string.Empty;
        var frameLength = 0;
        var jpnStart = 0;
        var zhoStart = 0;
        EventPart part;
        for (var i = 0; i < prevNonYuAss.Count; i++)
        {
            var evt = prevNonYuAss[i];
            if (evt.Name.Equals("0"))
            {
                if (evt.Text!.StartsWith("Shared"))
                {
                    partName = "Shared";
                }
                else
                {
                    part = new EventPart
                    {
                        events = prevNonYuAss[(partStart + 1)..i].ToArray(),
                        frameOffset = frameLength,
                        jpnStart = jpnStart,
                        zhoStart = zhoStart
                    };
                    prevDict.Add(partName!, part);
                    partStart = i;
                    _ = NijigasakiSplitValue(evt.Text!, out partName, out frameLength);
                }
            }

            UpdateStartLineNumber(evt, i, partStart, ref jpnStart, ref zhoStart);

            if (i == prevNonYuAss.Count - 1)
            {
                part = new EventPart
                {
                    events = prevNonYuAss[(partStart + 1)..].ToArray(),
                    frameOffset = frameLength,
                    jpnStart = jpnStart,
                    zhoStart = zhoStart
                };
                prevDict.Add(partName!, part);
            }
        }

        assData = new AssData();
        assData.ReadAssFile(preYuFile.FullName);
        var prevYuEvents = assData.Events ?? throw new InvalidDataException($"ASS events missing in {preYuFile.FullName}.");
        var prevYuAss = prevYuEvents.Collection;
        if (prevYuAss.Count == 0)
        {
            throw new InvalidDataException($"No events in {preYuFile.FullName}.");
        }
        _ = NijigasakiSplitValue(prevYuAss[0].Text, out partName, out frameLength);
        for (var i = 0; i < prevYuAss.Count; i++)
        {
            UpdateStartLineNumber(prevYuAss[i], i, 0, ref jpnStart, ref zhoStart);
        }
        part = new EventPart
        {
            events = prevYuAss[1..].ToArray(),
            frameOffset = frameLength,
            jpnStart = jpnStart,
            zhoStart = zhoStart
        };
        prevDict.Add(partName!, part);

        var hadShared = prevDict.TryGetValue("Shared", out var prevShared);
        foreach ((var k, var v) in prevDict)
        {
            if (k.Equals("Shared")) continue;
            var newAss = new AssData();
            newAss.Like(mainAssData);
            newAss.Styles = (AssStyles)mainAssData.Styles.DeepClone();
            var newEvents = newAss.Events ?? throw new InvalidDataException("Missing events in merged ass.");

            var jpnEndLine = 0;

            if (!k.Equals("Yu"))
            {
                AddPrevPart(newEvents.Collection, prevShared!, ref jpnEndLine);
            }
            AddPrevPart(newEvents.Collection, v, ref jpnEndLine);

            if (target == "m2ts" || (target == "main" && !k.Equals("Yu")))
            {
                WriteToFile(newAss, GetPrevFileName(filePrefix, fileSuffix, k, optDir), cleanAssArgs);
            }
            else
            {
                var mainCopy = mainEvents.Collection.Select(x => x.DeepClone()).ToList();
                var endCopy = endEvents.Collection.Select(x => x.DeepClone()).ToList();

                AddMainPart(newEvents.Collection, mainCopy, v.frameOffset);
                AddEndPart(newEvents.Collection, endCopy, v.frameOffset + mainFrameLength);
                WriteToFile(newAss, GetMainFileName(filePrefix, fileSuffix, k, optDir), cleanAssArgs);
            }
        }


        static bool NijigasakiSplitValue(string text, out string? part, out int frameLength)
        {
            var span = text.AsSpan();
            var index = span.IndexOf('(');
            if (index == -1)
            {
                part = null;
                frameLength = -1;
                return false;
            }

            part = span[..index].Trim().ToString();
            frameLength = int.Parse(span.TrimEnd()[(index + 1)..^1]);
            return true;
        }

        static void UpdateStartLineNumber(AssEvent evt, int index, int partStart, ref int jpnStart, ref int zhoStart)
        {
            if (evt.Name.Equals("1"))
            {
                if (evt.Text!.Equals("JPN"))
                {
                    jpnStart = index - partStart - 1;
                }
                else if (evt.Text.Equals("ZHO"))
                {
                    zhoStart = index - partStart - 1;
                }
            }
        }
    
        static void AddPrevPart(List<AssEvent> evt, EventPart part, ref int jpnEndLine)
        {
            for (var i = 0; i < part.events.Length; i++)
            {
                var e = part.events[i];
                if (!string.IsNullOrEmpty(e.Name))
                {
                    e.Name = string.Empty;
                }
                if (!e.IsDialogue)
                {
                    if ((e.Style is "Default" or "Top") && string.IsNullOrEmpty(e.Text))
                    {
                        part.events[i] = e;
                        continue;
                    }
                    e.IsDialogue = true;
                }
                part.events[i] = e;
            }

            if (evt.Count == 0)
            {
                evt.AddRange(part.events[1..part.zhoStart]);
                evt.AddRange(part.events[(part.zhoStart + 1)..]);
            }
            else
            {
                evt.InsertRange(jpnEndLine + 1, part.events[(part.jpnStart + 1)..part.zhoStart]);
                evt.AddRange(part.events[(part.zhoStart + 1)..]);
            }

            jpnEndLine += part.zhoStart - 2;
        }

        static void AddMainPart(List<AssEvent> evt, List<AssEvent> main, int frameOffset)
        {
            var _jpnStart = 0;
            var _zhoPrevStart = evt.FindIndex(x => x.Style.StartsWith("Dial-CH"));
            var _zhoMainStart = 0;
            var _firstLineNumber = main[0].lineNumber;
            foreach (var e in main)
            {
                if (e.Text == "Dialogue" && e.IsDialogue is false)
                {
                    _jpnStart = e.lineNumber - _firstLineNumber + 1;
                }

                if (e.Style.StartsWith("Dial-CH"))
                {
                    _zhoMainStart = e.lineNumber - _firstLineNumber;
                    break;
                }
            }

            Tpp.ShiftAss(main, TimeSpan.FromMilliseconds(Utils.FrameToMillisecond(frameOffset, Utils.UnifiedFps("23.976"))));
            evt.InsertRange(0, main[1.._jpnStart]);
            evt.InsertRange(_zhoPrevStart, main[_jpnStart.._zhoMainStart]);
            evt.AddRange(main[_zhoMainStart..]);
        }

        static void AddEndPart(List<AssEvent> evt, List<AssEvent> end, int frameOffset)
        {
            if (string.IsNullOrEmpty(evt.Last().Text))
            {
                evt.RemoveAt(evt.Count - 1);
            }

            Tpp.ShiftAss(end, TimeSpan.FromMilliseconds(Utils.FrameToMillisecond(frameOffset, Utils.UnifiedFps("23.976"))));
            evt.AddRange(end);
        }

        static void WriteToFile(AssData ass, string optName, Clean.CleanAssArgs cleanArgs)
        {
            Clean.CleanAss(ass, Path.GetFileNameWithoutExtension(optName), cleanArgs, out _, out _);
            ass.WriteAssFile(optName);
        }

        static string GetPrevFileName(ReadOnlySpan<char> prefix, ReadOnlySpan<char> suffix, string stem, DirectoryInfo optDir)
        {
            return Path.Combine(optDir.FullName, $"{prefix}[Prev][{stem}]{suffix}");
        }

        static string GetMainFileName(ReadOnlySpan<char> prefix, ReadOnlySpan<char> suffix, string stem, DirectoryInfo optDir)
        {
            return Path.Combine(optDir.FullName, $"{prefix}[{stem}]{suffix}");
        }
    }

    private class EventPart
    {
        internal AssEvent[] events = [];
        internal int frameOffset;
        internal int jpnStart;
        internal int zhoStart;
    }
}
