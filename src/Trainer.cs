using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace RebirthPubTrainer
{
    static class AppInfo
    {
        public const string Version = "1.0.0";
        public const string GameVersion = "0.65";
        public const int Port = 26969;
        public const string GitHubUrl = "https://github.com/AlanYyh/RebirthPubTrainer";
    }

    class TrainItem
    {
        public string Category;
        public string Id;
        public string Display;
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrainerForm());
        }
    }

    class GameClient
    {
        TcpClient client;
        NetworkStream stream;

        public bool Connected
        {
            get
            {
                return client != null && client.Connected;
            }
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

        public string Hello()
        {
            string resp = Send("HELLO");
            return resp;
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
        GameClient game = new GameClient();
        Panel sidebar;
        Panel content;
        StatusStrip statusBar;
        ToolStripStatusLabel statusLabel;
        System.Windows.Forms.Timer refreshTimer;

        string currentPage = "currency";
        Dictionary<string, TrainItem> currentItems;

        static readonly TrainItem[] Currency = {
            new TrainItem { Category = "currency", Id = "Gold", Display = "金币 Gold" },
            new TrainItem { Category = "currency", Id = "SoulJam", Display = "魂石 SoulJam" },
            new TrainItem { Category = "currency", Id = "AP", Display = "行动点 AP" },
            new TrainItem { Category = "currency", Id = "Stamina", Display = "体力 Stamina" },
            new TrainItem { Category = "currency", Id = "SkillPoint", Display = "技能点 SkillPoint" },
        };

        static readonly TrainItem[] Heroine = {
            new TrainItem { Category = "heroine", Id = "Nicole", Display = "妮可 Nicole" },
            new TrainItem { Category = "heroine", Id = "Irene", Display = "艾琳 Irene" },
            new TrainItem { Category = "heroine", Id = "Serena", Display = "瑟蕾娜 Serena" },
            new TrainItem { Category = "npc", Id = "Market_Merchant", Display = "市场商人 Market Merchant" },
            new TrainItem { Category = "npc", Id = "BlackMarket_Merchant", Display = "黑市商人 Black Market Merchant" },
            new TrainItem { Category = "npc", Id = "Clown", Display = "小丑 Clown" },
            new TrainItem { Category = "npc", Id = "Priest", Display = "神父 Priest" },
            new TrainItem { Category = "npc", Id = "Elena", Display = "艾莲娜 Elena" },
            new TrainItem { Category = "npc", Id = "Marie", Display = "玛丽 Marie" },
            new TrainItem { Category = "npc", Id = "Logan", Display = "罗根 Logan" },
            new TrainItem { Category = "npc", Id = "Nix", Display = "尼克斯 Nix" },
            new TrainItem { Category = "npc", Id = "Bob", Display = "鲍勃 Bob" },
            new TrainItem { Category = "npc", Id = "Runa", Display = "露娜 Runa" },
            new TrainItem { Category = "npc", Id = "Teddy", Display = "泰迪 Teddy" },
            new TrainItem { Category = "npc", Id = "Jack", Display = "杰克 Jack" },
            new TrainItem { Category = "npc", Id = "Charlotte", Display = "夏洛特 Charlotte" },
        };

        static readonly TrainItem[] Items = {
            new TrainItem { Category = "item", Id = "NormalRecipePiece", Display = "普通食谱碎片" },
            new TrainItem { Category = "item", Id = "RareRecipePiece", Display = "稀有食谱碎片" },
            new TrainItem { Category = "item", Id = "LegendaryRecipePiece", Display = "传说食谱碎片" },
            new TrainItem { Category = "item", Id = "NormalCostumePiece", Display = "普通服装碎片" },
            new TrainItem { Category = "item", Id = "RareCostumePiece", Display = "稀有服装碎片" },
            new TrainItem { Category = "item", Id = "LegendaryCostumePiece", Display = "传说服装碎片" },
            new TrainItem { Category = "item", Id = "NormalRelicPiece", Display = "普通遗物碎片" },
            new TrainItem { Category = "item", Id = "RareRelicPiece", Display = "稀有遗物碎片" },
            new TrainItem { Category = "item", Id = "LegendaryRelicPiece", Display = "传说遗物碎片" },
            new TrainItem { Category = "item", Id = "Ruby", Display = "红宝石" },
            new TrainItem { Category = "item", Id = "Sapphire", Display = "蓝宝石" },
            new TrainItem { Category = "item", Id = "Emerald", Display = "绿宝石" },
            new TrainItem { Category = "item", Id = "Topaz", Display = "黄玉" },
            new TrainItem { Category = "item", Id = "NormalRecipe", Display = "普通食谱" },
            new TrainItem { Category = "item", Id = "RareRecipe", Display = "稀有食谱" },
            new TrainItem { Category = "item", Id = "LegendaryRecipe", Display = "传说食谱" },
            new TrainItem { Category = "item", Id = "Chocolate", Display = "巧克力" },
            new TrainItem { Category = "item", Id = "EnergyBar", Display = "能量棒" },
            new TrainItem { Category = "item", Id = "EnergyDrink", Display = "能量饮料" },
            new TrainItem { Category = "item", Id = "LegendaryMushroom", Display = "传说蘑菇" },
            new TrainItem { Category = "item", Id = "HolyWater", Display = "圣水" },
            new TrainItem { Category = "item", Id = "CookieSet", Display = "曲奇套装" },
            new TrainItem { Category = "item", Id = "StrawberryCake", Display = "草莓蛋糕" },
            new TrainItem { Category = "item", Id = "Bouquet", Display = "花束" },
            new TrainItem { Category = "item", Id = "PoetryBook", Display = "诗集" },
            new TrainItem { Category = "item", Id = "LuxuryPerfume", Display = "高级香水" },
            new TrainItem { Category = "item", Id = "StuffedDoll", Display = "玩偶" },
            new TrainItem { Category = "item", Id = "LuxuryCushion", Display = "高级靠垫" },
            new TrainItem { Category = "item", Id = "SnowGlobe", Display = "雪花球" },
            new TrainItem { Category = "item", Id = "SilkScarf", Display = "丝巾" },
            new TrainItem { Category = "item", Id = "VintageWine", Display = "陈年红酒" },
            new TrainItem { Category = "item", Id = "JewelMusicBox", Display = "宝石音乐盒" },
            new TrainItem { Category = "item", Id = "LovePotion", Display = "爱情药水" },
            new TrainItem { Category = "item", Id = "RedPetal", Display = "红色花瓣" },
            new TrainItem { Category = "item", Id = "BluePetal", Display = "蓝色花瓣" },
            new TrainItem { Category = "item", Id = "SkillBook", Display = "技能书" },
            new TrainItem { Category = "item", Id = "AdvSkillBook", Display = "高级技能书" },
            new TrainItem { Category = "item", Id = "MagicalTeaLeaf", Display = "魔法茶叶" },
            new TrainItem { Category = "item", Id = "SacredTeaLeaf", Display = "神圣茶叶" },
            new TrainItem { Category = "item", Id = "MysteryBox", Display = "神秘盒子" },
            new TrainItem { Category = "item", Id = "AdvMysteryBox", Display = "高级神秘盒子" },
            new TrainItem { Category = "item", Id = "SecretMapA", Display = "秘密地图A" },
            new TrainItem { Category = "item", Id = "SecretMapB", Display = "秘密地图B" },
            new TrainItem { Category = "item", Id = "SecretMapC", Display = "秘密地图C" },
            new TrainItem { Category = "item", Id = "SecretMapD", Display = "秘密地图D" },
        };

        static readonly TrainItem[] Skills = {
            new TrainItem { Category = "skill", Id = "SkillPoint", Display = "技能点 SkillPoint" },
            new TrainItem { Category = "skill", Id = "PowerUp_01", Display = "PowerUp_01" },
            new TrainItem { Category = "skill", Id = "AvoidUp_01", Display = "AvoidUp_01" },
            new TrainItem { Category = "skill", Id = "PowerUp_02", Display = "PowerUp_02" },
            new TrainItem { Category = "skill", Id = "Turret_01", Display = "Turret_01" },
            new TrainItem { Category = "skill", Id = "LuckUp_01", Display = "LuckUp_01" },
            new TrainItem { Category = "skill", Id = "TurretPowerUp_01", Display = "TurretPowerUp_01" },
            new TrainItem { Category = "skill", Id = "MoreTurret_01", Display = "MoreTurret_01" },
            new TrainItem { Category = "skill", Id = "LuckUp_02", Display = "LuckUp_02" },
            new TrainItem { Category = "skill", Id = "MoveSpeedUp_01", Display = "MoveSpeedUp_01" },
            new TrainItem { Category = "skill", Id = "Protector_01", Display = "Protector_01" },
            new TrainItem { Category = "skill", Id = "AvoidUp_02", Display = "AvoidUp_02" },
            new TrainItem { Category = "skill", Id = "ProtectorUpgrade_01", Display = "ProtectorUpgrade_01" },
            new TrainItem { Category = "skill", Id = "Rebirth_01", Display = "Rebirth_01" },
            new TrainItem { Category = "skill", Id = "AttackSpeedUp_01", Display = "AttackSpeedUp_01" },
            new TrainItem { Category = "skill", Id = "Magnetic_01", Display = "Magnetic_01" },
            new TrainItem { Category = "skill", Id = "IceFairy_01", Display = "IceFairy_01" },
            new TrainItem { Category = "skill", Id = "MaxHP_01", Display = "MaxHP_01" },
            new TrainItem { Category = "skill", Id = "IceFairySlowUpgrade_01", Display = "IceFairySlowUpgrade_01" },
            new TrainItem { Category = "skill", Id = "MoreIceFairy_01", Display = "MoreIceFairy_01" },
            new TrainItem { Category = "skill", Id = "MaxHP_02", Display = "MaxHP_02" },
            new TrainItem { Category = "skill", Id = "BetterPotion", Display = "BetterPotion" },
            new TrainItem { Category = "skill", Id = "ChainAttack_01", Display = "ChainAttack_01" },
            new TrainItem { Category = "skill", Id = "AttackSpeedUp_02", Display = "AttackSpeedUp_02" },
            new TrainItem { Category = "skill", Id = "ChainAttackUpgrade_01", Display = "ChainAttackUpgrade_01" },
            new TrainItem { Category = "skill", Id = "ImmediateDeath", Display = "ImmediateDeath" },
        };

        public TrainerForm()
        {
            Text = "Rebirth Pub 修改器 v" + AppInfo.Version;
            ClientSize = new Size(760, 520);
            MinimumSize = new Size(640, 420);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;

            EnsureInstalled();

            BuildSidebar();
            BuildStatusBar();
            BuildContent();

            refreshTimer = new System.Windows.Forms.Timer();
            refreshTimer.Interval = 2000;
            refreshTimer.Tick += (s, e) => RefreshValues();
            refreshTimer.Start();

            Load += (s, e) => TryConnect();
        }

        void BuildSidebar()
        {
            sidebar = new Panel();
            sidebar.Dock = DockStyle.Left;
            sidebar.Width = 110;
            sidebar.BackColor = Color.FromArgb(40, 44, 52);
            Controls.Add(sidebar);

            string[] pages = { "currency|货币", "heroine|角色", "item|道具", "skill|技能", "gallery|图鉴", "about|关于" };
            int y = 8;
            foreach (string page in pages)
            {
                string[] kv = page.Split('|');
                Button btn = new Button();
                btn.Text = kv[1];
                btn.Tag = kv[0];
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderSize = 0;
                btn.BackColor = Color.FromArgb(40, 44, 52);
                btn.ForeColor = Color.White;
                btn.Height = 40;
                btn.Width = 98;
                btn.Left = 6;
                btn.Top = y;
                btn.Click += PageClick;
                sidebar.Controls.Add(btn);
                y += 46;
            }
        }

        void PageClick(object sender, EventArgs e)
        {
            Button btn = (Button)sender;
            currentPage = (string)btn.Tag;
            foreach (Control c in sidebar.Controls)
            {
                if (c is Button)
                {
                    Button b = (Button)c;
                    b.BackColor = (b == btn) ? Color.FromArgb(70, 130, 200) : Color.FromArgb(40, 44, 52);
                }
            }
            BuildContent();
            RefreshValues();
        }

        void BuildStatusBar()
        {
            statusBar = new StatusStrip();
            statusLabel = new ToolStripStatusLabel("就绪");
            statusBar.Items.Add(statusLabel);
            Controls.Add(statusBar);
        }

        void SetStatus(string text)
        {
            if (statusLabel != null) statusLabel.Text = text;
        }

        void BuildContent()
        {
            if (content == null)
            {
                content = new Panel();
                content.Dock = DockStyle.Fill;
                Controls.Add(content);
                content.BringToFront();
            }
            content.Controls.Clear();

            if (currentPage == "gallery")
            {
                BuildGalleryPage();
                return;
            }
            if (currentPage == "about")
            {
                BuildAboutPage();
                return;
            }

            TrainItem[] items = PageItems(currentPage);
            currentItems = new Dictionary<string, TrainItem>();
            foreach (TrainItem it in items) currentItems[it.Id] = it;

            Label hint = new Label();
            hint.Text = "双击列表项进行修改";
            hint.Dock = DockStyle.Top;
            hint.Height = 26;
            hint.Padding = new Padding(6, 4, 0, 0);
            content.Controls.Add(hint);

            ListView list = new ListView();
            list.Dock = DockStyle.Fill;
            list.View = View.Details;
            list.FullRowSelect = true;
            list.Columns.Add("名称", 320);
            list.Columns.Add("数值", 120);
            list.DoubleClick += ItemDoubleClick;
            foreach (TrainItem it in items)
            {
                ListViewItem li = new ListViewItem(it.Display);
                li.SubItems.Add("0");
                li.Tag = it;
                list.Items.Add(li);
            }
            content.Controls.Add(list);
            list.BringToFront();

            Button modBtn = new Button();
            modBtn.Text = "修改选中项";
            modBtn.Dock = DockStyle.Bottom;
            modBtn.Height = 34;
            modBtn.Click += (s, e) => ModifySelected(list);
            content.Controls.Add(modBtn);

            Button refreshBtn = new Button();
            refreshBtn.Text = "刷新";
            refreshBtn.Dock = DockStyle.Bottom;
            refreshBtn.Height = 34;
            refreshBtn.Click += (s, e) => RefreshValues();
            content.Controls.Add(refreshBtn);
        }

        TrainItem[] PageItems(string page)
        {
            if (page == "currency") return Currency;
            if (page == "heroine") return Heroine;
            if (page == "item") return Items;
            if (page == "skill") return Skills;
            return new TrainItem[0];
        }

        void BuildGalleryPage()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.Padding = new Padding(16);

            Label title = new Label();
            title.Text = "图鉴 / 收集";
            title.Font = new Font(Font.FontFamily, 14, FontStyle.Bold);
            title.Location = new Point(0, 0);
            title.AutoSize = true;
            p.Controls.Add(title);

            int y = 48;
            y = AddGalleryButton(p, y, "一键解锁全部图鉴", "gallery");
            y = AddGalleryButton(p, y, "解锁全部服装", "costume");
            y = AddGalleryButton(p, y, "解锁全部遗物", "relic");

            Label tip = new Label();
            tip.Text = "提示：解锁后进入对应界面即可看到效果。";
            tip.Location = new Point(0, y + 8);
            tip.AutoSize = true;
            p.Controls.Add(tip);

            content.Controls.Add(p);
        }

        int AddGalleryButton(Panel p, int y, string text, string what)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(0, y);
            b.Size = new Size(220, 38);
            b.Click += (s, e) => DoUnlock(what);
            p.Controls.Add(b);
            return y + 46;
        }

        void DoUnlock(string what)
        {
            if (!game.Connected && !game.Connect())
            {
                SetStatus("未连接游戏，请先启动游戏");
                return;
            }
            string resp = game.Unlock(what);
            if (resp != null && resp.StartsWith("OK"))
                SetStatus("已成功");
            else
                SetStatus("操作失败: " + resp);
        }

        void BuildAboutPage()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.AutoScroll = true;
            p.Padding = new Padding(16);

            int y = 8;

            Label title = new Label();
            title.Text = "Rebirth Pub 修改器";
            title.Font = new Font(Font.FontFamily, 16, FontStyle.Bold);
            title.Location = new Point(0, y);
            title.AutoSize = true;
            p.Controls.Add(title);
            y += 34;

            Label ver = new Label();
            ver.Text = "版本 v" + AppInfo.Version + "   对应游戏版本 " + AppInfo.GameVersion;
            ver.Location = new Point(0, y);
            ver.AutoSize = true;
            p.Controls.Add(ver);
            y += 26;

            Label free = new Label();
            free.Text = "本修改器完全免费且开源";
            free.Location = new Point(0, y);
            free.AutoSize = true;
            p.Controls.Add(free);
            y += 26;

            LinkLabel link = new LinkLabel();
            link.Text = AppInfo.GitHubUrl;
            link.Location = new Point(0, y);
            link.AutoSize = true;
            link.LinkClicked += (s, e) => OpenUrl(AppInfo.GitHubUrl);
            p.Controls.Add(link);
            y += 28;

            Label vpn = new Label();
            vpn.Text = "（该项目地址在海外，需要梯子访问）";
            vpn.ForeColor = Color.Gray;
            vpn.Location = new Point(0, y);
            vpn.AutoSize = true;
            p.Controls.Add(vpn);
            y += 26;

            Label warn = new Label();
            warn.Text = "如果你是收费获取，投诉你的卖家。";
            warn.Location = new Point(0, y);
            warn.AutoSize = true;
            p.Controls.Add(warn);
            y += 26;

            Label issues = new Label();
            issues.Text = "如果修改器有问题，请向我提交 issues。";
            issues.Location = new Point(0, y);
            issues.AutoSize = true;
            p.Controls.Add(issues);
            y += 26;

            Label qrTitle = new Label();
            qrTitle.Text = "赞赏支持";
            qrTitle.Font = new Font(Font.FontFamily, 12, FontStyle.Bold);
            qrTitle.Location = new Point(0, y);
            qrTitle.AutoSize = true;
            p.Controls.Add(qrTitle);
            y += 30;

            PictureBox qr1 = new PictureBox();
            qr1.Image = LoadQrImage("qr__1.jpg");
            qr1.SizeMode = PictureBoxSizeMode.Zoom;
            qr1.Location = new Point(0, y);
            qr1.Size = new Size(180, 180);
            p.Controls.Add(qr1);

            PictureBox qr2 = new PictureBox();
            qr2.Image = LoadQrImage("qr__2.jpg");
            qr2.SizeMode = PictureBoxSizeMode.Zoom;
            qr2.Location = new Point(190, y);
            qr2.Size = new Size(180, 180);
            p.Controls.Add(qr2);

            content.Controls.Add(p);
        }

        Image LoadQrImage(string name)
        {
            try
            {
                Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
                if (s == null) return null;
                using (s) return Image.FromStream(s);
            }
            catch { return null; }
        }

        void OpenUrl(string url)
        {
            try { System.Diagnostics.Process.Start(url); }
            catch { }
        }

        void ItemDoubleClick(object sender, EventArgs e)
        {
            ListView list = (ListView)sender;
            ModifySelected(list);
        }

        void ModifySelected(ListView list)
        {
            if (list.SelectedItems.Count == 0)
            {
                SetStatus("请先选中要修改的项");
                return;
            }
            ListViewItem li = list.SelectedItems[0];
            TrainItem item = (TrainItem)li.Tag;
            int current;
            if (!int.TryParse(li.SubItems[1].Text, out current)) current = 0;
            InputDialog dlg = new InputDialog(item.Display, current);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                DoSet(item, dlg.Value);
            }
        }

        void DoSet(TrainItem item, int value)
        {
            if (!game.Connected && !game.Connect())
            {
                SetStatus("未连接游戏，请先启动游戏");
                return;
            }
            string resp = game.Set(item.Category, item.Id, value);
            if (resp != null && resp.StartsWith("OK"))
                SetStatus("已成功");
            else
                SetStatus("修改失败: " + resp);
            RefreshValues();
        }

        void TryConnect()
        {
            Thread t = new Thread(() =>
            {
                bool ok = game.Connect();
                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (ok)
                        {
                            SetStatus("已连接游戏");
                            RefreshValues();
                        }
                        else
                        {
                            SetStatus("未连接游戏，请先启动游戏");
                        }
                    }));
                }
                catch { }
            });
            t.IsBackground = true;
            t.Start();
        }

        void RefreshValues()
        {
            if (!game.Connected) return;
            if (currentPage != "currency" && currentPage != "heroine" && currentPage != "item" && currentPage != "skill")
                return;
            TrainItem[] items = PageItems(currentPage);
            Dictionary<string, int> values = new Dictionary<string, int>();
            HashSet<string> categories = new HashSet<string>();
            foreach (TrainItem it in items) categories.Add(it.Category);
            foreach (string cat in categories)
            {
                Dictionary<string, int> part = game.GetValues(cat);
                if (part == null)
                {
                    SetStatus("未连接游戏，请先启动游戏");
                    return;
                }
                foreach (KeyValuePair<string, int> kv in part) values[kv.Key] = kv.Value;
            }
            ListView list = FindListView();
            if (list == null) return;
            foreach (ListViewItem li in list.Items)
            {
                TrainItem it = (TrainItem)li.Tag;
                int v;
                if (values.TryGetValue(it.Id, out v))
                    li.SubItems[1].Text = v.ToString();
            }
        }

        ListView FindListView()
        {
            foreach (Control c in content.Controls)
            {
                if (c is ListView) return (ListView)c;
            }
            return null;
        }

        void EnsureInstalled()
        {
            string root = Path.GetDirectoryName(Application.ExecutablePath);
            bool frameworkOk = File.Exists(Path.Combine(root, "winhttp.dll"))
                && File.Exists(Path.Combine(root, "BepInEx", "core", "BepInEx.dll"));
            bool pluginOk = File.Exists(Path.Combine(root, "BepInEx", "plugins", "RebirthPubTrainer.dll"));
            if (frameworkOk && pluginOk) return;

            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                foreach (string name in asm.GetManifestResourceNames())
                {
                    if (!name.StartsWith("pkg__")) continue;
                    string rel = name.Substring("pkg__".Length).Replace("__", "\\");
                    string target = Path.Combine(root, rel);
                    string dir = Path.GetDirectoryName(target);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    using (Stream s = asm.GetManifestResourceStream(name))
                    using (FileStream f = File.Create(target))
                    {
                        s.CopyTo(f);
                    }
                }
                SetStatus("已安装注入框架，请启动游戏");
            }
            catch (Exception e)
            {
                SetStatus("安装失败: " + e.Message);
            }
        }
    }

    class InputDialog : Form
    {
        public int Value;
        TextBox input;

        public InputDialog(string name, int current)
        {
            Text = "修改 " + name;
            ClientSize = new Size(300, 140);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            Label label = new Label();
            label.Text = "输入新值（当前：" + current + "）";
            label.Location = new Point(12, 16);
            label.AutoSize = true;
            Controls.Add(label);

            input = new TextBox();
            input.Text = current.ToString();
            input.Location = new Point(12, 42);
            input.Size = new Size(260, 24);
            input.SelectAll();
            Controls.Add(input);

            Button ok = new Button();
            ok.Text = "确定";
            ok.DialogResult = DialogResult.OK;
            ok.Location = new Point(100, 84);
            ok.Size = new Size(80, 30);
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = "取消";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(190, 84);
            cancel.Size = new Size(80, 30);
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK)
            {
                if (!int.TryParse(input.Text, out Value))
                {
                    e.Cancel = true;
                    MessageBox.Show(this, "请输入有效整数", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            base.OnFormClosing(e);
        }
    }
}
