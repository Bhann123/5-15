using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FimmFimmtan.App.Views;

namespace FimmFimmtan.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            desktop.MainWindow = window;
            if (desktop.Args is [var path, ..] && File.Exists(path))
                _ = window.ViewModel.LoadFileAsync(path);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
