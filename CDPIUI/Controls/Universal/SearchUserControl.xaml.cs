using CDPI_UI.Converters;
using CDPI_UI.Helper;
using CDPI_UI.Helper.Static;
using Microsoft.UI.Composition;
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
using TextControlBoxNS;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Controls.Universal;

// This part of code originally created by FrozenAssassine for https://github.com/FrozenAssassine/Fastedit

public sealed partial class SearchUserControl : UserControl
{
    private TextControlBox currentTextbox = null;
    public bool searchOpen = false;

    private SearchWindowState searchWindowState = SearchWindowState.Hidden;


    public SearchUserControl()
    {
        this.InitializeComponent();

        MatchCaseMenuFlyoutItem.IsChecked = SettingsManager.Instance.GetValue<bool>("SEARCH", "matchCase");
        WholeWordMenuFlyoutItem.IsChecked = SettingsManager.Instance.GetValue<bool>("SEARCH", "wholeWord");
    }

    private void CalcCenter(bool isReplaceOpened)
    {
        SearchWindowScaleTransform.CenterX = this.ActualWidth != 0 ? this.ActualWidth / 2 : this.MaxWidth / 2;
        SearchWindowScaleTransform.CenterY = (isReplaceOpened? 80 : 45) / 2;
    }

    private void BeginSearch(string searchword, bool matchCase, bool wholeWord)
    {
        var res = currentTextbox.BeginSearch(searchword, wholeWord, matchCase);
        ColorWindowBorder(res);
    }
    private void ToggleVisibility(bool visible)
    {
        var converter = new BoolToVisibilityConverter();
        textToReplaceTextBox.Visibility = ReplaceAllButton.Visibility =
            StartReplaceButton.Visibility = (Visibility)converter.Convert(visible, typeof(Visibility), null, Language);
    }
    private void ColorWindowBorder(SearchResult result)
    {
        SearchWindow.BorderBrush = (SolidColorBrush)(result == SearchResult.Found ? 
            ((App)Application.Current).Resources["SystemFillColorSuccessBrush"] : ((App)Application.Current).Resources["SystemFillColorCautionBrush"]);
    }
    private void HideWindow()
    {
        hideSearchAnimation.Begin();
        searchWindowState = SearchWindowState.Hidden;
    }
    private void ShowWindow()
    {
        this.Visibility = Visibility.Visible;
        showSearchAnimation.Begin();
        searchWindowState = SearchWindowState.Default;
    }
    private void ExpandReplace()
    {
        expandSearchAnimation.Begin();
        searchWindowState = SearchWindowState.Expanded;
    }
    private void CollapseReplace(bool skipAnim = false)
    {
        collapseSearchAnimation.Begin();
        if (skipAnim) collapseSearchAnimation.SkipToFill();
        searchWindowState = SearchWindowState.Default;
    }

    public void ShowSearch(TextControlBox textBox)
    {
        if (currentTextbox != null && currentTextbox != textBox)
        {
            currentTextbox.EndSearch();
            this.searchWindowState = SearchWindowState.Hidden;
        }

        currentTextbox = textBox;
        searchOpen = true;

        if (searchWindowState == SearchWindowState.Expanded)
        {
            CollapseReplace();
        }
        else if (searchWindowState == SearchWindowState.Hidden)
        {
            CalcCenter(false);
            CollapseReplace(true);
            ShowWindow();
        }

        if (currentTextbox.HasSelection && currentTextbox.CalculateSelectionPosition().Length < 200)
        {
            textToFindTextbox.Text = currentTextbox.SelectedText;
        }

        textToFindTextbox.Focus(FocusState.Keyboard);
        textToFindTextbox.SelectAll();
    }

