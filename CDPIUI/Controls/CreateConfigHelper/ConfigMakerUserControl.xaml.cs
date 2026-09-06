using CDPIUI.AddOns.ConfigImport;
using CDPIUI.AddOns.ConfigShare;
using CDPIUI.Controls.Dialogs.ComponentSettings;
using CDPIUI.Controls.Dialogs.CreateConfigHelper;
using CDPIUI.Controls.Universal;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.Data;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.System;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Helper.UserExperience;
using CDPIUI.Shared;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.ViewModels;
using CDPIUI.Views.CreateConfigHelper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using TextControlBoxNS;
using Unidecode.NET;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.UI;
using WinUI3Localizer;
using ParsedPresetOption = CDPIUI.Core.ComponentServices.Helpers.Configuration.ConfigCommandOption;

namespace CDPIUI.Controls.CreateConfigHelper;

public sealed class ConfigMakerCommandOptionViewModel
{
    public ComponentCommandHelpOption Source { get; set; } = new();
    public string DisplayName => Source.DisplayName;
    public string Description { get; set; } = string.Empty;
}

public sealed class ConfigMakerCommandModuleViewModel
{
    public string GroupName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsAll { get; set; }
}

public sealed class ConfigMakerDiagnosticViewModel
{
    public ComponentCommandDiagnostic Source { get; set; } = new();
    public string SeverityText { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Brush SeverityBrush => new SolidColorBrush(Source.Severity == ComponentCommandDiagnosticSeverity.Error
        ? Color.FromArgb(255, 196, 43, 28)
        : Color.FromArgb(255, 202, 80, 16));
}

public sealed class ConfigMakerPresetTreeItemViewModel
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int SourceIndex { get; init; } = -1;
    public int SourceLength { get; init; }
    public int SourceLine { get; init; }
    public int SourceColumn { get; init; }
    public Visibility DescriptionVisibility => string.IsNullOrWhiteSpace(Description)
        ? Visibility.Collapsed
        : Visibility.Visible;
    public Thickness ContentMargin => DescriptionVisibility == Visibility.Visible
        ? new Thickness(2, 4, 8, 4)
        : new Thickness(2, 0, 8, 0);
    public FontFamily TitleFontFamily { get; init; } = new("Segoe UI");
}

public sealed record ConfigMakerPresetFileInfo(
    string Name,
    string Path,
    string Folder,
    ConfigMakerPresetFileKind Kind = ConfigMakerPresetFileKind.SiteList,
    string OptionName = "",
    bool IsAttachedResource = false);

public enum ConfigMakerPresetFileKind
{
    SiteList,
    Library,
    Payload,
}

public sealed class ConfigMakerPresetFileTreeItem
{
    public string DisplayName { get; init; } = string.Empty;
    public string ToolTip { get; init; } = string.Empty;
    public ConfigMakerPresetFileInfo File { get; init; }
    public bool IsMissing { get; init; }
    public Brush Background => IsMissing
        ? new SolidColorBrush(Color.FromArgb(28, 255, 185, 0))
        : null;
    public Visibility ActionsVisibility => File == null
        ? Visibility.Collapsed
        : Visibility.Visible;
    public Visibility ExistingActionsVisibility => File != null && !IsMissing
        ? Visibility.Visible
        : Visibility.Collapsed;
    public Visibility MissingVisibility => File != null && IsMissing
        ? Visibility.Visible
        : Visibility.Collapsed;
    public Visibility RemoveVisibility => File?.IsAttachedResource == true
        ? Visibility.Visible
        : Visibility.Collapsed;
}

public sealed record ConfigMakerPresetGroupInfo(
    string Name,
    IReadOnlyList<string> Details);

public sealed class ConfigMakerPresetFileReplacedEventArgs(
    string commandText,
    string originalPath,
    string replacementPath) : EventArgs
{
    public string CommandText { get; } = commandText;
    public string OriginalPath { get; } = originalPath;
    public string ReplacementPath { get; } = replacementPath;
}

public sealed partial class ConfigMakerUserControl : UserControl
{
    public static readonly DependencyProperty ComponentIdProperty = DependencyProperty.Register(
        nameof(ComponentId),
        typeof(string),
        typeof(ConfigMakerUserControl),
        new PropertyMetadata(string.Empty, OnComponentIdChanged));

    public static readonly DependencyProperty CommandTextProperty = DependencyProperty.Register(
        nameof(CommandText),
        typeof(string),
        typeof(ConfigMakerUserControl),
        new PropertyMetadata(string.Empty, OnCommandTextChanged));

    public static readonly DependencyProperty IsEditorReadOnlyProperty = DependencyProperty.Register(
        nameof(IsEditorReadOnly),
        typeof(bool),
        typeof(ConfigMakerUserControl),
        new PropertyMetadata(false, OnIsEditorReadOnlyChanged));

    public static readonly DependencyProperty IsErrorCheckEnabledProperty = DependencyProperty.Register(
        nameof(IsErrorCheckEnabled),
        typeof(bool),
        typeof(ConfigMakerUserControl),
        new PropertyMetadata(true, OnIsErrorCheckEnabled));

    private readonly ILocalizer localizer = Localizer.Get();
    private readonly ComponentCommandHelpService helpService = new();
    private readonly ConfigMakerPresetDocument presetDocument = new();
    private ComponentCommandHelpDocument helpDocument = new();
    private CancellationTokenSource helpCancellation;
    private ConfigMakerCommandOptionViewModel selectedCommand;
    private bool updatingComponent;
    private bool updatingEditor;
    private bool isTesting;
    private bool isStartingTest;
    private bool restoreComponentAfterTest;
    private string testComponentId = string.Empty;
    private bool layoutInitialized;
    private bool updatingLayoutBounds;
    private bool hasPresetFiles;
    private bool hasPresetGroups;
    private bool usesExplicitPresetStructure;
    private ConfigMakerPresetFileInfo[] explicitPresetFiles = [];
    private ConfigMakerPresetGroupInfo[] explicitPresetGroups = [];
    private double savedEditorWidth = 620;
    private double savedEditorHeight = 420;
    private string highlightSignature = string.Empty;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer diagnosticsTimer;
    private string editorBackground = "Black";
    private bool updatingDesigner;
    private bool designerNeedsRefresh = true;
    private string presetBaseDirectory = string.Empty;

    public ICommand ZoomIn { get; }
    public ICommand ZoomOut { get; }
    public ICommand ZoomReset { get; }
    public ICommand Search { get; }
    public ICommand Replace { get; }
    public ICommand DesignerTextValueChangedCommand { get; }
    public ICommand DesignerBoolValueToggledCommand { get; }
    public ICommand DesignerSelectedGuidChangedCommand { get; }

    public ConfigMakerUserControl()
    {
        DesignerTextValueChangedCommand = new RelayCommand(parameter =>
            HandleDesignerTextValueChanged((Tuple<string, string>)parameter));
        DesignerBoolValueToggledCommand = new RelayCommand(parameter =>
            HandleDesignerBoolValueToggled((Tuple<string, bool>)parameter));
        DesignerSelectedGuidChangedCommand = new RelayCommand(parameter =>
            HandleDesignerSelectedGuidChanged((Tuple<string, string>)parameter));
        InitializeComponent();

        ZoomIn = new RelayCommand(p => ChangeZoom(5));
        ZoomOut = new RelayCommand(p => ChangeZoom(-5));
        ZoomReset = new RelayCommand(p => ChangeZoom(0));

        Search = new RelayCommand(p => EditorSearchControl.ShowSearch(CommandEditor));
        Replace = new RelayCommand(p => EditorSearchControl.ShowReplace(CommandEditor));

        CommandEditor.UseSpacesInsteadTabs = true;
        CommandEditor.NumberOfSpacesForTab = 4;
        diagnosticsTimer = DispatcherQueue.CreateTimer();
        diagnosticsTimer.Interval = TimeSpan.FromMilliseconds(300);
        diagnosticsTimer.IsRepeating = false;
        diagnosticsTimer.Tick += DiagnosticsTimer_Tick;
        presetDocument.ContentChanged += PresetDocument_ContentChanged;
        ApplyEditorTheme();

        Loaded += ConfigMakerUserControl_Loaded;
        Unloaded += ConfigMakerUserControl_Unloaded;
        ActualThemeChanged += ConfigMakerUserControl_ActualThemeChanged;
    }

    public ObservableCollection<ConfigMakerCommandOptionViewModel> CommandOptions { get; } = [];
    public ObservableCollection<ConfigMakerCommandModuleViewModel> CommandModules { get; } = [];
    public ObservableCollection<ConfigMakerDiagnosticViewModel> Diagnostics { get; } = [];
    public ObservableCollection<GraphicDesignerSettingItemModel> DesignerSettingItemModels { get; } = [];
    public ObservableCollection<GraphicDesignerExclusiveSettingItemModel> DesignerExclusiveSettingItemModels { get; } = [];
    public ObservableCollection<ConfigMakerVariableDefinition> PresetVariables => presetDocument.Variables;
    public ObservableCollection<ConfigMakerPresetResource> PresetResources => presetDocument.Resources;
    public ConfigMakerPresetDocument PresetDocument => presetDocument;



    public string ComponentId
    {
        get => (string)GetValue(ComponentIdProperty);
        set => SetValue(ComponentIdProperty, value);
    }

    public string CommandText
    {
        get => (string)GetValue(CommandTextProperty);
        set => SetValue(CommandTextProperty, value);
    }

    public bool IsEditorReadOnly
    {
        get => (bool)GetValue(IsEditorReadOnlyProperty);
        set => SetValue(IsEditorReadOnlyProperty, value);
    }

    public bool IsErrorCheckEnabled
    {
        get => (bool)GetValue(IsErrorCheckEnabledProperty);
        set => SetValue(IsErrorCheckEnabledProperty, value);
    }

    public bool UseInlineStatusMessages { get; set; } = true;

    public event Action<string> CommandTextChanged;
    public event EventHandler TestStateChanged;
    public event EventHandler EditorReadOnlyChanged;
    public event EventHandler PanelStateChanged;
    public event EventHandler<ConfigMakerPresetFileReplacedEventArgs> PresetFileReplaced;
    public event EventHandler<StatusNotificationRequestedEventArgs> StatusNotificationRequested;
    public event EventHandler DocumentStateChanged;

    public bool IsTesting => isTesting;
    public bool IsCommandPanelVisible => CommandPanel.Visibility == Visibility.Visible;
    public bool IsBottomPanelVisible => BottomPanel.Visibility == Visibility.Visible;
    public bool IsPresetFilesPanelVisible => PresetFilesPanel.Visibility == Visibility.Visible;
    public bool IsPresetStructureVisible => hasPresetFiles || HasPresetGroups;
    public bool HasPresetFiles => hasPresetFiles;
    public bool HasPresetGroups => hasPresetGroups || !usesExplicitPresetStructure;
    public bool HasVariables => presetDocument.HasVariables;
    public bool CanExportText => !HasVariables;
    public bool IsSimpleDesignerSupported => IsSimpleDesignerComponent(ComponentId);

    public void SetPresetStructure(
        IEnumerable<ConfigMakerPresetFileInfo> files,
        IEnumerable<ConfigMakerPresetGroupInfo> groups)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(groups);

