// Soul Launcher : écrit une sauvegarde UNDERTALE à partir d'un preset (+ réglages) puis lance le jeu.
// Outil de fan non officiel. Compatible C# 5 / .NET Framework 4.5+ (csc.exe intégré à Windows).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Soul Launcher")]
[assembly: AssemblyProduct("Soul Launcher")]
[assembly: AssemblyDescription("Lanceur de sauvegardes UNDERTALE fait par un fan (non officiel)")]
[assembly: AssemblyVersion("1.0.3.0")]
[assembly: AssemblyFileVersion("1.0.3.0")]

// ---------- Langue ----------
static class L
{
    public static bool En;
    public static string S(string fr, string en) { return En ? en : fr; }

    public static void Load()
    {
        string lang = Store.GetSetting("lang");
        En = lang == null ? !System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("fr", StringComparison.OrdinalIgnoreCase) : lang == "en";
    }

    public static string Category(string fr)
    {
        if (!En) return fr;
        switch (fr)
        {
            case "Neutre": return "Neutral";
            case "Pacifiste": return "Pacifist";
            case "Génocide": return "Genocide";
            case "Divers": return "Misc";
            default: return fr;
        }
    }
}

static class Program
{
    public const string AppName = "Soul Launcher";
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

    [STAThread]
    static int Main(string[] args)
    {
        try { SetProcessDPIAware(); } catch { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { } // TLS 1.2 pour GitHub
        Store.MigrateOldData();
        Updater.CleanupOld();
        L.Load();
        if (Array.IndexOf(args, "--uninstall") >= 0) { Installer.Uninstall(); return 0; }
        if (Array.IndexOf(args, "--install") >= 0) { Installer.Install(null, false); return 0; }
        Installer.RefreshRegistration();

        // Raccourci : SoulLauncher.exe --preset sans
        int i = Array.IndexOf(args, "--preset");
        if (i >= 0 && i + 1 < args.Length)
        {
            try
            {
                SaveState st = Store.FindPreset(args[i + 1]);
                if (st == null) { Dlg.Show(L.S("Preset introuvable : ", "Preset not found: ") + args[i + 1], AppName); return 1; }
                if (Array.IndexOf(args, "--no-launch") >= 0) { Store.WriteSave(st); return 0; }
                Store.Fullscreen = Store.GetSetting("fullscreen") == "1";
                string exe = GameFinder.Resolve();
                if (exe == null) { Dlg.Show(L.S("UNDERTALE introuvable. Ouvre Soul Launcher sans argument pour choisir le jeu.", "UNDERTALE not found. Open Soul Launcher without arguments to pick the game."), AppName); return 1; }
                Store.WriteAndLaunch(st, exe, null);
                return 0;
            }
            catch (Exception ex) { Dlg.Show(ex.Message, AppName); return 1; }
        }

        // Double-clic sur un fichier .soul (ou glissé sur l'exe)
        string toImport = args.FirstOrDefault(a => File.Exists(a) && (a.EndsWith(".soul", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(a).Equals("file0", StringComparison.OrdinalIgnoreCase)));
        try
        {
            if (Store.GetSetting("onboarded") != "1" || LogoIcons.Chosen() < 0)
            {
                using (OnboardingForm ob = new OnboardingForm(OnboardingForm.Mode.Full))
                    if (ob.ShowDialog() != DialogResult.OK) return 0;
            }
            if (toImport == null && Installer.OfferAtStartup()) return 0; // la version installée prend le relais
            Application.Run(new MainForm(toImport));
        }
        catch (Exception ex) { Dlg.Show(ex.ToString(), AppName + " — " + L.S("erreur", "error")); return 1; }
        return 0;
    }
}

// Une sauvegarde complète : 549 lignes de file0 + contenu de undertale.ini
class SaveState
{
    public string Key, Label, LabelEn, Category, Source, Original;
    public bool Custom, IsHeader, BuiltIn;
    public string[] Lines;
    public List<KeyValuePair<string, List<KeyValuePair<string, string>>>> Ini = new List<KeyValuePair<string, List<KeyValuePair<string, string>>>>();

    public string Display { get { return L.En && !string.IsNullOrEmpty(LabelEn) ? LabelEn : Label; } }

    public static SaveState Header(string title) { SaveState h = new SaveState(); h.Label = title; h.IsHeader = true; h.Key = "header:" + title; return h; }

    public static string[] Normalize(string[] lines)
    {
        var l = lines.Select(x => x.Trim()).ToList();
        while (l.Count > 549 && l[l.Count - 1].Length == 0) l.RemoveAt(l.Count - 1);
        while (l.Count < 549) l.Add("0");
        return l.Take(549).ToArray();
    }

    public SaveState Clone()
    {
        SaveState s = (SaveState)MemberwiseClone();
        s.Lines = (string[])Lines.Clone();
        s.Ini = new List<KeyValuePair<string, List<KeyValuePair<string, string>>>>();
        foreach (var sec in Ini) s.Ini.Add(new KeyValuePair<string, List<KeyValuePair<string, string>>>(sec.Key, new List<KeyValuePair<string, string>>(sec.Value)));
        return s;
    }

    public int Get(int line) { double v; return double.TryParse(Lines[line - 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) ? (int)v : 0; }
    public void Set(int line, int v) { Lines[line - 1] = v.ToString(); }

    public void SetIni(string section, string key, string value)
    {
        var sec = Ini.FirstOrDefault(s => s.Key == section);
        if (sec.Value == null)
        {
            sec = new KeyValuePair<string, List<KeyValuePair<string, string>>>(section, new List<KeyValuePair<string, string>>());
            Ini.Insert(0, sec);
        }
        int idx = sec.Value.FindIndex(kv => kv.Key == key);
        var nkv = new KeyValuePair<string, string>(key, value);
        if (idx >= 0) sec.Value[idx] = nkv; else sec.Value.Add(nkv);
    }

    public static List<KeyValuePair<string, List<KeyValuePair<string, string>>>> ParseIni(string text)
    {
        var res = new List<KeyValuePair<string, List<KeyValuePair<string, string>>>>();
        List<KeyValuePair<string, string>> cur = null;
        foreach (string raw in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string line = raw.Trim();
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                cur = new List<KeyValuePair<string, string>>();
                res.Add(new KeyValuePair<string, List<KeyValuePair<string, string>>>(line.Substring(1, line.Length - 2), cur));
            }
            else if (cur != null && line.Contains("="))
            {
                int eq = line.IndexOf('=');
                cur.Add(new KeyValuePair<string, string>(line.Substring(0, eq), line.Substring(eq + 1).Trim().Trim('"')));
            }
        }
        return res;
    }

    public string IniText()
    {
        StringBuilder sb = new StringBuilder();
        foreach (var sec in Ini)
        {
            sb.Append("[").Append(sec.Key).Append("]\r\n");
            foreach (var kv in sec.Value) sb.Append(kv.Key).Append("=\"").Append(kv.Value).Append("\"\r\n");
        }
        return sb.ToString();
    }

    public string File0Text() { return string.Join("\r\n", Lines) + "\r\n"; }

    public void SyncIni()
    {
        SetIni("General", "Name", Lines[0]);
        SetIni("General", "Love", Get(2).ToString());
        SetIni("General", "Kills", Get(12).ToString());
        SetIni("General", "Room", Get(548).ToString());
        SetIni("General", "Time", Get(549).ToString());
    }

    // ---- Infos pour l'aperçu ----
    public string RoomName()
    {
        int r = Get(548);
        return r >= 0 && r < GameData.Rooms.Length ? GameData.Rooms[r].Replace(" [SAVE]", "").Replace(" [Dogchecked]", "").Replace(" [SAVE?]", "") : ("#" + r);
    }

    public string Route()
    {
        string c = Category ?? "";
        if (c == "Génocide" || c == "Pacifiste" || c == "Neutre") return c;
        int kills = Get(12), lv = Get(2);
        if (lv >= 15 && kills >= 90) return "Génocide";
        return kills == 0 ? "Pacifiste" : "Neutre";
    }
}

// ---------- Données, saves, réglages ----------
static class Store
{
    public static readonly Encoding Utf8 = new UTF8Encoding(false);
    static string Local { get { return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); } }
    public static string SaveDir { get { return Path.Combine(Local, "UNDERTALE"); } }
    public static string AppDir { get { return Path.Combine(Local, "SoulLauncher"); } }
    public static string BackupDir { get { return Path.Combine(AppDir, "backups"); } }
    public static string CustomDir { get { return Path.Combine(AppDir, "presets"); } }
    static string SettingsFile { get { return Path.Combine(AppDir, "settings.txt"); } }

    // Reprend les données de l'ancien nom (Undertale Launcher)
    public static void MigrateOldData()
    {
        try
        {
            // Copie sans écraser (l'ancien dossier reste en place), une seule fois
            string old = Path.Combine(Local, "UndertaleLauncher");
            if (!Directory.Exists(old) || File.Exists(SettingsFile)) return;
            CopyMissing(old, AppDir);
        }
        catch { }
    }

    static void CopyMissing(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (string f in Directory.GetFiles(src))
        {
            string t = Path.Combine(dst, Path.GetFileName(f));
            if (!File.Exists(t)) try { File.Copy(f, t); } catch { }
        }
        foreach (string d in Directory.GetDirectories(src)) CopyMissing(d, Path.Combine(dst, Path.GetFileName(d)));
    }

    static readonly string[][] BuiltInInfo = {
        // clé, libellé EN, catégorie, description FR, description EN
        new[] { "ruins", "Ruins start", "Neutre", "Tout début du jeu, juste après la chute dans les Ruines.", "The very beginning, right after falling into the Ruins." },
        new[] { "undyne", "Undyne the Undying (genocide)", "Génocide", "Route génocide, juste avant le combat contre Undyne l'Immortelle.", "Genocide route, right before the Undyne the Undying fight." },
        new[] { "sans", "Right before Sans (genocide)", "Génocide", "Route génocide, au point de sauvegarde du Dernier Couloir, juste avant Sans.", "Genocide route, at the Last Corridor save point, right before Sans." },
        new[] { "asgore", "Right before Asgore (neutral)", "Neutre", "Route neutre, à la Barrière, juste avant le combat contre Asgore.", "Neutral route, at the Barrier, right before the Asgore fight." },
        new[] { "asgore-true", "Right before Asgore (true ending route)", "Pacifiste", "Route de la vraie fin, juste avant Asgore (amis de Papyrus, Undyne et Alphys).", "True ending route, right before Asgore (befriended Papyrus, Undyne and Alphys)." },
        new[] { "flowey", "Right before Omega Flowey", "Neutre", "Route neutre : le combat final contre Omega Flowey se lance au démarrage.", "Neutral route: the final Omega Flowey fight starts on load." },
        new[] { "pacifist", "True Pacifist ending", "Pacifiste", "Fin pacifiste, juste avant le combat final de la vraie fin.", "True Pacifist ending, right before the final fight." },
    };

    public static List<SaveState> BuiltIns()
    {
        var list = new List<SaveState>();
        foreach (string[] p in GameData.Presets)
        {
            SaveState s = new SaveState();
            s.Key = p[0]; s.Label = p[1]; s.BuiltIn = true;
            s.Lines = p[2].Split('\n');
            s.Ini = SaveState.ParseIni(p[3]);
            string[] info = BuiltInInfo.FirstOrDefault(b => b[0] == p[0]);
            if (info != null) { s.LabelEn = info[1]; s.Category = info[2]; s.Source = L.S(info[3], info[4]); }
            s.Original = "Flowey's Time Machine";
            list.Add(s);
        }
        return list;
    }

    public static List<SaveState> Customs()
    {
        var list = new List<SaveState>();
        if (!Directory.Exists(CustomDir)) return list;
        foreach (string f in Directory.GetFiles(CustomDir, "*.file0").OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase))
        {
            try
            {
                string[] lines = File.ReadAllLines(f, Utf8);
                if (lines.Length < 540) continue;
                SaveState s = new SaveState();
                string name = Path.GetFileNameWithoutExtension(f);
                s.Key = "perso:" + name; s.Label = name; s.Custom = true;
                s.Lines = SaveState.Normalize(lines);
                string ini = Path.ChangeExtension(f, ".ini");
                s.Ini = File.Exists(ini) ? SaveState.ParseIni(File.ReadAllText(ini, Utf8)) : new List<KeyValuePair<string, List<KeyValuePair<string, string>>>>();
                string meta = Path.ChangeExtension(f, ".txt");
                if (File.Exists(meta)) s.Source = File.ReadAllText(meta, Utf8).Trim();
                list.Add(s);
            }
            catch { }
        }
        return list;
    }

