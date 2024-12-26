using LibGit2Sharp;
using Mobsub.SubtitleParse.AssTypes;
using Mobsub.ZhConvert;
using OpenCCSharp.Conversion;
using System.Text;
using Mobsub.SubtitleProcess;

namespace Mobsub.SubtitleProcessNotAot;

public static class MergeSimplifiedChineseGitDiff
{
    public static void MergeDiffByDirectory(DirectoryInfo dir, string[] subtitleSuffixes, string startCommitId, string endCommitId, string confName)
    {
        if (FindGitRootDirectory(dir.FullName, out var repoLocalPath, out var relativePath))
        {
            subtitleSuffixes = subtitleSuffixes.Length == 0 ? GetLanguageSuffixes(dir) : subtitleSuffixes;
            if (subtitleSuffixes.Length == 0)
            {
                throw new ArgumentException();
            }
            else
            {
                var baseSuffix = subtitleSuffixes[0];
                var targetSuffix = subtitleSuffixes[1];
                MergeGitDiffToCht(repoLocalPath, startCommitId, endCommitId, relativePath, baseSuffix, targetSuffix, confName);
            }
        }
        else
        {
            throw new DirectoryNotFoundException();
        }
    }
    
    
    private struct MergeGitDiffParams
    {
        internal bool toCht;
        internal bool isAss;
        internal ChainedScriptConverter? converter;
    }

    public static void MergeGitDiffToCht(string repoLocalPath, string startCommitId, string endCommitId, string relativePath, string baseSuffix, string targetSuffix, string? convConfPath)
    {
        var pfParams = new MergeGitDiffParams() { toCht =  true, isAss = true };
        if (pfParams.toCht )
        {
            if (convConfPath is null)
            {
                throw new ArgumentNullException();
            }
            else
            {
                var dicts = OpenCCSharpUtils.LoadJson(new FileInfo(convConfPath));
                pfParams.converter = OpenCCSharpUtils.GetConverter(dicts);
            }
        }
        var evtFormats = AssConstants.FormatV4P.Split(',').Select(s => s.Trim()).ToArray();

        using var repo = new Repository(repoLocalPath);
        var commit1 = repo.Lookup<Commit>(startCommitId);
        var commit2 = repo.Lookup<Commit>(endCommitId);
        var tree1 = commit1.Tree;
        var tree2 = commit2.Tree;

        var relativePathProcess = relativePath.Contains('\\') ? relativePath.Replace("\\", "/") : relativePath;
        var diffs = repo.Diff.Compare<Patch>(tree1, tree2, new[] { relativePathProcess });
        var fullPath = new FileInfo(Path.Combine(repoLocalPath, relativePathProcess)).FullName;
        var filterDiffs = Directory.Exists(fullPath) ? diffs.Where(f => f.Path.EndsWith(baseSuffix)) : diffs;

        foreach (var diff in diffs)
        {
            if (diff.Status != ChangeKind.Modified)
            {
                continue;
            }
            var sourceFile = diff.OldPath;
            var targetFile = sourceFile.Replace(baseSuffix, targetSuffix);
            var sourceFileFull = new FileInfo(Path.Combine(repoLocalPath, sourceFile)).FullName;
            var targetFileFull = new FileInfo(Path.Combine(repoLocalPath, targetFile)).FullName;
            var targetFileTemp = targetFileFull + "_tmp";

            if (!File.Exists(targetFileFull))
            {
                Console.WriteLine("Warning: not found " + targetFile);
                //throw new Exception("Please check " + targetSuffix + ", not found " + targetFileFull);
                continue;
            }

            if (CompareLinesNumber(sourceFileFull, targetFileFull) != 0)
            {
                Console.WriteLine("Maybe error: " + targetFile);
            }

            var addLinesCopy = new List<Line>(diff.AddedLines.ToArray());
            var deleteLinesCopy = new List<Line>(diff.DeletedLines.ToArray());

            using var fs = new FileStream(targetFileFull, FileMode.Open, FileAccess.Read);
            using var sr = new StreamReader(fs);

            using var fsTmp = new FileStream(targetFileTemp, FileMode.Create, FileAccess.Write);
            using var sw = new StreamWriter(fsTmp, sr.CurrentEncoding);

            foreach (var line in MergeGitDiff(sr, deleteLinesCopy, addLinesCopy, pfParams, evtFormats))
            {
                sw.WriteLine(line);
            }

            sr.Close();
            fs.Close();
            File.Delete(targetFileFull);

            sw.Close();
            fsTmp.Close();
            File.Move(targetFileTemp, targetFileFull);
        }
    }

