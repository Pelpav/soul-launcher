// Boîtes de dialogue aux couleurs de Soul Launcher (remplacent les MessageBox de Windows).
// Style boîte de texte UNDERTALE : fond noir, cadre blanc, une âme en guise d'icône, cœur sur le bouton choisi.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

static class Dlg
{
    public static DialogResult Show(string text) { return Show(null, text, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.None); }
    public static DialogResult Show(string text, string caption) { return Show(null, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None); }
    public static DialogResult Show(IWin32Window owner, string text) { return Show(owner, text, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.None); }
    public static DialogResult Show(IWin32Window owner, string text, string caption) { return Show(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None); }
    public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons) { return Show(owner, text, caption, buttons, MessageBoxIcon.None); }
    public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) { return Show(null, text, caption, buttons, icon); }

    public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
    {
        return Show(owner, text, caption, buttons, icon, null);
    }

    // labels : textes des boutons à la place de Oui/Non/Annuler (même nombre)
    public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, string[] labels)
    {
        using (SoulDialog d = new SoulDialog(text, caption, buttons, icon, labels))
        {
            if (owner == null) { d.StartPosition = FormStartPosition.CenterScreen; d.TopMost = true; return d.ShowDialog(); }
            return d.ShowDialog(owner);
        }
    }
}

class SoulDialog : Form
{
    static readonly Color Yellow = Color.FromArgb(255, 200, 0);
    Color soul;
    string caption, text;
    List<Button> buttons = new List<Button>();
    Rectangle textRect;
    Point dragFrom;
    bool hasIcon;
    int sel;

    void Select(int i) { sel = i; foreach (Button o in buttons) o.Invalidate(); }

    public SoulDialog(string text, string caption, MessageBoxButtons kind, MessageBoxIcon icon, string[] labels)
    {
        this.text = text ?? "";
        this.caption = caption ?? Program.AppName;
        Text = this.caption;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        KeyPreview = true;
        DoubleBuffered = true;
        BackColor = Color.Black;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10f);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        try { Icon = LogoIcons.CurrentIcon(); } catch { }

        // Couleur de l'âme selon le type de message (couleurs des âmes du jeu)
        switch (icon)
        {
            case MessageBoxIcon.Warning: soul = Color.FromArgb(255, 160, 0); break;   // orange
            case MessageBoxIcon.Error: soul = Color.FromArgb(230, 30, 40); break;     // rouge (aussi Hand/Stop)
            case MessageBoxIcon.Question: soul = Color.FromArgb(60, 200, 255); break; // bleu clair
            case MessageBoxIcon.Information: soul = Yellow; break;
            default: soul = Color.FromArgb(230, 30, 40); break;
        }
        hasIcon = true;

