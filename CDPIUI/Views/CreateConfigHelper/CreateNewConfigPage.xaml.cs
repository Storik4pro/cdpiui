using CDPIUI.Controls.Dialogs.CreateConfigHelper;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.JSON;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.System;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Helper.LScript;

using CDPIUI.Shared;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.Specialized;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Windows.Media;
using TextControlBoxNS;
using Unidecode.NET;
using Windows.ApplicationModel.Chat;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
using WinRT.Interop;
using WinUI3Localizer;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrayNotify;
using Color = Windows.UI.Color;
using SolidColorBrush = Microsoft.UI.Xaml.Media.SolidColorBrush;
using CDPIUI.Controls.Default;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Views.CreateConfigHelper;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>

public class ConditionVariableModel
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string OnValue { get; set; }
    public string OffValue { get; set; }
}
public class VariableModel
{
    public string Name { get; set; }
    public string Value { get; set; }
    public AvailableVarValues AvailableValues { get; set; } = null;
    
}

public partial class GraphicDesignerSettingItemModel : INotifyPropertyChanged
{
    public string Guid { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string Type { get; set; }
    public List<EnumModel> AvailableEnumValues { get; set; }
    public bool EnableTextInput { get; set; }
    public string _value = "";
    public string Value {
        get => _value;
        set => SetField(ref _value, value);
    }

    private bool isChecked = false;
    public bool IsChecked
    {
        get => isChecked;
        set => SetField(ref isChecked, value);
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public event PropertyChangedEventHandler PropertyChanged;
}
public partial class GraphicDesignerExclusiveSettingItemModel : INotifyPropertyChanged
{
    public string Guid { get; set; }
    public string DisplayName { get; set; }

    private string _selectedItemGuid = "";
    public string SelectedItemGuid 
    {
        get => _selectedItemGuid;
        set => SetField(ref _selectedItemGuid, value);
    }
    public ObservableCollection<GraphicDesignerSettingItemModel> Items { get; set; }

    public string _value = "";
    public string Value {
        get => _value;
        set => SetField(ref _value, value);
    }

    private bool isChecked = false;
    public bool IsChecked
    {
        get => isChecked;
        set => SetField(ref isChecked, value);
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public event PropertyChangedEventHandler PropertyChanged;
}

public enum AskAutoFillMode
{
    Ask,
    Quiet
}

public enum PageOpenModes
{
    None,
    EditConfigNoSave,
    EditConfig,
    CreateConfigFromString,
}



public sealed partial class CreateNewConfigPage : TemplatePage
{
    private List<string> DesignerSupportedComponentIds = ["CSSIXC048", "CSGIVS036", "CSNIG9025"];
    
    private class ComponentModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public ObservableCollection<ConditionVariableModel> Conditions { get; } = new ObservableCollection<ConditionVariableModel>();
    public ObservableCollection<VariableModel> Variables { get; } = new ObservableCollection<VariableModel>();

    public ICommand RemoveConditionCommand { get; }
    public ICommand RemoveVariableCommand { get; }
    public ICommand VariableChangedCommand { get; }

    public ICommand GraphicDesignerTextValueChangedCommand { get; }
    public ICommand GraphicDesignerBoolValueToggledCommand { get; }
    public ICommand GraphicDesignerSelectedGuidChangedCommand { get; }
    public ObservableCollection<GraphicDesignerSettingItemModel> DesignerSettingItemModels { get; } = [];
    public ObservableCollection<GraphicDesignerExclusiveSettingItemModel> DesignerExclusiveSettingItemModels { get; } = [];

    private ConfigItem ConfigItem;

    private ILocalizer localizer = Localizer.Get();

    public string EditItemId = string.Empty;
    public PageOpenModes PageOpenMode = PageOpenModes.None;

    public CreateNewConfigPage()
    {
        InitializeComponent();
        this.DataContext = this;
        this.Loaded += CreateNewConfigPage_Loaded;
        RemoveConditionCommand = new RelayCommand(p => RemoveCondition(p));
        RemoveVariableCommand = new RelayCommand(p => RemoveVariable(p));
        VariableChangedCommand = new RelayCommand(p => VariableChangedValue((Tuple<string, string>)p));

        ConditionsListView.ItemsSource = Conditions;
        VariablesListView.ItemsSource = Variables;

        GraphicDesignerTextValueChangedCommand = new RelayCommand(p => HandleGraphicDesignerTextValueChanged((Tuple<string, string>)p));
        GraphicDesignerBoolValueToggledCommand = new RelayCommand(p => HandleGraphicDesignerBoolValueToggled((Tuple<string, bool>)p));
        GraphicDesignerSelectedGuidChangedCommand = new RelayCommand(p => HandleGraphicDesignerSelectedGuidChanged((Tuple<string, string>)p));

        GUIDesignerListView.ItemsSource = DesignerSettingItemModels;
        GUIDesignerExclusiveListView.ItemsSource = DesignerExclusiveSettingItemModels;

        CheckHighlight();

        StartupStringTextBox.UseSpacesInsteadTabs = true;
        StartupStringTextBox.NumberOfSpacesForTab = 4;

        InitPage();

        this.ActualThemeChanged += CreateNewConfigPage_ActualThemeChanged;
        this.ProcessKeyboardAccelerators += CreateNewConfigPage_ProcessKeyboardAccelerators;
        this.KeyDown += CreateNewConfigPage_KeyDown;
    }

    #region EventHandlers

    private void CreateNewConfigPage_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            SearchControl.Close();
        }
    }

    private void CreateNewConfigPage_ProcessKeyboardAccelerators(UIElement sender, ProcessKeyboardAcceleratorEventArgs args)
    {
        if ((args.Key == Windows.System.VirtualKey.Add || args.Key.ToString() == "187") && args.Modifiers == Windows.System.VirtualKeyModifiers.Control)
        {
            ChangeZoom(5);
        }
        else if ((args.Key == Windows.System.VirtualKey.Subtract || args.Key.ToString() == "189") && args.Modifiers == Windows.System.VirtualKeyModifiers.Control)
        {
            ChangeZoom(-5);
        }
    }   

    private void CreateNewConfigPage_ActualThemeChanged(FrameworkElement sender, object args)
    {
        if (MainGrid.Style == (Style)Resources["BlackStyle"] || MainGrid.Style == (Style)Resources["MicaSmokeStyle"])
        {
            CheckHighlight(ElementTheme.Dark);
        }
        else
        {
            CheckHighlight(ElementTheme.Default);
        }
    }

    #endregion


    #region DefaultDesigner
    private void CheckHighlight(ElementTheme elementTheme)
    {
        if (elementTheme == ElementTheme.Light || (elementTheme == ElementTheme.Default && ((App)Application.Current).CurrentTheme == ElementTheme.Light))
        {
            StartupStringTextBox.SyntaxHighlighting = new LightDefaultHighlighter();
            StartupStringTextBox.Design = TextControlBoxDesigns.DefaultLightDesign;
        }
        else
        {
            StartupStringTextBox.SyntaxHighlighting = new DarkDefaultHighlighter();
            StartupStringTextBox.Design = TextControlBoxDesigns.DefaultDarkDesign;
        }
    }
    private void CheckHighlight()
    {
        CheckHighlight(MainGrid.RequestedTheme);
    }

    public void OpenSearch()
    {
        if (IsSearchAvailable())
            SearchControl.ShowSearch(StartupStringTextBox);
    }
    public void OpenSearchAndReplace()
    {
        if (IsSearchAvailable())
            SearchControl.ShowReplace(StartupStringTextBox);
    }

    #endregion

    private void CreateNewConfigPage_Loaded(object sender, RoutedEventArgs e)
    {
        EditItemId = SharedConstants.LocalUserItemsId;

        if (Parameter != null) RunOnNavigatedToActions(Parameter);
        AuditSaveAvailable();
        Parameter = null;
        this.Loaded -= CreateNewConfigPage_Loaded;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        this.KeyDown -= CreateNewConfigPage_KeyDown;
        this.ProcessKeyboardAccelerators -= CreateNewConfigPage_ProcessKeyboardAccelerators;
        this.ActualThemeChanged -= CreateNewConfigPage_ActualThemeChanged;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
    }

    private void RunOnNavigatedToActions(object args)
    {
        if (args is NameValueCollection collection)
        {
            string operationType = collection.Get("type");
            if (operationType == "CFGSTRING") 
            { 
                string startupString = collection.Get("startupString");
                string componentId = collection.Get("componentId");

                ComponentChooseComboBox.SelectedItem = ComponentChooseComboBox.Items
                    .Cast<ComponentModel>()
                    .FirstOrDefault(c => c.Id == componentId);

                PageOpenMode = PageOpenModes.CreateConfigFromString;
                AddWrap(startupString);

                ConfigItem = new()
                {
                    startup_string = startupString,
                };
                AskAutoFillFiles(
                    ConfigItem,
                    LScriptLangHelper.ExecuteScript(
                        "$GETCURRENTDIR()", 
                    callItemId: SharedConstants.LocalUserItemsId), 
                    AskAutoFillMode.Quiet);
            }
            else if (operationType == "CFGIMPORT")
            {
                ConfigItem = JSONConvertor.DeserializeObject<ConfigItem>(collection.Get("configItem"));
                bool errorHappens = bool.Parse(collection.Get("errorHappens"));
                string filePath = collection.Get("filePath");

                PageTitleTextBlock.Text = localizer.GetLocalizedString("ImportConfigFromFile");
                if (ConfigItem.target != null)
                {
                    ComponentChooseComboBox.SelectedItem = ComponentChooseComboBox.Items
                        .Cast<ComponentModel>()
                        .FirstOrDefault(c => c.Id == ConfigItem.target[0]);
                }

                AddWrap();

                LoadVars(ConfigItem);

                AskAutoFillFiles(ConfigItem, filePath);
            }
            else if (operationType == "CFGEDIT")
            {
                ConfigItem = JSONConvertor.DeserializeObject<ConfigItem>(collection.Get("configItem"));

                PageTitleTextBlock.Text = localizer.GetLocalizedString("EditConfig");
                if (ConfigItem.target != null)
                {
                    ComponentChooseComboBox.SelectedItem = ComponentChooseComboBox.Items
                        .Cast<ComponentModel>()
                        .FirstOrDefault(c => c.Id == ConfigItem.target[0]);
                }
                ComponentChooseComboBox.IsEnabled = false;

                PageOpenMode = PageOpenModes.EditConfig;
                if (ConfigItem.packId != SharedConstants.LocalUserItemsId)
                {
                    DisplayNameTextBox.Text = $"{ConfigItem.name} ({localizer.GetLocalizedString("Edited")})";
                    SaveButtonText.Text = localizer.GetLocalizedString("SaveAsACopy");
                }
                else
                {
                    DisplayNameTextBox.Text = $"{ConfigItem.name}";
                }

                if (ConfigItem.packId != SharedConstants.LocalUserItemsId)
                {
                    AskAutoFillFiles(
                        ConfigItem,
                        LScriptLangHelper.ExecuteScript("$GETCURRENTDIR()", 
                        callItemId: ConfigItem.packId), AskAutoFillMode.Quiet);
                }

                LoadVars(ConfigItem);
                LoadConditions(ConfigItem);

                AddWrap();
            }
            else if (operationType == "CFGCREATEBYID")
            {
                string componentId = collection.Get("componentId");

                ComponentChooseComboBox.SelectedItem = ComponentChooseComboBox.Items
                    .Cast<ComponentModel>()
                    .FirstOrDefault(c => c.Id == componentId);
            }
            else if (operationType == "CFGRETURNEDITED")
            {
                PageTitleTextBlock.Text = localizer.GetLocalizedString("EditConfig");
                if (ConfigItem.target != null)
                {
                    ComponentChooseComboBox.SelectedItem = ComponentChooseComboBox.Items
                        .Cast<ComponentModel>()
                        .FirstOrDefault(c => c.Id == ConfigItem.target[0]);
                }
                ComponentChooseComboBox.IsEnabled = false;

                PageOpenMode = PageOpenModes.EditConfigNoSave;
                EditItemId = ConfigItem.packId;

                DisplayNameTextBox.Text = ConfigItem.name;
                SaveButtonText.Text = localizer.GetLocalizedString("Save");

                LoadVars(ConfigItem);
                LoadConditions(ConfigItem);

                AddWrap();
            }
        }
        CheckViewType();
    }

    private async void AskAutoFillFiles(ConfigItem configItem, string dir, AskAutoFillMode askAutoFillMode = AskAutoFillMode.Ask)
    {
        List<string> usedFiles = ConfigurationService.GetUsedFilesFromConfigItem(configItem);

        SelectUsedFilesForConfigContentDialog dialog = new(usedFiles, configItem.name, dir, configItem, askAutoFillMode)
        {
            XamlRoot = this.XamlRoot
        };
        var result = await dialog.ShowAsync();
        if (dialog.Result == CreateConfigResult.Selected)
        {
            ConfigItem = ConfigurationService.ReplaceFilesPath(ConfigItem, dialog.Files);
            AddWrap();

            LoadVars(configItem);
            AuditSaveAvailable();
        }
        else
        {
            CreateConfigHelperWindow.Instanse.IsOperationExitAskAvailable = false;
            Frame.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
        }
    } 

    private void LoadConditions(ConfigItem configItem)
    {
        Conditions.Clear();
        if (configItem.variables == null || configItem.variables.Count == 0)
            { return; }

        List<VariableItem> variables = ConfigurationService.GetVariables(configItem);
        ComponentHelper componentHelper = 
            ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId((ComponentChooseComboBox.SelectedItem as ComponentModel).Id);


        foreach (var variable in configItem.variables) 
        {
            var t = LScriptLangHelper.GetNameOnOffValuesFromConditionString(variable);
            if (t == null)
                continue;

            var (_var, conditionVarName, onValue, offValue) = t;
            Conditions.Add(
                new()
                {
                    Name = conditionVarName,
                    OnValue = onValue,
                    OffValue = offValue,
                    Description = componentHelper?.GetConfigHelper()?.GetLocalizedConfigVarName(_var, configItem.packId) ?? string.Empty,
                });
        }
    }

    private void LoadVars(ConfigItem configItem)
    {
        Variables.Clear();

        if (configItem.variables != null)
        {
            foreach (var variable in configItem.variables)
            {
                var t = LScriptLangHelper.GetNameOnOffValuesFromConditionString(variable);

                if (t == null)
                {
                    var spVar = variable.Split("=");
                    Variables.Add(new()
                    {
                        Name = spVar[0].Replace("%", ""),
                        Value = spVar[1]
                    });
                }
            }
        }

        if (configItem.commaVars != null)
        {
            foreach (var commaVar in configItem.commaVars)
            {
                try
                {
                    Variables.Add(new VariableModel
                    {
                        Name = commaVar.Key,
                        Value = commaVar.Value,
                        AvailableValues = configItem.availableCommaVarsValues?.FirstOrDefault(varItem => varItem.VarName == commaVar.Key, null)
                    });
                }
                catch (Exception ex)
                {
                    Logger.Instance.CreateWarningLog(nameof(CreateNewConfigPage), $"{ex}");
                }
            }
        }

        if (Variables.Count == 0)
        {
            VariablesStackPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            VariablesStackPanel.Visibility = Visibility.Visible;
        }
    }


    private void InitPage()
    {
        List<ComponentModel> components = new();
        foreach (var component in HardcodedItemIds.ComponentIds)
        {
            components.Add(new() 
            { 
                Id = component.Value,
                Name = component.Key.ToString()
            });
        }
        ComponentChooseComboBox.ItemsSource = components;

        AuditSaveAvailable();

        SaveButtonText.Text = localizer.GetLocalizedString("Save");
        PageTitleTextBlock.Text = localizer.GetLocalizedString("CreateNewConfig");
        TestButtonText.Text = localizer.GetLocalizedString("TestThis");
        ExpandToolTip.Content = localizer.GetLocalizedString("HideConfigSettings");
    }

    private bool IsSaveAvailable()
    {
        bool result = false;

        result = ComponentChooseComboBox.SelectedIndex != -1 &&
            !string.IsNullOrEmpty(DisplayNameTextBox.Text) &&
            !string.IsNullOrEmpty(StartupStringTextBox.Text);
        return result;
    }

    private void AuditSaveAvailable()
    {
        bool state = IsSaveAvailable();
        SaveConfigButton.IsEnabled = state;
        TestButtonText.IsEnabled = state;
    }

    private bool IsVarAddAvailable()
    {
        bool result = CreateConfigPageHelper.IsNameCorrect(VarNameTextBox.Text.Replace(" ", "")) &&
            !string.IsNullOrEmpty(OnValueTextBox.Text) &&
            !string.IsNullOrEmpty(OffValueTextBox.Text);
        return result;
    }

    private void AuditSaveVarAvailable()
    {
        bool state = IsVarAddAvailable();
        SaveConditionButton.IsEnabled = state;
    }

    private void CreatePreview()
    {
        if (!IsVarAddAvailable())
        {
            ConditionPreviewTextBlock.Text = localizer.GetLocalizedString("NothingToPreview");
            return;
        }
        ConditionPreviewTextBlock.Text = 
            LScriptLangHelper.CreateCondition(VarNameTextBox.Text.Replace(" ", ""), OnValueTextBox.Text, OffValueTextBox.Text);
    }

    private void SaveConditionButton_Click(object sender, RoutedEventArgs e)
    {
        CreateConfigHelperWindow.Instanse.IsOperationExitAskAvailable = true;
        ConditionVariableModel model = new()
        {
            Name = VarNameTextBox.Text,
            Description = DescriptionTextBox.Text,
            OnValue = OnValueTextBox.Text,
            OffValue = OffValueTextBox.Text,
        };
        Conditions.Add(model);
        ConditionFlyout.Hide();
    }

    private void ConditionFlyout_Opened(object sender, object e)
    {
        VarNameTextBox.Text = "";
        DescriptionTextBox.Text = "";
        OnValueTextBox.Text = "";
        OffValueTextBox.Text = "";
        ConditionPreviewTextBlock.Text = localizer.GetLocalizedString("NothingToPreview");
    }

    private Tuple<Dictionary<string, bool>, List<string>> CreateVariables(int secondsSinceEpoch)
    {
        Dictionary<string, bool> jparams = new();
        List<string> vars = new();

        try
        {

            string localAppData = CDPIUI.Core.Data.Directories.DataDirectory;
            string locFile = ConfigurationService.GetDefaultLocalePath(EditItemId);

            if (string.IsNullOrEmpty(locFile)) throw new FileNotFoundException("Loc file not found");

            if (!Directory.Exists(Path.GetDirectoryName(locFile)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(locFile));
            }

            Dictionary<string, string> localizationDict = [];
            if (File.Exists(locFile))
            {
                localizationDict = JSONConvertor.LoadJson<Dictionary<string, string>>(locFile);
            }
            localizationDict ??= [];

            foreach (var condition in Conditions)
            {
                jparams.Add($"{condition.Name}_var_{secondsSinceEpoch}", false);
                vars.Add($"%{condition.Name}%=$LOCALCONDITION({condition.Name}_var_{secondsSinceEpoch}==true ? {condition.OnValue} $SEPARATOR {condition.OffValue})");
                localizationDict.Add($"{condition.Name}_var_{secondsSinceEpoch}", condition.Description);
            }

            string jsonString = System.Text.Json.JsonSerializer.Serialize(localizationDict);
            File.WriteAllText(locFile, jsonString);

            return new(jparams, vars);
        }
        catch (Exception ex)
        {
            ShowErrorDialog(
                string.Format(localizer.GetLocalizedString("CannotSaveConfigMessage"), ErrorsHelper.Convertor.GetPrettyErrorCode("CREATE_CONFIG", ex)), 
                localizer.GetLocalizedString("SomethingWentWrong")
                );
            Logger.Instance.CreateWarningLog(nameof(CreateNewConfigPage), $"{ex}");
            return new(null, null);
        }
    }
    private ConfigItem CreateConfig(int secondsSinceEpoch)
    {
        string componentId = (ComponentChooseComboBox.SelectedItem as ComponentModel).Id;
        var (jparams, vars) = CreateVariables(secondsSinceEpoch);

        if (jparams == null || vars == null) return null;

        var configItem = CreateConfigPageHelper.CreateConfigItem(
            SharedConstants.LocalUserItemsId,
            DisplayNameTextBox.Text,
            componentId,
            jparams,
            vars,
            Variables.ToDictionary(v => v.Name, v => v.Value),
            Variables.Where(v => v.AvailableValues != null).Select(v => v.AvailableValues).ToList(),
            CreateConfigPageHelper.GetNormalText(StartupStringTextBox.Text, ListDirectory, BinDirectory)
            );

        return configItem;
    }

    private bool isRunned = false;
    private string curRunId = string.Empty;

    private void Instance_onProcessStateChanged(Tuple<string, bool> tuple)
    {
        if (tuple.Item1 != curRunId) return;
        if (tuple.Item2)
        {
            TestButtonGlyph.Glyph = "\uE71A";
            TestButtonText.Text = localizer.GetLocalizedString("StopTest");
            isRunned = true;
        }
        else
        {
            TestButtonGlyph.Glyph = "\uE768";
            TestButtonText.Text = localizer.GetLocalizedString("TestThis");
            isRunned = false;
        }
    }
    
    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        if (!isRunned)
        {
            isRunned = true;
            ComponentTasksManager.Instance.TaskStateUpdated += Instance_onProcessStateChanged;

            
            ComponentModel model = ComponentChooseComboBox.SelectedItem as ComponentModel;
            curRunId = model.Id;
            await ComponentTasksManager.Instance.StopTask(curRunId);
            ConvertDesignerLikeSettingsToString();
            var config = CreateConfig(0);
            if (config != null) 
                ComponentTasksManager.Instance.CreateAndRunNewTask(curRunId, ConfigurationService.GetStartupParametersByConfigItem(config));
        }
        else
        {
            await ComponentTasksManager.Instance.StopTask(curRunId);
            TestButtonGlyph.Glyph = "\uE768";
            TestButtonText.Text = localizer.GetLocalizedString("TestThis");
            isRunned = false;
            ComponentTasksManager.Instance.TaskStateUpdated -= Instance_onProcessStateChanged;
            curRunId = string.Empty;
        }
    }

