using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using BepInEx;
using UnityEngine;
using RebirthPub;
using RebirthPub.PlayScene;
using RebirthPub.PlayScene.SkillTree;

namespace RebirthPubTrainer
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    [BepInProcess("Rebirth Pub.exe")]
    public class TrainerPlugin : BaseUnityPlugin
    {
        const int Port = 26969;

        TcpListener listener;
        Thread serverThread;
        volatile bool running;

        readonly object sync = new object();
        readonly Queue<string> inQueue = new Queue<string>();
        readonly Queue<string> outQueue = new Queue<string>();
        readonly AutoResetEvent inSignal = new AutoResetEvent(false);
        readonly AutoResetEvent outSignal = new AutoResetEvent(false);

        void Awake()
        {
            try { StartServer(); }
            catch (Exception e) { Logger.LogError(e.ToString()); }
        }

        void StartServer()
        {
            running = true;
            listener = new TcpListener(IPAddress.Loopback, Port);
            listener.Start();
            serverThread = new Thread(ServerLoop);
            serverThread.IsBackground = true;
            serverThread.Start();
        }

        void OnDestroy()
        {
            running = false;
            try { listener.Stop(); } catch { }
        }

        void ServerLoop()
        {
            while (running)
            {
                TcpClient client;
                try { client = listener.AcceptTcpClient(); }
                catch { break; }
                Thread t = new Thread(() => Handle(client));
                t.IsBackground = true;
                t.Start();
            }
        }

        void Handle(TcpClient client)
        {
            try
            {
                using (client)
                using (Stream stream = client.GetStream())
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.NewLine = "\n";
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        writer.WriteLine(Dispatch(line));
                        writer.Flush();
                    }
                }
            }
            catch { }
        }

        string Dispatch(string command)
        {
            lock (sync) inQueue.Enqueue(command);
            inSignal.Set();
            if (!outSignal.WaitOne(8000)) return "ERR timeout";
            lock (sync) return outQueue.Dequeue();
        }

        void Update()
        {
            while (inSignal.WaitOne(0))
            {
                string command;
                lock (sync) command = inQueue.Dequeue();
                string response = Execute(command);
                lock (sync) outQueue.Enqueue(response);
                outSignal.Set();
            }
        }

        string Execute(string raw)
        {
            try
            {
                string[] parts = raw.Split(' ');
                string op = parts[0].ToUpperInvariant();
                if (op == "HELLO") return Hello();
                if (op == "GET") return Get(parts.Length > 1 ? parts[1] : "");
                if (op == "SET") return Set(parts);
                if (op == "SETALL") return SetAll(parts);
                if (op == "UNLOCK") return Unlock(parts.Length > 1 ? parts[1] : "");
                return "ERR bad command";
            }
            catch (Exception e) { return "ERR " + e.Message; }
        }

        string Hello()
        {
            return "HELLO OK " + PluginInfo.Version + "|" + (Ready() ? "ready" : "notready");
        }

        bool Ready()
        {
            try
            {
                DataRepositoryManager mgr = Manager();
                if (mgr == null) return false;
                return mgr.GetCostData(CostID.Gold) != null;
            }
            catch { return false; }
        }

        DataRepositoryManager Manager()
        {
            try { return DataRepositoryManager.Instance; }
            catch { return null; }
        }

        string Get(string category)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("DATA " + category);
            if (category == "currency")
            {
                sb.AppendLine("Gold|" + GetCost(CostID.Gold));
                sb.AppendLine("SoulJam|" + GetCost(CostID.SoulJam));
                sb.AppendLine("AP|" + GetCost(CostID.AP));
                sb.AppendLine("Stamina|" + GetCost(CostID.Stamina));
                sb.AppendLine("SkillPoint|" + GetCost(CostID.SkillPoint));
            }
            else if (category == "heroine")
            {
                foreach (HeroineID id in Heroines)
                    sb.AppendLine(id.ToString() + "|" + GetHeroineFavor(id));
            }
            else if (category == "npc")
            {
                foreach (NpcID id in Npcs)
                    sb.AppendLine(id.ToString() + "|" + GetNpcFavor(id));
            }
            else if (category == "item")
            {
                foreach (ItemID id in Enum.GetValues(typeof(ItemID)))
                    if ((int)id > 0)
                        sb.AppendLine(id.ToString() + "|" + GetItem(id));
            }
            else if (category == "skill")
            {
                sb.AppendLine("SkillPoint|" + GetCost(CostID.SkillPoint));
                foreach (SkillID id in Enum.GetValues(typeof(SkillID)))
                    if ((int)id > 0)
                        sb.AppendLine(id.ToString() + "|" + GetSkillLevel(id));
            }
            else
            {
                return "ERR unknown category";
            }
            sb.Append("END");
            return sb.ToString();
        }

        string Set(string[] parts)
        {
            if (parts.Length < 4) return "ERR usage";
            string category = parts[1];
            string id = parts[2];
            int value;
            if (!int.TryParse(parts[3], out value)) return "ERR bad value";
            if (category == "currency") return SetCurrency(id, value);
            if (category == "heroine") return SetHeroine(id, value);
            if (category == "npc") return SetNpc(id, value);
            if (category == "item") return SetItemId(id, value);
            if (category == "skill") return SetSkill(id, value);
            return "ERR unknown category";
        }

        string SetAll(string[] parts)
        {
            if (parts.Length < 3) return "ERR usage";
            string target = parts[1];
            int value;
            if (!int.TryParse(parts[2], out value)) return "ERR bad value";
            if (target == "item")
            {
                foreach (ItemID id in Enum.GetValues(typeof(ItemID)))
                    if ((int)id > 0) SetItem(id, value);
                return "OK";
            }
            if (target == "npc")
            {
                foreach (NpcID id in Npcs) SetNpcFavor(id, value);
                return "OK";
            }
            if (target == "heroine")
            {
                foreach (HeroineID id in Heroines) SetHeroineFavor(id, value);
                return "OK";
            }
            return "ERR unknown target";
        }

        string Unlock(string what)
        {
            if (what == "gallery")
            {
                DataRepositoryManager mgr = Manager();
                if (mgr == null) return "ERR not ready";
                mgr.UnlockAllGallery();
                return "OK";
            }
            if (what == "costume")
            {
                DataRepositoryManager mgr = Manager();
                if (mgr == null) return "ERR not ready";
                foreach (CostumeID id in Enum.GetValues(typeof(CostumeID)))
                {
                    if ((int)id <= 0) continue;
                    var dto = mgr.GetCostumeData(id);
                    if (dto == null) continue;
                    dto.IsUnlock = true;
                    mgr.UpdateCostumeData(dto);
                }
                return "OK";
            }
            if (what == "relic")
            {
                DataRepositoryManager mgr = Manager();
                if (mgr == null) return "ERR not ready";
                foreach (RelicID id in Enum.GetValues(typeof(RelicID)))
                {
                    if ((int)id <= 0) continue;
                    var dto = mgr.GetRelicData(id);
                    if (dto == null) continue;
                    dto.IsUnlocked = true;
                    mgr.UpdateRelicData(dto);
                }
                return "OK";
            }
            return "ERR unknown unlock";
        }

        string SetCurrency(string id, int value)
        {
            CostID cid = (CostID)Enum.Parse(typeof(CostID), id);
            SetCost(cid, value);
            return "OK " + GetCost(cid);
        }

        string SetHeroine(string id, int value)
        {
            HeroineID hid = (HeroineID)Enum.Parse(typeof(HeroineID), id);
            SetHeroineFavor(hid, value);
            return "OK " + GetHeroineFavor(hid);
        }

        string SetNpc(string id, int value)
        {
            NpcID nid = (NpcID)Enum.Parse(typeof(NpcID), id);
            SetNpcFavor(nid, value);
            return "OK " + GetNpcFavor(nid);
        }

        string SetItemId(string id, int value)
        {
            ItemID iid = (ItemID)Enum.Parse(typeof(ItemID), id);
            SetItem(iid, value);
            return "OK " + GetItem(iid);
        }

        string SetSkill(string id, int value)
        {
            if (id == "SkillPoint")
            {
                SetCost(CostID.SkillPoint, value);
                return "OK " + value;
            }
            SkillID sid = (SkillID)Enum.Parse(typeof(SkillID), id);
            SetSkillLevel(sid, value);
            return "OK " + value;
        }

        static readonly HeroineID[] Heroines = { HeroineID.Nicole, HeroineID.Irene, HeroineID.Serena };
        static readonly NpcID[] Npcs =
        {
            NpcID.Market_Merchant, NpcID.BlackMarket_Merchant, NpcID.Clown, NpcID.Priest, NpcID.Elena,
            NpcID.Marie, NpcID.Logan, NpcID.Nix, NpcID.Bob, NpcID.Runa, NpcID.Teddy, NpcID.Jack, NpcID.Charlotte
        };

        void SetCost(CostID id, int value)
        {
            try
            {
                DataRepositoryManager mgr = Manager();
                if (mgr == null) return;
                var dto = mgr.GetCostData(id);
                if (dto == null) return;
                int prev = dto.HaveAmount;
                dto.HaveAmount = value;
                mgr.UpdateCostData(dto);
                EventBus<CostAmountChangeEvent>.Notify(new CostAmountChangeEvent(id, prev, value), "", "", 0);
            }
            catch { }
        }

        int GetCost(CostID id)
        {
            try
            {
                DataRepositoryManager mgr = Manager();
                if (mgr == null) return 0;
                var dto = mgr.GetCostData(id);
                return dto == null ? 0 : dto.HaveAmount;
            }
            catch { return 0; }
        }

        void SetItem(ItemID id, int value)
        {
            try
            {
                DataRepositoryManager mgr = Manager();
                if (mgr == null) return;
                var dto = mgr.GetItemData(id);
                if (dto == null) return;
                dto.HaveAmount = value;
                mgr.UpdateItemData(dto);
                EventBus<ItemAmountChangeEvent>.Notify(new ItemAmountChangeEvent(id, value), "", "", 0);
            }
            catch { }
        }

        int GetItem(ItemID id)
        {
            try
            {
                DataRepositoryManager mgr = Manager();
                if (mgr == null) return 0;
                var dto = mgr.GetItemData(id);
                return dto == null ? 0 : dto.HaveAmount;
            }
            catch { return 0; }
        }

        void SetHeroineFavor(HeroineID id, int value)
        {
            try
            {
                DataRepositoryManager mgr = Manager();
                if (mgr == null) return;
                var dto = mgr.GetHeroineData(id);
                if (dto == null) return;
                int prev = dto.Favor;
                dto.Favor = value;
                mgr.UpdateHeroineData(dto);
                EventBus<HeroineFavorChangeEvent>.Notify(new HeroineFavorChangeEvent(id, prev, value), "", "", 0);
            }
            catch { }
        }

        int GetHeroineFavor(HeroineID id)
        {
            try
            {
                DataRepositoryManager mgr = Manager();
                if (mgr == null) return 0;
                var dto = mgr.GetHeroineData(id);
                return dto == null ? 0 : dto.Favor;
            }
            catch { return 0; }
        }

        void SetNpcFavor(NpcID id, int value)
        {
            try
            {
                DataRepositoryManager mgr = Manager();
                if (mgr == null) return;
                var dto = mgr.GetNpcData(id);
                if (dto == null) return;
                int prev = dto.Favor;
                dto.Favor = value;
                mgr.UpdateNpcData(dto);
                EventBus<NpcFavorChangeEvent>.Notify(new NpcFavorChangeEvent(id, prev, value), "", "", 0);
            }
            catch { }
        }

        int GetNpcFavor(NpcID id)
        {
            try
            {
                DataRepositoryManager mgr = Manager();
                if (mgr == null) return 0;
                var dto = mgr.GetNpcData(id);
                return dto == null ? 0 : dto.Favor;
            }
            catch { return 0; }
        }

        void SetSkillLevel(SkillID id, int value)
        {
            try
            {
                DataRepositoryManager mgr = Manager();
                if (mgr == null) return;
                var dto = mgr.GetExploreSkillData(id);
                if (dto == null) return;
                dto.Level = value;
                mgr.UpdateExploreSkillData(dto);
            }
            catch { }
        }

        int GetSkillLevel(SkillID id)
        {
            try
            {
                DataRepositoryManager mgr = Manager();
                if (mgr == null) return 0;
                var dto = mgr.GetExploreSkillData(id);
                return dto == null ? 0 : dto.Level;
            }
            catch { return 0; }
        }
    }

    static class PluginInfo
    {
        public const string GUID = "com.rebirthpub.trainer";
        public const string Name = "Rebirth Pub Trainer";
        public const string Version = "1.0.0";
    }
}
