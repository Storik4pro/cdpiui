using CDPI_UI.Helper;
using CDPI_UI.Helper.Static;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace CDPI_UI.Common
{
    public class CertificateNotTrusted : System.Exception
    {
        public CertificateNotTrusted() : base() { }
        public CertificateNotTrusted(string message) : base(message) { }
        public CertificateNotTrusted(string message, System.Exception inner) : base(message, inner) { }

        protected CertificateNotTrusted(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context)
        { }
    }
    public class CatalogNoSignature : System.Exception
    {
        public CatalogNoSignature() : base() { }
        public CatalogNoSignature(string message) : base(message) { }
        public CatalogNoSignature(string message, System.Exception inner) : base(message, inner) { }

        protected CatalogNoSignature(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context)
        { }
    }
    public class CatalogInvalid : System.Exception
    {
        public CatalogInvalid() : base() { }
        public CatalogInvalid(string message) : base(message) { }
        public CatalogInvalid(string message, System.Exception inner) : base(message, inner) { }

        protected CatalogInvalid(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context)
        { }
    }

    public class CertificateCheck
    {
        public enum CatalogCheckResult
        {
            Success,
            FailureNoSignature,
            FailureNotTrustedSignature,
            FailureNotValid,
            FailureUnknown
        }

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
                $"-CatalogFilePath \"{catalogFile}\" " +
                $"-Path \"{compareDirectory}\" " +
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
