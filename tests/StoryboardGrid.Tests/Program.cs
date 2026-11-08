using StoryboardGrid.Core;
using SkiaSharp;
using System.Runtime.InteropServices;
using System.IO;

if(args.Contains("--color-math")){ColorMathTests.Run();return;}
if(args.Contains("--large-grid-policy")){ImageService.Guard(19212,10812,19212L*10812*4);Console.WriteLine("PASS 25 UHD images are not blocked by a fixed memory budget");return;}
if(args.Length==3&&args[0]=="--uhd-grid"){LargeImageTests.GridUhd(args[1],args[2]);return;}
ColorMathTests.Run();

if(args.Length==3 && args[0]=="--large-input") {try{LargeImageTests.Run(args[1],args[2]);}catch(Exception ex){Console.Error.WriteLine(ex);Environment.ExitCode=1;}return;}

if(args.Contains("--export-safety")){ExportSafetyTests.Run();return;}

ExportProgressTests.Run();
WorkspaceIsolationTests.Run();
if(args.Contains("--workspace-regression"))return;
if(args.Contains("--progress-regression"))return;
CanvasPreviewTests.Run();
CanvasGeometryTests.Run();
ExportSafetyTests.Run();
JpegExportTests.Run();
if(args.Contains("--preview-regression"))return;

