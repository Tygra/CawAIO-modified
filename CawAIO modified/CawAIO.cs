using System;
using System.IO;
using System.Linq;
using System.ComponentModel;
using System.Collections.Generic;
using System.Timers;
using TShockAPI;
using TShockAPI.Extensions;
using Terraria;
using Newtonsoft.Json;
using TerrariaApi;
using TerrariaApi.Server;
using TShockAPI.DB;
using Wolfje.Plugins.SEconomy;
using Wolfje.Plugins.SEconomy.Journal;

namespace CawAIO
{
    [ApiVersion(1, 21)]
    public class CawAIO : TerrariaPlugin
    {
        private Config config;
        public DateTime LastCheck = DateTime.UtcNow;
        public DateTime SLastCheck = DateTime.UtcNow;
        public CPlayer[] Playerlist = new CPlayer[256];
        DateTime DLastCheck = DateTime.UtcNow;

        public override Version Version
        {
            get { return new Version("2.1"); }
        }

        public override string Name
        {
            get { return "CAWAIO"; }
        }

        public override string Author
        {
            get { return "CAWCAWCAW"; }
        }

        public override string Description
        {
            get { return "Randomized Commands"; }
        }

        public CawAIO(Main game)
            : base(game)
        {
            Order = 1;
        }

        #region Initialize
        public override void Initialize()
        {
            TShockAPI.Commands.ChatCommands.Add(new Command("caw.bunny", Bunny, "bunny"));
            TShockAPI.Commands.ChatCommands.Add(new Command("caw.reload", Reload_Config, "creload"));
            TShockAPI.Commands.ChatCommands.Add(new Command("caw.monstergamble", MonsterGamble, "monstergamble", "mg"));
            ServerApi.Hooks.ServerChat.Register(this, ShadowDodgeCommandBlock);
            ServerApi.Hooks.GameUpdate.Register(this, DisableShadowDodgeBuff);
            ServerApi.Hooks.ServerJoin.Register(this, OnJoin);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
            ServerApi.Hooks.GameUpdate.Register(this, Cooldowns);
            ReadConfig();
        }
        #endregion

