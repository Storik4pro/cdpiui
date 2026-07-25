using CDPIUI.Core.Basic;
using CDPIUI.Core.Store.Network.Models;
using CDPIUI.Core.Store.Repository;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.Shared.Extentions;
using CDPIUI.Shared.Models;
using CDPIUI.Shared.PrettyErrorConvertionService;
using CDPIUI.Shared.Secrets;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
namespace CDPIUI.Core.Store.Network
{
    public interface IAPIWorker
    {
        /// <summary>
        /// Get latest version and release notes for URL and current 
        /// version control type
        /// </summary>
        /// <param name="url">Url to check</param>
        /// <returns>Latest release info</returns>
        Task<OperationResultModel<ReleaseInfoModel>> GetLastVersionAndVersionNotes(string url);
    }

    internal class APIWorker : IAPIWorker
    {
        public string? Token;
        public SupportedVersionControls VersionControl;

        public async Task<OperationResultModel<APILinkModel>> GetDownloadLinkForVersion(
            string? repoUrl,
            string? targetFileOrFileType,
            string? version = null, 
            string? prefferedFile = null
            )
        {
            APILinkModel linkModel = new();

            try
            {
                if (string.IsNullOrEmpty(Token)) throw new ArgumentNullException(nameof(Token));
                if (string.IsNullOrEmpty(repoUrl)) throw new ArgumentNullException(nameof(repoUrl));
                if (string.IsNullOrEmpty(targetFileOrFileType)) throw new ArgumentNullException(nameof(targetFileOrFileType));


                HttpResponseMessage response = await GetGithubResponse(repoUrl, version);

                using var stream = await response.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);
                var root = doc.RootElement;

                linkModel.version = root.GetProperty("tag_name").GetString();

                var assets = GetAssetsForVersionControl(root, VersionControl);
                if (assets == null)
                {
                    return OperationResultModel<APILinkModel>
                        .FailureResult(ErrorModel.OnlyErrorCode(PrettyErrorCode.ERROR_HTTP_INVALID_SERVER_RESPONSE));
                }

                var matches = ((JsonElement.ArrayEnumerator)assets)
                    .Select(a => new
                    {
                        Name = TryGetValue(a, "name"),
                        Url = GetDownloadUrlForVersionControl(a, VersionControl)
                    })
                    .Where(a =>
                        !string.IsNullOrEmpty(a.Name)
                    )
                    .Where(a =>
                        string.Equals(a.Name, targetFileOrFileType, StringComparison.OrdinalIgnoreCase)
                        || a.Name.EndsWith(
                            FileSystemService.GetFileExtention(targetFileOrFileType.ToEnum<FileExtentionTypes>()), 
                            StringComparison.OrdinalIgnoreCase)
                    )
                    .Where(a =>
                        prefferedFile is null ||
                        a.Name.Contains(prefferedFile, StringComparison.Ordinal)
                    )
                    .ToList();

                if (matches.Count == 0)
                {
                    return OperationResultModel<APILinkModel>
                        .FailureResult(ErrorModel.OnlyErrorCode(PrettyErrorCode.INVALID_URI));
                }

                // FIX: Possible issue on update process.
                if (matches.Count > 1)
                {
                    return OperationResultModel<APILinkModel>
                        .FailureResult(ErrorModel.OnlyErrorCode(PrettyErrorCode.TOO_MANY_VARIANTS));
                }

                linkModel.link = matches[0].Url;

                return OperationResultModel<APILinkModel>.SuccessResult(linkModel);
            }
            catch (Exception ex)
            {
                return OperationResultModel<APILinkModel>
                        .FailureResult(ErrorsHelper.Convertor.GetErrorModel(nameof(APIWorker), ex));
            }
        }

        public async Task<OperationResultModel<List<DownloadLinkModel>>> GetDownloadLinksAsync(List<FileToDownload>? filesToDownload)
        {
            List<DownloadLinkModel> links = [];

            if (filesToDownload == null)
                return OperationResultModel<List<DownloadLinkModel>>
                    .FailureResult(ErrorModel.OnlyErrorCode(PrettyErrorCode.NULL_REFERENCE));

            foreach (var file in filesToDownload)
            {
                string? downloadUrl, downloadVersion;
                if (file.version_control == "external_site_only_last")
                {
                    downloadUrl = file.download_link;
                    downloadVersion = null;
                }
                else
                {
                    if (file.version_control_link is null || file.type is null)
                        return OperationResultModel<List<DownloadLinkModel>>
                            .FailureResult(ErrorModel.OnlyErrorCode(PrettyErrorCode.NULL_REFERENCE));

                    var result = 
                        await GetDownloadLinkForVersion(
                            file.version_control_link, 
                            file.type, 
                            file.preffered_version, 
                            file.preffered_to_download_file_name
                            );

                    if (result.ErrorHappens)
                    {
                        return OperationResultModel<List<DownloadLinkModel>>
                            .FailureResult(result.Error!);
                    }
                    else
                    {
                        downloadUrl = result.Result.link;
                        downloadVersion = result.Result.version;
                    }
                }

                links.Add(new()
                {
                    link = downloadUrl,
                    version = downloadVersion,
                    type = file.type,
                    archive_root_folder = file.archive_root_folder,
                    actions = file.actions,
                    target_executable_file = null
                });
            }

            return OperationResultModel<List<DownloadLinkModel>>
                .SuccessResult(links);
        }

