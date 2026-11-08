using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using SkiaSharp;
using StoryboardGrid.Core;

internal static class LargeImageTests
{
    public static void GridUhd(string folder,string output)
    {
        Directory.CreateDirectory(output);
        var pictures=Directory.GetFiles(folder,"uhd-*.png").Order().Select(ImageService.Inspect).ToArray();
        if(pictures.Length!=25||pictures.Any(p=>p.Width!=3840||p.Height!=2160))throw new Exception("Expected 25 distinct UHD input files.");
        var layout=GridLayout.Calculate(pictures,5,5,16,9,3);
        foreach(var format in new[]{SKEncodedImageFormat.Png,SKEncodedImageFormat.Jpeg})
        {
            string target=Path.Combine(output,format==SKEncodedImageFormat.Png?"native-5x5.png":"native-5x5.jpg");
            var timer=System.Diagnostics.Stopwatch.StartNew();
            ImageService.Compose(pictures,layout,false,true,SKColors.White,SKColors.White,target,null,default,format);
            using var result=ImageService.Decode(target);
            if(result.Width!=19212||result.Height!=10812)throw new Exception("Native UHD output dimensions mismatch.");
            if(format==SKEncodedImageFormat.Png)
            for(int i=0;i<pictures.Length;i++)
            {
                using var source=ImageService.Decode(pictures[i].Path);var a=new byte[source.Width*4];var b=new byte[a.Length];
                for(int row=0;row<source.Height;row++)
                {
                    Marshal.Copy(source.GetPixels()+row*source.RowBytes,a,0,a.Length);
                    Marshal.Copy(result.GetPixels()+(i/5*2163+row)*result.RowBytes+i%5*3843*4,b,0,b.Length);
                    if(!a.SequenceEqual(b))throw new Exception($"Native tile {i} pixels differ.");
                }
            }
            Console.WriteLine($"PASS native 25 UHD {format} -> {result.Width}x{result.Height}, {new FileInfo(target).Length} bytes, {timer.Elapsed.TotalSeconds:0.0}s; PNG full-pixel verification");
        }
    }
    public static void Run(string path,string folder)
    {
        Directory.CreateDirectory(folder);var info=ImageService.Inspect(path);byte[] hash=SHA256.HashData(File.ReadAllBytes(path));
        var xs=Cuts.Equal(info.Width,3);var ys=Cuts.Equal(info.Height,3);
        var files=ImageService.Split(info,xs,ys,0,folder,null,default);
        using(var original=ImageService.Decode(path))
        for(int i=0;i<files.Length;i++)
        {
            using var tile=ImageService.Decode(files[i]);int c=i%3,r=i/3;
            if(tile.Width!=xs[c+1]-xs[c]||tile.Height!=ys[r+1]-ys[r])throw new Exception("Large split dimensions mismatch.");
            var a=new byte[tile.Width*4];var b=new byte[a.Length];
            for(int y=0;y<tile.Height;y++){Marshal.Copy(original.GetPixels()+(ys[r]+y)*original.RowBytes+xs[c]*4,a,0,a.Length);Marshal.Copy(tile.GetPixels()+y*tile.RowBytes,b,0,b.Length);if(!a.SequenceEqual(b))throw new Exception("Large split pixel mismatch.");}
        }
        var layout=new GridLayout(1,1,info.Width,info.Height,0);
        ImageService.Compose([info],layout,true,false,SKColors.White,SKColors.White,Path.Combine(folder,"large-copy.png"),null,default);
        ImageService.Compose([info],layout,true,false,SKColors.White,SKColors.White,Path.Combine(folder,"large-q100.jpg"),null,default,SKEncodedImageFormat.Jpeg);
        if(!SHA256.HashData(File.ReadAllBytes(path)).SequenceEqual(hash))throw new Exception("Source changed.");
        Console.WriteLine($"PASS real {info.Width}x{info.Height} image split into 9 exact-pixel PNGs");
        Console.WriteLine("PASS large PNG and quality-100 JPEG exports preserve dimensions; original file unchanged");
    }
}
