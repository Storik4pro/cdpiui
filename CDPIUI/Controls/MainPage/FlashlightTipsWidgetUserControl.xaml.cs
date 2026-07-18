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
using CDPIUI.Helper;
using Microsoft.UI.Xaml.Media.Animation;
using CDPIUI.Helper.Static;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Controls.MainPage;

public sealed partial class FlashlightTipsWidgetUserControl : UserControl
{

    private List<string> Tips = [];

    private int _currentIndex = 0;
    private bool _isAnimating = false;

    private int _targetIndex = 0;
    private double _slideInFromX = 0;

    private ILocalizer localizer = Localizer.Get();

    public FlashlightTipsWidgetUserControl()
    {
        InitializeComponent();

        this.Visibility = SettingsManager.Instance.GetValue<bool>("APPEARANCE", "showFlashlightWidget") ? Visibility.Visible : Visibility.Collapsed;

        LoadTips();

        ConnectHandlers();
    }

    public void ConnectHandlers()
    {
        SlideOutStoryboard.Completed += SlideOutStoryboard_Completed;
        SlideInStoryboard.Completed += SideInStoryboard_Completed;
        this.Unloaded += FlashlightTipsWidgetUserControl_Unloaded;
    }

    private void FlashlightTipsWidgetUserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        SlideOutStoryboard.Completed -= SlideOutStoryboard_Completed;
        SlideInStoryboard.Completed -= SideInStoryboard_Completed;
        this.Unloaded -= FlashlightTipsWidgetUserControl_Unloaded;
    }

    private void LoadTips()
    {
        if (!FlashlightHelper.LoadFlashlightTips(Tips))
        {
            Tips.Add(localizer.GetLocalizedString("FlashlightBad"));
            Tips.Add(":(");
            TipTitleTextBlock.Text = localizer.GetLocalizedString("FlashlightBadTitle");
        }
        else
        {
            TipTitleTextBlock.Text = localizer.GetLocalizedString("/Flashlight/AreYouKnow");
        }

        _currentIndex = Tips.Count;
        MoveNext();
    }

    private void PreviousTipButton_Click(object sender, RoutedEventArgs e)
    {
        MovePrevious();
    }

    private void NextTipButton_Click(object sender, RoutedEventArgs e)
    {
        MoveNext();
    }

    private void HideFlashlightWidgetButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsManager.Instance.SetValue("APPEARANCE", "showFlashlightWidget", false);
        this.Visibility = Visibility.Collapsed;
        // TODO: Add goodbye message.
    }

    public void MoveNext()
    {
        if (_isAnimating || Tips.Count <= 1) return;

        _targetIndex = (_currentIndex + 1) % Tips.Count;
        StartTransition(slideOutToX: -200, slideInFromX: 200);
    }

    public void MovePrevious()
    {
        if (_isAnimating || Tips.Count <= 1) return;

        _targetIndex = (_currentIndex - 1 + Tips.Count) % Tips.Count;
        StartTransition(slideOutToX: 200, slideInFromX: -200);
    }


    private void StartTransition(double slideOutToX, double slideInFromX)
    {
        _isAnimating = true;
        _slideInFromX = slideInFromX;

        SlideOutAnimation.To = slideOutToX;

        SlideOutStoryboard.Begin();
    }

    private void SlideOutStoryboard_Completed(object sender, object e)
    {
        _currentIndex = _targetIndex;
        TipDecriptionTextBlock.Text = Tips[_currentIndex];

        TextTransform.X = _slideInFromX;

        SlideInStoryboard.Begin();
    }

    private void SideInStoryboard_Completed(Object sender, object e)
    {
        _isAnimating = false;
    }
}
