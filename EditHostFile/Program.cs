using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EditHostFile
{
    internal class Program
    {
        private static void Log(string message)
        {
            Console.WriteLine($"[CDPI UI/EditHostFileHelper] {message}");
        }

        static void Main(string[] args)
        {
            Log("Getting ready...");
            string pathpart = "hosts";
            if (System.Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                //is windows NT
                pathpart = "system32\\drivers\\etc\\hosts";
            }
            string hostfile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), pathpart);

            if (args.Contains("/remove"))
            {
                RemoveAddedLines(hostfile);
            }
            else if (args.Contains("/add"))
            {
                AddNewLines(hostfile);
            }
            else if (args.Contains("/recover"))
            {
                RestoreBackup(hostfile);
            }
            Log("Work finished. Goodbye~");
        }

        private static void CreateBackup(string path)
        {
            Log($"Creating backup for \"{path}\" in \"{path}.bak\"");
            File.Copy(path, $"{path}.bak", true);
        }

        private static void RestoreBackup(string path)
        {
            Log($"Restoring backup for \"{path}\" in \"{path}.bak\"");
            File.Copy($"{path}.bak", path, true);
            File.Delete($"{path}.bak");
        }

        private static void RemoveAddedLines(string path)
        {
            Log($"Removing added lines in \"{path}\"");
            string textContent = File.ReadAllText(path);
            textContent = Regex.Replace(textContent, @"(# \[CDOM-B\](?:.*?)# \[CDOM-E\](?:.*?))(?:\n|$)", "", RegexOptions.Singleline);

            File.WriteAllText(path, textContent);
        }

        private static void AddNewLines(string path)
        {
            Log($"Adding new lines in \"{path}\"");
            Assembly assembly = Assembly.GetExecutingAssembly();
            var stream = assembly.GetManifestResourceStream("EditHostFile.Data.hosts");
            StreamReader reader = new StreamReader(stream);
            string text = reader.ReadToEnd();

            RemoveAddedLines(path);
            CreateBackup(path);

            string[] lines = File.ReadAllLines(path);
            foreach (string line in text.Split('\n'))
            {
                if (!lines.Contains(line))
                {
                    File.AppendAllLines(path, new String[] { line });
                }
            }

            Log($"Adding new lines in \"{path}\". Status: COMPLETE");

        }
    }
}
