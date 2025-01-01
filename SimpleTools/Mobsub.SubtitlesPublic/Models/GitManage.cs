using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibGit2Sharp;
using Mobsub.SubtitleProcess;
using SharpSevenZip;

namespace Mobsub.SubtitlesPublic.Models;

public class GitManage(BaseConfig config)
{
    private const string privateRepoUrl = "https://github.com/Nekomoekissaten-SUB/sub_backup";
    private const string publicRepoUrl = "https://github.com/Nekomoekissaten-SUB/Nekomoekissaten-Storage";
    private const string publicDownloadUrlPrefix = "https://github.com/Nekomoekissaten-SUB/Nekomoekissaten-Storage/releases/download/subtitle_pkg";
    private const string publicDownloadEffectUrlPrefix = "https://github.com/Nekomoekissaten-SUB/Nekomoekissaten-Storage/releases/download/subtitle_effect";
    private readonly string privateReadme = Path.Combine(config.PrivateRepoPath, "README.md");

    internal ProjectInfo? project;
    
    internal string[] GetPrivateDirectories()
    {
        var directories = Directory.GetDirectories(config.PrivateRepoPath);
        var dirs = directories.Select(dir => Path.GetRelativePath(config.PrivateRepoPath, dir))
            .Where(relativePath => !relativePath.StartsWith('.') && !relativePath.StartsWith('_')).ToArray();
        return dirs;
    }

    public async Task Execute(string relativePath, string jpnName)
    {
        project = new ProjectInfo()
        {
            DirectoryName = relativePath,
            DirectoryPath = Path.Combine(config.PrivateRepoPath, relativePath),
            ProjectNameJpn = jpnName,
        };
        
        // loop all files and dirs
        var files = Directory.GetFiles(project.DirectoryPath);
        var assFiles = new List<string>();
        var pkgFiles = new List<string>();
        List<string> otherFiles = [Path.Combine(project.DirectoryPath, "tmp")];
        foreach (var f in files)
        {
            var ext = Path.GetExtension(f);
            switch (ext)
            {
                case ".ass":
                    SetLanguageAndEpisode(Path.GetFileName(f));
                    assFiles.Add(f);
                    break;
                case ".7z" when
                    ((f.Contains(project.DirectoryName) || f.Contains(project.ProjectNameEng!)) && !f.Contains("Font", StringComparison.OrdinalIgnoreCase)):
                    pkgFiles.Add(f);
                    break;
                case ".yml" or ".yaml":
                    project.MergeConfigure = f;
                    break;
                default:
                    otherFiles.Add(f);
                    break;
            }
        }

        otherFiles.AddRange(Directory.GetDirectories(project.DirectoryPath).Where(d => d.Contains("Font", StringComparison.OrdinalIgnoreCase)));

        // package ass files and move package
        project.PackageName = $"{project.DirectoryName}_{(project.TimelineType == TimelineType.BluRay ? "BD" : "Web")}_{(project.Language == SubtitlesLanguage.Dual ? "JPCH" : "zho")}.7z";
        if (project.MergeConfigure is not null)
            project.PackageEffectName = $"{project.DirectoryName}_Effect.7z";
        
        var packageFiles = GetPackageFiles(assFiles, out var effectFiles)!;
        if (pkgFiles.Count == 0)
        {
            _ = await CompressAssFiles(packageFiles, project.PackageName!);
        }
        else
        {
            File.Move(pkgFiles[0], Path.Combine(config.TempUploadPath, project.PackageName));
        }

        if (effectFiles is not null)
        {
            _ = await CompressAssFiles(effectFiles, project.PackageEffectName!);
        }
        
        // move subs to standalone dir
        var subsDir = Path.Combine(project.DirectoryPath!, "Subs");
        if (!Directory.Exists(subsDir))
            Directory.CreateDirectory(subsDir);
        foreach (var f in assFiles)
        {
            if (effectFiles is not null)
            {
                if (effectFiles.Contains(f))
                {
                    continue;
                }
            }
            File.Move(f, Path.Combine(subsDir, Path.GetFileName(f)));
        }
        if (project.MergeConfigure != null)
            File.Move(project.MergeConfigure, Path.Combine(subsDir, Path.GetFileName(project.MergeConfigure)));
        await GetUsedFonts(subsDir);

        // remove unused files
        foreach (var f in otherFiles)
        {
            if (File.Exists(f))
            {
                File.Delete(f);
                continue;
            }

            if (Directory.Exists(f))
            {
                Directory.Delete(f, true);
            }
        }
        
        // update private readme
        _ = AppendPrivateReadme(jpnName);
        
        // create project readme
        var readmePath = Path.Combine(project.DirectoryPath, "README.md");
        await File.WriteAllTextAsync(readmePath, GenerateNewReadmeMarkdown());
        
        // move to public
        Console.WriteLine($"{project.ProjectNameEng}({project.Episodes}, {project.Language}, {project.TimelineType}): {publicRepoUrl}/tree/master/{relativePath}");
        // var publicProjectDir = Path.Combine(config.PublicRepoPath, relativePath);
        // Directory.Move(project.DirectoryPath, publicProjectDir);
    }

