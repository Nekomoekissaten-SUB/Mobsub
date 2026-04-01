using Mobsub.Helper.ZhConvert;
using Mobsub.SubtitleParse.AssTypes;
using OpenCCSharp.Conversion;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using static XenoAtom.Interop.libgit2;

namespace Mobsub.SubtitleProcess;

public static class MergeSimplifiedChineseGitDiff
{
    public sealed record GitCommitInfo(string Sha, string Summary, string CommitterName, DateTimeOffset When);

    public sealed record GitDiffEntryInfo(string Path, git_delta_t Status);

    public static void MergeDiffByDirectory(DirectoryInfo dir, string[] subtitleSuffixes, string startCommitId, string endCommitId, string confName)
    {
        if (FindGitRootDirectory(dir.FullName, out var repoLocalPath, out var relativePath))
        {
            subtitleSuffixes = subtitleSuffixes.Length == 0 ? GetLanguageSuffixes(dir) : subtitleSuffixes;
            if (subtitleSuffixes.Length == 0)
            {
                throw new ArgumentException();
            }

            var baseSuffix = subtitleSuffixes[0];
            var targetSuffix = subtitleSuffixes[1];
            MergeGitDiffToCht(repoLocalPath, startCommitId, endCommitId, relativePath, baseSuffix, targetSuffix, confName);
        }
        else
        {
            throw new DirectoryNotFoundException();
        }
    }


    public struct MergeGitDiffParams
    {
        internal bool toCht;
        internal bool isAss;
        internal ChainedScriptConverter? converter;
    }

    private sealed class GitPatchEntry
    {
        public GitPatchEntry(string oldPath, git_delta_t status)
        {
            OldPath = oldPath;
            Status = status;
        }

        public string OldPath { get; }
        public git_delta_t Status { get; }
        public List<GitLine> AddedLines { get; } = [];
        public List<GitLine> DeletedLines { get; } = [];
    }

    private readonly struct GitLine
    {
        public GitLine(int lineNumber, string? content)
        {
            LineNumber = lineNumber;
            Content = content;
        }

        public int LineNumber { get; }
        public string? Content { get; }
    }

    public static MergeGitDiffParams GetMergeGitDiffParams(string? convConfPath, bool isAss = true)
    {
        var pfParams = new MergeGitDiffParams() { toCht = true, isAss = isAss };
        if (pfParams.toCht)
        {
            if (convConfPath is null)
            {
                throw new ArgumentNullException();
            }

            var dicts = OpenCCSharpUtils.LoadJson(new FileInfo(convConfPath));
            pfParams.converter = OpenCCSharpUtils.GetConverter(dicts);
        }

        return pfParams;
    }

    public static void MergeGitDiffToCht(string repoLocalPath, string startCommitId, string endCommitId,
        string relativePath, string baseSuffix, string targetSuffix, string? convConfPath)
    {
        var pfParams = GetMergeGitDiffParams(convConfPath);

        var diffs = GetDirectoryPatch(repoLocalPath, startCommitId, endCommitId, relativePath);
        MergeGitDiffToCht(diffs, repoLocalPath, baseSuffix, targetSuffix, pfParams);
    }

    public static IReadOnlyList<GitCommitInfo> GetWorkDirectoryCommits(
        string repoLocalPath,
        string relativePath,
        int maxCount = 0,
        CancellationToken cancellationToken = default)
    {
        var commits = new List<GitCommitInfo>();
        git_libgit2_init().Check();
        try
        {
            git_repository repo;
            git_repository_open(out repo, repoLocalPath).Check();
            try
            {
                git_revwalk walk;
                git_revwalk_new(out walk, repo).Check();
                try
                {
                    git_revwalk_sorting(walk, git_sort_t.GIT_SORT_TIME | git_sort_t.GIT_SORT_TOPOLOGICAL);
                    git_revwalk_push_head(walk).Check();
                    var normalizedRelativePath = NormalizeRelativePath(relativePath);

                    git_oid oid;
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var res = git_revwalk_next(out oid, walk);
                        if (res.ErrorCode == git_error_code.GIT_ITEROVER)
                        {
                            break;
                        }
                        res.Check();

                        git_commit commit;
                        git_commit_lookup(out commit, repo, in oid).Check();
                        try
                        {
                            if (!CommitTouchesPath(commit, normalizedRelativePath))
                            {
                                continue;
                            }
                            commits.Add(ReadCommitInfo(commit, in oid));
                            if (maxCount > 0 && commits.Count >= maxCount)
                            {
                                break;
                            }
                        }
                        finally
                        {
                            git_commit_free(commit);
                        }
                    }
                }
                finally
                {
                    git_revwalk_free(walk);
                }
            }
            finally
            {
                git_repository_free(repo);
            }
        }
        finally
        {
            git_libgit2_shutdown();
        }

