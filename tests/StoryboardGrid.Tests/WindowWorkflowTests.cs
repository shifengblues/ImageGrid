using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using StoryboardGrid;

internal static class WindowWorkflowTests
{
    public static void Run()
    {
        Exception? error=null;
        var thread=new Thread(()=>
        {
            var app=new App {ShutdownMode=ShutdownMode.OnExplicitShutdown};app.InitializeComponent();
            string directory=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../output/window-workflow"));
            var frame=new DispatcherFrame();
            _=Dispatcher.CurrentDispatcher.BeginInvoke(new Action(async()=>
            {
                try{await ReleaseCheck.CheckWindowExportAsync(directory);}
                catch(Exception ex){error=ex;}
                finally{frame.Continue=false;}
            }));
            Dispatcher.PushFrame(frame);app.Shutdown();
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();
        if(!thread.Join(TimeSpan.FromSeconds(30)))throw new Exception("Window export workflow timed out.");
        if(error!=null)throw new Exception("Actual window export workflow regression",error);
        Console.WriteLine("PASS actual MainWindow compose export three times with live UI progress");
        Console.WriteLine("PASS actual MainWindow split export three times with live UI progress");
        Console.WriteLine("PASS window export save cancellation and split filename conflicts");
        Console.WriteLine("PASS actual thumbnail X button preserves remaining image order");
        Console.WriteLine("PASS green accent, removed subtitles, split navigation and invalid export settings");
        Console.WriteLine("PASS window recovers from dialog and worker failures before exporting again");
        Console.WriteLine("PASS actual canvas renders 100 percent zoom and split handles directory conflicts");
        Console.WriteLine("PASS hidden compose validation does not block split export");
        Console.WriteLine("PASS actual page navigation preserves independent layouts and adjusted cuts");
        Console.WriteLine("PASS HSV picker exact input, invalid input protection and edge selection");
        Console.WriteLine("PASS light and dark themes with system-follow policy and picker rendering");
        Console.WriteLine("PASS rounded green dropdown items and themed confirmation dialogs");
        Console.WriteLine("PASS clear confirmation cancel and accept preserve intended behavior");
        Console.WriteLine("PASS bundled logo and icon; default options fit and small window scrolls");
        Console.WriteLine("PASS cleared canvas resets stale image zoom indicator");
        Console.WriteLine("PASS batch import over occupied cell appends without changing supplied order");
        Console.WriteLine("PASS partial import failure preserves valid files and failed replacement preserves original");
        Console.WriteLine("PASS successful single replacement preserves all other positions");
        Console.WriteLine("PASS import dialog errors recover and canceled selection preserves pictures");
        Console.WriteLine("PASS import can reopen successfully after error and stays blocked during active task");
    }
}
