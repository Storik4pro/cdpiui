using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;

namespace CDPIUI.Controls.BlockCheck2;

public sealed partial class BlockCheck2PresetEditorControl : UserControl
{
    private BlockCheck2PresetDraft? draft;
    private bool refreshingEditor;
    private bool updatingDraftFromEditor;

    public BlockCheck2PresetEditorControl()
    {
        InitializeComponent();
        ConfigMaker.ComponentId = HardcodedItemIds.ComponentIds[Components.Zapret2];
        ConfigMaker.SetCommandPanelVisible(false);
        ConfigMaker.CommandTextChanged += ConfigMaker_CommandTextChanged;
        ConfigMaker.PresetFileReplaced += ConfigMaker_PresetFileReplaced;
    }

    public ConfigMakerUserControl Editor => ConfigMaker;

    public void LoadDraft(BlockCheck2PresetDraft value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (draft != null)
        {
            draft.PropertyChanged -= Draft_PropertyChanged;
        }
        draft = value;
        draft.PropertyChanged += Draft_PropertyChanged;
        RefreshFromDraft();
    }

    public void SetExpertEditing(bool enabled)
    {
        if (draft == null)
        {
            return;
        }
        if (enabled)
        {
            draft.BeginExpertEditing();
        }
        else
        {
            draft.DiscardExpertChanges();
        }
        RefreshFromDraft();
    }

    private void Draft_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!updatingDraftFromEditor &&
            (e.PropertyName is nameof(BlockCheck2PresetDraft.EffectiveArguments) or
                nameof(BlockCheck2PresetDraft.IsExpertEditingEnabled)))
        {
            RefreshEditor();
        }
        if (e.PropertyName is nameof(BlockCheck2PresetDraft.StructuredArguments) or
            nameof(BlockCheck2PresetDraft.Files))
        {
            RebuildTrees();
        }
    }

    private void RefreshFromDraft()
    {
        RefreshEditor();
        RebuildTrees();
    }

    private void RefreshEditor()
    {
        if (draft == null)
        {
            return;
        }
        refreshingEditor = true;
        try
        {
            ConfigMaker.CommandText = ComponentCommandLineFormatter.FormatByFlags(
                draft.EffectiveArguments);
            ConfigMaker.IsEditorReadOnly = !draft.IsExpertEditingEnabled;
            ConfigMaker.SetCommandPanelVisible(draft.IsExpertEditingEnabled);
        }
        finally
        {
            refreshingEditor = false;
        }
    }

    private void RebuildTrees()
    {
        if (draft == null)
        {
            ConfigMaker.ClearPresetStructure();
            return;
        }
        ConfigMaker.SetPresetStructure(
            draft.Files.Select(file => new ConfigMakerPresetFileInfo(
                file.Name,
                file.Path,
                file.Folder,
                file.Kind switch
                {
                    BlockCheck2PresetFileKind.Library => ConfigMakerPresetFileKind.Library,
                    BlockCheck2PresetFileKind.Payload => ConfigMakerPresetFileKind.Payload,
                    _ => ConfigMakerPresetFileKind.SiteList,
                })),
            draft.Groups.Select(group => new ConfigMakerPresetGroupInfo(
                group.Name,
                [group.Route, group.Scope, group.Strategies])));
    }

    private void ConfigMaker_CommandTextChanged(string text)
    {
        if (!refreshingEditor && draft?.IsExpertEditingEnabled == true)
        {
            updatingDraftFromEditor = true;
            try
            {
                draft.SetExpertArguments(text);
            }
            finally
            {
                updatingDraftFromEditor = false;
            }
        }
    }

    private void ConfigMaker_PresetFileReplaced(
        object? sender,
        ConfigMakerPresetFileReplacedEventArgs e)
    {
        if (draft != null)
        {
            draft.ApplyFileReferenceChange(
                e.CommandText,
                e.OriginalPath,
                e.ReplacementPath);
        }
    }

}
