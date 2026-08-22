using CDPIUI.AddOns.ConfigImport;
using CDPIUI.Controls.Dialogs.ComponentSettings;
using CDPIUI.Controls.Universal;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.System;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Helper.UserExperience;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using TextControlBoxNS;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;
using WinUI3Localizer;

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
    string OptionName = "");

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

    public ICommand ZoomIn { get; }
    public ICommand ZoomOut { get; }
    public ICommand ZoomReset { get; }
    public ICommand Search { get; }
    public ICommand Replace { get; }

    public ConfigMakerUserControl()
    {
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
        ApplyEditorTheme();

        Loaded += ConfigMakerUserControl_Loaded;
        Unloaded += ConfigMakerUserControl_Unloaded;
        ActualThemeChanged += ConfigMakerUserControl_ActualThemeChanged;
    }

    public ObservableCollection<ConfigMakerCommandOptionViewModel> CommandOptions { get; } = [];
    public ObservableCollection<ConfigMakerCommandModuleViewModel> CommandModules { get; } = [];
    public ObservableCollection<ConfigMakerDiagnosticViewModel> Diagnostics { get; } = [];



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

    public bool IsTesting => isTesting;
    public bool IsCommandPanelVisible => CommandPanel.Visibility == Visibility.Visible;
    public bool IsBottomPanelVisible => BottomPanel.Visibility == Visibility.Visible;
    public bool IsPresetFilesPanelVisible => PresetFilesPanel.Visibility == Visibility.Visible;
    public bool IsPresetStructureVisible => hasPresetFiles || HasPresetGroups;
    public bool HasPresetFiles => hasPresetFiles;
    public bool HasPresetGroups => hasPresetGroups || !usesExplicitPresetStructure;

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
        ConfigMakerPresetFileInfo[] files = usesExplicitPresetStructure
            ? EnrichExplicitPresetFiles(explicitPresetFiles, detectedFiles)
            : detectedFiles;

        RebuildPresetFilesTree(files);
        RebuildPresetGroupsTree(ParseCommandOptions(CommandText));

        bool filesAvailabilityChanged = hasPresetFiles != (files.Length > 0);
        bool groupsAvailabilityChanged = hasPresetGroups != (PresetGroupsTreeView.RootNodes.Count > 0);
        hasPresetFiles = files.Length > 0;
        hasPresetGroups = PresetGroupsTreeView.RootNodes.Count > 0;

        if (filesPanelWasVisible && !hasPresetFiles)
        {
            SetPresetFilesPanelVisible(false);
        }
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
        visible &= hasPresetFiles;
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

    private ConfigImportResult CreateAutoCorrectResult(string missingPath)
    {
        var component = DatabaseHelper.Instance.GetItemById(ComponentId);
        string componentDirectory = component?.Directory ?? string.Empty;
        string sourceDirectory = Directory.Exists(componentDirectory)
            ? componentDirectory
            : Path.GetDirectoryName(missingPath) ?? Environment.CurrentDirectory;
        return new ConfigImportResult
        {
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

    private void TryCreateEmptyPresetFile(string missingPath)
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
            ShowEditorMessage(
                string.Format(
                    localizer.GetLocalizedString("ConfigMakerPresetFileEmptyCreatedMessage"),
                    missingPath),
                InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowEditorMessage(
                string.Format(
                    localizer.GetLocalizedString("ConfigMakerPresetFileReplaceFailedMessage"),
                    ex.Message),
                InfoBarSeverity.Error);
        }
    }

    private void ApplyPresetFileReplacement(ConfigMakerPresetFileInfo file, string replacementPath)
    {
        try
        {
            string fullReplacementPath = Path.GetFullPath(replacementPath);
            if (!File.Exists(fullReplacementPath))
            {
                throw new FileNotFoundException(null, fullReplacementPath);
            }

            string commandPath = MakePresetCommandPath(fullReplacementPath);
            string updatedCommand = ReplacePresetFileInCommand(CommandText, file, commandPath);
            if (string.Equals(updatedCommand, CommandText, StringComparison.Ordinal))
            {
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
                                Path = commandPath,
                                Folder = GetPresetDisplayFolder(commandPath),
                            }
                            : item)
                    .ToArray();
            }
            CommandText = ComponentCommandLineFormatter.FormatByFlags(updatedCommand);
            PresetFileReplaced?.Invoke(
                this,
                new ConfigMakerPresetFileReplacedEventArgs(
                    CommandText,
                    file.Path,
                    commandPath));
            ShowEditorMessage(
                string.Format(
                    localizer.GetLocalizedString("ConfigMakerPresetFileReplacedMessage"),
                    fullReplacementPath),
                InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowEditorMessage(
                string.Format(
                    localizer.GetLocalizedString("ConfigMakerPresetFileReplaceFailedMessage"),
                    ex.Message),
                InfoBarSeverity.Error);
        }
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

    private string ResolvePresetFilePath(string sourcePath)
    {
        string path = (sourcePath ?? string.Empty).Trim().Trim('"');
        if (path.StartsWith("@", StringComparison.Ordinal) ||
            path.StartsWith("$", StringComparison.Ordinal))
        {
            path = path[1..];
        }
        if (Path.IsPathFullyQualified(path))
        {
            return Path.GetFullPath(path);
        }

        path = path.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string componentDirectory = DatabaseHelper.Instance.GetItemById(ComponentId)?.Directory ?? string.Empty;
        return string.IsNullOrWhiteSpace(componentDirectory)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(componentDirectory, path));
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

    private string MakePresetCommandPath(string fullPath)
    {
        string componentDirectory = DatabaseHelper.Instance.GetItemById(ComponentId)?.Directory ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(componentDirectory))
        {
            try
            {
                string relativePath = Path.GetRelativePath(
                    Path.GetFullPath(componentDirectory),
                    Path.GetFullPath(fullPath));
                if (!Path.IsPathRooted(relativePath) &&
                    !relativePath.Equals("..", StringComparison.Ordinal) &&
                    !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    return $"/{relativePath.Replace(Path.DirectorySeparatorChar, '/')}";
                }
            }
            catch
            {
            }
        }
        return Path.GetFullPath(fullPath);
    }

    private static string GetPresetDisplayFolder(string path)
    {
        string normalized = NormalizePresetPath(path);
        int separator = normalized.LastIndexOf('/');
        return separator > 0 ? normalized[..separator] : string.Empty;
    }

    private static bool PresetPathsEqual(string left, string right) =>
        string.Equals(
            NormalizePresetPath(left).TrimStart('/'),
            NormalizePresetPath(right).TrimStart('/'),
            StringComparison.OrdinalIgnoreCase);

    private static string NormalizePresetPath(string value)
    {
        string normalized = UnquoteOptionValue(value).Trim();
        if (normalized.StartsWith('@') || normalized.StartsWith('$'))
        {
            normalized = normalized[1..];
        }
        return normalized.Replace('\\', '/');
    }

    private string ReplacePresetFileInCommand(
        string commandText,
        ConfigMakerPresetFileInfo file,
        string replacementPath)
    {
        List<string> tokens = ComponentCommandLineFormatter.Tokenize(commandText).ToList();
        bool replaced = false;
        for (int index = 0; index < tokens.Count; index++)
        {
            string token = tokens[index];
            if (!TryGetOptionName(token, out string optionName, out int equalsIndex))
            {
                continue;
            }
            if (equalsIndex >= 0)
            {
                string value = token[(equalsIndex + 1)..];
                if (TryReplacePresetOptionValue(
                    optionName,
                    value,
                    file.Path,
                    file.Kind,
                    replacementPath,
                    out string updatedValue))
                {
                    tokens[index] = $"{token[..equalsIndex]}={updatedValue}";
                    replaced = true;
                }
            }
            else if (index + 1 < tokens.Count && !IsCommandOption(tokens[index + 1]) &&
                TryReplacePresetOptionValue(
                    optionName,
                    tokens[index + 1],
                    file.Path,
                    file.Kind,
                    replacementPath,
                    out string updatedValue))
            {
                tokens[index + 1] = updatedValue;
                replaced = true;
            }
        }
        return replaced ? string.Join(' ', tokens) : commandText;
    }

    private static bool TryReplacePresetOptionValue(
        string optionName,
        string value,
        string missingPath,
        ConfigMakerPresetFileKind missingKind,
        string replacementPath,
        out string updatedValue)
    {
        updatedValue = value;
        ConfigMakerPresetFileInfo detected = TryExtractPresetFile(new ParsedPresetOption(
            optionName,
            $"{optionName}={value}",
            value));
        if (detected == null ||
            detected.Kind != missingKind ||
            !PresetPathsEqual(detected.Path, missingPath))
        {
            return false;
        }

        string unquoted = UnquoteOptionValue(value);
        if (IsSiteListOption(optionName))
        {
            updatedValue = QuoteOptionValue(replacementPath, force: true);
            return true;
        }
        if (string.Equals(optionName, "--lua-init", StringComparison.OrdinalIgnoreCase))
        {
            char marker = unquoted.Length > 0 && (unquoted[0] == '@' || unquoted[0] == '$')
                ? unquoted[0]
                : '@';
            updatedValue = QuoteOptionValue($"{marker}{replacementPath}", force: false);
            return true;
        }
        if (string.Equals(optionName, "--blob", StringComparison.OrdinalIgnoreCase))
        {
            int separator = unquoted.IndexOf(':');
            string prefix = separator >= 0 ? unquoted[..(separator + 1)] : string.Empty;
            string source = separator >= 0 ? unquoted[(separator + 1)..] : unquoted;
            char marker = source.Length > 0 && (source[0] == '@' || source[0] == '$')
                ? source[0]
                : '@';
            updatedValue = QuoteOptionValue($"{prefix}{marker}{replacementPath}", force: false);
            return true;
        }

        updatedValue = QuoteOptionValue(replacementPath, force: false);
        return true;
    }

    private static string QuoteOptionValue(string value, bool force)
    {
        bool quote = force || value.Any(char.IsWhiteSpace) || value.Contains('"');
        return quote
            ? $"\"{value.Replace("\"", "\\\"")}\""
            : value;
    }

    private static IEnumerable<ConfigMakerPresetFileInfo> ExtractPresetFiles(string commandText)
    {
        return ParseCommandOptions(commandText)
            .Select(TryExtractPresetFile)
            .Where(file => file != null)
            .DistinctBy(file => (file.Kind, file.Path), PresetFileKeyComparer.Instance);
    }

    private static ConfigMakerPresetFileInfo TryExtractPresetFile(ParsedPresetOption option)
    {
        if (string.IsNullOrWhiteSpace(option.Value))
        {
            return null;
        }

        ConfigMakerPresetFileKind kind;
        string path = UnquoteOptionValue(option.Value);
        if (IsSiteListOption(option.Name))
        {
            kind = ConfigMakerPresetFileKind.SiteList;
        }
        else if (string.Equals(option.Name, "--lua-init", StringComparison.OrdinalIgnoreCase))
        {
            kind = ConfigMakerPresetFileKind.Library;
        }
        else if (string.Equals(option.Name, "--blob", StringComparison.OrdinalIgnoreCase))
        {
            kind = ConfigMakerPresetFileKind.Payload;
            int separator = path.IndexOf(':');
            path = separator >= 0 ? path[(separator + 1)..] : path;
        }
        else
        {
            return null;
        }

        path = path.Trim();
        if (path.StartsWith('@') || path.StartsWith('$'))
        {
            path = path[1..];
        }
        path = UnquoteOptionValue(path);
        if (string.IsNullOrWhiteSpace(path) ||
            (kind != ConfigMakerPresetFileKind.SiteList && !LooksLikeFilePath(path)))
        {
            return null;
        }

        string normalized = NormalizePresetPath(path);
        string name = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? normalized;
        return new ConfigMakerPresetFileInfo(
            name,
            normalized,
            GetPresetDisplayFolder(normalized),
            kind,
            option.Name);
    }

    private static bool LooksLikeFilePath(string path) =>
        path.Contains('/') || path.Contains('\\') || !string.IsNullOrWhiteSpace(Path.GetExtension(path));

    private static bool IsSiteListOption(string optionName) => optionName.ToLowerInvariant() is
        "--hostlist" or
        "--hostlist-exclude" or
        "--hostlist-auto" or
        "--ipset" or
        "--ipset-exclude";

    private static IReadOnlyList<ParsedPresetOption> ParseCommandOptions(string commandText)
    {
        commandText ??= string.Empty;
        IReadOnlyList<string> tokens = ComponentCommandLineFormatter.Tokenize(commandText);
        List<ParsedPresetOption> result = [];
        int searchIndex = 0;
        for (int index = 0; index < tokens.Count; index++)
        {
            string token = tokens[index];
            if (!TryGetOptionName(token, out string name, out int equalsIndex))
            {
                continue;
            }

            string value = equalsIndex >= 0 ? token[(equalsIndex + 1)..] : string.Empty;
            string displayText = token;
            int sourceIndex = commandText.IndexOf(token, searchIndex, StringComparison.Ordinal);
            int sourceEnd = sourceIndex >= 0 ? sourceIndex + token.Length : -1;
            if (equalsIndex < 0 && index + 1 < tokens.Count && !IsCommandOption(tokens[index + 1]))
            {
                value = tokens[++index];
                displayText = $"{token} {value}";
                int valueIndex = commandText.IndexOf(
                    value,
                    Math.Max(searchIndex, sourceEnd),
                    StringComparison.Ordinal);
                if (valueIndex >= 0)
                {
                    sourceEnd = valueIndex + value.Length;
                }
            }
            int sourceLength = sourceIndex >= 0 && sourceEnd >= sourceIndex
                ? sourceEnd - sourceIndex
                : 0;
            (int sourceLine, int sourceColumn) = GetSourcePosition(commandText, sourceIndex);
            result.Add(new ParsedPresetOption(
                name,
                displayText,
                value,
                sourceIndex,
                sourceLength,
                sourceLine,
                sourceColumn));
            if (sourceEnd >= 0)
            {
                searchIndex = sourceEnd;
            }
        }
        return result;
    }

    private static (int Line, int Column) GetSourcePosition(string text, int sourceIndex)
    {
        if (sourceIndex < 0)
        {
            return (0, 0);
        }

        int line = 0;
        int lineStart = 0;
        for (int index = 0; index < sourceIndex; index++)
        {
            if (text[index] == '\n')
            {
                line++;
                lineStart = index + 1;
            }
        }
        return (line, sourceIndex - lineStart);
    }

    private static bool TryGetOptionName(string token, out string name, out int equalsIndex)
    {
        equalsIndex = token.IndexOf('=');
        name = equalsIndex >= 0 ? token[..equalsIndex] : token;
        return IsCommandOption(name);
    }

    private static bool IsCommandOption(string token) =>
        token.StartsWith("--", StringComparison.Ordinal) ||
        (token.Length > 1 && token[0] == '-');

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

    private static string UnquoteOptionValue(string value)
    {
        string result = (value ?? string.Empty).Trim();
        if (result.Length >= 2 &&
            ((result[0] == '"' && result[^1] == '"') ||
             (result[0] == '\'' && result[^1] == '\'')))
        {
            result = result[1..^1];
        }
        return result.Replace("\\\"", "\"");
    }

    private sealed record ParsedPresetOption(
        string Name,
        string DisplayText,
        string Value,
        int SourceIndex = -1,
        int SourceLength = 0,
        int SourceLine = 0,
        int SourceColumn = 0);

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
        if (dependencyObject is ConfigMakerUserControl control &&
            !control.updatingComponent &&
            control.IsLoaded)
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
        ConfigOutput.ComponentId = componentId;
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
        if (!string.IsNullOrWhiteSpace(CommandEditor.Text))
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
        CommandEditor.ClearUndoRedoHistory();
    }

    public async Task SaveTextAsync()
    {
        Microsoft.Win32.SaveFileDialog dialog = new()
        {
            OverwritePrompt = true,
            FileName = "config.txt",
            DefaultExt = ".txt",
            Filter = localizer.GetLocalizedString("ConfigMakerTextFileFilter"),
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await File.WriteAllTextAsync(dialog.FileName, CommandEditor.Text ?? string.Empty);
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

        string arguments = ComponentCommandLineFormatter.ToSingleLine(CommandEditor.Text);
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
        IReadOnlyList<ComponentCommandDiagnostic> results =
            ComponentCommandValidationService.Validate(CommandEditor.Text, helpDocument.Options);

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
