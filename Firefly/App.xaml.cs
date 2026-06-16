using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Collections.Generic;


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
        _trayWindow = new();

        if (!RestoreLive2dWindows())
        {
            _window.Activate();
        }
    }


    public void ShowMainWindow()
    {
        _window ??= new();
        _window.Show();
    }


    private bool RestoreLive2dWindows()
    {
        if (_window is null)
            return false;

        var savedStates = Live2dWindowState.LoadAll();
        if (savedStates.Count == 0)
            return false;

        var toDelete = new List<string>();
        int restoredCount = 0;

        foreach (var state in savedStates)
        {
            if (!Live2dWindowState.IsMonitorAvailable(state.MonitorDevice))
            {
                toDelete.Add(state.Id);
                continue;
            }

            try
            {
                _window.RestoreLive2dWindow(state);
                restoredCount++;
            }
            catch
            {
                toDelete.Add(state.Id);
            }
        }

        foreach (var id in toDelete)
        {
            Live2dWindowState.DeleteById(id);
        }

        return restoredCount > 0;
    }


    private void AppInstanceInstance_Activated(object? sender, AppActivationArguments e)
    {
        _window?.DispatcherQueue.TryEnqueue(ShowMainWindow);
    }


}
