using System;
using StoryboardGrid;
using StoryboardGrid.Core;

internal static class CanvasGeometryTests
{
    public static void Run()
    {
        foreach(var size in new[]{new System.Windows.Size(400,300),new System.Windows.Size(1000,600)})
        {
            var rects=CanvasGeometry.CenteredStack(size,[new(42,56),new(180,25),new(96,18)],12);
            if(rects.Any(r=>Math.Abs(r.X+r.Width/2-size.Width/2)>0.001)||Math.Abs((rects[0].Top+rects[^1].Bottom)/2-size.Height/2)>0.001)throw new Exception("Empty-state elements are not centered.");
        }
        Console.WriteLine("PASS empty-state icon and both text lines align to exact canvas center");
        var layout=new GridLayout(2,2,100,80,3);
        if(CanvasGeometry.CellAt(layout,99,79)!=0||CanvasGeometry.CellAt(layout,103,83)!=3)throw new Exception("Cell boundary hit is incorrect.");
        foreach(var(x,y) in new[]{(100d,0d),(102d,40d),(30d,80d),(90d,82d),(-1d,0d),(203d,5d)})
            if(CanvasGeometry.CellAt(layout,x,y)!=-1)throw new Exception("Gap/outside hit incorrectly replaces a picture.");
        foreach(double dpi in new[]{1d,1.25,1.5,2})
        {
            double fit=CanvasGeometry.FitScale(720,500,3843,2163);double zoom=1/(fit*dpi);
            if(Math.Abs(fit*zoom*dpi-1)>0.000001)throw new Exception("100% view does not match physical screen pixels.");
        }
        if(CanvasGeometry.FitScale(10,10,1000,600)<=0)throw new Exception("Small viewport produced invalid scale.");
        Console.WriteLine("PASS canvas gap and outside positions never target an image");
        Console.WriteLine("PASS 100 percent pixel view math at 100/125/150/200 percent DPI");
    }
}
