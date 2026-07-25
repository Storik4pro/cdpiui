using CDPIUI.Core.Basic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CDPIUI.Core.Store
{
    public static partial class VersionHelper
    {
        /// <summary>
        /// Compare two version strings
        /// </summary>
        /// <param name="oldVersion">Version 1</param>
        /// <param name="newVersion">Version 2</param>
        /// <returns>
        /// A value that indicates the relative order of the objects being compared. The
        /// return value has these meanings:
        /// <br></br>
        /// Value – Meaning<br></br>
        /// Less than zero – This instance precedes other in the sort order.<br></br>
        /// Zero – This instance occurs in the same position in the sort order as other.<br></br>
        /// Greater than zero – This instance follows other in the sort order.<br></br>
        ///</returns>
        public static int CompareVersionStrings(string? oldVersion, string? newVersion)
        {
            if (string.IsNullOrEmpty(oldVersion)  || string.IsNullOrEmpty(newVersion)) return 0;
            if (oldVersion == "%CURRENT%") return 0;

            if (oldVersion.StartsWith('v')) oldVersion = oldVersion[1..];
            if (newVersion.StartsWith('v')) newVersion = newVersion[1..];

            if (oldVersion.Contains("rc")) oldVersion = oldVersion.Replace("rc", "-rc");
            if (newVersion.Contains("rc")) newVersion = newVersion.Replace("rc", "-rc");

            if (Semver.SemVersion.TryParse(oldVersion, out var oldSemVersion) && Semver.SemVersion.TryParse(newVersion, out var newSemVersion))
            {
                return Semver.SemVersion.ComparePrecedence(oldSemVersion, newSemVersion);
            }
            else if (Version.TryParse(oldVersion, out var oldVerVersion) && Version.TryParse(newVersion, out var newVerVersion))
            {
                if (oldVerVersion < newVerVersion) return -1;
                else if (oldVerVersion > newVerVersion) return 1;
                else return 0;
            }

            if (DateTime.TryParseExact(oldVersion, "ddMMyyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime oldData) &&
                DateTime.TryParseExact(newVersion, "ddMMyyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime newData))
            {
                return DateTime.Compare(oldData, newData);
            }

            Logger.Instance.CreateErrorLog(nameof(VersionHelper), $"Cannot compare {oldVersion} and {newVersion}.");
            return 0;
        }

        /// <summary>
        /// Is version string are correct
        /// </summary>
        /// <param name="version">Version string</param>
        /// <returns>true if correct, otherwise false</returns>
        public static bool IsVersionCorrect(string version)
        {
            return Version.TryParse(version, out var _);
        }

        /// <summary>
        /// Is id correct StoreId
        /// </summary>
        /// <param name="id">Id</param>
        /// <returns>true if correct, otherwise false</returns>
        public static bool IsIdCorrect(string id)
        {
            return CheckIdRegex().IsMatch(id);
        }

        [GeneratedRegex(@"^[a-zA-Z0-9\-]+$")]
        private static partial Regex CheckIdRegex();
    }
}
