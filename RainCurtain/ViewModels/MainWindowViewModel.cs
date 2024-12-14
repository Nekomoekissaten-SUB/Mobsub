using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.RainCurtain.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public ObservableCollection<AssEvent> AssEvents { get; }
    public bool LayerIsVisible { get; set; }
    public bool ActorNameIsVisible { get; set; }
    public bool MarginLIsVisible { get; set; }
    public bool MarginRIsVisible { get; set; }
    public bool MarginVIsVisible { get; set; }
    public bool EffectIsVisible { get; set; }
    
    public MainWindowViewModel()
    {
        var events = new List<AssEvent> 
        {
            new AssEvent(),
            new AssEvent()
            {
                LineNumber = 1,
                IsDialogue = false,
                Start = new AssTime(2000),
                End = new AssTime(1000)
            },
        };
        AssEvents = new ObservableCollection<AssEvent>(events);

        LayerIsVisible = events.Any(e => e.Layer != 0);
        ActorNameIsVisible = events.Any(e => !string.IsNullOrEmpty(e.Name));
        MarginLIsVisible = events.Any(e => e.MarginL != 0);
        MarginRIsVisible = events.Any(e => e.MarginR != 0);
        MarginVIsVisible = events.Any(e => e.MarginV != 0);
        EffectIsVisible = events.Any(e => !string.IsNullOrEmpty(e.Effect));
    }
}