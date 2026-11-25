// Installation comme une vraie application (sans droits admin) :
// copie dans %LOCALAPPDATA%\Programs\Soul Launcher, raccourcis menu Démarrer + Bureau,
// entrée dans « Applications installées » de Windows pour désinstaller proprement.
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

static class Installer
{
    const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\SoulLauncher";

    public static string InstallDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Soul Launcher"); } }
    public static string InstalledExe { get { return Path.Combine(InstallDir, "SoulLauncher.exe"); } }
    static string StartMenuLnk { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Soul Launcher.lnk"); } }
    static string DesktopLnk { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Soul Launcher.lnk"); } }
    static string PresetShortcutsDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Soul Launcher"); } }

    public static bool RunningInstalled { get { return string.Equals(Path.GetFullPath(Application.ExecutablePath), Path.GetFullPath(InstalledExe), StringComparison.OrdinalIgnoreCase); } }
    public static bool IsInstalled { get { return File.Exists(InstalledExe); } }

    // Au premier lancement d'un exe téléchargé : proposer l'installation
    public static bool OfferAtStartup()
    {
        if (RunningInstalled || Store.GetSetting("installPrompt") == "no") return false;
        string msg = IsInstalled
            ? L.S("Soul Launcher est déjà installé sur ce PC, mais tu as ouvert une autre copie.\n\nOuvrir la version installée (et la mettre à jour avec celle-ci si elle est plus récente) ?",
                  "Soul Launcher is already installed on this PC, but you opened another copy.\n\nOpen the installed version (updating it with this one if newer)?")
            : L.S("Installer Soul Launcher sur ce PC ?\n\n• Raccourcis dans le menu Démarrer et sur le Bureau\n• Visible dans « Applications installées » (désinstallation propre)\n• Mises à jour automatiques sans blocage de Windows\n\nAucun droit administrateur nécessaire. Le fichier téléchargé pourra ensuite être supprimé.",
                  "Install Soul Launcher on this PC?\n\n• Shortcuts in the Start menu and on the Desktop\n• Listed in \"Installed apps\" (clean uninstall)\n• Automatic updates without Windows blocking them\n\nNo admin rights needed. You can delete the downloaded file afterwards.");
        DialogResult r = Dlg.Show(null, msg, L.S("Installation", "Install"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information,
            new[] { IsInstalled ? L.S("Ouvrir l'installée", "Open installed") : L.S("Installer", "Install"), L.S("Pas maintenant", "Not now"), L.S("Ne plus demander", "Don't ask again") });
        if (r == DialogResult.Cancel) { Store.SetSetting("installPrompt", "no"); return false; }
        if (r != DialogResult.Yes) return false;
        return Install(null, true);
    }

    // true = la version installée a été lancée, l'exe courant doit se fermer
    public static bool Install(IWin32Window owner, bool relaunch)
    {
        try
        {
            Directory.CreateDirectory(InstallDir);
            string src = Application.ExecutablePath;
            bool copy = !RunningInstalled;
            if (copy && File.Exists(InstalledExe))
            {
                // Ne pas remplacer une version installée plus récente par une plus ancienne
                Version inst = null;
                try { inst = System.Reflection.AssemblyName.GetAssemblyName(InstalledExe).Version; } catch { }
                if (inst != null && inst >= Updater.Current) copy = false;
            }
            if (copy)
            {
                string tmp = InstalledExe + ".tmp";
                File.Copy(src, tmp, true);
                if (File.Exists(InstalledExe)) File.Delete(InstalledExe);
                File.Move(tmp, InstalledExe);
            }
            CreateShortcut(StartMenuLnk, InstalledExe, "");
            try { CreateShortcut(DesktopLnk, InstalledExe, ""); } catch { } // le Bureau peut être protégé par Windows
            Register();
            if (relaunch && !RunningInstalled)
            {
                Process.Start(new ProcessStartInfo(InstalledExe) { UseShellExecute = false, WorkingDirectory = InstallDir });
                return true;
            }
            Dlg.Show(owner, L.S("Soul Launcher est installé : menu Démarrer et Bureau.", "Soul Launcher is installed: Start menu and Desktop."), Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }
        catch (Exception ex)
        {
            Dlg.Show(owner, L.S("Installation impossible : ", "Couldn't install: ") + ex.Message, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }

    static void CreateShortcut(string lnk, string target, string args)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(lnk));
        Type t = Type.GetTypeFromProgID("WScript.Shell");
        dynamic shell = Activator.CreateInstance(t);
        dynamic sc = shell.CreateShortcut(lnk);
        sc.TargetPath = target;
        sc.Arguments = args;
        sc.WorkingDirectory = Path.GetDirectoryName(target);
        sc.Description = "Soul Launcher — UNDERTALE";
        sc.IconLocation = File.Exists(LogoIcons.IcoPath) ? LogoIcons.IcoPath + ",0" : target + ",0";
        sc.Save();
    }

    // Entrée « Applications installées » (pour l'utilisateur courant)
    public static void Register()
    {
        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(UninstallKey))
        {
            k.SetValue("DisplayName", "Soul Launcher");
            k.SetValue("DisplayVersion", Updater.CurrentText);
            k.SetValue("Publisher", "Soul Launcher (fan, non officiel)");
            k.SetValue("DisplayIcon", File.Exists(LogoIcons.IcoPath) ? LogoIcons.IcoPath : InstalledExe);
            k.SetValue("InstallLocation", InstallDir);
            k.SetValue("UninstallString", "\"" + InstalledExe + "\" --uninstall");
            k.SetValue("URLInfoAbout", "https://github.com/" + Updater.Repo);
            k.SetValue("NoModify", 1, RegistryValueKind.DWord);
            k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            try { k.SetValue("EstimatedSize", (int)(new FileInfo(InstalledExe).Length / 1024), RegistryValueKind.DWord); } catch { }
        }
    }

    // Garde la version affichée par Windows à jour après une mise à jour automatique
    public static void RefreshRegistration()
    {
        if (!RunningInstalled) return;
        try { Register(); } catch { }
        // Si les .soul étaient associés à une autre copie (ex. Téléchargements), on les fait pointer ici
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Classes\SoulLauncher.Save\shell\open\command"))
                if (k != null && !SoulFile.IsAssociated()) { k.Close(); SoulFile.RegisterAssociation(); }
        }
        catch { }
    }

    public static bool Uninstall()
    {
        if (Dlg.Show(null, L.S("Désinstaller Soul Launcher ?", "Uninstall Soul Launcher?"), Program.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question,
            new[] { L.S("Désinstaller", "Uninstall"), L.S("Annuler", "Cancel") }) != DialogResult.Yes) return false;
        DialogResult keep = Dlg.Show(null,
            L.S("Garder tes données ?\n\nTes saves perso et les copies de ta vraie sauvegarde UNDERTALE sont dans :\n", "Keep your data?\n\nYour own saves and the copies of your real UNDERTALE save are in:\n") + Store.AppDir +
            L.S("\n\nTa partie UNDERTALE elle-même n'est jamais supprimée.", "\n\nYour UNDERTALE game itself is never deleted."),
            Program.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question,
            new[] { L.S("Garder (conseillé)", "Keep (recommended)"), L.S("Tout supprimer", "Delete everything") });

        foreach (string f in new[] { StartMenuLnk, DesktopLnk }) try { if (File.Exists(f)) File.Delete(f); } catch { }
        try { if (Directory.Exists(PresetShortcutsDir)) Directory.Delete(PresetShortcutsDir, true); } catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); } catch { }
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Classes\.soul", true))
                if (k != null && (k.GetValue("") as string) == "SoulLauncher.Save") { k.Close(); Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\.soul", false); }
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\SoulLauncher.Save", false);
        }
        catch { }
        if (keep == DialogResult.No) try { Directory.Delete(Store.AppDir, true); } catch { }

        // Supprime le dossier d'installation une fois ce programme fermé
        string dirs = "\"" + InstallDir + "\"";
        Process.Start(new ProcessStartInfo("cmd.exe", "/c ping 127.0.0.1 -n 3 >nul & rmdir /s /q " + dirs) { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true, UseShellExecute = false });
        Dlg.Show(null, L.S("Soul Launcher est désinstallé.", "Soul Launcher has been uninstalled."), Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        return true;
    }
}
