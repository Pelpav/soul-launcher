using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

class MainForm : Form
{
    static readonly Color Yellow = Color.FromArgb(255, 200, 0);
    List<SaveState> presets = new List<SaveState>();
    List<SaveState> allPresets = new List<SaveState>();
    SaveState current, selected;
    bool loading, suppressSelect;
    int lastIndex = -1;

    TextBox txtGame, txtName, txtSearch;
    Label lblCount, lblBonus, lblInfo;
    Button launchButton, bFav;
    ListBox lstPresets;
    NumericUpDown numLv, numHp, numAt, numDf, numGold, numExp;
    ComboBox cboWeapon, cboArmor, cboRoom, cboFill;
    ComboBox[] cboItems = new ComboBox[8];
    PreviewPanel preview;
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);

    static readonly int[] ExpForLv = { 0, 10, 30, 70, 120, 200, 300, 500, 800, 1200, 1700, 2500, 3500, 5000, 7000, 10000, 15000, 25000, 50000, 99999 };

    public MainForm(string importOnStart)
    {
        Text = Program.AppName + "  " + Updater.CurrentText;
        Font = new Font("Segoe UI", 9f);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1180, 600);
        BackColor = Color.Black;
        ForeColor = Color.White;
        AllowDrop = true;

        // Chemin du jeu
        Controls.Add(Lbl("UNDERTALE.exe :", 12, 15));
        txtGame = new TextBox { Location = new Point(118, 12), Width = 720, BackColor = Color.FromArgb(25, 25, 25), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
        txtGame.Text = GameFinder.Resolve() ?? "";
        txtGame.Leave += delegate { if (GameFinder.IsGame(txtGame.Text)) Store.SetSetting("game", txtGame.Text); };
        Controls.Add(txtGame);
        Button bDetect = Btn(L.S("Détecter", "Detect"), 846, 10, 84, 26);
        bDetect.Click += delegate { DetectGame(true); };
        Controls.Add(bDetect);
        Button bBrowse = Btn(L.S("Parcourir…", "Browse…"), 936, 10, 90, 26);
        bBrowse.Click += delegate { BrowseGame(); };
        Controls.Add(bBrowse);
        Button bSettings = Btn("⚙  " + L.S("Réglages", "Settings"), 1036, 10, 132, 26);
        bSettings.Click += delegate { using (SettingsForm f = new SettingsForm(this)) f.ShowDialog(this); };
        Controls.Add(bSettings);

        // Liste des presets
        Controls.Add(Lbl(L.S("POINT DE DÉPART", "STARTING POINT"), 12, 52, true));
        lblCount = Lbl("", 180, 52); lblCount.AutoSize = false; lblCount.Size = new Size(122, 18); lblCount.TextAlign = ContentAlignment.TopRight; lblCount.ForeColor = Color.Gray; Controls.Add(lblCount);
        txtSearch = new TextBox { Location = new Point(12, 74), Width = 254, BackColor = Color.FromArgb(25, 25, 25), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
        txtSearch.HandleCreated += delegate { SendMessage(txtSearch.Handle, 0x1501, (IntPtr)1, L.S("🔍 Rechercher (Sans, Mettaton, génocide…)", "🔍 Search (Sans, Mettaton, genocide…)")); };
        Timer searchTimer = new Timer { Interval = 150 };
        searchTimer.Tick += delegate { searchTimer.Stop(); ApplyFilter(null); };
        txtSearch.TextChanged += delegate { searchTimer.Stop(); searchTimer.Start(); };
        txtSearch.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape) { txtSearch.Clear(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Enter) { lstPresets.Focus(); e.SuppressKeyPress = true; }
        };
        txtSearch.Enter += delegate { AcceptButton = null; };
        txtSearch.Leave += delegate { AcceptButton = launchButton; };
        Controls.Add(txtSearch);
        Button bRandom = Btn("🎲", 270, 73, 32, 23);
        bRandom.Padding = Padding.Empty;
        new ToolTip().SetToolTip(bRandom, L.S("Preset au hasard", "Random preset"));
        bRandom.Click += delegate { PickRandom(); };
        Controls.Add(bRandom);
        lstPresets = new ListBox { Location = new Point(12, 100), Size = new Size(290, 292), BackColor = Color.FromArgb(15, 15, 15), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, IntegralHeight = false, ItemHeight = 26, DrawMode = DrawMode.OwnerDrawFixed };
        lstPresets.DrawItem += DrawPresetItem;
        lstPresets.SelectedIndexChanged += delegate { OnPresetSelected(); };
        lstPresets.DoubleClick += delegate { Launch(); };
        Controls.Add(lstPresets);
        CheckBox chkGeno = new CheckBox { Text = L.S("Annuler la fin génocide (monde effacé)", "Undo the genocide ending (erased world)"), Location = new Point(12, 398), AutoSize = true, Checked = true, ForeColor = Color.White };
        chkGeno.CheckedChanged += delegate { Store.ClearGenocideOnLaunch = chkGeno.Checked; };
        Controls.Add(chkGeno);
        Button bImport = Btn("⬇  " + L.S("Importer ma partie actuelle", "Import my current game"), 12, 424, 290, 30);
        bImport.BackColor = Color.FromArgb(40, 34, 0);
        bImport.FlatAppearance.BorderColor = Yellow;
        bImport.Click += delegate { ImportCurrentGame(); };
        Controls.Add(bImport);
        Button bSaveCustom = Btn(L.S("Enregistrer comme preset perso", "Save as my own preset"), 12, 460, 290, 28);
        bSaveCustom.Click += delegate { SaveCustom(); };
        Controls.Add(bSaveCustom);
        Button bDelCustom = Btn(L.S("Supprimer le preset perso", "Delete my preset"), 12, 492, 290, 28);
        bDelCustom.Click += delegate { DeleteCustom(); };
        Controls.Add(bDelCustom);

        // Personnage
        int x = 322;
        Controls.Add(Lbl(L.S("PERSONNAGE", "CHARACTER"), x, 52, true));
        Controls.Add(Lbl(L.S("Nom", "Name"), x, 80));
        txtName = new TextBox { Location = new Point(x + 70, 77), Width = 120, MaxLength = 12, BackColor = Color.FromArgb(25, 25, 25), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
        txtName.TextChanged += delegate { Apply(); };
        Controls.Add(txtName);
        numLv = Num("LV", x, 108, 1, 20);
        numHp = Num(L.S("PV max", "Max HP"), x, 136, 1, 9999);
        numAt = Num(L.S("ATQ", "AT"), x, 164, 0, 999);
        numDf = Num(L.S("DEF", "DF"), x, 192, 0, 999);
        numExp = Num("EXP", x, 220, 0, 99999);
        numGold = Num(L.S("OR", "GOLD"), x, 248, 0, 99999);
        numLv.ValueChanged += delegate { if (!loading && numExp.Value < ExpForLv[(int)numLv.Value - 1]) numExp.Value = ExpForLv[(int)numLv.Value - 1]; };

        Controls.Add(Lbl(L.S("ÉQUIPEMENT", "EQUIPMENT"), x, 284, true));
        Controls.Add(Lbl(L.S("Arme", "Weapon"), x, 312)); cboWeapon = Cbo(x + 70, 309, 190);
        Controls.Add(Lbl(L.S("Armure", "Armor"), x, 342)); cboArmor = Cbo(x + 70, 339, 190);
        lblBonus = Lbl("", x, 370); lblBonus.ForeColor = Color.Gray; Controls.Add(lblBonus);
        foreach (int id in GameData.WeaponsIds) cboWeapon.Items.Add(new Opt(id, GameData.Items[id]));
        foreach (int id in GameData.ArmorsIds) cboArmor.Items.Add(new Opt(id, GameData.Items[id]));

        Controls.Add(Lbl(L.S("LIEU DE DÉPART", "STARTING ROOM"), x, 400, true));
        cboRoom = Cbo(x, 424, 260);
        CheckBox chkFull = new CheckBox { Text = L.S("Plein écran", "Fullscreen"), Location = new Point(x, 450), AutoSize = true, ForeColor = Color.White, Checked = Store.GetSetting("fullscreen") == "1" };
        Store.Fullscreen = chkFull.Checked;
        chkFull.CheckedChanged += delegate { Store.Fullscreen = chkFull.Checked; Store.SetSetting("fullscreen", chkFull.Checked ? "1" : "0"); };
        Controls.Add(chkFull);

        // Inventaire
        int x2 = 612;
        Controls.Add(Lbl(L.S("INVENTAIRE", "INVENTORY"), x2, 52, true));
        for (int i = 0; i < 8; i++)
        {
            Controls.Add(Lbl((i + 1).ToString(), x2, 80 + i * 30));
            cboItems[i] = Cbo(x2 + 22, 77 + i * 30, 254);
            for (int id = 0; id < GameData.Items.Length; id++) cboItems[i].Items.Add(new Opt(id, id == 0 ? L.S("— vide —", "— empty —") : GameData.Items[id]));
        }
        Controls.Add(Lbl(L.S("Remplir avec :", "Fill with:"), x2, 326));
        cboFill = Cbo(x2 + 95, 323, 181);
        foreach (int id in new[] { 11, 43, 40, 21, 61, 35, 42 }) cboFill.Items.Add(new Opt(id, GameData.Items[id]));
        cboFill.SelectedIndex = 0;
        Button bFill = Btn(L.S("Remplir l'inventaire", "Fill the inventory"), x2, 352, 276, 28);
        bFill.Click += delegate { int id = ((Opt)cboFill.SelectedItem).Id; foreach (var c in cboItems) SelectOpt(c, id); };
        Controls.Add(bFill);
        Button bMax = Btn(L.S("Stats max (LV 20, PV 999, ATQ/DEF 99)", "Max stats (LV 20, HP 999, AT/DF 99)"), x2, 386, 276, 28);
        bMax.Click += delegate { loading = true; numLv.Value = 20; numExp.Value = 99999; numHp.Value = 999; numAt.Value = 99; numDf.Value = 99; loading = false; Apply(); };
        Controls.Add(bMax);
        Button bGod = Btn(L.S("Équipement ultime (Real Knife + Locket)", "Best gear (Real Knife + Locket)"), x2, 420, 276, 28);
        bGod.Click += delegate { SelectOpt(cboWeapon, 52); SelectOpt(cboArmor, 53); };
        Controls.Add(bGod);

        // Aperçu
        int x3 = 904;
        Controls.Add(Lbl(L.S("APERÇU", "PREVIEW"), x3, 52, true));
        preview = new PreviewPanel { Location = new Point(x3, 74), Size = new Size(264, 346) };
        Controls.Add(preview);
        bFav = Btn("☆  " + L.S("Ajouter aux favoris", "Add to favorites"), x3, 428, 264, 28);
        bFav.Click += delegate { ToggleFavorite(); };
        Controls.Add(bFav);
        Button bExport = Btn("📤  " + L.S("Partager (.soul)", "Share (.soul)"), x3, 462, 130, 30);
        bExport.Click += delegate { ExportSoul(); };
        Controls.Add(bExport);
        Button bImportSoul = Btn("📥  " + L.S("Ouvrir .soul", "Open .soul"), x3 + 134, 462, 130, 30);
        bImportSoul.Click += delegate { OpenSoulDialog(); };
        Controls.Add(bImportSoul);
        Label dropHint = Lbl(L.S("Astuce : glisse un fichier .soul sur la fenêtre", "Tip: drop a .soul file on the window"), x3, 498); dropHint.ForeColor = Color.Gray; dropHint.AutoSize = false; dropHint.Size = new Size(264, 30); Controls.Add(dropHint);

        // Lancer
        Button bLaunch = new Button { Text = "★  " + L.S("LANCER UNDERTALE", "START UNDERTALE"), Location = new Point(322, 470), Size = new Size(566, 52), FlatStyle = FlatStyle.Flat, BackColor = Yellow, ForeColor = Color.Black, Font = new Font("Segoe UI", 13f, FontStyle.Bold) };
        bLaunch.FlatAppearance.BorderSize = 0;
        bLaunch.Click += delegate { Launch(); };
        Controls.Add(bLaunch);
        AcceptButton = bLaunch;
        launchButton = bLaunch;

        Button bRestore = Btn(L.S("Restaurer ma vraie sauvegarde", "Restore my real save"), 12, 540, 290, 28);
        bRestore.Click += delegate { Restore(); };
        Controls.Add(bRestore);
        Button bOpen = Btn(L.S("Ouvrir le dossier des saves", "Open the save folder"), 322, 540, 200, 28);
        bOpen.Click += delegate { Directory.CreateDirectory(Store.SaveDir); Process.Start("explorer.exe", Store.SaveDir); };
        Controls.Add(bOpen);
        Button bShortcut = Btn(L.S("Raccourci menu Démarrer", "Start menu shortcut"), 530, 540, 240, 28);
        bShortcut.Click += delegate { MakeShortcut(); };
        Controls.Add(bShortcut);
        lblInfo = Lbl("", 12, 576); lblInfo.ForeColor = Color.Gray; lblInfo.AutoSize = false; lblInfo.Size = new Size(1156, 20); Controls.Add(lblInfo);

        foreach (Control c in Controls)
        {
            NumericUpDown n = c as NumericUpDown;
            if (n != null) n.ValueChanged += delegate { Apply(); };
        }
        cboWeapon.SelectedIndexChanged += delegate { Apply(); };
        cboArmor.SelectedIndexChanged += delegate { Apply(); };
        cboRoom.SelectedIndexChanged += delegate { Apply(); };
        foreach (var c in cboItems) c.SelectedIndexChanged += delegate { Apply(); };

        DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
        DragDrop += (s, e) =>
        {
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null) foreach (string f in files) ImportSoul(f);
        };

        RefreshIcon();
        ReloadPresets(null);
        KeyPreview = true;
        Shown += delegate
        {
            txtSearch.Focus();
            if (!GameFinder.IsGame(txtGame.Text)) lblInfo.Text = L.S("UNDERTALE pas trouvé automatiquement : clique « Détecter » ou « Parcourir… ».", "UNDERTALE not found automatically: click \"Detect\" or \"Browse…\".");
            if (importOnStart != null) ImportSoul(importOnStart);
            Updater.CheckAtStartup(this);
        };
    }

    public void RefreshIcon()
    {
        try { Icon = LogoIcons.CurrentIcon() ?? Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.F)) { txtSearch.Focus(); txtSearch.SelectAll(); return true; }
        if (keyData == (Keys.Control | Keys.D)) { ToggleFavorite(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ---------- UI helpers ----------
    class Opt
    {
        public int Id; public string Name;
        public Opt(int id, string name) { Id = id; Name = name; }
        public override string ToString() { return Name; }
    }

    Label Lbl(string t, int x, int y, bool header = false)
    {
        Label l = new Label { Text = t, Location = new Point(x, y), AutoSize = true, ForeColor = header ? Yellow : Color.White };
        if (header) l.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        return l;
    }

    Button Btn(string t, int x, int y, int w, int h)
    {
        Button b = new Button { Text = t, Location = new Point(x, y), Size = new Size(w, h), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.White };
        b.FlatAppearance.BorderColor = Color.FromArgb(90, 90, 90);
        return b;
    }

    NumericUpDown Num(string label, int x, int y, int min, int max)
    {
        Controls.Add(Lbl(label, x, y + 3));
        NumericUpDown n = new NumericUpDown { Location = new Point(x + 70, y), Width = 120, Minimum = min, Maximum = max, BackColor = Color.FromArgb(25, 25, 25), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
        Controls.Add(n);
        return n;
    }

    ComboBox Cbo(int x, int y, int w)
    {
        ComboBox c = new ComboBox { Location = new Point(x, y), Width = w, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(25, 25, 25), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, MaxDropDownItems = 20 };
        Controls.Add(c);
        return c;
    }

    void SelectOpt(ComboBox c, int id)
    {
        for (int i = 0; i < c.Items.Count; i++)
            if (((Opt)c.Items[i]).Id == id) { c.SelectedIndex = i; return; }
        c.Items.Add(new Opt(id, id >= 0 && id < GameData.Items.Length ? GameData.Items[id] : ("#" + id)));
        c.SelectedIndex = c.Items.Count - 1;
    }

    void DrawPresetItem(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= presets.Count) return;
        SaveState p = presets[e.Index];
        if (p.IsHeader)
        {
            using (var hb = new SolidBrush(Color.FromArgb(28, 28, 28))) e.Graphics.FillRectangle(hb, e.Bounds);
            using (var hf = new Font(Font, FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, p.Label.ToUpper(), hf, new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height), Yellow, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            return;
        }
        bool sel = (e.State & DrawItemState.Selected) != 0;
        using (var b = new SolidBrush(sel ? Yellow : Color.FromArgb(15, 15, 15))) e.Graphics.FillRectangle(b, e.Bounds);
        string t = (sel ? "♥ " : "   ") + p.Display;
        TextRenderer.DrawText(e.Graphics, t, Font, new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 24, e.Bounds.Height), sel ? Color.Black : Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        if (favs.Contains(p.Key))
            TextRenderer.DrawText(e.Graphics, "★", Font, new Rectangle(e.Bounds.Right - 20, e.Bounds.Y, 18, e.Bounds.Height), sel ? Color.Black : Yellow, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
    }

    // ---------- Liste ----------
    HashSet<string> favs = new HashSet<string>();

    void ReloadPresets(string selectKey)
    {
        favs = new HashSet<string>(Store.GetList("fav"));
        var builtins = Store.BuiltIns();
        var mine = Store.Customs();
        var packs = Store.Packs();
        var byKey = new Dictionary<string, SaveState>();
        foreach (var s in builtins.Concat(mine).Concat(packs.SelectMany(c => c.Value))) if (!byKey.ContainsKey(s.Key)) byKey[s.Key] = s;

        allPresets = new List<SaveState>();
        var favList = Store.GetList("fav").Where(byKey.ContainsKey).Select(k => byKey[k]).ToList();
        if (favList.Count > 0) { allPresets.Add(SaveState.Header("★ " + L.S("Favoris", "Favorites"))); allPresets.AddRange(favList); }
        var rec = Store.GetList("recent").Where(byKey.ContainsKey).Select(k => byKey[k]).ToList();
        if (rec.Count > 0) { allPresets.Add(SaveState.Header(L.S("Récents", "Recent"))); allPresets.AddRange(rec); }
        allPresets.Add(SaveState.Header(L.S("Intégrés", "Built-in")));
        allPresets.AddRange(builtins);
        if (mine.Count > 0) { allPresets.Add(SaveState.Header(L.S("Mes saves", "My saves"))); allPresets.AddRange(mine); }
        foreach (var cat in packs) { allPresets.Add(SaveState.Header("Pack · " + L.Category(cat.Key))); allPresets.AddRange(cat.Value); }
        totalCount = byKey.Count;
        ApplyFilter(selectKey ?? (current != null ? current.Key : "sans"));
    }

    int totalCount;

    static string Fold(string s)
    {
        StringBuilder sb = new StringBuilder();
        foreach (char c in (s ?? "").Normalize(NormalizationForm.FormD))
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().ToLowerInvariant();
    }

    void ApplyFilter(string selectKey)
    {
        string[] words = Fold(txtSearch.Text).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        presets = new List<SaveState>();
        SaveState header = null;
        bool headerAdded = false;
        var seen = new HashSet<string>();
        foreach (SaveState p in allPresets)
        {
            if (p.IsHeader) { header = p; headerAdded = false; continue; }
            string hay = Fold(p.Label + " " + p.LabelEn + " " + (header != null ? header.Label : "") + " " + p.Category + " " + L.Category(p.Category ?? "") + " " + p.Route() + " " + p.RoomName());
            if (!words.All(w => hay.Contains(w))) continue;
            // Pendant une recherche, un même preset (favori + catégorie) n'apparaît qu'une fois
            if (words.Length > 0 && !seen.Add(p.Key)) continue;
            if (header != null && !headerAdded) { presets.Add(header); headerAdded = true; }
            presets.Add(p);
        }
        int matches = words.Length == 0 ? totalCount : seen.Count;
        lstPresets.BeginUpdate();
        lstPresets.Items.Clear();
        foreach (var p in presets) lstPresets.Items.Add(p.Label);
        lstPresets.EndUpdate();
        lblCount.Text = words.Length == 0 ? matches + " presets" : (matches == 0 ? L.S("Aucun résultat", "No results") : matches + L.S(" trouvé" + (matches > 1 ? "s" : ""), " found"));
        lastIndex = -1;
        if (presets.Count == 0) return;
        // En liste complète, on resélectionne dans la catégorie d'origine plutôt que dans Favoris/Récents
        int idx = -1;
        if (selectKey != null)
        {
            idx = presets.FindLastIndex(p => p.Key == selectKey);
        }
        if (idx < 0) idx = presets.FindIndex(p => !p.IsHeader);
        if (idx < 0) return;
        suppressSelect = current != null && presets[idx].Key == current.Key;
        lstPresets.SelectedIndex = idx;
        suppressSelect = false;
        lastIndex = idx;
        UpdateFavButton();
    }

    void OnPresetSelected()
    {
        int i = lstPresets.SelectedIndex;
        if (i < 0) return;
        if (presets[i].IsHeader)
        {
            int dir = i < lastIndex ? -1 : 1;
            int j = i + dir;
            while (j >= 0 && j < presets.Count && presets[j].IsHeader) j += dir;
            if (j < 0 || j >= presets.Count) { j = i - dir; while (j >= 0 && j < presets.Count && presets[j].IsHeader) j -= dir; }
            if (j >= 0 && j < presets.Count) lstPresets.SelectedIndex = j;
            return;
        }
        lastIndex = i;
        if (!suppressSelect) LoadPreset(presets[i]);
    }

    static readonly Random rng = new Random();

    // Choisit un preset au hasard parmi ceux affichés (respecte la recherche)
    void PickRandom()
    {
        var idx = Enumerable.Range(0, presets.Count).Where(i => !presets[i].IsHeader).ToList();
        if (idx.Count == 0) return;
        int pick = idx[rng.Next(idx.Count)];
        if (idx.Count > 1 && pick == lstPresets.SelectedIndex) pick = idx[(idx.IndexOf(pick) + 1) % idx.Count];
        lstPresets.SelectedIndex = pick;
        lstPresets.Focus();
    }

    void UpdateFavButton()
    {
        bool f = current != null && favs.Contains(current.Key);
        bFav.Text = f ? "★  " + L.S("Retirer des favoris", "Remove from favorites") : "☆  " + L.S("Ajouter aux favoris", "Add to favorites");
        bFav.ForeColor = f ? Yellow : Color.White;
    }

    void ToggleFavorite()
    {
        if (current == null || string.IsNullOrEmpty(current.Key)) return;
        Store.ToggleFavorite(current.Key);
        string k = current.Key;
        ReloadPresets(k);
        lblInfo.Text = favs.Contains(k) ? L.S("Ajouté aux favoris (en haut de la liste). Ctrl+D pour basculer.", "Added to favorites (top of the list). Ctrl+D to toggle.") : L.S("Retiré des favoris.", "Removed from favorites.");
    }

    // ---------- Presets ----------
    void LoadPreset(SaveState p)
    {
        loading = true;
        selected = p;
        current = p.Clone();
        txtName.Text = current.Lines[0];
        numLv.Value = Clamp(current.Get(2), numLv);
        numHp.Value = Clamp(current.Get(3), numHp);
        numAt.Value = Clamp(current.Get(5), numAt);
        numDf.Value = Clamp(current.Get(7), numDf);
        numExp.Value = Clamp(current.Get(10), numExp);
        numGold.Value = Clamp(current.Get(11), numGold);
        SelectOpt(cboWeapon, current.Get(29));
        SelectOpt(cboArmor, current.Get(30));
        for (int i = 0; i < 8; i++) SelectOpt(cboItems[i], current.Get(13 + 2 * i));

        cboRoom.Items.Clear();
        int room = current.Get(548);
        bool found = false;
        for (int i = 0; i < GameData.Rooms.Length; i++)
        {
            if (GameData.Rooms[i].Contains("[SAVE]") || i == room)
            {
                cboRoom.Items.Add(new Opt(i, i + " — " + GameData.Rooms[i].Replace(" [SAVE]", "").Replace(" [Dogchecked]", "") + (i == room ? "  (preset)" : "")));
                if (i == room) { cboRoom.SelectedIndex = cboRoom.Items.Count - 1; found = true; }
            }
        }
        if (!found) SelectOpt(cboRoom, room);
        loading = false;
        Apply();
        UpdateFavButton();
        lblInfo.Text = p.Custom ? L.S("Ta save. Modifie les champs puis lance.", "Your save. Edit the fields, then start.")
            : p.BuiltIn ? L.S("Preset intégré (Flowey's Time Machine). Modifie les champs puis lance.", "Built-in preset (Flowey's Time Machine). Edit the fields, then start.")
            : L.S("Pack communautaire. Modifie les champs puis lance.", "Community pack. Edit the fields, then start.");
        if (Store.GenocideEnded()) lblInfo.Text = L.S("⚠ Fin génocide détectée (monde effacé) : écran noir au démarrage si la case n'est pas cochée.", "⚠ Genocide ending detected (erased world): black screen on start unless the box is ticked.");
    }

    static decimal Clamp(int v, NumericUpDown n) { return Math.Max(n.Minimum, Math.Min(n.Maximum, v)); }
    static int Lookup(int[] ids, int[] vals, int id) { int i = Array.IndexOf(ids, id); return i >= 0 ? vals[i] : 0; }

    void Apply()
    {
        if (loading || current == null) return;
        string name = txtName.Text.Trim();
        current.Lines[0] = name.Length > 0 ? name : "Frisk";
        current.Set(2, (int)numLv.Value);
        current.Set(3, (int)numHp.Value);
        current.Set(5, (int)numAt.Value);
        current.Set(7, (int)numDf.Value);
        current.Set(10, (int)numExp.Value);
        current.Set(11, (int)numGold.Value);
        if (cboWeapon.SelectedItem != null && cboArmor.SelectedItem != null)
        {
            int w = ((Opt)cboWeapon.SelectedItem).Id, a = ((Opt)cboArmor.SelectedItem).Id;
            current.Set(29, w);
            current.Set(30, a);
            int wat = Lookup(GameData.WeaponsIds, GameData.WeaponAtk, w) + Lookup(GameData.ArmorsIds, GameData.ArmorAtk, a);
            int adf = Lookup(GameData.ArmorsIds, GameData.ArmorDef, a);
            current.Set(6, wat);
            current.Set(8, adf);
            lblBonus.Text = L.S("Bonus équipement : ATQ +", "Gear bonus: AT +") + wat + L.S("   DEF +", "   DF +") + adf;
        }
        for (int i = 0; i < 8; i++)
            if (cboItems[i].SelectedItem != null) current.Set(13 + 2 * i, ((Opt)cboItems[i].SelectedItem).Id);
        if (cboRoom.SelectedItem != null) current.Set(548, ((Opt)cboRoom.SelectedItem).Id);
        current.SyncIni();
        preview.SetState(current);
    }

    // ---------- Jeu ----------
    void DetectGame(bool deep)
    {
        Cursor = Cursors.WaitCursor;
        List<string> found;
        try
        {
            found = GameFinder.KnownLocations();
            if (found.Count == 0 && deep) { lblInfo.Text = L.S("Recherche dans tes dossiers…", "Searching your folders…"); Application.DoEvents(); found = GameFinder.DeepSearch(); }
        }
        finally { Cursor = Cursors.Default; }
        if (found.Count == 0)
        {
            Dlg.Show(this, L.S("UNDERTALE n'a pas été trouvé (Steam, GOG, Xbox, dossiers habituels).\nIndique UNDERTALE.exe avec « Parcourir… ».", "UNDERTALE was not found (Steam, GOG, Xbox, usual folders).\nPick UNDERTALE.exe with \"Browse…\"."), Text);
            BrowseGame();
            return;
        }
        string pick = found.Count == 1 ? found[0] : Choose(L.S("Plusieurs copies trouvées : laquelle utiliser ?", "Several copies found: which one?"), found);
        if (pick == null) return;
        txtGame.Text = pick;
        Store.SetSetting("game", pick);
        lblInfo.Text = L.S("Jeu trouvé : ", "Game found: ") + pick;
    }

    string Choose(string title, List<string> items)
    {
        using (Form f = new Form { Text = title, ClientSize = new Size(560, 220), FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false, Font = Font })
        {
            ListBox lb = new ListBox { Location = new Point(12, 12), Size = new Size(536, 160) };
            foreach (string s in items) lb.Items.Add(s);
            lb.SelectedIndex = 0;
            Button ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(392, 182), Width = 75 };
            Button cancel = new Button { Text = L.S("Annuler", "Cancel"), DialogResult = DialogResult.Cancel, Location = new Point(473, 182), Width = 75 };
            lb.DoubleClick += delegate { f.DialogResult = DialogResult.OK; };
            f.Controls.AddRange(new Control[] { lb, ok, cancel });
            f.AcceptButton = ok; f.CancelButton = cancel;
            return f.ShowDialog(this) == DialogResult.OK ? (string)lb.SelectedItem : null;
        }
    }

    void BrowseGame()
    {
        using (OpenFileDialog d = new OpenFileDialog { Filter = "UNDERTALE|UNDERTALE.exe|" + L.S("Exécutables", "Programs") + "|*.exe", Title = L.S("Choisir UNDERTALE.exe", "Pick UNDERTALE.exe") })
        {
            if (File.Exists(txtGame.Text)) d.InitialDirectory = Path.GetDirectoryName(txtGame.Text);
            if (d.ShowDialog(this) != DialogResult.OK) return;
            if (!GameFinder.IsGame(d.FileName) && Dlg.Show(this, L.S("Ce fichier ne ressemble pas à UNDERTALE (pas de data.win à côté). L'utiliser quand même ?", "This doesn't look like UNDERTALE (no data.win next to it). Use it anyway?"), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            txtGame.Text = d.FileName;
            Store.SetSetting("game", d.FileName);
        }
    }

    void Launch()
    {
        if (current == null) return;
        Apply();
        string exe = txtGame.Text;
        if (!File.Exists(exe)) { DetectGame(true); exe = txtGame.Text; if (!File.Exists(exe)) return; }
        Store.SetSetting("game", exe);
        try
        {
            if (!Store.WriteAndLaunch(current, exe, this)) return;
            lblInfo.Text = L.S("Sauvegarde écrite — choisis « Continuer » dans le jeu.", "Save written — pick \"Continue\" in the game.");
            string k = current.Key;
            ReloadPresets(k);
            WindowState = FormWindowState.Minimized;
        }
        catch (Exception ex) { Dlg.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    // ---------- Saves perso et partage ----------
    string AskName(string title, string def)
    {
        using (Form f = new Form { Text = title, ClientSize = new Size(360, 90), FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false, Font = Font })
        {
            TextBox t = new TextBox { Location = new Point(12, 14), Width = 336, Text = Store.SafeName(def) };
            Button ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(192, 50), Width = 75 };
            Button cancel = new Button { Text = L.S("Annuler", "Cancel"), DialogResult = DialogResult.Cancel, Location = new Point(273, 50), Width = 75 };
            f.Controls.AddRange(new Control[] { t, ok, cancel });
            f.AcceptButton = ok; f.CancelButton = cancel;
            if (f.ShowDialog(this) != DialogResult.OK) return null;
            string n = Store.SafeName(t.Text);
            if (n.Length == 0) return null;
            if (File.Exists(Path.Combine(Store.CustomDir, n + ".file0")) &&
                Dlg.Show(this, L.S("« ", "\"") + n + L.S(" » existe déjà. Le remplacer ?", "\" already exists. Replace it?"), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return null;
            return n;
        }
    }

    void ImportCurrentGame()
    {
        bool mine;
        SaveState s = Store.ReadCurrentGame(out mine);
        if (s == null)
        {
            Dlg.Show(this, L.S("Aucune partie trouvée dans ", "No game found in ") + Store.SaveDir + L.S(".\nJoue, sauvegarde à une étoile jaune, ferme le jeu, puis réessaie.", ".\nPlay, save at a yellow star, close the game, then try again."), Text);
            return;
        }
        if (Store.GameRunning())
            Dlg.Show(this, L.S("UNDERTALE est ouvert : l'import prend la dernière sauvegarde faite à une étoile jaune.", "UNDERTALE is open: the import takes the last save made at a yellow star."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        if (mine && Dlg.Show(this, L.S("Cette save est celle que Soul Launcher a écrite, pas une partie jouée depuis.\nL'importer quand même ?", "This is the save Soul Launcher wrote, not a game played since.\nImport it anyway?"), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        string n = AskName(L.S("Nom de ta save", "Name your save"), s.Lines[0] + " LV" + s.Get(2) + " · " + s.RoomName());
        if (n == null) return;
        s.SyncIni();
        Store.SaveCustom(n, s);
        ReloadPresets("perso:" + n);
        lblInfo.Text = L.S("Partie importée : « ", "Game imported: \"") + n + L.S(" ». Partage-la avec « Partager (.soul) ».", "\". Share it with \"Share (.soul)\".");
    }

    void SaveCustom()
    {
        if (current == null) return;
        Apply();
        string n = AskName(L.S("Nom du preset perso", "Name of your preset"), current.Custom ? current.Label : current.Display + L.S(" (perso)", " (mine)"));
        if (n == null) return;
        SaveState s = current.Clone(); s.Custom = true;
        Store.SaveCustom(n, s);
        ReloadPresets("perso:" + n);
        lblInfo.Text = L.S("Preset enregistré dans ", "Preset saved in ") + Store.CustomDir;
    }

    void DeleteCustom()
    {
        if (current == null || !current.Custom) { Dlg.Show(this, L.S("Sélectionne une de tes saves (section « Mes saves »).", "Select one of your saves (\"My saves\" section)."), Text); return; }
        string n = current.Key.Substring(6);
        if (Dlg.Show(this, L.S("Supprimer « ", "Delete \"") + n + L.S(" » ?", "\"?"), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        Store.DeleteCustom(n);
        current = null;
        ReloadPresets(null);
    }

    void ExportSoul()
    {
        if (current == null) return;
        Apply();
        string name = current.Custom ? current.Label : current.Display;
        using (SaveFileDialog d = new SaveFileDialog { Filter = L.S("Save Soul Launcher", "Soul Launcher save") + " (*.soul)|*.soul", FileName = Store.SafeName(name) + ".soul", Title = L.S("Partager cette save", "Share this save") })
        {
            // Téléchargements : pas protégé par l'Accès contrôlé aux dossiers, contrairement à Documents et au Bureau
            string dl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (Directory.Exists(dl)) d.InitialDirectory = dl;
            if (d.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                SoulFile.Export(current, name, d.FileName);
                Process.Start("explorer.exe", "/select,\"" + d.FileName + "\"");
                lblInfo.Text = L.S("Fichier prêt à envoyer (Discord, WhatsApp…) : ", "File ready to send (Discord, WhatsApp…): ") + d.FileName;
            }
            catch (UnauthorizedAccessException) { Dlg.Show(this, L.S("Windows bloque l'écriture ici (protection des dossiers). Enregistre plutôt dans Téléchargements.", "Windows blocks writing here (folder protection). Save to Downloads instead."), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            catch (Exception ex) { Dlg.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }

    void OpenSoulDialog()
    {
        using (OpenFileDialog d = new OpenFileDialog { Filter = L.S("Saves", "Saves") + " (*.soul;file0)|*.soul;file0", Title = L.S("Ouvrir une save reçue", "Open a received save"), Multiselect = true })
            if (d.ShowDialog(this) == DialogResult.OK) foreach (string f in d.FileNames) ImportSoul(f);
    }

    void ImportSoul(string path)
    {
        try
        {
            string name;
            SaveState s = SoulFile.Import(path, out name);
            string n = AskName(L.S("Nom de la save reçue", "Name of the received save"), name);
            if (n == null) return;
            s.SyncIni();
            s.Custom = true;
            s.Source = L.S("Reçue via ", "Received via ") + Path.GetFileName(path);
            Store.SaveCustom(n, s);
            ReloadPresets("perso:" + n);
            lblInfo.Text = L.S("Save ajoutée à « Mes saves » : ", "Save added to \"My saves\": ") + n;
        }
        catch (Exception ex) { Dlg.Show(this, Path.GetFileName(path) + " : " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    void Restore()
    {
        string b = Store.LatestBackup();
        if (b == null)
        {
            if (!Store.HasGenocideBackup()) { Dlg.Show(this, L.S("Rien à restaurer : Soul Launcher n'a encore rien remplacé.", "Nothing to restore: Soul Launcher hasn't replaced anything yet."), Text); return; }
            if (Dlg.Show(this, L.S("Pas d'ancienne save, mais la fin génocide a été mise de côté.\nRemettre le monde effacé (écran noir au prochain démarrage) ?", "No old save, but the genocide ending was set aside.\nPut the erased world back (black screen on next start)?"), Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            foreach (string n in new[] { "file0", "file9", "undertale.ini" }) { string p = Path.Combine(Store.SaveDir, n); if (File.Exists(p)) File.Delete(p); }
            Store.RestoreGenocide();
            lblInfo.Text = L.S("Fin génocide restaurée.", "Genocide ending restored.");
            return;
        }
        if (Store.GameRunning()) { Dlg.Show(this, L.S("Ferme UNDERTALE d'abord.", "Close UNDERTALE first."), Text); return; }
        if (Dlg.Show(this, L.S("Remettre la sauvegarde du ", "Restore the save from ") + Path.GetFileName(b).Replace("_", " ") + " ?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        Store.RestoreBackup(b);
        Store.RestoreGenocide();
        lblInfo.Text = L.S("Sauvegarde restaurée depuis ", "Save restored from ") + b;
    }

    void MakeShortcut()
    {
        if (current == null) return;
        string key = current.Key;
        if (!current.Custom)
        {
            string n = AskName(L.S("Nom du raccourci (enregistré aussi dans « Mes saves »)", "Shortcut name (also saved in \"My saves\")"), current.Display);
            if (n == null) return;
            Apply();
            SaveState s = current.Clone(); s.Custom = true;
            Store.SaveCustom(n, s);
            key = "perso:" + n;
            ReloadPresets(key);
        }
        string label = key.StartsWith("perso:") ? key.Substring(6) : key;
        // Menu Démarrer plutôt que Bureau : le Bureau est protégé par l'Accès contrôlé aux dossiers
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Soul Launcher");
        Directory.CreateDirectory(dir);
        string lnk = Path.Combine(dir, "UNDERTALE - " + label + ".lnk");
        Type t = Type.GetTypeFromProgID("WScript.Shell");
        dynamic shell = Activator.CreateInstance(t);
        dynamic sc = shell.CreateShortcut(lnk);
        sc.TargetPath = Application.ExecutablePath;
        sc.Arguments = "--preset \"" + key + "\"";
        sc.WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath);
        sc.IconLocation = File.Exists(LogoIcons.IcoPath) ? LogoIcons.IcoPath + ",0" : Application.ExecutablePath + ",0";
        sc.Save();
        lblInfo.Text = L.S("Raccourci créé : ", "Shortcut created: ") + lnk;
    }
}

// Carte d'aperçu dessinée à la main : titre, route, lieu, stats, équipement, objets, source
class PreviewPanel : Control
{
    SaveState s;
    public PreviewPanel() { DoubleBuffered = true; BackColor = Color.FromArgb(15, 15, 15); }
    public void SetState(SaveState st) { s = st; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(BackColor);
        using (Pen p = new Pen(Color.FromArgb(60, 60, 60))) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        if (s == null) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int x = 12, y = 10, w = Width - 24;
        Color yellow = Color.FromArgb(255, 200, 0), gray = Color.FromArgb(150, 150, 150);
        using (Font fTitle = new Font("Segoe UI", 10.5f, FontStyle.Bold))
        using (Font f = new Font("Segoe UI", 8.75f))
        using (Font fb = new Font("Segoe UI", 8.75f, FontStyle.Bold))
        {
            string title = s.Custom ? s.Label : s.Display;
            Size ts = TextRenderer.MeasureText(g, title, fTitle, new Size(w, 60), TextFormatFlags.WordBreak);
            TextRenderer.DrawText(g, title, fTitle, new Rectangle(x, y, w, Math.Min(ts.Height, 60)), Color.White, TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
            y += Math.Min(ts.Height, 60) + 6;

            // Badge de route
            string route = s.Route();
            Color rc = route == "Génocide" ? Color.FromArgb(220, 50, 50) : route == "Pacifiste" ? Color.FromArgb(60, 190, 90) : yellow;
            string rt = L.Category(route).ToUpper();
            Size rs = TextRenderer.MeasureText(rt, fb);
            using (SolidBrush b = new SolidBrush(rc)) g.FillRectangle(b, x, y, rs.Width + 10, 20);
            TextRenderer.DrawText(g, rt, fb, new Rectangle(x, y, rs.Width + 10, 20), Color.Black, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            y += 28;

            TextRenderer.DrawText(g, "📍 " + s.RoomName(), f, new Rectangle(x, y, w, 34), Color.White, TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
            y += 36;

            int time = s.Get(549) / 30;
            string[][] stats = {
                new[] { "LV", s.Get(2).ToString(), L.S("PV", "HP"), s.Get(3).ToString() },
                new[] { L.S("ATQ", "AT"), s.Get(5) + "(" + s.Get(6) + ")", L.S("DEF", "DF"), s.Get(7) + "(" + s.Get(8) + ")" },
                new[] { L.S("OR", "GOLD"), s.Get(11).ToString(), L.S("Tués", "Kills"), s.Get(12).ToString() },
                new[] { "EXP", s.Get(10).ToString(), L.S("Temps", "Time"), (time / 3600) + ":" + ((time / 60) % 60).ToString("00") },
            };
            foreach (string[] row in stats)
            {
                TextRenderer.DrawText(g, row[0], f, new Point(x, y), gray);
                TextRenderer.DrawText(g, row[1], fb, new Point(x + 44, y), Color.White);
                TextRenderer.DrawText(g, row[2], f, new Point(x + w / 2, y), gray);
                TextRenderer.DrawText(g, row[3], fb, new Point(x + w / 2 + 44, y), Color.White);
                y += 18;
            }
            y += 6;
            TextRenderer.DrawText(g, "⚔ " + ItemName(s.Get(29)) + "   🛡 " + ItemName(s.Get(30)), f, new Rectangle(x, y, w, 34), Color.White, TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
            y += 36;

            var items = Enumerable.Range(0, 8).Select(i => s.Get(13 + 2 * i)).Where(id => id > 0).Select(ItemName).ToList();
            string inv = items.Count == 0 ? L.S("Inventaire vide", "Empty inventory") : string.Join(", ", items.GroupBy(n => n).Select(gr => gr.Count() > 1 ? gr.Key + " ×" + gr.Count() : gr.Key));
            TextRenderer.DrawText(g, L.S("Objets : ", "Items: ") + inv, f, new Rectangle(x, y, w, 50), Color.FromArgb(210, 210, 210), TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
            y += 54;

            string desc = s.BuiltIn ? s.Source
                : s.Custom ? (s.Source ?? L.S("Ta save perso.", "Your own save."))
                : L.S("Source : ", "Source: ") + (s.Source ?? "?") + (string.IsNullOrEmpty(s.Original) ? "" : "\n" + s.Original);
            if (!string.IsNullOrEmpty(desc) && y < Height - 14)
                TextRenderer.DrawText(g, desc, f, new Rectangle(x, y, w, Height - y - 8), gray, TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
        }
    }

    static string ItemName(int id) { return id >= 0 && id < GameData.Items.Length ? (id == 0 ? "—" : GameData.Items[id]) : "#" + id; }
}
