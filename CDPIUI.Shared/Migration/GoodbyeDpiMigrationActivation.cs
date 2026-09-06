using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CDPIUI.Shared.Migration
{
    public sealed class GoodbyeDpiMigrationActivationRequest
    {
        public int ProtocolVersion { get; init; }
        public string ArchivePath { get; init; } = string.Empty;
        public string ArchiveSha256 { get; init; } = string.Empty;
        public Guid MigrationId { get; init; }
        public string ResponsePipeName { get; init; } = string.Empty;
        public string SessionToken { get; init; } = string.Empty;
        public string RawArgument { get; init; } = string.Empty;
    }

    /// <summary>
    /// Private activation contract between GDPIUI-Updater and CDPIUI. Variable-length
    /// values are base64url encoded so the validated argument can pass through TrayIcon.
    /// </summary>
    public static class GoodbyeDpiMigrationActivation
    {
        public const int CurrentProtocolVersion = 1;
        public const string ArgumentPrefix = "--gdpiui-migration=";
        public const string ResponsePipePrefix = "GDPIUI-Updater-";

        private const int MaximumArgumentLength = 16384;
        private const int MaximumArchivePathLength = 4096;
        private const int MaximumPipeNameLength = 180;

        public static bool IsActivationArgument(string? argument) =>
            TryParseArgument(argument, out _);

        public static bool TryFindArgument(
            IEnumerable<string>? arguments,
            out GoodbyeDpiMigrationActivationRequest? request)
        {
            request = null;
            if (arguments == null)
                return false;

            foreach (string argument in arguments)
            {
                if (TryParseArgument(argument, out request))
                    return true;
            }
            return false;
        }

        public static bool TryFindArgument(
            string? commandLineArguments,
            out GoodbyeDpiMigrationActivationRequest? request)
        {
            request = null;
            return !string.IsNullOrWhiteSpace(commandLineArguments) && TryFindArgument(
                commandLineArguments.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries),
                out request);
        }

        public static bool TryParseArgument(
            string? argument,
            out GoodbyeDpiMigrationActivationRequest? request)
        {
            request = null;
            string raw = (argument ?? string.Empty).Trim().Trim('"');
            if (raw.Length == 0 || raw.Length > MaximumArgumentLength ||
                !raw.StartsWith(ArgumentPrefix, StringComparison.Ordinal))
                return false;

            string[] parts = raw[ArgumentPrefix.Length..].Split('.');
            if (parts.Length != 6 || parts.Any(string.IsNullOrWhiteSpace) ||
                !int.TryParse(parts[0], out int version) || version != CurrentProtocolVersion ||
                !IsLowerHexSha256(parts[2]) ||
                !Guid.TryParseExact(parts[3], "N", out Guid migrationId) ||
                !TryDecodeBase64Url(parts[1], out string archivePath) ||
                !TryDecodeBase64Url(parts[4], out string pipeName) ||
                !TryDecodeToken(parts[5]))
                return false;

            try { archivePath = Path.GetFullPath(archivePath); }
            catch { return false; }

            if (archivePath.Length == 0 || archivePath.Length > MaximumArchivePathLength ||
                !string.Equals(Path.GetExtension(archivePath), ".zip", StringComparison.OrdinalIgnoreCase) ||
                !IsValidPipeName(pipeName))
                return false;

            request = new GoodbyeDpiMigrationActivationRequest
            {
                ProtocolVersion = version,
                ArchivePath = archivePath,
                ArchiveSha256 = parts[2],
                MigrationId = migrationId,
                ResponsePipeName = pipeName,
                SessionToken = parts[5],
                RawArgument = raw
            };
            return true;
        }

        public static string CreateArgument(
            string archivePath,
            string archiveSha256,
            Guid migrationId,
            string responsePipeName,
            string sessionToken)
        {
            string value = string.Join(
                ".",
                CurrentProtocolVersion.ToString(),
                EncodeBase64Url(Path.GetFullPath(archivePath)),
                archiveSha256?.ToLowerInvariant(),
                migrationId.ToString("N"),
                EncodeBase64Url(responsePipeName),
                sessionToken);

            if (!TryParseArgument(ArgumentPrefix + value, out _))
                throw new ArgumentException("The migration activation data is invalid.");
            return ArgumentPrefix + value;
        }

        private static bool IsLowerHexSha256(string value) =>
            value.Length == 64 && value.All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f');

        private static bool IsValidPipeName(string value)
        {
            if (value.Length <= ResponsePipePrefix.Length || value.Length > MaximumPipeNameLength ||
                !value.StartsWith(ResponsePipePrefix, StringComparison.Ordinal))
                return false;

            return value.All(character =>
                character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_');
        }

        private static bool TryDecodeToken(string value)
        {
            try
            {
                byte[] token = DecodeBase64Url(value);
                return token.Length is >= 32 and <= 64 && EncodeBase64Url(token) == value;
            }
            catch { return false; }
        }

        private static bool TryDecodeBase64Url(string value, out string decoded)
        {
            decoded = string.Empty;
            try
            {
                byte[] bytes = DecodeBase64Url(value);
                decoded = new UTF8Encoding(false, true).GetString(bytes);
                return EncodeBase64Url(bytes) == value;
            }
            catch { return false; }
        }

        private static byte[] DecodeBase64Url(string value)
        {
            string padded = value.Replace('-', '+').Replace('_', '/');
            padded += (padded.Length % 4) switch
            {
                0 => string.Empty,
                2 => "==",
                3 => "=",
                _ => throw new FormatException("Invalid base64url length.")
            };
            return Convert.FromBase64String(padded);
        }

        private static string EncodeBase64Url(string value) =>
            EncodeBase64Url(Encoding.UTF8.GetBytes(value));

        private static string EncodeBase64Url(byte[] value) =>
            Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
