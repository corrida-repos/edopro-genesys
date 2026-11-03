using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace GenesysDeckPoints {
  public sealed class Settings { public bool followSaved = true; public bool onTop = true; public int x = -1, y = -1, width = 420, height = 560; public string deck; }
  public static class Json {
    public static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer { MaxJsonLength = 10000000 };
    public static T Read<T>(string path) { return Serializer.Deserialize<T>(File.ReadAllText(path, Encoding.UTF8)); }
    public static void Save(string path, object data) { File.WriteAllText(path, Serializer.Serialize(data), new UTF8Encoding(false)); }
  }
  // Reads the points and bans from lflists/genesys.lflist.conf: "code copies [points] # name".
  public sealed class PointList {
    public Dictionary<uint, int> points = new Dictionary<uint, int>();
    public HashSet<uint> banned = new HashSet<uint>();
    public int cap = 100; public string name = "Genesys"; public DateTime written;
    public static PointList Load(string file) {
      var list = new PointList { written = File.GetLastWriteTime(file) }; bool inList = false;
      foreach(string raw in File.ReadAllLines(file, Encoding.UTF8)) {
        string line = raw; int hash = line.IndexOf('#'); if(hash >= 0) line = line.Substring(0, hash); line = line.Trim();
        if(line.Length == 0) continue;
        if(line[0] == '!') { if(inList) break; inList = true; list.name = line.Substring(1).Trim(); continue; }
        string[] parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        if(parts[0] == "$points_limit") { int cap; if(parts.Length > 1 && int.TryParse(parts[1], out cap)) list.cap = cap; continue; }
        uint code; int copies, value;
        if(parts.Length < 2 || !uint.TryParse(parts[0], out code) || !int.TryParse(parts[1], out copies)) continue;
        if(copies == 0) list.banned.Add(code);
        if(parts.Length > 2 && int.TryParse(parts[2], out value) && value > 0) list.points[code] = value;
      }
      if(list.points.Count < 200) throw new InvalidDataException("The Genesys list has no points column or looks incomplete: " + file);
      return list;
    }
  }
  public static class ListFile {
    // The GitHub-updated copy wins over a manually installed one.
    public static string Find(string root) {
      string repo = Path.Combine(root,"repositories","genesys","lflists","genesys.lflist.conf");
      return File.Exists(repo) ? repo : Path.Combine(root,"lflists","genesys.lflist.conf");
    }
  }
  public sealed class Card { public uint code, alias, type; public string name; }
  public sealed class Cards {
    public Dictionary<uint, Card> All = new Dictionary<uint, Card>();
    [DllImport("sqlite3.dll", CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_open_v2(byte[] file, out IntPtr db, int flags, IntPtr vfs);
    [DllImport("sqlite3.dll", CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_prepare_v2(IntPtr db, byte[] sql, int n, out IntPtr stmt, IntPtr tail);
    [DllImport("sqlite3.dll", CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_step(IntPtr stmt);
    [DllImport("sqlite3.dll", CallingConvention=CallingConvention.Cdecl)] static extern long sqlite3_column_int64(IntPtr stmt, int col);
    [DllImport("sqlite3.dll", CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_column_text(IntPtr stmt, int col);
    [DllImport("sqlite3.dll", CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_finalize(IntPtr stmt);
    [DllImport("sqlite3.dll", CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_close(IntPtr db);
    static byte[] Utf8(string s) { return Encoding.UTF8.GetBytes(s + "\0"); }
    static string Text(IntPtr p) {
      int n = 0; while(Marshal.ReadByte(p,n) != 0) n++;
      byte[] b = new byte[n]; Marshal.Copy(p,b,0,n); return Encoding.UTF8.GetString(b);
    }
    public static Cards Load(string root) {
      var cards = new Cards();
      var files = new List<string>();
      string primary = Path.Combine(root,"expansions","cards.cdb");
      if(File.Exists(primary)) files.Add(primary);
      if(Directory.Exists(Path.Combine(root,"expansions"))) files.AddRange(Directory.GetFiles(Path.Combine(root,"expansions"),"*.cdb").Where(p => p != primary).OrderBy(p=>p));
      if(Directory.Exists(Path.Combine(root,"repositories"))) files.AddRange(Directory.GetFiles(Path.Combine(root,"repositories"),"*.cdb",SearchOption.AllDirectories).Where(p=>p.IndexOf("languages",StringComparison.OrdinalIgnoreCase)<0).OrderBy(p=>p));
      foreach(string file in files) {
        IntPtr db, stmt = IntPtr.Zero;
        if(sqlite3_open_v2(Utf8(file),out db,1,IntPtr.Zero) != 0) { if(db != IntPtr.Zero) sqlite3_close(db); continue; }
        try {
          if(sqlite3_prepare_v2(db,Utf8("SELECT d.id,d.alias,d.type,t.name FROM datas d JOIN texts t ON d.id=t.id"),-1,out stmt,IntPtr.Zero) != 0) continue;
          int result;
          while((result=sqlite3_step(stmt)) == 100) {
            var c = new Card { code=(uint)sqlite3_column_int64(stmt,0), alias=(uint)sqlite3_column_int64(stmt,1), type=(uint)sqlite3_column_int64(stmt,2), name=Text(sqlite3_column_text(stmt,3)) };
            cards.All[c.code]=c;
          }
          if(result != 101) throw new InvalidDataException("Could not finish reading card database: " + file);
        } finally { if(stmt != IntPtr.Zero) sqlite3_finalize(stmt); sqlite3_close(db); }
      }
      if(cards.All.Count < 1000) throw new InvalidDataException("EDOPro card databases were not found.");
      return cards;
    }
    // Alternate artworks use their original card's entry, as EDOPro does for banlists.
    public uint Canonical(uint code) {
      Card c; return All.TryGetValue(code,out c) && c.alias != 0 && Math.Abs((long)c.code-c.alias) < 10 ? c.alias : code;
    }
    public string Name(uint code) { Card c; return All.TryGetValue(code,out c) ? c.name : "Unknown card " + code; }
  }
  public sealed class Deck {
    public uint[][] sections = { new uint[0], new uint[0], new uint[0] };
    public static Deck FromFile(string file) {
      var lists = new[] { new List<uint>(), new List<uint>(), new List<uint>() }; int section=0;
      foreach(string line in File.ReadAllLines(file)) {
        string s=line.Trim(); if(s=="#main")section=0; else if(s=="#extra")section=1; else if(s=="!side")section=2; else { uint id; if(uint.TryParse(s,out id)) lists[section].Add(id); }
      }
      return new Deck { sections=lists.Select(x=>x.ToArray()).ToArray() };
    }
  }
  public sealed class Row { public string Section, Card; public int Copies, Each, Points; }
  public sealed class Evaluation {
    public static readonly string[] Names = {"Main","Extra","Side"};
    public int[] subtotal = new int[3]; public int total, unknown, forbidden;
    public List<Row> rows = new List<Row>(), notAllowed = new List<Row>(); public List<string> problems = new List<string>();
    public static Evaluation Of(Deck deck, Cards cards, PointList list) {
      var r = new Evaluation();
      for(int s=0;s<3;s++) foreach(var group in deck.sections[s].GroupBy(x=>x)) {
        uint code = group.Key, canonical = cards.Canonical(code); int n = group.Count(), each;
        bool known = cards.All.ContainsKey(code) || list.points.ContainsKey(code) || list.banned.Contains(code);
        if(!known) { r.unknown += n; r.problems.Add("Unknown card " + code + " (" + Names[s] + ")"); continue; }
        Card c; bool linkOrPendulum = cards.All.TryGetValue(code,out c) && (c.type & (0x1000000U|0x4000000U)) != 0;
        if(linkOrPendulum || list.banned.Contains(code) || list.banned.Contains(canonical)) { r.forbidden += n; r.problems.Add("Not allowed (Link/Pendulum): " + cards.Name(code)); r.notAllowed.Add(new Row { Section=Names[s], Card=cards.Name(code), Copies=n }); }
        if(!list.points.TryGetValue(code,out each)) list.points.TryGetValue(canonical,out each);
        r.subtotal[s] += each*n;
        if(each > 0) r.rows.Add(new Row { Section=Names[s], Card=cards.Name(code), Copies=n, Each=each, Points=each*n });
      }
      r.total = r.subtotal.Sum(); return r;
    }
  }
  public sealed class MainForm : Form {
    readonly string root, home, deckDir; string listFile; readonly Cards cards; PointList list; readonly Settings settings;
    readonly ComboBox decks = new ComboBox(); readonly CheckBox follow = new CheckBox(), onTop = new CheckBox();
    readonly Label total = new Label(), breakdown = new Label(), warning = new Label(), status = new Label();
    readonly ListView rows = new ListView(); readonly FileSystemWatcher deckWatch, listWatch;
    readonly Timer debounce = new Timer { Interval = 300 }; string changed; bool listChanged;
    readonly float dpi; static readonly Color Back = Color.FromArgb(25,21,36), Ok = Color.FromArgb(104,231,163), Over = Color.FromArgb(255,99,112), Warn = Color.FromArgb(255,208,90);

    public MainForm(string r, string h, Cards c) {
      root=r; home=h; cards=c; deckDir=Path.Combine(root,"deck"); listFile=ListFile.Find(root); Directory.CreateDirectory(deckDir);
      try { settings = Json.Read<Settings>(Path.Combine(home,"settings.json")); } catch { settings = new Settings(); }
      Text="Genesys deck points"; BackColor=Back; ForeColor=Color.White; Font=new Font("Segoe UI",9.5f);
      using(var g = CreateGraphics()) dpi = g.DpiX / 96f; // sizes below are at 100% scaling
      MinimumSize=Scaled(380,380); Size=Scaled(settings.width,settings.height); TopMost=settings.onTop;
      if(settings.x >= 0 && SystemInformation.VirtualScreen.Contains(settings.x,settings.y)) { StartPosition=FormStartPosition.Manual; Location=new Point(settings.x,settings.y); }

      var top = new TableLayoutPanel { Dock=DockStyle.Top, AutoSize=true, AutoSizeMode=AutoSizeMode.GrowAndShrink, ColumnCount=2, Padding=new Padding(10,8,10,6) };
      top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      decks.Dock=DockStyle.Fill; decks.DropDownStyle=ComboBoxStyle.DropDownList; decks.DropDownHeight=400;
      follow.Text="Follow last saved deck"; follow.AutoSize=true; follow.Checked=settings.followSaved;
      onTop.Text="Always on top"; onTop.AutoSize=true; onTop.Checked=settings.onTop;
      total.AutoSize=true; total.Font=new Font("Segoe UI",22,FontStyle.Bold); total.TextAlign=ContentAlignment.MiddleLeft;
      breakdown.AutoSize=true; breakdown.ForeColor=Color.Gainsboro;
      warning.AutoSize=true; warning.ForeColor=Over; warning.Font=new Font("Segoe UI",9.5f,FontStyle.Bold);
      status.AutoSize=true; status.ForeColor=Color.Silver; status.Font=new Font("Segoe UI",8.5f);
      top.Controls.Add(decks,0,0); top.SetColumnSpan(decks,2);
      top.Controls.Add(follow,0,1); top.Controls.Add(onTop,1,1);
      top.Controls.Add(total,0,2); top.SetColumnSpan(total,2);
      top.Controls.Add(breakdown,0,3); top.SetColumnSpan(breakdown,2);
      top.Controls.Add(warning,0,4); top.SetColumnSpan(warning,2);
      top.Controls.Add(status,0,5); top.SetColumnSpan(status,2);
      for(int i=0;i<6;i++) top.RowStyles.Add(new RowStyle(SizeType.AutoSize));

      rows.Dock=DockStyle.Fill; rows.View=View.Details; rows.FullRowSelect=true; rows.HeaderStyle=ColumnHeaderStyle.Nonclickable;
      rows.BackColor=Color.FromArgb(34,29,48); rows.ForeColor=Color.White; rows.BorderStyle=BorderStyle.None;
      rows.Columns.Add("Card",S(200)); rows.Columns.Add("Pile",S(50)); rows.Columns.Add("×",S(30),HorizontalAlignment.Right); rows.Columns.Add("Each",S(55),HorizontalAlignment.Right); rows.Columns.Add("Pts",S(40),HorizontalAlignment.Right);
      Controls.Add(rows); Controls.Add(top);
      Resize += (s,e) => rows.Columns[0].Width = Math.Max(S(120), rows.ClientSize.Width-S(180)-SystemInformation.VerticalScrollBarWidth);

      decks.SelectedIndexChanged += (s,e) => { settings.deck = decks.SelectedItem as string; Evaluate(); };
      follow.CheckedChanged += (s,e) => settings.followSaved = follow.Checked;
      onTop.CheckedChanged += (s,e) => { settings.onTop = onTop.Checked; TopMost = onTop.Checked; };
      FormClosing += (s,e) => { if(WindowState==FormWindowState.Normal) { settings.x=Left; settings.y=Top; settings.width=(int)(Width/dpi); settings.height=(int)(Height/dpi); } try { Json.Save(Path.Combine(home,"settings.json"),settings); } catch {} };

      // EDOPro writes the .ydk when you save; the watcher picks it up and recounts.
      deckWatch = new FileSystemWatcher(deckDir,"*.ydk") { IncludeSubdirectories=true, NotifyFilter=NotifyFilters.LastWrite|NotifyFilters.FileName, SynchronizingObject=this };
      FileSystemEventHandler onDeck = (s,e) => { changed = e.FullPath; debounce.Stop(); debounce.Start(); };
      deckWatch.Changed += onDeck; deckWatch.Created += onDeck; deckWatch.Renamed += (s,e) => onDeck(s,e); deckWatch.Deleted += (s,e) => { debounce.Stop(); debounce.Start(); };
      // Watch the whole folder: on a fresh install EDOPro downloads the list into repositories\genesys on its first launch.
      listWatch = new FileSystemWatcher(root,"genesys.lflist.conf") { IncludeSubdirectories=true, NotifyFilter=NotifyFilters.LastWrite|NotifyFilters.FileName, SynchronizingObject=this };
      FileSystemEventHandler onList = (s,e) => { listChanged = true; debounce.Stop(); debounce.Start(); };
      listWatch.Changed += onList; listWatch.Created += onList; listWatch.Renamed += (s,e) => onList(s,e);
      debounce.Tick += (s,e) => { debounce.Stop(); OnFilesChanged(); };

      LoadList(); FillDecks(settings.followSaved || settings.deck == null ? Latest() : settings.deck);
      deckWatch.EnableRaisingEvents = true; listWatch.EnableRaisingEvents = true;
    }
    int S(int v) { return (int)(v*dpi); }
    Size Scaled(int w, int h) { return new Size(S(w),S(h)); }
    string Relative(string path) { return path.Substring(deckDir.Length).TrimStart('\\','/'); }
    string Latest() { var f = new DirectoryInfo(deckDir).GetFiles("*.ydk",SearchOption.AllDirectories).OrderByDescending(x=>x.LastWriteTimeUtc).FirstOrDefault(); return f == null ? null : Relative(f.FullName); }
    void FillDecks(string select) {
      decks.BeginUpdate(); decks.Items.Clear();
      foreach(string f in Directory.GetFiles(deckDir,"*.ydk",SearchOption.AllDirectories).Select(Relative).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase)) decks.Items.Add(f);
      decks.EndUpdate();
      int i = select == null ? -1 : decks.Items.IndexOf(select);
      if(i < 0 && decks.Items.Count > 0) i = 0;
      if(i >= 0) decks.SelectedIndex = i; else Evaluate();
    }
    void LoadList() {
      listFile = ListFile.Find(root);
      try { list = PointList.Load(listFile); }
      catch(Exception e) { if(list == null) status.Text = e is FileNotFoundException || e is DirectoryNotFoundException ? "Start EDOPro once so it can download the Genesys list." : e.Message; }
    }
    void OnFilesChanged() {
      if(listChanged) { listChanged = false; LoadList(); }
      string current = decks.SelectedItem as string, pick = current;
      if(changed != null && follow.Checked && File.Exists(changed)) pick = Relative(changed);
      changed = null; FillDecks(pick);
      if(pick == current) Evaluate();
    }
    void Evaluate() {
      rows.BeginUpdate(); rows.Items.Clear();
      try {
        string name = decks.SelectedItem as string;
        warning.Visible = false;
        if(list == null) { total.Text="?"; total.ForeColor=Warn; breakdown.Text=""; return; }
        if(name == null) { total.Text="No decks"; total.ForeColor=Color.Silver; breakdown.Text=""; return; }
        Deck d;
        try { d = Deck.FromFile(Path.Combine(deckDir,name)); }
        catch(IOException) { debounce.Stop(); debounce.Start(); return; } // EDOPro is still writing it
        var v = Evaluation.Of(d,cards,list);
        bool over = v.total > list.cap;
        total.Text = (v.unknown > 0 ? "?" : v.total.ToString()) + " / " + list.cap;
        total.ForeColor = v.unknown > 0 ? Warn : over ? Over : Ok;
        breakdown.Text = "Main " + v.subtotal[0] + "  ·  Extra " + v.subtotal[1] + "  ·  Side " + v.subtotal[2] + (over ? "   (" + (v.total-list.cap) + " over)" : "");
        status.Text = list.name + " list from " + list.written.ToString("d MMM yyyy") + "  ·  updates when you save the deck in EDOPro";
        var notes = new List<string>();
        if(v.forbidden > 0) notes.Add(v.forbidden + (v.forbidden == 1 ? " card is" : " cards are") + " Link/Pendulum (not allowed)");
        if(v.unknown > 0) notes.Add(v.unknown + (v.unknown == 1 ? " card isn't" : " cards aren't") + " recognised");
        warning.Text = string.Join(Environment.NewLine, notes.ToArray()); warning.Visible = notes.Count > 0;
        foreach(Row row in v.notAllowed) rows.Items.Add(new ListViewItem(new[] { row.Card, row.Section, row.Copies.ToString(), "banned", "" }) { ForeColor = Over });
        foreach(string p in v.problems.Where(x => x.StartsWith("Unknown"))) rows.Items.Add(new ListViewItem(new[] { p, "", "", "", "" }) { ForeColor = Warn });
        foreach(Row row in v.rows.OrderByDescending(x=>x.Points).ThenBy(x=>x.Card)) rows.Items.Add(new ListViewItem(new[] { row.Card, row.Section, row.Copies.ToString(), row.Each.ToString(), row.Points.ToString() }));
      } finally { rows.EndUpdate(); }
    }
  }
  public static class Program {
    [STAThread] public static int Main(string[] args) {
      try {
        string home = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        string root = Path.GetFullPath(Path.Combine(home,"..",".."));
        string listFile = ListFile.Find(root);
        Cards cards = Cards.Load(root);
        if(args.Length > 1 && args[0] == "--check") {
          var v = Evaluation.Of(Deck.FromFile(args[1]),cards,PointList.Load(listFile));
          Console.WriteLine(Json.Serializer.Serialize(new { total=v.total, main=v.subtotal[0], extra=v.subtotal[1], side=v.subtotal[2], unknown=v.unknown, forbidden=v.forbidden, rows=v.rows, problems=v.problems }));
          return 0;
        }
        if(args.Length > 0 && args[0] == "--self-test") return Tests.Run(cards,PointList.Load(listFile),root);
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        bool first; using(var mutex = new System.Threading.Mutex(true,"GenesysDeckPoints",out first)) {
          if(!first) return 0;
          if(args.Contains("--start-edopro") && !Process.GetProcessesByName("EDOPro").Any()) Process.Start(new ProcessStartInfo(Path.Combine(root,"EDOPro.exe")) { WorkingDirectory=root });
          Application.Run(new MainForm(root,home,cards));
        }
        return 0;
      } catch(Exception e) { if(args.Length > 0 && args[0].StartsWith("--check") || args.Contains("--self-test")) { Console.Error.WriteLine(e); return 1; } MessageBox.Show(e.Message,"Genesys deck points",MessageBoxButtons.OK,MessageBoxIcon.Error); return 1; }
    }
  }
  public static class Tests {
    static int n;
    static void Check(bool ok, string text) { if(!ok) throw new Exception("FAILED: " + text); n++; Console.WriteLine("PASS " + text); }
    static Evaluation Of(Cards c, PointList p, uint[] main, uint[] extra = null) { return Evaluation.Of(new Deck { sections = new[] { main, extra ?? new uint[0], new uint[0] } }, c, p); }
    public static int Run(Cards cards, PointList p, string root) {
      Check(p.cap == 100 && p.points[14558127] == 18, "List loads points and the 100 cap");
      var e = Evaluation.Of(new Deck { sections = new[] { new uint[]{14558127,14558127}, new uint[]{14558127}, new uint[]{14558127} } }, cards, p);
      Check(e.total == 72 && e.subtotal[1] == 18 && e.subtotal[2] == 18, "Duplicates and Main/Extra/Side all count");
      Check(Of(cards,p,new uint[]{14558128}).total == 18, "Ash Blossom's alternate art counts 18");
      Check(Of(cards,p,new uint[]{89631139}).total == 0, "Unlisted card costs zero");
      e = Of(cards,p,new uint[]{1999999999}); Check(e.unknown == 1, "Unknown card is flagged, not counted as zero");
      var link = cards.All.Values.First(c => (c.type & 0x4000000) != 0);
      Check(Of(cards,p,new uint[0],new[]{link.code}).forbidden == 1, "Link monsters are flagged as not allowed");
      // The point cards of BEG Experiment 7: Ash, Melody, 3 Wishes, Called by the Grave, 3 Impermanence.
      e = Of(cards,p,new uint[]{14558127,48800175,80326401,80326401,80326401,24224830,10045474,10045474,10045474,89631139});
      Check(e.total == 134 && e.unknown == 0 && e.forbidden == 0, "BEG Experiment 7's point cards total 134 / 100");
      Console.WriteLine(n + " checks passed."); return 0;
    }
  }
}
