using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace RebirthPubTrainer
{
    static class AppInfo
    {
        public const string Version = "1.0.0";
        public const string GameVersion = "0.65";
        public const int Port = 26969;
        public const string GitHubUrl = "https://github.com/AlanYyh/RebirthPubTrainer";
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Installer.Ensure();
            Application.Run(new TrainerForm());
        }
    }

    static class Installer
    {
        public static void Ensure()
        {
            string root = Path.GetDirectoryName(Application.ExecutablePath);
            bool need = !File.Exists(Path.Combine(root, "winhttp.dll"))
                || !File.Exists(Path.Combine(root, "BepInEx", "core", "BepInEx.dll"))
                || !File.Exists(Path.Combine(root, "BepInEx", "plugins", "RebirthPubTrainer.dll"))
                || !File.Exists(Path.Combine(root, "Microsoft.Web.WebView2.WinForms.dll"))
                || !File.Exists(Path.Combine(root, "Microsoft.Web.WebView2.Core.dll"))
                || !File.Exists(Path.Combine(root, "WebView2Loader.dll"));
            if (!need) return;
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                foreach (string name in asm.GetManifestResourceNames())
                {
                    if (!name.StartsWith("pkg__")) continue;
                    string rel = name.Substring(5).Replace("__", "\\");
                    string target = Path.Combine(root, rel);
                    string dir = Path.GetDirectoryName(target);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    using (Stream s = asm.GetManifestResourceStream(name))
                    using (FileStream f = File.Create(target))
                        s.CopyTo(f);
                }
            }
            catch { }
        }
    }

    class GameClient
    {
        readonly object gate = new object();
        TcpClient client;
        NetworkStream stream;

        public bool Connected
        {
            get { return client != null && client.Connected; }
        }

        public bool Connect()
        {
            Disconnect();
            try
            {
                client = new TcpClient();
                IAsyncResult ar = client.BeginConnect("127.0.0.1", AppInfo.Port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(900))
                {
                    client.Close();
                    client = null;
                    return false;
                }
                client.EndConnect(ar);
                stream = client.GetStream();
                stream.ReadTimeout = 4000;
                stream.WriteTimeout = 4000;
                return true;
            }
            catch
            {
                Disconnect();
                return false;
            }
        }

        public void Disconnect()
        {
            try { if (stream != null) stream.Close(); } catch { }
            try { if (client != null) client.Close(); } catch { }
            stream = null;
            client = null;
        }

        string ReadLine()
        {
            List<byte> buf = new List<byte>();
            while (true)
            {
                int b = stream.ReadByte();
                if (b < 0) return null;
                if (b == (byte)'\n') break;
                if (b != (byte)'\r') buf.Add((byte)b);
            }
            return Encoding.UTF8.GetString(buf.ToArray());
        }

        public string Send(string command)
        {
            lock (gate)
            {
                if (!Connected && !Connect()) return null;
                try
                {
                    byte[] data = Encoding.UTF8.GetBytes(command + "\n");
                    stream.Write(data, 0, data.Length);
                    string first = ReadLine();
                    if (first == null) return null;
                    if (first.StartsWith("DATA "))
                    {
                        StringBuilder sb = new StringBuilder(first);
                        string line;
                        while ((line = ReadLine()) != null && line != "END")
                        {
                            sb.Append('\n');
                            sb.Append(line);
                        }
                        return sb.ToString();
                    }
                    return first;
                }
                catch
                {
                    Disconnect();
                    return null;
                }
            }
        }

        public string Hello()
        {
            return Send("HELLO");
        }

        public Dictionary<string, int> GetValues(string category)
        {
            Dictionary<string, int> map = new Dictionary<string, int>();
            string resp = Send("GET " + category);
            if (resp == null) return null;
            string[] lines = resp.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("DATA") || line == "END") continue;
                int idx = line.IndexOf('|');
                if (idx < 0) continue;
                string id = line.Substring(0, idx);
                int value;
                if (int.TryParse(line.Substring(idx + 1), out value))
                    map[id] = value;
            }
            return map;
        }

        public string Set(string category, string id, int value)
        {
            return Send("SET " + category + " " + id + " " + value);
        }

        public string SetAll(string target, int value)
        {
            return Send("SETALL " + target + " " + value);
        }

        public string Unlock(string what)
        {
            return Send("UNLOCK " + what);
        }
    }

    class TrainerForm : Form
    {
        WebView2 webView;
        GameClient game = new GameClient();
        JavaScriptSerializer json = new JavaScriptSerializer();

        public TrainerForm()
        {
            Text = "Rebirth Pub 修改器 v" + AppInfo.Version;
            ClientSize = new Size(1080, 700);
            MinimumSize = new Size(900, 620);
            StartPosition = FormStartPosition.CenterScreen;
            Icon = LoadIcon();

            webView = new WebView2();
            webView.Dock = DockStyle.Fill;
            Controls.Add(webView);

            Load += async (s, e) => { await InitAsync(); };
        }

        Icon LoadIcon()
        {
            try
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("ico__app.ico"))
                    return new Icon(s);
            }
            catch { return null; }
        }

        async Task InitAsync()
        {
            try
            {
                await webView.EnsureCoreWebView2Async(null);
                CoreWebView2 core = webView.CoreWebView2;
                core.Settings.AreDefaultContextMenusEnabled = false;
                core.Settings.AreDevToolsEnabled = false;
                core.WebMessageReceived += OnWebMessage;
                webView.NavigateToString(BuildHtml());
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "WebView2 初始化失败。请安装 Microsoft Edge WebView2 运行时。\n\n" + ex.Message,
                    "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        string ReadResource(string name)
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            using (StreamReader r = new StreamReader(s, Encoding.UTF8))
                return r.ReadToEnd();
        }

        byte[] ReadResourceBytes(string name)
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            using (MemoryStream m = new MemoryStream())
            {
                s.CopyTo(m);
                return m.ToArray();
            }
        }

        string DataUri(string name, string mime)
        {
            return "data:" + mime + ";base64," + Convert.ToBase64String(ReadResourceBytes(name));
        }

        string BuildHtml()
        {
            string html = ReadResource("ui__index.html");
            html = html.Replace("{{QR1}}", DataUri("qr__1.jpg", "image/jpeg"));
            html = html.Replace("{{QR2}}", DataUri("qr__2.jpg", "image/jpeg"));
            return html;
        }

        void OnWebMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string cmd = e.WebMessageAsJson;
            ThreadPool.QueueUserWorkItem(delegate(object _)
            {
                string resp = HandleCommand(cmd);
                if (resp != null)
                {
                    try
                    {
                        BeginInvoke(new Action(() =>
                        {
                            try { webView.CoreWebView2.PostWebMessageAsJson(resp); }
                            catch { }
                        }));
                    }
                    catch { }
                }
            });
        }

        string HandleCommand(string cmdJson)
        {
            try
            {
                Dictionary<string, object> o = (Dictionary<string, object>)json.DeserializeObject(cmdJson);
                string type = Str(o, "type");
                if (type == "hello") return HelloJson();
                if (type == "get") return GetJson(Str(o, "category"));
                if (type == "set") return SetJson(Str(o, "category"), Str(o, "id"), Int(o, "value"));
                if (type == "setall") return SetAllJson(Str(o, "target"), Int(o, "value"));
                if (type == "unlock") return UnlockJson(Str(o, "what"));
                if (type == "open")
                {
                    try { System.Diagnostics.Process.Start(Str(o, "url")); }
                    catch { }
                    return null;
                }
                return null;
            }
            catch { return StatusJson(false, "内部错误"); }
        }

        static string Str(Dictionary<string, object> o, string k)
        {
            object v;
            return o.TryGetValue(k, out v) && v != null ? v.ToString() : "";
        }

        static int Int(Dictionary<string, object> o, string k)
        {
            object v;
            return o.TryGetValue(k, out v) ? Convert.ToInt32(v) : 0;
        }

        string J(object o)
        {
            return json.Serialize(o);
        }

        string HelloJson()
        {
            string r = game.Hello();
            if (r == null) return J(new Dictionary<string, object> { { "type", "hello" }, { "state", "offline" } });
            string[] parts = r.Split('|');
            string state = parts.Length > 1 ? parts[1] : "notready";
            return J(new Dictionary<string, object> { { "type", "hello" }, { "state", state } });
        }

        string GetJson(string category)
        {
            Dictionary<string, int> map = game.GetValues(category);
            if (map == null) return null;
            return J(new Dictionary<string, object> { { "type", "values" }, { "category", category }, { "data", map } });
        }

        string SetJson(string category, string id, int value)
        {
            string r = game.Set(category, id, value);
            if (r == null) return StatusJson(false, "未连接游戏");
            if (r.StartsWith("OK")) return StatusJson(true, "已成功");
            return StatusJson(false, "修改失败: " + r);
        }

        string SetAllJson(string target, int value)
        {
            string r = game.SetAll(target, value);
            if (r == null) return StatusJson(false, "未连接游戏");
            if (r.StartsWith("OK")) return StatusJson(true, "已成功");
            return StatusJson(false, r);
        }

        string UnlockJson(string what)
        {
            string r = game.Unlock(what);
            if (r == null) return StatusJson(false, "未连接游戏");
            if (r.StartsWith("OK")) return StatusJson(true, "已成功");
            return StatusJson(false, r);
        }

        string StatusJson(bool ok, string text)
        {
            return J(new Dictionary<string, object> { { "type", "status" }, { "ok", ok }, { "text", text } });
        }
    }
}
