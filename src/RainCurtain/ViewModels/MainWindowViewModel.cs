using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualBasic.FileIO;
using Mobsub.Helper.Avalonia.Services;
using Mobsub.RainCurtain.ViewModels.Converter;
using Mobsub.SubtitleParse;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.RainCurtain.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private AssData? assData;
    
    public ObservableCollection<AssEvent> AssEvents { get; set; }
    [ObservableProperty] private bool _layerIsVisible;
    [ObservableProperty] private bool _actorNameIsVisible;
    [ObservableProperty] private bool _marginLIsVisible;
    [ObservableProperty] private bool _marginRIsVisible;
    [ObservableProperty] private bool _marginVIsVisible;
    [ObservableProperty] private bool _effectIsVisible;
    
    [ObservableProperty] private AssEvent? _selectedEvent;
    
    public MainWindowViewModel()
    {
        // var events = new List<AssEvent> 
        // {
        //     new AssEvent(),
        //     new AssEvent()
        //     {
        //         LineNumber = 1,
        //         IsDialogue = false,
        //         Start = new AssTime(2000),
        //         End = new AssTime(1000)
        //     },
        // };
        AssEvents = [];
    }

    [RelayCommand]
    private async Task OpenFile(CancellationToken token)
    {
        try
        {
            var filesService = App.Current?.Services?.GetService<IFilesService>();
            if (filesService is null) throw new NullReferenceException("Missing File Service instance.");

            var file = await filesService.OpenFileAsync();
            if (file is null) return;
            
            var localPath = file.TryGetLocalPath();
            if (localPath is null) return;
            await LoadDataFromFile(localPath);
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            throw;
        }
    }

    private async Task LoadDataFromFile(string fileName)
    {
        var ext = System.IO.Path.GetExtension(fileName);
        await Task.Run(() =>
        {
            if (ext == ".ass")
            {
                assData = new AssData();
                assData.ReadAssFile(fileName);
            }
        });

        if (ext == ".ass")
        {
            CreateAssGrid();
            OnPropertyChanged(nameof(AssEvents));
        }
    }

    private void CreateAssGrid()
    {
        if (assData is null) return;
        
        var events = assData.Events.Collection;
        AssEvents.Clear();
        AssEvents = new ObservableCollection<AssEvent>(events);
    
        LayerIsVisible = events.Any(e => e.Layer != 0);
        ActorNameIsVisible = events.Any(e => !string.IsNullOrEmpty(e.Name));
        MarginLIsVisible = events.Any(e => e.MarginL != 0);
        MarginRIsVisible = events.Any(e => e.MarginR != 0);
        MarginVIsVisible = events.Any(e => e.MarginV != 0);
        EffectIsVisible = events.Any(e => !string.IsNullOrEmpty(e.Effect));
    }

    [RelayCommand]
    private async Task CommentSelected(IList selectedItems, CancellationToken token)
    {
        if (SelectedEvent is null) { return; }
        try
        {
            var evts = selectedItems.Cast<AssEvent>().ToList();
            var isComment = !SelectedEvent.IsDialogue;
            await Task.Run(() =>
            {
                foreach (var evt in evts)
                {
                    evt.IsDialogue = !isComment;
                }
            }, token);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
    
    private async Task<IStorageFile?> DoOpenFilePickerAsync()
    {
        throw new NotImplementedException();
    }
}