        return commits;
    }

    public static GitCommitInfo? GetLatestCommitInfo(string repoLocalPath, string relativePath, string? refName)
    {
        git_libgit2_init().Check();
        try
        {
            git_repository repo;
            git_repository_open(out repo, repoLocalPath).Check();
            try
            {
                git_revwalk walk;
                git_revwalk_new(out walk, repo).Check();
                try
                {
                    git_revwalk_sorting(walk, git_sort_t.GIT_SORT_TIME | git_sort_t.GIT_SORT_TOPOLOGICAL);
                    var normalizedRelativePath = NormalizeRelativePath(relativePath);

                    if (!string.IsNullOrWhiteSpace(refName))
                    {
                        var pushResult = git_revwalk_push_ref(walk, refName);
                        if (pushResult.ErrorCode == git_error_code.GIT_ENOTFOUND)
                        {
                            return null;
                        }
                        pushResult.Check();
                    }
                    else
                    {
                        git_revwalk_push_head(walk).Check();
                    }

                    git_oid oid;
                    while (true)
                    {
                        var res = git_revwalk_next(out oid, walk);
                        if (res.ErrorCode == git_error_code.GIT_ITEROVER)
                        {
                            break;
                        }
                        res.Check();

                        git_commit commit;
                        git_commit_lookup(out commit, repo, in oid).Check();
                        try
                        {
                            if (!CommitTouchesPath(commit, normalizedRelativePath))
                            {
                                continue;
                            }
                            return ReadCommitInfo(commit, in oid);
                        }
                        finally
                        {
                            git_commit_free(commit);
                        }
                    }
                }
                finally
                {
                    git_revwalk_free(walk);
                }
            }
            finally
            {
                git_repository_free(repo);
            }
        }
        finally
        {
            git_libgit2_shutdown();
        }

        return null;
    }

    public static IReadOnlyList<GitDiffEntryInfo> GetDirectoryDiffEntries(string repoLocalPath, string startCommitId, string endCommitId, string relativePath)
    {
        var result = new List<GitDiffEntryInfo>();
        git_libgit2_init().Check();
        try
        {
            git_repository repo;
            git_repository_open(out repo, repoLocalPath).Check();
            try
            {
                var startCommit = LookupCommit(repo, startCommitId);
                var endCommit = LookupCommit(repo, endCommitId);
                try
                {
                    git_tree startTree;
                    git_tree endTree;
                    git_commit_tree(out startTree, startCommit).Check();
                    git_commit_tree(out endTree, endCommit).Check();
                    try
                    {
                        git_diff diff;
                        git_diff_options options;
                        git_diff_options_init(out options, GIT_DIFF_OPTIONS_VERSION).Check();
                        git_diff_tree_to_tree(out diff, repo, startTree, endTree, options).Check();
                        try
                        {
                            AppendDiffEntryInfo(result, diff, relativePath);
                        }
                        finally
                        {
                            git_diff_free(diff);
                        }
                    }
                    finally
                    {
                        git_tree_free(startTree);
                        git_tree_free(endTree);
                    }
                }
                finally
                {
                    git_commit_free(startCommit);
                    git_commit_free(endCommit);
                }
            }
            finally
            {
                git_repository_free(repo);
            }
        }
        finally
        {
            git_libgit2_shutdown();
        }

        return result;
    }

    private static void MergeGitDiffToCht(IReadOnlyList<GitPatchEntry> diffs, string repoLocalPath, string baseSuffix, string targetSuffix, MergeGitDiffParams pfParams)
    {
        if (diffs.Count == 0) return;

        var evtFormats = AssConstants.EventFormatV4P.Split(',').Select(s => s.Trim()).ToArray();

        foreach (var diff in diffs)
        {
            if (diff.Status != git_delta_t.GIT_DELTA_MODIFIED)
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

            var addLinesCopy = new List<GitLine>(diff.AddedLines);
            var deleteLinesCopy = new List<GitLine>(diff.DeletedLines);

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

    private static IReadOnlyList<GitPatchEntry> GetDirectoryPatch(string repoLocalPath, string startCommitId, string endCommitId, string relativePath)
    {
        var result = new List<GitPatchEntry>();
        git_libgit2_init().Check();
        try
        {
            git_repository repo;
            git_repository_open(out repo, repoLocalPath).Check();
            try
            {
                var startCommit = LookupCommit(repo, startCommitId);
                var endCommit = LookupCommit(repo, endCommitId);
                try
                {
                    git_tree startTree;
                    git_tree endTree;
                    git_commit_tree(out startTree, startCommit).Check();
                    git_commit_tree(out endTree, endCommit).Check();
                    try
                    {
                        git_diff diff;
                        git_diff_options options;
                        git_diff_options_init(out options, GIT_DIFF_OPTIONS_VERSION).Check();
                        git_diff_tree_to_tree(out diff, repo, startTree, endTree, options).Check();
                        try
                        {
                            AppendDiffEntries(result, diff, relativePath);
                        }
                        finally
                        {
                            git_diff_free(diff);
                        }
                    }
                    finally
                    {
                        git_tree_free(startTree);
                        git_tree_free(endTree);
                    }
                }
                finally
                {
                    git_commit_free(startCommit);
                    git_commit_free(endCommit);
                }
            }
            finally
            {
                git_repository_free(repo);
            }
        }
        finally
        {
            git_libgit2_shutdown();
        }

        return result;
    }

    private static IEnumerable<string> MergeGitDiff(StreamReader sr, List<GitLine> deleteLines, List<GitLine> addLines, MergeGitDiffParams pfParams, string[]? formats)
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
            var content = addLine.Content;
            if (content is not null)
            {
                yield return content.TrimEnd('\n');
            }
        }
    }

    private static void GitEventLineConvertToCht(ReadOnlySpan<char> sp, ConvertSimplifiedChinese evtConverter, StringBuilder sb, int lineNum, string[] formats)
    {
        if (!TryParseEventLine(sp, lineNum, formats, out var evt))
        {
            sb.Append(sp.TrimEnd('\n'));
            return;
        }

        evtConverter.ZhConvertEventByOpenccSharp(ref evt);
        using var writer = new StringWriter(sb);
        Mobsub.SubtitleParse.AssTypes.Helper.Write(writer, evt, formats, false);
    }

    private static bool TryParseEventLine(ReadOnlySpan<char> sp, int lineNum, string[] formats, out AssEvent evt)
    {
        var trimmed = sp.TrimEnd("\r\n".AsSpan());
        var sepIndex = trimmed.IndexOf(':');
        if (sepIndex <= 0)
        {
            evt = default;
            return false;
        }

        var header = trimmed[..sepIndex].Trim();
        if (!header.Equals("Dialogue", StringComparison.OrdinalIgnoreCase) && !header.Equals("Comment", StringComparison.OrdinalIgnoreCase))
        {
            evt = default;
            return false;
        }

        var lineBytes = Encoding.UTF8.GetBytes(trimmed.ToString());
        evt = new AssEvent(lineBytes, lineNum - 1, lineBytes.AsSpan(0, sepIndex), formats);
        return true;
    }

    private static bool IsEventLine(ReadOnlySpan<char> span)
    {
        var trimmed = span.TrimStart();
        return trimmed.StartsWith("Dialogue:".AsSpan(), StringComparison.OrdinalIgnoreCase)
               || trimmed.StartsWith("Comment:".AsSpan(), StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareLinesNumber(string file1, string file2)
    {
        var fl1 = File.ReadLines(file1).Count();
        var fl2 = File.ReadLines(file2).Count();
        return fl2 - fl1;
    }
    private static string GetAddLine(List<GitLine> addLines, int currentLineNumber, ref int offset, MergeGitDiffParams pfParams, string[]? formats, out bool add)
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
                    var content = line.Content!;
                    var span = content.AsSpan();
                    if (evtConverter is not null && IsEventLine(span))
                    {
                        GitEventLineConvertToCht(span, evtConverter, sb, lineNum, formats!);
                    }
                    else
                    {
                        sb.Append(content.TrimEnd('\n'));
                    }

                    offset++;
                    offsetCount++;
                }
            }
        }
        if (add) { addLines.RemoveRange(startIndex, offsetCount); }

        return sb.ToString();
    }

    private static bool CommitTouchesPath(git_commit commit, string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            return true;
        }

        git_tree currentTree;
        git_commit_tree(out currentTree, commit).Check();
        git_tree_entry currentEntry = default;
        var hasCurrent = false;
        try
        {
            var currentEntryResult = git_tree_entry_bypath(out currentEntry, currentTree, relativePath);
            if (currentEntryResult == (int)git_error_code.GIT_ENOTFOUND)
            {
                hasCurrent = false;
            }
            else
            {
                if (currentEntryResult != 0)
                {
                    throw new LibGit2Exception((git_error_code)currentEntryResult, "git_tree_entry_bypath failed.");
                }
                hasCurrent = true;
            }

            var parentCount = git_commit_parentcount(commit);
            if (parentCount == UIntPtr.Zero)
            {
                return hasCurrent;
            }

            git_commit parentCommit;
            git_commit_parent(out parentCommit, commit, 0).Check();
            try
            {
                git_tree parentTree;
                git_commit_tree(out parentTree, parentCommit).Check();
                git_tree_entry parentEntry = default;
                var hasParent = false;
                try
                {
                    var parentEntryResult = git_tree_entry_bypath(out parentEntry, parentTree, relativePath);
                    if (parentEntryResult == (int)git_error_code.GIT_ENOTFOUND)
                    {
                        hasParent = false;
                    }
                    else
                    {
                        if (parentEntryResult != 0)
                        {
                            throw new LibGit2Exception((git_error_code)parentEntryResult, "git_tree_entry_bypath failed.");
                        }
                        hasParent = true;
                    }

                    if (hasCurrent && hasParent)
                    {
                        unsafe
                        {
                            var currentId = git_tree_entry_id(currentEntry);
                            var parentId = git_tree_entry_id(parentEntry);
                            if (currentId == null || parentId == null)
                            {
                                return true;
                            }
                            return git_oid_equal(in *currentId, in *parentId) == 0;
                        }
                    }

                    return hasCurrent || hasParent;
                }
                finally
                {
                    if (hasParent)
                    {
                        git_tree_entry_free(parentEntry);
                    }
                    git_tree_free(parentTree);
                }
            }
            finally
            {
                git_commit_free(parentCommit);
            }
        }
        finally
        {
            if (hasCurrent)
            {
                git_tree_entry_free(currentEntry);
            }
            git_tree_free(currentTree);
        }
    }

    private static unsafe GitCommitInfo ReadCommitInfo(git_commit commit, in git_oid oid)
    {
        var summary = git_commit_summary(commit);
        var committer = git_commit_committer(commit);
        var committerName = committer == null ? string.Empty : Marshal.PtrToStringUTF8((nint)committer->name) ?? string.Empty;
        var when = DateTimeOffset.FromUnixTimeSeconds(git_commit_time(commit))
            .ToOffset(TimeSpan.FromMinutes(git_commit_time_offset(commit)));
        return new GitCommitInfo(FormatOid(in oid), summary ?? string.Empty, committerName, when);
    }

    private static unsafe string FormatOid(in git_oid oid)
    {
        var oidPtr = git_oid_tostr_s(in oid);
        if (oidPtr == null)
        {
            return string.Empty;
        }
        return Marshal.PtrToStringUTF8((nint)oidPtr) ?? string.Empty;
    }

    private static unsafe void AppendDiffEntryInfo(List<GitDiffEntryInfo> entries, git_diff diff, string relativePath)
    {
        var normalizedRelativePath = NormalizeRelativePath(relativePath);
        var deltaCount = ToUIntPtr(git_diff_num_deltas(diff));

        for (nuint i = 0; i < deltaCount; i++)
        {
            var deltaPtr = git_diff_get_delta(diff, (UIntPtr)i);
            if (deltaPtr == null)
            {
                continue;
            }
            var delta = *deltaPtr;
            var oldPath = delta.old_file.path_string ?? string.Empty;
            var newPath = delta.new_file.path_string ?? string.Empty;
            var path = string.IsNullOrEmpty(oldPath) ? newPath : oldPath;
            if (!IsUnderRelativePath(normalizedRelativePath, path))
            {
                continue;
            }
            entries.Add(new GitDiffEntryInfo(path, delta.status));
        }
    }

    private static unsafe void AppendDiffEntries(List<GitPatchEntry> entries, git_diff diff, string relativePath)
    {
        var normalizedRelativePath = NormalizeRelativePath(relativePath);
        var deltaCount = ToUIntPtr(git_diff_num_deltas(diff));

        for (nuint i = 0; i < deltaCount; i++)
        {
            git_patch patch;
            var patchResult = git_patch_from_diff(out patch, diff, (UIntPtr)i);
            if (patchResult.Failure || patch.Equals(default))
            {
                continue;
            }

            try
            {
                var deltaPtr = git_patch_get_delta(patch);
                if (deltaPtr == null)
                {
                    continue;
                }

                var delta = *deltaPtr;
                if (delta.status != git_delta_t.GIT_DELTA_MODIFIED)
                {
                    continue;
                }

                var path = delta.old_file.path_string;
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                if (!IsUnderRelativePath(normalizedRelativePath, path))
                {
                    continue;
                }

                var entry = new GitPatchEntry(path, delta.status);
                AppendPatchLines(entry, patch);
                entries.Add(entry);
            }
            finally
            {
                git_patch_free(patch);
            }
        }
    }

    private static void AppendPatchLines(GitPatchEntry entry, git_patch patch)
    {
        var hunkCount = ToUIntPtr(git_patch_num_hunks(patch));

        for (nuint hunkIndex = 0; hunkIndex < hunkCount; hunkIndex++)
        {
            var lineCount = ToUIntPtr(git_patch_num_lines_in_hunk(patch, (UIntPtr)hunkIndex));

            for (nuint lineIndex = 0; lineIndex < lineCount; lineIndex++)
            {
                unsafe
                {
                    git_diff_line* linePtr;
                    git_patch_get_line_in_hunk(out linePtr, patch, (UIntPtr)hunkIndex, (UIntPtr)lineIndex).Check();
                    var line = *linePtr;

                    switch ((git_diff_line_t)line.origin)
                    {
                        case git_diff_line_t.GIT_DIFF_LINE_ADDITION:
                        case git_diff_line_t.GIT_DIFF_LINE_ADD_EOFNL:
                            entry.AddedLines.Add(new GitLine((int)line.new_lineno, line.content_string));
                            break;
                        case git_diff_line_t.GIT_DIFF_LINE_DELETION:
                        case git_diff_line_t.GIT_DIFF_LINE_DEL_EOFNL:
                            entry.DeletedLines.Add(new GitLine((int)line.old_lineno, line.content_string));
                            break;
                    }
                }
            }
        }
    }

    private static unsafe git_commit LookupCommit(git_repository repo, string commitId)
    {
        git_object obj;
        git_revparse_single(out obj, repo, commitId).Check();
        try
        {
            git_object peeled;
            git_object_peel(out peeled, obj, git_object_t.GIT_OBJECT_COMMIT).Check();
            try
            {
                var oidPtr = git_object_id(peeled);
                git_commit commit;
                git_commit_lookup(out commit, repo, in *oidPtr).Check();
                return commit;
            }
            finally
            {
                git_object_free(peeled);
            }
        }
        finally
        {
            git_object_free(obj);
        }
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            return string.Empty;
        }

        var normalized = relativePath.Replace('\\', '/');
        return normalized == "." ? string.Empty : normalized;
    }

    private static bool IsUnderRelativePath(string relativePath, string path)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            return true;
        }

        var normalizedPath = path.Replace('\\', '/');
        if (!normalizedPath.StartsWith(relativePath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return normalizedPath.Length == relativePath.Length || normalizedPath[relativePath.Length] == '/';
    }

    private static nuint ToUIntPtr(UIntPtr value) => (nuint)value;
    private static nuint ToUIntPtr(int value) => (nuint)value;


    public static bool FindGitRootDirectory(string startPath, out string rootPath, out string relativePath)
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
