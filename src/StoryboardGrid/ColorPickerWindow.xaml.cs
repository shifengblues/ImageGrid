using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace StoryboardGrid;
public partial class ColorPickerWindow : Window
{
    readonly Color original;
    bool ready,updating;
    double hue,saturation,value;
    public Color SelectedColor {get;private set;}
    public ColorPickerWindow(Color initial)
    {
        if(Application.Current!=null)ThemeManager.Initialize(Application.Current);
        original=initial;InitializeComponent();ThemeManager.Register(this);OldSwatch.Background=new SolidColorBrush(initial);ready=true;SetColor(initial);
        Field.Picked+=(s,y)=>{saturation=s;value=1-y;SetColor(ColorMath.FromHsv(hue,saturation,value),preserveHsv:true);};
        HueStrip.Picked+=(_,y)=>{hue=y*360;SetColor(ColorMath.FromHsv(hue,saturation,value),preserveHsv:true);};
    }
    void SetColor(Color color,TextBox? editing=null,bool preserveHsv=false)
    {
        updating=true;SelectedColor=color;if(!preserveHsv)(hue,saturation,value)=ColorMath.ToHsv(color);
        if(editing!=HexInput)HexInput.Text=ColorMath.Hex(color);
        if(editing!=RedInput)RedInput.Text=color.R.ToString();if(editing!=GreenInput)GreenInput.Text=color.G.ToString();if(editing!=BlueInput)BlueInput.Text=color.B.ToString();
        Field.Set(hue,saturation,value);HueStrip.Set(hue,saturation,value);NewSwatch.Background=new SolidColorBrush(color);
        HsvLabel.Text=$"H {hue:0}°    S {saturation*100:0}%    B {value*100:0}%";
        InputError.Text="";ApplyButton.IsEnabled=true;updating=false;
    }
    void HexChanged(object sender,TextChangedEventArgs e)
    {
        if(!ready||updating)return;
        if(ColorMath.TryHex(HexInput.Text,out var color))SetColor(color,HexInput);else Invalid("请输入 6 位 HEX 色值");
    }
    void RgbChanged(object sender,TextChangedEventArgs e)
    {
        if(!ready||updating)return;
        if(byte.TryParse(RedInput.Text,out byte r)&&byte.TryParse(GreenInput.Text,out byte g)&&byte.TryParse(BlueInput.Text,out byte b))SetColor(Color.FromRgb(r,g,b),(TextBox)sender);else Invalid("RGB 通道范围为 0–255");
    }
    void Invalid(string message){InputError.Text=message;ApplyButton.IsEnabled=false;}
    void Restore(object sender,RoutedEventArgs e)=>SetColor(original);
    void ApplyColor(object sender,RoutedEventArgs e){if(ApplyButton.IsEnabled)DialogResult=true;}
}
