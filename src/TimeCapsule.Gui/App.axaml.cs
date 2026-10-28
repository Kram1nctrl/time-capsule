using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.Linq;

namespace TimeCapsule.Gui;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var main = new MainWindow();
            // Started by the login autostart entry: only the widget, no app window.
            var widgetOnly = desktop.Args?.Contains("--widget") == true && main.WidgetOn;
            if (!widgetOnly) desktop.MainWindow = main;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
