using System.Globalization;
using System.Text;
using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Analysis;

public static class CandidateNormalizer
{
    public static string GetPlanSignature(StrategyDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        StringBuilder signature = new();

        signature.Append("prehost=")
            .Append(definition.RequiresPreHost ? '1' : '0')
            .Append(";inbound=")
            .Append(definition.RequiresInboundTraffic ? '1' : '0');

        foreach (BlobDefinition blob in definition.Blobs.OrderBy(blob => blob.Name, StringComparer.Ordinal))
        {
            signature.Append(";blob=")
                .Append(blob.Name.Trim())
                .Append(':')
                .Append(blob.Source.Trim());
        }

        foreach (LuaActionDefinition action in definition.Actions)
        {
            signature.Append(";action=")
                .Append(action.Function.Trim().ToLowerInvariant())
                .Append("|payload=")
                .AppendJoin(',', action.Payloads.Select(payload => payload.Trim().ToLowerInvariant()).Order())
                .Append("|in=")
                .Append(action.InRange.Trim().ToLowerInvariant())
                .Append("|out=")
                .Append(action.OutRange.Trim().ToLowerInvariant());

            foreach (LuaArgumentDefinition argument in action.Arguments)
            {
                signature.Append("|arg=")
                    .Append(argument.Name.Trim().ToLowerInvariant())
                    .Append('=')
                    .Append(argument.Value?.Trim() ?? string.Empty);
            }
        }

        return signature.ToString();
    }

    public static string GetDisplayFingerprint(StrategyDefinition definition) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            Encoding.UTF8.GetBytes(GetPlanSignature(definition))))[..16]
            .ToLower(CultureInfo.InvariantCulture);
}
