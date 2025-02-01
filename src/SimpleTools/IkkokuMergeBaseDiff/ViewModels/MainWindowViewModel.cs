using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibGit2Sharp;
using Microsoft.Extensions.DependencyInjection;
using Mobsub.Helper.Avalonia.Services;
using Mobsub.SubtitleProcessNotAot;

namespace Mobsub.IkkokuMergeBaseDiff.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private string _workDirectory = string.Empty;
    [ObservableProperty] private string _gitRepositoryPath = string.Empty;
    [ObservableProperty] private string _zhConvertConfig = string.Empty;
    
    private Repository? _repository;
    private string _workDirectoryRelativePath = string.Empty;
    private List<Commit> _workDirectoryCommits = [];
    public ObservableCollection<Commit> StartCommits { get; private set; } = [];
    public ObservableCollection<Commit> EndCommits { get; private set; } = [];
    [ObservableProperty] private Commit? _startCommit;
    [ObservableProperty] private Commit? _endCommit;

    [RelayCommand]
    private async Task SelectWorkDirectory(CancellationToken token)
    {
        var filesService = App.Current?.Services?.GetService<IFilesService>();
        if (filesService is null) throw new NullReferenceException("Missing File Service instance.");

        var file = await filesService.SelectFolderAsync();

        var localPath = file?.TryGetLocalPath();
        if (localPath is null) return;
        WorkDirectory = localPath;
            
        MergeSimplifiedChineseGitDiff.FindGitRootDirectory(localPath, out var rootPath, out var relativePath);
        GitRepositoryPath = rootPath;
        _repository = new Repository(GitRepositoryPath);
        _workDirectoryRelativePath = relativePath;
        _workDirectoryCommits = await GetWorkDirectoryCommits(_repository, relativePath);
            
        StartCommits = new ObservableCollection<Commit>(_workDirectoryCommits);
        EndCommits = new ObservableCollection<Commit>(_workDirectoryCommits);
        
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
        if (_repository is null || StartCommit is null || EndCommit is null) return;
        
        await Task.Run(() =>
        {
            var diffs = MergeSimplifiedChineseGitDiff.GetDirectoryPatch(_repository, StartCommit.Id.Sha, EndCommit.Id.Sha, _workDirectoryRelativePath);
        }, token);
    }

    [RelayCommand]
    private async Task MergePatch(CancellationToken token)
    {
        if (ZhConvertConfig == string.Empty) return;
        if (_repository is null || StartCommit is null || EndCommit is null) return;
        
        await Task.Run(() =>
        {
           var pfParams = MergeSimplifiedChineseGitDiff.GetMergeGitDiffParams(ZhConvertConfig, true);
           var diffs = MergeSimplifiedChineseGitDiff.GetDirectoryPatch(_repository, StartCommit.Id.Sha,
               EndCommit.Id.Sha, _workDirectoryRelativePath);

           var baseSuffix = ".SC.";
           foreach (var diff in diffs)
           {
               if (diff.OldPath.Contains(".JPSC."))
               {
                   baseSuffix = ".JPSC.";
                   break;
               }
           }

           var targetSuffix = baseSuffix == ".SC." ? ".TC." : ".JPTC.";
           
           MergeSimplifiedChineseGitDiff.MergeGitDiffToCht(diffs, GitRepositoryPath, baseSuffix, targetSuffix, pfParams);
        });
    }
    
    private static async Task<List<Commit>> GetWorkDirectoryCommits(Repository repo, string relativePath)
    {
        var commits = new List<Commit>();
        await Task.Run(() =>
        {
            foreach (var commit in repo.Commits)
            {
                var currentTree = commit.Tree;
                var parentTree = commit.Parents.Any() ? commit.Parents.First().Tree : null;

                var currentFolderEntry = currentTree[relativePath];

                if (parentTree != null)
                {
                    var parentFolderEntry = parentTree[relativePath];

                    if (currentFolderEntry != null && parentFolderEntry != null)
                    {
                        if (!currentFolderEntry.Target.Sha.Equals(parentFolderEntry.Target.Sha))
                        {
                            commits.Add(commit);
                        }
                    }
                    else if (currentFolderEntry != null || parentFolderEntry != null)
                    {
                        commits.Add(commit);
                    }
                }
                else if (currentFolderEntry != null)
                {
                    commits.Add(commit);
                }
            }
        });
        return commits;
    }
}