using CDPIUI.AddOns.BlockCheck2.Models;
using System.Security.Cryptography;
using System.Text;

namespace CDPIUI.AddOns.BlockCheck2.Presentation;

public enum BlockCheckSiteListProcessingMode
{
    WholeList,
    EachSite,
}

public sealed record BlockCheckSiteListInput(
    string DisplayName,
    string FilePath,
    BlockCheckSiteListProcessingMode ProcessingMode = BlockCheckSiteListProcessingMode.WholeList,
    string? IndividualSiteListDirectory = null);

public sealed class BlockCheckSiteListLoadResult
{
    public IReadOnlyList<BlockCheckTarget> Targets { get; init; } = [];
    public IReadOnlyList<BlockCheckIssue> Issues { get; init; } = [];
}

public sealed class BlockCheckSiteListLoader
{
    private readonly BlockCheckTargetInputParser _targetParser;

    public BlockCheckSiteListLoader(BlockCheckTargetInputParser? targetParser = null)
    {
        _targetParser = targetParser ?? new BlockCheckTargetInputParser();
    }

    public BlockCheckSiteListLoadResult Load(
        IEnumerable<BlockCheckSiteListInput> siteLists,
        BlockCheckTargetInputOptions options)
    {
        ArgumentNullException.ThrowIfNull(siteLists);
        ArgumentNullException.ThrowIfNull(options);

        List<BlockCheckTarget> targets = [];
        List<BlockCheckIssue> issues = [];
        foreach (BlockCheckSiteListInput siteList in siteLists
                     .DistinctBy(item => item.FilePath, StringComparer.OrdinalIgnoreCase))
        {
            string subject = string.IsNullOrWhiteSpace(siteList.DisplayName)
                ? siteList.FilePath
                : siteList.DisplayName;
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(siteList.FilePath);
                if (!File.Exists(fullPath))
                {
                    issues.Add(Error(
                        "SITE_LIST_NOT_FOUND",
                        $"Site list file was not found: {siteList.FilePath}",
                        subject));
                    continue;
                }
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
            {
                issues.Add(Error(
                    "SITE_LIST_PATH_INVALID",
                    $"Site list path is invalid: {exception.Message}",
                    subject));
                continue;
            }

            string[] domains;
            try
            {
                domains = File.ReadLines(fullPath)
                    .Select(NormalizeListLine)
                    .Where(line => line.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                issues.Add(Error(
                    "SITE_LIST_READ_FAILED",
                    $"Site list could not be read: {exception.Message}",
                    subject));
                continue;
            }

            if (domains.Length == 0)
            {
                issues.Add(Error(
                    "SITE_LIST_EMPTY",
                    "Site list contains no domains.",
                    subject));
                continue;
            }

            if (siteList.ProcessingMode == BlockCheckSiteListProcessingMode.EachSite)
            {
                LoadIndividualSites(siteList, subject, domains, options, targets, issues);
            }
            else
            {
                BlockCheckTargetInputResult parsed = _targetParser.Parse(
                    string.Join(Environment.NewLine, domains),
                    options);
                issues.AddRange(parsed.Issues.Select(issue => issue with
                {
                    SubjectId = $"{subject}:{issue.SubjectId ?? "list"}",
                }));
                targets.AddRange(parsed.Targets.Select(target => CloneWithHostList(target, fullPath)));
            }
        }

        return new BlockCheckSiteListLoadResult
        {
            Targets = targets,
            Issues = issues,
        };
    }

    private void LoadIndividualSites(
        BlockCheckSiteListInput siteList,
        string subject,
        IReadOnlyList<string> domains,
        BlockCheckTargetInputOptions options,
        ICollection<BlockCheckTarget> targets,
        ICollection<BlockCheckIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(siteList.IndividualSiteListDirectory))
        {
            issues.Add(Error(
                "SITE_LIST_INDIVIDUAL_DIRECTORY_MISSING",
                "A storage directory is required when every site is processed separately.",
                subject));
            return;
        }

        string outputDirectory;
        try
        {
            outputDirectory = Path.GetFullPath(siteList.IndividualSiteListDirectory);
            Directory.CreateDirectory(outputDirectory);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            issues.Add(Error(
                "SITE_LIST_INDIVIDUAL_DIRECTORY_FAILED",
                $"Could not prepare individual site lists: {exception.Message}",
                subject));
            return;
        }

        HashSet<string> currentFiles = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < domains.Count; index++)
        {
            string domain = domains[index];
            string fileName = $"site_{index + 1:D4}_{ShortHash(domain)}.txt";
            string filePath = Path.Combine(outputDirectory, fileName);
            try
            {
                File.WriteAllText(filePath, domain + Environment.NewLine, new UTF8Encoding(false));
                currentFiles.Add(filePath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                issues.Add(Error(
                    "SITE_LIST_INDIVIDUAL_WRITE_FAILED",
                    $"Could not create a list for an individual site: {exception.Message}",
                    $"{subject}:{index + 1}"));
                continue;
            }

            BlockCheckTargetInputResult parsed = _targetParser.Parse(domain, options);
            foreach (BlockCheckIssue issue in parsed.Issues)
            {
                issues.Add(issue with
                {
                    SubjectId = $"{subject}:{issue.SubjectId ?? (index + 1).ToString()}"
                });
            }

            foreach (BlockCheckTarget target in parsed.Targets)
                targets.Add(CloneWithHostList(target, filePath));
        }

        RemoveStaleIndividualLists(outputDirectory, currentFiles);
    }

    private static void RemoveStaleIndividualLists(
        string outputDirectory,
        IReadOnlySet<string> currentFiles)
    {
        try
        {
            foreach (string filePath in Directory.EnumerateFiles(
                         outputDirectory,
                         "site_*.txt",
                         SearchOption.TopDirectoryOnly))
            {
                if (!currentFiles.Contains(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Stale generated inputs do not affect the current selection.
        }
    }

    private static string ShortHash(string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToHexString(hash.AsSpan(0, 4)).ToLowerInvariant();
    }

    private static string NormalizeListLine(string? value)
    {
        string line = (value ?? string.Empty).Trim().TrimStart('\uFEFF');
        if (line.Length == 0 || line.StartsWith('#'))
        {
            return string.Empty;
        }

        return line.StartsWith('^') ? line[1..] : line;
    }

    private static BlockCheckTarget CloneWithHostList(BlockCheckTarget target, string fullPath) => new()
    {
        Id = target.Id,
        Host = target.Host,
        Path = target.Path,
        Protocol = target.Protocol,
        IpVersion = target.IpVersion,
        CustomPort = target.CustomPort,
        HostListPaths = [fullPath],
    };

    private static BlockCheckIssue Error(string code, string message, string? subjectId = null) =>
        new(BlockCheckIssueSeverity.Error, code, message, subjectId);
}
