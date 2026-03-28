using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;


namespace Firefly;

public partial class App : Application
{

    public static new App Current => (App)Application.Current;


    private System.Timers.Timer _gcTimer;


    private MainWindow? _window;


    private TrayWindow _trayWindow;


    public App()
    {
        InitializeComponent();
        _gcTimer = new(TimeSpan.FromSeconds(60));
        _gcTimer.Elapsed += (_, _) => GC.Collect();
        _gcTimer.Start();
    }



    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var instance = AppInstance.FindOrRegisterForKey("Firefly");
        if (!instance.IsCurrent)
        {
            await instance.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
            Environment.Exit(0);
        }
        else
        {
            instance.Activated += AppInstanceInstance_Activated;
        }
        _window = new();
        _window.Activate();
        _trayWindow = new();
    }


    public void ShowMainWindow()
    {
        _window ??= new();
        _window.Show();
    }


    private void AppInstanceInstance_Activated(object? sender, AppActivationArguments e)
    {
        _window?.DispatcherQueue.TryEnqueue(ShowMainWindow);
    }


}
