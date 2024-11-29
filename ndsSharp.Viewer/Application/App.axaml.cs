using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ndsSharp.FileExplorer.Services;

namespace ndsSharp.FileExplorer.Application;

public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ApplicationService.Application = desktop;
            ApplicationService.Initialize();
        }

        base.OnFrameworkInitializationCompleted();
    }
}