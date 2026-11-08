using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SkiaSharp;
using StoryboardGrid.Core;

namespace StoryboardGrid;
public partial class MainWindow : Window
{
    readonly Workspace model=new();
    CancellationTokenSource? cancellation;
    string? outputFolder;
    string lineSignature="";
    internal Workspace State=>model;
    internal Func<SaveFileDialog,bool?>? SaveDialogOverride {get;set;}
    internal Func<OpenFolderDialog,bool?>? FolderDialogOverride {get;set;}
    internal Action<string>? ErrorDialogOverride {get;set;}
    internal System.Func<bool>? ClearConfirmationOverride {get;set;}
    internal Func<OpenFileDialog,bool?>? OpenDialogOverride {get;set;}
    public MainWindow()
    {
        if(Application.Current!=null)ThemeManager.Initialize(Application.Current);
        InitializeComponent(); DataContext=model; Canvas.Model=model;
        var workArea=SystemParameters.WorkArea;Width=System.Math.Min(1360,workArea.Width);Height=System.Math.Min(1000,workArea.Height);
        ThemeManager.Register(this);ThemeCombo.SelectedIndex=ThemeManager.Mode;
        ThemeManager.Changed+=ThemeUpdated;
        Canvas.ZoomChanged+=scale=>ZoomLabel.Text=scale>0?$"{scale*100:0}%":"—";
        Closed+=(_,_)=>{ThemeManager.Changed-=ThemeUpdated;Canvas.ReleasePreviews();};
        model.Changed+=()=>{ Canvas.PrunePreviews(); Canvas.InvalidateVisual(); SizeSettings.Visibility=model.Mode==1?Visibility.Visible:Visibility.Collapsed; ModeNote.Text=model.Mode==0?"保留解码后的像素和尺寸；自动计算统一单格。透明像素保留，不烘焙为背景色。":model.Mode==2?"自动计算统一单格；小图等比放大、居中填满，比例不同时裁切边缘，不拉伸。":"此模式会等比重采样；Fill 会居中裁切，不属于原像素保留。"; UpdateLines(); };
        model.Images.CollectionChanged+=(_,_)=>BuildThumbnails();
        Canvas.FilesDropped+=async(paths,index)=>await ImportAsync(paths,index);
        Canvas.ReplaceRequested+=index=>ChooseFiles(index);
        Canvas.ImportRequested+=()=>ChooseFiles();
        model.Refresh();
        Loaded+=async(_,_)=> { var args=Environment.GetCommandLineArgs().Skip(1).Where(File.Exists).ToArray();if(args.Length>0)await ImportAsync(args); };
    }
    void ShowCompose(object s,RoutedEventArgs e)=>Page(false);
    void ShowSplit(object s,RoutedEventArgs e)=>Page(true);
    void Page(bool split)
    {
        if(model.Busy)return;
        model.SplitMode=split; PageTitle.Text=split?"宫格拆分":"宫格合成";
        ComposeSettings.Visibility=split?Visibility.Collapsed:Visibility.Visible;SplitSettings.Visibility=split?Visibility.Visible:Visibility.Collapsed;
        ThumbScroll.Visibility=split?Visibility.Collapsed:Visibility.Visible;SplitHelp.Visibility=split?Visibility.Visible:Visibility.Collapsed;
        ClearButton.Visibility=split?Visibility.Collapsed:Visibility.Visible;
        ExportButton.Content=split?"导出全部图片  ↗":"导出宫格图  ↗";
        ComposeTab.SetResourceReference(Control.BackgroundProperty,split?"Card":"AccentSoft");SplitTab.SetResourceReference(Control.BackgroundProperty,split?"AccentSoft":"Card");
        ComposeTab.SetResourceReference(Control.ForegroundProperty,split?"Text":"AccentText");SplitTab.SetResourceReference(Control.ForegroundProperty,split?"AccentText":"Text");
        FormatOptions.Visibility=split?Visibility.Collapsed:Visibility.Visible;
        Canvas.Fit();model.Refresh();
    }
    void ImportClick(object s,RoutedEventArgs e)=>ChooseFiles();
    void ThemeChanged(object sender,SelectionChangedEventArgs e){if(ThemeCombo?.SelectedIndex>=0){ThemeManager.SetMode(ThemeCombo.SelectedIndex);Canvas?.InvalidateVisual();}}
    void ThemeUpdated()=>Canvas.InvalidateVisual();
    async void ChooseFiles(int index=-1)=>await ChooseFilesAsync(index);
    internal async Task ChooseFilesAsync(int index=-1)
    {
        if(model.Busy)return;
        try
        {
            var dialog=new OpenFileDialog {Title=model.SplitMode?"选择宫格图片":"选择图片",Filter="图片|*.png;*.jpg;*.jpeg;*.webp;*.bmp",Multiselect=!model.SplitMode&&index<0};
            if((OpenDialogOverride is {} select?select(dialog):dialog.ShowDialog(this))==true)await ImportAsync(dialog.FileNames,index);
        }
        catch(Exception error){ShowError(error);}
    }
    internal async Task ImportAsync(string[] paths,int index=-1)
    {
        if(model.Busy)return;
        if(!model.SplitMode&&paths.Length>1)index=-1;
        if((model.SplitMode||index>=0)&&paths.Length!=1) {model.Status="请拖入一张图片替换此格；批量追加请使用「导入图片」或拖入窗口顶部。";return;}
        await Run(async ct=>
        {
            var errors=new List<string>();int count=0;
            foreach(string path in paths)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if(!new[]{".png",".jpg",".jpeg",".bmp",".webp"}.Contains(Path.GetExtension(path).ToLowerInvariant()))throw new IOException("仅支持 PNG / JPEG / WebP / BMP。");
                    var entry=await Task.Run(()=>Load(path),ct);ct.ThrowIfCancellationRequested();
                    if(model.SplitMode) {model.Source=entry;model.ResetCuts();Canvas.Fit();model.Refresh();}
                    else if(index>=0)model.Replace(index,entry);else model.Images.Add(entry);
                    count++; Progress.Value=(double)count/paths.Length;
                }
                catch(Exception ex) when(ex is not OperationCanceledException) {errors.Add($"{Path.GetFileName(path)}：{ex.Message}");}
            }
            model.Status=$"已{(index>=0?"替换":"导入")} {count} 张图片"+(errors.Count>0?$"；{errors.Count} 张失败":"");
            if(errors.Count>0){string message=string.Join("\n",errors);if(ErrorDialogOverride is {} show)show(message);else PromptWindow.Show(this,"部分图片无法导入",message);}
        },"正在读取图片与生成缩略图…");
    }
    internal static ImageEntry Load(string path, int maxEdge=480)
    {
        var p=ImageService.Inspect(path);using var thumbnail=ImageService.Thumbnail(path,maxEdge);
        var bitmap=BitmapSource.Create(thumbnail.Width,thumbnail.Height,96,96,PixelFormats.Pbgra32,null,thumbnail.GetPixels(),thumbnail.RowBytes*thumbnail.Height,thumbnail.RowBytes);bitmap.Freeze();return new(p,bitmap);
    }
    void WindowDrop(object s,DragEventArgs e)
    {
        if(e.Handled||!e.Data.GetDataPresent(DataFormats.FileDrop))return;
        _=ImportAsync((string[])e.Data.GetData(DataFormats.FileDrop));e.Handled=true;
    }
    void BuildThumbnails()
    {
        Thumbnails.Children.Clear();
        for(int i=0;i<model.Images.Count;i++)
        {
            int index=i;var entry=model.Images[i];var stack=new StackPanel {Width=132,Margin=new Thickness(0,0,10,0)};
            var image=new System.Windows.Controls.Image {Source=entry?.Thumbnail,Height=62,Stretch=Stretch.Uniform};
            var tile=new Grid();tile.Children.Add(image);
            var delete=new Button {Content="×",Width=22,Height=22,Padding=new Thickness(0),Margin=new Thickness(0,3,3,0),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,FontSize=17,ToolTip="删除此图片",Background=Brushes.White};
            delete.SetBinding(IsEnabledProperty,new System.Windows.Data.Binding(nameof(Workspace.Idle)){Source=model});
            delete.Click+=(_,e)=>{DeleteImage(index);e.Handled=true;};tile.Children.Add(delete);
            var border=new Border {Child=tile,Background=new SolidColorBrush(Color.FromRgb(242,244,248)),CornerRadius=new CornerRadius(5),AllowDrop=true,ToolTip="拖动排序 · 双击替换 · 点击 × 删除"};
            border.SetResourceReference(Border.BackgroundProperty,"ThumbnailBackground");delete.SetResourceReference(Control.BackgroundProperty,"Card");
            Point down=default;
            border.MouseLeftButtonDown+=(_,e)=>{down=e.GetPosition(border);if(e.ClickCount==2)ChooseFiles(index);};
            border.MouseMove+=(_,e)=>{if(!model.Busy&&!delete.IsMouseOver&&e.LeftButton==MouseButtonState.Pressed&&(e.GetPosition(border)-down).Length>6)DragDrop.DoDragDrop(border,new DataObject("StoryboardIndex",index),DragDropEffects.Move);};
            border.DragOver+=(_,e)=>{border.BorderBrush=Brushes.SeaGreen;border.BorderThickness=new Thickness(3,0,0,0);e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.Move;e.Handled=true;};
            border.DragLeave+=(_,_)=>border.BorderThickness=new Thickness(0);
            border.Drop+=async(_,e)=>{border.BorderThickness=new Thickness(0);if(!model.Busy){if(e.Data.GetData("StoryboardIndex") is int from)model.Move(from,index);else if(e.Data.GetDataPresent(DataFormats.FileDrop))await ImportAsync((string[])e.Data.GetData(DataFormats.FileDrop),index);}e.Handled=true;};
            var menu=new ContextMenu();var replace=new MenuItem {Header="替换图片"};replace.Click+=(_,_)=>ChooseFiles(index);var remove=new MenuItem{Header="删除图片"};remove.Click+=(_,_)=>DeleteImage(index);menu.Items.Add(replace);menu.Items.Add(remove);border.ContextMenu=menu;
            stack.Children.Add(border);stack.Children.Add(new TextBlock {Text=$"{i+1:00}  {entry?.Name??"空格"}",FontSize=10,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0,5,0,0),ToolTip=entry?.Picture.Path});stack.Children.Add(new TextBlock{Text=entry?.Dimensions??"双击填入图片",FontSize=9,Foreground=Brushes.Gray});Thumbnails.Children.Add(stack);
        }
    }
    void DeleteImage(int index) {if(!model.Busy&&index>=0&&index<model.Images.Count){model.Images.RemoveAt(index);model.Status="图片已删除";}}
    void ClearClick(object s,RoutedEventArgs e)=>ClearImages();
    internal void ClearImages() {if(!model.Busy&&model.Images.Count>0&&(ClearConfirmationOverride?.Invoke()??PromptWindow.Show(this,"清空图片","将移除全部已导入图片。\n原始文件不会被删除。","清空图片",true))){model.Images.Clear();model.Status="已清空图片";}}
    void FitClick(object s,RoutedEventArgs e)=>Canvas.Fit();
    void PixelViewClick(object s,RoutedEventArgs e)=>Canvas.PixelView();
    void Preset(object s,RoutedEventArgs e) {int n=int.Parse((string)((Button)s).Tag);model.Columns=n;model.Rows=n;Canvas.Fit();}
    void RatioChanged(object s,SelectionChangedEventArgs e)
    {
        if(CustomRatio==null)return;CustomRatio.Visibility=RatioCombo.SelectedIndex==5?Visibility.Visible:Visibility.Collapsed;
        var ratios=new[]{(16,9),(9,16),(1,1),(4,3),(3,2)};if(RatioCombo.SelectedIndex is >=0 and <5){var(w,h)=ratios[RatioCombo.SelectedIndex];model.RatioW=w;model.RatioH=h;}
    }
    void SizePreset(object s,SelectionChangedEventArgs e) {var sizes=new[]{(1920,1080),(1280,720),(1080,1920)};int i=((ComboBox)s).SelectedIndex;if(i is >=0 and <3){model.TargetW=sizes[i].Item1;model.TargetH=sizes[i].Item2;}}
    void PickColor(object s,RoutedEventArgs e)
    {
        bool gap=(string)((Button)s).Tag=="gap";
        ColorMath.TryHex(gap?model.GapColor:model.Background,out var initial);
        var dialog=new ColorPickerWindow(initial){Owner=this};
        if(dialog.ShowDialog()==true){string hex=ColorMath.Hex(dialog.SelectedColor);if(gap)model.GapColor=hex;else model.Background=hex;}
    }
    void ResetCuts(object s,RoutedEventArgs e) {model.ResetCuts();model.Refresh();model.Status="已恢复均匀等分";}
    void UpdateLines()
    {
        string signature=$"{model.Xs.Length}/{model.Ys.Length}";
        if(LineCombo==null)return;
        if(signature!=lineSignature || LineCombo.Items.Count==0)
        {
            lineSignature=signature;LineCombo.Items.Clear();for(int i=1;i<model.Xs.Length-1;i++)LineCombo.Items.Add($"纵线 {i}");for(int i=1;i<model.Ys.Length-1;i++)LineCombo.Items.Add($"横线 {i}");if(LineCombo.Items.Count>0)LineCombo.SelectedIndex=0;
        }
        if(!LinePosition.IsKeyboardFocusWithin)LineSelected(this,null!);
    }
    void LineSelected(object s,SelectionChangedEventArgs e)
    {
        if(LinePosition==null||LineCombo.SelectedIndex<0)return;int i=LineCombo.SelectedIndex;int n=model.Xs.Length-2;
        LinePosition.Text=(i<n?model.Xs[i+1]:model.Ys[i-n+1]).ToString();
    }
    void ApplyLine(object s,RoutedEventArgs e)
    {
        int i=LineCombo.SelectedIndex;if(i<0||!int.TryParse(LinePosition.Text,out int value)){model.Status="请输入有效像素坐标";return;}
        int n=model.Xs.Length-2;var a=i<n?model.Xs:model.Ys;int at=i<n?i+1:i-n+1;
        if(value<=a[at-1]+2*model.Inset||value>=a[at+1]-2*model.Inset){model.Status="坐标超出相邻分界线，或无法容纳当前内缩值";return;}a[at]=value;model.Refresh();
    }
    async void ExportClick(object s,RoutedEventArgs e) => await ExportAsync();
    internal async Task ExportAsync()
    {
        if(model.Busy)return;
        try{await ExportCoreAsync();}
        catch(Exception ex){ShowError(ex);}
    }
    async Task ExportCoreAsync()
    {
        // Reject text currently showing a WPF conversion error instead of exporting stale values.
        if(HasValidationError(Settings)){model.Status="请先修正参数中的无效数字。";return;}
        model.Refresh();if(model.Error!=null){model.Status=model.Error;return;}
        if(model.SplitMode)
        {
            if(model.Source==null){model.Status="请先导入宫格图片";return;}
            var dialog=new OpenFolderDialog {Title="选择图片输出目录",InitialDirectory=outputFolder??""};if((FolderDialogOverride is {} selectFolder?selectFolder(dialog):dialog.ShowDialog(this))!=true)return;
            string folder=dialog.FolderName;var source=model.Source.Picture;var xs=(int[])model.Xs.Clone();var ys=(int[])model.Ys.Clone();int inset=model.Inset;
            int count=(xs.Length-1)*(ys.Length-1);
            if(HasSplitConflict(folder,source.Path,count))folder=Path.Combine(folder,$"{Path.GetFileNameWithoutExtension(source.Path)}_图片_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..4]}");
            await Run(async ct=>{Directory.CreateDirectory(folder);var files=await Task.Run(()=>ImageService.Split(source,xs,ys,inset,folder,new ExportProgress(Dispatcher,v=>Progress.Value=v),ct),ct);Exported(folder,$"已导出 {files.Length} 张图片");},"正在按像素区域拆分导出…");
        }
        else
        {
            if(!model.Images.Any(x=>x!=null)){model.Status="请先导入图片";return;}
            var layout=model.Layout!;
            bool jpeg=model.ExportFormat==1;
            var format=jpeg?SKEncodedImageFormat.Jpeg:SKEncodedImageFormat.Png;
            var dialog=new SaveFileDialog {Title=$"导出 {layout.Width}×{layout.Height} px · {(jpeg?"JPG 100 · 最高质量":model.Mode==0?"PNG · 原像素":"PNG · 重采样 / 可能裁切")}",FileName=jpeg?"Image_Grid.jpg":"Image_Grid.png",Filter=jpeg?"JPG 图片（最高质量）|*.jpg;*.jpeg":"PNG 图片（无损）|*.png",InitialDirectory=outputFolder??"",DefaultExt=jpeg?".jpg":".png"};if((SaveDialogOverride is {} selectFile?selectFile(dialog):dialog.ShowDialog(this))!=true)return;
            if(model.Images.Any(p=>p!=null&&string.Equals(Path.GetFullPath(p.Picture.Path),Path.GetFullPath(dialog.FileName),StringComparison.OrdinalIgnoreCase))){model.Status="不能覆盖源图片，请选择其他文件名。";return;}
            var pictures=model.Images.Select(x=>x?.Picture).ToArray();bool original=model.Mode==0,fill=model.FillCells;
            var bg=SKColor.Parse(model.Background);var gap=SKColor.Parse(model.GapColor);
            await Run(async ct=>{await Task.Run(()=>ImageService.Compose(pictures,layout,original,fill,bg,gap,dialog.FileName,new ExportProgress(Dispatcher,v=>Progress.Value=v),ct,format),ct);Exported(Path.GetDirectoryName(dialog.FileName)!,$"已导出 {layout.Width}×{layout.Height} px {(jpeg?"JPG":"PNG")} 宫格图");},$"正在从原始图片生成 {(jpeg?"JPG":"PNG")}…");
        }
    }
    internal static bool HasSplitConflict(string folder,string source,int count)=>Enumerable.Range(1,count).Any(i=>
    {
        string path=Path.Combine(folder,$"{Path.GetFileNameWithoutExtension(source)}_{i:00}.png");return File.Exists(path)||Directory.Exists(path);
    });
    static bool HasValidationError(DependencyObject node)
    {
        if(node is UIElement element && element.Visibility!=Visibility.Visible)return false;
        if(Validation.GetHasError(node))return true;for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)if(HasValidationError(VisualTreeHelper.GetChild(node,i)))return true;return false;
    }
    void Exported(string folder,string message){outputFolder=folder;OpenFolderButton.Visibility=Visibility.Visible;model.Status=$"{message} · {folder}";}
    async Task Run(Func<CancellationToken,Task> work,string message)
    {
        if(model.Busy)return;model.Busy=true;model.Status=message;cancellation=new();Progress.Value=0;Progress.Visibility=CancelButton.Visibility=Visibility.Visible;
        try {await work(cancellation.Token);}
        catch(OperationCanceledException){model.Status="已取消处理；未完成的导出不会保留。";}
        catch(Exception ex){ShowError(ex);}
        finally {cancellation.Dispose();cancellation=null;model.Busy=false;Progress.Visibility=CancelButton.Visibility=Visibility.Collapsed;}
    }
    void ShowError(Exception error)
    {
        model.Status="处理失败："+error.Message;
        if(ErrorDialogOverride is {} show)show(error.Message);else PromptWindow.Show(this,"处理失败",error.Message);
    }
    void CancelClick(object s,RoutedEventArgs e){cancellation?.Cancel();model.Status="正在取消，请稍候…";}
    void OpenFolder(object s,RoutedEventArgs e){if(outputFolder!=null)try{Process.Start(new ProcessStartInfo {FileName=outputFolder,UseShellExecute=true});}catch(Exception ex){model.Status=ex.Message;}}
    void WindowClosing(object? s,CancelEventArgs e){if(model.Busy){cancellation?.Cancel();model.Status="正在取消处理，完成后可关闭窗口。";e.Cancel=true;}}
}
