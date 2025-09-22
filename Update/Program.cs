using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace Update
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            string extractFileName = null;
            string destinationPath = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--directory-to-zip" && i + 1 < args.Length)
                {
                    extractFileName = args[++i]; 
                }
                else if (args[i] == "--destination-directory" && i + 1 < args.Length)
                {
                    destinationPath = args[++i];
                }
            }
            if (!string.IsNullOrEmpty(extractFileName) && !string.IsNullOrEmpty(destinationPath))
            {
                InstallHelper.Instance.Init(extractFileName, destinationPath, "CDPIUI_TrayIcon.exe");
                

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainWindow());
            }
        }
    }
}