var images = Enumerable.Range(0, 4).Select(i => new Picture($"{i}.png", 1920, 1080)).ToArray();
var layout = GridLayout.Calculate(images, 2, 2, 16, 9, 3);
Check(layout.Width == 3843 && layout.Height == 2163, "2×2 exact output dimensions");
Check(GridLayout.Calculate(images, 2, 2, 16, 9, 0).Width == 3840, "zero gap");
var mixed = GridLayout.Calculate([new("a", 1000, 2000), new("b", 2001, 100)], 2, 2, 16, 9, 3);
Check(mixed.CellWidth % 16 == 0 && mixed.CellHeight % 9 == 0 && mixed.CellHeight >= 2000 && mixed.CellWidth >= 2001, "integer ratio and complete containment");
var boundaries = Cuts.Equal(11, 3);
Check(boundaries.SequenceEqual(new[] { 0, 3, 7, 11 }), "remainder coverage");
Check(Cuts.Regions([0, 5, 11], [0, 4, 9], 1).Select(r => r.Width).SequenceEqual(new[] {3,4,3,4}), "precise inset and row order");
try { Cuts.Regions([0, 2], [0, 2], 1); throw new Exception("invalid inset accepted"); } catch (ArgumentException) { }
try { GridLayout.Calculate(images, 1, 1, 16, 9, 3); throw new Exception("overflow accepted"); } catch (ArgumentException) { }
Console.WriteLine("All geometry tests passed.");
string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../output"));Directory.CreateDirectory(root);
var sourcePaths=new List<string>();
for(int i=0;i<4;i++)
{
    string path=Path.Combine(root,$"分镜_{i+1}.png");using var bitmap=new SKBitmap(1920,1080,SKColorType.Bgra8888,SKAlphaType.Unpremul);
    byte[] pixels=new byte[bitmap.ByteCount];
    for(int y=0;y<1080;y++)for(int x=0;x<1920;x++){int at=y*bitmap.RowBytes+x*4;pixels[at]=(byte)(x+i*37);pixels[at+1]=(byte)(y+i*51);pixels[at+2]=(byte)(x+y);pixels[at+3]=(byte)(x%3==0?127:255);}
    Marshal.Copy(pixels,0,bitmap.GetPixels(),pixels.Length);Write(bitmap,path,SKEncodedImageFormat.Png);sourcePaths.Add(path);
}
var real=sourcePaths.Select(ImageService.Inspect).Cast<Picture?>().ToArray();
string composed=Path.Combine(root,"Storyboard_Grid.png");
ImageService.Compose(real,layout,true,false,SKColors.White,SKColors.Magenta,composed,null,default);
using(var output=ImageService.Decode(composed))
{
    Check(output.Width==3843&&output.Height==2163,"real PNG 3843×2163");
    for(int i=0;i<4;i++){using var input=ImageService.Decode(sourcePaths[i]);Check(EqualRegion(input,output,0,0,i%2*1923,i/2*1083,input.Width,input.Height),$"all BGRA pixels including alpha, tile {i}");}
    Check(output.GetPixel(1920,12)==SKColors.Magenta && output.GetPixel(12,1080)==SKColors.Magenta,"exact gap color");
}
var noGap=GridLayout.Calculate(real,2,2,16,9,0);string clean=Path.Combine(root,"clean.png");
ImageService.Compose(real,noGap,true,false,SKColors.White,SKColors.White,clean,null,default);
string splitDir=Path.Combine(root,"split");Directory.CreateDirectory(splitDir);
foreach(var f in Directory.GetFiles(splitDir))File.Delete(f);
var originalHash=System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(clean));
var splitFiles=ImageService.Split(ImageService.Inspect(clean),Cuts.Equal(3840,2),Cuts.Equal(2160,2),0,splitDir,null,default);
for(int i=0;i<4;i++){using var input=ImageService.Decode(sourcePaths[i]);using var tile=ImageService.Decode(splitFiles[i]);Check(tile.Width==1920&&tile.Height==1080&&EqualRegion(input,tile,0,0,0,0,1920,1080),$"split {i+1:00} exact pixels / no guides");}
Check(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(clean)).SequenceEqual(originalHash),"source file unchanged");
using(var odd=new SKBitmap(11,9)){odd.Erase(SKColors.Coral);Write(odd,Path.Combine(root,"odd.png"),SKEncodedImageFormat.Png);}
string oddDir=Path.Combine(root,"odd-split");Directory.CreateDirectory(oddDir);foreach(var f in Directory.GetFiles(oddDir))File.Delete(f);
var oddFiles=ImageService.Split(ImageService.Inspect(Path.Combine(root,"odd.png")),[0,4,11],[0,5,9],1,oddDir,null,default);
Check(oddFiles.Select(ImageService.Inspect).Select(p=>(p.Width,p.Height)).SequenceEqual(new[]{(2,3),(5,3),(2,2),(5,2)}),"manual boundaries and exact inset");
foreach(var format in new[]{SKEncodedImageFormat.Jpeg,SKEncodedImageFormat.Webp,SKEncodedImageFormat.Bmp})
{
    using var bitmap=new SKBitmap(41,27);for(int y=0;y<27;y++)for(int x=0;x<41;x++)bitmap.SetPixel(x,y,new SKColor((byte)(x*6),(byte)(y*9),(byte)((x+y)*3)));
    // Skia does not encode BMP; a hand-written BMP fixture is generated below.
    if(format==SKEncodedImageFormat.Bmp)continue;
    string path=Path.Combine(root,"format."+format.ToString().ToLowerInvariant());Write(bitmap,path,format);using var decoded=ImageService.Decode(path);Check(decoded.Width==41&&decoded.Height==27,"decode "+format);
}
try{ImageService.Guard(0,100000);throw new Exception("invalid size accepted");}catch(ArgumentException){Console.WriteLine("PASS invalid image dimensions rejected without fixed memory budget");}
string canceled=Path.Combine(root,"cancel.png");if(File.Exists(canceled))File.Delete(canceled);
using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{ImageService.Compose(real,layout,true,false,SKColors.White,SKColors.White,canceled,null,cancel.Token);throw new Exception("cancel ignored");}catch(OperationCanceledException){Check(!File.Exists(canceled),"canceled export leaves no output");}}
try{ImageService.Split(ImageService.Inspect(clean),[0,1920,3840],[0,1080,2160],0,splitDir,null,default);throw new Exception("conflict overwritten");}catch(IOException){Check(File.Exists(splitFiles[0]),"existing split files preserved");}
Console.WriteLine("All image export tests passed. Fixtures: "+root);
foreach(int count in new[]{1,4,9,16,25})
{
    int n=(int)Math.Ceiling(Math.Sqrt(count));var entries=Enumerable.Range(0,count).Select(i=>real[i%4]).ToArray();
    var small=GridLayout.Calculate(entries,n,n,16,9,3,64,36);
    string path=Path.Combine(root,$"batch-{count}.png");ImageService.Compose(entries,small,false,false,SKColors.White,SKColors.Blue,path,null,default);
    Check(ImageService.Inspect(path).Width==n*64+(n-1)*3,$"{count} images unified Fit");
}
var blanks=GridLayout.Calculate([real[0],null],2,2,16,9,3,64,36);
string blankPath=Path.Combine(root,"blank-fill.png");ImageService.Compose([real[0],null],blanks,false,true,SKColors.Yellow,SKColors.Blue,blankPath,null,default);
using(var bitmap=ImageService.Decode(blankPath))Check(bitmap.GetPixel(80,20)==SKColors.Yellow,"empty cells use configured background");
using(var fixture=new SKBitmap(17,11,SKColorType.Bgra8888,SKAlphaType.Unpremul))
{
    fixture.Erase(SKColors.OrangeRed);fixture.SetPixel(0,0,new SKColor(17,37,71,0));
    string path=Path.Combine(root,"transparent.png");Write(fixture,path,SKEncodedImageFormat.Png);
    var info=ImageService.Inspect(path);var one=GridLayout.Calculate([info],1,1,17,11,0);string output=Path.Combine(root,"transparent-out.png");
    ImageService.Compose([info],one,true,false,SKColors.White,SKColors.White,output,null,default);
    using var input=ImageService.Decode(path);using var result=ImageService.Decode(output);Check(EqualRegion(input,result,0,0,0,0,17,11),"fully transparent decoded pixels preserved");
}
// Insert EXIF orientation into one encoded JPEG; compare every pixel with the same JPEG without EXIF.
byte[] jpeg=File.ReadAllBytes(Path.Combine(root,"format.jpeg"));
using(var baseline=ImageService.Decode(Path.Combine(root,"format.jpeg")))
for(int origin=1;origin<=8;origin++)
{
    byte[] exif=[0xFF,0xE1,0,34,69,120,105,102,0,0,73,73,42,0,8,0,0,0,1,0,0x12,1,3,0,1,0,0,0,(byte)origin,0,0,0,0,0,0,0];
    string path=Path.Combine(root,$"exif-{origin}.jpg");File.WriteAllBytes(path,jpeg[..2].Concat(exif).Concat(jpeg[2..]).ToArray());
    using var oriented=ImageService.Decode(path);bool same=true;
    for(int y=0;y<baseline.Height;y++)for(int x=0;x<baseline.Width;x++)
    {
        var(dx,dy)=origin switch{2=>(baseline.Width-1-x,y),3=>(baseline.Width-1-x,baseline.Height-1-y),4=>(x,baseline.Height-1-y),5=>(y,x),6=>(baseline.Height-1-y,x),7=>(baseline.Height-1-y,baseline.Width-1-x),8=>(y,baseline.Width-1-x),_=>(x,y)};
        if(baseline.GetPixel(x,y)!=oriented.GetPixel(dx,dy))same=false;
    }
    Check(same&&oriented.Width==(origin>=5?27:41),$"EXIF orientation {origin}");
}
Console.WriteLine("Extended format and batch tests passed.");
string bmpPath=Path.Combine(root,"fixture.bmp");
using(var writer=new BinaryWriter(File.Create(bmpPath)))
{
    writer.Write((ushort)0x4D42);writer.Write(70);writer.Write(0);writer.Write(54);writer.Write(40);writer.Write(2);writer.Write(2);writer.Write((ushort)1);writer.Write((ushort)24);writer.Write(0);writer.Write(16);writer.Write(0);writer.Write(0);writer.Write(0);writer.Write(0);writer.Write(new byte[]{0,0,255,0,255,0,0,0,255,0,0,255,255,255,0,0});
}
using(var b=ImageService.Decode(bmpPath))Check(b.Width==2&&b.Height==2&&b.GetPixel(0,0)==SKColors.Blue,"BMP native decoder");
string rollback=Path.Combine(root,"rollback");Directory.CreateDirectory(rollback);foreach(var f in Directory.GetFiles(rollback))File.Delete(f);
using(var cancel=new CancellationTokenSource())
{
    try{ImageService.Split(ImageService.Inspect(clean),[0,1920,3840],[0,1080,2160],0,rollback,new InlineProgress(_=>cancel.Cancel()),cancel.Token);throw new Exception("mid-task cancellation ignored");}
    catch(OperationCanceledException){Check(Directory.GetFiles(rollback).Length==0,"mid-split cancellation removes owned outputs");}
}
var workspace=new StoryboardGrid.Workspace();
foreach(var p in picturesForModel())workspace.Images.Add(new StoryboardGrid.ImageEntry(p,null!));
workspace.Move(0,3);Check(workspace.Images.Select(x=>x!.Name).SequenceEqual(new[]{"b","c","d","a"}),"insert reorder preserves all items");
workspace.Replace(1,new StoryboardGrid.ImageEntry(new Picture("e",100,100),null!));Check(workspace.Images.Select(x=>x!.Name).SequenceEqual(new[]{"b","e","d","a"}),"replacement preserves other positions");
workspace.Replace(6,new StoryboardGrid.ImageEntry(new Picture("f",100,100),null!));Check(workspace.Images.Count==7&&workspace.Images[4]==null&&workspace.Images[5]==null,"replace empty slot preserves holes");
Check(workspace.Error!=null,"overcapacity blocks export validation");
Console.WriteLine("All checks passed.");
string aspectPath=Path.Combine(root,"aspect.png");
using(var image=new SKBitmap(16,8)) {image.Erase(SKColors.Red);image.Erase(SKColors.Blue,new SKRectI(8,0,16,8));Write(image,aspectPath,SKEncodedImageFormat.Png);}
var aspect=ImageService.Inspect(aspectPath);var square=GridLayout.Calculate([aspect],1,1,1,1,0,8,8);
foreach(bool fill in new[]{false,true})
{
    string path=Path.Combine(root,fill?"fill.png":"fit.png");ImageService.Compose([aspect],square,false,fill,SKColors.Yellow,SKColors.White,path,null,default);
    using var b=ImageService.Decode(path);
    Check(b.GetPixel(0,0)==(fill?SKColors.Red:SKColors.Yellow)&&b.GetPixel(0,3)==SKColors.Red&&b.GetPixel(7,3)==SKColors.Blue,fill?"Fill centers and crops without stretching":"Fit preserves aspect with exact letterbox");
}
try{ImageService.Compose(real,layout,true,false,SKColors.White,SKColors.White,sourcePaths[0],null,default);throw new Exception("source overwritten");}catch(IOException){Check(true,"source overwrite rejected by service");}
Console.WriteLine("Final checks passed.");
WindowWorkflowTests.Run();
var stressRoot=Path.Combine(root,"stress-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stressRoot);
string preserved=Path.Combine(stressRoot,"preserved.png");File.Copy(aspectPath,preserved);byte[] preservedBytes=File.ReadAllBytes(preserved);
for(int iteration=0;iteration<8;iteration++)
{
    using var cancel=new CancellationTokenSource();cancel.Cancel();
    try{ImageService.Compose([aspect],square,false,false,SKColors.White,SKColors.White,preserved,null,cancel.Token);throw new Exception("canceled export replaced existing file");}catch(OperationCanceledException){}
    try{ImageService.Compose([new Picture(Path.Combine(stressRoot,"missing.png"),16,8)],square,false,false,SKColors.White,SKColors.White,preserved,null,default);throw new Exception("missing source accepted");}catch(FileNotFoundException){}
    if(!File.ReadAllBytes(preserved).SequenceEqual(preservedBytes))throw new Exception("failed export changed existing destination");
    string target=Path.Combine(stressRoot,$"repeat-{iteration}.png");ImageService.Compose([aspect],square,false,iteration%2==0,SKColors.White,SKColors.White,target,null,default);
    using var decoded=ImageService.Decode(target);if(decoded.Width!=8||decoded.Height!=8)throw new Exception("Repeated export damaged output.");
}
string directoryTarget=Path.Combine(stressRoot,"directory.png");Directory.CreateDirectory(directoryTarget);
try{ImageService.Compose([aspect],square,false,false,SKColors.White,SKColors.White,directoryTarget,null,default);throw new Exception("directory destination accepted");}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){}
Check(Directory.GetFiles(stressRoot,"*.tmp").Length==0,"failed save cleans temporary files");
Check(File.ReadAllBytes(preserved).SequenceEqual(preservedBytes),"eight canceled and missing-source exports preserve existing destination");
Check(Directory.GetFiles(stressRoot,"repeat-*.png").Length==8,"eight repeated Fit and Fill exports decode correctly");
static Picture[] picturesForModel()=>new[]{"a","b","c","d"}.Select(p=>new Picture(p,100,100)).ToArray();
static void Write(SKBitmap bitmap,string path,SKEncodedImageFormat format){using var data=bitmap.Encode(format,100);using var file=File.Create(path);data.SaveTo(file);}
static bool EqualRegion(SKBitmap a,SKBitmap b,int ax,int ay,int bx,int by,int w,int h)
{
    var left=new byte[w*4];var right=new byte[w*4];
    for(int y=0;y<h;y++){Marshal.Copy(a.GetPixels()+(ay+y)*a.RowBytes+ax*4,left,0,left.Length);Marshal.Copy(b.GetPixels()+(by+y)*b.RowBytes+bx*4,right,0,right.Length);if(!left.SequenceEqual(right))return false;}return true;
}
static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
sealed class InlineProgress(Action<double> callback):IProgress<double>{public void Report(double value)=>callback(value);}
