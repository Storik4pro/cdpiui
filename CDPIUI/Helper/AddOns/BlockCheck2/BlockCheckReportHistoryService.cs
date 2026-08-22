using CDPIUI.AddOns.BlockCheck2.Reporting;
using CDPIUI.Core.Basic;
using CDPIUI.Helper.BlockCheck2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CDPIUI.Helper.AddOns.BlockCheck2;

public sealed record BlockCheckReportHistoryEntry(
    string FilePath,
    BlockCheckReport Report,
    long FileSize,
    DateTimeOffset LastWriteTimeUtc);

public sealed class BlockCheckReportHistoryService
{
    public const int MaximumReportCount = 50;

    private readonly BlockCheckReportSerializer serializer;

    public BlockCheckReportHistoryService(BlockCheckReportSerializer serializer = null)
    {
        this.serializer = serializer ?? new BlockCheckReportSerializer();
    }

    public static string HistoryDirectory => BlockCheck2HistoryStoreItemService.StorageDirectory;

    public async Task<string> SaveAsync(
        BlockCheckReport report,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        BlockCheck2HistoryStoreItemService.EnsureRegistered();
        string status = report.Success
            ? "success"
            : report.IsBestEffort
                ? "best-effort"
                : "failed";
        string fileName = string.Concat(
            report.CreatedAtUtc.ToUniversalTime().ToString("yyyyMMdd'T'HHmmssfff'Z'"),
            "-",
            report.RunPreset.ToString().ToLowerInvariant(),
            "-",
            status,
            "-",
            Guid.NewGuid().ToString("N")[..8],
            ".json");
        string destinationPath = Path.Combine(HistoryDirectory, fileName);
        string temporaryPath = destinationPath + ".tmp";

        try
        {
            await serializer.SaveJsonAsync(temporaryPath, report, cancellationToken);
            File.Move(temporaryPath, destinationPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }

        TrimHistory();
        return destinationPath;
    }

    public async Task<IReadOnlyList<BlockCheckReportHistoryEntry>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(HistoryDirectory))
        {
            return [];
        }

        List<BlockCheckReportHistoryEntry> entries = [];
        foreach (string filePath in Directory.EnumerateFiles(
                     HistoryDirectory,
                     "*.json",
                     SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                string json = await File.ReadAllTextAsync(filePath, cancellationToken);
                BlockCheckReport report = serializer.DeserializeJson(json);
                FileInfo file = new(filePath);
                entries.Add(new BlockCheckReportHistoryEntry(
                    filePath,
                    report,
                    file.Exists ? file.Length : 0,
                    file.Exists
                        ? new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero)
                        : report.CreatedAtUtc));
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                Logger.Instance.CreateWarningLog(
                    nameof(BlockCheckReportHistoryService),
                    $"Cannot read BlockCheck2 report history file '{filePath}': {exception.Message}");
            }
        }

        return entries
            .OrderByDescending(entry => entry.Report.CreatedAtUtc)
            .ThenByDescending(entry => entry.LastWriteTimeUtc)
            .ToArray();
    }

    public void Delete(string filePath)
    {
        string fullPath = EnsureHistoryPath(filePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    private static string EnsureHistoryPath(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        string root = Path.GetFullPath(HistoryDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(filePath);
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The report is outside the BlockCheck2 history directory.");
        }

        return fullPath;
    }

    private static void TrimHistory()
    {
        try
        {
            FileInfo[] expired = new DirectoryInfo(HistoryDirectory)
                .EnumerateFiles("*.json", SearchOption.TopDirectoryOnly)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Skip(MaximumReportCount)
                .ToArray();
            foreach (FileInfo file in expired)
            {
                file.Delete();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Logger.Instance.CreateWarningLog(
                nameof(BlockCheckReportHistoryService),
                $"Cannot trim BlockCheck2 report history: {exception.Message}");
        }
    }
}
