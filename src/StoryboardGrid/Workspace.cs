using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StoryboardGrid.Core;

namespace StoryboardGrid;
public record ImageEntry(Picture Picture, BitmapSource Thumbnail)
{
    public string Name => System.IO.Path.GetFileName(Picture.Path);
    public string Dimensions => $"{Picture.Width} × {Picture.Height}";
}
public class Workspace : INotifyPropertyChanged
{
    public ObservableCollection<ImageEntry?> Images { get; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Changed;
    int composeColumns=2, composeRows=2, splitColumns=2, splitRows=2, gap=3, ratioW=16, ratioH=9, targetW=1920, targetH=1080, mode=2, fit, inset;
    bool split, busy;
    string background="#FFFFFF", gapColor="#FFFFFF", status="所有图片仅在本机处理";
    public int Columns { get=>SplitMode?splitColumns:composeColumns; set { if(Columns==value)return;if(SplitMode){splitColumns=value;ResetAxis(true);}else composeColumns=value;Refresh(); } }
    public int Rows { get=>SplitMode?splitRows:composeRows; set { if(Rows==value)return;if(SplitMode){splitRows=value;ResetAxis(false);}else composeRows=value;Refresh(); } }
    public int Gap { get=>gap; set {gap=value; Refresh();} }
    public int RatioW { get=>ratioW; set {ratioW=value; Refresh();} }
    public int RatioH { get=>ratioH; set {ratioH=value; Refresh();} }
    public int TargetW { get=>targetW; set {targetW=value; Refresh();} }
    public int TargetH { get=>targetH; set {targetH=value; Refresh();} }
    public int Mode { get=>mode; set {mode=value; Refresh();} }
    public bool FillCells => Mode==2 || Mode==1 && Fit==1;
    public int Fit { get=>fit; set {fit=value; Refresh();} }
    int exportFormat;
    public int ExportFormat {get=>exportFormat;set{exportFormat=value;Refresh();}}
    public int Inset { get=>inset; set {inset=value; Refresh();} }
    public string Background { get=>background; set {background=value; Refresh();} }
    public string GapColor { get=>gapColor; set {gapColor=value; Refresh();} }
    public string Status { get=>status; set {status=value; Notify();} }
    public bool Busy { get=>busy; set {busy=value; Notify(); Notify(nameof(Idle));} }
    public bool Idle => !Busy;
    public bool SplitMode { get=>split; set {split=value; Refresh();} }
    public ImageEntry? Source {get;set;}
    public int[] Xs {get;set;} = [0,1];
    public int[] Ys {get;set;} = [0,1];
    public GridLayout? Layout {get;private set;}
    public string Summary {get;private set;} = "";
    public string? Error {get;private set;}
    public Workspace() { Images.CollectionChanged += (_,_)=>Refresh(); Refresh(); }
    public void Notify([CallerMemberName]string? property=null)=>PropertyChanged?.Invoke(this,new(property));
    public void ResetCuts()
    {
        ResetAxis(true);ResetAxis(false);
    }
    void ResetAxis(bool vertical)
    {
        if(Source==null)return;
        try {if(vertical)Xs=Cuts.Equal(Source.Picture.Width,splitColumns);else Ys=Cuts.Equal(Source.Picture.Height,splitRows);}
        catch(ArgumentException) { }
    }
    public void Refresh()
    {
        Error=null;
        try
        {
            if (SplitMode)
            {
                if(Columns is <1 or >10 || Rows is <1 or >10) throw new ArgumentException("行列须为 1–10。");
                if(Source != null)
                {
                    if(Xs.Length!=Columns+1 || Ys.Length!=Rows+1) throw new ArgumentException("行列不能超过图像像素尺寸。");
                    var regions=Cuts.Regions(Xs,Ys,Inset);
                    Summary=$"{Source.Name}  ·  {Source.Dimensions} px  ·  {regions.Length} 张图片  ·  每边内缩 {Inset}px";
                }
                else Summary="导入一张宫格图片开始拆分";
            }
            else
            {
                _=ParseColor(Background); _=ParseColor(GapColor);
                if(Mode==1 && (TargetW<1 || TargetH<1)) throw new ArgumentException("单格宽高必须大于 0。");
                Layout=GridLayout.Calculate(Images.Take(Columns*Rows).Select(x=>x?.Picture),Columns,Rows,RatioW,RatioH,Gap,Mode==1?TargetW:0,Mode==1?TargetH:0);
                if(Images.Count>Columns*Rows)throw new ArgumentException($"共 {Images.Count} 张图片，宫格仅容纳 {Columns*Rows} 张，请增加行列或删除图片。");
                Summary=$"{Images.Count(x=>x!=null)} 张图片  ·  {Columns} 列 × {Rows} 行  ·  单格 {Layout.CellWidth}×{Layout.CellHeight}  ·  输出 {Layout.Width}×{Layout.Height} px  ·  {(Mode==0?"原像素 / 不缩放":Mode==2?"自动填满 / 居中裁切":Fit==0?"Fit / 等比重采样":"Fill / 居中裁切")}";
            }
        }
        catch(Exception e) when(e is ArgumentException or OverflowException or FormatException) { Error=e.Message; Summary=e.Message; }
        Notify(""); Changed?.Invoke();
    }
    public static Color ParseColor(string s)
    {
        if(s.Length!=7 || s[0]!='#') throw new FormatException("颜色请输入 #RRGGBB，例如 #FFFFFF。");
        return (Color)ColorConverter.ConvertFromString(s);
    }
    public void Move(int from,int to)
    {
        if(from<0 || from>=Images.Count || to<0) return;
        Images.Move(from,Math.Min(to,Images.Count-1));
    }
    public void Replace(int index,ImageEntry entry)
    {
        while(Images.Count<=index) Images.Add(null);
        Images[index]=entry;
    }
}
