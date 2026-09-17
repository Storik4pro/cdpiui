using CDPIUI.Controls.ComponentSettings;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.Store.Database;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CDPIUI.Helper;

internal sealed class ComponentPresetListSubscription : IDisposable
{
    private readonly string componentId;
    private readonly DispatcherQueue dispatcher;
    private readonly Action<IReadOnlyList<ConfigSelectorItem>> apply;
    private ComponentHelper helper;
    private bool disposed;
    private int version;

    public ComponentPresetListSubscription(string componentId, DispatcherQueue dispatcher,
        Action<IReadOnlyList<ConfigSelectorItem>> apply)
    {
        this.componentId = componentId;
        this.dispatcher = dispatcher;
        this.apply = apply;
        ComponentItemsLoaderHelper.Instance.InitRequested += Rebind;
        Rebind();
    }

    private void Rebind() => dispatcher.TryEnqueue(() =>
    {
        if (disposed) return;
        var current = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(componentId);
        if (!ReferenceEquals(current, helper))
        {
            if (helper != null) helper.ConfigListUpdated -= Refresh;
            helper = current;
            if (helper != null) helper.ConfigListUpdated += Refresh;
        }
        Refresh();
    });

    public void Refresh() => dispatcher.TryEnqueue(async () =>
    {
        if (disposed) return;
        int request = ++version;
        var source = helper;
        try
        {
            var items = await Task.Run(() => source?.GetConfigHelper().GetConfigItems()
                .Where(item => !item.MarkAsRemoved)
                .Select(item => new ConfigSelectorItem
                {
                    FileName = item.file_name, PackId = item.packId, DisplayName = item.name,
                    PackDisplayName = DatabaseHelper.Instance.GetItemById(item.packId)?.ShortName ?? item.packId,
                    IsLegacyConfig = item.IsLegacy
                }).ToArray() ?? Array.Empty<ConfigSelectorItem>());
            if (!disposed && request == version && ReferenceEquals(source, helper)) apply(items);
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(nameof(ComponentPresetListSubscription), exception.ToString());
        }
    });

    public void Dispose()
    {
        disposed = true;
        ++version;
        ComponentItemsLoaderHelper.Instance.InitRequested -= Rebind;
        if (helper != null) helper.ConfigListUpdated -= Refresh;
        helper = null;
    }
}
