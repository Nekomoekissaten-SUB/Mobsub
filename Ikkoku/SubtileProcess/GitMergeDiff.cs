using LibGit2Sharp;
using Mobsub.AssTypes;
using Mobsub.ZhConvert;
using OpenCCSharp.Conversion;
using System.Text;

namespace Mobsub.Ikkoku;

public partial class SubtileProcess
{
    private struct GitMergeDiffParams
    {
        internal bool toCht;
        internal bool isAss;
        internal ChainedScriptConverter? converter;
    }

    public static void GitMergeDiffToCht(string repoLocalPath, string startCommitId, string endCommitId, string relativePath, string baseSuffix, string targetSuffix, string? convConfPath)
    {
        var pfParams = new GitMergeDiffParams() { toCht =  true, isAss = true };
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
        Commit commit1 = repo.Lookup<Commit>(startCommitId);
        Commit commit2 = repo.Lookup<Commit>(endCommitId);
        Tree tree1 = commit1.Tree;
        Tree tree2 = commit2.Tree;

        var relativePathProcss = relativePath.Contains('\\') ? relativePath.Replace("\\", "/") : relativePath;
        var diffs = repo.Diff.Compare<Patch>(tree1, tree2, new[] { relativePathProcss });
        var fullPath = new FileInfo(Path.Combine(repoLocalPath, relativePathProcss)).FullName;
        var filterDiffs = Directory.Exists(fullPath) ? diffs.Where(f => f.Path.EndsWith(baseSuffix)) : diffs;

        foreach (var diff in diffs)
        {
            var sourceFile = diff.OldPath;
            var targetFile = sourceFile.Replace(baseSuffix, targetSuffix);
            var sourceFileFull = new FileInfo(Path.Combine(repoLocalPath, sourceFile)).FullName;
            var targetFileFull = new FileInfo(Path.Combine(repoLocalPath, targetFile)).FullName;
            var targetFileTemp = targetFileFull + "_tmp";

            if (!File.Exists(targetFileFull))
            {
                throw new Exception("Please check " + targetSuffix + ", not found " + targetFileFull);
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

            foreach (var line in GitMergeDiff(sr, deleteLinesCopy, addLinesCopy, pfParams, evtFormats))
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

    private static IEnumerable<string> GitMergeDiff(StreamReader sr, List<Line> deleteLines, List<Line> addLines, GitMergeDiffParams pfParams, string[]? formats)
    {
        // now only line by line correspondence
        string? line;
        var currentLineNumber = 1;

        while ((line = sr.ReadLine()) != null)
        {
            var deleteLine = deleteLines.FirstOrDefault(l => l.LineNumber == currentLineNumber).Content;
            var addLine = addLines.FirstOrDefault(l => l.LineNumber == currentLineNumber);
            
            var add = addLine.Content != null;
            var addContent = string.Empty;
            if (add)
            {
                var span = addLine.Content!.AsSpan();
                if (pfParams.isAss && pfParams.toCht && AssConstants.IsEventLine(span))
                {
                    addContent = GitEventLineConvertToCht(span, pfParams.converter!, currentLineNumber, formats!);
                }
                else
                {
                    addContent = addLine.Content!.TrimEnd('\n');
                }
            }

            if (deleteLine != null)
            {
                if (add)
                {
                    yield return addContent;
                    addLines.Remove(addLine);
                }
            }
            else
            {
                if (add)
                {
                    yield return addContent;
                    addLines.Remove(addLine);
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

    private static string GitEventLineConvertToCht(ReadOnlySpan<char> sp, ChainedScriptConverter converter, int lineNum, string[] formats)
    {
        var evt = new AssEvent();
        evt.Read(sp[..^1], lineNum - 1, formats);
        if (!NotZhConvert(evt))
        {
            ZhConvertEventLineByOpenccsharp(evt.Text, new StringBuilder(), converter, out var changes);
        }  
        var sb = new StringBuilder();
        evt.Write(sb, formats, false);
        return sb.ToString();
    }

    private static int CompareLinesNumber(string file1, string file2)
    {
        var fl1 = File.ReadLines(file1).Count();
        var fl2 = File.ReadLines(file2).Count();
        return fl2 - fl1;
    }
}
