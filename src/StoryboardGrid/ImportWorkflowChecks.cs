using System;
using System.IO;
using System.Threading.Tasks;
using SkiaSharp;

namespace StoryboardGrid;
internal static class ImportWorkflowChecks
{
    internal static async Task RunAsync(MainWindow window,string source,string directory)
    {
        window.State.Images.Clear();window.State.SplitMode=false;window.State.Columns=4;window.State.Rows=3;
        string second=Path.Combine(directory,"import-second.png");
        using(var image=new SKBitmap(64,36)){image.Erase(SKColors.CornflowerBlue);using var data=image.Encode(SKEncodedImageFormat.Png,100);using var stream=File.Create(second);data.SaveTo(stream);}
        var initial=MainWindow.Load(source);window.State.Images.Add(initial);
        await window.ImportAsync([second,source],0);
        if(window.State.Images.Count!=3||!ReferenceEquals(initial,window.State.Images[0])||window.State.Images[1]!.Picture.Path!=second||window.State.Images[2]!.Picture.Path!=source)throw new Exception("Batch drop on occupied cell did not append in supplied order.");
        string message="";window.ErrorDialogOverride=text=>message=text;
        string missing=Path.Combine(directory,"missing-import.png");
        await window.ImportAsync([missing,source]);
        if(window.State.Images.Count!=4||!message.Contains("missing-import.png")||window.State.Busy)throw new Exception("Partial import failure lost valid images or left window busy.");
        int count=window.State.Images.Count;await window.ImportAsync([missing],0);
        if(window.State.Images.Count!=count||!ReferenceEquals(initial,window.State.Images[0]))throw new Exception("Failed replacement removed the original picture.");
        var unchanged=window.State.Images[2];await window.ImportAsync([second],1);
        if(window.State.Images.Count!=count||!ReferenceEquals(unchanged,window.State.Images[2])||window.State.Images[1]!.Picture.Path!=second)throw new Exception("Single replacement changed other positions.");
    }
    internal static async Task CheckPickerAsync(MainWindow window,string source)
    {
        int count=window.State.Images.Count;string error="";window.ErrorDialogOverride=text=>error=text;
        window.OpenDialogOverride=_=>throw new IOException("Import file dialog unavailable");
        try{await window.ChooseFilesAsync();}catch(Exception ex){throw new Exception("Import file dialog failure escaped its UI boundary.",ex);}
        if(!error.Contains("Import file dialog unavailable")||window.State.Busy||window.State.Images.Count!=count)throw new Exception("Import dialog failure changed state.");
        window.OpenDialogOverride=_=>false;await window.ChooseFilesAsync();
        if(window.State.Images.Count!=count)throw new Exception("Canceled import changed pictures.");
        window.OpenDialogOverride=dialog=>{dialog.FileName=source;return true;};await window.ChooseFilesAsync();
        if(window.State.Images.Count!=count+1||!window.State.Status.StartsWith("已导入"))throw new Exception("Import did not recover after dialog failure.");
        bool opened=false;window.OpenDialogOverride=_=>{opened=true;return false;};window.State.Busy=true;await window.ChooseFilesAsync();window.State.Busy=false;
        if(opened)throw new Exception("Import dialog opened during active task.");
        window.OpenDialogOverride=null;
    }
}
