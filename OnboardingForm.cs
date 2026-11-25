using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

class OnboardingForm : Form
{
    public enum Mode { Full, WarningOnly, LogoOnly }
    Panel page1, page2;
    LogoTile[] tiles;
    Button bOk;
    Label lblChoice;
    Mode mode;
    public int ChosenLogo = -1;

    public OnboardingForm(Mode m)
    {
        mode = m;
        Text = Program.AppName + " — " + (m == Mode.LogoOnly ? "Logo" : L.S("Bienvenue", "Welcome"));
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false; MinimizeBox = false;
        StartPosition = m == Mode.Full ? FormStartPosition.CenterScreen : FormStartPosition.CenterParent;
        ClientSize = new Size(520, 560);
        BackColor = Color.Black; ForeColor = Color.White;
        try { Icon = LogoIcons.CurrentIcon() ?? Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        // ---- Page 1 : avertissement ----
        page1 = new Panel { Dock = DockStyle.Fill };
        page1.Controls.Add(new Label { Text = L.S("BIENVENUE DANS SOUL LAUNCHER", "WELCOME TO SOUL LAUNCHER"), Location = new Point(30, 28), AutoSize = true, ForeColor = Color.FromArgb(255, 200, 0), Font = new Font("Segoe UI", 15f, FontStyle.Bold) });
        page1.Controls.Add(new Label
        {
            Location = new Point(30, 76), Size = new Size(460, 300),
            Text = L.S(
                "Soul Launcher est un outil non officiel, fait par un fan.\n\n" +
                "Il n'est ni affilié à, ni approuvé, ni sponsorisé par Toby Fox ou les ayants droit d'UNDERTALE. " +
                "UNDERTALE et ses personnages appartiennent à Toby Fox.\n\n" +
                "Soul Launcher ne contient aucun fichier du jeu : il faut posséder UNDERTALE. Il écrit seulement des sauvegardes " +
                "dans le dossier de saves d'UNDERTALE, puis lance le jeu installé sur ton PC. Ta vraie sauvegarde est copiée " +
                "avant d'être remplacée, et le bouton « Restaurer ma vraie sauvegarde » la remet.\n\n" +
                "Presets intégrés : Flowey's Time Machine (crumblingstatue). Packs : saves partagées par la communauté (auteurs crédités dans l'aperçu).",
                "Soul Launcher is an unofficial fan-made tool.\n\n" +
                "It is not affiliated with, endorsed or sponsored by Toby Fox or the UNDERTALE rights holders. " +
                "UNDERTALE and its characters belong to Toby Fox.\n\n" +
                "Soul Launcher contains no game files: you need to own UNDERTALE. It only writes save files " +
                "to UNDERTALE's save folder, then starts the game installed on your PC. Your real save is copied " +
                "before being replaced, and the \"Restore my real save\" button puts it back.\n\n" +
                "Built-in presets: Flowey's Time Machine (crumblingstatue). Packs: saves shared by the community (authors credited in the preview).")
        });
        CheckBox chk = new CheckBox { Text = L.S("J'ai lu et compris", "I have read and understood"), Location = new Point(30, 390), AutoSize = true, Checked = m != Mode.Full };
        page1.Controls.Add(chk);
        Button bNext = MakeButton(m == Mode.Full ? L.S("Suivant  ›", "Next  ›") : L.S("Fermer", "Close"), new Point(350, 490), new Size(140, 40), true);
        bNext.Enabled = chk.Checked;
        chk.CheckedChanged += delegate { bNext.Enabled = chk.Checked; };
        bNext.Click += delegate
        {
            if (mode == Mode.WarningOnly) { DialogResult = DialogResult.OK; Close(); return; }
            page1.Visible = false; page2.Visible = true; tiles[Math.Max(0, LogoIcons.Chosen())].Focus();
        };
        page1.Controls.Add(bNext);
        if (m == Mode.Full)
        {
            Button bQuit = MakeButton(L.S("Quitter", "Quit"), new Point(30, 490), new Size(110, 40), false);
            bQuit.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            page1.Controls.Add(bQuit);
        }

        // ---- Page 2 : choix du logo ----
        page2 = new Panel { Dock = DockStyle.Fill, Visible = m == Mode.LogoOnly };
        page1.Visible = m != Mode.LogoOnly;
        page2.Controls.Add(new Label { Text = L.S("CHOISIS TON LOGO", "PICK YOUR LOGO"), Location = new Point(30, 20), AutoSize = true, ForeColor = Color.FromArgb(255, 200, 0), Font = new Font("Segoe UI", 16f, FontStyle.Bold) });
        page2.Controls.Add(new Label { Text = L.S("Utilisé pour la fenêtre et les raccourcis. Modifiable dans les Réglages.", "Used for the window and shortcuts. You can change it in Settings."), Location = new Point(30, 58), AutoSize = true, ForeColor = Color.Gray });
        tiles = new LogoTile[Logos.Png.Length];
        for (int i = 0; i < tiles.Length; i++)
        {
            LogoTile t = new LogoTile(i) { Location = new Point(40 + (i % 5) * 88, 86 + (i / 5) * 88) };
            t.Click += delegate { Pick(t.Index); };
            t.DoubleClick += delegate { Pick(t.Index); Finish(); };
            tiles[i] = t;
            page2.Controls.Add(t);
        }
        lblChoice = new Label { Location = new Point(30, 448), Size = new Size(460, 26), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 11f, FontStyle.Bold), Text = L.S("Clique sur un logo", "Click a logo") };
        page2.Controls.Add(lblChoice);
        bOk = MakeButton(L.S("Valider ce logo", "Use this logo"), new Point(310, 490), new Size(180, 40), true);
        bOk.Enabled = false;
        bOk.Click += delegate { Finish(); };
        page2.Controls.Add(bOk);
        Button bBack = MakeButton(m == Mode.LogoOnly ? L.S("Annuler", "Cancel") : L.S("‹  Retour", "‹  Back"), new Point(30, 490), new Size(110, 40), false);
        bBack.Click += delegate
        {
            if (mode == Mode.LogoOnly) { DialogResult = DialogResult.Cancel; Close(); return; }
            page2.Visible = false; page1.Visible = true;
        };
        page2.Controls.Add(bBack);

        Controls.Add(page2);
        Controls.Add(page1);
        if (m == Mode.LogoOnly && LogoIcons.Chosen() >= 0) Shown += delegate { Pick(LogoIcons.Chosen()); };
    }

