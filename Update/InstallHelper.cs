using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Update
{
    public enum InstallState
    {
        None,
        Prepare,
        Unpack,
        Copy,
        Finalize,
        Completed,
        Error
    }

    public enum InstallError
    {
        None,
        PermissionDenied,
        FileNotFound,
        ExtractingFile,
        UnexpectedError,
        LaunchingApp,
        NoOldVersion,
        OK
    }

    public class InstallHelper
    {
        private static InstallHelper _instance;
        private static readonly object _lock = new object();

        public static InstallHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new InstallHelper();
                    return _instance;
                }
            }
        }

        private string _archiveFilePath;
        private string _targetFolder;
        private string _targetExecutable;
        private readonly List<string> _skipFiles = new List<string> { "update.exe" };
        private const string UiRootPrefix = ""; 

        public Action<InstallState> InstallationStateChanged;
        public Action<double> ProgressChanged;
        public Action<string> CurrentFileChanged;
        public Action<InstallError, string> ErrorOccurred;

        public InstallError FinishStatus { get; private set; } = InstallError.None;

        public InstallHelper()
        {
        }

        public void Init(string archiveFilePath, string targetFolder, string targetExecutable)
        {
            _archiveFilePath = archiveFilePath;
            _targetFolder = targetFolder;
            _targetExecutable = targetExecutable;
            FinishStatus = InstallError.None;
        }

        public void StartInstall()
        {
            Task.Run(async () =>
            {
                try
                {
                    InstallationStateChanged?.Invoke(InstallState.Prepare);
                    Log("Starting extraction process");
                    CloseProcessIfRunning("goodbyeDPI");
                    CloseProcessIfRunning("CDPIUI");
                    CloseProcessIfRunning("CDPIUI_TrayIcon");

                    InstallationStateChanged?.Invoke(InstallState.Unpack);
                    bool extractedOk = await ExtractZipAsync();

                    if (FinishStatus == InstallError.None && extractedOk)
                    {
                        InstallationStateChanged?.Invoke(InstallState.Finalize);
                        FinishStatus = InstallError.OK;
                        bool launched = LaunchApplication(false);
                        if (launched)
                        {
                            InstallationStateChanged?.Invoke(InstallState.Completed);
                            ProgressChanged?.Invoke(1.0);
                            Log("Extraction completed successfully and application launched.");
                        }
                        else
                        {
                            InstallationStateChanged?.Invoke(InstallState.Error);
                        }
                        Application.Exit();
                    }
                    else
                    {
                        Log("An error occurred during extraction. Attempting downgrade.");
                        InstallationStateChanged?.Invoke(InstallState.Copy);
                        bool downgraded = DowngradeProgram();

                        if (downgraded)
                        {
                            Log("Successfully downgraded to previous version.");
                            FinishStatus = InstallError.OK;
                            bool launched = LaunchApplication(true);
                            if (launched)
                            {
                                InstallationStateChanged?.Invoke(InstallState.Completed);
                                ProgressChanged?.Invoke(1.0);
                                Application.Exit();
                            }
                            else
                            {
                                InstallationStateChanged?.Invoke(InstallState.Error);
                            }
                        }
                        else
                        {
                            InstallationStateChanged?.Invoke(InstallState.Error);
                        }

                        Application.Exit();
                    }
                }
                catch (Exception ex)
                {
                    Log($"Unexpected top-level error: {ex}");
                    FinishStatus = InstallError.UnexpectedError;
                    ErrorOccurred?.Invoke(FinishStatus, ex.Message);
                    InstallationStateChanged?.Invoke(InstallState.Error);
                }
            });
        }

        private void CloseProcessIfRunning(string processNameWithoutExt)
        {
            try
            {
                var procs = Process.GetProcessesByName(processNameWithoutExt);
                foreach (var p in procs)
                {
                    try
                    {
                        p.Kill();
                        p.WaitForExit(2000);
                        Log($"process {processNameWithoutExt}.exe terminated (pid {p.Id})");
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Log($"Error while trying to close process {processNameWithoutExt}: {ex}");
            }
        }

        private async Task<bool> ExtractZipAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_archiveFilePath) || !File.Exists(_archiveFilePath))
                {
                    FinishStatus = InstallError.FileNotFound;
                    // ErrorOccurred?.Invoke(FinishStatus, "Zip archive not found.");
                    return false;
                }

                if (!Directory.Exists(_targetFolder))
                    Directory.CreateDirectory(_targetFolder);

                using (var zip = ZipFile.OpenRead(_archiveFilePath))
                {
                    var relevantEntries = zip.Entries
                        .Where(e =>
                        {
                            var name = e.FullName.Replace('\\', '/');
                            return name.StartsWith(UiRootPrefix, StringComparison.OrdinalIgnoreCase);
                        })
                        .ToList();

                    var fileEntries = relevantEntries
                        .Where(e => !e.FullName.EndsWith("/"))
                        .ToList();

                    int totalFiles = fileEntries.Count;
                    int extractedFiles = 0;

                    if (totalFiles == 0)
                    {
                        Log($"No files found to extract in archive under '{UiRootPrefix}'.");

                        ProgressChanged?.Invoke(1.0);
                        return false;
                    }

                    foreach (var entry in relevantEntries)
                    {
                        var entryName = entry.FullName.Replace('\\', '/');
                        var relativePath = entryName.Substring(UiRootPrefix.Length);
                        if (string.IsNullOrEmpty(relativePath))
                            continue; 

                        string destPath = Path.Combine(_targetFolder, relativePath);
                        string destDir = Path.GetDirectoryName(destPath);

                        var fileName = Path.GetFileName(relativePath);
                        if (_skipFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase) && File.Exists(destPath))
                        {
                            Log($"Skipping {relativePath}");
                            continue;
                        }

                        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                            Directory.CreateDirectory(destDir);

                        if (entry.FullName.EndsWith("/"))
                        {
                            if (!Directory.Exists(destPath))
                                Directory.CreateDirectory(destPath);
                        }
                        else
                        {
                            try
                            {
                                CurrentFileChanged?.Invoke($"Extracting {relativePath}");
                                using (var stream = entry.Open())
                                using (var outFile = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
                                {
                                    await stream.CopyToAsync(outFile);
                                }
                            }
                            catch (UnauthorizedAccessException uaEx)
                            {
                                Log($"Permission denied when extracting {relativePath}: {uaEx.Message}");
                                FinishStatus = InstallError.PermissionDenied;
                                // ErrorOccurred?.Invoke(FinishStatus, $"Permission denied when extracting {relativePath}");
                                return false;
                            }
                            catch (IOException ioEx)
                            {
                                Log($"IO error when extracting {relativePath}: {ioEx.Message}");
                                FinishStatus = InstallError.ExtractingFile;
                                // ErrorOccurred?.Invoke(FinishStatus, $"IO error when extracting {relativePath}: {ioEx.Message}");
                                return false;
                            }
                            catch (Exception ex)
                            {
                                Log($"Error extracting {relativePath}: {ex}");
                                FinishStatus = InstallError.ExtractingFile;
                                // ErrorOccurred?.Invoke(FinishStatus, $"Error extracting {relativePath}: {ex.Message}");
                                return false;
                            }

                            extractedFiles++;
                            double progress = (double)extractedFiles / Math.Max(1, totalFiles);
                            ProgressChanged?.Invoke(progress);
                        }
                    } 

                    ProgressChanged?.Invoke(1.0);
                } 
                return true;
            }
            catch (Exception ex)
            {
                Log($"Extraction failed: {ex}");
                FinishStatus = InstallError.UnexpectedError;
                // ErrorOccurred?.Invoke(FinishStatus, ex.Message);
                return false;
            }
        }

        private bool DowngradeProgram()
        {
            try
            {
                string oldDirectory = Path.Combine(_targetFolder, ".old");
                if (!Directory.Exists(oldDirectory))
                {
                    FinishStatus = InstallError.NoOldVersion;
                    ErrorOccurred?.Invoke(FinishStatus, "No old version found to downgrade to.");
                    return false;
                }

                var allFiles = Directory.GetFiles(oldDirectory, "*", SearchOption.AllDirectories);
                int total = allFiles.Length;
                int idx = 0;

                foreach (var src in allFiles)
                {
                    var relative = GetRelativePath(oldDirectory, src);
                    var dest = Path.Combine(_targetFolder, relative);
                    var destDir = Path.GetDirectoryName(dest);
                    if (!Directory.Exists(destDir))
                        Directory.CreateDirectory(destDir);

                    try
                    {
                        File.Copy(src, dest, true);
                        idx++;
                        ProgressChanged?.Invoke((double)idx / Math.Max(1, total));
                        CurrentFileChanged?.Invoke($"Downgraded {relative}");
                    }
                    catch (UnauthorizedAccessException uaEx)
                    {
                        Log($"Permission denied while downgrading {relative}: {uaEx.Message}");
                        FinishStatus = InstallError.PermissionDenied;
                        ErrorOccurred?.Invoke(FinishStatus, $"Permission denied while downgrading {relative}");
                        return false;
                    }
                    catch (Exception ex)
                    {
                        Log($"Error while downgrading {relative}: {ex}");
                        FinishStatus = InstallError.UnexpectedError;
                        ErrorOccurred?.Invoke(FinishStatus, $"Error while downgrading {relative}: {ex.Message}");
                        return false;
                    }
                }

                ProgressChanged?.Invoke(1.0);
                return true;
            }
            catch (Exception ex)
            {
                Log($"Downgrade failed: {ex}");
                FinishStatus = InstallError.UnexpectedError;
                ErrorOccurred?.Invoke(FinishStatus, ex.Message);
                return false;
            }
        }

        private bool LaunchApplication(bool error = false)
        {
            try
            {
                string exePath = Path.Combine(_targetFolder, _targetExecutable);
                if (!File.Exists(exePath))
                {
                    FinishStatus = InstallError.FileNotFound;
                    ErrorOccurred?.Invoke(FinishStatus, $"{exePath} not found");
                    return false;
                }

                string param = error ? "--after-failed-update" : "--after-patching";

                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = param,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WorkingDirectory = _targetFolder
                };

                Process.Start(psi);
                FinishStatus = InstallError.OK;
                CurrentFileChanged?.Invoke($"Launching {_targetExecutable} {param}");
                Log($"Launching {_targetExecutable} with {param} parameter");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Failed to launch application: {ex}");
                FinishStatus = InstallError.LaunchingApp;
                ErrorOccurred?.Invoke(FinishStatus, ex.Message);
                return false;
            }
        }

        private void Log(string text)
        {
            try
            {
                string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {text}{Environment.NewLine}";
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update.log"), logLine);
            }
            catch
            {
                // pass
            }
        }
        private string GetRelativePath(string basePath, string fullPath)
        {
            if (string.IsNullOrEmpty(basePath)) throw new ArgumentNullException(nameof(basePath));
            if (string.IsNullOrEmpty(fullPath)) throw new ArgumentNullException(nameof(fullPath));

            Uri baseUri = new Uri(basePath.EndsWith(Path.DirectorySeparatorChar.ToString()) ? basePath : basePath + Path.DirectorySeparatorChar);
            Uri fullUri = new Uri(fullPath);

            if (baseUri.IsBaseOf(fullUri))
            {
                return Uri.UnescapeDataString(baseUri.MakeRelativeUri(fullUri).ToString());
            }
            else
            {
                throw new InvalidOperationException("The fullPath is not a subpath of basePath.");
            }
        }
    }
}