    public void ShowReplace(TextControlBox textBox)
    {
        if (currentTextbox != null && currentTextbox != textBox)
        {
            currentTextbox.EndSearch();
            this.searchWindowState = SearchWindowState.Hidden;
        }

        currentTextbox = textBox;
        searchOpen = true;

        if (searchWindowState == SearchWindowState.Default)
        {
            ExpandReplace();
        }
        else if (searchWindowState == SearchWindowState.Hidden)
        {
            CalcCenter(true);
            ShowWindow();
            ExpandReplace();
        }


        if (currentTextbox.HasSelection && currentTextbox.CalculateSelectionPosition().Length < 200)
        {
            textToFindTextbox.Text = currentTextbox.SelectedText;
        }

        textToReplaceTextBox.Focus(FocusState.Keyboard);
        textToReplaceTextBox.SelectAll();
        textToFindTextbox.Focus(FocusState.Keyboard);
        textToFindTextbox.SelectAll();
    }

    public void Close()
    {
        if (!searchOpen || currentTextbox == null)
            return;

        searchOpen = false;
        currentTextbox.EndSearch();
        HideWindow();
        currentTextbox.Focus(FocusState.Programmatic);

        currentTextbox = null;
    }

    private void UpdateSearch()
    {
        BeginSearch(textToFindTextbox.Text, MatchCaseMenuFlyoutItem.IsChecked, WholeWordMenuFlyoutItem.IsChecked);
    }

    private void ReplaceTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            ReplaceCurrentButton_Click(null, null);
        }
    }
    private void SearchTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (currentTextbox == null)
            return;

        //Search down on Enter and up on Shift + Enter//
        var shift = UIHelper.IsKeyPressed(VirtualKey.Shift);
        if (e.Key == VirtualKey.Enter)
        {
            if (shift)
                currentTextbox.FindPrevious();
            else
                currentTextbox.FindNext();
        }
    }
    private void SearchUpButton_Click(object sender, RoutedEventArgs e)
    {
        currentTextbox.FindPrevious();
    }
    private void SearchDownButton_Click(object sender, RoutedEventArgs e)
    {
        currentTextbox.FindNext();
    }
    private void ReplaceAllButton_Click(object sender, RoutedEventArgs e)
    {
        var res = currentTextbox.ReplaceAll(
            textToFindTextbox.Text,
            textToReplaceTextBox.Text,
            MatchCaseMenuFlyoutItem.IsChecked,
            WholeWordMenuFlyoutItem.IsChecked
            );

        ColorWindowBorder(res);
    }
    private void ReplaceCurrentButton_Click(object sender, RoutedEventArgs e)
    {
        var res = currentTextbox.ReplaceNext(textToReplaceTextBox.Text);
        ColorWindowBorder(res);
    }
    private void SearchWindow_CloseButtonClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
    private void ExpandSearchBoxForReplaceButton_Click(object sender, RoutedEventArgs e)
    {
        if (searchWindowState == SearchWindowState.Expanded)
            ShowSearch(currentTextbox);
        else
            ShowReplace(currentTextbox);
    }
    private void TextBoxes_GotFocus(object sender, RoutedEventArgs e)
    {
        (sender as TextBox)?.SelectAll();
    }

    private void TextToFindTextbox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSearch();
    }
    private void SearchProperties_Changed(object sender, RoutedEventArgs e)
    {
        UpdateSearch();
    }

    private void CollapseSearchAnimation_Completed(object sender, object e)
    {
        ToggleVisibility(false);
    }
    private void ExpandSearchAnimation_Completed(object sender, object e)
    {
        ToggleVisibility(true);
    }
    private void HideSearchAnimation_Completed(object sender, object e)
    {
        this.Visibility = Visibility.Collapsed;
    }

    private void MatchCaseMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
    {
        SettingsManager.Instance.SetValue("SEARCH", "matchCase", MatchCaseMenuFlyoutItem.IsChecked);
        UpdateSearch();
    }

    private void WholeWordMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
    {
        SettingsManager.Instance.SetValue("SEARCH", "wholeWord", WholeWordMenuFlyoutItem.IsChecked);
        UpdateSearch();
    }
}
public enum SearchWindowState
{
    Expanded, //replace and search
    Default, //only search
    Hidden //not visible
}
