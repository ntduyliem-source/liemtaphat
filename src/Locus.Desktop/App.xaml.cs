using System.Windows;

namespace Locus.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length == 2 && e.Args[0] == "--smoke-output")
        {
            try { Shutdown(PackageSmoke.Run(System.IO.Path.GetFullPath(e.Args[1]))); }
            catch { Shutdown(1); }
            return;
        }
        if (e.Args.Length == 2 && e.Args[0] == "--word-probe")
        {
            MainWindow = new WordProbeWindow(e.Args[1]); MainWindow.Show(); return;
        }
        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}
