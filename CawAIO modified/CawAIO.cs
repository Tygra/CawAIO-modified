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
    [ApiVersion(1, 19)]
    public class CawAIO : TerrariaPlugin
    {
        public int WarningCount = 0;
        private Config config;
        public DateTime LastCheck = DateTime.UtcNow;
        public DateTime SLastCheck = DateTime.UtcNow;
        public CPlayer[] Playerlist = new CPlayer[256];
        DateTime DLastCheck = DateTime.UtcNow;

        public override Version Version
        {
            get { return new Version("1.9.5"); }
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
            TShockAPI.Commands.ChatCommands.Add(new Command("caw.smack", Smack, "smack"));
            TShockAPI.Commands.ChatCommands.Add(new Command("caw.bunny", Bunny, "bunny"));
            TShockAPI.Commands.ChatCommands.Add(new Command("caw.reload", Reload_Config, "creload"));
            TShockAPI.Commands.ChatCommands.Add(new Command("caw.monstergamble", MonsterGamble, "monstergamble", "mg"));
            ServerApi.Hooks.ServerChat.Register(this, ShadowDodgeCommandBlock);
            ServerApi.Hooks.ServerChat.Register(this, Actionfor);
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
                ServerApi.Hooks.ServerChat.Deregister(this, Actionfor);
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
                            if (p.TPlayer.buffType[i] == 59 && p.TPlayer.buffTime[i] > 20 && !p.Group.HasPermission("caw.shadowbypass"))
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
                                TSPlayer.All.SendSuccessMessage(string.Format("{0} has randomly spawned {1} {2} time(s).", args.Player.Name, npcs.name, amount));
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

        #region Smack command
        private void Smack(CommandArgs args)
        {
            if (args.Parameters.Count > 0)
            {
                string plStr = string.Join(" ", args.Parameters);
                var players = TShock.Utils.FindPlayer(plStr);
                if (players.Count == 0)
                    args.Player.SendErrorMessage("No player matched your query '{0}'", plStr);
                else if (players.Count > 1)
                    TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
                else
                {
                    var plr = players[0];
                    TSPlayer.All.SendSuccessMessage(string.Format("{0} smacked {1}.",
                                                         args.Player.Name, plr.Name));
                    TShock.Log.Info(args.Player.Name + " smacked " + plr.Name);
                }
            }
            else
                args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /smack <player>");
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

        #region Block Banned Words
        private void Actionfor(ServerChatEventArgs args)
        {
            var ignored = new List<string>();
            var censored = new List<string>();
            var warningwords = new List<string>();
            var player = TShock.Players[args.Who];
            var text = args.Text;

            if (player == null)
            {
                return;
            }

            if (!args.Text.ToLower().StartsWith("/") || args.Text.ToLower().StartsWith("/w") ||
                args.Text.ToLower().StartsWith("/r") || args.Text.ToLower().StartsWith("/me") ||
                args.Text.ToLower().StartsWith("/c") || args.Text.ToLower().StartsWith("/party"))
            {
                foreach (string Word in config.BanWords)
                {
                    if (player.Group.HasPermission("caw.filterbypass"))
                    {
                        args.Handled = false;
                    }

                    else if (args.Text.ToLower().Equals(Word))
                    {
                        if (player.mute)
                        {
                            player.SendErrorMessage("You are muted!");
                            return;
                        }
                        else
                        {
                            switch (config.ActionForBannedWord)
                            {
                                case "tempban":
                                    args.Handled = true;
                                    if (config.WarningSystem)
                                    {
                                        foreach (var wplayer in Playerlist)
                                        {
                                            if (wplayer == null)
                                            {
                                                return;
                                            }
                                            if (wplayer.WarningCount >= config.AmountofWarningBeforeAction)
                                            {
                                                TShock.Bans.AddBan(player.IP, player.Name, player.UUID, config.KickMessage, false, player.User.Name, DateTime.UtcNow.AddMinutes(config.BanTimeInMinutes).ToString("m"));
                                            }
                                            else
                                            {
                                                wplayer.WarningCount += 1;
                                                warningwords.Add(Word);
                                                player.SendErrorMessage("You have said a banned word: " + string.Join(" ", warningwords) + " You will be temp-banned in " + (config.AmountofWarningBeforeAction - wplayer.WarningCount) + " more incidents.");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        TShock.Bans.AddBan(player.IP, player.Name, player.UUID, config.KickMessage, false, player.User.Name, DateTime.UtcNow.AddMinutes(config.BanTimeInMinutes).ToString("m"));
                                    }
                                    return;
                                case "ban":
                                    args.Handled = true;
                                    if (config.WarningSystem)
                                    {
                                        foreach (var wplayer in Playerlist)
                                        {
                                            if (wplayer == null)
                                            {
                                                return;
                                            }
                                            if (wplayer.WarningCount >= config.AmountofWarningBeforeAction)
                                            {
                                                TShock.Bans.AddBan(player.IP, player.Name, player.UUID, config.KickMessage, false, player.User.Name);
                                            }
                                            else
                                            {
                                                wplayer.WarningCount += 1;
                                                warningwords.Add(Word);
                                                player.SendErrorMessage("You have said a banned word: " + string.Join(" ", warningwords) + " You will be banned in " + (config.AmountofWarningBeforeAction - wplayer.WarningCount) + " more incidents.");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        TShock.Bans.AddBan(player.IP, player.Name, player.UUID, config.KickMessage, false, player.User.Name);
                                    }
                                    return;
                                case "kick":
                                    args.Handled = true;
                                    if (config.WarningSystem)
                                    {
                                        foreach (var wplayer in Playerlist)
                                        {
                                            if (wplayer == null)
                                            {
                                                return;
                                            }
                                            if (wplayer.WarningCount >= config.AmountofWarningBeforeAction)
                                            {
                                                TShock.Utils.Kick(player, config.KickMessage, true, false);
                                            }
                                            else
                                            {
                                                wplayer.WarningCount += 1;
                                                warningwords.Add(Word);
                                                player.SendErrorMessage("You have said a banned word: " + string.Join(" ", warningwords) + " You will be kicked in " + (config.AmountofWarningBeforeAction - wplayer.WarningCount) + " more incidents.");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        TShock.Utils.Kick(player, config.KickMessage, true, false);
                                    }
                                    return;
                                case "ignore":
                                    args.Handled = true;
                                    ignored.Add(Word);
                                    break;
                                case "censor":
                                    args.Handled = true;
                                    text = args.Text;
                                    text = args.Text.Replace(Word, new string('*', Word.Length));
                                    string.Format(config.ChatFormat, player.Group.Name, player.Group.Prefix, player.Name, player.Group.Suffix, text, player.Group.R, player.Group.G, player.Group.B);
                                    //TSPlayer.All.SendMessage("<" + "(" + player.Group.Name + ") " + player.Name + ">" + text, player.Group.R, player.Group.G, player.Group.B);
                                    //TSPlayer.All.SendMessage(player.Group.Prefix + player.Name + ": " + text, player.Group.R, player.Group.G, player.Group.B);
                                    return;
                                case "donothing":
                                    args.Handled = false;
                                    break;
                            }
                        }
                    }
                }
                if (warningwords.Count > 0 && WarningCount < 3)
                {
                    player.SendErrorMessage("Your message has been ignored for saying: " + string.Join(", ", warningwords));
                    player.SendErrorMessage("Your warning count is now: {0}. After {1} warnings you will be {2}", WarningCount, config.AmountofWarningBeforeAction, config.ActionForBannedWord.ToString());
                }
                if (ignored.Count > 0)
                {
                    player.SendErrorMessage("Your message has been ignored for saying: " + string.Join(", ", ignored));
                    return;
                }
            }
            else
            {
                args.Handled = false;
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
            public string ActionForBannedWord = "ignore";
            public string ChatFormat = "[{0}]{2}{3}{4}: {5}";
            public bool WarningSystem = true;
            public int AmountofWarningBeforeAction = 3;
            public string[] BanWords = { "yolo", "swag", "can i be staff", "can i be admin" };
            public string KickMessage = "You have said a banned word.";
            public int BanTimeInMinutes = 10;
            public bool SEconomy = false;
            public bool BlockShadowDodgeBuff = false;
            public int BlockShadowDodgeTimerInSeconds = 1;
            public int MonsterGambleCost = 50000;
            public int MonsterGambleCooldown = 0;
            public int[] MonsterExclude = { 9, 22, 68, 17, 18, 37, 38, 19, 20, 37, 54, 68, 106, 123, 124, 107, 108, 113, 142, 178, 207, 208, 209, 227, 228, 160, 229, 353, 368 };

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