    private static IEnumerable<string> MergeGitDiff(StreamReader sr, List<Line> deleteLines, List<Line> addLines, MergeGitDiffParams pfParams, string[]? formats)
    {
        // now only line by line correspondence
        string? line;
        var currentLineNumber = 1;
        var offset = 0;

        while ((line = sr.ReadLine()) != null)
        {
            var deleteLine = deleteLines.FirstOrDefault(l => l.LineNumber == currentLineNumber).Content;
            var addLine = GetAddLine(addLines, currentLineNumber, ref offset, pfParams, formats, out var add);

            if (deleteLine != null)
            {
                if (add)
                {
                    yield return addLine;
                }
                offset--;
            }
            else
            {
                if (add)
                {
                    yield return addLine;
                    yield return line;
                }
                else
                {
                    yield return line;
                }
            }
            currentLineNumber++;
        }

        foreach (var addLine in addLines)
        {
            yield return addLine.Content.TrimEnd('\n');
        }
    }

    private static void GitEventLineConvertToCht(ReadOnlySpan<char> sp, ConvertSimplifiedChinese evtConverter, StringBuilder sb, int lineNum, string[] formats)
    {
        var evt = new AssEvent();
        evt.Read(sp[..^1], lineNum - 1, formats);
        evtConverter.ZhConvertEventByOpenccSharp(evt);
        evt.Write(sb, formats, false);
    }

    private static int CompareLinesNumber(string file1, string file2)
    {
        var fl1 = File.ReadLines(file1).Count();
        var fl2 = File.ReadLines(file2).Count();
        return fl2 - fl1;
    }
    private static string GetAddLine(List<Line> addLines, int currentLineNumber, ref int offset, MergeGitDiffParams pfParams, string[]? formats, out bool add)
    {
        var sb = new StringBuilder();
        var evtConverter = pfParams is { isAss: true, toCht: true } ? new ConvertSimplifiedChinese(pfParams.converter!) : null;
        add = false;

        var startIndex = 0;
        var offsetCount = 0;
        for (var i = 0; i < addLines.Count; i++)
        {
            var line = addLines[i];
            var lineNum = currentLineNumber + offset;
            if (line.LineNumber == lineNum)
            {
                if (offsetCount == 0) { startIndex = i; }
                add = line.Content != null;

                if (add)
                {
                    if (sb.Length > 0) { sb.Append(Environment.NewLine); }
                    var span = line.Content!.AsSpan();
                    if (evtConverter is not null && AssConstants.IsEventLine(span))
                    {
                        GitEventLineConvertToCht(span, evtConverter, sb, lineNum, formats!);
                    }
                    else
                    {
                        sb.Append(line.Content!.TrimEnd('\n'));
                    }

                    offset++;
                    offsetCount++;
                }
            }
        }
        if (add) { addLines.RemoveRange(startIndex, offsetCount); }
        
        return sb.ToString();
    }

    
    private static bool FindGitRootDirectory(string startPath, out string rootPath, out string relativePath)
    {
        var currentPath = startPath;
        rootPath = relativePath = string.Empty;

        while (!string.IsNullOrEmpty(currentPath))
        {
            var gitPath = Path.Combine(currentPath, ".git");
            if (Directory.Exists(gitPath))
            {
                rootPath = currentPath;
                break;
            }
            var parentDir = Directory.GetParent(currentPath);
            if (parentDir == null)
            {
                break;
            }
            currentPath = parentDir.FullName;
        }

        if (!string.IsNullOrEmpty(rootPath))
        {
            relativePath = Path.GetRelativePath(rootPath, startPath);
            return true;
        }
        return false;
    }
    
    private static string[] GetLanguageSuffixes(DirectoryInfo path)
    {
        var suffixes = new[] { "*.jpsc.ass", "*.sc.ass", "*.chs.ass", "*.gb.ass" };
        var options = new EnumerationOptions
        {
            MatchCasing = MatchCasing.CaseInsensitive,
            RecurseSubdirectories = false
        };
        string? ssuffix = null;

        foreach (var suffix in suffixes)
        {
            var files = path.GetFiles(suffix, options);
            if (files.Length > 0)
            {
                ssuffix = files[0].Name[^(suffix.Length - 1)..];
                break;
            }
        }

        return ssuffix is null ? [] : [ssuffix, GetChtSuffix(ssuffix)];
    }
    
    private static string GetChtSuffix(string suffix)
    {
        var mappings = new[]
        {
            new { From = "sc", To = "tc" },
            new { From = "chs", To = "cht" },
            new { From = "gb", To = "big5" }
        };

        var sb = new StringBuilder(suffix);

        foreach (var mapping in mappings)
        {
            var index = sb.ToString().IndexOf(mapping.From, StringComparison.OrdinalIgnoreCase);
            if (index != -1)
            {
                var actualFrom = suffix.Substring(index, mapping.From.Length);
                var replacement = actualFrom.Select((c, i) =>
                    char.IsUpper(c) ? mapping.To[i].ToString().ToUpper() : mapping.To[i].ToString().ToLower()
                ).Aggregate((a, b) => a + b);
                sb.Replace(actualFrom, replacement, index, mapping.From.Length);
                break;
            }
        }
        return sb.ToString();
    }
}