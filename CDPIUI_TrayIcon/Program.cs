using CDPIUI_TrayIcon.Helper;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;
using Application = System.Windows.Forms.Application;

class Programm
{
    static void Main(string[] args)
    {
        PipeServer.Instance.Init();
        PipeServer.Instance.Start();

        _ = TrayIconHelper.Instance;
        


        Application.Run();
    }

}