        // Taille du texte
        int maxW = 440;
        Size ts;
        using (Graphics g = CreateGraphics())
            ts = TextRenderer.MeasureText(g, this.text, Font, new Size(maxW, 2000), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        int left = hasIcon ? 74 : 26;
        textRect = new Rectangle(left, 50, Math.Max(ts.Width, 220) + 4, ts.Height + 4);

        // Boutons
        string[][] defs;
        switch (kind)
        {
            case MessageBoxButtons.YesNo: defs = new[] { new[] { L.S("Oui", "Yes"), "Yes" }, new[] { L.S("Non", "No"), "No" } }; break;
            case MessageBoxButtons.YesNoCancel: defs = new[] { new[] { L.S("Oui", "Yes"), "Yes" }, new[] { L.S("Non", "No"), "No" }, new[] { L.S("Annuler", "Cancel"), "Cancel" } }; break;
            case MessageBoxButtons.OKCancel: defs = new[] { new[] { "OK", "OK" }, new[] { L.S("Annuler", "Cancel"), "Cancel" } }; break;
            case MessageBoxButtons.RetryCancel: defs = new[] { new[] { L.S("Réessayer", "Retry"), "Retry" }, new[] { L.S("Annuler", "Cancel"), "Cancel" } }; break;
            default: defs = new[] { new[] { "OK", "OK" } }; break;
        }
        if (labels != null && labels.Length == defs.Length)
            for (int i = 0; i < defs.Length; i++) defs[i] = new[] { labels[i], defs[i][1] };
        int bw = 112, bh = 34, gap = 10;
        using (Font bf = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            foreach (string[] d in defs) bw = Math.Max(bw, TextRenderer.MeasureText(d[0], bf).Width + 44);
        int width = Math.Max(textRect.Right + 26, defs.Length * (bw + gap) + 40);
        width = Math.Max(width, 360);
        int by = textRect.Bottom + 22;
        int bx = width - 22 - defs.Length * bw - (defs.Length - 1) * gap;
        foreach (string[] d in defs)
        {
            Button b = new Button { Text = d[0], Location = new Point(bx, by), Size = new Size(bw, bh), FlatStyle = FlatStyle.Flat, BackColor = Color.Black, ForeColor = Color.White, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), DialogResult = (DialogResult)Enum.Parse(typeof(DialogResult), d[1]), TabStop = true };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(25, 25, 25);
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(40, 40, 40);
            b.Paint += PaintButton;
            int idx = buttons.Count;
            b.GotFocus += delegate { Select(idx); };
            b.MouseEnter += delegate { Select(idx); };
            buttons.Add(b);
            Controls.Add(b);
            bx += bw + gap;
        }
        ClientSize = new Size(width, by + bh + 20);
        AcceptButton = buttons[0];
        Button cancel = buttons.Find(x => x.DialogResult == DialogResult.Cancel) ?? buttons.Find(x => x.DialogResult == DialogResult.No) ?? (buttons.Count == 1 ? buttons[0] : null);
        if (cancel != null) CancelButton = cancel;

        // Déplacer la fenêtre en tirant n'importe où
        MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) dragFrom = e.Location; };
        MouseMove += (s, e) => { if (e.Button == MouseButtons.Left) Location = new Point(Location.X + e.X - dragFrom.X, Location.Y + e.Y - dragFrom.Y); };
        Shown += delegate { Activate(); buttons[0].Focus(); Select(0); };
    }

    // Flèches gauche/droite : passer d'un bouton à l'autre, comme dans le jeu
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Left || keyData == Keys.Right)
        {
            int i = (sel + (keyData == Keys.Right ? 1 : buttons.Count - 1)) % buttons.Count;
            buttons[i].Focus();
            Select(i);
            return true;
        }
        if (keyData == Keys.Z || keyData == Keys.Space || keyData == Keys.Enter) { buttons[sel].PerformClick(); return true; }
        if (keyData == Keys.X && CancelButton != null) { ((Button)CancelButton).PerformClick(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    void PaintButton(object sender, PaintEventArgs e)
    {
        Button b = (Button)sender;
        bool on = buttons.IndexOf(b) == sel;
        e.Graphics.Clear(b.BackColor);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Color c = on ? Yellow : Color.White;
        Size ts = TextRenderer.MeasureText(b.Text, b.Font);
        int total = ts.Width + (on ? 20 : 0);
        int x = (b.Width - total) / 2;
        if (on)
        {
            using (SolidBrush br = new SolidBrush(Color.FromArgb(230, 30, 40))) e.Graphics.FillPath(br, Heart(new RectangleF(x, b.Height / 2 - 7, 14, 13)));
            x += 20;
        }
        TextRenderer.DrawText(e.Graphics, b.Text, b.Font, new Point(x, (b.Height - ts.Height) / 2), c);
    }

    public static GraphicsPath Heart(RectangleF r)
    {
        GraphicsPath p = new GraphicsPath();
        float w = r.Width, h = r.Height, x = r.X, y = r.Y;
        p.AddBezier(x + w / 2, y + h * 0.28f, x + w / 2, y, x, y, x, y + h * 0.32f);
        p.AddBezier(x, y + h * 0.32f, x, y + h * 0.62f, x + w * 0.35f, y + h * 0.78f, x + w / 2, y + h);
        p.AddBezier(x + w / 2, y + h, x + w * 0.65f, y + h * 0.78f, x + w, y + h * 0.62f, x + w, y + h * 0.32f);
        p.AddBezier(x + w, y + h * 0.32f, x + w, y, x + w / 2, y, x + w / 2, y + h * 0.28f);
        p.CloseFigure();
        return p;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Color.Black);
        // Double cadre blanc façon boîte de texte du jeu
        using (Pen p = new Pen(Color.White, 4)) g.DrawRectangle(p, 2, 2, ClientSize.Width - 5, ClientSize.Height - 5);
        TextRenderer.DrawText(g, caption.ToUpper(), new Font("Segoe UI", 9f, FontStyle.Bold), new Point(22, 16), Yellow);
        using (Pen p = new Pen(Color.FromArgb(60, 60, 60))) g.DrawLine(p, 18, 40, ClientSize.Width - 18, 40);
        if (hasIcon)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush br = new SolidBrush(soul)) g.FillPath(br, Heart(new RectangleF(26, 54, 30, 27)));
        }
        TextRenderer.DrawText(g, text, Font, textRect, Color.White, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
    }
}