    private void SetLanguageAndEpisode(ReadOnlySpan<char> span)
    {
        if (project is null)
            return;
        
        if (project.Language is null)
        {
            var ext = GetLanguageFromFileName(span);
            if (ext.SequenceEqual("JPSC".AsSpan()) || ext.SequenceEqual("JPTC".AsSpan()))
            {
                project.Language = SubtitlesLanguage.Dual;
            }
            else
            {
                project.Language = SubtitlesLanguage.Single;
            }
        }

        if (project.TimelineType is null)
        {
            var p1 = span.LastIndexOf('[');
            var p2 = span.LastIndexOf(']');
            if (p1 > 0 && p2 > 0)
            {
                var s = span[(p1 + 1)..p2];
                project.TimelineType = s.StartsWith("BD") ? TimelineType.BluRay : TimelineType.Web;
            }
        }

        if (project.ProjectNameEng is null)
        {
            var p1 = span.IndexOf(']');
            var p2 = FindIndex(span, '[', 2);
            if (p1 > 0 && p2 > 0)
            {
                project.ProjectNameEng = span[(p1 + 2)..(p2 - 1)].ToString();
            }
        }
        
        var epStr = GetEpisodeFromFileName(span);
        if (epStr.Length == 0) { return; }
        if (int.TryParse(epStr, out var ep))
        {
            if (ep > project.Episodes && project.Episodes > -1)
            {
                project.Episodes = ep;
            }
        }
        else
        {
            if (float.TryParse(epStr, out _))
            {
                
            }
            else if (epStr.IndexOf('-') > 0 || epStr.StartsWith("MOV", StringComparison.OrdinalIgnoreCase))
            {
                project.Episodes = -1;
            }
        }
    }

    private static ReadOnlySpan<char> GetLanguageFromFileName(ReadOnlySpan<char> span)
    {
        return Path.GetExtension(Path.GetFileNameWithoutExtension(span))[1..];
    }

    private static ReadOnlySpan<char> GetEpisodeFromFileName(ReadOnlySpan<char> span)
    {
        var p1 = FindIndex(span, '[', 2);
        var p2 = FindIndex(span, ']', 2);

        if (p1 == -1 || p2 == -1)
        {
            return Span<char>.Empty;
        }

        return span[(p1 + 1)..p2];
    }
    
    private static int FindIndex(ReadOnlySpan<char> span, char c, int count)
    {
        Debug.WriteLine(span.ToString());
        var pos = 0;
        var i = 0;
        while (i < count)
        {
            var p = span[pos..].IndexOf(c);
            if (p == -1)
            {
                if (i == 0)
                {
                    return -1;
                }
            }
            else
            {
                pos += p + 1;
            }
            i++;
        }
        Debug.WriteLine($"{pos}: {span[(pos - 1)..]}");
        return pos - 1;
    }
    
    internal string? GetLatestCommitsUri(string relativePath)
    {
        using var repo = new Repository(config.PrivateRepoPath);
        
        var branch = repo.Branches["master"];
        if (branch == null)
            return null;
        
        var latestCommit = branch.Commits.FirstOrDefault(
            commit => commit.Parents.Select(
                    parent => repo.Diff.Compare<TreeChanges>(parent.Tree, commit.Tree))
            .Any(changes => changes.Any(change => change.Path.StartsWith(relativePath, StringComparison.OrdinalIgnoreCase))));

        return latestCommit == null ? null : $"{privateRepoUrl}/commits/{latestCommit.Sha}/{relativePath}";
    }

