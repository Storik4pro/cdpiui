using CDPI_UI.Default;
using CDPI_UI.Helper.Static;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class EditHostFileWindow : TemplateWindow
{
    private ILocalizer localizer = Localizer.Get();
    public EditHostFileWindow()
    {
        InitializeComponent();

        this.Title = UIHelper.GetWindowName(localizer.GetLocalizedString("EditHostsFileWindowTitle"));

        DisableResizeFeature();

        TitleIcon = TitleImageRectagle;
        TitleBar = WindowMoveAera;
        IconUri = @"Assets/Icons/EditHostsFile.ico";

        ContentFrame.Navigate(typeof(Views.EditHostsFile.MainPage));

        SetTitleBar(WindowMoveAera);
    }
}