    // ---- Packs : intégrés dans l'exe (packs.zip) + dossier « packs » optionnel à côté de l'exe ----
    static readonly string[] CatOrder = { "Neutre", "Pacifiste", "Génocide", "Secrets", "Divers" };
    static List<KeyValuePair<string, List<SaveState>>> packCache;

    public static List<KeyValuePair<string, List<SaveState>>> Packs()
    {
        if (packCache != null) return packCache;
        var byCat = new Dictionary<string, List<SaveState>>();
        var meta = new Dictionary<string, string[]>(); // "Cat/Nom" -> [en, source]
        Action<string, string, string[], string> add = (cat, name, file0, ini) =>
        {
            if (file0.Length < 540) return;
            SaveState s = new SaveState();
            s.Key = "pack:" + cat + "/" + name; s.Label = name; s.Category = cat;
            s.Lines = SaveState.Normalize(file0);
            s.Ini = ini != null ? SaveState.ParseIni(ini) : new List<KeyValuePair<string, List<KeyValuePair<string, string>>>>();
            string[] m;
            if (meta.TryGetValue(cat + "/" + name, out m)) { s.LabelEn = m[0]; s.Source = m[1]; s.Original = m[2]; }
            s.SyncIni();
            List<SaveState> l;
            if (!byCat.TryGetValue(cat, out l)) { l = new List<SaveState>(); byCat[cat] = l; }
            if (!l.Any(x => x.Key == s.Key)) l.Add(s);
        };

        try
        {
            using (Stream rs = Assembly.GetExecutingAssembly().GetManifestResourceStream("packs.zip"))
                if (rs != null)
                    using (ZipArchive zip = new ZipArchive(rs, ZipArchiveMode.Read))
                    {
                        ZipArchiveEntry me = zip.Entries.FirstOrDefault(e => e.FullName == "packs_meta.tsv");
                        if (me != null)
                            using (var r = new StreamReader(me.Open(), Encoding.UTF8))
                            {
                                string line;
                                while ((line = r.ReadLine()) != null)
                                {
                                    string[] p = line.Split('\t');
                                    if (p.Length >= 5) meta[p[0] + "/" + p[1]] = new[] { p[2], p[3], p[4] };
                                }
                            }
                        foreach (ZipArchiveEntry e in zip.Entries.Where(e => e.FullName.EndsWith(".file0")))
                        {
                            string[] parts = e.FullName.Split('/');
                            if (parts.Length != 2) continue;
                            string cat = parts[0], name = parts[1].Substring(0, parts[1].Length - 6);
                            string[] f0 = ReadLines(e);
                            ZipArchiveEntry ie = zip.GetEntry(cat + "/" + name + ".ini");
                            add(cat, name, f0, ie != null ? string.Join("\n", ReadLines(ie)) : null);
                        }
                    }
        }
        catch { }

        string ext = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "packs");
        if (Directory.Exists(ext))
            foreach (string dir in Directory.GetDirectories(ext))
                foreach (string f in Directory.GetFiles(dir, "*.file0"))
                {
                    try
                    {
                        string ini = Path.ChangeExtension(f, ".ini");
                        add(Path.GetFileName(dir), Path.GetFileNameWithoutExtension(f), File.ReadAllLines(f, Utf8), File.Exists(ini) ? File.ReadAllText(ini, Utf8) : null);
                    }
                    catch { }
                }

