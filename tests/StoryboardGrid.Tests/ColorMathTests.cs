using System;
using System.Windows.Media;
using StoryboardGrid;

internal static class ColorMathTests
{
    public static void Run()
    {
        foreach(byte r in new byte[]{0,1,64,128,254,255})foreach(byte g in new byte[]{0,1,64,128,254,255})foreach(byte b in new byte[]{0,1,64,128,254,255})
        {
            var original=Color.FromRgb(r,g,b);var(h,s,v)=ColorMath.ToHsv(original);
            if(ColorMath.FromHsv(h,s,v)!=original)throw new Exception("RGB/HSV roundtrip changed color.");
        }
        if(ColorMath.FromHsv(360,1,1)!=Colors.Red||ColorMath.FromHsv(120,1,1)!=Color.FromRgb(0,255,0)||ColorMath.FromHsv(240,1,1)!=Colors.Blue)throw new Exception("Hue primaries incorrect.");
        if(!ColorMath.TryHex(" #16a065 ",out var c)||ColorMath.Hex(c)!="#16A065")throw new Exception("HEX parsing failed.");
        foreach(string bad in new[]{"#fff","#xyzxyz","#FFFFFFFF","","##112233"})if(ColorMath.TryHex(bad,out _))throw new Exception("Invalid HEX accepted.");
        Console.WriteLine("PASS 216 RGB and HSV roundtrips and hue primaries");
        Console.WriteLine("PASS exact HEX values and invalid input rejection");
    }
}
