namespace StoryboardGrid.Core;

public record Picture(string Path, int Width, int Height);
public record PixelRect(int X, int Y, int Width, int Height);
public record GridLayout(int Columns, int Rows, int CellWidth, int CellHeight, int Gap)
{
    public int Width => checked(Columns * CellWidth + (Columns - 1) * Gap);
    public int Height => checked(Rows * CellHeight + (Rows - 1) * Gap);
    public static GridLayout Calculate(IEnumerable<Picture?> pictures, int columns, int rows, int ratioW, int ratioH, int gap, int targetW = 0, int targetH = 0)
    {
        var items = pictures.ToArray();
        if (columns is < 1 or > 10 || rows is < 1 or > 10 || gap is < 0 or > 100) throw new ArgumentException("行列须为 1–10，间距须为 0–100px。");
        if (items.Length > columns * rows) throw new ArgumentException($"共 {items.Length} 张图片，宫格仅容纳 {columns * rows} 张，请增加行列或删除图片。");
        if (ratioW is < 1 or > 10000 || ratioH is < 1 or > 10000) throw new ArgumentException("比例须为 1–10000 的整数。");
        int gcd = Gcd(ratioW, ratioH); ratioW /= gcd; ratioH /= gcd;
        int factor = 1;
        foreach (var p in items.OfType<Picture>()) factor = Math.Max(factor, Math.Max((p.Width + ratioW - 1) / ratioW, (p.Height + ratioH - 1) / ratioH));
        var result = new GridLayout(columns, rows, targetW > 0 ? targetW : checked(ratioW * factor), targetH > 0 ? targetH : checked(ratioH * factor), gap);
        if (result.CellWidth < 1 || result.CellHeight < 1) throw new ArgumentException("单格尺寸必须大于 0。");
        _ = result.Width; _ = result.Height;
        return result;
    }
    static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);
}

public static class Cuts
{
    public static int[] Equal(int length, int count)
    {
        if (count is < 1 or > 10 || length < count) throw new ArgumentException("图像尺寸不足以按此行列切分。");
        return Enumerable.Range(0, count + 1).Select(i => (int)((long)i * length / count)).ToArray();
    }
    public static PixelRect[] Regions(int[] x, int[] y, int inset)
    {
        if (inset is < 0 or > 32767) throw new ArgumentException("内缩须在 0–32767px 范围内。");
        var result = new List<PixelRect>();
        for (int r = 0; r < y.Length - 1; r++)
            for (int c = 0; c < x.Length - 1; c++)
            {
                int w = x[c + 1] - x[c] - 2 * inset, h = y[r + 1] - y[r] - 2 * inset;
                if (w <= 0 || h <= 0) throw new ArgumentException("内缩过大或切分线交叉，输出区域必须至少为 1×1 像素。");
                result.Add(new(x[c] + inset, y[r] + inset, w, h));
            }
        return result.ToArray();
    }
}
