using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace StoryboardGrid;
// Only the canvas requests larger previews. The bottom strip retains its 480px thumbnails.
internal sealed class CanvasPreviewCache : IDisposable
{
    const long Budget=64L*1024*1024;
    sealed record Cached(BitmapSource Image, int Edge, long Used);
    readonly Dictionary<ImageEntry,Cached> cache=new();
    readonly Dictionary<ImageEntry,int> pending=new();
    readonly HashSet<ImageEntry> failed=new();
    readonly HashSet<ImageEntry> deferred=new();
    readonly DispatcherTimer timer;
    bool loading;
    bool disposed;
    long clock;
    public event Action? Changed;
    public CanvasPreviewCache(Dispatcher dispatcher)
    {
        timer=new DispatcherTimer(TimeSpan.FromMilliseconds(150),DispatcherPriority.Background,Tick,dispatcher);timer.Stop();
    }
    public BitmapSource Get(ImageEntry entry,double displayedEdge)
    {
        if(disposed)return entry.Thumbnail;
        int wanted=Math.Min(Math.Max(entry.Picture.Width,entry.Picture.Height),TargetEdge(displayedEdge));
        BitmapSource image=entry.Thumbnail;int edge=Math.Max(image.PixelWidth,image.PixelHeight);
        if(cache.TryGetValue(entry,out var cached)){image=cached.Image;edge=cached.Edge;cache[entry]=cached with{Used=++clock};}
        if(wanted>edge&&!failed.Contains(entry)&&!deferred.Contains(entry)){pending[entry]=wanted;if(!loading)timer.Start();}
        return image;
    }
    internal static int TargetEdge(double size) => size<=480?480:size<=960?960:size<=1920?1920:size<=3840?3840:4096;
    public void RetryVisible() {deferred.Clear();failed.Clear();}
    public void Dispose()
    {
        disposed=true;timer.Stop();timer.Tick-=Tick;pending.Clear();cache.Clear();failed.Clear();deferred.Clear();Changed=null;
    }
    public void Prune(IEnumerable<ImageEntry> retained)
    {
        var keep=retained.ToHashSet();
        foreach(var key in cache.Keys.Where(k=>!keep.Contains(k)).ToArray())cache.Remove(key);
        foreach(var key in pending.Keys.Where(k=>!keep.Contains(k)).ToArray())pending.Remove(key);
        failed.RemoveWhere(k=>!keep.Contains(k));
        deferred.RemoveWhere(k=>!keep.Contains(k));
    }
    async void Tick(object? sender,EventArgs args)
    {
        timer.Stop();if(loading||pending.Count==0)return;
        var request=pending.Last();loading=true;
        try
        {
            var image=await Task.Run(()=>MainWindow.Load(request.Key.Picture.Path,request.Value).Thumbnail);
            // A removed/replaced item may have disappeared while the worker was decoding it.
            if(!disposed&&pending.ContainsKey(request.Key))
            {
                cache[request.Key]=new(image,request.Value,++clock);
                if(pending[request.Key]<=request.Value)pending.Remove(request.Key);
                while(cache.Values.Sum(c=>(long)c.Image.PixelWidth*c.Image.PixelHeight*4)>Budget)
                {
                    var evicted=cache.MinBy(p=>p.Value.Used).Key;
                    cache.Remove(evicted);pending.Remove(evicted);deferred.Add(evicted);
                }
                Changed?.Invoke();
            }
        }
        catch(Exception) {if(!disposed){failed.Add(request.Key);pending.Remove(request.Key);}}
        finally {loading=false;if(!disposed&&pending.Count>0)timer.Start();}
    }
}
