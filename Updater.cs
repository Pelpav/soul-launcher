// Mises à jour automatiques via GitHub Releases :
// au lancement, si une version plus récente existe, on propose de la télécharger puis on remplace l'exe et on relance.
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

class UpdateInfo
{
    public string Version, PageUrl, ExeUrl, ShaUrl, Notes;
}

static class Updater
{
    public const string DefaultRepo = "Pelpav/soul-launcher";
    public const string AssetName = "SoulLauncher.exe";

    public static string Repo { get { string r = Store.GetSetting("updateRepo"); return string.IsNullOrWhiteSpace(r) ? DefaultRepo : r.Trim(); } }
    public static bool AutoCheck { get { return Store.GetSetting("autoUpdate") != "0"; } }
    public static Version Current { get { return Assembly.GetExecutingAssembly().GetName().Version; } }
    public static string CurrentText { get { Version v = Current; return v.Major + "." + v.Minor + "." + v.Build; } }

    static string Get(string url, int timeoutMs)
    {
        HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
        req.UserAgent = "SoulLauncher";
        req.Accept = "application/vnd.github+json";
        req.Timeout = timeoutMs; req.ReadWriteTimeout = timeoutMs;
        using (WebResponse resp = req.GetResponse())
        using (StreamReader r = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            return r.ReadToEnd();
    }

    static string Unescape(string s) { return Regex.Unescape(s ?? ""); }

    // null = pas de version plus récente. Lève une exception si pas de connexion.
    public static UpdateInfo Check(int timeoutMs)
    {
        if (!Regex.IsMatch(Repo, @"^[\w.-]+/[\w.-]+$")) return null;
        string json = Get("https://api.github.com/repos/" + Repo + "/releases/latest", timeoutMs);
        Match tag = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"v?([0-9][0-9.]*)[^\"]*\"");
        if (!tag.Success) return null;
        Version latest;
        if (!Version.TryParse(Pad(tag.Groups[1].Value), out latest) || latest <= Current) return null;
        UpdateInfo u = new UpdateInfo();
        u.Version = tag.Groups[1].Value;
        Match page = Regex.Match(json, "\"html_url\"\\s*:\\s*\"(https://github\\.com/[^\"]+/releases/tag/[^\"]+)\"");
        u.PageUrl = page.Success ? page.Groups[1].Value : "https://github.com/" + Repo + "/releases/latest";
        foreach (Match m in Regex.Matches(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+)\""))
        {
            string url = m.Groups[1].Value;
            string file = Uri.UnescapeDataString(url.Substring(url.LastIndexOf('/') + 1));
            if (file.Equals(AssetName, StringComparison.OrdinalIgnoreCase)) u.ExeUrl = url;
            else if (file.Equals(AssetName + ".sha256", StringComparison.OrdinalIgnoreCase)) u.ShaUrl = url;
        }
        Match body = Regex.Match(json, "\"body\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
        if (body.Success) u.Notes = Unescape(body.Groups[1].Value).Trim();
        return u;
    }

    static string Pad(string v) { var p = v.Trim('.').Split('.').ToList(); while (p.Count < 4) p.Add("0"); return string.Join(".", p.Take(4)); }

    // Au démarrage : supprime l'ancien exe laissé par la dernière mise à jour
    public static void CleanupOld()
    {
        string old = Application.ExecutablePath + ".old";
        for (int i = 0; i < 10 && File.Exists(old); i++)
        {
            try { File.Delete(old); } catch { Thread.Sleep(300); }
        }
    }

    // Vérification silencieuse en arrière-plan ; demande à l'utilisateur seulement s'il y a une nouvelle version
    public static void CheckAtStartup(Form owner)
    {
        if (!AutoCheck) return;
        Thread t = new Thread(() =>
        {
            UpdateInfo u = null;
            try { u = Check(6000); } catch { return; } // pas de connexion : on ne dit rien
            if (u == null || u.Version == Store.GetSetting("skipVersion")) return;
            try { owner.BeginInvoke((Action)(() => Offer(owner, u, true))); } catch { }
        });
        t.IsBackground = true;
        t.Start();
    }

    public static void Offer(Form owner, UpdateInfo u, bool atStartup)
    {
        string notes = string.IsNullOrEmpty(u.Notes) ? "" : "\n\n" + (u.Notes.Length > 600 ? u.Notes.Substring(0, 600) + "…" : u.Notes);
        DialogResult r = Dlg.Show(owner,
            L.S("Une nouvelle version de Soul Launcher est disponible : ", "A new version of Soul Launcher is available: ") + u.Version +
            L.S(" (tu as la ", " (you have ") + CurrentText + ")." + notes + "\n\n" +
            L.S("La télécharger et l'installer maintenant ?", "Download and install it now?"),
            L.S("Mise à jour", "Update"), atStartup ? MessageBoxButtons.YesNoCancel : MessageBoxButtons.YesNo, MessageBoxIcon.Information,
            atStartup ? new[] { L.S("Installer", "Install"), L.S("Plus tard", "Later"), L.S("Ignorer", "Skip") } : new[] { L.S("Installer", "Install"), L.S("Plus tard", "Later") });
        if (r == DialogResult.Cancel) { Store.SetSetting("skipVersion", u.Version); return; }
        if (r != DialogResult.Yes) return;
        if (u.ExeUrl == null) { Process.Start(u.PageUrl); return; }
        using (UpdateForm f = new UpdateForm(u)) f.ShowDialog(owner);
    }

