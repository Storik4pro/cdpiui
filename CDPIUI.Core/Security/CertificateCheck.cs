using CDPIUI.Shared.Exceptions.Catalog;
using CDPIUI.Shared.Secrets;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace CDPIUI.Core.Security
{
    public enum CatalogCheckResult
    {
        Success,
        FailureNoSignature,
        FailureNotTrustedSignature,
        FailureNotValid,
        FailureUnknown
    }

    public class CertificateCheck
    {
        public static async Task<CatalogCheckResult> CheckCatalog(string catalogFile, string compareDirectory)
        {
            CatalogCheckResult catalogCheckResult;
            bool result = false;
            try
            {
                result = await OpenAndCompareCatalogFile(catalogFile, compareDirectory);
                if (result) catalogCheckResult = CatalogCheckResult.Success;
                else catalogCheckResult = CatalogCheckResult.FailureNotValid;
            }
            catch (Exception ex)
            {
                if (ex is CertificateNotTrusted) catalogCheckResult = CatalogCheckResult.FailureNotTrustedSignature;
                else if (ex is CryptographicException) catalogCheckResult = CatalogCheckResult.FailureNoSignature;
                else catalogCheckResult = CatalogCheckResult.FailureUnknown;
            }

            await Task.CompletedTask;
            return catalogCheckResult;
        }

        private static async Task<bool> OpenAndCompareCatalogFile(string catalogFile, string compareDirectory)
        {
            X509Certificate catalogFileSign = X509Certificate.CreateFromSignedFile(catalogFile);
            if (!Secret.TrustedCertificates.Contains(catalogFileSign.GetCertHashString()))
            {
                throw new CertificateNotTrusted("Certificate not trusted");
            }
            
            ProcessStartInfo startInfo = new ProcessStartInfo();

            startInfo.FileName = @"powershell.exe";
            startInfo.Arguments = 
                $"Test-FileCatalog " +
                $"-CatalogFilePath \'{catalogFile}\' " +
                $"-Path \'{compareDirectory}\' " +
                $"-FilesToSkip catalog.cat";
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;

            Process process = new Process();
            
            process.StartInfo = startInfo;
            process.Start();
            await process.WaitForExitAsync();

            string output = process.StandardOutput.ReadToEnd().ReplaceLineEndings("");
            if (string.Equals("Valid", output, StringComparison.OrdinalIgnoreCase))
                return true;
            else 
                return false;
        }
    }
}
