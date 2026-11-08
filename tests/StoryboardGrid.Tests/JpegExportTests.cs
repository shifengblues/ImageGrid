using System;
using System.IO;
using SkiaSharp;
using StoryboardGrid.Core;

internal static class JpegExportTests
{
    public static void Run()
    {
        string folder=Path.Combine(Path.GetTempPath(),"StoryboardGrid-jpeg-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try
        {
            string source=Path.Combine(folder,"alpha.png"),target=Path.Combine(folder,"q100.jpg");
            using(var b=new SKBitmap(64,32,SKColorType.Bgra8888,SKAlphaType.Unpremul))
            {b.Erase(new SKColor(255,0,0,0));b.Erase(new SKColor(255,0,0,128),new SKRectI(32,0,64,32));using var data=b.Encode(SKEncodedImageFormat.Png,100);using var file=File.Create(source);data.SaveTo(file);}
            var info=ImageService.Inspect(source);var layout=new GridLayout(1,1,64,32,0);
            ImageService.Compose([info],layout,true,false,new SKColor(20,160,80),SKColors.White,target,null,default,SKEncodedImageFormat.Jpeg);
            byte[] bytes=File.ReadAllBytes(target);if(bytes[0]!=255||bytes[1]!=216)throw new Exception("JPG output is not JPEG data.");
            using var image=ImageService.Decode(target);var clear=image.GetPixel(8,16);var half=image.GetPixel(52,16);
            if(image.Width!=64||image.Height!=32||clear.Alpha!=255||Math.Abs(clear.Red-20)>3||Math.Abs(clear.Green-160)>3||Math.Abs(clear.Blue-80)>3)throw new Exception("JPEG transparent background flattening failed.");
            if(Math.Abs(half.Red-138)>3||Math.Abs(half.Green-80)>3||Math.Abs(half.Blue-40)>3)throw new Exception("JPEG semi-transparent pixels flattened incorrectly.");
        }
        finally {foreach(string file in Directory.GetFiles(folder))File.Delete(file);Directory.Delete(folder);}
        Console.WriteLine("PASS quality-100 JPEG uses JPEG encoding and keeps original dimensions");
        Console.WriteLine("PASS JPEG flattens transparent and semi-transparent pixels onto configured background");
    }
}
