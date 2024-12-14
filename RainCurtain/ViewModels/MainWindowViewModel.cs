using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.Shapes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualBasic.FileIO;
using Mobsub.RainCurtain.Services;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.RainCurtain.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private AssData? assData;
    
    public ObservableCollection<AssEvent> AssEvents { get; set; }
    public bool LayerIsVisible { get; set; }
    public bool ActorNameIsVisible { get; set; }
    public bool MarginLIsVisible { get; set; }
    public bool MarginRIsVisible { get; set; }
    public bool MarginVIsVisible { get; set; }
    public bool EffectIsVisible { get; set; }
    
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
            OnPropertyChanged(nameof(AssEvents));
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            throw;
        }
    }

    private async Task LoadDataFromFile(string fileName)
    {
        await Task.Run(() =>
        {
            var ext = System.IO.Path.GetExtension(fileName);
            if (ext == ".ass")
            {
                assData = new AssData();
                assData.ReadAssFile(fileName);
            }
        });
        
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

    private async Task<IStorageFile?> DoOpenFilePickerAsync()
    {
        throw new NotImplementedException();
    }
}