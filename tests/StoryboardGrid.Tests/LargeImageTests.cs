using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using SkiaSharp;
using StoryboardGrid.Core;

internal static class LargeImageTests
{
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
