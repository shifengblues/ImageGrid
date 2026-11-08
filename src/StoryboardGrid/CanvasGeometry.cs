using System;
using System.Linq;
using System.Windows;
using StoryboardGrid.Core;

namespace StoryboardGrid;
internal static class CanvasGeometry
{
    internal static Rect[] CenteredStack(Size available,Size[] items,double gap)
    {
        double y=(available.Height-items.Sum(i=>i.Height)-gap*(items.Length-1))/2;
        return items.Select(item=>{var rect=new Rect((available.Width-item.Width)/2,y,item.Width,item.Height);y+=item.Height+gap;return rect;}).ToArray();
    }
    internal static double FitScale(double width,double height,double contentWidth,double contentHeight) =>
        Math.Max(0.00001,Math.Min(Math.Max(1,width-64)/contentWidth,Math.Max(1,height-64)/contentHeight));
    internal static int CellAt(GridLayout layout,double x,double y)
    {
        if(x<0||y<0||x>=layout.Width||y>=layout.Height)return -1;
        int strideX=layout.CellWidth+layout.Gap,strideY=layout.CellHeight+layout.Gap;
        if(x%strideX>=layout.CellWidth||y%strideY>=layout.CellHeight)return -1;
        return (int)(y/strideY)*layout.Columns+(int)(x/strideX);
    }
}
