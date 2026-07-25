using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.ObjectModel;
using CDPIUI.ViewModels;
using WinUI3Localizer;
using CDPIUI.Helper;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Controls.Dialogs.MainPage;

public sealed partial class CommunityContentDialog : ContentDialog
{
    private ObservableCollection<UrlViewModel> Models = [];

    private ILocalizer localizer = Localizer.Get();

    public CommunityContentDialog()
    {
        InitializeComponent();

        MainListView.ItemsSource = Models;

        Init();
    }

    private UrlViewModel CreateNewModel(string name, string description, string url)
    {
        string locName = localizer.GetLocalizedString(name);
        string locDesc = localizer.GetLocalizedString(description);
        return new()
        {
            Name = locName,
            Description = locDesc,
            Url = url
        };
    }

    private void Init()
    {
        Models.Clear();
        Models.Add(CreateNewModel("TelegramMainUrlName", "TelegramMainUrlDescription", UrlOpenHelper.TelegramMainUrl));
        Models.Add(CreateNewModel("NTCPartyName", "NTCPartyDescription", UrlOpenHelper.NTCParty));
        Models.Add(CreateNewModel("BBDName", "BBDDecription", UrlOpenHelper.BBD));

        Models.Add(CreateNewModel("TelegramMemeUrlName", "TelegramMemeUrlDescription", UrlOpenHelper.TelegramMemeUrl));
        Models.Add(CreateNewModel("TelegramLUrlName", "TelegramLUrlDescription", UrlOpenHelper.TelegramLUrl));
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        var data = (sender as FrameworkElement).DataContext as UrlViewModel;

        UrlOpenHelper.LaunchUrl(data.Url);
    }
}
