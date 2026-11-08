using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Data;
using SkiaSharp;
using StoryboardGrid.Core;

namespace StoryboardGrid;
// Invoked by the release script to exercise bundled native decoders and PNG export in the final EXE.
internal static class ReleaseCheck
{
    internal static async Task CheckWindowExportAsync(string directory)
    {
        await FillSpacingChecks.RunAsync(directory);
        string owned=Path.Combine(directory,"window-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(owned);
        string source=Path.Combine(owned,"source.png");
        using(var b=new SKBitmap(160,90)){b.Erase(SKColors.Coral);using var data=b.Encode(SKEncodedImageFormat.Png,100);using var stream=File.Create(source);data.SaveTo(stream);}
        var window=new MainWindow();
        int originalTheme=ThemeManager.Mode;
        try
        {
            var themes=(ComboBox)window.FindName("ThemeCombo");themes.SelectedIndex=1;
            if(!ThemeManager.IsDark||((SolidColorBrush)ThemeManager.Brush("AppBackground")).Color!=Color.FromRgb(25,29,34))throw new Exception("Dark theme did not apply.");
            ColorPickerChecks.Run(directory,"-dark");themes.SelectedIndex=0;
            if(ThemeManager.IsDark||!ThemeManager.ResolveDark(2,true)||ThemeManager.ResolveDark(2,false)||ThemeManager.ResolveDark(0,true)||!ThemeManager.ResolveDark(1,false))throw new Exception("Light/system theme policy failed.");
            window.State.Images.Add(MainWindow.Load(source));
            var remaining=window.State.Images[0];
            window.State.Images.Add(MainWindow.Load(source));window.State.Move(0,1);
            var thumbs=(Panel)window.FindName("Thumbnails");
            var first=(StackPanel)thumbs.Children[0];var border=(Border)first.Children[0];
            ((Grid)border.Child).Children.OfType<Button>().Single().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(window.State.Images.Count!=1||!ReferenceEquals(window.State.Images[0],remaining))throw new Exception("Thumbnail delete button changed remaining image order.");
            if(((SolidColorBrush)Application.Current.FindResource("Accent")).Color!=Color.FromRgb(22,134,93)||window.FindName("PageSubtitle")!=null)throw new Exception("Green theme or subtitle removal did not apply.");
            window.State.Gap=-1;await window.ExportAsync();if(window.State.Busy||window.State.Error==null)throw new Exception("Invalid export settings were accepted.");window.State.Gap=3;
            int errors=0;window.ErrorDialogOverride=_=>errors++;
            window.SaveDialogOverride=_=>throw new IOException("File dialog unavailable");await window.ExportAsync();
            if(errors!=1||window.State.Busy||!window.State.Status.StartsWith("处理失败"))throw new Exception("File dialog failure escaped or left the window busy.");
            window.State.Images[0]=new ImageEntry(new Picture(Path.Combine(owned,"missing.png"),160,90),remaining!.Thumbnail);
            string failedOutput=Path.Combine(owned,"must-not-exist.png");window.SaveDialogOverride=dialog=>{dialog.FileName=failedOutput;return true;};await window.ExportAsync();
            if(errors!=2||window.State.Busy||File.Exists(failedOutput))throw new Exception("Worker failure did not restore the window state.");
            window.State.Images[0]=remaining;
            var canvas=(GridCanvas)window.FindName("Canvas");canvas.Measure(new Size(720,480));canvas.Arrange(new Rect(0,0,720,480));
            var render=new RenderTargetBitmap(720,480,96,96,PixelFormats.Pbgra32);canvas.UpdateLayout();await Dispatcher.Yield(DispatcherPriority.Background);render.Render(canvas);
            canvas.PixelView();canvas.Arrange(new Rect(0,0,720,480));await Dispatcher.Yield(DispatcherPriority.Background);render.Render(canvas);
            if(((TextBlock)window.FindName("ZoomLabel")).Text!="100%")throw new Exception($"Actual 100% canvas rendering failed: {((TextBlock)window.FindName("ZoomLabel")).Text}, viewport {canvas.ActualWidth}x{canvas.ActualHeight}.");
            window.SaveDialogOverride=dialog=>false;
            await window.ExportAsync();
            if(window.State.Busy)throw new Exception("Canceling save dialog left the UI busy.");
            for(int i=0;i<3;i++)
            {
                string target=Path.Combine(owned,$"grid-{i}.png");
                window.SaveDialogOverride=dialog=>{dialog.FileName=target;return true;};
                await window.ExportAsync();await Dispatcher.Yield(DispatcherPriority.Background);
                if(!File.Exists(target)||window.State.Busy||!window.State.Status.StartsWith("已导出"))throw new Exception("Window composition workflow failed.");
            }
            window.State.ExportFormat=1;string jpg=Path.Combine(owned,"window-q100.jpg");
            window.SaveDialogOverride=dialog=>{if(dialog.DefaultExt.TrimStart('.')!="jpg"||!dialog.Filter.Contains("最高质量")||dialog.Filter.Contains("有损"))throw new Exception("JPEG save dialog is not correctly labeled.");dialog.FileName=jpg;return true;};
            await window.ExportAsync();if(!File.Exists(jpg))throw new Exception("Window JPEG export failed: "+window.State.Status);using(var decoded=ImageService.Decode(jpg)){if(decoded.Width!=323||decoded.Height!=183)throw new Exception("Window JPEG export dimensions mismatch.");}
            window.State.ExportFormat=0;
            window.State.Source=MainWindow.Load(source);window.State.ResetCuts();
            ((Button)window.FindName("SplitTab")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(((TextBlock)window.FindName("PageTitle")).Text!="宫格拆分")throw new Exception("Split navigation failed.");
            window.FolderDialogOverride=dialog=>false;await window.ExportAsync();if(window.State.Busy)throw new Exception("Canceling folder dialog left the UI busy.");
            window.FolderDialogOverride=dialog=>{dialog.FolderName=owned;return true;};
            var hiddenInput=new TextBox();hiddenInput.SetBinding(TextBox.TextProperty,new Binding(nameof(Workspace.TargetW)){Source=window.State});
            ((StackPanel)window.FindName("ComposeSettings")).Children.Add(hiddenInput);
            var expression=hiddenInput.GetBindingExpression(TextBox.TextProperty)!;
            Validation.MarkInvalid(expression,new ValidationError(new ExceptionValidationRule(),expression,"Invalid hidden compose input",null));
            Directory.CreateDirectory(Path.Combine(owned,"source_01.png"));
            for(int i=0;i<3;i++)
            {
                await window.ExportAsync();await Dispatcher.Yield(DispatcherPriority.Background);
                if(window.State.Busy||!window.State.Status.StartsWith("已导出 4 张"))throw new Exception("Window split workflow failed.");
            }
            if(Directory.GetFiles(owned,"source_*.png",SearchOption.AllDirectories).Length!=12)throw new Exception("Repeated split file conflict handling failed.");
            ((StackPanel)window.FindName("ComposeSettings")).Children.Remove(hiddenInput);
            window.State.Xs[1]=63;window.State.Ys[1]=41;window.State.Refresh();
            ((Button)window.FindName("ComposeTab")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));window.State.Columns=4;window.State.Rows=3;
            ((Button)window.FindName("SplitTab")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(window.State.Columns!=2||window.State.Rows!=2||window.State.Xs[1]!=63||window.State.Ys[1]!=41)throw new Exception("Actual page switching lost split layout or manual cuts.");
            ColorPickerChecks.Run(directory);
            if(((TextBlock)window.FindName("VersionLabel")).Text!="v1.0.8"||((TextBlock)window.FindName("BrandLabel")).Text!="Image Grid"||window.Title!="Image Grid")throw new Exception("Product name/version was not updated.");
            var root=(FrameworkElement)window.Content;root.Measure(new Size(1080,720));root.Arrange(new Rect(0,0,1080,720));root.UpdateLayout();
            var themeBox=(ComboBox)window.FindName("ThemeCombo");var version=(TextBlock)window.FindName("VersionLabel");
            if(themeBox.TranslatePoint(new Point(),root).Y<root.ActualHeight*0.7||version.TranslatePoint(new Point(),root).Y<themeBox.TranslatePoint(new Point(),root).Y+themeBox.ActualHeight)throw new Exception("Appearance selector is not above the bottom-left version label.");
            var formatPanel=(StackPanel)window.FindName("FormatOptions");
            if(!formatPanel.Children.OfType<TextBlock>().Any(t=>t.Text=="JPG不支持透明通道；透明区域按画布背景色合成。"))throw new Exception("Requested JPEG description differs.");
            var formatBox=formatPanel.Children.OfType<ComboBox>().Single();
            if(((ComboBoxItem)formatBox.Items[1]).Content.ToString()!="JPG · 最高质量 100")throw new Exception("JPEG option wording differs.");
            UiPolishChecks.Run(window,directory);
            await ImportWorkflowChecks.RunAsync(window,source,owned);
            await ImportWorkflowChecks.CheckPickerAsync(window,source);
            File.WriteAllText(Path.Combine(directory,"window-export-check.json"),JsonSerializer.Serialize(new{passed=true,autoFillCentered=true,gapPreviewAndExport=true,overCapacityPreviewRefresh=true,overCapacityExportBlocked=true,composeRuns=3,splitRuns=3,saveDialogCancel=true,folderDialogCancel=true,splitConflictsPreserved=true,directoryConflictsPreserved=true,realWindowAndProgress=true,greenTheme=true,subtitlesRemoved=true,thumbnailDeleteButton=true,invalidSettingsRejected=true,dialogFailureRecovered=true,workerFailureRecovered=true,pixelViewRendered=true,systemDialogs="Selections and failures supplied by test; export event workflow unchanged"},new JsonSerializerOptions{WriteIndented=true}));
        }
        finally {window.Close();ThemeManager.SetMode(originalTheme);}
    }
    public static void Run(string directory)
    {
        Directory.CreateDirectory(directory);
        string owned=Path.Combine(directory,"probe-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(owned);
        var files=new string[4];
        for(int i=0;i<4;i++)
        {
            files[i]=Path.Combine(owned,$"source-{i}.png");using var b=new SKBitmap(1920,1080);b.Erase(new SKColor((byte)(30+i*40),60,160));using var data=b.Encode(SKEncodedImageFormat.Png,100);using var stream=File.Create(files[i]);data.SaveTo(stream);
        }
        var pictures=files.Select(ImageService.Inspect).ToArray();
        var layout=GridLayout.Calculate(pictures,2,2,16,9,3);
        string composed=Path.Combine(owned,"grid.png");ImageService.Compose(pictures,layout,true,false,SKColors.White,SKColors.White,composed,null,default);
        using(var image=ImageService.Decode(composed))if(image.Width!=3843||image.Height!=2163||image.GetPixel(1920,0)!=SKColors.White)throw new Exception("Composition verification failed.");
        var split=ImageService.Split(pictures[0],[0,960,1920],[0,540,1080],0,owned,null,default);
        foreach(string f in split) {using var b=ImageService.Decode(f);if(b.Width!=960||b.Height!=540)throw new Exception("Split verification failed.");}
        foreach(var format in new[]{SKEncodedImageFormat.Jpeg,SKEncodedImageFormat.Webp})
        {
            string file=Path.Combine(owned,"decode."+format.ToString().ToLowerInvariant());using var b=new SKBitmap(40,30);b.Erase(SKColors.Coral);using(var data=b.Encode(format,95))using(var stream=File.Create(file))data.SaveTo(stream);
            var entry=MainWindow.Load(file);if(entry.Thumbnail.PixelWidth!=40)throw new Exception("Thumbnail decoder verification failed.");
        }
        File.WriteAllText(Path.Combine(directory,"release-check.json"),JsonSerializer.Serialize(new{passed=true,framework=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,architecture=System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),composition="3843x2163",splitCount=split.Length,decoders=new[]{"PNG","JPEG","WebP"},fixtures=owned},new JsonSerializerOptions{WriteIndented=true}));
    }
}
