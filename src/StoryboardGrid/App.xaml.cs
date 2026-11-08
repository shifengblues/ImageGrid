using System.Windows;
using System;
using System.IO;
using System.Threading.Tasks;

namespace StoryboardGrid;
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if(e.Args.Length==2 && e.Args[0]=="--verify")
        {
            ShutdownMode=ShutdownMode.OnExplicitShutdown;
            try {await Task.Run(()=>ReleaseCheck.Run(e.Args[1]));await ReleaseCheck.CheckWindowExportAsync(e.Args[1]);Shutdown(0);}
            catch(Exception ex){Directory.CreateDirectory(e.Args[1]);File.WriteAllText(Path.Combine(e.Args[1],"failed.txt"),ex.ToString());Shutdown(1);}
            return;
        }
        MainWindow=new MainWindow();MainWindow.Show();
    }
}