    Button MakeButton(string text, Point loc, Size size, bool primary)
    {
        Button b = new Button { Text = text, Location = loc, Size = size, FlatStyle = FlatStyle.Flat };
        b.BackColor = primary ? Color.FromArgb(255, 200, 0) : Color.FromArgb(30, 30, 30);
        b.ForeColor = primary ? Color.Black : Color.White;
        b.Font = new Font("Segoe UI", 10f, primary ? FontStyle.Bold : FontStyle.Regular);
        b.FlatAppearance.BorderColor = Color.FromArgb(90, 90, 90);
        b.FlatAppearance.BorderSize = primary ? 0 : 1;
        b.EnabledChanged += delegate { if (primary) b.BackColor = b.Enabled ? Color.FromArgb(255, 200, 0) : Color.FromArgb(70, 60, 20); };
        return b;
    }

    void Pick(int i)
    {
        ChosenLogo = i;
        foreach (LogoTile t in tiles) { t.Selected = t.Index == i; t.Invalidate(); }
        lblChoice.Text = (i + 1) + ". " + Logos.Names[i];
        bOk.Enabled = true;
        try { Icon = new Icon(PreviewIco(i)); } catch { }
    }

    string PreviewIco(int i)
    {
        string p = Path.Combine(Path.GetTempPath(), "soul_launcher_logo_preview.ico");
        LogoIcons.SaveIco(i, p);
        return p;
    }

    void Finish()
    {
        if (ChosenLogo < 0) return;
        LogoIcons.SaveIco(ChosenLogo, LogoIcons.IcoPath);
        Store.SetSetting("logo", ChosenLogo.ToString());
        Store.SetSetting("onboarded", "1");
        DialogResult = DialogResult.OK;
        Close();
    }
}
