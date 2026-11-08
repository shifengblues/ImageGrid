using SkiaSharp;

namespace StoryboardGrid.Core;

public static class ImageService
{
    public const long MaxWorkingBytes = 768L * 1024 * 1024;
    public static void Guard(int w, int h, long extra = 0)
    {
        long bytes = checked((long)w * h * 4 + extra);
        long available = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        if (w <= 0 || h <= 0 || w > 65535 || h > 65535 || bytes > Math.Min(MaxWorkingBytes, available / 3))
            throw new ArgumentException($"预计工作像素缓冲约 {bytes/1048576d:0} MB，超过当前安全上限 {Math.Min(MaxWorkingBytes,available/3)/1048576d:0} MB（边长最多 65535px）。请使用更小的图像/统一尺寸，或内存更充足的设备；程序不会自动降低输出分辨率。");
    }
    public static Picture Inspect(string path)
    {
        using var stream = File.OpenRead(path);
        using var codec = SKCodec.Create(stream) ?? throw new IOException("无法解码图片或格式不支持。");
        var swap = (int)codec.EncodedOrigin >= 5;
        return new(path, swap ? codec.Info.Height : codec.Info.Width, swap ? codec.Info.Width : codec.Info.Height);
    }
    public static SKBitmap Decode(string path, int previewEdge = 0)
    {
        using var stream = File.OpenRead(path);
        using var codec = SKCodec.Create(stream) ?? throw new IOException("无法解码图片。");
        var size = previewEdge > 0 ? codec.GetScaledDimensions(Math.Min(1f, (float)previewEdge / Math.Max(codec.Info.Width, codec.Info.Height))) : new SKSizeI(codec.Info.Width, codec.Info.Height);
        int origin=(int)codec.EncodedOrigin;
        Guard(size.Width, size.Height, origin==1?0:(long)size.Width*size.Height*4);
        var raw = new SKBitmap(new SKImageInfo(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        bool transferred=false;
        SKBitmap? oriented=null;
        try
        {
        var result = codec.GetPixels(raw.Info, raw.GetPixels());
        if (result != SKCodecResult.Success) throw new IOException($"图像解码不完整：{result}");
        if (origin == 1) {transferred=true;return raw;}
        bool swap = origin >= 5;
        oriented = new SKBitmap(swap ? raw.Height : raw.Width, swap ? raw.Width : raw.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        unsafe
        {
            for (int y = 0; y < raw.Height; y++) for (int x = 0; x < raw.Width; x++)
            {
                var (dx, dy) = origin switch {
                    2 => (raw.Width - 1 - x, y), 3 => (raw.Width - 1 - x, raw.Height - 1 - y),
                    4 => (x, raw.Height - 1 - y), 5 => (y, x), 6 => (raw.Height - 1 - y, x),
                    7 => (raw.Height - 1 - y, raw.Width - 1 - x), 8 => (y, raw.Width - 1 - x), _ => (x,y) };
                *(uint*)((byte*)oriented.GetPixels() + dy * oriented.RowBytes + dx * 4) = *(uint*)((byte*)raw.GetPixels() + y * raw.RowBytes + x * 4);
            }
        }
        return oriented;
        }
        catch {oriented?.Dispose();throw;}
        finally {if(!transferred)raw.Dispose();}
    }
    public static SKBitmap Thumbnail(string path, int maxEdge = 480)
    {
        using var decoded = Decode(path, maxEdge);
        double scale = Math.Min(1, (double)maxEdge / Math.Max(decoded.Width, decoded.Height));
        return decoded.Resize(new SKImageInfo(Math.Max(1,(int)(decoded.Width * scale)), Math.Max(1,(int)(decoded.Height * scale)), SKColorType.Bgra8888, SKAlphaType.Premul), new SKSamplingOptions(SKFilterMode.Linear)) ?? throw new IOException("缩略图生成失败。");
    }
    public static unsafe void CopyPixels(SKBitmap source, SKBitmap target, PixelRect region, int dx, int dy, CancellationToken ct)
    {
        for (int y = 0; y < region.Height; y++)
        {
            ct.ThrowIfCancellationRequested();
            Buffer.MemoryCopy((byte*)source.GetPixels() + (region.Y + y) * source.RowBytes + region.X * 4,
                (byte*)target.GetPixels() + (dy + y) * target.RowBytes + dx * 4, region.Width * 4L, region.Width * 4L);
        }
    }
    public static void Compose(IReadOnlyList<Picture?> pictures, GridLayout layout, bool original, bool fill, SKColor background, SKColor gapColor, string path, IProgress<double>? progress, CancellationToken ct, SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        ct.ThrowIfCancellationRequested();
        if (pictures.Count > layout.Columns * layout.Rows) throw new ArgumentException("图片数量超出宫格容量。");
        if(format is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg))throw new ArgumentException("仅支持 PNG 或 JPEG 导出。");
        long largest = pictures.OfType<Picture>().Select(p=>DecodePeak(p.Path)).DefaultIfEmpty().Max();
        Guard(layout.Width, layout.Height, Math.Max(largest,(long)layout.Width*layout.Height*4)+1048576);
        if(pictures.OfType<Picture>().Any(p=>string.Equals(Path.GetFullPath(p.Path),Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase))) throw new IOException("不能覆盖源图片。");
        using var output = new SKBitmap(layout.Width, layout.Height, SKColorType.Bgra8888, original ? SKAlphaType.Unpremul : SKAlphaType.Premul);
        output.Erase(background);
        // Erase rectangles without a drawing surface: unpremultiplied output preserves transparent RGB.
        for (int c = 1; c < layout.Columns; c++) output.Erase(gapColor, SKRectI.Create(c * layout.CellWidth + (c - 1) * layout.Gap, 0, layout.Gap, layout.Height));
        for (int r = 1; r < layout.Rows; r++) output.Erase(gapColor, SKRectI.Create(0, r * layout.CellHeight + (r - 1) * layout.Gap, layout.Width, layout.Gap));
        for (int i = 0; i < pictures.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (pictures[i] is {} p)
            {
                using var source = Decode(p.Path);
                if(source.Width!=p.Width||source.Height!=p.Height)throw new IOException("源图尺寸发生变化，请重新导入后再导出。");
                int x = i % layout.Columns * (layout.CellWidth + layout.Gap), y = i / layout.Columns * (layout.CellHeight + layout.Gap);
                if (original)
                {
                    if (source.Width > layout.CellWidth || source.Height > layout.CellHeight) throw new IOException("源图尺寸发生变化，请重新导入。");
                    CopyPixels(source, output, new(0,0,source.Width,source.Height), x + (layout.CellWidth-source.Width)/2, y + (layout.CellHeight-source.Height)/2, ct);
                }
                else
                {
                    using var canvas = new SKCanvas(output);
                    float s = fill ? Math.Max((float)layout.CellWidth/source.Width,(float)layout.CellHeight/source.Height) : Math.Min((float)layout.CellWidth/source.Width,(float)layout.CellHeight/source.Height);
                    canvas.Save(); canvas.ClipRect(SKRect.Create(x,y,layout.CellWidth,layout.CellHeight));
                    source.SetImmutable();using var image=SKImage.FromBitmap(source);
                    canvas.DrawImage(image, SKRect.Create(x+(layout.CellWidth-source.Width*s)/2,y+(layout.CellHeight-source.Height*s)/2,source.Width*s,source.Height*s),new SKSamplingOptions(SKFilterMode.Linear)); canvas.Restore();
                }
            }
            progress?.Report((i+1d)/(pictures.Count+1));
        }
        if(format==SKEncodedImageFormat.Jpeg)Flatten(output,background,ct);
        Save(output, path, ct, format:format); progress?.Report(1);
    }
    public static string[] Split(Picture picture, int[] xs, int[] ys, int inset, string directory, IProgress<double>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(xs.Length<2 || ys.Length<2 || xs[0]!=0 || ys[0]!=0 || xs[^1]!=picture.Width || ys[^1]!=picture.Height) throw new ArgumentException("切分边界必须完整覆盖原图。");
        var regions = Cuts.Regions(xs, ys, inset);
        long largestTile=regions.Max(r=>(long)r.Width*r.Height*4);
        Guard(picture.Width, picture.Height,Math.Max(DecodePeak(picture.Path)-(long)picture.Width*picture.Height*4,largestTile)+1048576);
        using var source = Decode(picture.Path);
        if (source.Width != picture.Width || source.Height != picture.Height) throw new IOException("源图尺寸发生变化，请重新导入。");
        var written = new List<string>();
        try
        {
            for(int i=0;i<regions.Length;i++)
            {
                ct.ThrowIfCancellationRequested(); var r=regions[i];
                using var tile = new SKBitmap();
                if(!source.ExtractSubset(tile,SKRectI.Create(r.X,r.Y,r.Width,r.Height)))throw new IOException("无法提取切分区域。");
                string file = Path.Combine(directory,$"{Path.GetFileNameWithoutExtension(picture.Path)}_{i+1:00}.png");
                Save(tile,file,ct, false); written.Add(file); progress?.Report((i+1d)/regions.Length);
            }
            return written.ToArray();
        }
        catch
        {
            foreach(var file in written) { try { File.Delete(file); } catch { } }
            throw;
        }
    }
    static long DecodePeak(string path)
    {
        using var stream=File.OpenRead(path);using var codec=SKCodec.Create(stream)??throw new IOException("无法解码图片。");
        return (long)codec.Info.Width*codec.Info.Height*4*((int)codec.EncodedOrigin==1?1:2);
    }
    static unsafe void Flatten(SKBitmap bitmap,SKColor background,CancellationToken ct)
    {
        bool premultiplied=bitmap.AlphaType==SKAlphaType.Premul;
        for(int y=0;y<bitmap.Height;y++)
        {
            ct.ThrowIfCancellationRequested();byte* row=(byte*)bitmap.GetPixels()+y*bitmap.RowBytes;
            for(int x=0;x<bitmap.Width;x++)
            {
                byte* p=row+x*4;int a=p[3];if(a==255)continue;
                p[0]=(byte)Math.Min(255,((premultiplied?p[0]*255:p[0]*a)+background.Blue*(255-a)+127)/255);
                p[1]=(byte)Math.Min(255,((premultiplied?p[1]*255:p[1]*a)+background.Green*(255-a)+127)/255);
                p[2]=(byte)Math.Min(255,((premultiplied?p[2]*255:p[2]*a)+background.Red*(255-a)+127)/255);p[3]=255;
            }
        }
    }
    static void Save(SKBitmap bitmap, string path, CancellationToken ct, bool overwrite = true, SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        string? root=Path.GetPathRoot(Path.GetFullPath(path));
        if(root!=null && !root.StartsWith(@"\\") && new DriveInfo(root).AvailableFreeSpace < bitmap.ByteCount + 1024L*1024) throw new IOException("磁盘可用空间不足，请选择空间更充足的输出目录。");
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            ct.ThrowIfCancellationRequested();
            bitmap.SetImmutable();
            using(var image=SKImage.FromBitmap(bitmap))
            using(var data=image.Encode(format,100) ?? throw new IOException("图像编码失败。"))
            using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write)) data.SaveTo(file);
            ct.ThrowIfCancellationRequested(); File.Move(temp,path,overwrite);
        }
        finally { if(File.Exists(temp)) File.Delete(temp); }
    }
}