        packCache = byCat
            .OrderBy(kv => { int i = Array.IndexOf(CatOrder, kv.Key); return i < 0 ? 99 : i; })
            .ThenBy(kv => kv.Key)
            .Select(kv => new KeyValuePair<string, List<SaveState>>(kv.Key, kv.Value.OrderBy(s => s.Display, StringComparer.CurrentCultureIgnoreCase).ToList()))
            .ToList();
        return packCache;
    }

    static string[] ReadLines(ZipArchiveEntry e)
    {
        using (var r = new StreamReader(e.Open(), Encoding.UTF8))
            return r.ReadToEnd().Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
    }

    public static IEnumerable<SaveState> AllPresets()
    {
        foreach (SaveState s in BuiltIns()) yield return s;
        foreach (SaveState s in Customs()) yield return s;
        foreach (var cat in Packs()) foreach (SaveState s in cat.Value) yield return s;
    }

    public static SaveState FindPreset(string key)
    {
        foreach (SaveState s in AllPresets())
            if (string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase)) return s;
        return null;
    }

    public static SaveState ReadCurrentGame(out bool writtenByLauncher)
    {
        writtenByLauncher = false;
        string f0 = Path.Combine(SaveDir, "file0");
        if (!File.Exists(f0)) return null;
        SaveState s = new SaveState();
        s.Lines = SaveState.Normalize(File.ReadAllLines(f0, Utf8));
        string ini = Path.Combine(SaveDir, "undertale.ini");
        s.Ini = File.Exists(ini) ? SaveState.ParseIni(File.ReadAllText(ini, Utf8)) : new List<KeyValuePair<string, List<KeyValuePair<string, string>>>>();
        writtenByLauncher = !Progressed();
        return s;
    }

    public static string SafeName(string n)
    {
        n = new string((n ?? "").Trim().Where(ch => Array.IndexOf(Path.GetInvalidFileNameChars(), ch) < 0 && ch != '|').ToArray());
        return n.Length > 60 ? n.Substring(0, 60).Trim() : n;
    }

    public static void SaveCustom(string name, SaveState st)
    {
        Directory.CreateDirectory(CustomDir);
        string b = Path.Combine(CustomDir, name);
        File.WriteAllText(b + ".file0", st.File0Text(), Utf8);
        File.WriteAllText(b + ".ini", st.IniText(), Utf8);
        if (!string.IsNullOrEmpty(st.Source) && st.Custom) File.WriteAllText(b + ".txt", st.Source, Utf8);
    }

    public static void DeleteCustom(string name)
    {
        string b = Path.Combine(CustomDir, name);
        foreach (string ext in new[] { ".file0", ".ini", ".txt" }) if (File.Exists(b + ext)) File.Delete(b + ext);
    }

    // ---- Réglages (clé=valeur) ----
    static Dictionary<string, string> ReadSettings()
    {
        var d = new Dictionary<string, string>();
        try
        {
            if (File.Exists(SettingsFile))
                foreach (string l in File.ReadAllLines(SettingsFile, Utf8))
                {
                    int eq = l.IndexOf('=');
                    if (eq > 0) d[l.Substring(0, eq)] = l.Substring(eq + 1);
                }
        }
        catch { }
        return d;
    }

    public static string GetSetting(string key) { string v; return ReadSettings().TryGetValue(key, out v) ? v : null; }

    public static void SetSetting(string key, string value)
    {
        var d = ReadSettings();
        d[key] = value;
        Directory.CreateDirectory(AppDir);
        File.WriteAllLines(SettingsFile, d.Select(kv => kv.Key + "=" + kv.Value), Utf8);
    }

    // ---- Favoris et récents ----
    public static List<string> GetList(string key) { string v = GetSetting(key); return string.IsNullOrEmpty(v) ? new List<string>() : v.Split('|').Where(x => x.Length > 0).ToList(); }
    public static void SetList(string key, List<string> l) { SetSetting(key, string.Join("|", l)); }
    public static bool IsFavorite(string k) { return GetList("fav").Contains(k); }
    public static void ToggleFavorite(string k) { var l = GetList("fav"); if (l.Contains(k)) l.Remove(k); else l.Add(k); SetList("fav", l); }
    public static void AddRecent(string k) { var l = GetList("recent"); l.Remove(k); l.Insert(0, k); SetList("recent", l.Take(6).ToList()); }

    public static string Hash(string file)
    {
        if (!File.Exists(file)) return "";
        using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(file)));
    }

    // Empreinte de toute la partie : file0 (save), file8 (combat contre Flowey) et undertale.ini
    public static string SaveHash()
    {
        return Hash(Path.Combine(SaveDir, "file0")) + "|" + Hash(Path.Combine(SaveDir, "file8")) + "|" + Hash(Path.Combine(SaveDir, "undertale.ini"));
    }

    public static bool HasSave() { return File.Exists(Path.Combine(SaveDir, "file0")) || File.Exists(Path.Combine(SaveDir, "file8")); }

    // true = le jeu a modifié la partie depuis que Soul Launcher l'a écrite (ou partie jamais écrite par lui)
    public static bool Progressed()
    {
        if (!HasSave()) return false;
        string last = GetSetting("lastSave");
        if (string.IsNullOrEmpty(last))
        {
            // Anciennes versions : seule l'empreinte de file0 était gardée
            string old = GetSetting("lastWritten");
            return string.IsNullOrEmpty(old) || old != Hash(Path.Combine(SaveDir, "file0"));
        }
        return last != SaveHash();
    }

    // ---- Copies de sécurité ----
    public static string BackupIfNeeded()
    {
        if (!Directory.Exists(SaveDir) || !HasSave()) return null;
        if (!Progressed()) return null;
        string dest = Path.Combine(BackupDir, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
        Directory.CreateDirectory(dest);
        foreach (string f in Directory.GetFiles(SaveDir)) File.Copy(f, Path.Combine(dest, Path.GetFileName(f)), true);
        var all = Directory.GetDirectories(BackupDir).Where(x => Path.GetFileName(x) != "fin_genocide").OrderByDescending(x => x).ToList();
        foreach (string old in all.Skip(30)) { try { Directory.Delete(old, true); } catch { } }
        return dest;
    }

    public static string LatestBackup()
    {
        if (!Directory.Exists(BackupDir)) return null;
        return Directory.GetDirectories(BackupDir).Where(x => Path.GetFileName(x) != "fin_genocide").OrderByDescending(x => x).FirstOrDefault();
    }

    public static void RestoreBackup(string dir)
    {
        Directory.CreateDirectory(SaveDir);
        foreach (string n in new[] { "file0", "file8", "file9", "undertale.ini" })
        {
            string p = Path.Combine(SaveDir, n);
            if (File.Exists(p)) File.Delete(p);
        }
        foreach (string f in Directory.GetFiles(dir)) File.Copy(f, Path.Combine(SaveDir, Path.GetFileName(f)), true);
        SetSetting("lastWritten", ""); SetSetting("lastSave", "");
    }

    public static bool GameRunning()
    {
        return Process.GetProcessesByName("Undertale").Length > 0 || Process.GetProcessesByName("UNDERTALE").Length > 0;
    }

    // ---- Fin génocide ----
    // system_information_962 = monde effacé (écran noir + vent au démarrage) ; 963 = âme vendue
    public static readonly string[] GenocideFiles = { "system_information_962", "system_information_963" };
    public static bool ClearGenocideOnLaunch = true;
    public static bool Fullscreen;

    static List<string> GenocideLocations()
    {
        var l = new List<string> { SaveDir };
        l.AddRange(SteamCloud.RemoteDirs());
        return l;
    }

    public static bool GenocideEnded()
    {
        return GenocideLocations().Any(d => GenocideFiles.Any(f => File.Exists(Path.Combine(d, f))));
    }

    static string KeepDir(string location)
    {
        string tag = location == SaveDir ? "saves" : "cloud_" + Math.Abs(location.ToLowerInvariant().GetHashCode());
        return Path.Combine(BackupDir, "fin_genocide", tag);
    }

    public static void ClearGenocide()
    {
        foreach (string d in GenocideLocations())
            foreach (string n in GenocideFiles)
            {
                string p = Path.Combine(d, n);
                if (!File.Exists(p)) continue;
                string keep = KeepDir(d);
                Directory.CreateDirectory(keep);
                File.Copy(p, Path.Combine(keep, n), true);
                File.WriteAllText(Path.Combine(keep, "origine.txt"), d, Utf8);
                try { File.Delete(p); } catch { }
            }
    }

    public static bool HasGenocideBackup() { return Directory.Exists(Path.Combine(BackupDir, "fin_genocide")); }

    public static void RestoreGenocide()
    {
        string root = Path.Combine(BackupDir, "fin_genocide");
        if (!Directory.Exists(root)) return;
        foreach (string keep in Directory.GetDirectories(root))
        {
            string origin = Path.Combine(keep, "origine.txt");
            string dest = File.Exists(origin) ? File.ReadAllText(origin, Utf8).Trim() : (Path.GetFileName(keep) == "saves" ? SaveDir : null);
            if (dest == null) continue;
            try
            {
                Directory.CreateDirectory(dest);
                foreach (string f in Directory.GetFiles(keep).Where(f => Path.GetFileName(f) != "origine.txt")) File.Copy(f, Path.Combine(dest, Path.GetFileName(f)), true);
            }
            catch { }
        }
    }

    // true = on peut lancer
    public static bool CheckCloud(IWin32Window owner)
    {
        if (!ClearGenocideOnLaunch) return true;
        bool hadSteam = SteamCloud.RemoteDirs().Any(d => !d.Contains("CODEX") && GenocideFiles.Any(f => File.Exists(Path.Combine(d, f))));
        ClearGenocide();
        string stuck = null;
        foreach (string d in SteamCloud.RemoteDirs())
            foreach (string n in GenocideFiles)
                if (File.Exists(Path.Combine(d, n))) stuck = Path.Combine(d, n);
        if (stuck != null)
        {
            Dlg.Show(owner,
                L.S("Le Steam Cloud garde la fin génocide et la remet à chaque démarrage (écran noir).\n\nWindows empêche Soul Launcher de supprimer ce fichier :\n",
                    "Steam Cloud keeps the genocide ending and puts it back on every start (black screen).\n\nWindows blocks Soul Launcher from deleting this file:\n") + stuck + "\n\n" +
                L.S("Le dossier va s'ouvrir : supprime ce fichier à la main (une copie est déjà sauvegardée), puis relance. C'est à faire une seule fois.",
                    "The folder will open: delete this file by hand (a copy is already backed up), then launch again. You only need to do this once."),
                Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            Process.Start("explorer.exe", "/select,\"" + stuck + "\"");
            return false;
        }
        if (hadSteam && GetSetting("steamCloudTip") != "1")
        {
            SetSetting("steamCloudTip", "1");
            Dlg.Show(owner,
                L.S("La fin génocide était aussi dans ton Steam Cloud : Soul Launcher l'a retirée (copie gardée).\n\nSi l'écran noir revient quand même, c'est que Steam la retélécharge : dans Steam, clic droit sur UNDERTALE → Propriétés → Général → décoche « Conserver les sauvegardes dans Steam Cloud », puis relance depuis Soul Launcher.",
                    "The genocide ending was also in your Steam Cloud: Soul Launcher removed it (copy kept).\n\nIf the black screen still comes back, Steam is downloading it again: in Steam, right-click UNDERTALE → Properties → General → untick \"Keep games saves in the Steam Cloud\", then launch again from Soul Launcher."),
                Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        return true;
    }

    public static void WriteSave(SaveState st)
    {
        BackupIfNeeded();
        if (ClearGenocideOnLaunch) ClearGenocide();
        Directory.CreateDirectory(SaveDir);
        string f0 = st.File0Text();
        File.WriteAllText(Path.Combine(SaveDir, "file0"), f0, Utf8);
        File.WriteAllText(Path.Combine(SaveDir, "file9"), f0, Utf8);
        string f8 = Path.Combine(SaveDir, "file8");
        if (File.Exists(f8)) File.Delete(f8);
        File.WriteAllText(Path.Combine(SaveDir, "undertale.ini"), st.IniText(), Utf8);
        SetSetting("lastSave", SaveHash());
    }

    public static bool WriteAndLaunch(SaveState st, string exe, IWin32Window owner)
    {
        if (GameRunning()) throw new Exception(L.S("UNDERTALE est déjà lancé. Ferme-le d'abord, sinon il écrasera la sauvegarde.", "UNDERTALE is already running. Close it first, or it will overwrite the save."));
        if (!CheckCloud(owner)) return false;
        // La partie a avancé depuis (ex. Flowey qui ferme le jeu exprès) : on demande avant d'écraser
        if (Progressed())
        {
            DialogResult r = Dlg.Show(owner,
                L.S("Ta partie a avancé depuis le dernier lancement (par exemple Flowey qui ferme le jeu exprès pendant son combat).\n\nTu veux la continuer, ou repartir du preset choisi ? Si tu repars du preset, ta partie est d'abord copiée (bouton « Restaurer ma vraie sauvegarde »).",
                    "Your game has moved on since the last launch (for example Flowey closing the game on purpose during his fight).\n\nContinue it, or start over from the chosen preset? If you start over, your game is copied first (\"Restore my real save\" button)."),
                L.S("Partie en cours", "Game in progress"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question,
                new[] { L.S("Continuer ma partie", "Continue my game"), L.S("Repartir du preset", "Use the preset"), L.S("Annuler", "Cancel") });
            if (r == DialogResult.Cancel) return false;
            if (r == DialogResult.Yes) { StartGame(exe); return true; }
        }
        WriteSave(st);
        if (!string.IsNullOrEmpty(st.Key)) AddRecent(st.Key);
        StartGame(exe);
        return true;
    }

    // Lance le jeu sans toucher à la sauvegarde
    public static bool ContinueGame(string exe, IWin32Window owner)
    {
        if (GameRunning()) throw new Exception(L.S("UNDERTALE est déjà lancé.", "UNDERTALE is already running."));
        if (!CheckCloud(owner)) return false;
        StartGame(exe);
        return true;
    }

    static void StartGame(string exe)
    {
        ProcessStartInfo psi = new ProcessStartInfo(exe);
        psi.WorkingDirectory = Path.GetDirectoryName(exe);
        psi.UseShellExecute = false;
        Process game = Process.Start(psi);
        if (Fullscreen && game != null) GoFullscreen(game);
    }

    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

    // UNDERTALE ne retient pas le plein écran : on attend sa fenêtre puis on appuie sur F4
    static void GoFullscreen(Process game)
    {
        var t = new System.Threading.Thread(() =>
        {
            try
            {
                for (int i = 0; i < 100 && !game.HasExited; i++)
                {
                    game.Refresh();
                    if (game.MainWindowHandle != IntPtr.Zero) break;
                    System.Threading.Thread.Sleep(200);
                }
                if (game.HasExited || game.MainWindowHandle == IntPtr.Zero) return;
                System.Threading.Thread.Sleep(1500);
                SetForegroundWindow(game.MainWindowHandle);
                System.Threading.Thread.Sleep(200);
                keybd_event(0x73, 0x3E, 0, UIntPtr.Zero);
                System.Threading.Thread.Sleep(120);
                keybd_event(0x73, 0x3E, 2, UIntPtr.Zero);
            }
            catch { }
        });
        t.IsBackground = false;
        t.Start();
    }
}

// ---------- Steam : bibliothèques et Steam Cloud ----------
static class SteamCloud
{
    public const string AppId = "391540";

    public static string SteamPath()
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
            {
                string p = k == null ? null : k.GetValue("SteamPath") as string;
                if (!string.IsNullOrEmpty(p) && Directory.Exists(p)) return p.Replace('/', '\\');
            }
        }
        catch { }
        string def = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
        return Directory.Exists(def) ? def : null;
    }

    public static List<string> Libraries()
    {
        var libs = new List<string>();
        string sp = SteamPath();
        if (sp == null) return libs;
        libs.Add(sp);
        try
        {
            string vdf = Path.Combine(sp, @"steamapps\libraryfolders.vdf");
            if (File.Exists(vdf))
                foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                {
                    string p = m.Groups[1].Value.Replace("\\\\", "\\");
                    if (Directory.Exists(p) && !libs.Contains(p, StringComparer.OrdinalIgnoreCase)) libs.Add(p);
                }
        }
        catch { }
        return libs;
    }

    // Dossiers « remote » du Steam Cloud d'UNDERTALE (vrai Steam + émulateurs courants)
    public static List<string> RemoteDirs()
    {
        var dirs = new List<string>();
        string sp = SteamPath();
        if (sp != null)
        {
            string ud = Path.Combine(sp, "userdata");
            try
            {
                if (Directory.Exists(ud))
                    foreach (string user in Directory.GetDirectories(ud))
                    {
                        string r = Path.Combine(user, AppId, "remote");
                        if (Directory.Exists(r)) dirs.Add(r);
                    }
            }
            catch { }
        }
        foreach (string r in new[] {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), @"Steam\CODEX\" + AppId + @"\remote"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"Steam\CODEX\" + AppId + @"\remote") })
            if (Directory.Exists(r)) dirs.Add(r);
        return dirs;
    }
}

// ---------- Détection automatique du jeu ----------
static class GameFinder
{
    public static bool IsGame(string exe)
    {
        try
        {
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return false;
            string n = Path.GetFileName(exe);
            return n.Equals("UNDERTALE.exe", StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(Path.GetDirectoryName(exe), "data.win"));
        }
        catch { return false; }
    }

    static IEnumerable<string> InDir(string dir)
    {
        if (string.IsNullOrEmpty(dir)) yield break;
        yield return Path.Combine(dir, "UNDERTALE.exe");
        yield return Path.Combine(dir, "Undertale", "UNDERTALE.exe");
    }

    // Emplacements connus : Steam, GOG, Xbox, programmes installés, dossiers habituels
    public static List<string> KnownLocations()
    {
        var c = new List<string>();
        c.AddRange(InDir(AppDomain.CurrentDomain.BaseDirectory));
        foreach (string lib in SteamCloud.Libraries()) c.Add(Path.Combine(lib, @"steamapps\common\Undertale\UNDERTALE.exe"));
        foreach (RegistryKey root in new[] { Registry.LocalMachine, Registry.CurrentUser })
            foreach (string sub in new[] { @"SOFTWARE\WOW6432Node\GOG.com\Games", @"SOFTWARE\GOG.com\Games" })
                try
                {
                    using (RegistryKey games = root.OpenSubKey(sub))
                        if (games != null)
                            foreach (string id in games.GetSubKeyNames())
                                using (RegistryKey g = games.OpenSubKey(id))
                                {
                                    string name = (g.GetValue("gameName") ?? g.GetValue("GAMENAME") ?? "") as string;
                                    if (name != null && name.IndexOf("undertale", StringComparison.OrdinalIgnoreCase) >= 0)
                                        c.AddRange(InDir((g.GetValue("path") ?? g.GetValue("PATH")) as string));
                                }
                }
                catch { }
        foreach (RegistryKey root in new[] { Registry.LocalMachine, Registry.CurrentUser })
            foreach (string sub in new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" })
                try
                {
                    using (RegistryKey un = root.OpenSubKey(sub))
                        if (un != null)
                            foreach (string id in un.GetSubKeyNames())
                                using (RegistryKey g = un.OpenSubKey(id))
                                {
                                    string name = g.GetValue("DisplayName") as string;
                                    if (name != null && name.IndexOf("undertale", StringComparison.OrdinalIgnoreCase) >= 0)
                                        c.AddRange(InDir(g.GetValue("InstallLocation") as string));
                                }
                }
                catch { }
        string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        foreach (string d in new[] { Path.Combine(pf86, @"GOG Galaxy\Games\Undertale"), Path.Combine(pf, @"GOG Galaxy\Games\Undertale"), @"C:\GOG Games\Undertale", @"C:\XboxGames\Undertale\Content" })
            c.AddRange(InDir(d));
        return c.Where(IsGame).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    // Recherche plus large dans les dossiers de l'utilisateur et les dossiers de jeux (profondeur limitée)
    public static List<string> DeepSearch()
    {
        var found = new List<string>();
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var roots = new List<string> {
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Path.Combine(home, "Downloads"), Path.Combine(home, "Games"), Path.Combine(home, "OneDrive") };
        foreach (DriveInfo d in DriveInfo.GetDrives().Where(x => x.DriveType == DriveType.Fixed && x.IsReady))
            foreach (string n in new[] { "Games", "Jeux", "Program Files (x86)", "Program Files", "SteamLibrary", "XboxGames" })
                roots.Add(Path.Combine(d.RootDirectory.FullName, n));
        foreach (string r in roots.Distinct(StringComparer.OrdinalIgnoreCase)) Walk(r, 0, found);
        return found.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    static void Walk(string dir, int depth, List<string> found)
    {
        if (depth > 4 || found.Count > 10 || !Directory.Exists(dir)) return;
        try
        {
            foreach (string f in Directory.GetFiles(dir, "UNDERTALE.exe")) if (IsGame(f)) found.Add(f);
            foreach (string sub in Directory.GetDirectories(dir))
            {
                string n = Path.GetFileName(sub);
                if (n.StartsWith(".") || n.Equals("Windows", StringComparison.OrdinalIgnoreCase) || n.Equals("node_modules", StringComparison.OrdinalIgnoreCase)) continue;
                Walk(sub, depth + 1, found);
            }
        }
        catch { }
    }

    public static string Resolve()
    {
        string saved = Store.GetSetting("game");
        if (IsGame(saved)) return saved;
        string first = KnownLocations().FirstOrDefault();
        if (first != null) Store.SetSetting("game", first);
        return first;
    }
}

// ---------- Fichiers .soul (partage de saves) ----------
static class SoulFile
{
    // Format texte : en-tête, puis le file0 (549 lignes) et le undertale.ini
    public static void Export(SaveState s, string name, string path)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("SOUL-LAUNCHER-SAVE 1\r\n");
        sb.Append("name=").Append(name.Replace("\r", " ").Replace("\n", " ")).Append("\r\n");
        sb.Append("route=").Append(s.Route()).Append("\r\n");
        sb.Append("exported=").Append(DateTime.Now.ToString("yyyy-MM-dd")).Append("\r\n");
        sb.Append("---file0---\r\n").Append(s.File0Text());
        sb.Append("---ini---\r\n").Append(s.IniText());
        File.WriteAllText(path, sb.ToString(), Store.Utf8);
    }

    public static SaveState Import(string path, out string name)
    {
        name = Path.GetFileNameWithoutExtension(path);
        string[] lines = File.ReadAllLines(path, Store.Utf8);
        if (Path.GetFileName(path).Equals("file0", StringComparison.OrdinalIgnoreCase))
        {
            SaveState raw = new SaveState();
            raw.Lines = SaveState.Normalize(lines);
            string ini = Path.Combine(Path.GetDirectoryName(path), "undertale.ini");
            raw.Ini = File.Exists(ini) ? SaveState.ParseIni(File.ReadAllText(ini, Store.Utf8)) : new List<KeyValuePair<string, List<KeyValuePair<string, string>>>>();
            name = raw.Lines[0] + " LV" + raw.Get(2) + " · " + raw.RoomName();
            Validate(raw);
            return raw;
        }
        if (lines.Length == 0 || !lines[0].StartsWith("SOUL-LAUNCHER-SAVE")) throw new Exception(L.S("Ce fichier n'est pas une save Soul Launcher.", "This file is not a Soul Launcher save."));
        int f0 = Array.IndexOf(lines, "---file0---"), ini0 = Array.IndexOf(lines, "---ini---");
        if (f0 < 0 || ini0 < f0) throw new Exception(L.S("Fichier .soul abîmé.", "Damaged .soul file."));
        foreach (string l in lines.Take(f0)) if (l.StartsWith("name=")) name = l.Substring(5).Trim();
        SaveState s = new SaveState();
        s.Lines = SaveState.Normalize(lines.Skip(f0 + 1).Take(ini0 - f0 - 1).ToArray());
        s.Ini = SaveState.ParseIni(string.Join("\n", lines.Skip(ini0 + 1)));
        Validate(s);
        return s;
    }

    static void Validate(SaveState s)
    {
        double d;
        for (int i = 1; i < 549; i++)
            if (!double.TryParse(s.Lines[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d))
                throw new Exception(L.S("Save invalide (ligne ", "Invalid save (line ") + (i + 1) + ").");
        int room = s.Get(548);
        if (room < 0 || room >= GameData.Rooms.Length) throw new Exception(L.S("Save invalide : salle inconnue.", "Invalid save: unknown room."));
    }

    [DllImport("shell32.dll")] static extern void SHChangeNotify(int e, uint f, IntPtr a, IntPtr b);

    // Associe les .soul à Soul Launcher pour l'utilisateur courant (pas besoin d'être admin)
    public static void RegisterAssociation()
    {
        string exe = Application.ExecutablePath;
        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.soul")) k.SetValue("", "SoulLauncher.Save");
        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\SoulLauncher.Save"))
        {
            k.SetValue("", L.S("Save Soul Launcher", "Soul Launcher save"));
            using (RegistryKey ic = k.CreateSubKey("DefaultIcon")) ic.SetValue("", (File.Exists(LogoIcons.IcoPath) ? LogoIcons.IcoPath : exe) + ",0");
            using (RegistryKey cmd = k.CreateSubKey(@"shell\open\command")) cmd.SetValue("", "\"" + exe + "\" \"%1\"");
        }
        SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    }

    public static bool IsAssociated()
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Classes\SoulLauncher.Save\shell\open\command"))
                return k != null && ((k.GetValue("") as string) ?? "").IndexOf(Application.ExecutablePath, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch { return false; }
    }
}

