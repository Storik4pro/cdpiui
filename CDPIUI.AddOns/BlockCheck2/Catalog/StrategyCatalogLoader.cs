using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Catalog;

public static class StrategyCatalogLoader
{
    private const string EmbeddedCatalogSuffix = "BlockCheck2.Catalog.strategy-catalog.v1.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static StrategyCatalog LoadBuiltIn()
    {
        Assembly assembly = typeof(StrategyCatalogLoader).Assembly;
        string? resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(EmbeddedCatalogSuffix, StringComparison.Ordinal));

        if (resourceName == null)
        {
            throw new InvalidOperationException(
                $"Embedded BlockCheck2 catalog '{EmbeddedCatalogSuffix}' was not found.");
        }

        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded BlockCheck2 catalog '{resourceName}' could not be opened.");

        return BuiltInStrategyCatalogExpander.Expand(Load(stream));
    }

    public static StrategyCatalog Load(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using MemoryStream stream = new(System.Text.Encoding.UTF8.GetBytes(json));
        return Load(stream);
    }

    public static StrategyCatalog Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return JsonSerializer.Deserialize<StrategyCatalog>(stream, SerializerOptions)
            ?? throw new InvalidDataException("The BlockCheck2 strategy catalog is empty.");
    }
}
