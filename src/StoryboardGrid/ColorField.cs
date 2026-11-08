using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace StoryboardGrid;
public sealed class ColorField : FrameworkElement
{
    public bool IsHueStrip {get;set;}
    double hue,saturation,value;
    public event Action<double,double>? Picked;
    public ColorField()
    {
        Focusable=true;Cursor=Cursors.Cross;
        MouseLeftButtonDown+=(_,e)=>{Focus();CaptureMouse();SelectAt(e.GetPosition(this));e.Handled=true;};
        MouseMove+=(_,e)=>{if(IsMouseCaptured&&e.LeftButton==MouseButtonState.Pressed)SelectAt(e.GetPosition(this));};
        MouseLeftButtonUp+=(_,_)=>ReleaseMouseCapture();
        KeyDown+=(_,e)=>
        {
            double step=Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)?0.1:0.01;
            if(e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down))return;
            if(IsHueStrip)Picked?.Invoke(0,Math.Clamp(hue/360+(e.Key is Key.Down or Key.Right?step:-step),0,1));
            else Picked?.Invoke(Math.Clamp(saturation+(e.Key==Key.Right?step:e.Key==Key.Left?-step:0),0,1),Math.Clamp(1-value+(e.Key==Key.Down?step:e.Key==Key.Up?-step:0),0,1));
            e.Handled=true;
        };
    }
    public void Set(double h,double s,double v){hue=h;saturation=s;value=v;InvalidateVisual();}
    internal void SelectAt(Point p)=>Picked?.Invoke(Math.Clamp(p.X/Math.Max(1,ActualWidth),0,1),Math.Clamp(p.Y/Math.Max(1,ActualHeight),0,1));
    protected override void OnRender(DrawingContext dc)
    {
        var rect=new Rect(RenderSize);
        if(IsHueStrip)
        {
            var rainbow=new LinearGradientBrush{StartPoint=new(0,0),EndPoint=new(0,1)};
            for(int i=0;i<=6;i++)rainbow.GradientStops.Add(new(ColorMath.FromHsv(i*60,1,1),i/6d));
            dc.DrawRoundedRectangle(rainbow,null,rect,5,5);
            double y=Math.Clamp(hue/360*ActualHeight,2,Math.Max(2,ActualHeight-2));
            dc.DrawRoundedRectangle(null,new Pen(Brushes.Black,4),new Rect(-3,y-3,ActualWidth+6,6),2,2);
            dc.DrawRoundedRectangle(null,new Pen(Brushes.White,2),new Rect(-3,y-3,ActualWidth+6,6),2,2);
        }
        else
        {
            dc.PushClip(new RectangleGeometry(rect,6,6));
            dc.DrawRectangle(new SolidColorBrush(ColorMath.FromHsv(hue,1,1)),null,rect);
            dc.DrawRectangle(new LinearGradientBrush(Colors.White,Color.FromArgb(0,255,255,255),new Point(0,0),new Point(1,0)),null,rect);
            dc.DrawRectangle(new LinearGradientBrush(Color.FromArgb(0,0,0,0),Colors.Black,new Point(0,0),new Point(0,1)),null,rect);
            var point=new Point(saturation*ActualWidth,(1-value)*ActualHeight);
            dc.DrawEllipse(null,new Pen(Brushes.Black,3),point,6,6);dc.DrawEllipse(null,new Pen(Brushes.White,1.5),point,6,6);dc.Pop();
        }
        if(IsKeyboardFocused)dc.DrawRoundedRectangle(null,new Pen(new SolidColorBrush(Color.FromRgb(22,134,93)),2),rect,6,6);
    }
}
