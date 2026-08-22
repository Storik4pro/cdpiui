using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.Store.Database;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CDPIUI.Helper.CreateConfigHelper;

public sealed class ConfigMakerComponentInfo
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string ExecutableName { get; init; } = string.Empty;

    public override string ToString() => $"{DisplayName} ({ExecutableName})";
}

public static class ConfigMakerComponentCatalog
{
    public static IReadOnlyList<ConfigMakerComponentInfo> GetAvailableComponents() =>
        ComponentItemsLoaderHelper.Instance
            .GetComponentHelpers()
            .Select(CreateComponentInfo)
            .Where(component => component != null)
            .Cast<ConfigMakerComponentInfo>()
            .OrderBy(component => component.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    private static ConfigMakerComponentInfo CreateComponentInfo(ComponentHelper helper)
    {
        DatabaseStoreItem item = DatabaseHelper.Instance.GetItemById(helper.Id);
        string executablePath = helper.GetExecutablePath();
        if (item == null || string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return null;
        }

        return new ConfigMakerComponentInfo
        {
            Id = helper.Id,
            DisplayName = item.ShortName ?? item.Name ?? helper.Id,
            ExecutableName = Path.GetFileName(executablePath),
        };
    }
}