    internal async Task<string?> CompressAssFiles(string[] files, string target)
    {
        if (project is null)
            return null;
        
        var compressor = new SharpSevenZipCompressor()
        {
            ArchiveFormat = OutArchiveFormat.SevenZip,
            CompressionLevel = CompressionLevel.High,
            CompressionMethod = CompressionMethod.Lzma2,
            CompressionMode = CompressionMode.Create,
            DirectoryStructure = false,
            FastCompression = true,
            IncludeEmptyDirectories = false,
        };

        var pkg = Path.Combine(config.TempUploadPath, target);
        await compressor.CompressFilesAsync(pkg, files);
        return pkg;
    }

    internal async Task GetUsedFonts(string subsDir)
    {
        var lafCmdInfo = new ProcessStartInfo(config.ListAssFontsBinaryPath);
        lafCmdInfo.ArgumentList.Add(subsDir);
        lafCmdInfo.ArgumentList.Add("-Exit");
        lafCmdInfo.ArgumentList.Add("-SaveText");
        var lafCmd = Process.Start(lafCmdInfo);
        var genUsedFontsFile = Path.Combine(subsDir, "fonts.txt");
        while (!File.Exists(genUsedFontsFile))
        {
            await Task.Delay(1000);
        }
        var fonts = await File.ReadAllTextAsync(genUsedFontsFile);
        project!.UsedFonts = fonts.TrimEnd();
        File.Delete(genUsedFontsFile);
    }

    private string[]? GetPackageFiles(List<string> assFiles, out string[]? effectFiles)
    {
        effectFiles = null;
        if (project is null)
            return null;
        if (project.MergeConfigure is null)
            return assFiles.ToArray();

        HashSet<string> langs = [];
        HashSet<string> eps = [];
        List<string> effectFileList = [];
        foreach (var f in assFiles)
        {
            var name = Path.GetFileName(f);
            var ep = GetEpisodeFromFileName(name);
            if (ep.Length == 0)
            {
                effectFileList.Add(f);
                continue;
            }

            eps.Add(ep.ToString());
            langs.Add(GetLanguageFromFileName(name).ToString());
        }
        
        var mergebase = new MergeByConfig(project.MergeConfigure);
        var baseDir = Path.GetDirectoryName(assFiles[0]);
        var optPath = Path.Combine(baseDir!, "tmp");
        if (Path.Exists(optPath))
        {
            Directory.Delete(optPath, true);
            Directory.CreateDirectory(optPath);
        }

        var targetFiles = (
            from ep in eps
            from lang in langs
            select mergebase.Merge(ep, lang, baseDir!, optPath, "all")
            ).ToArray();

        if (effectFileList.Count > 0)
            effectFiles = effectFileList.ToArray();
        
        return targetFiles;
    }
    
    public bool AppendPrivateReadme(string jpnName)
    {
        if (project is null)
            return false;
        var commitsUri = GetLatestCommitsUri(project.DirectoryName!);
        if (commitsUri == null)
            return false;
        var text = $"{jpnName} | {project.ProjectNameEng} | [commits]({commitsUri})";
        
        using var fs = new FileStream(privateReadme, FileMode.Open, FileAccess.ReadWrite);
        fs.Seek(-1, SeekOrigin.End);
        var c = (char)fs.ReadByte();
        var isLastLineEmpty = c == '\n';
        using var writer = new StreamWriter(fs);
        if (!isLastLineEmpty)
        {
            writer.WriteLine();
        }
        writer.WriteLine(text);
        return true;
    }

    internal string? GenerateNewReadmeMarkdown()
    {
        if (project is null)
            return null;

        var sb = new StringBuilder();
        sb.Append(@$"# {project.ProjectNameJpn}

内含 {(project.Episodes > 1 ? $"01-{project.Episodes}" : "01")} {(project.Language == SubtitlesLanguage.Dual ? "简繁日双语" : "简繁中文")} {(project.TimelineType == TimelineType.BluRay ? "BDRip" : "Web")}{(project.MergeConfigure is null ? " " : $"（含[特效]({publicDownloadEffectUrlPrefix}/{project.PackageEffectName})）")}字幕  
[字幕合集]({publicDownloadUrlPrefix}/{project.PackageName})

所需字体：
```
{project.UsedFonts}
```
");
        return sb.ToString();
    }
}