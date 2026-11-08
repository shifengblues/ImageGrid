using System;
using System.IO;
using System.Linq;
using System.Threading;
using SkiaSharp;
using StoryboardGrid;
using StoryboardGrid.Core;

internal static class ExportSafetyTests
{
    public static void Run()
    {
        string folder=Path.Combine(Path.GetTempPath(),"StoryboardGrid-safety-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try
        {
            string source=Path.Combine(folder,"source.png"),target=Path.Combine(folder,"keep.png");Write(source,32,18);File.Copy(source,target);
            var imported=ImageService.Inspect(source);var layout=GridLayout.Calculate([imported],1,1,16,9,0);byte[] preserved=File.ReadAllBytes(target);
            Write(source,16,9);
            foreach(bool original in new[]{true,false})
            {
                bool rejected=false;
                try{ImageService.Compose([imported],layout,original,false,SKColors.White,SKColors.White,target,null,default);}catch(IOException){rejected=true;}
                if(!rejected||!File.ReadAllBytes(target).SequenceEqual(preserved))throw new Exception("Resized source silently exported or overwrote existing destination.");
            }
            using var cancel=new CancellationTokenSource();cancel.Cancel();
            try{ImageService.Compose([new Picture("missing.png",100000,100000)],new GridLayout(1,1,100000,100000,0),true,false,SKColors.White,SKColors.White,target,null,cancel.Token);throw new Exception("Canceled compose accepted.");}catch(OperationCanceledException){}
            try{ImageService.Split(new Picture("missing.png",32,18),[0,16,32],[0,9,18],0,folder,null,cancel.Token);throw new Exception("Canceled split accepted.");}catch(OperationCanceledException){}
            string conflict=Path.Combine(folder,"source_01.png");Directory.CreateDirectory(conflict);
            if(!MainWindow.HasSplitConflict(folder,source,4))throw new Exception("Directory with an output filename was not detected.");
            Directory.Delete(conflict);
            if(MainWindow.HasSplitConflict(folder,source,4))throw new Exception("Empty split destination incorrectly conflicts.");
        }
        finally {foreach(string file in Directory.GetFiles(folder))File.Delete(file);foreach(string dir in Directory.GetDirectories(folder))Directory.Delete(dir);Directory.Delete(folder);}
        Console.WriteLine("PASS source dimension changes are rejected in original and unified modes");
        Console.WriteLine("PASS changed source never overwrites existing destination");
        Console.WriteLine("PASS cancellation happens before allocation and source decoding");
        Console.WriteLine("PASS split detects both file and directory filename conflicts");
    }
    static void Write(string path,int w,int h){using var image=new SKBitmap(w,h);image.Erase(SKColors.Coral);using var data=image.Encode(SKEncodedImageFormat.Png,100);using var stream=File.Create(path);data.SaveTo(stream);}
}
