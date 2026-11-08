using System;
using System.IO;
using System.Threading;
using System.Windows.Threading;
using SkiaSharp;
using StoryboardGrid;

internal static class CanvasPreviewTests
{
    public static void Run()
    {
        Exception? error=null;
        var thread=new Thread(()=>
        {
            string path=Path.Combine(Path.GetTempPath(),"StoryboardGrid-preview-"+Guid.NewGuid().ToString("N")+".png");
            try
            {
                using(var image=new SKBitmap(1600,900)){image.Erase(SKColors.Coral);using var data=image.Encode(SKEncodedImageFormat.Png,100);using var stream=File.Create(path);data.SaveTo(stream);}
                var entry=MainWindow.Load(path);var cache=new CanvasPreviewCache(Dispatcher.CurrentDispatcher);var frame=new DispatcherFrame();bool upgraded=false;
                if(entry.Thumbnail.PixelWidth!=480)throw new Exception("Bottom thumbnail must remain small.");
                cache.Changed+=()=>
                {
                    var image=cache.Get(entry,1200);
                    upgraded=image.PixelWidth==1600&&image.IsFrozen;
                    frame.Continue=false;
                };
                cache.Get(entry,1200);
                var timeout=new DispatcherTimer {Interval=TimeSpan.FromSeconds(10)};timeout.Tick+=(_,_)=>frame.Continue=false;timeout.Start();
                Dispatcher.PushFrame(frame);timeout.Stop();cache.Prune([]);
                if(!upgraded)throw new Exception("Canvas preview did not upgrade to original 1600px source.");
                if(entry.Thumbnail.PixelWidth!=480||CanvasPreviewCache.TargetEdge(100000)!=4096)throw new Exception("Preview bounds or thumbnail isolation failed.");
                // If an external edit makes the source smaller, a completed request must not decode forever.
                using(var image=new SKBitmap(800,450)){image.Erase(SKColors.Coral);using var data=image.Encode(SKEncodedImageFormat.Png,100);using var stream=File.Create(path);data.SaveTo(stream);}
                var changedSource=new CanvasPreviewCache(Dispatcher.CurrentDispatcher);int refreshes=0;
                changedSource.Changed+=()=>{refreshes++;changedSource.Get(entry,1200);};changedSource.Get(entry,1200);
                frame=new DispatcherFrame();var settle=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};settle.Tick+=(_,_)=>frame.Continue=false;settle.Start();Dispatcher.PushFrame(frame);settle.Stop();changedSource.Prune([]);
                if(refreshes!=1)throw new Exception($"Changed source caused repeated preview decoding: {refreshes}");
                var restored=new CanvasPreviewCache(Dispatcher.CurrentDispatcher);bool recovered=false;File.Delete(path);
                restored.Get(entry,1200);Pump(500);
                using(var image=new SKBitmap(1600,900)){image.Erase(SKColors.Coral);using var data=image.Encode(SKEncodedImageFormat.Png,100);using var stream=File.Create(path);data.SaveTo(stream);}
                restored.Changed+=()=>recovered=restored.Get(entry,1200).PixelWidth==1600;
                restored.RetryVisible();restored.Get(entry,1200);Pump(600);
                if(!recovered)throw new Exception("Restored source cannot recover clearer preview after Fit/retry.");
                restored.Dispose();bool afterClose=false;restored.Changed+=()=>afterClose=true;restored.Get(entry,4000);Pump(300);
                if(afterClose)throw new Exception("Disposed preview continued decoding.");
                cache.Dispose();changedSource.Dispose();
            }
            catch(Exception ex){error=ex;}
            finally {if(File.Exists(path))File.Delete(path);Dispatcher.CurrentDispatcher.InvokeShutdown();}
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();
        if(!thread.Join(TimeSpan.FromSeconds(15)))throw new Exception("Preview regression test timed out.");
        if(error!=null)throw new Exception("Adaptive canvas preview regression",error);
        Console.WriteLine("PASS canvas asynchronously upgrades 480px thumbnail to 1600px preview");
        Console.WriteLine("PASS bottom thumbnails stay small and canvas preview resolution is bounded");
        Console.WriteLine("PASS externally resized source does not cause repeated preview decoding");
        Console.WriteLine("PASS restored source retries high-resolution preview without reimport");
        Console.WriteLine("PASS disposed canvas cache ignores further preview requests");
    }
    static void Pump(int milliseconds)
    {
        var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(milliseconds)};
        timer.Tick+=(_,_)=>frame.Continue=false;timer.Start();Dispatcher.PushFrame(frame);timer.Stop();
    }
}