        explicitPresetFiles = files
            .Where(file => !string.IsNullOrWhiteSpace(file.Path))
            .DistinctBy(file => (file.Kind, file.Path), PresetFileKeyComparer.Instance)
            .ToArray();
        explicitPresetGroups = groups.ToArray();
        usesExplicitPresetStructure = true;
        RebuildPresetStructure();
        SetPresetFilesPanelVisible(hasPresetFiles);
    }

    public void ClearPresetStructure()
    {
        usesExplicitPresetStructure = false;
        explicitPresetFiles = [];
        explicitPresetGroups = [];
        RebuildPresetStructure();
    }

    private void RebuildPresetStructure()
    {
        bool filesPanelWasVisible = IsPresetFilesPanelVisible;
        ConfigMakerPresetFileInfo[] detectedFiles = ExtractPresetFiles(CommandText).ToArray();
        ConfigMakerPresetFileInfo[] baseFiles = usesExplicitPresetStructure
            ? EnrichExplicitPresetFiles(explicitPresetFiles, detectedFiles)
            : detectedFiles;
        ConfigMakerPresetFileInfo[] attachedFiles = presetDocument.Resources
            .Select(resource => new ConfigMakerPresetFileInfo(
                resource.Alias,
                resource.Reference,
                Path.GetDirectoryName(resource.Path) ?? string.Empty,
                ToPresetFileKind(resource.Kind),
                IsAttachedResource: true))
            .ToArray();
        ConfigMakerPresetFileInfo[] files = baseFiles
            .Concat(attachedFiles)
            .DistinctBy(file => (file.Kind, file.Path), PresetFileKeyComparer.Instance)
            .ToArray();

        RebuildPresetFilesTree(files);
        RebuildPresetGroupsTree(ParseCommandOptions(CommandText));

        bool filesAvailabilityChanged = hasPresetFiles != (files.Length > 0);
        bool groupsAvailabilityChanged = hasPresetGroups != (PresetGroupsTreeView.RootNodes.Count > 0);
        hasPresetFiles = files.Length > 0;
        hasPresetGroups = PresetGroupsTreeView.RootNodes.Count > 0;

        UpdatePresetGroupsTab();
        if (filesAvailabilityChanged || groupsAvailabilityChanged)
        {
            PanelStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private ConfigMakerPresetFileInfo[] EnrichExplicitPresetFiles(
        IReadOnlyList<ConfigMakerPresetFileInfo> files,
        IReadOnlyList<ConfigMakerPresetFileInfo> detectedFiles)
    {
        return files
            .Select(file =>
            {
                ConfigMakerPresetFileInfo detected = detectedFiles.FirstOrDefault(candidate =>
                    candidate.Kind == file.Kind && PresetPathsEqual(candidate.Path, file.Path));
                return detected == null || !string.IsNullOrWhiteSpace(file.OptionName)
                    ? file
                    : file with { OptionName = detected.OptionName };
            })
            .ToArray();
    }

    private void RebuildPresetFilesTree(IReadOnlyList<ConfigMakerPresetFileInfo> fileItems)
    {
        PresetFilesTreeView.RootNodes.Clear();
        foreach (ConfigMakerPresetFileKind kind in Enum.GetValues<ConfigMakerPresetFileKind>())
        {
            ConfigMakerPresetFileInfo[] categoryFiles = fileItems
                .Where(file => file.Kind == kind)
                .OrderBy(file => file.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
            if (categoryFiles.Length == 0)
            {
                continue;
            }

            TreeViewNode categoryNode = new()
            {
                Content = new ConfigMakerPresetFileTreeItem
                {
                    DisplayName = PresetFileCategoryName(kind),
                    ToolTip = PresetFileCategoryName(kind),
                },
                IsExpanded = true,
            };
            foreach (ConfigMakerPresetFileInfo file in categoryFiles)
            {
                string resolvedPath = TryResolvePresetFilePath(file.Path);
                bool isMissing = string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath);
                categoryNode.Children.Add(new TreeViewNode
                {
                    Content = new ConfigMakerPresetFileTreeItem
                    {
                        DisplayName = file.Name,
                        ToolTip = isMissing
                            ? string.Format(
                                localizer.GetLocalizedString("ConfigMakerPresetFileMissingMessage"),
                                resolvedPath ?? file.Path)
                            : file.IsAttachedResource
                                ? $"{file.Path}{Environment.NewLine}{resolvedPath}"
                                : resolvedPath,
                        File = file,
                        IsMissing = isMissing,
                    },
                });
            }
            PresetFilesTreeView.RootNodes.Add(categoryNode);
        }
    }

    private void RebuildPresetGroupsTree(IReadOnlyList<ParsedPresetOption> options)
    {
        PresetGroupsTreeView.RootNodes.Clear();
        if (options.Count == 0)
        {
            AddExplicitPresetGroupsFallback();
            return;
        }

        int firstProfileOption = options
            .Select((option, index) => new { option, index })
            .Where(item => IsProfileOption(item.option.Name))
            .Select(item => item.index)
            .DefaultIfEmpty(options.Count)
            .First();
        IReadOnlyList<ParsedPresetOption> common = options.Take(firstProfileOption).ToArray();
        AddPresetOptionGroup(
            localizer.GetLocalizedString("ConfigMakerPresetCommonFiltersGroup"),
            common.Where(option => IsCommonFilter(option.Name)),
            "ConfigMakerZapret2CommonFiltersDescription");
        AddPresetOptionGroup(
            localizer.GetLocalizedString("ConfigMakerPresetCommonOptionsGroup"),
            common.Where(option => !IsCommonFilter(option.Name)),
            "ConfigMakerZapret2CommonOptionsDescription");

        List<List<ParsedPresetOption>> profiles = [];
        List<ParsedPresetOption> current = [];
        foreach (ParsedPresetOption option in options.Skip(firstProfileOption))
        {
            if (string.Equals(option.Name, "--new", StringComparison.OrdinalIgnoreCase))
            {
                if (current.Count > 0)
                {
                    profiles.Add(current);
                    current = [];
                }
                continue;
            }
            current.Add(option);
        }
        if (current.Count > 0)
        {
            profiles.Add(current);
        }

        for (int index = 0; index < profiles.Count; index++)
        {
            AddPresetProfileGroup(profiles[index], index);
        }

        if (PresetGroupsTreeView.RootNodes.Count == 0)
        {
            AddExplicitPresetGroupsFallback();
        }
    }

    private void AddPresetProfileGroup(IReadOnlyList<ParsedPresetOption> options, int profileIndex)
    {
        string parsedName = options
            .FirstOrDefault(option => string.Equals(option.Name, "--name", StringComparison.OrdinalIgnoreCase))
            ?.Value;
        string explicitName = profileIndex < explicitPresetGroups.Length
            ? explicitPresetGroups[profileIndex].Name
            : string.Empty;
        string name = !string.IsNullOrWhiteSpace(explicitName)
            ? explicitName
            : !string.IsNullOrWhiteSpace(parsedName)
                ? UnquoteOptionValue(parsedName)
                : string.Format(
                    localizer.GetLocalizedString("ConfigMakerPresetProfileFallbackName"),
                    profileIndex + 1);

        TreeViewNode profileNode = new()
        {
            Content = CreatePresetTreeItem(
                name,
                GetZapret2Description("ConfigMakerZapret2ProfileDescription")),
            IsExpanded = true,
        };
        TreeViewNode filtersNode = CreateOptionNode(
            localizer.GetLocalizedString("ConfigMakerPresetProfileFiltersGroup"),
            options.Where(option => IsProfileFilter(option.Name)),
            "ConfigMakerZapret2ProfileFiltersDescription");
        TreeViewNode actionsNode = CreateOptionNode(
            localizer.GetLocalizedString("ConfigMakerPresetProfileActionsGroup"),
            options.Where(option => !IsProfileFilter(option.Name)),
            "ConfigMakerZapret2ProfileActionsDescription");
        if (filtersNode != null)
        {
            profileNode.Children.Add(filtersNode);
        }
        if (actionsNode != null)
        {
            profileNode.Children.Add(actionsNode);
        }
        if (profileNode.Children.Count > 0)
        {
            PresetGroupsTreeView.RootNodes.Add(profileNode);
        }
    }

    private void AddPresetOptionGroup(
        string name,
        IEnumerable<ParsedPresetOption> options,
        string descriptionResourceKey)
    {
        TreeViewNode node = CreateOptionNode(name, options, descriptionResourceKey);
        if (node != null)
        {
            PresetGroupsTreeView.RootNodes.Add(node);
        }
    }

    private TreeViewNode CreateOptionNode(
        string name,
        IEnumerable<ParsedPresetOption> options,
        string descriptionResourceKey)
    {
        ParsedPresetOption[] items = options.ToArray();
        if (items.Length == 0)
        {
            return null;
        }
        TreeViewNode node = new()
        {
            Content = CreatePresetTreeItem(name, GetZapret2Description(descriptionResourceKey)),
            IsExpanded = true,
        };
        foreach (ParsedPresetOption option in items)
        {
            node.Children.Add(new TreeViewNode
            {
                Content = CreatePresetTreeItem(
                    option.DisplayText,
                    GetPresetOptionDescription(option),
                    isCode: true,
                    source: option),
            });
        }
        return node;
    }

    private ConfigMakerPresetTreeItemViewModel CreatePresetTreeItem(
        string title,
        string description = "",
        bool isCode = false,
        ParsedPresetOption source = null) => new()
        {
            Title = title ?? string.Empty,
            Description = description ?? string.Empty,
            TitleFontFamily = new FontFamily(isCode ? ConsoleFontHelper.Instance.FontFamily.Source.ToString() : "Segoe UI"),
            SourceIndex = source?.SourceIndex ?? -1,
            SourceLength = source?.SourceLength ?? 0,
            SourceLine = source?.SourceLine ?? 0,
            SourceColumn = source?.SourceColumn ?? 0,
        };

    private string GetZapret2Description(string resourceKey) => IsZapret2Component &&
        !string.IsNullOrWhiteSpace(resourceKey)
            ? localizer.GetLocalizedString(resourceKey)
            : string.Empty;

    private string GetPresetOptionDescription(ParsedPresetOption option)
    {
        if (!IsZapret2Component)
        {
            return string.Empty;
        }

        List<string> descriptions = [];
        string resourceKey = Zapret2OptionDescriptionProvider.GetOptionResourceKey(option.Name);
        string contextualDescription = GetZapret2ContextualOptionDescription(option);
        if (!string.IsNullOrWhiteSpace(contextualDescription))
        {
            descriptions.Add(contextualDescription);
        }
        else if (!string.IsNullOrWhiteSpace(resourceKey))
        {
            descriptions.Add(localizer.GetLocalizedString(resourceKey));
            string value = GetReadableOptionValue(option.Value);
            if (!string.IsNullOrWhiteSpace(value))
            {
                descriptions.Add(string.Format(
                    localizer.GetLocalizedString("ConfigMakerZapret2CurrentValueDescription"),
                    value));
            }
        }
        if (string.Equals(option.Name, "--lua-desync", StringComparison.OrdinalIgnoreCase))
        {
            string functionResourceKey = Zapret2OptionDescriptionProvider
                .GetLuaFunctionResourceKey(option.Value);
            if (!string.IsNullOrWhiteSpace(functionResourceKey))
            {
                descriptions.Add(localizer.GetLocalizedString(functionResourceKey));
            }
        }

        if (descriptions.Count == 0)
        {
            string helpDescription = helpDocument.Options
                .FirstOrDefault(item => item.Matches(option.Name))
                ?.Description
                ?.Trim()
                .TrimStart(';')
                .Trim();
            descriptions.Add(string.IsNullOrWhiteSpace(helpDescription)
                ? localizer.GetLocalizedString("ConfigMakerZapret2NoFlagDescription")
                : helpDescription);
        }
        return string.Join(' ', descriptions.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private string GetZapret2ContextualOptionDescription(ParsedPresetOption option)
    {
        string value = GetReadableOptionValue(option.Value);
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string optionName = option.Name.ToLowerInvariant();
        return optionName switch
        {
            "--filter-l3" => string.Format(
                localizer.GetLocalizedString("ConfigMakerZapret2FilterL3SpecificDescription"),
                NormalizeIpVersion(value)),
            "--wf-l3" => string.Format(
                localizer.GetLocalizedString("ConfigMakerZapret2WinDivertL3SpecificDescription"),
                NormalizeIpVersion(value)),
            "--filter-tcp" => FormatTransportFilterDescription("TCP", value),
            "--filter-udp" => FormatTransportFilterDescription("UDP", value),
            "--filter-l7" => string.Format(
                localizer.GetLocalizedString("ConfigMakerZapret2FilterL7SpecificDescription"),
                FormatListValue(value)),
            "--hostlist" => FormatSpecificDescription(
                "ConfigMakerZapret2HostListFileSpecificDescription",
                value),
            "--hostlist-domains" => FormatSpecificDescription(
                "ConfigMakerZapret2HostListDomainsSpecificDescription",
                FormatListValue(value)),
            "--hostlist-exclude" => FormatSpecificDescription(
                "ConfigMakerZapret2HostListExcludeFileSpecificDescription",
                value),
            "--hostlist-exclude-domains" => FormatSpecificDescription(
                "ConfigMakerZapret2HostListExcludeDomainsSpecificDescription",
                FormatListValue(value)),
            "--hostlist-auto" => FormatSpecificDescription(
                "ConfigMakerZapret2HostListAutoSpecificDescription",
                value),
            "--ipset" => FormatSpecificDescription(
                "ConfigMakerZapret2IpSetFileSpecificDescription",
                value),
            "--ipset-ip" => FormatSpecificDescription(
                "ConfigMakerZapret2IpSetInlineSpecificDescription",
                FormatListValue(value)),
            "--ipset-exclude" => FormatSpecificDescription(
                "ConfigMakerZapret2IpSetExcludeFileSpecificDescription",
                value),
            "--ipset-exclude-ip" => FormatSpecificDescription(
                "ConfigMakerZapret2IpSetExcludeInlineSpecificDescription",
                FormatListValue(value)),
            "--payload" => FormatSpecificDescription(
                "ConfigMakerZapret2PayloadSpecificDescription",
                FormatListValue(value)),
            "--out-range" => FormatRangeDescription(value, isOutgoing: true),
            "--in-range" => FormatRangeDescription(value, isOutgoing: false),
            "--name" => FormatSpecificDescription(
                "ConfigMakerZapret2ProfileNameSpecificDescription",
                value),
            "--lua-init" => FormatSpecificDescription(
                "ConfigMakerZapret2LuaInitSpecificDescription",
                value.TrimStart('@', '$')),
            "--blob" => FormatBlobDescription(value),
            "--lua-desync" => FormatLuaInvocationDescription(value),
            "--wf-iface" => FormatSpecificDescription(
                "ConfigMakerZapret2WinDivertInterfaceSpecificDescription",
                value),
            "--ssid-filter" => FormatSpecificDescription(
                "ConfigMakerZapret2SsidFilterSpecificDescription",
                FormatListValue(value)),
            "--nlm-filter" => FormatSpecificDescription(
                "ConfigMakerZapret2NlmFilterSpecificDescription",
                FormatListValue(value)),
            _ => string.Empty,
        };
    }

    private string FormatTransportFilterDescription(string protocol, string value)
    {
        bool excludes = value.StartsWith('~');
        string ports = FormatListValue(excludes ? value[1..] : value);
        if (!excludes && ports == "*")
        {
            return string.Format(
                localizer.GetLocalizedString("ConfigMakerZapret2TransportFilterAllSpecificDescription"),
                protocol);
        }
        return string.Format(
            localizer.GetLocalizedString(excludes
                ? "ConfigMakerZapret2TransportFilterExcludedSpecificDescription"
                : "ConfigMakerZapret2TransportFilterIncludedSpecificDescription"),
            protocol,
            ports);
    }

    private string FormatBlobDescription(string value)
    {
        int separator = value.IndexOf(':');
        if (separator <= 0 || separator >= value.Length - 1)
        {
            return string.Empty;
        }
        string blobName = value[..separator];
        string source = value[(separator + 1)..].TrimStart('@', '$');
        return string.Format(
            localizer.GetLocalizedString("ConfigMakerZapret2BlobSpecificDescription"),
            source,
            blobName);
    }

    private string FormatLuaInvocationDescription(string value)
    {
        string[] parts = value.Split(':', StringSplitOptions.RemoveEmptyEntries);
        string functionName = parts.FirstOrDefault() ?? value;
        return parts.Length > 1
            ? string.Format(
                localizer.GetLocalizedString("ConfigMakerZapret2LuaInvocationWithArgumentsDescription"),
                functionName,
                string.Join(", ", parts.Skip(1)))
            : string.Format(
                localizer.GetLocalizedString("ConfigMakerZapret2LuaInvocationDescription"),
                functionName);
    }

    private string FormatRangeDescription(string value, bool isOutgoing)
    {
        string direction = localizer.GetLocalizedString(isOutgoing
            ? "ConfigMakerZapret2RangeOutgoingDirection"
            : "ConfigMakerZapret2RangeIncomingDirection");
        if (string.Equals(value, "a", StringComparison.OrdinalIgnoreCase))
        {
            return string.Format(
                localizer.GetLocalizedString("ConfigMakerZapret2RangeAlwaysDescription"),
                direction);
        }
        if (string.Equals(value, "x", StringComparison.OrdinalIgnoreCase))
        {
            return string.Format(
                localizer.GetLocalizedString("ConfigMakerZapret2RangeNeverDescription"),
                direction);
        }
        if (!TryParseRange(value, out Zapret2Range range))
        {
            return FormatSpecificDescription(
                isOutgoing
                    ? "ConfigMakerZapret2OutRangeSpecificDescription"
                    : "ConfigMakerZapret2InRangeSpecificDescription",
                value);
        }

        if (TryFormatPacketRange(range, direction, out string packetDescription))
        {
            return packetDescription;
        }

        List<string> conditions = [];
        if (range.Lower is Zapret2RangeBoundary lower)
        {
            conditions.Add(string.Format(
                localizer.GetLocalizedString("ConfigMakerZapret2RangeLowerCondition"),
                GetRangeCounterDescription(lower.Mode),
                lower.Value));
        }
        if (range.Upper is Zapret2RangeBoundary upper)
        {
            conditions.Add(string.Format(
                localizer.GetLocalizedString(range.UpperExclusive
                    ? "ConfigMakerZapret2RangeUpperExclusiveCondition"
                    : "ConfigMakerZapret2RangeUpperInclusiveCondition"),
                GetRangeCounterDescription(upper.Mode),
                upper.Value));
        }
        if (conditions.Count == 0 || conditions.Any(string.IsNullOrWhiteSpace))
        {
            return FormatSpecificDescription(
                isOutgoing
                    ? "ConfigMakerZapret2OutRangeSpecificDescription"
                    : "ConfigMakerZapret2InRangeSpecificDescription",
                value);
        }
        return string.Format(
            localizer.GetLocalizedString("ConfigMakerZapret2RangeConditionsDescription"),
            direction,
            string.Join(
                localizer.GetLocalizedString("ConfigMakerZapret2RangeConditionSeparator"),
                conditions));
    }

    private bool TryFormatPacketRange(
        Zapret2Range range,
        string direction,
        out string description)
    {
        description = string.Empty;
        char mode = range.Lower?.Mode ?? range.Upper?.Mode ?? '\0';
        if (mode is not ('n' or 'd') ||
            (range.Lower is Zapret2RangeBoundary lower && lower.Mode != mode) ||
            (range.Upper is Zapret2RangeBoundary upper && upper.Mode != mode))
        {
            return false;
        }

        string singularUnit = localizer.GetLocalizedString(mode == 'n'
            ? "ConfigMakerZapret2RangePacketSingular"
            : "ConfigMakerZapret2RangeDataPacketSingular");
        string pluralUnit = localizer.GetLocalizedString(mode == 'n'
            ? "ConfigMakerZapret2RangePacketPlural"
            : "ConfigMakerZapret2RangeDataPacketPlural");
        long? first = range.Lower?.Value;
        long? last = range.Upper?.Value;
        if (last.HasValue && range.UpperExclusive)
        {
            last--;
        }
        if (last < 1 || first > last)
        {
            description = string.Format(
                localizer.GetLocalizedString("ConfigMakerZapret2RangeNeverDescription"),
                direction);
            return true;
        }
        if (!first.HasValue && last == 1)
        {
            description = string.Format(
                localizer.GetLocalizedString("ConfigMakerZapret2RangeFirstPacketDescription"),
                singularUnit,
                direction);
            return true;
        }
        if (!first.HasValue && last.HasValue)
        {
            description = string.Format(
                localizer.GetLocalizedString("ConfigMakerZapret2RangeFirstPacketsDescription"),
                last.Value,
                pluralUnit,
                direction);
            return true;
        }
        if (first.HasValue && !last.HasValue)
        {
            description = string.Format(
                localizer.GetLocalizedString("ConfigMakerZapret2RangeFromPacketDescription"),
                singularUnit,
                first.Value,
                direction);
            return true;
        }

        description = string.Format(
            localizer.GetLocalizedString("ConfigMakerZapret2RangePacketIntervalDescription"),
            pluralUnit,
            first,
            last,
            direction);
        return true;
    }

    private string GetRangeCounterDescription(char mode) =>
        localizer.GetLocalizedString(mode switch
        {
            'b' => "ConfigMakerZapret2RangeByteCounter",
            's' => "ConfigMakerZapret2RangeSequenceStartCounter",
            'p' => "ConfigMakerZapret2RangeSequenceEndCounter",
            'n' => "ConfigMakerZapret2RangePacketNumberCounter",
            'd' => "ConfigMakerZapret2RangeDataPacketNumberCounter",
            _ => "ConfigMakerZapret2RangeUnknownCounter",
        });

    private static bool TryParseRange(string value, out Zapret2Range range)
    {
        range = default;
        int separatorIndex = value.IndexOf('<');
        bool upperExclusive = separatorIndex >= 0;
        if (separatorIndex < 0)
        {
            separatorIndex = value.IndexOf('-');
        }
        if (separatorIndex < 0)
        {
            return false;
        }

        string lowerText = value[..separatorIndex];
        string upperText = value[(separatorIndex + 1)..];
        if (!TryParseRangeBoundary(lowerText, out Zapret2RangeBoundary? lower) ||
            !TryParseRangeBoundary(upperText, out Zapret2RangeBoundary? upper) ||
            lower is null && upper is null)
        {
            return false;
        }
        range = new Zapret2Range(lower, upper, upperExclusive);
        return true;
    }

    private static bool TryParseRangeBoundary(
        string value,
        out Zapret2RangeBoundary? boundary)
    {
        boundary = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }
        char mode = char.ToLowerInvariant(value[0]);
        if (mode is not ('n' or 'd' or 'b' or 's' or 'p') ||
            !long.TryParse(value[1..], out long number) ||
            number < 0)
        {
            return false;
        }
        boundary = new Zapret2RangeBoundary(mode, number);
        return true;
    }

    private string FormatSpecificDescription(string resourceKey, string value) => string.Format(
        localizer.GetLocalizedString(resourceKey),
        value);

    private static string GetReadableOptionValue(string value) =>
        UnquoteOptionValue(value).Trim();

    private static string NormalizeIpVersion(string value) => value.ToLowerInvariant() switch
    {
        "ipv4" => "IPv4",
        "ipv6" => "IPv6",
        _ => value,
    };

    private static string FormatListValue(string value) =>
        string.Join(", ", value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private bool IsZapret2Component => string.Equals(
        ComponentId,
        HardcodedItemIds.ComponentIds[Components.Zapret2],
        StringComparison.OrdinalIgnoreCase);

    private void AddExplicitPresetGroupsFallback()
    {
        foreach (ConfigMakerPresetGroupInfo group in explicitPresetGroups)
        {
            TreeViewNode node = new() { Content = CreatePresetTreeItem(group.Name) };
            foreach (string detail in group.Details.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                node.Children.Add(new TreeViewNode
                {
                    Content = CreatePresetTreeItem(detail, isCode: true),
                });
            }
            PresetGroupsTreeView.RootNodes.Add(node);
        }
    }

    public void SetPresetFilesPanelVisible(bool visible)
    {
        PresetFilesPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        PresetFilesContentSizer.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        PresetFilesColumn.Width = visible ? GridLength.Auto : new GridLength(0);
        PresetFilesSizerColumn.Width = visible ? new GridLength(12) : new GridLength(0);
        layoutInitialized = false;
        UpdateLayoutBounds();
        PanelStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdatePresetGroupsTab()
    {
        bool shouldShow = hasPresetGroups || !usesExplicitPresetStructure;
        bool containsItem = BottomSelector.Items.Contains(PresetGroupsSelectorItem);
        if (shouldShow && !containsItem)
        {
            BottomSelector.Items.Add(PresetGroupsSelectorItem);
        }
        else if (!shouldShow && containsItem)
        {
            if (BottomSelector.SelectedIndex == BottomSelector.Items.IndexOf(PresetGroupsSelectorItem))
            {
                BottomSelector.SelectIndex(0);
            }
            BottomSelector.Items.Remove(PresetGroupsSelectorItem);
        }
    }

    private string PresetFileCategoryName(ConfigMakerPresetFileKind kind) =>
        localizer.GetLocalizedString(kind switch
        {
            ConfigMakerPresetFileKind.Library => "ConfigMakerPresetFileCategoryLibraries",
            ConfigMakerPresetFileKind.Payload => "ConfigMakerPresetFileCategoryPayloads",
            _ => "ConfigMakerPresetFileCategorySiteLists",
        });

    private async void ReplacePresetFileItem_Click(object sender, RoutedEventArgs e)
    {
        ConfigMakerPresetFileInfo file = GetPresetFileFromMenuSender(sender);
        if (file == null)
        {
            return;
        }

        string missingPath = TryResolvePresetFilePath(file.Path);
        if (string.IsNullOrWhiteSpace(missingPath))
        {
            ShowEditorMessage(
                localizer.GetLocalizedString("ConfigMakerPresetFileInvalidPathMessage"),
                InfoBarSeverity.Error);
            return;
        }
        if (File.Exists(missingPath))
        {
            RebuildPresetStructure();
            return;
        }

        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = localizer.GetLocalizedString("ConfigMakerPresetFileReplaceDialogTitle"),
            Content = CreateReplacementDialogContent(missingPath),
            PrimaryButtonText = localizer.GetLocalizedString("ConfigMakerPresetFileApplyAutoCorrectionButton"),
            SecondaryButtonText = localizer.GetLocalizedString("ConfigMakerPresetFileChooseManuallyButton"),
            CloseButtonText = localizer.GetLocalizedString("Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };
        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ConfigImportAutoCorrector autoCorrector = new();
            bool suggestEmptyFile = autoCorrector.ShouldSuggestEmptyFile(missingPath);
            if (suggestEmptyFile)
            {
                TryCreateEmptyPresetFile(missingPath);
            }
            else
            {
                ConfigImportResult autoCorrectResult = CreateAutoCorrectResult(missingPath);
                string suggestion = await Task.Run(() => autoCorrector.FindReplacement(
                    autoCorrectResult,
                    missingPath));
                if (!string.IsNullOrWhiteSpace(suggestion))
                {
                    ApplyPresetFileReplacement(file, suggestion);
                    return;
                }

                ShowEditorMessage(
                    localizer.GetLocalizedString("ConfigMakerPresetFileNoSuggestionMessage"),
                    InfoBarSeverity.Warning);
                string selectedPath = ChoosePresetFileReplacement(missingPath);
                if (!string.IsNullOrWhiteSpace(selectedPath))
                {
                    ApplyPresetFileReplacement(file, selectedPath);
                }
            }
        }
        else if (result == ContentDialogResult.Secondary)
        {
            string selectedPath = ChoosePresetFileReplacement(missingPath);
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                ApplyPresetFileReplacement(file, selectedPath);
            }
        }
    }

    private ConfigImportResult CreateAutoCorrectResult(
        string missingPath,
        ConfigItem sourceConfig = null)
    {
        var component = DatabaseHelper.Instance.GetItemById(ComponentId);
        string componentDirectory = component?.Directory ?? string.Empty;
        string presetDirectory = GetPresetBaseDirectory();
        string sourceDirectory = Directory.Exists(presetDirectory)
            ? presetDirectory
            : Directory.Exists(componentDirectory)
                ? componentDirectory
            : Path.GetDirectoryName(missingPath) ?? Environment.CurrentDirectory;
        return new ConfigImportResult
        {
            Config = sourceConfig,
            Target = new ConfigImportTarget(
                ComponentId ?? string.Empty,
                component?.ShortName ?? ComponentId ?? string.Empty,
                component?.Executable ?? string.Empty,
                component?.CurrentVersion,
                componentDirectory),
            SourcePath = Path.Combine(sourceDirectory, ".cdpiui-preset.txt"),
            Issues = [],
            SourceFiles = [],
            ReferencedFiles = [missingPath],
            MissingReferencedFiles = [missingPath],
            MissingFileResolutions = [],
            GeneratedFiles = [],
        };
    }

    private FrameworkElement CreateReplacementDialogContent(string missingPath)
    {
        StackPanel content = new() { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = string.Format(
                localizer.GetLocalizedString("ConfigMakerPresetFileMissingMessage"),
                missingPath),
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(new TextBlock
        {
            Text = localizer.GetLocalizedString("ConfigMakerPresetFileReplacementChoiceMessage"),
            TextWrapping = TextWrapping.Wrap,
        });
        return content;
    }

    private string ChoosePresetFileReplacement(string missingPath)
    {
        string extension = Path.GetExtension(missingPath);
        using System.Windows.Forms.OpenFileDialog dialog = new()
        {
            Title = localizer.GetLocalizedString("ConfigImportChooseReplacementButton.Content"),
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Filter = string.IsNullOrWhiteSpace(extension)
                ? $"{localizer.GetLocalizedString("AllSupported")} (*.*)|*.*"
                : $"{extension.TrimStart('.').ToUpperInvariant()} (*{extension})|*{extension}|{localizer.GetLocalizedString("AllSupported")} (*.*)|*.*",
            FilterIndex = 1,
            RestoreDirectory = true,
        };
        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK
            ? dialog.FileName
            : string.Empty;
    }

    private bool TryCreateEmptyPresetFile(string missingPath, bool showStatus = true)
    {
        try
        {
            string directory = Path.GetDirectoryName(missingPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
            using (new FileStream(
                missingPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read))
            {
            }
            RebuildPresetStructure();
            if (showStatus)
            {
                ShowEditorMessage(
                    string.Format(
                        localizer.GetLocalizedString("ConfigMakerPresetFileEmptyCreatedMessage"),
                        missingPath),
                    InfoBarSeverity.Success);
            }
            return true;
        }
        catch (Exception ex)
        {
            if (showStatus)
            {
                ShowEditorMessage(
                    string.Format(
                        localizer.GetLocalizedString("ConfigMakerPresetFileReplaceFailedMessage"),
                        ex.Message),
                    InfoBarSeverity.Error);
            }
            return false;
        }
    }

    private void ApplyPresetFileReplacement(ConfigMakerPresetFileInfo file, string replacementPath) =>
        TryApplyPresetFileReplacement(file, replacementPath, showStatus: true);

    private bool TryApplyPresetFileReplacement(
        ConfigMakerPresetFileInfo file,
        string replacementPath,
        bool showStatus)
    {
        try
        {
            string fullReplacementPath = Path.GetFullPath(replacementPath);
            if (!File.Exists(fullReplacementPath))
            {
                throw new FileNotFoundException(null, fullReplacementPath);
            }

            ConfigMakerPresetResource attachedResource = FindAttachedResource(file.Path);
            if (attachedResource != null)
            {
                attachedResource.Path = fullReplacementPath;
                attachedResource.IsBuiltIn = IsPathInsideComponent(fullReplacementPath);
                RebuildPresetStructure();
                if (showStatus)
                {
                    ShowEditorMessage(
                        string.Format(
                            localizer.GetLocalizedString("ConfigMakerPresetFileReplacedMessage"),
                            fullReplacementPath),
                        InfoBarSeverity.Success);
                }
                return true;
            }

            ConfigMakerPresetResource replacementResource = GetOrCreatePresetResource(
                fullReplacementPath,
                file.Kind,
                out bool resourceAdded);
            string replacementReference = replacementResource.Reference;
            string updatedCommand = ReplacePresetFileInCommand(
                CommandText,
                file,
                replacementReference);
            bool commandChanged = !string.Equals(
                updatedCommand,
                CommandText,
                StringComparison.Ordinal);
            bool variablesChanged = ReplacePresetFileInVariables(file, replacementReference);
            if (!commandChanged && !variablesChanged)
            {
                if (resourceAdded)
                {
                    presetDocument.Resources.Remove(replacementResource);
                }
                throw new InvalidOperationException(
                    localizer.GetLocalizedString("ConfigMakerPresetFileReferenceNotFoundMessage"));
            }

            if (usesExplicitPresetStructure)
            {
                explicitPresetFiles = explicitPresetFiles
                    .Select(item => ReferenceEquals(item, file) ||
                        (item.Kind == file.Kind && PresetPathsEqual(item.Path, file.Path))
                            ? item with
                            {
                                Name = Path.GetFileName(fullReplacementPath),
                                Path = replacementReference,
                                Folder = GetPresetDisplayFolder(replacementReference),
                            }
                            : item)
                    .ToArray();
            }
            if (commandChanged)
            {
                CommandText = ComponentCommandLineFormatter.FormatByFlags(updatedCommand);
            }
            RebuildPresetStructure();
            PresetFileReplaced?.Invoke(
                this,
                new ConfigMakerPresetFileReplacedEventArgs(
                    CommandText,
                    file.Path,
                    replacementReference));
            if (showStatus)
            {
                ShowEditorMessage(
                    string.Format(
                        localizer.GetLocalizedString("ConfigMakerPresetFileReplacedMessage"),
                        fullReplacementPath),
                    InfoBarSeverity.Success);
            }
            return true;
        }
        catch (Exception ex)
        {
            if (showStatus)
            {
                ShowEditorMessage(
                    string.Format(
                        localizer.GetLocalizedString("ConfigMakerPresetFileReplaceFailedMessage"),
                        ex.Message),
                    InfoBarSeverity.Error);
            }
            return false;
        }
    }

    private bool ReplacePresetFileInVariables(
        ConfigMakerPresetFileInfo file,
        string replacementPath)
    {
        bool replaced = false;
        foreach (ConfigMakerVariableDefinition variable in presetDocument.Variables)
        {
            variable.Value = ReplacePresetFileInVariableValue(
                variable.Value,
                file,
                replacementPath,
                ref replaced);
            variable.OnValue = ReplacePresetFileInVariableValue(
                variable.OnValue,
                file,
                replacementPath,
                ref replaced);
            variable.OffValue = ReplacePresetFileInVariableValue(
                variable.OffValue,
                file,
                replacementPath,
                ref replaced);
            for (int index = 0; index < variable.Values.Count; index++)
            {
                variable.Values[index] = ReplacePresetFileInVariableValue(
                    variable.Values[index],
                    file,
                    replacementPath,
                    ref replaced);
            }
        }
        return replaced;
    }

    private string ReplacePresetFileInVariableValue(
        string value,
        ConfigMakerPresetFileInfo file,
        string replacementPath,
        ref bool replaced)
    {
        string updated = ReplacePresetFileInCommand(value ?? string.Empty, file, replacementPath);
        if (!string.Equals(updated, value, StringComparison.Ordinal))
        {
            replaced = true;
            return updated;
        }

        ConfigMakerPresetFileInfo directFile = TryExtractDirectPresetFile(value);
        if (directFile != null &&
            directFile.Kind == file.Kind &&
            PresetPathsEqual(directFile.Path, file.Path))
        {
            replaced = true;
            return replacementPath;
        }
        return value;
    }

    private async void OpenPresetFileItem_Click(object sender, RoutedEventArgs e)
    {
        ConfigMakerPresetFileInfo file = GetPresetFileFromMenuSender(sender);
        if (file == null)
        {
            return;
        }

        string path = ResolvePresetFilePath(file.Path);
        if (!File.Exists(path))
        {
            ShowEditorMessage(
                string.Format(localizer.GetLocalizedString("ConfigMakerPresetFileMissingMessage"), path),
                InfoBarSeverity.Warning);
            return;
        }

        if (file.Kind == ConfigMakerPresetFileKind.SiteList ||
            string.Equals(Path.GetExtension(path), ".txt", StringComparison.OrdinalIgnoreCase))
        {
            int openMode = SettingsManager.Instance.GetValue<int>(
                "FILEOPENACTIONS",
                "mode");
            string selectedApplication = SettingsManager.Instance.GetValue<string>(
                "FILEOPENACTIONS",
                "applicationPath");
            bool choiceWasSaved = SettingsManager.Instance.GetValue<bool>(
                    "FILEOPENACTIONS",
                    "isDialogShown") &&
                (openMode == (int)TextFileOpenModes.FollowSystem ||
                 (openMode == (int)TextFileOpenModes.UserChoose && File.Exists(selectedApplication)));
            bool askAgain = !SettingsManager.Instance.GetValueOrDefault<bool>(
                "FILEOPENACTIONS",
                "doNotRemindAgain",
                defaultValue: true);
            if (!choiceWasSaved || askAgain)
            {
                EditSitelistAskApplicationContentDialog dialog = new()
                {
                    XamlRoot = XamlRoot,
                    FilePath = path,
                };
                await dialog.ShowAsync();
                if (dialog.IsSuccess)
                {
                    SettingsManager.Instance.SetValue("FILEOPENACTIONS", "isDialogShown", true);
                }
                return;
            }

            ShellHelper.OpenFile(path);
            return;
        }

        ShellHelper.OpenFileInDefaultApp(path);
    }

    private void ShowPresetFileInFolderItem_Click(object sender, RoutedEventArgs e)
    {
        ConfigMakerPresetFileInfo file = GetPresetFileFromMenuSender(sender);
        if (file == null)
        {
            return;
        }

        string path = ResolvePresetFilePath(file.Path);
        if (!File.Exists(path))
        {
            ShowEditorMessage(
                string.Format(localizer.GetLocalizedString("ConfigMakerPresetFileMissingMessage"), path),
                InfoBarSeverity.Warning);
            return;
        }
        ShellHelper.LookupFileInDirectory(path);
    }

    private void AddPresetFileButton_Click(object sender, RoutedEventArgs e)
    {
        using System.Windows.Forms.OpenFileDialog dialog = new()
        {
            Title = localizer.GetLocalizedString("ConfigMakerAddPresetFileDialogTitle"),
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Filter = localizer.GetLocalizedString("ConfigMakerPresetResourceFileFilter"),
            Multiselect = true,
            RestoreDirectory = true,
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }
        AddPresetFiles(dialog.FileNames);
    }

    public void AddPresetFiles(IEnumerable<string> filePaths)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        int added = 0;
        foreach (string sourcePath in filePaths)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                continue;
            }
            string fullPath = Path.GetFullPath(sourcePath);
            if (presetDocument.Resources.Any(resource =>
                    string.Equals(
                        TryResolvePresetFilePath(resource.Reference),
                        fullPath,
                        StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }
            string alias = CreateUniqueResourceAlias(Path.GetFileName(fullPath));
            presetDocument.Resources.Add(new ConfigMakerPresetResource
            {
                Alias = alias,
                Path = fullPath,
                Kind = InferResourceKind(fullPath),
                IsBuiltIn = IsPathInsideComponent(fullPath),
            });
            added++;
        }
        if (added > 0)
        {
            AttachExistingPresetFiles();
            RebuildPresetStructure();
            SetPresetFilesPanelVisible(true);
            ShowEditorMessage(
                string.Format(
                    localizer.GetLocalizedString("ConfigMakerPresetFilesAddedMessage"),
                    added),
                InfoBarSeverity.Success);
        }
    }

    private void EditorPanel_DragOver(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = localizer.GetLocalizedString("ConfigMakerAddPresetFileDialogTitle");
        e.DragUIOverride.IsCaptionVisible = true;
        e.Handled = true;
    }

    private async void EditorPanel_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        try
        {
            IReadOnlyList<IStorageItem> droppedItems = await e.DataView.GetStorageItemsAsync();
            AddPresetFiles(droppedItems
                .OfType<StorageFile>()
                .Select(file => file.Path)
                .Where(path => !string.IsNullOrWhiteSpace(path)));
        }
        catch (Exception exception)
        {
            ShowEditorMessage(exception.Message, InfoBarSeverity.Error);
        }
        finally
        {
            e.Handled = true;
        }
    }

    private async void RemovePresetFileItem_Click(object sender, RoutedEventArgs e)
    {
        ConfigMakerPresetFileInfo file = GetPresetFileFromMenuSender(sender);
        ConfigMakerPresetResource resource = file == null ? null : FindAttachedResource(file.Path);
        if (resource == null)
        {
            return;
        }
        bool isReferenced = CommandText.Contains(resource.Reference, StringComparison.OrdinalIgnoreCase) ||
            presetDocument.Variables
                .SelectMany(GetPresetVariableCandidateValues)
                .Any(value => value.Contains(resource.Reference, StringComparison.OrdinalIgnoreCase));
        if (isReferenced)
        {
            ContentDialog confirmation = new()
            {
                XamlRoot = XamlRoot,
                Title = localizer.GetLocalizedString("ConfigMakerRemoveReferencedFileDialogTitle"),
                Content = localizer.GetLocalizedString("ConfigMakerRemoveReferencedFileDialogMessage"),
                PrimaryButtonText = localizer.GetLocalizedString("Remove"),
                CloseButtonText = localizer.GetLocalizedString("Cancel"),
                DefaultButton = ContentDialogButton.Close,
            };
            if (await confirmation.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }
        presetDocument.Resources.Remove(resource);
        RebuildPresetStructure();
        ScheduleDiagnostics();
    }

    private void PresetFilesTreeView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (IsEditorReadOnly ||
            PresetFilesTreeView.SelectedNode?.Content is not ConfigMakerPresetFileTreeItem item ||
            item.File == null)
        {
            return;
        }
        string reference = item.File.Path.Trim().Trim('"');
        if (!reference.StartsWith("preset://", StringComparison.OrdinalIgnoreCase))
        {
            string resolvedPath = TryResolvePresetFilePath(reference);
            if (!string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath))
            {
                string fullPath = Path.GetFullPath(resolvedPath);
                ConfigMakerPresetResource resource = presetDocument.Resources.FirstOrDefault(candidate =>
                    string.Equals(
                        TryResolvePresetFilePath(candidate.Reference),
                        fullPath,
                        StringComparison.OrdinalIgnoreCase));
                if (resource == null)
                {
                    resource = new ConfigMakerPresetResource
                    {
                        Alias = CreateUniqueResourceAlias(Path.GetFileName(fullPath)),
                        Path = fullPath,
                        Kind = ToConfigMakerResourceKind(item.File.Kind),
                        IsBuiltIn = IsPathInsideComponent(fullPath),
                    };
                    presetDocument.Resources.Add(resource);
                    RebuildPresetStructure();
                }
                reference = resource.Reference;
            }
        }
        InsertAtCursor($"\"{reference}\"");
    }

    private string CreateUniqueResourceAlias(string fileName)
    {
        string normalized = fileName.Unidecode();
        normalized = string.Concat(normalized.Select(character =>
            char.IsLetterOrDigit(character) || character is '.' or '_' or '-'
                ? character
                : '_'));
        normalized = normalized.Trim('_');
        if (normalized.Length == 0)
        {
            normalized = "resource.bin";
        }
        string stem = Path.GetFileNameWithoutExtension(normalized);
        string extension = Path.GetExtension(normalized);
        string candidate = normalized;
        int suffix = 2;
        while (presetDocument.Resources.Any(resource =>
                   string.Equals(resource.Alias, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{stem}_{suffix++}{extension}";
        }
        return candidate;
    }

    private static ConfigMakerResourceKind InferResourceKind(string filePath) =>
        Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".txt" or ".list" => ConfigMakerResourceKind.SiteList,
            ".lua" => ConfigMakerResourceKind.Library,
            ".bin" or ".dat" or ".der" or ".pem" => ConfigMakerResourceKind.Payload,
            _ => ConfigMakerResourceKind.Other,
        };

    private static ConfigMakerPresetFileKind ToPresetFileKind(ConfigMakerResourceKind kind) => kind switch
    {
        ConfigMakerResourceKind.Library => ConfigMakerPresetFileKind.Library,
        ConfigMakerResourceKind.Payload or ConfigMakerResourceKind.Other => ConfigMakerPresetFileKind.Payload,
        _ => ConfigMakerPresetFileKind.SiteList,
    };

    private bool IsPathInsideComponent(string filePath)
    {
        string componentDirectory = DatabaseHelper.Instance.GetItemById(ComponentId)?.Directory ?? string.Empty;
        if (string.IsNullOrWhiteSpace(componentDirectory))
        {
            return false;
        }
        string fullPath = Path.GetFullPath(filePath);
        string fullRoot = Path.GetFullPath(componentDirectory).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        return string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase) ||
               fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static ConfigMakerPresetFileInfo GetPresetFileFromMenuSender(object sender)
    {
        if (sender is not FrameworkElement element)
        {
            return null;
        }
        if (element.Tag is ConfigMakerPresetFileInfo taggedFile)
        {
            return taggedFile;
        }
        if (element.DataContext is TreeViewNode node &&
            node.Content is ConfigMakerPresetFileTreeItem nodeItem)
        {
            return nodeItem.File;
        }
        return (element.DataContext as ConfigMakerPresetFileTreeItem)?.File;
    }

    private string ResolvePresetFilePath(string sourcePath) =>
        CreateResourceConfigSnapshot().ResolveFilePath(
            sourcePath,
            DatabaseHelper.Instance.GetItemById(ComponentId)?.Directory ?? Directories.CurrentDirectory,
            GetPresetBaseDirectory());

    private string GetPresetBaseDirectory()
    {
        if (!string.IsNullOrWhiteSpace(presetBaseDirectory))
        {
            return presetBaseDirectory;
        }
        return DatabaseHelper.Instance.GetItemById(ComponentId)?.Directory ?? string.Empty;
    }

    private static string GetConfigItemBaseDirectory(ConfigItem item)
    {
        string packId = item?.packId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(packId))
        {
            return string.Empty;
        }
        string registeredDirectory = DatabaseHelper.Instance.GetItemById(packId)?.Directory ?? string.Empty;
        return !string.IsNullOrWhiteSpace(registeredDirectory)
            ? Path.GetFullPath(registeredDirectory)
            : Path.GetFullPath(Path.Combine(Directories.StoreItemsDirectory, packId));
    }

    private ConfigMakerPresetResource FindAttachedResource(string reference)
    {
        string alias = (reference ?? string.Empty).Trim().Trim('"', '\'');
        if (alias.StartsWith("preset://", StringComparison.OrdinalIgnoreCase))
        {
            alias = alias["preset://".Length..];
        }
        return presetDocument.Resources.FirstOrDefault(resource =>
            string.Equals(resource.Alias, alias, StringComparison.OrdinalIgnoreCase));
    }

    private string TryResolvePresetFilePath(string sourcePath)
    {
        try
        {
            return ResolvePresetFilePath(sourcePath);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetPresetDisplayFolder(string path) =>
        ConfigFileReferences.GetDisplayFolder(path);

    private static bool PresetPathsEqual(string left, string right) =>
        ConfigFileReferences.PathsEqual(left, right);

    private string ReplacePresetFileInCommand(
        string commandText,
        ConfigMakerPresetFileInfo file,
        string replacementPath) =>
        ConfigFileReferences.ReplaceFileInText(commandText, file.Path, ToCoreFileKind(file.Kind), replacementPath);

    private ConfigItem CreateResourceConfigSnapshot(string commandText = null)
    {
        ConfigItem snapshot = presetDocument.ToConfigItem(string.Empty, presetDocument.Name);
        snapshot.startup_string = commandText ?? CommandText;
        return snapshot;
    }

    private static ConfigFileKind ToCoreFileKind(ConfigMakerPresetFileKind kind) => kind switch
    {
        ConfigMakerPresetFileKind.Library => ConfigFileKind.Library,
        ConfigMakerPresetFileKind.Payload => ConfigFileKind.Payload,
        _ => ConfigFileKind.SiteList,
    };

    private static ConfigMakerPresetFileInfo ToEditorFile(ConfigUsedFile file) => file == null ? null : new(
        file.Name,
        file.Path,
        file.Folder,
        file.Kind switch
        {
            ConfigFileKind.Library => ConfigMakerPresetFileKind.Library,
            ConfigFileKind.Payload or ConfigFileKind.Other => ConfigMakerPresetFileKind.Payload,
            _ => ConfigMakerPresetFileKind.SiteList,
        },
        file.OptionName,
        file.IsAttachedResource);

    private IEnumerable<ConfigMakerPresetFileInfo> ExtractPresetFiles(string commandText) =>
        CreateResourceConfigSnapshot(commandText).UsedFiles.Select(ToEditorFile);

    private IEnumerable<ConfigMakerPresetFileInfo> ExtractPresetFilesFromText(string text)
    {
        ConfigItem snapshot = CreateResourceConfigSnapshot();
        return ConfigFileReferences.ExtractFromText(text, snapshot.ExpandFileReference).Select(ToEditorFile);
    }

    private ConfigMakerPresetFileInfo TryExtractDirectPresetFile(string value) =>
        ToEditorFile(ConfigFileReferences.ExtractDirectFile(value, CreateResourceConfigSnapshot().ExpandFileReference));

    private static IEnumerable<string> GetPresetVariableCandidateValues(
        ConfigMakerVariableDefinition variable)
    {
        if (!string.IsNullOrWhiteSpace(variable.Value))
        {
            yield return variable.Value;
        }
        foreach (string value in variable.Values.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            yield return value;
        }
        if (!string.IsNullOrWhiteSpace(variable.OnValue))
        {
            yield return variable.OnValue;
        }
        if (!string.IsNullOrWhiteSpace(variable.OffValue))
        {
            yield return variable.OffValue;
        }
    }

    private static IReadOnlyList<ParsedPresetOption> ParseCommandOptions(string commandText) =>
        ConfigCommandLine.ParseOptions(commandText);

    private static bool IsCommonFilter(string optionName) =>
        optionName.StartsWith("--wf-", StringComparison.OrdinalIgnoreCase) ||
        optionName.StartsWith("--ssid-filter", StringComparison.OrdinalIgnoreCase) ||
        optionName.StartsWith("--nlm-", StringComparison.OrdinalIgnoreCase);

    private static bool IsProfileOption(string optionName) =>
        string.Equals(optionName, "--new", StringComparison.OrdinalIgnoreCase) ||
        IsProfileFilter(optionName) ||
        optionName.StartsWith("--payload", StringComparison.OrdinalIgnoreCase) ||
        optionName.EndsWith("-range", StringComparison.OrdinalIgnoreCase) ||
        optionName.Contains("desync", StringComparison.OrdinalIgnoreCase);

    private static bool IsProfileFilter(string optionName) =>
        optionName.StartsWith("--filter-", StringComparison.OrdinalIgnoreCase) ||
        optionName.StartsWith("--hostlist", StringComparison.OrdinalIgnoreCase) ||
        optionName.StartsWith("--ipset", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(optionName, "--name", StringComparison.OrdinalIgnoreCase);

    private static string UnquoteOptionValue(string value) => ConfigCommandLine.Unquote(value);

    private readonly record struct Zapret2Range(
        Zapret2RangeBoundary? Lower,
        Zapret2RangeBoundary? Upper,
        bool UpperExclusive);

    private readonly record struct Zapret2RangeBoundary(char Mode, long Value);

    private sealed class PresetFileKeyComparer : IEqualityComparer<(ConfigMakerPresetFileKind Kind, string Path)>
    {
        public static PresetFileKeyComparer Instance { get; } = new();

        public bool Equals(
            (ConfigMakerPresetFileKind Kind, string Path) left,
            (ConfigMakerPresetFileKind Kind, string Path) right) =>
            left.Kind == right.Kind &&
            string.Equals(left.Path, right.Path, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((ConfigMakerPresetFileKind Kind, string Path) value) =>
            HashCode.Combine(value.Kind, StringComparer.OrdinalIgnoreCase.GetHashCode(value.Path));
    }

    public void LoadPresetDocument(ConfigMakerPresetDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        presetBaseDirectory = string.Empty;
        presetDocument.Name = document.Name;
        presetDocument.ComponentId = document.ComponentId;
        presetDocument.PackId = document.PackId;
        presetDocument.FileName = document.FileName;
        presetDocument.Meta = document.Meta;
        presetDocument.TargetVersion = document.TargetVersion;
        presetDocument.Variables.Clear();
        foreach (ConfigMakerVariableDefinition variable in document.Variables)
        {
            presetDocument.Variables.Add(CloneVariable(variable));
        }
        presetDocument.Resources.Clear();
        foreach (ConfigMakerPresetResource resource in document.Resources)
        {
            presetDocument.Resources.Add(new ConfigMakerPresetResource
            {
                Alias = resource.Alias,
                Path = resource.Path,
                Kind = resource.Kind,
                IsBuiltIn = resource.IsBuiltIn,
            });
        }
        ComponentId = document.ComponentId;
        SetEditorText(document.CommandText);
        RebuildPresetStructure();
        UpdateSimpleDesignerAvailability();
        DocumentStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task LoadConfigItem(
        ConfigItem item,
        bool applyAutoCorrectorSilently = false)
    {
        ArgumentNullException.ThrowIfNull(item);
        LoadPresetDocument(ConfigMakerPresetDocument.FromConfigItem(item));
        presetBaseDirectory = GetConfigItemBaseDirectory(item);
        RebuildPresetStructure();
        if (applyAutoCorrectorSilently)
        {
            await ApplyAutoCorrectorSilentlyAsync(item);
        }
        AttachExistingPresetFiles();
        RebuildPresetStructure();
    }

    private async Task ApplyAutoCorrectorSilentlyAsync(ConfigItem sourceConfig)
    {
        ConfigImportAutoCorrector autoCorrector = new();
        foreach (ConfigMakerPresetFileInfo file in ExtractPresetFiles(CommandText).ToArray())
        {
            string missingPath = TryResolvePresetFilePath(file.Path);
            if (string.IsNullOrWhiteSpace(missingPath) || File.Exists(missingPath))
            {
                continue;
            }
            try
            {
                if (autoCorrector.ShouldSuggestEmptyFile(missingPath))
                {
                    TryCreateEmptyPresetFile(missingPath, showStatus: false);
                    continue;
                }
                ConfigImportResult autoCorrectResult = CreateAutoCorrectResult(
                    missingPath,
                    sourceConfig);
                string suggestion = await Task.Run(() => autoCorrector.FindReplacement(
                    autoCorrectResult,
                    missingPath));
                if (!string.IsNullOrWhiteSpace(suggestion) && File.Exists(suggestion))
                {
                    TryApplyPresetFileReplacement(file, suggestion, showStatus: false);
                }
            }
            catch
            {
                // A silent import leaves unresolved references unchanged.
            }
        }
    }

    private void AttachExistingPresetFiles()
    {
        string updatedCommand = CommandText;
        foreach (ConfigMakerPresetFileInfo file in ExtractPresetFilesFromText(updatedCommand).ToArray())
        {
            ConfigMakerPresetResource resource = GetOrCreatePresetResource(file, out bool resourceAdded);
            if (resource == null)
            {
                continue;
            }

            string rewritten = ReplacePresetFileInCommand(
                updatedCommand,
                file,
                resource.Reference);
            if (string.Equals(rewritten, updatedCommand, StringComparison.Ordinal))
            {
                if (resourceAdded)
                {
                    presetDocument.Resources.Remove(resource);
                }
                continue;
            }
            updatedCommand = rewritten;
        }

        AttachExistingPresetFilesInVariables();

        if (!string.Equals(updatedCommand, CommandText, StringComparison.Ordinal))
        {
            SetEditorText(ComponentCommandLineFormatter.FormatByFlags(updatedCommand));
        }
    }

    private ConfigMakerPresetResource GetOrCreatePresetResource(
        ConfigMakerPresetFileInfo file,
        out bool resourceAdded)
    {
        resourceAdded = false;
        if (file == null || file.Path.StartsWith("preset://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string resolvedPath = TryResolvePresetFilePath(file.Path);
        if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
        {
            return null;
        }

        return GetOrCreatePresetResource(
            Path.GetFullPath(resolvedPath),
            file.Kind,
            out resourceAdded);
    }

    private ConfigMakerPresetResource GetOrCreatePresetResource(
        string fullPath,
        ConfigMakerPresetFileKind kind,
        out bool resourceAdded)
    {
        resourceAdded = false;
        ConfigMakerPresetResource resource = presetDocument.Resources.FirstOrDefault(candidate =>
            string.Equals(
                TryResolvePresetFilePath(candidate.Reference),
                fullPath,
                StringComparison.OrdinalIgnoreCase));
        if (resource != null)
        {
            return resource;
        }

        resource = new ConfigMakerPresetResource
        {
            Alias = CreateUniqueResourceAlias(Path.GetFileName(fullPath)),
            Path = fullPath,
            Kind = ToConfigMakerResourceKind(kind),
            IsBuiltIn = IsPathInsideComponent(fullPath),
        };
        presetDocument.Resources.Add(resource);
        resourceAdded = true;
        return resource;
    }

    private void AttachExistingPresetFilesInVariables()
    {
        foreach (ConfigMakerVariableDefinition variable in presetDocument.Variables)
        {
            variable.Value = AttachExistingPresetFilesInVariableValue(variable.Value);
            variable.OnValue = AttachExistingPresetFilesInVariableValue(variable.OnValue);
            variable.OffValue = AttachExistingPresetFilesInVariableValue(variable.OffValue);
            for (int index = 0; index < variable.Values.Count; index++)
            {
                variable.Values[index] = AttachExistingPresetFilesInVariableValue(
                    variable.Values[index]);
            }
        }
    }

    private string AttachExistingPresetFilesInVariableValue(string value)
    {
        string updatedValue = value ?? string.Empty;
        ConfigMakerPresetFileInfo directFile = TryExtractDirectPresetFile(updatedValue);
        ConfigMakerPresetFileInfo[] files = ExtractPresetFilesFromText(updatedValue)
            .Append(directFile)
            .Where(file => file != null)
            .DistinctBy(file => (file.Kind, file.Path), PresetFileKeyComparer.Instance)
            .ToArray();

        foreach (ConfigMakerPresetFileInfo file in files)
        {
            ConfigMakerPresetResource resource = GetOrCreatePresetResource(file, out bool resourceAdded);
            if (resource == null)
            {
                continue;
            }

            string rewritten = ReplacePresetFileInCommand(updatedValue, file, resource.Reference);
            if (string.Equals(rewritten, updatedValue, StringComparison.Ordinal) &&
                directFile != null &&
                PresetPathsEqual(directFile.Path, file.Path))
            {
                rewritten = resource.Reference;
            }
            if (string.Equals(rewritten, updatedValue, StringComparison.Ordinal))
            {
                if (resourceAdded)
                {
                    presetDocument.Resources.Remove(resource);
                }
                continue;
            }
            updatedValue = rewritten;
        }
        return updatedValue;
    }

    private static ConfigMakerResourceKind ToConfigMakerResourceKind(ConfigMakerPresetFileKind kind) =>
        kind switch
        {
            ConfigMakerPresetFileKind.Library => ConfigMakerResourceKind.Library,
            ConfigMakerPresetFileKind.Payload => ConfigMakerResourceKind.Payload,
            _ => ConfigMakerResourceKind.SiteList,
        };

    public ConfigItem CreateConfigItem(string packId, string presetName)
    {
        SyncDocumentFromEditor();
        return presetDocument.ToConfigItem(packId, presetName);
    }

    public async Task<ConfigMakerPresetSaveResult> SaveToApplicationAsync()
    {
        SyncDocumentFromEditor();
        AttachExistingPresetFiles();
        SyncDocumentFromEditor();
        ConfigMakerSavePresetContentDialog dialog = new(presetDocument.Name)
        {
            XamlRoot = XamlRoot,
            OverwriteEnable = !string.IsNullOrWhiteSpace(presetDocument.PackId) &&
                              !string.IsNullOrWhiteSpace(presetDocument.FileName),
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return ConfigMakerPresetSaveResult.Failed("CANCELED");
        }

        ConfigMakerPresetStorageService storageService = new();
        bool overwrite = dialog.Result == ConfigMakerSavePresetContentDialogResult.Overwrite;
        ConfigMakerPresetSaveResult result = overwrite
            ? await storageService.OverwriteAsync(presetDocument)
            : await storageService.SaveAsync(dialog.PresetName, presetDocument);
        if (result.Success)
        {
            if (!overwrite)
            {
                presetDocument.Name = dialog.PresetName;
            }
            presetDocument.PackId = result.PackId;
            presetDocument.FileName = result.ConfigFileName;
            foreach (ConfigMakerPresetResource resource in presetDocument.Resources)
            {
                ConfigMakerResourceMetadata? storedResource = result.StoredResources.FirstOrDefault(candidate =>
                    string.Equals(candidate.alias, resource.Alias, StringComparison.OrdinalIgnoreCase));
                if (storedResource == null || string.IsNullOrWhiteSpace(storedResource.path))
                {
                    continue;
                }
                resource.Path = storedResource.path;
                resource.IsBuiltIn = storedResource.isBuiltIn;
            }
            ShowEditorMessage(
                string.Format(
                    localizer.GetLocalizedString("ConfigMakerPresetSavedMessage"),
                    presetDocument.Name),
                InfoBarSeverity.Success);
        }
        else if (!string.Equals(result.ErrorCode, "CANCELED", StringComparison.Ordinal))
        {
            ShowEditorMessage(
                string.Format(
                    localizer.GetLocalizedString("ConfigMakerPresetSaveFailedMessage"),
                    string.IsNullOrWhiteSpace(result.ErrorDetails)
                        ? result.ErrorCode
                        : result.ErrorDetails),
                InfoBarSeverity.Error);
        }
        return result;
    }

    private async void CreateVariableButton_Click(object sender, RoutedEventArgs e)
    {
        ConfigMakerVariableContentDialog dialog = new(presetDocument)
        {
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary &&
            dialog.ResultVariable != null)
        {
            presetDocument.Variables.Add(dialog.ResultVariable);
            AttachExistingPresetFiles();
            ToolsSelector.SelectIndex(1);
            RebuildPresetStructure();
            ScheduleDiagnostics();
        }
    }

    private async void EditVariableButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ConfigMakerVariableDefinition variable)
        {
            return;
        }
        await EditVariableAsync(variable);
    }

    private async Task EditVariableAsync(ConfigMakerVariableDefinition variable)
    {
        ConfigMakerVariableContentDialog dialog = new(presetDocument, variable)
        {
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary ||
            dialog.ResultVariable == null)
        {
            return;
        }
        int index = presetDocument.Variables.IndexOf(variable);
        if (index < 0)
        {
            return;
        }
        if (!string.Equals(dialog.OriginalName, dialog.ResultVariable.Name, StringComparison.Ordinal))
        {
            presetDocument.ReplaceVariableReference(
                dialog.OriginalName,
                dialog.ResultVariable.Name);
            SetEditorText(presetDocument.CommandText);
        }
        presetDocument.Variables[index] = dialog.ResultVariable;
        AttachExistingPresetFiles();
        RebuildPresetStructure();
        ScheduleDiagnostics();
    }

    private async void RemoveVariableButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ConfigMakerVariableDefinition variable)
        {
            return;
        }
        bool referenced = CommandText.Contains(variable.Reference, StringComparison.OrdinalIgnoreCase);
        if (referenced)
        {
            ContentDialog confirmation = new()
            {
                XamlRoot = XamlRoot,
                Title = localizer.GetLocalizedString("ConfigMakerRemoveReferencedVariableDialogTitle"),
                Content = localizer.GetLocalizedString("ConfigMakerRemoveReferencedVariableDialogMessage"),
                PrimaryButtonText = localizer.GetLocalizedString("Remove"),
                CloseButtonText = localizer.GetLocalizedString("Cancel"),
                DefaultButton = ContentDialogButton.Close,
            };
            if (await confirmation.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }
        presetDocument.Variables.Remove(variable);
        RebuildPresetStructure();
        ScheduleDiagnostics();
    }

    private void PresetVariablesListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (!IsEditorReadOnly &&
            PresetVariablesListView.SelectedItem is ConfigMakerVariableDefinition variable)
        {
            InsertAtCursor(variable.Reference);
        }
    }

    private void PresetDocument_ContentChanged(object sender, EventArgs e)
    {
        DocumentStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SyncDocumentFromEditor()
    {
        presetDocument.ComponentId = ComponentId;
        presetDocument.CommandText = CommandEditor.Text ?? string.Empty;
    }

    private static ConfigMakerVariableDefinition CloneVariable(ConfigMakerVariableDefinition source)
    {
        ConfigMakerVariableDefinition result = new()
        {
            Id = source.Id,
            Name = source.Name,
            Kind = source.Kind,
            StorageKind = source.StorageKind,
            Value = source.Value,
            Description = source.Description,
            OnValue = source.OnValue,
            OffValue = source.OffValue,
            InternalParameterName = source.InternalParameterName,
            IsSwitchEnabled = source.IsSwitchEnabled,
        };
        foreach (string value in source.Values)
        {
            result.Values.Add(value);
        }
        return result;
    }

    private static bool IsSimpleDesignerComponent(string componentId) =>
        string.Equals(componentId, HardcodedItemIds.ComponentIds[Components.GoodbyeDPI], StringComparison.OrdinalIgnoreCase) ||
        string.Equals(componentId, HardcodedItemIds.ComponentIds[Components.SpoofDPI], StringComparison.OrdinalIgnoreCase) ||
        string.Equals(componentId, HardcodedItemIds.ComponentIds[Components.NoDPI], StringComparison.OrdinalIgnoreCase);

    private void UpdateSimpleDesignerAvailability()
    {
        if (EditorViewSelector == null || SimpleDesignerSelectorItem == null)
        {
            return;
        }
        bool supported = IsSimpleDesignerSupported;
        bool contains = EditorViewSelector.Items.Contains(SimpleDesignerSelectorItem);
        if (supported && !contains)
        {
            EditorViewSelector.Items.Add(SimpleDesignerSelectorItem);
        }
        else if (!supported && contains)
        {
            if (ReferenceEquals(EditorViewSelector.SelectedItem, SimpleDesignerSelectorItem))
            {
                EditorViewSelector.SelectIndex(0);
            }
            EditorViewSelector.Items.Remove(SimpleDesignerSelectorItem);
            DesignerSettingItemModels.Clear();
            DesignerExclusiveSettingItemModels.Clear();
        }
    }

    private void EditorViewSelector_SelectionChanged(
        object sender,
        CDPIUI.Controls.Navigation.AnimatedSelectorBarSelectionChangedEventArgs e)
    {
        if (ReferenceEquals(e.OldItem, SimpleDesignerSelectorItem))
        {
            ApplySimpleDesignerToCommand();
        }
        if (ReferenceEquals(e.NewItem, SimpleDesignerSelectorItem) && designerNeedsRefresh)
        {
            LoadSimpleDesigner();
        }
    }

    private void LoadSimpleDesigner()
    {
        if (!IsSimpleDesignerSupported)
        {
            return;
        }
        updatingDesigner = true;
        try
        {
            AdditionalDesignerArgumentsTextBox.Text = ComponentCommandLineFormatter.ToSingleLine(CommandEditor.Text);
            DesignerSettingItemModels.Clear();
            DesignerExclusiveSettingItemModels.Clear();
            if (string.Equals(
                    ComponentId,
                    HardcodedItemIds.ComponentIds[Components.GoodbyeDPI],
                    StringComparison.OrdinalIgnoreCase))
            {
                GraphicDesignerHelper.LoadGoodbyeDPIDesignerConfig(
                    DesignerSettingItemModels,
                    DesignerExclusiveSettingItemModels);
            }
            else if (string.Equals(
                         ComponentId,
                         HardcodedItemIds.ComponentIds[Components.SpoofDPI],
                         StringComparison.OrdinalIgnoreCase))
            {
                GraphicDesignerHelper.LoadSpoofDPIDesignerConfig(
                    DesignerSettingItemModels,
                    DesignerExclusiveSettingItemModels);
            }
            else
            {
                string componentDirectory = DatabaseHelper.Instance.GetItemById(ComponentId)?.Directory ?? string.Empty;
                string annotationPath = Path.Combine(componentDirectory, "edannotationfile.xml");
                if (!File.Exists(annotationPath))
                {
                    ShowEditorMessage(
                        localizer.GetLocalizedString("ConfigMakerSimpleDesignerAnnotationMissingMessage"),
                        InfoBarSeverity.Warning);
                    return;
                }
                GraphicDesignerHelper.XML_LoadDesignerConfig(
                    annotationPath,
                    "nodpi",
                    DesignerSettingItemModels,
                    DesignerExclusiveSettingItemModels);
            }
            AttachSimpleDesignerCommands();
            AdditionalDesignerArgumentsTextBox.Text =
                GraphicDesignerHelper.ConvertStringToGraphicDesignerSettings(
                    DesignerSettingItemModels,
                    DesignerExclusiveSettingItemModels,
                    ComponentCommandLineFormatter.ToSingleLine(CommandEditor.Text));
            designerNeedsRefresh = false;
        }
        catch (Exception exception)
        {
            DesignerSettingItemModels.Clear();
            DesignerExclusiveSettingItemModels.Clear();
            Core.Basic.Logger.Instance.CreateWarningLog(nameof(ConfigMakerUserControl), exception.ToString());
            ShowEditorMessage(exception.Message, InfoBarSeverity.Warning);
        }
        finally
        {
            updatingDesigner = false;
        }
    }

    private void AttachSimpleDesignerCommands()
    {
        foreach (GraphicDesignerSettingItemModel item in DesignerSettingItemModels)
        {
            item.DesignerTextValueChangedCommand = DesignerTextValueChangedCommand;
            item.DesignerBoolValueToggledCommand = DesignerBoolValueToggledCommand;
        }
        foreach (GraphicDesignerExclusiveSettingItemModel group in DesignerExclusiveSettingItemModels)
        {
            group.DesignerTextValueChangedCommand = DesignerTextValueChangedCommand;
            group.DesignerBoolValueToggledCommand = DesignerBoolValueToggledCommand;
            group.DesignerSelectedGuidChangedCommand = DesignerSelectedGuidChangedCommand;
        }
    }

    private void HandleDesignerTextValueChanged(Tuple<string, string> change)
    {
        GraphicDesignerSettingItemModel item = FindDesignerItem(change.Item1);
        if (item != null)
        {
            item.Value = change.Item2;
            ApplySimpleDesignerToCommand();
        }
    }

    private void HandleDesignerBoolValueToggled(Tuple<string, bool> change)
    {
        GraphicDesignerSettingItemModel item = FindDesignerItem(change.Item1);
        if (item != null)
        {
            item.IsChecked = change.Item2;
            ApplySimpleDesignerToCommand();
        }
    }

    private void HandleDesignerSelectedGuidChanged(Tuple<string, string> change)
    {
        GraphicDesignerExclusiveSettingItemModel group = DesignerExclusiveSettingItemModels.FirstOrDefault(item =>
            string.Equals(item.Guid, change.Item1, StringComparison.Ordinal));
        if (group != null)
        {
            group.SelectedItemGuid = change.Item2;
            ApplySimpleDesignerToCommand();
        }
    }

    private GraphicDesignerSettingItemModel FindDesignerItem(string id) =>
        DesignerSettingItemModels.FirstOrDefault(item => string.Equals(item.Guid, id, StringComparison.Ordinal)) ??
        DesignerExclusiveSettingItemModels
            .SelectMany(group => group.Items)
            .FirstOrDefault(item => string.Equals(item.Guid, id, StringComparison.Ordinal));

    private void AdditionalDesignerArgumentsTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
        ApplySimpleDesignerToCommand();

    private void ApplySimpleDesignerToCommand()
    {
        if (updatingDesigner || designerNeedsRefresh || !IsSimpleDesignerSupported)
        {
            return;
        }
        updatingDesigner = true;
        try
        {
            string command = GraphicDesignerHelper.ConvertGraphicDesignerSettingsToString(
                DesignerSettingItemModels,
                DesignerExclusiveSettingItemModels,
                AdditionalDesignerArgumentsTextBox.Text ?? string.Empty);
            SetEditorText(ComponentCommandLineFormatter.FormatByFlags(command.Trim()));
            designerNeedsRefresh = false;
        }
        finally
        {
            updatingDesigner = false;
        }
    }

    private static void OnIsEditorReadOnlyChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        ConfigMakerUserControl control = (ConfigMakerUserControl)dependencyObject;
        bool isReadOnly = (bool)args.NewValue;
        control.ReplaceEditorButton.IsEnabled = !isReadOnly;
        control.InsertButton.IsEnabled = !isReadOnly && control.selectedCommand != null;
        control.EditorReadOnlyChanged?.Invoke(control, EventArgs.Empty);
        control.EditorSearchControl.ReplaceEnable = !isReadOnly;
    }

    private static void OnIsErrorCheckEnabled(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        // pass
    }

    public async Task StopTestAsync(bool showCompletionMessage = false)
    {
        if (!isTesting)
        {
            return;
        }

        string componentId = testComponentId;
        bool restore = restoreComponentAfterTest;
        isTesting = false;
        isStartingTest = false;
        ComponentTasksManager.Instance.TaskStateUpdated -= ComponentTasksManager_TaskStateUpdated;
        UpdateTestButtons();

        if (!string.IsNullOrWhiteSpace(componentId))
        {
            await ComponentTasksManager.Instance.StopTask(componentId);
            if (restore)
            {
                await ComponentTasksManager.Instance.CreateAndRunNewTask(componentId);
            }
        }

        testComponentId = string.Empty;
        restoreComponentAfterTest = false;
        if (showCompletionMessage)
        {
            ShowEditorMessage(
                localizer.GetLocalizedString("ConfigMakerTestStoppedMessage"),
                InfoBarSeverity.Informational);
        }
    }

    private static void OnComponentIdChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not ConfigMakerUserControl control)
        {
            return;
        }
        control.presetDocument.ComponentId = (string)args.NewValue;
        control.designerNeedsRefresh = true;
        control.UpdateSimpleDesignerAvailability();
        if (!control.updatingComponent && control.IsLoaded)
        {
            _ = control.SetComponentAsync((string)args.NewValue);
        }
    }

    private static void OnCommandTextChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not ConfigMakerUserControl control || control.updatingEditor)
        {
            return;
        }

        control.presetDocument.CommandText = (string)args.NewValue;
        control.designerNeedsRefresh = true;
        control.SetEditorText((string)args.NewValue, updateProperty: false);
    }

    private async void ConfigMakerUserControl_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateLayoutBounds();
        LoadEditorBackground();
        UpdateZoomStatus();
        RebuildDiagnostics();
        if (!string.IsNullOrWhiteSpace(ComponentId))
        {
            await SetComponentAsync(ComponentId);
        }
        else
        {
            ShowEditorMessage(
                localizer.GetLocalizedString("ConfigMakerSelectComponentMessage"),
                InfoBarSeverity.Informational);
        }
    }

    private void ConfigMakerUserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        helpCancellation?.Cancel();
        diagnosticsTimer.Stop();
        EditorSearchControl.Close();
        _ = StopTestAsync();
    }

    private void ConfigMakerUserControl_ActualThemeChanged(FrameworkElement sender, object args) => ApplyEditorTheme();

    private void ApplyEditorTheme()
    {
        bool usesForcedDarkBackground = editorBackground is "Black" or "MicaSmoke";
        bool isLight = !usesForcedDarkBackground &&
            (ActualTheme == ElementTheme.Light ||
             (ActualTheme == ElementTheme.Default && ((App)Application.Current).CurrentTheme == ElementTheme.Light));
        string[] errorTokens = Diagnostics
            .Where(item => item.Source.Severity == ComponentCommandDiagnosticSeverity.Error)
            .Select(item => item.Source.Token)
            .ToArray();
        string[] warningTokens = Diagnostics
            .Where(item => item.Source.Severity == ComponentCommandDiagnosticSeverity.Warning)
            .Select(item => item.Source.Token)
            .ToArray();
        string newSignature = $"{editorBackground}|{isLight}|{string.Join(',', errorTokens)}|{string.Join(',', warningTokens)}";
        if (string.Equals(highlightSignature, newSignature, StringComparison.Ordinal))
        {
            return;
        }

        highlightSignature = newSignature;
        CommandEditor.SyntaxHighlighting = isLight
            ? new LightDefaultHighlighter(errorTokens, warningTokens)
            : new DarkDefaultHighlighter(errorTokens, warningTokens);
        CommandEditor.Design = isLight
            ? TextControlBoxDesigns.DefaultLightDesign
            : TextControlBoxDesigns.DefaultDarkDesign;
    }

    public async Task SetComponentAsync(string componentId)
    {
        componentId = componentId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(componentId))
        {
            return;
        }

        if (isTesting && !string.Equals(testComponentId, componentId, StringComparison.OrdinalIgnoreCase))
        {
            await StopTestAsync();
        }

        updatingComponent = true;
        ComponentId = componentId;
        updatingComponent = false;
        presetDocument.ComponentId = componentId;
        ConfigOutput.ComponentId = componentId;
        UpdateSimpleDesignerAvailability();
        RebuildPresetStructure();
        await LoadHelpAsync(componentId, forceRefresh: false);
    }

    private async Task LoadHelpAsync(string componentId, bool forceRefresh)
    {
        helpCancellation?.Cancel();
        helpCancellation?.Dispose();
        helpCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = helpCancellation.Token;

        HelpProgressRing.IsActive = true;
        CommandListView.IsEnabled = false;
        UsageTextBlock.Text = localizer.GetLocalizedString("ConfigMakerLoadingHelpMessage");
        try
        {
            ComponentCommandHelpDocument document = await helpService.LoadAsync(
                componentId,
                cancellationToken,
                forceRefresh);
            if (cancellationToken.IsCancellationRequested ||
                !string.Equals(ComponentId, componentId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            helpDocument = document;
            UsageTextBlock.Text = !string.IsNullOrWhiteSpace(document.Usage)
                ? document.Usage
                : string.Format(
                    localizer.GetLocalizedString("ConfigMakerHelpOptionsLoadedMessage"),
                    document.Options.Count);
            RebuildCommandModules();
            RebuildCommandOptions();
            RebuildPresetStructure();
            RebuildDiagnostics();

            if (!string.IsNullOrWhiteSpace(document.Error))
            {
                ShowEditorMessage(
                    string.Format(
                        localizer.GetLocalizedString("ConfigMakerHelpLoadFailedMessage"),
                        document.Error),
                    InfoBarSeverity.Warning);
            }
            else if (document.Options.Count == 0)
            {
                ShowEditorMessage(
                    localizer.GetLocalizedString("ConfigMakerHelpParseEmptyMessage"),
                    InfoBarSeverity.Warning);
            }
            else
            {
                EditorInfoBar.IsOpen = false;
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                HelpProgressRing.IsActive = false;
                CommandListView.IsEnabled = true;
            }
        }
    }

    private void RebuildCommandOptions()
    {
        string query = CommandSearchBox.Text?.Trim() ?? string.Empty;
        IEnumerable<ComponentCommandHelpOption> filtered = helpDocument.Options;
        if (ModuleComboBox.SelectedItem is ConfigMakerCommandModuleViewModel module && !module.IsAll)
        {
            filtered = filtered.Where(option =>
                string.Equals(option.GroupName, module.GroupName, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(option =>
                option.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                option.Syntax.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                option.Description.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        CommandOptions.Clear();
        foreach (ComponentCommandHelpOption option in filtered
                     .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            CommandOptions.Add(new ConfigMakerCommandOptionViewModel
            {
                Source = option,
                Description = string.IsNullOrWhiteSpace(option.Description)
                    ? localizer.GetLocalizedString("ConfigMakerNoDescriptionMessage")
                    : option.Description,
            });
        }
    }

    private void RebuildCommandModules()
    {
        string previousGroup = (ModuleComboBox.SelectedItem as ConfigMakerCommandModuleViewModel)?.GroupName;
        bool previousWasAll = (ModuleComboBox.SelectedItem as ConfigMakerCommandModuleViewModel)?.IsAll != false;
        List<string> namedGroups = helpDocument.Options
            .Select(option => option.GroupName)
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        CommandModules.Clear();
        CommandModules.Add(new ConfigMakerCommandModuleViewModel
        {
            DisplayName = localizer.GetLocalizedString("ConfigMakerAllModules"),
            IsAll = true,
        });

        if (namedGroups.Count > 0 && helpDocument.Options.Any(option => string.IsNullOrWhiteSpace(option.GroupName)))
        {
            CommandModules.Add(new ConfigMakerCommandModuleViewModel
            {
                DisplayName = localizer.GetLocalizedString("ConfigMakerGeneralModule"),
                GroupName = string.Empty,
            });
        }

        foreach (string group in namedGroups)
        {
            CommandModules.Add(new ConfigMakerCommandModuleViewModel
            {
                DisplayName = group,
                GroupName = group,
            });
        }

        ModuleComboBox.Visibility = namedGroups.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ModuleComboBox.SelectedItem = previousWasAll
            ? CommandModules[0]
            : CommandModules.FirstOrDefault(item =>
                !item.IsAll && string.Equals(item.GroupName, previousGroup, StringComparison.OrdinalIgnoreCase))
                ?? CommandModules[0];
    }

    private void ModuleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        RebuildCommandOptions();

    private void CommandSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            RebuildCommandOptions();
        }
    }

    private void CommandListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        selectedCommand = CommandListView.SelectedItem as ConfigMakerCommandOptionViewModel;
        if (selectedCommand == null)
        {
            CommandDetailsBorder.Visibility = Visibility.Collapsed;
            CopyButton.IsEnabled = false;
            InsertButton.IsEnabled = false;
            Grid.SetRowSpan(CommandListView, 2);
            return;
        }

        ComponentCommandHelpOption option = selectedCommand.Source;
        SelectedCommandNameTextBlock.Text = option.DisplayName;
        SelectedCommandSyntaxTextBlock.Text = option.Syntax;
        SelectedCommandDescriptionTextBlock.Text = selectedCommand.Description;

        bool hasArgument = !string.IsNullOrWhiteSpace(option.ArgumentPlaceholder);
        CommandArgumentPanel.Visibility = hasArgument ? Visibility.Visible : Visibility.Collapsed;
        CopyButton.IsEnabled = true;
        InsertButton.IsEnabled = !IsEditorReadOnly;

        CommandArgumentTextBox.Text = string.Empty;
        CommandArgumentTextBox.PlaceholderText = option.ArgumentPlaceholder;
        CommandArgumentLabelTextBlock.Text = hasArgument
            ? string.Format(
                localizer.GetLocalizedString(
                    option.IsArgumentRequired
                        ? "ConfigMakerRequiredArgumentLabel"
                        : "ConfigMakerOptionalArgumentLabel"),
                option.ArgumentPlaceholder)
            : string.Empty;
        CommandDetailsBorder.Visibility = Visibility.Visible;
        Grid.SetRowSpan(CommandListView, 1);
    }

    public async Task RefreshHelpAsync()
    {
        if (!string.IsNullOrWhiteSpace(ComponentId))
        {
            await LoadHelpAsync(ComponentId, forceRefresh: true);
        }
    }

    private void InsertCommandButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsEditorReadOnly)
        {
            return;
        }
        string command = BuildSelectedCommand();
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        InsertAtCursor(command);
    }

    private void CopyCommandButton_Click(object sender, RoutedEventArgs e)
    {
        string command = BuildSelectedCommand();
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        DataPackage package = new();
        package.SetText(command);
        Clipboard.SetContent(package);
    }

    private string BuildSelectedCommand()
    {
        if (selectedCommand == null)
        {
            return string.Empty;
        }

        ComponentCommandHelpOption option = selectedCommand.Source;
        string command = option.DisplayName;
        if (string.IsNullOrWhiteSpace(option.ArgumentPlaceholder))
        {
            return command;
        }

        string argument = CommandArgumentTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(argument))
        {
            if (!option.IsArgumentRequired)
            {
                return command;
            }

            ShowEditorMessage(
                localizer.GetLocalizedString("ConfigMakerRequiredArgumentMissingMessage"),
                InfoBarSeverity.Warning);
            return string.Empty;
        }

        bool usesEquals = option.Syntax.Contains($"{command}=", StringComparison.OrdinalIgnoreCase);
        return usesEquals ? $"{command}={argument}" : $"{command} {argument}";
    }

    private void InsertAtCursor(string command)
    {
        string text = (CommandEditor.Text ?? string.Empty)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');
        Windows.Foundation.Point cursor = CommandEditor.GetCursorPosition();
        string[] lines = text.Split('\n');
        int lineIndex = Math.Clamp((int)cursor.Y, 0, Math.Max(0, lines.Length - 1));
        int characterIndex = Math.Clamp((int)cursor.X, 0, lines[lineIndex].Length);
        int absoluteIndex = lines.Take(lineIndex).Sum(line => line.Length + 1) + characterIndex;

        string before = text[..absoluteIndex];
        string after = text[absoluteIndex..];
        string prefix = before.Length > 0 && !char.IsWhiteSpace(before[^1]) ? "\n" : string.Empty;
        string suffix = after.Length > 0 && !char.IsWhiteSpace(after[0]) ? "\n" : string.Empty;
        string result = before + prefix + command + suffix + after;
        int newCursorIndex = before.Length + prefix.Length + command.Length;

        SetEditorText(result.Replace("\n", Environment.NewLine));
        SetCursorFromAbsoluteIndex(result, newCursorIndex);
        CommandEditor.Focus(FocusState.Programmatic);
    }

    private void SetCursorFromAbsoluteIndex(string normalizedText, int absoluteIndex)
    {
        string before = normalizedText[..Math.Clamp(absoluteIndex, 0, normalizedText.Length)];
        string[] lines = before.Split('\n');
        CommandEditor.SetCursorPosition(lines.Length - 1, lines[^1].Length);
    }

    public async Task NewDocumentAsync()
    {
        if (IsEditorReadOnly)
        {
            return;
        }
        if (!string.IsNullOrWhiteSpace(CommandEditor.Text) ||
            presetDocument.HasVariables ||
            presetDocument.HasResources)
        {
            ContentDialog dialog = new()
            {
                XamlRoot = XamlRoot,
                Title = localizer.GetLocalizedString("ConfigMakerNewConfirmationTitle"),
                Content = localizer.GetLocalizedString("ConfigMakerNewConfirmationMessage"),
                PrimaryButtonText = localizer.GetLocalizedString("Continue"),
                CloseButtonText = localizer.GetLocalizedString("Cancel"),
                DefaultButton = ContentDialogButton.Close,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        SetEditorText(string.Empty);
        presetBaseDirectory = string.Empty;
        presetDocument.Name = string.Empty;
        presetDocument.Variables.Clear();
        presetDocument.Resources.Clear();
        ClearPresetStructure();
        CommandEditor.ClearUndoRedoHistory();
    }

    public async Task SaveTextAsync()
    {
        try
        {
            string filePath = string.Empty;

            Microsoft.Win32.SaveFileDialog dialog = new()
            {
                OverwritePrompt = true,
                FileName = "config.cdpiconfig",
                DefaultExt = ".cdpiconfig",
                Filter = (!HasVariables ? localizer.GetLocalizedString("ConfigMakerTextFileFilter") + "|" : "") + localizer.GetLocalizedString("ConfigMakerCCFileFilter"),
            };
            dialog.FilterIndex = dialog.Filter.Count();
            if (dialog.ShowDialog() != true)
            {
                return;
            }
            filePath = dialog.FileName;

            SyncDocumentFromEditor();
            string exportText = ConfigMakerPresetStorageService
                .CreateResolvedCommandForTextExport(presetDocument);

            if (Path.GetExtension(filePath) == ".cdpiconfig")
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                using var package = await new ConfigShareService()
                    .ExportAsync(
                    presetDocument.ToConfigItem(Guid.NewGuid().ToString(), fileName),
                    fileName,
                    SettingsManager.Instance.GetValueOrDefault("CONFIGKIT", "lastUsedDevName", defaultValue: Environment.UserName));
                await FileSystemService.CopyFileAsync(package.ArchivePath, filePath);
            }
            else await File.WriteAllTextAsync(filePath, exportText);

            ShowEditorMessage(
                localizer.GetLocalizedString("ConfigMakerTextSavedMessage"),
                InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            ShowEditorMessage(
                string.Format(
                    localizer.GetLocalizedString("ConfigMakerTextSaveFailedMessage"),
                    exception.Message),
                InfoBarSeverity.Error);
        }
        
    }

    public void FormatCommand()
    {
        if (IsEditorReadOnly)
        {
            return;
        }
        SetEditorText(ComponentCommandLineFormatter.FormatByFlags(CommandEditor.Text));
        CommandEditor.SetCursorPosition(0, 0);
    }

    public async Task StartTestAsync()
    {
        if (string.IsNullOrWhiteSpace(ComponentId))
        {
            ShowEditorMessage(
                localizer.GetLocalizedString("ConfigMakerSelectComponentMessage"),
                InfoBarSeverity.Warning);
            return;
        }

        string arguments;
        try
        {
            SyncDocumentFromEditor();
            arguments = ComponentCommandLineFormatter.ToSingleLine(
                ConfigMakerPresetStorageService.CreateResolvedCommandForTest(presetDocument));
        }
        catch (Exception exception)
        {
            ShowEditorMessage(
                string.Format(
                    localizer.GetLocalizedString("ConfigMakerResolvePresetFailedMessage"),
                    exception.Message),
                InfoBarSeverity.Error);
            return;
        }
        if (string.IsNullOrWhiteSpace(arguments))
        {
            ShowEditorMessage(
                localizer.GetLocalizedString("ConfigMakerEmptyArgumentsMessage"),
                InfoBarSeverity.Warning);
            return;
        }

        await StopTestAsync();
        bool componentWasRunning = await ComponentTasksManager.Instance.IsTaskRunned(ComponentId);
        if (componentWasRunning)
        {
            ContentDialog confirmation = new()
            {
                XamlRoot = XamlRoot,
                Title = localizer.GetLocalizedString("ConfigMakerReplaceRunningComponentTitle"),
                Content = localizer.GetLocalizedString("ConfigMakerReplaceRunningComponentMessage"),
                PrimaryButtonText = localizer.GetLocalizedString("Continue"),
                CloseButtonText = localizer.GetLocalizedString("Cancel"),
                DefaultButton = ContentDialogButton.Close,
            };
            if (await confirmation.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        restoreComponentAfterTest = componentWasRunning;
        await ComponentTasksManager.Instance.StopTask(ComponentId);

        testComponentId = ComponentId;
        isTesting = true;
        isStartingTest = true;
        ComponentTasksManager.Instance.TaskStateUpdated += ComponentTasksManager_TaskStateUpdated;
        UpdateTestButtons();
        ShowEditorMessage(
            localizer.GetLocalizedString("ConfigMakerTestStartedMessage"),
            InfoBarSeverity.Informational);

        try
        {
            await ComponentTasksManager.Instance.CreateAndRunNewTask(testComponentId, arguments);
            isStartingTest = false;
            await ConfigOutput.RefreshComponentAsync();
        }
        catch (Exception exception)
        {
            isStartingTest = false;
            ShowEditorMessage(
                string.Format(
                    localizer.GetLocalizedString("ConfigMakerTestStartFailedMessage"),
                    exception.Message),
                InfoBarSeverity.Error);
            await StopTestAsync();
        }
    }

    private void ComponentTasksManager_TaskStateUpdated(Tuple<string, bool> state)
    {
        if (!isTesting || isStartingTest ||
            !string.Equals(state.Item1, testComponentId, StringComparison.OrdinalIgnoreCase) ||
            state.Item2)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(async () =>
        {
            ShowEditorMessage(
                localizer.GetLocalizedString("ConfigMakerTestExitedMessage"),
                InfoBarSeverity.Warning);
            await StopTestAsync();
        });
    }

    private void UpdateTestButtons()
    {
        TestStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CommandEditor_TextChanged(TextControlBox sender)
    {
        if (updatingEditor)
        {
            return;
        }
        if (IsEditorReadOnly)
        {
            SetEditorText(CommandText, updateProperty: false);
            return;
        }

        updatingEditor = true;
        CommandText = sender.Text ?? string.Empty;
        updatingEditor = false;
        presetDocument.CommandText = CommandText;
        designerNeedsRefresh = true;
        RebuildPresetStructure();
        CommandTextChanged?.Invoke(CommandText);
        ScheduleDiagnostics();
    }

    private void SetEditorText(string text, bool updateProperty = true)
    {
        updatingEditor = true;
        CommandEditor.Text = text ?? string.Empty;
        if (updateProperty)
        {
            CommandText = CommandEditor.Text;
        }
        presetDocument.CommandText = CommandEditor.Text;
        designerNeedsRefresh = true;
        updatingEditor = false;
        RebuildPresetStructure();
        RebuildDiagnostics();
        CommandTextChanged?.Invoke(CommandEditor.Text);
    }

    private void ScheduleDiagnostics()
    {
        diagnosticsTimer.Stop();
        diagnosticsTimer.Start();
    }

    private void DiagnosticsTimer_Tick(
        Microsoft.UI.Dispatching.DispatcherQueueTimer sender,
        object args)
    {
        sender.Stop();
        RebuildDiagnostics();
    }

    private void RebuildDiagnostics()
    {
        List<ComponentCommandDiagnostic> results = ComponentCommandValidationService
            .Validate(CommandEditor.Text, helpDocument.Options)
            .ToList();
        AddPresetReferenceDiagnostics(CommandEditor.Text, results);

        Diagnostics.Clear();
        foreach (ComponentCommandDiagnostic diagnostic in results)
        {
            Diagnostics.Add(new ConfigMakerDiagnosticViewModel
            {
                Source = diagnostic,
                SeverityText = localizer.GetLocalizedString(
                    diagnostic.Severity == ComponentCommandDiagnosticSeverity.Error
                        ? "ConfigMakerDiagnosticErrorSeverity"
                        : "ConfigMakerDiagnosticWarningSeverity"),
                Description = diagnostic.Kind switch
                {
                    ComponentCommandDiagnosticKind.UnknownFlag => string.Format(
                        localizer.GetLocalizedString("ConfigMakerUnknownFlagDiagnostic"),
                        diagnostic.Token),
                    ComponentCommandDiagnosticKind.MissingRequiredArgument => string.Format(
                        localizer.GetLocalizedString("ConfigMakerMissingArgumentDiagnostic"),
                        diagnostic.Token),
                    ComponentCommandDiagnosticKind.UnresolvedVariable => string.Format(
                        localizer.GetLocalizedString("ConfigMakerUnresolvedVariableDiagnostic"),
                        diagnostic.Token),
                    ComponentCommandDiagnosticKind.UnresolvedResource => string.Format(
                        localizer.GetLocalizedString("ConfigMakerUnresolvedResourceDiagnostic"),
                        diagnostic.Token),
                    _ => localizer.GetLocalizedString("ConfigMakerUnterminatedQuoteDiagnostic"),
                },
            });
        }

        int errorCount = results.Count(item => item.Severity == ComponentCommandDiagnosticSeverity.Error);
        int warningCount = results.Count - errorCount;
        ErrorCountTextBlock.Text = string.Format(
            localizer.GetLocalizedString("ConfigMakerDiagnosticErrorCount"),
            errorCount);
        WarningCountTextBlock.Text = string.Format(
            localizer.GetLocalizedString("ConfigMakerDiagnosticWarningCount"),
            warningCount);
        ApplyEditorTheme();
    }

    private void AddPresetReferenceDiagnostics(
        string commandText,
        ICollection<ComponentCommandDiagnostic> diagnostics)
    {
        string normalized = (commandText ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        string[] lines = normalized.Split('\n');
        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            foreach (Match match in Regex.Matches(
                         lines[lineIndex],
                         "%[A-Za-z][A-Za-z0-9_]*%",
                         RegexOptions.CultureInvariant))
            {
                string name = match.Value.Trim('%');
                bool known = presetDocument.Variables.Any(variable =>
                    string.Equals(variable.Name, name, StringComparison.OrdinalIgnoreCase));
                if (known ||
                    string.Equals(name, "CURRENT", StringComparison.OrdinalIgnoreCase) ||
                    Environment.GetEnvironmentVariable(name) != null)
                {
                    continue;
                }
                diagnostics.Add(new ComponentCommandDiagnostic
                {
                    Code = "CFG004",
                    Kind = ComponentCommandDiagnosticKind.UnresolvedVariable,
                    Severity = ComponentCommandDiagnosticSeverity.Error,
                    Token = match.Value,
                    Line = lineIndex + 1,
                    Column = match.Index + 1,
                });
            }
            foreach (Match match in Regex.Matches(
                         lines[lineIndex],
                         "preset://[A-Za-z0-9_.-]+",
                         RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                if (FindAttachedResource(match.Value) != null)
                {
                    continue;
                }
                diagnostics.Add(new ComponentCommandDiagnostic
                {
                    Code = "CFG005",
                    Kind = ComponentCommandDiagnosticKind.UnresolvedResource,
                    Severity = ComponentCommandDiagnosticSeverity.Error,
                    Token = match.Value,
                    Line = lineIndex + 1,
                    Column = match.Index + 1,
                });
            }
        }
    }

    private void DiagnosticsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DiagnosticsListView.SelectedItem is ConfigMakerDiagnosticViewModel diagnostic)
        {
            GoToDiagnostic(diagnostic);
        }
    }

    private void DiagnosticsListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (DiagnosticsListView.SelectedItem is ConfigMakerDiagnosticViewModel diagnostic)
        {
            GoToDiagnostic(diagnostic);
        }
    }

    private void PresetGroupsTreeView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (PresetGroupsTreeView.SelectedNode?.Content is not ConfigMakerPresetTreeItemViewModel item ||
            item.SourceIndex < 0 ||
            item.SourceLength <= 0)
        {
            return;
        }

        CommandEditor.Focus(FocusState.Programmatic);
        CommandEditor.SetCursorPosition(
            item.SourceLine,
            item.SourceColumn,
            scrollIntoView: true,
            autoClamp: true);
        CommandEditor.SetSelection(item.SourceIndex, item.SourceLength);
        CommandEditor.ScrollLineToCenter(item.SourceLine);
        e.Handled = true;
    }

    private void GoToDiagnostic(ConfigMakerDiagnosticViewModel diagnostic)
    {
        CommandEditor.SetCursorPosition(
            Math.Max(0, diagnostic.Source.Line - 1),
            Math.Max(0, diagnostic.Source.Column - 1));
        CommandEditor.Focus(FocusState.Programmatic);
    }

    public void ShowOutputTab()
    {
        SetBottomPanelVisible(true);
        BottomSelector.SelectIndex(0);
    }

    public void ShowDiagnosticsTab()
    {
        SetBottomPanelVisible(true);
        BottomSelector.SelectIndex(1);
    }

    public void ShowPresetGroupsTab()
    {
        if (!HasPresetGroups)
        {
            return;
        }
        SetBottomPanelVisible(true);
        BottomSelector.SelectIndex(BottomSelector.Items.IndexOf(PresetGroupsSelectorItem));
    }

    private void DiagnosticsStatusButton_Click(object sender, RoutedEventArgs e) =>
        ShowDiagnosticsTab();

    private void SearchButton_Click(object sender, RoutedEventArgs e) =>
        EditorSearchControl.ShowSearch(CommandEditor);

    private void ReplaceButton_Click(object sender, RoutedEventArgs e) =>
        EditorSearchControl.ShowReplace(CommandEditor);

    

    private void ZoomOutButton_Click(object sender, RoutedEventArgs e) => ChangeZoom(-5);

    public void ZoomInButton_Click(object sender, RoutedEventArgs e) => ChangeZoom(5);

    private void ZoomResetButton_Click(object sender, RoutedEventArgs e) => ChangeZoom(0);

    private void ChangeZoom(int delta)
    {
        CommandEditor.ZoomFactor = delta == 0
            ? 100
            : Math.Clamp(CommandEditor.ZoomFactor + delta, 25, 400);
        UpdateZoomStatus();
    }

    private void UpdateZoomStatus() =>
        ZoomResetButton.Content = $"{CommandEditor.ZoomFactor}%";

    private void LoadEditorBackground()
    {
        string savedBackground = SettingsManager.Instance.GetValue<string>(
            "APPEARANCE",
            "configEditorBackground");
        editorBackground = savedBackground switch
        {
            "Mica" => "Mica",
            "MicaTransparent" => "MicaTransparent",
            "MicaSmoke" => "MicaSmoke",
            _ => "Black",
        };
        DefaultBackgroundItem.IsChecked = editorBackground == "Black";
        MicaBackgroundItem.IsChecked = editorBackground == "Mica";
        MicaAltBackgroundItem.IsChecked = editorBackground == "MicaTransparent";
        MicaSmokeBackgroundItem.IsChecked = editorBackground == "MicaSmoke";
        ApplyEditorBackground();
    }

    private void EditorBackgroundItem_Click(object sender, RoutedEventArgs e)
    {
        editorBackground = sender switch
        {
            RadioMenuFlyoutItem item when ReferenceEquals(item, MicaBackgroundItem) => "Mica",
            RadioMenuFlyoutItem item when ReferenceEquals(item, MicaAltBackgroundItem) => "MicaTransparent",
            RadioMenuFlyoutItem item when ReferenceEquals(item, MicaSmokeBackgroundItem) => "MicaSmoke",
            _ => "Black",
        };
        SettingsManager.Instance.SetValue(
            "APPEARANCE",
            "configEditorBackground",
            editorBackground);
        ApplyEditorBackground();
    }

    private void ApplyEditorBackground()
    {
        EditorPanel.Background = editorBackground switch
        {
            "Mica" => GetApplicationBrush("LayerFillColorDefaultBrush"),
            "MicaTransparent" => new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
            "MicaSmoke" => GetApplicationBrush("SmokeFillColorDefaultBrush"),
            _ => new SolidColorBrush(Color.FromArgb(255, 0, 0, 0)),
        };
        highlightSignature = string.Empty;
        ApplyEditorTheme();
    }

    private static Brush GetApplicationBrush(string resourceKey) =>
        Application.Current.Resources.TryGetValue(resourceKey, out object resource) && resource is Brush brush
            ? brush
            : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

    public void SetCommandPanelVisible(bool visible)
    {
        if (visible == IsCommandPanelVisible)
        {
            return;
        }

        if (!visible)
        {
            savedEditorWidth = EditorWorkspace.ActualWidth;
        }

        CommandPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        VerticalContentSizer.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        VerticalSizerColumn.Width = visible ? new GridLength(12) : new GridLength(0);
        layoutInitialized = false;
        UpdateLayoutBounds();
        PanelStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetBottomPanelVisible(bool visible)
    {
        if (visible == IsBottomPanelVisible)
        {
            return;
        }

        if (!visible)
        {
            savedEditorHeight = EditorPanel.ActualHeight;
        }

        BottomPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        HorizontalContentSizer.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        HorizontalSizerRow.Height = visible ? new GridLength(12) : new GridLength(0);
        layoutInitialized = false;
        UpdateLayoutBounds();
        PanelStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateLayoutBounds();

    private void ResizablePanel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!updatingLayoutBounds)
        {
            UpdateLayoutBounds();
        }
    }

    private void UpdateLayoutBounds()
    {
        if (updatingLayoutBounds || RootGrid.ActualWidth <= 0 || RootGrid.ActualHeight <= 0)
        {
            return;
        }

        updatingLayoutBounds = true;
        try
        {
        const double editorMinWidth = 360;
        const double commandMinWidth = 340;
        const double editorMinHeight = 180;
        const double bottomMinHeight = 110;

        double filesWidth = 0;
        if (IsPresetFilesPanelVisible)
        {
            double maximumFilesWidth = Math.Max(
                150,
                RootGrid.ActualWidth - editorMinWidth -
                (IsCommandPanelVisible ? commandMinWidth + VerticalSizerColumn.Width.Value : 0) -
                PresetFilesSizerColumn.Width.Value);
            PresetFilesPanel.MaxWidth = Math.Max(
                PresetFilesPanel.MinWidth,
                Math.Min(RootGrid.ActualWidth * 0.45, maximumFilesWidth));
            PresetFilesPanel.Width = Math.Clamp(
                PresetFilesPanel.Width,
                PresetFilesPanel.MinWidth,
                PresetFilesPanel.MaxWidth);
            filesWidth = PresetFilesPanel.Width + PresetFilesSizerColumn.Width.Value;
        }

        double availableMainWidth = Math.Max(editorMinWidth, RootGrid.ActualWidth - filesWidth);

        if (IsCommandPanelVisible)
        {
            double maxEditorWidth = Math.Max(
                editorMinWidth,
                availableMainWidth - commandMinWidth - VerticalSizerColumn.Width.Value);
            EditorWorkspace.MaxWidth = maxEditorWidth;
            double requestedWidth = layoutInitialized
                ? EditorWorkspace.Width
                : Math.Min(savedEditorWidth, maxEditorWidth);
            EditorWorkspace.Width = maxEditorWidth;
        }
        else
        {
            EditorWorkspace.MaxWidth = availableMainWidth;
            EditorWorkspace.Width = availableMainWidth;
        }

        double availableMainHeight = Math.Max(editorMinHeight, RootGrid.ActualHeight);

        if (IsBottomPanelVisible)
        {
            double maxEditorHeight = Math.Max(
                editorMinHeight,
                availableMainHeight - bottomMinHeight - HorizontalSizerRow.Height.Value);
            EditorPanel.MaxHeight = maxEditorHeight;
            double requestedHeight = layoutInitialized
                ? EditorPanel.Height
                : Math.Min(savedEditorHeight, maxEditorHeight);
            EditorPanel.Height = Math.Clamp(requestedHeight, editorMinHeight, maxEditorHeight);
        }
        else
        {
            EditorPanel.MaxHeight = availableMainHeight;
            EditorPanel.Height = availableMainHeight;
        }

        layoutInitialized = true;
        }
        finally
        {
            updatingLayoutBounds = false;
        }
    }

    private void ShowEditorMessage(string message, InfoBarSeverity severity)
    {
        string title = severity switch
        {
            InfoBarSeverity.Error => localizer.GetLocalizedString("SomethingWentWrong"),
            InfoBarSeverity.Warning => localizer.GetLocalizedString("Attention"),
            _ => localizer.GetLocalizedString("Information"),
        };
        StatusNotificationRequested?.Invoke(
            this,
            new StatusNotificationRequestedEventArgs(severity, title, message));
        if (!UseInlineStatusMessages)
        {
            EditorInfoBar.IsOpen = false;
            return;
        }

        EditorInfoBar.Title = title;
        EditorInfoBar.Message = message;
        EditorInfoBar.Severity = severity;
        EditorInfoBar.IsOpen = true;
    }

    private void CommandEditor_ZoomChanged(TextControlBox sender, int zoomFactor)
    {
        UpdateZoomStatus();
    }

    private void CloseTreeViewButton_Click(object sender, RoutedEventArgs e)
    {
       SetPresetFilesPanelVisible(false);
    }

    private void CloseCommandsViewButton_Click(object sender, RoutedEventArgs e)
    {
        SetCommandPanelVisible(false);
    }

    private void CloseCommandsBottomPanelButton_Click(object sender, RoutedEventArgs e)
    {
        SetBottomPanelVisible(false);
    }
}
