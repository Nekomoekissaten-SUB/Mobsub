using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.RainCurtain.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public ObservableCollection<AssEvent> AssEvents { get; }
    public bool LayerIsVisible { get; set; }
    
    public MainWindowViewModel()
    {
        var events = new List<AssEvent> 
        {
            new AssEvent(),
            new AssEvent()
            {
                IsDialogue = false,
                End = new AssTime(1000)
            },
        };
        AssEvents = new ObservableCollection<AssEvent>(events);

        LayerIsVisible = events.Any(e => e.Layer != 0);
    }
}