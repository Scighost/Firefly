using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Windowing;
using System;


namespace Firefly;

public partial class Live2DWindowInfo : ObservableObject
{

    public string Name { get; set; }


    public Live2dWindow Window { get; set; }

    public event EventHandler<object> Closed;


    public bool IsVisible { get; set => SetProperty(ref field, value); } = true;

    public bool IsNotVisible { get; set => SetProperty(ref field, value); }



    public Live2DWindowInfo(string name, Live2dWindow window)
    {
        Name = name;
        Window = window;
        Window.AppWindow.Changed += AppWindow_Changed;
        Window.AppWindow.Destroying += AppWindow_Destroying;
    }


    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidVisibilityChange)
        {
            IsVisible = sender.IsVisible;
            IsNotVisible = !IsVisible;
        }
    }


    private void AppWindow_Destroying(AppWindow sender, object args)
    {
        sender.Changed -= AppWindow_Changed;
        sender.Destroying -= AppWindow_Destroying;
        Closed?.Invoke(this, args);
    }


    [RelayCommand]
    public void ChangeVisible()
    {
        if (Window.Visible)
        {
            Window.AppWindow.Hide();
        }
        else
        {
            Window.AppWindow.Show();
        }
    }


    [RelayCommand]
    public void CloseWindow()
    {
        if (Window.AppWindow is not null)
        {
            Window.Close();
        }
    }


}