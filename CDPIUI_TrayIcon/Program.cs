using CDPIUI_TrayIcon.Helper;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;
using Application = System.Windows.Forms.Application;

class Programm
{
    private const string MutexName = "Global\\AppTrayIconMutex_3f6a1b9d-8c2a-4f3b-9f2a-0f1a2b3c4d5e";
    private static bool created;

    static void Main(string[] args)
    {
        using (var mutex = new Mutex(true, MutexName, out created))
        {
            if (!created)
            {
                MessageBox.Show("Tray app is already running.");
                return;
            }

            
        }
        PipeServer.Instance.Init();
        PipeServer.Instance.Start();

        _ = TrayIconHelper.Instance;
        


        Application.Run();
    }

}