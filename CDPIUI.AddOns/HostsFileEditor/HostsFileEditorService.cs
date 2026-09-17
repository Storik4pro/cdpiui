using CDPIUI.Shared.Exceptions;
using System.Diagnostics;

namespace CDPIUI.AddOns.HostsFileEditor
{
    public class HostsFileEditService
    {
        private enum Flags
        {
            add,
            remove,
            recover,
        }
        private static Process RunEditorWithFlag(Flags flag)
        {
            string startupString = $"/{flag}";
            string path = Path.Combine(Core.Data.Directories.CurrentDirectory,
                "EditHostFile.exe");

            if (!Path.Exists(path)) throw new ApplicationFilesDamagedException("File not found");

            var psi = new ProcessStartInfo(path, startupString)
            {
                UseShellExecute = true,
                Verb = "runas"
            };
            var process = Process.Start(psi);
            return process;
        }

        public static async Task<int> AddDomains()
        {
            Process process = RunEditorWithFlag(Flags.add);
            await process.WaitForExitAsync();

            return process.ExitCode;
        }

        public static async Task<int> RemoveDomains()
        {
            Process process = RunEditorWithFlag(Flags.remove);
            await process.WaitForExitAsync();

            return process.ExitCode;
        }

        public static async Task<int> RestoreDomains()
        {
            Process process = RunEditorWithFlag(Flags.recover);
            await process.WaitForExitAsync();

            return process.ExitCode;
        }
    }
}
