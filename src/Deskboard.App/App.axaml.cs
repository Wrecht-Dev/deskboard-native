using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Deskboard.App.Services;
using Deskboard.App.ViewModels;
using Deskboard.App.Views;
using Deskboard.App.Views;

namespace Deskboard.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        RequestedThemeVariant = AppSettings.Theme == "light" ? ThemeVariant.Light : ThemeVariant.Dark;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainView = new MainViewModel();
            desktop.MainWindow = new MainWindow { DataContext = mainView };
            if (!mainView.Client.HasSession && !mainView.DemoMode)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                {
                    var login = new LoginViewModel(mainView.Client);
                    var window = new Views.LoginWindow { DataContext = login };
                    await window.ShowDialog(desktop.MainWindow);
                    if (mainView.Client.HasSession) await mainView.InitializeAsync();
                });
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}
