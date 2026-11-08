using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SkiaSharp;
using StoryboardGrid.Core;

namespace StoryboardGrid;
internal static class FillSpacingChecks
{
    internal static async Task RunAsync(string directory)
    {
        var window=new MainWindow();
        try
        {
            var root=(FrameworkElement)window.Content;
            root.Measure(new Size(1360,960));root.Arrange(new Rect(0,0,1360,960));root.UpdateLayout();
            var slider=FindSlider((DependencyObject)window.FindName("ComposeSettings"));
            string source=Path.Combine(directory,"spacing-small.png");
            Directory.CreateDirectory(directory);
            using(var bitmap=new SKBitmap(120,90)){bitmap.Erase(SKColors.Coral);using var data=bitmap.Encode(SKEncodedImageFormat.Png,100);using var stream=File.Create(source);data.SaveTo(stream);}
            for(int i=0;i<5;i++)window.State.Images.Add(MainWindow.Load(source));
            slider.SetCurrentValue(RangeBase.ValueProperty,60d);
            await Dispatcher.Yield(DispatcherPriority.Background);
            if(window.State.Gap!=60||window.State.Layout!.Gap!=60)throw new Exception($"Gap slider failed: slider={slider.Value}, state={window.State.Gap}, layout={window.State.Layout!.Gap}.");
            Console.WriteLine("PASS actual gap slider immediately updates workspace and layout");
            if(window.State.Error==null)throw new Exception("Over-capacity images must still block export.");
            window.SaveDialogOverride=_=>throw new Exception("Over-capacity export opened a save dialog.");
            await window.ExportAsync();
            Slider.IncreaseLarge.Execute(null,slider);
            if(window.State.Layout!.Gap!=slider.Value||window.State.Gap!=slider.Value)throw new Exception("Slider track command did not refresh over-capacity preview.");
            window.State.Images.Clear();window.State.Rows=1;window.State.Background="#FFFF00";window.State.GapColor="#FF00FF";
            using(var bitmap=new SKBitmap(120,90))
            {
                bitmap.Erase(SKColors.Coral);using(var canvas=new SKCanvas(bitmap)){using var paint=new SKPaint{Color=SKColors.Lime};canvas.DrawRect(40,25,40,40,paint);}
                using var data=bitmap.Encode(SKEncodedImageFormat.Png,100);using var stream=File.Create(source);data.SaveTo(stream);
            }
            string large=Path.Combine(directory,"spacing-large.png");
            using(var bitmap=new SKBitmap(320,180)){bitmap.Erase(SKColors.Blue);using var data=bitmap.Encode(SKEncodedImageFormat.Png,100);using var stream=File.Create(large);data.SaveTo(stream);}
            window.State.Images.Add(MainWindow.Load(source));window.State.Images.Add(MainWindow.Load(large));
            if(window.State.Mode!=2)throw new Exception("New workspace does not default to automatic fill.");
            window.ErrorDialogOverride=message=>throw new Exception(message);
            var preview=(GridCanvas)window.FindName("Canvas");
            foreach(int gap in new[]{0,60,100})
            {
                slider.SetCurrentValue(RangeBase.ValueProperty,(double)gap);
                await Dispatcher.Yield(DispatcherPriority.Background);
                var layout=window.State.Layout!;
                preview.Fit();preview.Measure(new Size(720,480));preview.Arrange(new Rect(0,0,720,480));preview.UpdateLayout();
                var image=new RenderTargetBitmap(720,480,96,96,PixelFormats.Pbgra32);image.Render(preview);
                double scale=CanvasGeometry.FitScale(720,480,layout.Width,layout.Height);
                double ox=(720-layout.Width*scale)/2,oy=(480-layout.Height*scale)/2;
                CheckPreview(image,(int)(ox+20*scale),(int)(oy+140*scale),SKColors.Coral);
                if(gap>0)CheckPreview(image,(int)(ox+(320+gap/2d)*scale),(int)(oy+140*scale),SKColors.Magenta);
                string target=Path.Combine(directory,$"auto-fill-gap-{gap}.png");window.SaveDialogOverride=dialog=>{dialog.FileName=target;return true;};await window.ExportAsync();
                using var output=ImageService.Decode(target);
                if(output.Width!=640+gap||output.Height!=180||output.GetPixel(20,140)!=SKColors.Coral||output.GetPixel(320+gap+20,140)!=SKColors.Blue)throw new Exception("Auto fill left blank space or exported wrong dimensions.");
                if(gap>0&&Enumerable.Range(320,gap).Any(x=>output.GetPixel(x,140)!=SKColors.Magenta))throw new Exception("Exported gap width/color mismatch.");
                int across=Enumerable.Range(0,320).Count(x=>output.GetPixel(x,90)==SKColors.Lime);
                int down=Enumerable.Range(0,180).Count(y=>output.GetPixel(160,y)==SKColors.Lime);
                if(Math.Abs(across-down)>1||across<100||output.GetPixel(160,90)!=SKColors.Lime)throw new Exception("Small image was distorted or not centered.");
                using var stream=File.Create(Path.Combine(directory,$"preview-gap-{gap}.png"));var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));encoder.Save(stream);
            }
            Console.WriteLine("PASS small 4:3 image scales to fill 16:9 cell, centered without stretching");
            Console.WriteLine("PASS gap 0/60/100 updates actual canvas and exported pixels consistently");
            window.State.Mode=0;
            string original=Path.Combine(directory,"original-small.png");window.SaveDialogOverride=dialog=>{dialog.FileName=original;return true;};await window.ExportAsync();
            using(var output=ImageService.Decode(original)){if(output.GetPixel(20,140)!=SKColors.Yellow||output.GetPixel(160,90)!=SKColors.Lime)throw new Exception("Original pixel mode no longer preserves centered small image.");}
            Console.WriteLine("PASS original pixel mode remains available without enlarging small images");
        }
        finally{window.Close();}
    }
    static void CheckPreview(BitmapSource image,int x,int y,SKColor expected)
    {
        byte[] pixel=new byte[4];image.CopyPixels(new Int32Rect(x,y,1,1),pixel,4,0);
        if(Math.Abs(pixel[0]-expected.Blue)>2||Math.Abs(pixel[1]-expected.Green)>2||Math.Abs(pixel[2]-expected.Red)>2)throw new Exception($"Canvas pixel mismatch at {x},{y}: {pixel[2]},{pixel[1]},{pixel[0]}.");
    }
    static Slider FindSlider(DependencyObject parent)
    {
        if(parent is Slider slider)return slider;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
        {
            var found=FindSlider(VisualTreeHelper.GetChild(parent,i));if(found!=null)return found;
        }
        return null!;
    }
}
