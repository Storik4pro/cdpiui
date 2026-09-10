using CDPIUI.Shared.Secrets;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;

namespace CDPIUI.Core.Store.Network
{
    /// <summary>
    /// Store-only requests worker
    /// </summary>
    internal sealed class RequestWorker : IDisposable
    {
        private readonly Lazy<HttpClient> directClient;
        private readonly Lazy<HttpClient> proxyClient;
        private readonly bool ownsClient;
        private int disposed;

        public RequestWorker(HttpClient? client = null)
        {
            directClient = new(() => client ?? new HttpClient(CreateHandler(proxyEnable: false)));
            proxyClient = new(() => client ?? new HttpClient(CreateHandler(proxyEnable: true)));
            ownsClient = client == null;
        }

        private static HttpClientHandler CreateHandler(bool proxyEnable)
        {
            return new HttpClientHandler { AllowAutoRedirect = !proxyEnable };
        }


        public async Task<HttpResponseMessage> GetAsync(
            string url,
            string? token = null,
            string authenticationScheme = "Bearer",
            bool useStoreUserAgent = false,
            RangeHeaderValue? range = null,
            HttpCompletionOption completionOption = HttpCompletionOption.ResponseHeadersRead,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
            bool proxyEnable = StoreProxyHelper.UseProxyForStoreConnections;
            using var request = CreateRequest(url, token, authenticationScheme, useStoreUserAgent, proxyEnable);
            request.Headers.Range = range;
            var client = proxyEnable ? proxyClient.Value : directClient.Value;
            return await client.SendAsync(request, completionOption, cancellationToken);
        }

        public async Task<string> GetStringAsync(
            string url,
            string? token = null,
            string authenticationScheme = "Bearer",
            bool useStoreUserAgent = false,
            CancellationToken cancellationToken = default)
        {
            using var response = await GetAsync(url, token, authenticationScheme, useStoreUserAgent,
                completionOption: HttpCompletionOption.ResponseContentRead, cancellationToken: cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }

        public async Task DownloadFileAsync(
            string url,
            string destinationPath,
            Action<long, long, TimeSpan>? progress = null,
            string? token = null,
            string authenticationScheme = "Bearer",
            bool useStoreUserAgent = false,
            HttpCompletionOption completionOption = HttpCompletionOption.ResponseHeadersRead,
            CancellationToken cancellationToken = default)
        {
            using var response = await GetAsync(url, token, authenticationScheme, useStoreUserAgent,
                completionOption: completionOption, cancellationToken: cancellationToken);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? -1L;
            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            var buffer = new byte[81920];
            long totalRead = 0;
            var stopwatch = Stopwatch.StartNew();
            var lastUpdate = stopwatch.Elapsed;
            int read;

            while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
                totalRead += read;

                var now = stopwatch.Elapsed;
                if ((now - lastUpdate).TotalSeconds >= 1 || totalRead == totalBytes)
                {
                    progress?.Invoke(totalRead, totalBytes, now);
                    lastUpdate = now;
                }
            }
        }

        private static HttpRequestMessage CreateRequest(
            string url, string? token, string authenticationScheme, bool useStoreUserAgent, bool proxyEnable)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var sourceUri) ||
                (sourceUri.Scheme != Uri.UriSchemeHttp && sourceUri.Scheme != Uri.UriSchemeHttps))
                throw new ArgumentException("The source URL must be an absolute HTTP or HTTPS URL.", nameof(url));

            var request = new HttpRequestMessage(HttpMethod.Get, proxyEnable ? CreateProxyUri(url) : sourceUri);
            try
            {
                if (proxyEnable)
                    request.Headers.Add("X-Key", Secret.ProxyKey);
                if (useStoreUserAgent)
                    request.Headers.UserAgent.Add(new ProductInfoHeaderValue("CDPIUI_Components_Store", ApplicationInfo.Version));
                if (token != null)
                    request.Headers.Authorization = new AuthenticationHeaderValue(authenticationScheme, token);

                Debug.WriteLine(request.ToString());

                return request;
            }
            catch
            {
                request.Dispose();
                throw;
            }
        }

        private static Uri CreateProxyUri(string url)
        {
            if (string.IsNullOrWhiteSpace(Secret.ProxyKey))
                throw new InvalidOperationException("Secret.ProxyKey is not configured.");
            if (!Uri.TryCreate(Secret.ProxyURL, UriKind.Absolute, out var proxyUri) ||
                proxyUri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(proxyUri.UserInfo) ||
                !string.IsNullOrEmpty(proxyUri.Fragment))
                throw new InvalidOperationException("Secret.ProxyURL must be an absolute HTTPS fetch endpoint or server URL.");

            string encodedUrl = Convert.ToBase64String(Encoding.UTF8.GetBytes(url))
                .TrimEnd('=');
            var builder = new UriBuilder(proxyUri);
            if (builder.Path == "/")
                builder.Path = "/v1/fetch";
            string query = builder.Query.TrimStart('?');
            builder.Query = string.IsNullOrEmpty(query) ? $"u={encodedUrl}" : $"{query}&u={encodedUrl}";
            Debug.WriteLine(builder.Uri);
            return builder.Uri;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0 && ownsClient)
            {
                if (directClient.IsValueCreated)
                    directClient.Value.Dispose();
                if (proxyClient.IsValueCreated)
                    proxyClient.Value.Dispose();
            }
        }
    }
}
