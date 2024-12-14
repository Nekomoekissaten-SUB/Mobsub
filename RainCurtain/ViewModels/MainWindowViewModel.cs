using System;
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
using Mobsub.RainCurtain.Services;
using Mobsub.RainCurtain.ViewModels.Converter;
using Mobsub.SubtitleParse;
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
        }
    }

    private void CreateAssGrid()
    {
        if (assData is null) return;
        
        var events = assData.Events.Collection;
        if (events.Count <= 0) return;
        AssEvents.Clear();
        AssEvents = new ObservableCollection<AssEvent>(events);
    
        LayerIsVisible = events.Any(e => e.Layer != 0);
        ActorNameIsVisible = events.Any(e => !string.IsNullOrEmpty(e.Name));
        MarginLIsVisible = events.Any(e => e.MarginL != 0);
        MarginRIsVisible = events.Any(e => e.MarginR != 0);
        MarginVIsVisible = events.Any(e => e.MarginV != 0);
        EffectIsVisible = events.Any(e => !string.IsNullOrEmpty(e.Effect));
    }
    
    // public FlatTreeDataGridSource<AssEvent> AssEventSource { get; set; }
    // private void CreateAssGrid()
    // {
    //     if (assData is null) return;
    //     
    //     var events = assData.Events.Collection;
    //     AssEvents.Clear();
    //     AssEvents = new ObservableCollection<AssEvent>(events);
    //
    //     LayerIsVisible = events.Any(e => e.Layer != 0);
    //     ActorNameIsVisible = events.Any(e => !string.IsNullOrEmpty(e.Name));
    //     MarginLIsVisible = events.Any(e => e.MarginL != 0);
    //     MarginRIsVisible = events.Any(e => e.MarginR != 0);
    //     MarginVIsVisible = events.Any(e => e.MarginV != 0);
    //     EffectIsVisible = events.Any(e => !string.IsNullOrEmpty(e.Effect));
    //     
    //     AssEventSource = new FlatTreeDataGridSource<AssEvent>(events);
    //     
    //     if (LayerIsVisible)
    //     {
    //         AssEventSource.Columns.Add(new TextColumn<AssEvent,int>("L", x => x.Layer));
    //     }
    //
    //     AssEventSource.Columns.Add(new TemplateColumn<AssEvent>("Start", "AssStartCell"));
    //     AssEventSource.Columns.Add(new TemplateColumn<AssEvent>("End", new FuncDataTemplate<AssEvent>((_, _) => new TextBlock()
    //     {
    //         [!TextBlock.TextProperty] = new Binding("End"){ Converter = new AssTimeConverter(), ConverterParameter = "ctsRounding" },
    //         [!TextBlock.BackgroundProperty] = new MultiBinding()
    //         {
    //             Bindings = new List<IBinding>()
    //             {
    //                 new Binding("Start"), new Binding("End")
    //             },
    //             Converter = new InvalidValueColorConverter()
    //         }
    //     })));
    //     AssEventSource.Columns.Add(new TemplateColumn<AssEvent>("End", "AssEndCell"));
    //     AssEventSource.Columns.Add(new TextColumn<AssEvent,string>("Style", x => x.Style));
    //     if (ActorNameIsVisible)
    //     {
    //         AssEventSource.Columns.Add(new TextColumn<AssEvent,string>("Actor", x => x.Name));
    //     }
    //     if (MarginLIsVisible)
    //     {
    //         AssEventSource.Columns.Add(new TextColumn<AssEvent,int>("MarginL", x => x.MarginL));
    //     }
    //     if (MarginRIsVisible)
    //     {
    //         AssEventSource.Columns.Add(new TextColumn<AssEvent,int>("MarginR", x => x.MarginR));
    //     }
    //     if (MarginVIsVisible)
    //     {
    //         AssEventSource.Columns.Add(new TextColumn<AssEvent,int>("MarginV", x => x.MarginV));
    //     }
    //     
    //     if (EffectIsVisible)
    //     {
    //         AssEventSource.Columns.Add(new TextColumn<AssEvent,string>("Effect", x => x.Effect));
    //     }
    //     
    //     AssEventSource.Columns.Add(new TextColumn<AssEvent,string>("Text", x => x.Text, width: GridLength.Star));
    // }

    private async Task<IStorageFile?> DoOpenFilePickerAsync()
    {
        throw new NotImplementedException();
    }
}