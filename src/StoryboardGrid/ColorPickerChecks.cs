using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StoryboardGrid;
internal static class ColorPickerChecks
{
    internal static void Run(string directory,string suffix="")
    {
        var picker=new ColorPickerWindow(Color.FromRgb(22,134,93));
        try
        {
            var hex=(TextBox)picker.FindName("HexInput");var apply=(Button)picker.FindName("ApplyButton");
            hex.Text="#0080FF";
            if(picker.SelectedColor!=Color.FromRgb(0,128,255))throw new Exception("Color picker HEX did not update selection.");
            ((TextBox)picker.FindName("RedInput")).Text="255";((TextBox)picker.FindName("GreenInput")).Text="128";((TextBox)picker.FindName("BlueInput")).Text="0";
            if(hex.Text!="#FF8000")throw new Exception("Color picker RGB did not update HEX.");
            hex.Text="#12";if(apply.IsEnabled||picker.SelectedColor!=Color.FromRgb(255,128,0))throw new Exception("Invalid HEX changed color or remained applicable.");
            hex.Text="#16865D";
            var field=(ColorField)picker.FindName("Field");field.Measure(new Size(320,320));field.Arrange(new Rect(0,0,320,320));
            field.SelectAt(new Point(0,0));if(picker.SelectedColor!=Colors.White)throw new Exception("Color field white corner failed.");
            field.SelectAt(new Point(320,320));if(picker.SelectedColor!=Colors.Black)throw new Exception("Color field black corner failed.");
            var hue=(ColorField)picker.FindName("HueStrip");hue.Measure(new Size(24,320));hue.Arrange(new Rect(0,0,24,320));hue.SelectAt(new Point(12,160));field.SelectAt(new Point(320,0));
            if(picker.SelectedColor!=Colors.Cyan)throw new Exception("Hue strip and saturation field are not synchronized.");
            hex.Text="#00FFFF";
            var root=(System.Windows.Controls.Grid)picker.Content;root.SetResourceReference(System.Windows.Controls.Panel.BackgroundProperty,"AppBackground");
            root.Measure(new Size(660,560));root.Arrange(new Rect(0,0,660,560));root.UpdateLayout();
            var blue=(TextBox)picker.FindName("BlueInput");var blueOrigin=blue.TranslatePoint(new Point(),root);
            if(blueOrigin.Y+blue.ActualHeight>374||blue.ActualHeight<30)throw new Exception("Color picker RGB inputs are clipped.");
            var image=new RenderTargetBitmap(660,560,96,96,PixelFormats.Pbgra32);image.Render(root);
            using var stream=File.Create(Path.Combine(directory,$"color-picker-preview{suffix}.png"));var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));encoder.Save(stream);
        }
        finally{picker.Close();}
    }
}
