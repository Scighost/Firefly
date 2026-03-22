using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;


namespace Firefly;

[ObservableObject]
public sealed partial class InfoDialog : ContentDialog
{


    public ReleaseInfo? NewRelease { get; set => SetProperty(ref field, value); }


    public bool NewVersionVisbility => NewRelease is not null;



    public InfoDialog()
    {
        InitializeComponent();
    }




    [RelayCommand]
    private void Close()
    {
        Hide();
    }




}