        public async Task<OperationResultModel<ReleaseInfoModel>> GetLastVersionAndVersionNotes(string repoUrl)
        {
            string notes;
            string tag;
            try
            {
                HttpResponseMessage response = await GetGithubResponse(repoUrl, null);

                using var stream = await response.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);
                var root = doc.RootElement;

                tag = root.GetProperty("tag_name").GetString();
                notes = GetReleaseNotesForVersionControl(root, VersionControl);

                return OperationResultModel<ReleaseInfoModel>
                    .SuccessResult(ReleaseInfoModel.BasicReleaseInfo(tag, notes)); 
            }
            catch (Exception ex)
            {
                return OperationResultModel<ReleaseInfoModel>
                    .FailureResult(ErrorsHelper.Convertor.GetErrorModel(nameof(GetLastVersionAndVersionNotes), ex));
            }
        }


        private async Task<HttpResponseMessage> GetGithubResponse(
            string repoUrl, 
            string? version)
        {
            if (string.IsNullOrEmpty(Token)) throw new ArgumentNullException(nameof(Token));
            var uri = new Uri(repoUrl);
            var parts = uri.AbsolutePath.Trim('/').Split('/');
            if (parts.Length < 2)
                throw new ArgumentException("Invalid GitHub repository URL.", nameof(repoUrl));
            var owner = parts[0];
            var repo = parts[1];


            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("CDPIUI_Components_Store", ApplicationInfo.Version));
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("token", Token);

            string apiUrl = GetApiUrlForVersion(owner, repo, version, VersionControl);


            var response = await client.GetAsync(apiUrl);
            Logger.Instance.CreateDebugLog(nameof(APIWorker), version ?? "Version not provided");
            Logger.Instance.CreateDebugLog(nameof(APIWorker), apiUrl);

            response.EnsureSuccessStatusCode();
            return response;
        }

        private static string GetApiUrlForVersion(string owner, string repo, string? version, SupportedVersionControls versionControl)
        {
            string url = string.Empty;
            if (versionControl == SupportedVersionControls.GitHub)
            {
                url = string.IsNullOrEmpty(version)
                    ? $"https://api.github.com/repos/{owner}/{repo}/releases/latest"
                    : $"https://api.github.com/repos/{owner}/{repo}/releases/tags/{version}";
            }
            else if (versionControl == SupportedVersionControls.GitLab)
            {
                url = string.IsNullOrEmpty(version)
                    ? $"https://gitlab.com/api/v4/projects/{owner}%2F{repo}/releases/permalink/latest"
                    : $"https://gitlab.com/api/v4/projects/{owner}%2F{repo}/releases/{version}";
            }
            return url;
        }

        

        private static JsonElement.ArrayEnumerator? GetAssetsForVersionControl(JsonElement root, SupportedVersionControls versionControl)
        {
            return versionControl switch
            {
                SupportedVersionControls.GitHub => (JsonElement.ArrayEnumerator?)root.GetProperty("assets").EnumerateArray(),
                SupportedVersionControls.GitLab => (JsonElement.ArrayEnumerator?)root.GetProperty("assets").GetProperty("links").EnumerateArray(),
                SupportedVersionControls.None => null,
                _ => null,
            };
        }

        private static string GetDownloadUrlForVersionControl(JsonElement root, SupportedVersionControls versionControl)
        {
            return versionControl switch
            {
                SupportedVersionControls.GitHub => root.GetProperty("browser_download_url").GetString() ?? string.Empty,
                SupportedVersionControls.GitLab => root.GetProperty("url").GetString() ?? string.Empty,
                SupportedVersionControls.None => string.Empty,
                _ => string.Empty,
            };
        }

        private static string GetReleaseNotesForVersionControl(JsonElement root, SupportedVersionControls versionControl)
        {
            return versionControl switch
            {
                SupportedVersionControls.GitHub => root.GetProperty("body").GetString() ?? string.Empty,
                SupportedVersionControls.GitLab => root.GetProperty("description").GetString() ?? string.Empty,
                SupportedVersionControls.None => string.Empty,
                _ => string.Empty,
            };
        }

        private static string TryGetValue(JsonElement jsonElement, string key)
        {
            if (jsonElement.TryGetProperty(key, out var value))
            {
                return value.GetString() ?? string.Empty;
            }
            return string.Empty;
        }
    }
}
