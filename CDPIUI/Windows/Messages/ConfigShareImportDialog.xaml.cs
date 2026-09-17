using CDPIUI.Core;
using CDPIUI.AddOns.ConfigShare;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.Store.Database;
using CDPIUI.Default;
using CDPIUI.Helper.AddOns.ConfigShare;
using CDPIUI.Shared;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using WinUI3Localizer;

namespace CDPIUI.Messages;

public sealed partial class ConfigShareImportDialog : TemplateWindow
{
    private readonly ConfigShareService service = new();
    private ConfigSharePackage package;
    private bool busy;
    private bool closed;
    private bool initialized;
    private bool installed;
    private bool isDragDropImport;
    private Window modalOwner;

    private readonly ILocalizer localizer = Localizer.Get();

    private List<string> Modes = [];

    public ConfigShareImportDialog()
    {
        InitializeComponent();
        WindowTitle = localizer.GetLocalizedString("ConfigShareImportTitle");
        IconUri = @"Assets/favicon.ico";
        CustomTitleBarUserControl = TitleBarControl;
        DisableResizeFeature();

        Modes = [localizer.GetLocalizedString("AsKit"), localizer.GetLocalizedString("AsNewKit")];
        ModeSelectionCombobox.ItemsSource = Modes;
        ModeSelectionCombobox.SelectedIndex = 0;

        ProgressText.Text = localizer.GetLocalizedString("ConfigShareReading");

        DestinationCombo.SelectionChanged += (_, _) => UpdateDestination();
        Closed += (_, _) => { closed = true; if (!busy) package?.Dispose(); };
        AppWindow.Closing += (_, args) => args.Cancel = busy;
        Activated += OnFirstActivated;

        initialized = true;
    }

    public void ConfigureForDragDrop(Window owner)
    {
        isDragDropImport = true;
        modalOwner = owner;
        AlwaysAskCheckBox.Visibility = Visibility.Visible;
        AlwaysAskCheckBox.IsChecked = SettingsManager.Instance.GetValueOrDefault<bool>(
            "CONFIGSHARE", "AlwaysAskIfDragNDropConfigAdded", defaultValue: true);
    }

    private void AlwaysAskCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (isDragDropImport)
            SettingsManager.Instance.SetValue("CONFIGSHARE", "AlwaysAskIfDragNDropConfigAdded", AlwaysAskCheckBox.IsChecked == true);
    }

    private void OnFirstActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnFirstActivated;
        ((App)Application.Current).ShowWindowModalAsync(this, modalOwner);
    }

    public async Task SetFileAsync(string path)
    {
        if (package != null) return;
        SourcePath.Text = string.Format(localizer.GetLocalizedString("Source"), path);
        try
        {
            var loaded = await service.ReadAsync(path);
            if (closed) { loaded.Dispose(); return; }
            package = loaded;
            PresetName.Text = string.Format(localizer.GetLocalizedString("StoreSmallAddItemName"), package.Manifest.Name);

            DeveloperName.Text = string.Format(localizer.GetLocalizedString("StoreSmallDeveloperText"),  package.Manifest.Developer);

            KitName.Text = string.Format(localizer.GetLocalizedString("ConfigShareKitBy"), package.Manifest.Developer);
            var packs = service.GetDestinationPacks().ToList();
            foreach (var pack in packs)
                pack.ShortName = pack.Id == SharedConstants.LocalUserItemsId
                    ? localizer.GetLocalizedString("ConfigShareLocal") : pack.ShortName ?? pack.Name ?? pack.Id;
            DestinationCombo.ItemsSource = packs;
            DestinationCombo.SelectedItem = packs.FirstOrDefault();

            if (packs.Count == 0) ModeSelectionCombobox.SelectedItem = Modes[1];

            DestCard.Visibility = Visibility.Visible;
            UpdateDestination();
            ProgressPanel.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception) { if (!closed) ShowError(exception); }
        finally
        {
            if (!closed)
            {
                ProgressPanel.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void KitName_Changed(object sender, TextChangedEventArgs e) => UpdateDestination();
    private void UpdateDestination()
    {
        if (!initialized) return;
        bool newKit = (string)ModeSelectionCombobox.SelectedItem == Modes[1];
        NameCard.Visibility = newKit ? Visibility.Visible : Visibility.Collapsed;
        DestCard.Visibility = newKit ? Visibility.Collapsed : Visibility.Visible;
        InstallButton.IsEnabled = !busy && !installed && package != null &&
            (newKit ? !string.IsNullOrWhiteSpace(KitName.Text) : DestinationCombo.SelectedItem is DatabaseStoreItem);
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (busy || package == null || installed) return;

        busy = true;
        UpdateDestination();
        ModeSelectionCombobox.IsEnabled = DestinationCombo.IsEnabled = KitName.IsEnabled = false;
        CancelButton.IsEnabled = false;
        ErrorBar.IsOpen = false;
        ProgressPanel.Visibility = Visibility.Visible;
        ProgressText.Text = localizer.GetLocalizedString("ConfigShareInstalling");

        InstallButton.IsEnabled = false;

        try
        {
            await ConfigShareUI.OfferComponentInstallAsync(Content.XamlRoot, package.Config);
            if (closed) return;
            bool newKit = (string)ModeSelectionCombobox.SelectedItem == Modes[1];
            await service.InstallAsync(package, package.Manifest.Name,
                (DestinationCombo.SelectedItem as DatabaseStoreItem)?.Id,
                newKit ? KitName.Text : null);
            installed = true;
            try
            {
                string componentId = ConfigShareService.GetInstalledComponentId(package.Config);
                if (componentId != null)
                    ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(componentId)?.ReInitConfigs();
            }
            catch (Exception exception) { Logger.Instance.CreateWarningLog(nameof(ConfigShareImportDialog), $"Preset list refresh: {exception}"); }
            if (closed) return;
            SuccessBar.Message = localizer.GetLocalizedString("ConfigShareImported");
            SuccessBar.IsOpen = true;
            InstallButton.Visibility = DestinationPanel.Visibility = Visibility.Collapsed;
            CancelButton.Content = localizer.GetLocalizedString("ConfigShareClose");
            package.Dispose();
        }
        catch (Exception exception) { if (!closed) ShowError(exception); }
        finally
        {
            busy = false;
            if (closed) package?.Dispose();
            else
            {
                CancelButton.IsEnabled = ModeSelectionCombobox.IsEnabled = DestinationCombo.IsEnabled = KitName.IsEnabled = true;
                ProgressPanel.Visibility = Visibility.Collapsed;
                UpdateDestination();
            }
        }
    }

    private void ShowError(Exception exception)
    {
        Logger.Instance.CreateWarningLog(nameof(ConfigShareImportDialog), exception.ToString());
        ErrorBar.Message = ConfigShareUI.ErrorText(exception);
        ErrorBar.IsOpen = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) { if (!busy) Close(); }

    private void ModeSelectionCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateDestination();
    }
}
