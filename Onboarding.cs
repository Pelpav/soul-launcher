// Premier lancement : avertissement de non-affiliation puis choix du logo (conservé ensuite).
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

static class LogoIcons
{
    public static string IcoPath { get { return Path.Combine(Store.AppDir, "logo.ico"); } }

    public static Bitmap Get(int i)
    {
        using (var ms = new MemoryStream(Convert.FromBase64String(Logos.Png[i])))
        using (var b = new Bitmap(ms)) return new Bitmap(b);
    }

    public static Bitmap Scale(Bitmap src, int size)
    {
        Bitmap dst = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(dst))
        {
            bool pixel = size >= src.Width;
            g.InterpolationMode = pixel ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(src, new Rectangle(0, 0, size, size));
        }
        return dst;
    }

    // Écrit un .ico multi-tailles : DIB 32 bits jusqu'à 64 px, PNG pour 256 px
    public static void SaveIco(int index, string path)
    {
        int[] sizes = { 16, 24, 32, 48, 64, 256 };
        byte[][] data = new byte[sizes.Length][];
        using (Bitmap src = Get(index))
        {
            for (int k = 0; k < sizes.Length; k++)
                using (Bitmap b = Scale(src, sizes[k]))
                    data[k] = sizes[k] == 256 ? Png(b) : Dib(b);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using (var fs = new FileStream(path, FileMode.Create))
        using (var w = new BinaryWriter(fs))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int k = 0; k < sizes.Length; k++)
            {
                w.Write((byte)(sizes[k] >= 256 ? 0 : sizes[k]));
                w.Write((byte)(sizes[k] >= 256 ? 0 : sizes[k]));
                w.Write((byte)0); w.Write((byte)0);
                w.Write((short)1); w.Write((short)32);
                w.Write(data[k].Length); w.Write(offset);
                offset += data[k].Length;
            }
            foreach (byte[] d in data) w.Write(d);
        }
    }

    static byte[] Png(Bitmap b)
    {
        using (var ms = new MemoryStream()) { b.Save(ms, ImageFormat.Png); return ms.ToArray(); }
    }

    static byte[] Dib(Bitmap b)
    {
        int n = b.Width, maskRow = ((n + 31) / 32) * 4;
        using (var ms = new MemoryStream())
        using (var w = new BinaryWriter(ms))
        {
            w.Write(40); w.Write(n); w.Write(n * 2); w.Write((short)1); w.Write((short)32);
            w.Write(0); w.Write(n * n * 4 + maskRow * n); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
            for (int y = n - 1; y >= 0; y--)
                for (int x = 0; x < n; x++)
                {
                    Color c = b.GetPixel(x, y);
                    w.Write(c.B); w.Write(c.G); w.Write(c.R); w.Write(c.A);
                }
            w.Write(new byte[maskRow * n]); // masque AND vide : la transparence vient du canal alpha
            return ms.ToArray();
        }
    }

    public static int Chosen()
    {
        int i;
        return int.TryParse(Store.GetSetting("logo"), out i) && i >= 0 && i < Logos.Png.Length ? i : -1;
    }

    public static Icon CurrentIcon()
    {
        int i = Chosen();
        if (i < 0) return null;
        try
        {
            if (!File.Exists(IcoPath)) SaveIco(i, IcoPath);
            return new Icon(IcoPath);
        }
        catch { return null; }
    }
}

class LogoTile : Control
{
    public int Index;
    public bool Selected;
    bool hover;
    Bitmap img;

    public LogoTile(int index)
    {
        Index = index;
        img = LogoIcons.Get(index);
        Size = new Size(84, 84);
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) OnClick(EventArgs.Empty);
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Parent.BackColor);
        Color border = Selected ? Color.FromArgb(255, 200, 0) : (hover || Focused ? Color.FromArgb(120, 120, 120) : Color.Transparent);
        if (border != Color.Transparent)
            using (Pen p = new Pen(border, 3)) g.DrawRectangle(p, 1, 1, Width - 3, Height - 3);
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(img, new Rectangle(10, 10, 64, 64));
    }
}