        #region Dispose
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.ServerChat.Deregister(this, ShadowDodgeCommandBlock);
                ServerApi.Hooks.GameUpdate.Deregister(this, DisableShadowDodgeBuff);
                ServerApi.Hooks.ServerJoin.Deregister(this, OnJoin);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
                ServerApi.Hooks.GameUpdate.Deregister(this, Cooldowns);
            }
            base.Dispose(disposing);
        }
        #endregion

        #region Playerlist OnJoin/OnLeave
        public void OnJoin(JoinEventArgs args)
        {
            Playerlist[args.Who] = new CPlayer(args.Who);
        }
        public void OnLeave(LeaveEventArgs args)
        {
            Playerlist[args.Who] = null;
        }
        #endregion

        #region Disable Shadow Dodge Buff
        private void DisableShadowDodgeBuff(EventArgs e)
        {
            if ((DateTime.UtcNow - SLastCheck).TotalSeconds >= config.BlockShadowDodgeTimerInSeconds)
            {
                foreach (TSPlayer p in TShock.Players)
                {
                    if (p != null && p.Active && p.ConnectionAlive)
                    {
                        for (int i = 0; i < p.TPlayer.buffType.Length; i++)
                        {
                            if (p.TPlayer.buffType[i] == 59 && p.TPlayer.buffTime[i] > 30 && !p.Group.HasPermission("caw.shadowbypass"))
                            {
                                p.TPlayer.buffTime[i] = 0;
                                p.SendErrorMessage("You are not allowed to use shadow dodge!");
                                p.Disable("Using Shadow Dodge buff for greater than 20 seconds.", true);
                            }
                        }
                    }
                }
                SLastCheck = DateTime.UtcNow;
            }
        }
        #endregion

        #region Cooldowns
        private void Cooldowns(EventArgs args)
        {
            if ((DateTime.UtcNow - LastCheck).TotalSeconds >= 1)
            {
                LastCheck = DateTime.UtcNow;
                foreach (var player in Playerlist)
                {
                    if (player == null)
                    {
                        continue;
                    }
                    if (player.MonsterGambleCooldown > 0)
                    {
                        player.MonsterGambleCooldown--;
                    }
                }
            }
        }
        #endregion

        #region Monster Gambling
        private void MonsterGamble(CommandArgs args)
        {
            Random random = new Random();
            int amount = random.Next(1, 50);
            var Journalpayment = Wolfje.Plugins.SEconomy.Journal.BankAccountTransferOptions.AnnounceToSender;
            var selectedPlayer = SEconomyPlugin.Instance.GetBankAccount(args.Player.User.Name);
            var playeramount = selectedPlayer.Balance;
            var player = Playerlist[args.Player.Index];
            Money moneyamount = -config.MonsterGambleCost;
            Money moneyamount2 = config.MonsterGambleCost;

            if (player.MonsterGambleCooldown == 0)
            {
                if (!args.Player.Group.HasPermission("caw.nocooldown"))
                {
                    player.MonsterGambleCooldown = config.MonsterGambleCooldown;
                }

                if (config.SEconomy)
                {
                    {
                        if (!args.Player.Group.HasPermission("caw.gamble.nocost"))
                        {
                            if (playeramount > moneyamount2)
                            {
                                int monsteramount;
                                do
                                {
                                    monsteramount = random.Next(1, 539);
                                    args.Player.SendInfoMessage("You have gambled a banned monster, attempting to regamble...", Color.Yellow);
                                } while (config.MonsterExclude.Contains(monsteramount));

                                NPC npcs = TShock.Utils.GetNPCById(monsteramount);
                                TSPlayer.Server.SpawnNPC(npcs.type, npcs.name, amount, args.Player.TileX, args.Player.TileY, 50, 20);                                
                                args.Player.SendSuccessMessage("You have lost {0} for monster gambling.", moneyamount2);
                                SEconomyPlugin.Instance.WorldAccount.TransferToAsync(selectedPlayer, moneyamount, Journalpayment, string.Format("{0} has been lost for monster gambling", moneyamount2, args.Player.Name), string.Format("CawAIO: " + "Monster Gambling"));
                                TShock.Log.ConsoleInfo("{0} has spawnned {1} {2}.", args.Player.Name, amount, npcs.name);
                            }
                            else
                            {
                                args.Player.SendErrorMessage("You need {0} to gamble, you have {1}.", moneyamount2, selectedPlayer.Balance);
                            }
                        }
                        else
                        {
                            if (args.Player.Group.HasPermission("caw.gamble.nocost"))
                            {
                                int monsteramount;
                                do
                                {
                                    monsteramount = random.Next(1, 539);
                                } while (config.MonsterExclude.Contains(monsteramount));
                                NPC npcs = TShock.Utils.GetNPCById(monsteramount);
                                TSPlayer.Server.SpawnNPC(npcs.type, npcs.name, amount, args.Player.TileX, args.Player.TileY, 50, 20);
                                TSPlayer.All.SendSuccessMessage(string.Format("{0} has randomly spawned {1} {2} time(s).", args.Player.Name, npcs.name, amount));
                                args.Player.SendSuccessMessage("You have lost nothing for monster gambling.");
                                TShock.Log.ConsoleInfo("{0} has spawnned {1} {2}.", args.Player.Name, amount, npcs.name);
                            }
                        }
                    }
                }
                else
                {
                    int Randnpc;

                    do Randnpc = random.Next(1, 539);
                    while (config.MonsterExclude.Contains(Randnpc));

                    NPC npcs = TShock.Utils.GetNPCById(Randnpc);
                    TSPlayer.Server.SpawnNPC(npcs.type, npcs.name, amount, args.Player.TileX, args.Player.TileY, 50, 20);

                    TSPlayer.All.SendSuccessMessage(string.Format("{0} has randomly spawned {1} {2} time(s).", args.Player.Name,
                        npcs.name, amount));
                    TShock.Log.ConsoleInfo("{0} has spawnned {1} {2}.", args.Player.Name, amount, npcs.name);
                }
            }
            else
            {
                args.Player.SendErrorMessage("This command is on cooldown for {0} seconds.", (player.MonsterGambleCooldown));
            }
        }
        #endregion        

        #region Bunny Command
        private void Bunny(CommandArgs args)
        {
            TSPlayer player = TShock.Players[args.Player.Index];
            {
                player.SendMessage("You have been buffed with a pet bunny (I think it wants your carrot).", Color.Green);
                player.SetBuff(40, 60, true);
            }
        }
        #endregion

        #region Block Shadow Dodge Command Usage
        private void ShadowDodgeCommandBlock(ServerChatEventArgs args)
        {
            if (args.Handled)
            {
                return;
            }
            TSPlayer player = TShock.Players[args.Who];
            if (player == null)
            {
                return;
            }

            if (config.BlockShadowDodgeBuff)
            {
                if (args.Text.ToLower().StartsWith("/buff") && args.Text.ToLower().Contains("shadow d") ||
                    args.Text.ToLower().StartsWith("/buff") && args.Text.ToLower().Contains("\"shadow d") ||
                    args.Text.ToLower().StartsWith("/buff") && args.Text.ToLower().Contains("59"))
                {
                    if (player.Group.HasPermission("caw.shadowbypass"))
                    {
                        args.Handled = false;
                    }
                    else
                    {
                        args.Handled = true;
                        player.SendInfoMessage("Shadow Dodge is not a buff you can use on this server through commands.");
                    }
                }
            }
        }
        #endregion
        

        #region Create Config File
        private void CreateConfig()
        {
            string filepath = Path.Combine(TShock.SavePath, "CawAIO.json");
            try
            {
                using (var stream = new FileStream(filepath, FileMode.Create, FileAccess.Write, FileShare.Write))
                {
                    using (var sr = new StreamWriter(stream))
                    {
                        config = new Config();
                        var configString = JsonConvert.SerializeObject(config, Formatting.Indented);
                        sr.Write(configString);
                    }
                    stream.Close();
                }
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError(ex.Message);
            }
        }
        #endregion

        #region Read Config File
        private bool ReadConfig()
        {
            string filepath = Path.Combine(TShock.SavePath, "CawAIO.json");
            try
            {
                if (File.Exists(filepath))
                {
                    using (var stream = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        using (var sr = new StreamReader(stream))
                        {
                            var configString = sr.ReadToEnd();
                            config = JsonConvert.DeserializeObject<Config>(configString);
                        }
                        stream.Close();
                    }
                    return true;
                }
                else
                {
                    TShock.Log.ConsoleError("CawAIO config not found. Creating new one...");
                    CreateConfig();
                    return false;
                }
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError(ex.Message);
            }
            return false;
        }
        #endregion

        #region Config Class
        public class Config
        {
            public bool SEconomy = true;
            public bool BlockShadowDodgeBuff = true;
            public int BlockShadowDodgeTimerInSeconds = 30;
            public int MonsterGambleCost = 1000;
            public int MonsterGambleCooldown = 30;
            public int[] MonsterExclude = { 17, 18, 19, 20, 22, 37, 38, 54, 68, 85, 105, 106, 107, 108, 123, 124, 125, 126, 128, 129, 130, 131, 134, 135, 136, 139, 142, 143, 144, 145, 158, 159, 160, 162, 166, 172, 178, 207, 208, 209, 212, 213, 214, 215, 216, 227, 228, 229, 245, 246, 247, 248, 251, 253, 262, 263, 264, 269, 276, 281, 282, 288, 290, 305, 306, 307, 308, 309, 310, 311, 312, 313, 314, 315, 325, 326, 327, 328, 338, 339, 340, 344, 345, 346, 353, 354, 368, 369, 370, 372, 373, 376, 381, 382, 383, 385, 386, 389, 390, 391, 392, 393, 394, 395, 396, 397, 398, 399, 422, 439, 441, 453, 460, 461, 462, 463, 466, 467, 468, 473, 474, 475, 476, 477, 491, 492, 493, 507, 517, 521, 2889, 2890, 2891, 2892, 2893, 2894, 2895, 3564 };

        }
        #endregion

        #region Reload Config File
        private void Reload_Config(CommandArgs args)
        {
            if (ReadConfig())
            {
                args.Player.SendMessage("CawAIO config reloaded sucessfully.", Color.Yellow);
            }
            else
            {
                args.Player.SendErrorMessage("CawAIO config reloaded unsucessfully. Check logs for details.");
            }
        }
        #endregion
    }
}