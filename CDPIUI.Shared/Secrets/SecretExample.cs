using System.Collections.Generic;

namespace CDPIUI.Shared.Secrets
{
    public interface ISecret
    {
        /// <summary>
        /// Token for acces GitHub Store repository.
        /// If value is empty, Store is unavailable.
        /// </summary>
        static string? GitHubToken { get; }
        /// <summary>
        /// Token for acces GitLab Store repository.
        /// If value is empty, Store is unavailable.
        /// </summary>
        static string? GitLabToken { get; }

        /// <summary>
        /// Authentication GUID for pipe. 
        /// If value is empty, pipe may be insecure.
        /// </summary>
        static string? AuthGuid { get; set; }

        /// <summary>
        /// Trusted certifications. 
        /// If this value is empty:
        /// SIGNEDPACK files cannot be installed
        /// SIGNEDPATCH files cannot be installed (also application updates may not work)
        /// </summary>
        static List<string>? TrustedCertificates { get; }
    }

    /// <summary>
    /// Example of Secret.cs file.
    /// To build this application create Secret.cs file and copy example (with interface)
    /// to that file. Also, rename class in that file as "Secret.cs"
    /// </summary>
    internal class SecretExample : ISecret
    {
        public static string GitHubToken = "";
        public static string GitLabToken = "";

        public static string AuthGuid = "";

        public static readonly List<string> TrustedCertificates = [];
    }
}