    private async void ShowDialog()
    {
        CreateCompleteDialog dialog = new()
        {
            XamlRoot = this.XamlRoot
        };
        CreateConfigHelperWindow.Instanse.IsOperationExitAskAvailable = false;
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            CreateConfigHelperWindow.Instanse.Close();
        else
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
            else
                Frame.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
        }

    }

    private async void SaveConfigButton_Click(object sender, RoutedEventArgs e)
    {
        ConvertDesignerLikeSettingsToString();
        TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
        int secondsSinceEpoch = (int)t.TotalSeconds;

        ConfigItem configItem = CreateConfig(secondsSinceEpoch);

        if (configItem == null)
        {
            return;
        }

        if (PageOpenMode == PageOpenModes.EditConfigNoSave)
        {
            var window = ((App)Application.Current).GetCurrentWindowFromType<CreateConfigHelperWindow>();
            if (window != null) window.IsOperationExitAskAvailable = false;
            window?.NavigateBackWithParameter(configItem);
            return;
        }

        string src;
        if (ConfigItem == null || ConfigItem.packId != SharedConstants.LocalUserItemsId)
            src = DisplayNameTextBox.Text + $"_{secondsSinceEpoch}.json";
        else
            src = ConfigItem.file_name;

        string transl = src.Unidecode();
        transl = Regex.Replace(transl, @"\s+", "_");
        transl = Regex.Replace(transl, @"[^A-Za-z0-9_\.-]", "");
        transl = Regex.Replace(transl, "_+", "_").Trim('_');
        
        string errorCode = await ConfigurationService.SaveConfigItem(transl, SharedConstants.LocalUserItemsId, configItem);
        if (!string.IsNullOrEmpty(errorCode))
        {
            ShowErrorDialog(
                string.Format(localizer.GetLocalizedString("CannotSaveConfigMessage"), errorCode),
                localizer.GetLocalizedString("SomethingWentWrong")
                );
        }

        ComponentHelper componentHelper =
                ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(
                    (ComponentChooseComboBox.SelectedItem as ComponentModel).Id);
        componentHelper?.ReInitConfigs();

        ShowDialog();
    }

    private void ComponentChooseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        CheckViewType();

        CreateConfigHelperWindow.Instanse.IsOperationExitAskAvailable = true;
        AuditSaveAvailable();
    }

    private bool IsSearchAvailable()
    {
        if (DesignerSupportedComponentIds.Contains(((ComponentModel)ComponentChooseComboBox.SelectedItem)?.Id ?? ""))
            return MainPivot.SelectedItem == DefaultDesigner;
        else return true;
    }

    private void CheckViewType()
    {
        if (ComponentChooseComboBox.SelectedItem == null)
        {
            AdditionalSettingsPanel.Visibility = Visibility.Collapsed;
            NotReadyErrorGrid.Visibility = Visibility.Visible;
            return;
        }
        else
        {
            AdditionalSettingsPanel.Visibility = Visibility.Visible;
            NotReadyErrorGrid.Visibility = Visibility.Collapsed;
        }
        if (DesignerSupportedComponentIds.Contains(((ComponentModel)ComponentChooseComboBox.SelectedItem).Id))
        {
            DesignerItem.IsSelected = true;
            MainPivot.Visibility = Visibility.Visible;
            HandleSelectionChange(false);
        }
        else
        {
            HandleSelectionChange(true);
            DefaultDesigner.IsSelected = true;
            MainPivot.Visibility = Visibility.Collapsed;
        }        
    }

    private void DisplayNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        CreateConfigHelperWindow.Instanse.IsOperationExitAskAvailable = true;
        AuditSaveAvailable();
    }

    private void StartupStringTextBox_TextChanged(TextControlBox sender)
    {
        CreateConfigHelperWindow.Instanse.IsOperationExitAskAvailable = true;
        AuditSaveAvailable();
    }

    private void VarNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        CreatePreview();
        AuditSaveVarAvailable();
    }

    private void OnValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        CreatePreview();
        AuditSaveVarAvailable();
    }

    private void OffValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        CreatePreview();
        AuditSaveVarAvailable();
    }

    private void RemoveCondition(object parameter)
    {
        if (parameter is ConditionVariableModel vm && Conditions.Contains(vm))
        {
            Conditions.Remove(vm);
        }
    }

    private void RemoveVariable(object parameter)
    {
        if (parameter is VariableModel vm && Variables.Contains(vm))
        {
            Variables.Remove(vm);
        }
    }
    private void VariableChangedValue(Tuple<string, string> parameter)
    {
        CreateConfigHelperWindow.Instanse.IsOperationExitAskAvailable = true;
        Logger.Instance.CreateDebugLog(nameof(CreateNewConfigPage), $"Variable changed: {parameter.Item1} to {parameter.Item2}");
        foreach (var var in Variables)
        {
            if (var.Name == parameter.Item1)
            {
                var.Value = parameter.Item2;
                break;
            }
        }
    }

    private bool isOpened = true;

    private void ExpandButton_Click(object sender, RoutedEventArgs e)
    {
        if (!isOpened)
        {
            ShowInfoPanel.Begin();
            HideInfoPanel.Stop();
            InfoPanelDefinition.Width = new GridLength(350);
            ShowButtonFontIcon.Glyph = "\uE76C";
            ExpandToolTip.Content = localizer.GetLocalizedString("HideConfigSettings");
        }
        else
        {
            ShowInfoPanel.Stop();
            HideInfoPanel.Begin();
            InfoPanelDefinition.Width = new GridLength(0);
            ShowButtonFontIcon.Glyph = "\uE76B";
            ExpandToolTip.Content = localizer.GetLocalizedString("ShowConfigSettings");
        }
        isOpened = !isOpened;
    }

    private string ListDirectory = string.Empty;
    private string BinDirectory = string.Empty;

    private string GetPrettyLookText(string text)
    {
        var result = CreateConfigPageHelper.ApplyPrettyFilesReplacement(text);

        ListDirectory = result.ListDirectory;
        BinDirectory = result.BinDirectory;

        CheckFolderOpenButtonsVisibility();

        return result.ResultText;
    }

    private void AddWrap()
    {
        if (ConfigItem != null)
        {
            string text = CreateConfigPageHelper.ApplyWrappingToString(ConfigItem.startup_string);
            StartupStringTextBox.Text = GetPrettyLookText(text);
            StartupStringTextBox.SetCursorPosition(0, 0);
        }
    }
    private void AddWrap(string startupString)
    {
        string text = CreateConfigPageHelper.ApplyWrappingToString(startupString);
        StartupStringTextBox.Text = GetPrettyLookText(text);
        StartupStringTextBox.SetCursorPosition(0, 0);
    }

    private void HandleSelectionChange(bool showDefaultOnly = false)
    {
        ChangeBackground();
        if (MainPivot.SelectedItem == DefaultDesigner || showDefaultOnly)
        {
            StartupStringTextBox.Visibility = Visibility.Visible;
            GUIDesignerGrid.Visibility = Visibility.Collapsed;
            if (!showDefaultOnly) ConvertDesignerLikeSettingsToString();
        }
        else
        {
            StartupStringTextBox.Visibility = Visibility.Collapsed;
            GUIDesignerGrid.Visibility = Visibility.Visible;
            LoadDesigner();
            ConvertStringToDesignerLikeSettings();
            SearchControl.Close();
        }
    }

    public void ChangeZoom(int zoom)
    {
        if (zoom == 0) StartupStringTextBox.ZoomFactor = 100;
        else StartupStringTextBox.ZoomFactor += zoom;
    }

    public void ChangeBackground(bool showDefaultOnly = false)
    {
        Style style;
        string background = SettingsManager.Instance.GetValue<string>("APPEARANCE", "configEditorBackground");
        if (MainPivot.SelectedItem == DefaultDesigner || showDefaultOnly || MainPivot.SelectedItem == null)
        {
            style = background switch
            {
                "MicaSmoke" => (Style)Resources["MicaSmokeStyle"],
                "Mica" => (Style)Resources["MicaStyle"],
                "MicaTransparent" => (Style)Resources["TransparentStyle"],
                _ => (Style)Resources["BlackStyle"],
            };
        }
        else
        {
            style = (Style)Resources["GrayStyle"];
        }

        if (style == (Style)Resources["BlackStyle"] || style == (Style)Resources["MicaSmokeStyle"])
        {
            CheckHighlight(ElementTheme.Dark);
        }
        else
        {
            CheckHighlight(ElementTheme.Default);
        }

        

        MainGrid.Style = style;
    }

    #region GraphicDesigner

    private void MainPivot_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        HandleSelectionChange();
    }

    private string NowConfigLoadedId = string.Empty;

    private void LoadDesigner()
    {
        if (ComponentChooseComboBox.SelectedItem == null) return;
        string id = ((ComponentModel)ComponentChooseComboBox.SelectedItem).Id;
        if (HardcodedItemIds.ComponentIds[Core.Store.Data.Components.GoodbyeDPI] == id && NowConfigLoadedId != HardcodedItemIds.ComponentIds[Core.Store.Data.Components.GoodbyeDPI])
        {
            GraphicDesignerHelper.LoadGoodbyeDPIDesignerConfig(DesignerSettingItemModels, DesignerExclusiveSettingItemModels);
            NowConfigLoadedId = HardcodedItemIds.ComponentIds[Core.Store.Data.Components.GoodbyeDPI];
        }
        else if (HardcodedItemIds.ComponentIds[Core.Store.Data.Components.SpoofDPI] == id && NowConfigLoadedId != HardcodedItemIds.ComponentIds[Core.Store.Data.Components.SpoofDPI])
        {
            GraphicDesignerHelper.LoadSpoofDPIDesignerConfig(DesignerSettingItemModels, DesignerExclusiveSettingItemModels);
            NowConfigLoadedId = HardcodedItemIds.ComponentIds[Core.Store.Data.Components.SpoofDPI];
        }
        else if (HardcodedItemIds.ComponentIds[Core.Store.Data.Components.NoDPI] == id && NowConfigLoadedId != HardcodedItemIds.ComponentIds[Core.Store.Data.Components.NoDPI])
        {
            string dir = DatabaseHelper.Instance.GetItemById(id)?.Directory?? string.Empty;
            string xmlFile = Path.Combine(dir, "edannotationfile.xml");
            if (File.Exists(xmlFile))
            {
                GraphicDesignerHelper.XML_LoadDesignerConfig(xmlFile, "nodpi", DesignerSettingItemModels, DesignerExclusiveSettingItemModels);
                NowConfigLoadedId = HardcodedItemIds.ComponentIds[Core.Store.Data.Components.NoDPI];
            }
            
        }
    }

    private void HandleGraphicDesignerTextValueChanged(Tuple<string, string> tuple)
    {
        string guid = tuple.Item1;
        string value = tuple.Item2;

        var item = DesignerSettingItemModels.FirstOrDefault(x => x.Guid == guid);
        if (item != null)
        {
            item.Value = value;
        }
        else
        {

            item = DesignerExclusiveSettingItemModels.FirstOrDefault(x => x.Items.FirstOrDefault(y => y.Guid == guid) != null)?.Items.FirstOrDefault(y => y.Guid == guid);
            
            if (item != null)
            {
                item.Value = value;
            }
        }
    }

    private void HandleGraphicDesignerBoolValueToggled(Tuple<string, bool> tuple)
    {
        
        string guid = tuple.Item1;
        bool _value = tuple.Item2;

        var item = DesignerSettingItemModels.FirstOrDefault(x => x.Guid == guid);
        if (item != null)
        {
            item.IsChecked = _value;
        }
        else
        {
            item = DesignerExclusiveSettingItemModels.FirstOrDefault(x => x.Items.FirstOrDefault(y => y.Guid == guid) != null)?.Items.FirstOrDefault(y => y.Guid == guid);
            if (item != null)
            {
                item.IsChecked = _value;
            }
        }
    }

    private void HandleGraphicDesignerSelectedGuidChanged(Tuple<string, string> tuple)
    {
        string guid = tuple.Item1;
        string selGuid = tuple.Item2;
        var item = DesignerExclusiveSettingItemModels.FirstOrDefault(x => x.Guid == guid);
        if (item != null)
        {
            Debug.WriteLine(selGuid);
            item.SelectedItemGuid = selGuid;
        }
    }

    private void ConvertStringToDesignerLikeSettings()
    {
        StartupStringTextBox.Text = StartupStringTextBox.Text.Replace("\n", " ");
        AdditionalSettingsTextBox.Text = GraphicDesignerHelper.ConvertStringToGraphicDesignerSettings(DesignerSettingItemModels, DesignerExclusiveSettingItemModels, StartupStringTextBox.Text);
    }
    private void ConvertDesignerLikeSettingsToString()
    {
        if (ComponentChooseComboBox.SelectedItem != null && !DesignerSupportedComponentIds.Contains(((ComponentModel)ComponentChooseComboBox.SelectedItem).Id))
        {
            return;
        }

        StartupStringTextBox.Text = GraphicDesignerHelper.ConvertGraphicDesignerSettingsToString(DesignerSettingItemModels, DesignerExclusiveSettingItemModels, AdditionalSettingsTextBox.Text);
        StartupStringTextBox.Text = StartupStringTextBox.Text.Replace("\n", " ");
    }

    #endregion

    #region OpenConfigFilesFolder

    private void CheckFolderOpenButtonsVisibility()
    {
        OpenBinsConfigDirectory.Visibility = Visibility.Collapsed;
        OpenListsConfigDirectory.Visibility = Visibility.Collapsed;
        if (!string.IsNullOrEmpty(ListDirectory) && ConfigItem != null)
        {
            OpenListsConfigDirectory.Visibility = Visibility.Visible;
        }
        if (!string.IsNullOrEmpty(BinDirectory) && ConfigItem != null)
        {
            OpenBinsConfigDirectory.Visibility = Visibility.Visible;
        }
    }

    private void OpenListsConfigDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(ListDirectory) && ConfigItem != null)
        {
            ShellHelper.LookupDirectory(LScriptLangHelper.ExecuteScript(ListDirectory, callItemId: SharedConstants.LocalUserItemsId));
        }
    }

    private void OpenBinsConfigDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(BinDirectory) && ConfigItem != null)
        {
            ShellHelper.LookupDirectory(LScriptLangHelper.ExecuteScript(BinDirectory, callItemId: SharedConstants.LocalUserItemsId));
        }
    }
    #endregion

    private async void ShowErrorDialog(string message, string title)
    {
        var dlg = new MessageDialog(message, title);
        InitializeWithWindow.Initialize(
            dlg, WindowNative.GetWindowHandle(await ((App)Application.Current).SafeCreateNewWindow<CreateConfigHelperWindow>())
            );
        await dlg.ShowAsync();
    }
}
