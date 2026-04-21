﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Mobsub.Helper.Avalonia.Services;
using Mobsub.SubtitleProcess;

namespace Mobsub.IkkokuMergeBaseDiff.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const int MaxCommitListCount = 500;

    [ObservableProperty] private string _workDirectory = string.Empty;
    [ObservableProperty] private string _gitRepositoryPath = string.Empty;
    [ObservableProperty] private string _zhConvertConfig = string.Empty;
    
    private string _workDirectoryRelativePath = string.Empty;
    private IReadOnlyList<MergeSimplifiedChineseGitDiff.GitCommitInfo> _workDirectoryCommits = Array.Empty<MergeSimplifiedChineseGitDiff.GitCommitInfo>();
    public ObservableCollection<MergeSimplifiedChineseGitDiff.GitCommitInfo> StartCommits { get; private set; } = [];
    public ObservableCollection<MergeSimplifiedChineseGitDiff.GitCommitInfo> EndCommits { get; private set; } = [];
    [ObservableProperty] private MergeSimplifiedChineseGitDiff.GitCommitInfo? _startCommit;
    [ObservableProperty] private MergeSimplifiedChineseGitDiff.GitCommitInfo? _endCommit;

    [RelayCommand]
    private async Task SelectWorkDirectory(CancellationToken token)
    {
        var filesService = App.Current?.Services?.GetService<IFilesService>();
        if (filesService is null) throw new NullReferenceException("Missing File Service instance.");

        var file = await filesService.SelectFolderAsync();

        var localPath = file?.TryGetLocalPath();
        if (localPath is null) return;
        WorkDirectory = localPath;
            
        if (!MergeSimplifiedChineseGitDiff.FindGitRootDirectory(localPath, out var rootPath, out var relativePath))
        {
            GitRepositoryPath = string.Empty;
            return;
        }

        GitRepositoryPath = rootPath;
        _workDirectoryRelativePath = relativePath;
        _workDirectoryCommits = await Task.Run(() =>
            MergeSimplifiedChineseGitDiff.GetWorkDirectoryCommits(GitRepositoryPath, relativePath, MaxCommitListCount, token), token);
            
        StartCommits = new ObservableCollection<MergeSimplifiedChineseGitDiff.GitCommitInfo>(_workDirectoryCommits);
        EndCommits = new ObservableCollection<MergeSimplifiedChineseGitDiff.GitCommitInfo>(_workDirectoryCommits);
        StartCommit = StartCommits.FirstOrDefault();
        EndCommit = EndCommits.FirstOrDefault();
        
        OnPropertyChanged(nameof(StartCommits));
        OnPropertyChanged(nameof(EndCommits));
    }

    [RelayCommand]
    private async Task SelectZhConvertConfig(CancellationToken token)
    {
        var filesService = App.Current?.Services?.GetService<IFilesService>();
        if (filesService is null) throw new NullReferenceException("Missing File Service instance.");

        var file = await filesService.OpenFileAsync();

        var localPath = file?.TryGetLocalPath();
        if (localPath is null) return;
        ZhConvertConfig = localPath;
    }
    
    [RelayCommand]
    private async Task DisplayPatchChanges(CancellationToken token)
    {
        if (StartCommit is null || EndCommit is null) return;
        
        await Task.Run(() =>
        {
            _ = BuildPatch();
        }, token);
    }

    [RelayCommand]
    private async Task MergePatch(CancellationToken token)
    {
        if (ZhConvertConfig == string.Empty) return;
        if (StartCommit is null || EndCommit is null) return;
        
        await Task.Run(() =>
        {
            var patch = BuildPatch();
            var baseSuffix = ".SC.";
            foreach (var diff in patch)
            {
                if (diff.Path.Contains(".JPSC.", StringComparison.OrdinalIgnoreCase))
                {
                    baseSuffix = ".JPSC.";
                    break;
                }
            }

            var targetSuffix = baseSuffix == ".SC." ? ".TC." : ".JPTC.";
            MergeSimplifiedChineseGitDiff.MergeGitDiffToCht(GitRepositoryPath, StartCommit.Sha,
                EndCommit.Sha, _workDirectoryRelativePath, baseSuffix, targetSuffix, ZhConvertConfig);
        });
    }

    private IReadOnlyList<MergeSimplifiedChineseGitDiff.GitDiffEntryInfo> BuildPatch()
    {
        if (StartCommit is null || EndCommit is null || string.IsNullOrEmpty(GitRepositoryPath))
        {
            return Array.Empty<MergeSimplifiedChineseGitDiff.GitDiffEntryInfo>();
        }

        return MergeSimplifiedChineseGitDiff.GetDirectoryDiffEntries(GitRepositoryPath, StartCommit.Sha, EndCommit.Sha, _workDirectoryRelativePath);
    }
}
