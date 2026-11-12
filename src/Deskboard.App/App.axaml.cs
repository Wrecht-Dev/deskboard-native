using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using Deskboard.App.Services;
using Deskboard.App.ViewModels;
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
            var demo = Environment.GetEnvironmentVariable("DESKBOARD_DEMO") == "1";
            var client = new Core.Api.TumblrClient();
            var window = new MainWindow();
            desktop.MainWindow = window;
            window.Show();

            void StartShell()
            {
                window.DataContext = new MainViewModel(client);
                window.Activate();
            }

            if (demo || client.HasSession)
            {
                StartShell();
            }
            else if (!FirstRun.WelcomeShown())
            {
                var welcome = new WelcomeWindow();
                welcome.Closed += (_, _) =>
                {
                    FirstRun.MarkWelcomeShown();
                    if (welcome.Choice == "browse")
                    {
                        client.AutoRefreshSession = false;
                        StartShell();
                        return;
                    }
                    if (welcome.Choice == "browser") client.RefreshSessionFromBrowser();
                    if (client.HasSession) { StartShell(); return; }

                    var login = new LoginViewModel(client);
                    var loginWindow = new LoginWindow { DataContext = login };
                    login.Completed += (_, ok) => StartShell();
                    loginWindow.Closed += (_, _) => { if (window.DataContext is null) StartShell(); };
                    loginWindow.Show();
                };
                welcome.Show();
            }
            else
            {
                var login = new LoginViewModel(client);
                var loginWindow = new LoginWindow { DataContext = login };
                login.Completed += (_, ok) => StartShell();
                loginWindow.Closed += (_, _) => { if (window.DataContext is null) StartShell(); };
                loginWindow.Show();
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}
