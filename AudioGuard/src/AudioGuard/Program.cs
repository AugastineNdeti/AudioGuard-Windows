using System.Threading;
using System.Windows.Forms;

namespace AudioGuard;

internal static class Program
{
    private static Mutex? _singleInstance;

    [STAThread]
    private static void Main()
    {
        _singleInstance = new Mutex(true, "Local\\AudioGuard.SingleInstance", out bool createdNew);
        if (!createdNew)
            return;

        ApplicationConfiguration.Initialize();
        Application.Run(new AudioGuardContext());
        GC.KeepAlive(_singleInstance);
    }
}
