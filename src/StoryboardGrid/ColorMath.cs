using System;
using System.Globalization;
using System.Windows.Media;

namespace StoryboardGrid;
internal static class ColorMath
{
    internal static Color FromHsv(double hue,double saturation,double value)
    {
        double h=((hue%360)+360)%360/60,s=Math.Clamp(saturation,0,1),v=Math.Clamp(value,0,1);
        double c=v*s,x=c*(1-Math.Abs(h%2-1)),m=v-c;
        var(r,g,b)=h switch{<1=>(c,x,0d),<2=>(x,c,0d),<3=>(0d,c,x),<4=>(0d,x,c),<5=>(x,0d,c),_=>(c,0d,x)};
        return Color.FromRgb((byte)Math.Round((r+m)*255),(byte)Math.Round((g+m)*255),(byte)Math.Round((b+m)*255));
    }
    internal static (double Hue,double Saturation,double Value) ToHsv(Color color)
    {
        double r=color.R/255d,g=color.G/255d,b=color.B/255d,max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b)),d=max-min;
        double hue=d==0?0:max==r?60*((g-b)/d%6):max==g?60*((b-r)/d+2):60*((r-g)/d+4);
        return ((hue+360)%360,max==0?0:d/max,max);
    }
    internal static string Hex(Color color)=>$"#{color.R:X2}{color.G:X2}{color.B:X2}";
    internal static bool TryHex(string text,out Color color)
    {
        string hex=text.Trim();if(hex.StartsWith('#'))hex=hex[1..];
        if(hex.Length==6&&uint.TryParse(hex,NumberStyles.HexNumber,CultureInfo.InvariantCulture,out uint rgb)){color=Color.FromRgb((byte)(rgb>>16),(byte)(rgb>>8),(byte)rgb);return true;}
        color=Colors.White;return false;
    }
}
