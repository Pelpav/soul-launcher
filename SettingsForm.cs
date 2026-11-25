using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

class SettingsForm : Form
{
    MainForm main;

    public SettingsForm(MainForm owner)
    {
        main = owner;
        Text = Program.AppName + " — " + L.S("Réglages", "Settings");
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(460, 510);
        BackColor = Color.Black; ForeColor = Color.White;
        Icon = owner.Icon;

        int y = 16;
        Head(L.S("LANGUE", "LANGUAGE"), y); y += 26;
        ComboBox cboLang = new ComboBox { Location = new Point(20, y), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(25, 25, 25), ForeColor = Color.White };
        cboLang.Items.AddRange(new object[] { "Français", "English" });
        cboLang.SelectedIndex = L.En ? 1 : 0;
        cboLang.SelectedIndexChanged += delegate
        {
            bool en = cboLang.SelectedIndex == 1;
            if (en == L.En) return;
            Store.SetSetting("lang", en ? "en" : "fr");
            if (Dlg.Show(this, en ? "Restart Soul Launcher now to switch to English?" : "Redémarrer Soul Launcher maintenant pour passer en français ?", Program.AppName, MessageBoxButtons.YesNo) == DialogResult.Yes)
                Application.Restart();
        };
        Controls.Add(cboLang);
        y += 40;

        Head(L.S("APPARENCE", "LOOK"), y); y += 26;
        Button bLogo = Btn(L.S("Changer de logo…", "Change logo…"), 20, y, 200);
        bLogo.Click += delegate
        {
            using (OnboardingForm f = new OnboardingForm(OnboardingForm.Mode.LogoOnly))
                if (f.ShowDialog(this) == DialogResult.OK) { main.RefreshIcon(); Icon = main.Icon; }
        };
        Controls.Add(bLogo);
        Button bWarn = Btn(L.S("Revoir l'avertissement", "Show the disclaimer"), 230, y, 200);
        bWarn.Click += delegate { using (OnboardingForm f = new OnboardingForm(OnboardingForm.Mode.WarningOnly)) f.ShowDialog(this); };
        Controls.Add(bWarn);
        y += 44;

        Head(L.S("FICHIERS .SOUL", ".SOUL FILES"), y); y += 26;
        Button bAssoc = Btn(SoulFile.IsAssociated() ? L.S("✓ Les .soul s'ouvrent avec Soul Launcher", "✓ .soul files open with Soul Launcher") : L.S("Ouvrir les .soul avec Soul Launcher", "Open .soul files with Soul Launcher"), 20, y, 410);
        bAssoc.Click += delegate
        {
            try
            {
                SoulFile.RegisterAssociation();
                bAssoc.Text = L.S("✓ Les .soul s'ouvrent avec Soul Launcher", "✓ .soul files open with Soul Launcher");
            }
            catch (Exception ex) { Dlg.Show(this, ex.Message, Program.AppName); }
        };
        Controls.Add(bAssoc);
        y += 44;

        Head(L.S("MISES À JOUR", "UPDATES"), y); y += 26;
        CheckBox chkAuto = new CheckBox { Text = L.S("Chercher une nouvelle version au lancement", "Check for a new version on start"), Location = new Point(20, y), AutoSize = true, Checked = Updater.AutoCheck };
        chkAuto.CheckedChanged += delegate { Store.SetSetting("autoUpdate", chkAuto.Checked ? "1" : "0"); };
        Controls.Add(chkAuto);
        y += 28;
        Label ver = new Label { Text = L.S("Version installée : ", "Installed version: ") + Updater.CurrentText + "   ·   github.com/" + Updater.Repo, Location = new Point(20, y), AutoSize = true, ForeColor = Color.Gray };
        Controls.Add(ver);
        y += 24;
        Button bCheck = Btn(L.S("Vérifier maintenant", "Check now"), 20, y, 200);
        bCheck.Click += delegate
        {
            Cursor = Cursors.WaitCursor;
            try
            {
                UpdateInfo u = Updater.Check(10000);
                Cursor = Cursors.Default;
                if (u == null) Dlg.Show(this, L.S("Tu as la dernière version (", "You have the latest version (") + Updater.CurrentText + ").", Program.AppName);
                else Updater.Offer(this, u, false);
            }
            catch (Exception ex) { Cursor = Cursors.Default; Dlg.Show(this, L.S("Impossible de vérifier (connexion ?) : ", "Couldn't check (connection?): ") + ex.Message, Program.AppName); }
        };
        Controls.Add(bCheck);
        Button bPage = Btn(L.S("Page du projet", "Project page"), 230, y, 200);
        bPage.Click += delegate { Process.Start("https://github.com/" + Updater.Repo); };
        Controls.Add(bPage);
        y += 44;

        Head(L.S("DONNÉES", "DATA"), y); y += 26;
        Button bData = Btn(L.S("Ouvrir le dossier de Soul Launcher", "Open Soul Launcher's folder"), 20, y, 410);
        bData.Click += delegate { Directory.CreateDirectory(Store.AppDir); Process.Start("explorer.exe", Store.AppDir); };
        Controls.Add(bData);
        y += 36;
        Button bInst = Btn(Installer.RunningInstalled ? L.S("Désinstaller Soul Launcher…", "Uninstall Soul Launcher…") : L.S("Installer sur ce PC (menu Démarrer, Bureau)", "Install on this PC (Start menu, Desktop)"), 20, y, 410);
        bInst.Click += delegate
        {
            if (Installer.RunningInstalled) { if (Installer.Uninstall()) Environment.Exit(0); return; }
            if (Installer.Install(this, true)) Application.Exit();
        };
        Controls.Add(bInst);
        y += 36;
        Button bBug = Btn(L.S("🐞 Signaler un bug / proposer une idée", "🐞 Report a bug / suggest an idea"), 20, y, 410);
        bBug.Click += delegate { Process.Start("https://github.com/" + Updater.Repo + "/issues/new"); };
        Controls.Add(bBug);
    }

    void Head(string t, int y)
    {
        Controls.Add(new Label { Text = t, Location = new Point(20, y), AutoSize = true, ForeColor = Color.FromArgb(255, 200, 0), Font = new Font("Segoe UI", 9f, FontStyle.Bold) });
    }

    Button Btn(string t, int x, int y, int w)
    {
        Button b = new Button { Text = t, Location = new Point(x, y), Size = new Size(w, 30), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.White };
        b.FlatAppearance.BorderColor = Color.FromArgb(90, 90, 90);
        return b;
    }
}
