using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;
using SkiaSharp;
using StoryboardGrid;
using StoryboardGrid.Core;

internal static class ExportProgressTests
{
    public static void Run()
    {
        Exception? error=null;
        var thread=new Thread(()=>
        {
            try
            {
                string folder=Path.Combine(Path.GetTempPath(),"StoryboardGrid-progress-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
                try
                {
                    string source=Path.Combine(folder,"source.png");
                    using(var image=new SKBitmap(32,16)) {image.Erase(SKColors.Coral);using var data=image.Encode(SKEncodedImageFormat.Png,100);using var file=File.Create(source);data.SaveTo(file);}
                    var picture=ImageService.Inspect(source);
                    Exercise(progress=>ImageService.Compose([picture],GridLayout.Calculate([picture],1,1,2,1,0),true,false,SKColors.White,SKColors.White,Path.Combine(folder,"grid.png"),progress,default));
                    Exercise(progress=>ImageService.Split(picture,[0,16,32],[0,8,16],0,folder,progress,default));
                }
                finally {foreach(var file in Directory.GetFiles(folder))File.Delete(file);Directory.Delete(folder);}
            }
            catch(Exception ex){error=ex;}
            finally {Dispatcher.CurrentDispatcher.InvokeShutdown();}
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();
        if(!thread.Join(TimeSpan.FromSeconds(30)))throw new Exception("WPF export progress test timed out.");
        if(error!=null)throw new Exception("WPF export progress regression",error);
        Console.WriteLine("PASS compose progress updates real WPF control from worker");
        Console.WriteLine("PASS split progress updates real WPF control from worker");
    }
    static void Exercise(Action<IProgress<double>> export)
    {
        var dispatcher=Dispatcher.CurrentDispatcher;var frame=new DispatcherFrame();var bar=new ProgressBar{Maximum=1};int updates=0;
        var finished=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker=Task.Run(async()=>
        {
            try
            {
                if(SynchronizationContext.Current!=null)throw new Exception("Test must construct progress on worker without UI context.");
                export(new ExportProgress(dispatcher,value=>
                {
                    try{bar.Value=value;updates++;if(value==1)finished.TrySetResult();}
                    catch(Exception ex){finished.TrySetException(ex);}
                }));
                await finished.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            finally {_ = dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>frame.Continue=false));}
        });
        Dispatcher.PushFrame(frame);worker.GetAwaiter().GetResult();
        if(updates==0||bar.Value!=1)throw new Exception("Export progress did not complete on UI thread.");
    }
}