    // Remplace l'exe en cours (Windows autorise à renommer un exe lancé) puis relance la nouvelle version
    public static void Install(string newExe)
    {
        string target = Application.ExecutablePath;
        string old = target + ".old";
        if (File.Exists(old)) File.Delete(old);
        File.Move(target, old);
        try { File.Move(newExe, target); }
        catch { File.Move(old, target); throw; }
        Process.Start(new ProcessStartInfo(target, "--updated") { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(target) });
        Environment.Exit(0);
    }

    public static string Sha256(string file)
    {
        using (var sha = SHA256.Create())
        using (var fs = File.OpenRead(file))
            return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
    }
}

class UpdateForm : Form
{
    ProgressBar bar;
    Label lbl;
    WebClient wc;
    UpdateInfo info;
    string tmp;

    public UpdateForm(UpdateInfo u)
    {
        info = u;
        Text = Program.AppName + " — " + L.S("mise à jour", "update");
        Font = new Font("Segoe UI", 9.5f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; ControlBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(420, 120);
        BackColor = Color.Black; ForeColor = Color.White;
        lbl = new Label { Location = new Point(16, 16), Size = new Size(388, 22), Text = L.S("Téléchargement de la version ", "Downloading version ") + u.Version + "…" };
        bar = new ProgressBar { Location = new Point(16, 46), Size = new Size(388, 22) };
        Button cancel = new Button { Text = L.S("Annuler", "Cancel"), Location = new Point(314, 80), Size = new Size(90, 28), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(30, 30, 30) };
        cancel.Click += delegate { if (wc != null) wc.CancelAsync(); DialogResult = DialogResult.Cancel; Close(); };
        Controls.AddRange(new Control[] { lbl, bar, cancel });
        Shown += delegate { Start(); };
    }

    void Start()
    {
        string dir = Path.Combine(Store.AppDir, "update");
        Directory.CreateDirectory(dir);
        tmp = Path.Combine(dir, "SoulLauncher.new.exe");
        if (File.Exists(tmp)) File.Delete(tmp);
        wc = new WebClient();
        wc.Headers[HttpRequestHeader.UserAgent] = "SoulLauncher";
        wc.DownloadProgressChanged += (s, e) => { bar.Value = Math.Max(0, Math.Min(100, e.ProgressPercentage)); };
        wc.DownloadFileCompleted += (s, e) =>
        {
            if (e.Cancelled) return;
            if (e.Error != null) { Fail(e.Error.Message); return; }
            Finish();
        };
        wc.DownloadFileAsync(new Uri(info.ExeUrl), tmp);
    }

    void Finish()
    {
        try
        {
            lbl.Text = L.S("Vérification…", "Verifying…");
            Application.DoEvents();
            if (new FileInfo(tmp).Length < 100000) throw new Exception(L.S("fichier téléchargé incomplet", "incomplete download"));
            if (info.ShaUrl != null)
            {
                string expected;
                using (WebClient w2 = new WebClient()) { w2.Headers[HttpRequestHeader.UserAgent] = "SoulLauncher"; expected = w2.DownloadString(info.ShaUrl); }
                Match m = Regex.Match(expected, "[0-9a-fA-F]{64}");
                if (!m.Success || !m.Value.Equals(Updater.Sha256(tmp), StringComparison.OrdinalIgnoreCase))
                    throw new Exception(L.S("l'empreinte SHA-256 ne correspond pas (fichier abîmé ou modifié)", "SHA-256 mismatch (damaged or modified file)"));
            }
            lbl.Text = L.S("Installation…", "Installing…");
            Application.DoEvents();
            Updater.Install(tmp);
        }
        catch (Exception ex) { Fail(ex.Message); }
    }

    void Fail(string msg)
    {
        try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
        if (Dlg.Show(this,
            L.S("La mise à jour automatique a échoué : ", "The automatic update failed: ") + msg + "\n\n" +
            L.S("Si Soul Launcher est dans Documents ou sur le Bureau, Windows (protection contre les ransomwares) peut l'empêcher de se remplacer. Ouvrir la page de téléchargement pour le faire à la main ?",
                "If Soul Launcher is in Documents or on the Desktop, Windows (ransomware protection) may block it from replacing itself. Open the download page to do it by hand?"),
            Program.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            Process.Start(info.PageUrl);
        DialogResult = DialogResult.Abort;
        Close();
    }
}
