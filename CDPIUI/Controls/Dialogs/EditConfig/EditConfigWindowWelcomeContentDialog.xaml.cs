using CDPIUI.AddOns.ConfigImport;
using CDPIUI.Controls.ComponentSettings;
using CDPIUI.Controls.Dialogs.CreateConfigHelper;
using CDPIUI.Controls.Universal;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper;
using CDPIUI.Helper.AddOns.ConfigImport;
using CDPIUI.Helper.LScript;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;


namespace CDPIUI.Controls.Dialogs.EditConfig;

public enum EditConfigWelcomeWindowResult
{
    None,
    CreateNewConfig,
    ImportConfigFromFile,
    EditConfig,
    EditConfigKit,
    Exit,
}

public sealed partial class EditConfigWindowWelcomeContentDialog : ContentDialog
{

    private readonly ObservableCollection<ConfigSelectorItem> ConfigModels = [];
    private readonly ObservableCollection<ViewStoreItemModel> ConfigKits = [];

    public EditConfigWelcomeWindowResult Result { get; private set; } = EditConfigWelcomeWindowResult.None;
    public object ResultObject { get; private set; } = null;
    public string ResultSelectedComponentId { get; private set; } = string.Empty;

    private ILocalizer localizer = Localizer.Get();

    public EditConfigWindowWelcomeContentDialog()
    {
        InitializeComponent();

        InitDialog();
        LoadConfigKits();

        ConfigsListView.ItemsSource = ConfigModels;
        ConfigKitsListView.ItemsSource = ConfigKits;

        this.Closing += EditConfigWindowWelcomeContentDialog_Closing;
    }

    private void EditConfigWindowWelcomeContentDialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
    {
        if (Result is EditConfigWelcomeWindowResult.None) args.Cancel = true;
        else this.Closing -= EditConfigWindowWelcomeContentDialog_Closing;
    }

    private void InitDialog()
    {
        List<ViewComponentModel> components = new();
        foreach (var component in HardcodedItemIds.ComponentIds)
        {
            components.Add(new()
            {
                StoreId = component.Value,
                DisplayName = component.Key.ToString(),
                ImageSource =
                    new BitmapImage(UIHelper.GetUriFromString(LScriptLangHelper.ExecuteScript(DatabaseHelper.Instance.GetItemById(component.Value)?.IconPath ?? string.Empty)))
            });
        }
        ComponentChooseComboBox.ItemsSource = components;
    }

    private void InitModel(string componentId)
    {
        ConfigModels.Clear();
        ComponentHelper componentHelper =
            ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(
                componentId);

        List<ConfigItem> items = componentHelper?.GetConfigHelper()?.GetConfigItems();

        if (items != null)
        {

            foreach (var item in items)
            {
                ConfigModels.Add(
                    new()
                    {
                        DisplayName = item.name,
                        FileName = item.file_name,
                        PackId = item.packId,
                        PackDisplayName = DatabaseHelper.Instance.GetItemById(item.packId).ShortName,
                        IsLegacyConfig = item.IsLegacy,
                    });
            }
        }

        if (ConfigModels.Count == 0)
        {
            ConfigsNotFoundStackPanel.Visibility = Visibility.Visible;
        }
        else
        {
            ConfigsNotFoundStackPanel.Visibility = Visibility.Collapsed;
        }
    }

    private async void LoadConfigKits()
    {
        ConfigKits.Clear();
        List<DatabaseStoreItem> items = DatabaseHelper.Instance.GetItemsByType("configlist");

        foreach (DatabaseStoreItem item in items)
        {
            ConfigKits.Add(new()
            {
                StoreId = item.Id,
                Name = item.ShortName,
                Developer = item.Developer,
                ColorHEX = item.BackgroudColor,
                ImageSource = new BitmapImage(UIHelper.GetUriFromString(LScriptLangHelper.ExecuteScript(item.IconPath, scriptArgs: item.Directory)))
            });
        }

        await Task.CompletedTask;
    }

    private void ComponentChooseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        InitModel((ComponentChooseComboBox.SelectedItem as ViewComponentModel).StoreId);
    }

    private void CreateNewConfig_Click(object sender, RoutedEventArgs e)
    {
        CloseWithResult(EditConfigWelcomeWindowResult.CreateNewConfig);
    }

    private void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        LoadingStackPanel.Visibility = Visibility.Visible;
        SomethingWentWrongStackPanel.Visibility = Visibility.Collapsed;
        var result = ConfigImportHelper.ImportConfigFromFile((ComponentChooseComboBox.SelectedItem as ViewComponentModel)?.StoreId);
        if (result.Success && result.Result.IsSuccessful) CloseWithResult(EditConfigWelcomeWindowResult.ImportConfigFromFile, result.Result.Config);
        else if (result.Success)
        {
            SomethingWentWrongStackPanel.Visibility = Visibility.Visible;
        }
        LoadingStackPanel.Visibility = Visibility.Collapsed;
    }
    
    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (ConfigsListView.SelectedItem is ConfigSelectorItem model)
        {
            try
            {
                ComponentHelper componentHelper =
                    ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(
                        (ComponentChooseComboBox.SelectedItem as ViewComponentModel).StoreId);
                var item = componentHelper.GetConfigHelper().GetConfigItem(model.FileName, model.PackId);

                CloseWithResult(EditConfigWelcomeWindowResult.EditConfig, item);
            }
            catch { }
        }
    }

    private void EditKitButton_Click(object sender, RoutedEventArgs e)
    {
        CloseWithResult(EditConfigWelcomeWindowResult.EditConfigKit, (ConfigKitsListView.SelectedItem as ViewStoreItemModel)?.StoreId ?? string.Empty);
    }

    private void ToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is LargeToggleButton btn && btn == EditConfigKitButton)  EditConfigButton.IsChecked = false;
        if (sender is LargeToggleButton btn1 && btn1 == EditConfigButton) EditConfigKitButton.IsChecked = false;

        EmptyGrid.Visibility = ((EditConfigButton.IsChecked ?? false) || (EditConfigKitButton.IsChecked ?? false)) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        CloseWithResult(EditConfigWelcomeWindowResult.Exit);
    }

    private void CloseWithResult(EditConfigWelcomeWindowResult result, object @object = null)
    {
        Result = result;
        ResultObject = @object;
        ResultSelectedComponentId = (ComponentChooseComboBox.SelectedItem as ViewComponentModel)?.StoreId ?? string.Empty;
        this.Hide();
    }
}
