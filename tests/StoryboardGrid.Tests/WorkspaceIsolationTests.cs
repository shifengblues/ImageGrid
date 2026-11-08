using System;
using System.Linq;
using StoryboardGrid;
using StoryboardGrid.Core;

internal static class WorkspaceIsolationTests
{
    public static void Run()
    {
        var state=new Workspace();state.Columns=4;state.Rows=3;
        state.Source=new ImageEntry(new Picture("split.png",1000,600),null!);
        state.SplitMode=true;state.ResetCuts();
        if(state.Columns!=2||state.Rows!=2)throw new Exception("Split layout inherited compose layout.");
        state.Xs[1]=420;state.Ys[1]=230;
        state.Columns=state.Columns;
        if(state.Xs[1]!=420||state.Ys[1]!=230)throw new Exception("Rebinding unchanged layout erased manual cuts.");
        state.Rows=3;
        if(state.Xs[1]!=420||!state.Ys.SequenceEqual(new[]{0,200,400,600}))throw new Exception("Changing rows erased vertical cuts.");
        state.Ys[1]=170;state.Columns=3;
        if(state.Ys[1]!=170)throw new Exception("Changing columns erased horizontal cuts.");
        state.SplitMode=false;
        if(state.Columns!=4||state.Rows!=3)throw new Exception("Compose layout was not preserved.");
        state.Columns=5;state.GapColor="#bad";
        if(state.Error==null)throw new Exception("Invalid compose color accepted.");
        state.SplitMode=true;
        if(state.Columns!=3||state.Rows!=3||state.Ys[1]!=170||state.Error!=null)throw new Exception("Compose settings damaged split state.");
        state.Columns=0;if(state.Error==null)throw new Exception("Invalid split columns accepted.");
        state.Columns=3;if(state.Error!=null)throw new Exception("Corrected split layout did not recover.");
        Console.WriteLine("PASS compose and split keep independent layouts and manual cuts");
        Console.WriteLine("PASS unchanged layout and one-axis edits preserve other manual cuts");
        Console.WriteLine("PASS invalid compose color does not block splitting; invalid split layout recovers");
    }
}
