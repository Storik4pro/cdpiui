using CDPI_UI.Controls.Dialogs.CreateConfigHelper;
using CDPI_UI.Helper;
using CDPI_UI.Helper.Items;
using CDPI_UI.Helper.LScript;
using CDPI_UI.Helper.Static;
using CDPI_UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Unidecode.NET;
using Windows.ApplicationModel.Chat;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Views.CreateConfigHelper;

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
public enum AskAutoFillMode
{
    Ask,
    Qiet
}

public sealed partial class CreateNewConfigPage : Page
{
    
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

    private object navigationParameter;

    private ConfigItem ConfigItem;

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

        InitPage();

    }

    private void CreateNewConfigPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (navigationParameter != null && navigationParameter is Tuple<string, ConfigItem, bool, string> tuple)
        {
            LoadVars(tuple.Item2);
            
            AskAutoFillFiles(tuple.Item2, tuple.Item4);
        }
        if (navigationParameter != null && navigationParameter is Tuple<string, ConfigItem> editCfgTuple)
        {
            if (editCfgTuple.Item2.packId != StateHelper.LocalUserItemsId)
            {
                AskAutoFillFiles(
                    editCfgTuple.Item2,
                    LScriptLangHelper.ExecuteScript("$GETCURRENTDIR()", callItemId: editCfgTuple.Item2.packId), AskAutoFillMode.Qiet);
            }
        }
        if (navigationParameter != null && navigationParameter is Tuple<string, string, string> createNewFromString)
        {
            ConfigItem = new()
            {
                startup_string = createNewFromString.Item2,
            };
            AskAutoFillFiles(
                    ConfigItem,
                    LScriptLangHelper.ExecuteScript("$GETCURRENTDIR()", callItemId: StateHelper.LocalUserItemsId), AskAutoFillMode.Qiet);
        }
        AuditSaveAvailable();
        navigationParameter = null;
        this.Loaded -= CreateNewConfigPage_Loaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is Tuple<string, ConfigItem, bool, string> tuple)
        {
            string operationType = tuple.Item1;
            ConfigItem = tuple.Item2;
            bool errorHappens = tuple.Item3;

            PageTitleTextBlock.Text = "Import config from file";
            if (ConfigItem.target != null)
            {
                ComponentChooseComboBox.SelectedItem = ComponentChooseComboBox.Items
                    .Cast<ComponentModel>()
                    .FirstOrDefault(c => c.Id == ConfigItem.target[0]);
            }

            StartupStringTextBox.Text = ConfigItem.startup_string;

            navigationParameter = tuple;
        }
        else if (e.Parameter is Tuple<string, ConfigItem> editCfgTuple)
        {
            string operationType = editCfgTuple.Item1;
            ConfigItem = editCfgTuple.Item2;

            navigationParameter = editCfgTuple;

            if (operationType == "CFGEDIT")
            {
                PageTitleTextBlock.Text = "Edit config";
                if (ConfigItem.target != null)
                {
                    ComponentChooseComboBox.SelectedItem = ComponentChooseComboBox.Items
                        .Cast<ComponentModel>()
                        .FirstOrDefault(c => c.Id == ConfigItem.target[0]);
                }
                ComponentChooseComboBox.IsEnabled = false;


                if (ConfigItem.packId != StateHelper.LocalUserItemsId)
                {
                    DisplayNameTextBox.Text = $"{ConfigItem.name} (edited)";
                    SaveButtonText.Text = "Save as a copy";
                }
                else
                {
                    DisplayNameTextBox.Text = $"{ConfigItem.name}";
                }

                LoadVars(ConfigItem);
                LoadConditions(ConfigItem);
                
                StartupStringTextBox.Text = ConfigItem.startup_string;
            }
        }
        else if (e.Parameter is Tuple<string, string, string> createNewFromString)
        {
            navigationParameter = createNewFromString;

            ComponentChooseComboBox.SelectedItem = ComponentChooseComboBox.Items
                    .Cast<ComponentModel>()
                    .FirstOrDefault(c => c.Id == createNewFromString.Item3);

            if (createNewFromString.Item1 == "CFGSTRING")
            {
                StartupStringTextBox.Text = createNewFromString.Item2;
            }
        }
        else if (e.Parameter is Tuple<string, string> createNewConfig)
        {
            navigationParameter = createNewConfig;

            ComponentChooseComboBox.SelectedItem = ComponentChooseComboBox.Items
                    .Cast<ComponentModel>()
                    .FirstOrDefault(c => c.Id == createNewConfig.Item2);
        }
    }

    private async void AskAutoFillFiles(ConfigItem configItem, string dir, AskAutoFillMode askAutoFillMode = AskAutoFillMode.Ask)
    {
        List<string> usedFiles = ConfigHelper.GetUsedFilesFromConfigItem(configItem);

        SelectUsedFilesForConfigContentDialog dialog = new(usedFiles, configItem.name, dir, configItem, askAutoFillMode)
        {
            XamlRoot = this.XamlRoot
        };
        var result = await dialog.ShowAsync();
        if (dialog.Result == CreateConfigResult.Selected)
        {
            ConfigItem = ConfigHelper.ReplaceFilesPath(ConfigItem, dialog.Files);
            StartupStringTextBox.Text = ConfigItem.startup_string;

            LoadVars(configItem);
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

        List<VariableItem> variables = ConfigHelper.GetVariables(configItem);
        ComponentHelper componentHelper = 
            ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId((ComponentChooseComboBox.SelectedItem as ComponentModel).Id);

        foreach (var variable in configItem.variables) 
        {
            Debug.WriteLine(variable);
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
                    Description = componentHelper.GetConfigHelper().GetLocalizedConfigVarName(_var, configItem.packId)
                });
        }
    }

    private void LoadVars(ConfigItem configItem)
    {
        Variables.Clear();
        if (configItem.commaVars == null || configItem.commaVars.Count == 0)
        {
            VariablesStackPanel.Visibility = Visibility.Collapsed;
            return;
        }

        foreach (var commaVar in configItem.commaVars)
        {
            try
            {
                Variables.Add(new VariableModel
                {
                    Name = commaVar.Key,
                    Value = commaVar.Value,
                    AvailableValues = configItem.availableCommaVarsValues != null ?
                    configItem.availableCommaVarsValues.FirstOrDefault(varItem => varItem.VarName == commaVar.Key, null) :
                    null
                });
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateWarningLog(nameof(CreateNewConfigPage), $"{ex}");
            }
        }
        VariablesStackPanel.Visibility = Visibility.Visible;
    }

    private static bool IsBasicLetter(char c)
    {
        return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
    }

    private bool IsNameCorrect(string text)
    {
        
        if (string.IsNullOrWhiteSpace(text))
            return false;

        bool flag = true;

        foreach (char _char in text)
        {
            if (!IsBasicLetter(_char))
            {
                flag = false;
                break;
            }
        }
        return flag;
    
    }

    private void InitPage()
    {
        List<ComponentModel> components = new();
        foreach (var component in StateHelper.Instance.ComponentIdPairs)
        {
            if (component.Key == "ASGKOI001")
                continue;
            components.Add(new() 
            { 
                Id = component.Key,
                Name = component.Value
            });
        }
        ComponentChooseComboBox.ItemsSource = components;

        AuditSaveAvailable();
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
        TestButton.IsEnabled = state;
    }

    private bool IsVarAddAvailable()
    {
        bool result = IsNameCorrect(VarNameTextBox.Text.Replace(" ", "")) &&
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
            ConditionPreviewTextBlock.Text = "Nothing to preview";
            return;
        }
        ConditionPreviewTextBlock.Text = $"{VarNameTextBox.Text.Replace(" ", "")}==true ? {OnValueTextBox.Text} : {OffValueTextBox.Text}";
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
        ConditionPreviewTextBlock.Text = "Nothing to preview";
    }

    private Tuple<Dictionary<string, bool>, List<string>> CreateVariables(int secondsSinceEpoch)
    {
        Dictionary<string, bool> jparams = new();
        List<string> vars = new();

        try
        {

            string localAppData = StateHelper.GetDataDirectory();
            string locFile = Path.Combine(
                localAppData,
                StateHelper.StoreDirName,
                StateHelper.StoreItemsDirName,
                StateHelper.LocalUserItemsId,
                StateHelper.LocalUserItemLocFolder,
                "strings.json");

            if (!Directory.Exists(Path.GetDirectoryName(locFile)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(locFile));
            }

            if (!File.Exists(locFile))
                File.Create(locFile);

            Dictionary<string, string> localizationDict = Utils.LoadJson<Dictionary<string, string>>(locFile);
            if (localizationDict == null) localizationDict = new();

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
            // open error window
            Logger.Instance.CreateWarningLog(nameof(CreateNewConfigPage), $"{ex}");
            return new(null, null);
        }
    }
    private ConfigItem CreateConfig(int secondsSinceEpoch)
    {
        string componentId = (ComponentChooseComboBox.SelectedItem as ComponentModel).Id;
        var (jparams, vars) = CreateVariables(secondsSinceEpoch);
        Debug.WriteLine(jparams);
        Debug.WriteLine(vars);
        ConfigItem configItem = new()
        {
            meta = "UC:v1.0",
            packId = StateHelper.LocalUserItemsId,
            not_converted_name = DisplayNameTextBox.Text,
            target = [componentId, DatabaseHelper.Instance.GetItemById(componentId).CurrentVersion],
            jparams = jparams,
            variables = vars,
            commaVars = Variables.ToDictionary(v => v.Name, v => v.Value),
            availableCommaVarsValues = Variables.Where(v => v.AvailableValues != null).Select(v => v.AvailableValues).ToList(),
            startup_string = StartupStringTextBox.Text
        };
        foreach (var _v in configItem.commaVars)
        {
            Logger.Instance.CreateDebugLog(nameof(CreateNewConfigPage), $">>>{_v}");
        }
        return configItem;
    }

    private bool isRunned = false;

    private void Instance_onProcessStateChanged(string state)
    {
        if (state == "started")
        {
            TestButtonGlyph.Glyph = "\uE71A";
            TestButtonText.Text = "Stop testing";
            isRunned = true;
        }
        else
        {
            TestButtonGlyph.Glyph = "\uE768";
            TestButtonText.Text = "Test this";
            isRunned = false;
        }
    }

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        if (!isRunned)
        {
            isRunned = true;
            ProcessManager.Instance.onProcessStateChanged += Instance_onProcessStateChanged;

            await ProcessManager.Instance.StopProcess();
            ComponentModel model = ComponentChooseComboBox.SelectedItem as ComponentModel;
            await ProcessManager.Instance.StartProcess(model.Id, ConfigHelper.GetStartupParametersByConfigItem(CreateConfig(0)));
            
        }
        else
        {
            await ProcessManager.Instance.StopProcess();
            TestButtonGlyph.Glyph = "\uE768";
            TestButtonText.Text = "Test this";
            isRunned = false;
            ProcessManager.Instance.onProcessStateChanged -= Instance_onProcessStateChanged;
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
        TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
        int secondsSinceEpoch = (int)t.TotalSeconds;

        ConfigItem configItem = CreateConfig(secondsSinceEpoch);

        string src;
        if (ConfigItem == null || ConfigItem.packId != StateHelper.LocalUserItemsId)
            src = DisplayNameTextBox.Text + $"_{secondsSinceEpoch}.json";
        else
            src = ConfigItem.file_name;

        string transl = src.Unidecode();
        transl = Regex.Replace(transl, @"\s+", "_");
        transl = Regex.Replace(transl, @"[^A-Za-z0-9_\.-]", "");
        transl = Regex.Replace(transl, "_+", "_").Trim('_');

        await ConfigHelper.SaveConfigItem(transl, StateHelper.LocalUserItemsId, configItem);

        ComponentHelper componentHelper =
                ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(
                    (ComponentChooseComboBox.SelectedItem as ComponentModel).Id);
        componentHelper.ReInitConfigs();

        ShowDialog();
    }

    private void ComponentChooseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        CreateConfigHelperWindow.Instanse.IsOperationExitAskAvailable = true;
        AuditSaveAvailable();
    }

    private void DisplayNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        CreateConfigHelperWindow.Instanse.IsOperationExitAskAvailable = true;
        AuditSaveAvailable();
    }

    private void StartupStringTextBox_TextChanged(object sender, TextChangedEventArgs e)
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
}
