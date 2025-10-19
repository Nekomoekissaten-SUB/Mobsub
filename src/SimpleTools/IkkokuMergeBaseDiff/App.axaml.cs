using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Mobsub.IkkokuMergeBaseDiff.Views;
using Microsoft.Extensions.DependencyInjection;
using Mobsub.Helper.Avalonia.Services;
using Mobsub.IkkokuMergeBaseDiff.ViewModels;

namespace Mobsub.IkkokuMergeBaseDiff;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Line below is needed to remove Avalonia data validation.
            // Without this line you will get duplicate validations from both Avalonia and CT
            BindingPlugins.DataValidators.RemoveAt(0);
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
            
            var services = new ServiceCollection();
            services.AddSingleton<IFilesService>(x => new FilesService(desktop.MainWindow));
            Services = services.BuildServiceProvider();
        }

        base.OnFrameworkInitializationCompleted();
    }
    
    public new static App? Current => Application.Current as App;
    public IServiceProvider? Services { get; private set; }
}