using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace StoryboardGrid;
public class GridCanvas : FrameworkElement
{
    static readonly Brush Transparency=CreateTransparency();
    static Brush CreateTransparency()
    {
        var drawing=new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(Brushes.White,null,new RectangleGeometry(new Rect(0,0,16,16))));
        var gray=new SolidColorBrush(Color.FromRgb(225,228,234));
        drawing.Children.Add(new GeometryDrawing(gray,null,new RectangleGeometry(new Rect(0,0,8,8))));
        drawing.Children.Add(new GeometryDrawing(gray,null,new RectangleGeometry(new Rect(8,8,8,8))));
        var brush=new DrawingBrush(drawing){TileMode=TileMode.Tile,ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,16,16)};brush.Freeze();return brush;
    }
    public Workspace? Model {get;set;}
    readonly CanvasPreviewCache previews;
    public event Action<string[],int>? FilesDropped;
    public event Action<int>? ReplaceRequested;
    public event Action? ImportRequested;
    public event Action<int>? SelectionChanged;
    public event Action<double>? ZoomChanged;
    double lastPixelScale=-1;
    double zoom=1, panX,panY, scale=1, ox,oy, contentW=1,contentH=1;
    Point start, panStart;
    bool panning;
    bool batchDrop;
    int draggingLine=-1, selected=-1, dragIndex=-1, target=-1;
    bool vertical;
    int hoverLine=-1;
    bool hoverVertical;
    public GridCanvas()
    {
        previews=new CanvasPreviewCache(Dispatcher);previews.Changed+=InvalidateVisual;
        AllowDrop=true; Focusable=true;
        SizeChanged+=(_,_)=>InvalidateVisual();
        MouseWheel+=(_,e)=>{ var p=e.GetPosition(this); double old=zoom;double fit=CanvasGeometry.FitScale(ActualWidth,ActualHeight,contentW,contentH);zoom=Math.Clamp(zoom*(e.Delta>0?1.15:1/1.15),0.1,Math.Max(12,8/(fit*VisualTreeHelper.GetDpi(this).DpiScaleX))); panX=(panX+p.X-ActualWidth/2)*(zoom/old)-(p.X-ActualWidth/2); panY=(panY+p.Y-ActualHeight/2)*(zoom/old)-(p.Y-ActualHeight/2); previews.RetryVisible(); InvalidateVisual(); e.Handled=true; };
        MouseDown+=Down; MouseMove+=Move;
        MouseUp+=(_,_)=>{if(panning)previews.RetryVisible();panning=false; draggingLine=-1; ReleaseMouseCapture();InvalidateVisual();};
        LostMouseCapture+=(_,_)=>{panning=false;draggingLine=-1;};
        MouseLeave+=(_,_)=>{hoverLine=-1;if(Mouse.LeftButton==MouseButtonState.Released)selected=-1;InvalidateVisual();};
        DragOver+=Over; DragLeave+=(_,_)=>{target=-1;batchDrop=false;InvalidateVisual();}; Drop+=Dropped;
    }
    public void PrunePreviews() {if(Model!=null)previews.Prune(Model.Images.OfType<ImageEntry>().Concat(Model.Source==null?Array.Empty<ImageEntry>():new[]{Model.Source}));}
    public void Fit() {zoom=1;panX=panY=0;previews.RetryVisible();InvalidateVisual();}
    public void PixelView()
    {
        zoom=1/(CanvasGeometry.FitScale(ActualWidth,ActualHeight,contentW,contentH)*VisualTreeHelper.GetDpi(this).DpiScaleX);
        panX=panY=0;previews.RetryVisible();InvalidateVisual();
    }
    public void ReleasePreviews()=>previews.Dispose();
    Rect Screen(double x,double y,double w,double h)=>new(ox+x*scale,oy+y*scale,w*scale,h*scale);
    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(ThemeManager.Brush("CanvasBackground"),null,new Rect(RenderSize));
        if(Model==null)return;
        dc.PushClip(new RectangleGeometry(new Rect(RenderSize)));
        if(batchDrop){dc.DrawRoundedRectangle(null,new Pen(ThemeManager.Brush("Accent"),2),new Rect(8,8,System.Math.Max(1,ActualWidth-16),System.Math.Max(1,ActualHeight-16)),8,8);Badge(dc,"追加图片",new(16,16));}
        if(Model.SplitMode && Model.Source==null || !Model.SplitMode && Model.Images.Count==0)
        {
            if(lastPixelScale!=0){lastPixelScale=0;ZoomChanged?.Invoke(0);}
            var labels=new[]{CreateText("▧",42,"#9BB9AD"),CreateText(Model.SplitMode?"拖入一张宫格图，开始拆分":"把图片拖到这里",18,"#50695D"),CreateText("或点击导入图片",12,"#8992A3")};
            var positions=CanvasGeometry.CenteredStack(RenderSize,labels.Select(t=>new Size(t.WidthIncludingTrailingWhitespace,t.Height)).ToArray(),12);
            for(int i=0;i<labels.Length;i++)dc.DrawText(labels[i],positions[i].TopLeft);
            dc.Pop();return;
        }
        if(Model.SplitMode) {contentW=Model.Source!.Picture.Width;contentH=Model.Source.Picture.Height;}
        else if(Model.Layout is {} l) {contentW=l.Width; contentH=l.Height;} else {dc.Pop();return;}
        scale=CanvasGeometry.FitScale(ActualWidth,ActualHeight,contentW,contentH)*zoom;
        double pixelScale=scale*VisualTreeHelper.GetDpi(this).DpiScaleX;
        if(Math.Abs(pixelScale-lastPixelScale)>0.000001){lastPixelScale=pixelScale;ZoomChanged?.Invoke(pixelScale);}
        ox=(ActualWidth-contentW*scale)/2-panX; oy=(ActualHeight-contentH*scale)/2-panY;
        dc.DrawRectangle(Brushes.White,new Pen(new SolidColorBrush(Color.FromRgb(214,220,229)),1),Screen(0,0,contentW,contentH));
        if(Model.SplitMode)
        {
            dc.DrawRectangle(Transparency,null,Screen(0,0,contentW,contentH));
            dc.DrawImage(previews.Get(Model.Source!,Math.Max(contentW,contentH)*scale*VisualTreeHelper.GetDpi(this).PixelsPerDip),Screen(0,0,contentW,contentH));
            for(int r=0;r<Model.Ys.Length-1;r++)for(int c=0;c<Model.Xs.Length-1;c++)
            {
                int i=r*(Model.Xs.Length-1)+c;
                var rect=Screen(Model.Xs[c],Model.Ys[r],Model.Xs[c+1]-Model.Xs[c],Model.Ys[r+1]-Model.Ys[r]);
                Badge(dc,(i+1).ToString("00"),new(rect.X+8,rect.Y+8));
                if(Model.Inset>0 && rect.Width>2*Model.Inset*scale && rect.Height>2*Model.Inset*scale)
                    dc.DrawRectangle(null,new Pen(Brushes.White,1),Screen(Model.Xs[c]+Model.Inset,Model.Ys[r]+Model.Inset,Model.Xs[c+1]-Model.Xs[c]-2*Model.Inset,Model.Ys[r+1]-Model.Ys[r]-2*Model.Inset));
            }
            var pen=new Pen(new SolidColorBrush(Color.FromRgb(53,201,120)),2){DashStyle=new DashStyle(new double[]{4,3},0)};
            foreach(int x in Model.Xs.Skip(1).SkipLast(1))dc.DrawLine(pen,new(ox+x*scale,oy),new(ox+x*scale,oy+contentH*scale));
            foreach(int y in Model.Ys.Skip(1).SkipLast(1))dc.DrawLine(pen,new(ox,oy+y*scale),new(ox+contentW*scale,oy+y*scale));
            if(hoverLine>0)
            {
                var active=new Pen(new SolidColorBrush(Color.FromRgb(22,155,82)),3);
                if(hoverVertical && hoverLine<Model.Xs.Length-1)dc.DrawLine(active,new(ox+Model.Xs[hoverLine]*scale,oy),new(ox+Model.Xs[hoverLine]*scale,oy+contentH*scale));
                else if(!hoverVertical && hoverLine<Model.Ys.Length-1)dc.DrawLine(active,new(ox,oy+Model.Ys[hoverLine]*scale),new(ox+contentW*scale,oy+Model.Ys[hoverLine]*scale));
            }
        }
        else
        {
            var l=Model.Layout!;
            Brush bg=Brushes.White,gap=Brushes.White;
            try{bg=new SolidColorBrush(Workspace.ParseColor(Model.Background));gap=new SolidColorBrush(Workspace.ParseColor(Model.GapColor));}catch{}
            dc.DrawRectangle(gap,null,Screen(0,0,contentW,contentH));
            for(int i=0;i<l.Columns*l.Rows;i++)
            {
                int x=i%l.Columns*(l.CellWidth+l.Gap),y=i/l.Columns*(l.CellHeight+l.Gap);
                var rect=Screen(x,y,l.CellWidth,l.CellHeight); dc.DrawRectangle(bg,null,rect);
                if(i<Model.Images.Count && Model.Images[i] is {} entry)
                {
                    double s=Model.Mode==0?1:Model.FillCells?Math.Max((double)l.CellWidth/entry.Picture.Width,(double)l.CellHeight/entry.Picture.Height):Math.Min((double)l.CellWidth/entry.Picture.Width,(double)l.CellHeight/entry.Picture.Height);
                    double px=Model.Mode==0?(l.CellWidth-entry.Picture.Width)/2:(l.CellWidth-entry.Picture.Width*s)/2;
                    double py=Model.Mode==0?(l.CellHeight-entry.Picture.Height)/2:(l.CellHeight-entry.Picture.Height*s)/2;
                    dc.PushClip(new RectangleGeometry(rect)); dc.PushOpacity(i==dragIndex?0.45:1);
                    if(Model.Mode==0)dc.DrawRectangle(Transparency,null,Screen(x+px,y+py,entry.Picture.Width*s,entry.Picture.Height*s));
                    var imageRect=Screen(x+px,y+py,entry.Picture.Width*s,entry.Picture.Height*s);
                    var visible=Rect.Intersect(rect,new Rect(RenderSize));
                    dc.DrawImage(visible.IsEmpty?entry.Thumbnail:previews.Get(entry,Math.Max(imageRect.Width,imageRect.Height)*VisualTreeHelper.GetDpi(this).PixelsPerDip),imageRect);dc.Pop();dc.Pop();
                }
                else Text(dc,"空格",new(rect.X+rect.Width/2-14,rect.Y+rect.Height/2-8),11,"#A6ABB4");
                Badge(dc,(i+1).ToString("00"),new(rect.X+6,rect.Y+6));
                if(i==selected && rect.Width>80) {dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(220,255,255,255)),null,new(rect.Right-52,rect.Top+6,46,24),5,5);Text(dc,"替换",new(rect.Right-44,rect.Top+10),11,"#126745");}
                if(i==target || i==selected)dc.DrawRectangle(null,new Pen(new SolidColorBrush(Color.FromRgb(22,134,93)),i==target?3:1),rect);
                if(i==target) { dc.DrawLine(new Pen(Brushes.SeaGreen,4),rect.TopLeft,rect.BottomLeft); Badge(dc,dragIndex>=0?"插入此处":"替换此格",new(rect.X+6,rect.Bottom-27)); }
            }
        }
        dc.Pop();
    }
    void Badge(DrawingContext dc,string text,Point p) {dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(180,37,40,45)),null,new Rect(p,new Size(Math.Max(27,text.Length*12+8),22)),4,4);Text(dc,text,new(p.X+5,p.Y+3),11,"#FFFFFF");}
    FormattedText CreateText(string text,double size,string color)=>new(text,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),size,color=="#50695D"?ThemeManager.Brush("MutedText"):color is "#8992A3" or "#9BB9AD"?ThemeManager.Brush("FaintText"):(Brush)new BrushConverter().ConvertFromString(color)!,VisualTreeHelper.GetDpi(this).PixelsPerDip);
    void Text(DrawingContext dc,string text,Point point,double size,string color)=>dc.DrawText(CreateText(text,size,color),point);
    int Hit(Point p)
    {
        if(Model?.Layout is not {} l || scale<=0)return -1;
        double x=(p.X-ox)/scale,y=(p.Y-oy)/scale;
        return CanvasGeometry.CellAt(l,x,y);
    }
    (int index,bool vert) LineHit(Point p)
    {
        if(Model==null)return(-1,false);
        if(p.X<ox-8||p.X>ox+contentW*scale+8||p.Y<oy-8||p.Y>oy+contentH*scale+8)return(-1,false);
        for(int i=1;i<Model.Xs.Length-1;i++)if(Math.Abs(p.X-(ox+Model.Xs[i]*scale))<8)return(i,true);
        for(int i=1;i<Model.Ys.Length-1;i++)if(Math.Abs(p.Y-(oy+Model.Ys[i]*scale))<8)return(i,false);
        return(-1,false);
    }
    void Down(object s,MouseButtonEventArgs e)
    {
        Focus(); start=e.GetPosition(this); if(Model?.Busy!=false)return;
        if(e.ChangedButton is MouseButton.Middle or MouseButton.Right) {panning=true;panStart=new(panX,panY);CaptureMouse();return;}
        if(Model.SplitMode) {var hit=LineHit(start);draggingLine=hit.index;vertical=hit.vert;if(draggingLine>=0)CaptureMouse();return;}
        selected=Hit(start); SelectionChanged?.Invoke(selected);InvalidateVisual();
        if(selected>=0 && Model.Layout is {} l)
        {
            var rect=Screen(selected%l.Columns*(l.CellWidth+l.Gap),selected/l.Columns*(l.CellHeight+l.Gap),l.CellWidth,l.CellHeight);
            if(start.X>=rect.Right-52 && start.Y<=rect.Top+30) {ReplaceRequested?.Invoke(selected);return;}
            if(e.ClickCount==2)ReplaceRequested?.Invoke(selected);
        }
        else if(Model.Images.Count==0)ImportRequested?.Invoke();
    }
    void Move(object s,MouseEventArgs e)
    {
        var p=e.GetPosition(this); if(Model==null)return;
        if(panning) {panX=panStart.X-(p.X-start.X);panY=panStart.Y-(p.Y-start.Y);InvalidateVisual();return;}
        if(Model.Busy)return;
        if(draggingLine>=0)
        {
            int[] a=vertical?Model.Xs:Model.Ys;int min=a[draggingLine-1]+2*Model.Inset+1,max=a[draggingLine+1]-2*Model.Inset-1;
            if(min<=max)a[draggingLine]=Math.Clamp((int)Math.Round(((vertical?p.X:p.Y)-(vertical?ox:oy))/scale),min,max);
            Model.Refresh();return;
        }
        if(Model.SplitMode) {var h=LineHit(p);Cursor=h.index<0?Cursors.Arrow:h.vert?Cursors.SizeWE:Cursors.SizeNS;if(hoverLine!=h.index||hoverVertical!=h.vert){hoverLine=h.index;hoverVertical=h.vert;InvalidateVisual();}return;}
        Cursor=Cursors.Arrow;
        if(e.LeftButton==MouseButtonState.Pressed && selected>=0 && selected<Model.Images.Count && (p-start).Length>6)
        {
            dragIndex=selected;InvalidateVisual();try{DragDrop.DoDragDrop(this,new DataObject("StoryboardIndex",selected),DragDropEffects.Move);}finally{dragIndex=-1;target=-1;InvalidateVisual();}return;
        }
        int next=Hit(p);if(next!=selected){selected=next;InvalidateVisual();}
    }
    void Over(object s,DragEventArgs e)
    {
        if(Model?.Busy!=false){e.Effects=DragDropEffects.None;e.Handled=true;return;}
        batchDrop=!Model.SplitMode&&e.Data.GetDataPresent(DataFormats.FileDrop)&&e.Data.GetData(DataFormats.FileDrop) is string[] files&&files.Length>1;
        target=Model.SplitMode||batchDrop?-1:Hit(e.GetPosition(this));e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:e.Data.GetDataPresent("StoryboardIndex")?DragDropEffects.Move:DragDropEffects.None;InvalidateVisual();e.Handled=true;
    }
    void Dropped(object s,DragEventArgs e)
    {
        if(Model?.Busy!=false)return;
        int at=Hit(e.GetPosition(this));
        if(e.Data.GetDataPresent(DataFormats.FileDrop))FilesDropped?.Invoke((string[])e.Data.GetData(DataFormats.FileDrop),Model.SplitMode?-1:at);
        else if(e.Data.GetData("StoryboardIndex") is int from && at>=0)Model.Move(from,at);
        target=-1;batchDrop=false;InvalidateVisual();e.Handled=true;
    }
}
