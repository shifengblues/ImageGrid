using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StoryboardGrid;
internal static class UiPolishChecks
{
    internal static void Run(MainWindow window,string directory)
    {
        int original=ThemeManager.Mode;
        try
        {
            foreach(int mode in new[]{0,1})
            {
                ThemeManager.SetMode(mode);
                var item=new ComboBoxItem{Content="跟随系统",IsSelected=true};item.ApplyTemplate();
                var shell=(Border)item.Template.FindName("ItemShell",item);
                if(shell.CornerRadius.TopLeft<6||((SolidColorBrush)shell.Background).Color!=((SolidColorBrush)ThemeManager.Brush("AccentSoft")).Color)throw new Exception("Dropdown item uses system selection style.");
                var host=new StackPanel{Width=230,Background=ThemeManager.Brush("Card")};host.Children.Add(item);Render(host,250,65,Path.Combine(directory,$"dropdown-{mode}.png"));
                var dialog=new PromptWindow("清空图片","将移除全部已导入图片。\n原始文件不会被删除。","清空图片",true);
                var root=(FrameworkElement)dialog.Content;Render(root,464,245,Path.Combine(directory,$"prompt-{mode}.png"));
                ((Button)dialog.FindName("CancelButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(dialog.Accepted)throw new Exception("Cancel accepted destructive action.");
            }
            var accepted=new PromptWindow("清空图片","原始文件不会被删除。","清空图片",true);
            ((Button)accepted.FindName("ConfirmButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(!accepted.Accepted)throw new Exception("Confirm button did not accept.");
            int count=window.State.Images.Count;window.ClearConfirmationOverride=()=>false;window.ClearImages();if(window.State.Images.Count!=count)throw new Exception("Clear cancellation removed images.");
            window.ClearConfirmationOverride=()=>true;window.ClearImages();if(window.State.Images.Count!=0)throw new Exception("Confirmed clear did not clear images.");
            window.ClearConfirmationOverride=null;
            var logo=(Image)window.FindName("BrandLogo");if(logo.Source==null||window.Icon==null)throw new Exception("Brand/window icon resource missing.");
            ((Button)window.FindName("ComposeTab")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));window.State.Mode=2;
            var rootView=(FrameworkElement)window.Content;ThemeManager.SetMode(0);Render(rootView,1360,960,Path.Combine(directory,"main-light.png"));
            if(((TextBlock)window.FindName("ZoomLabel")).Text!="—")throw new Exception("Cleared canvas still displays the previous image zoom.");
            var scroll=(ScrollViewer)window.FindName("SettingsScroll");
            if(scroll.ScrollableHeight>1)throw new Exception($"Default options require scrolling: extent {scroll.ExtentHeight}, viewport {scroll.ViewportHeight}.");
            Render(rootView,1080,720,Path.Combine(directory,"main-small.png"));
            if(scroll.ScrollableHeight<=0)throw new Exception("Small window did not enable scrolling.");
            var bar=new ScrollBar();bar.ApplyTemplate();if(bar.Width>8)throw new Exception("Scrollbar is too wide.");
            ThemeManager.SetMode(1);Render(rootView,1360,960,Path.Combine(directory,"main-dark.png"));
        }
        finally{ThemeManager.SetMode(original);}
    }
    static void Render(FrameworkElement root,int width,int height,string path)
    {
        root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();
        var image=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);image.Render(root);
        using var stream=File.Create(path);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));encoder.Save(stream);
    }
}
