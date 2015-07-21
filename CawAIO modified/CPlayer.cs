using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terraria;
using TShockAPI;

namespace CawAIO
{
    public class CPlayer
    {
        public int Index { get; set; }
        public TSPlayer TSPlayer { get { return TShock.Players[Index]; } }
        public int MonsterGambleCooldown { get; set; }
        public int WarningCount { get; set; }
        public CPlayer(int index)
        {
            this.Index = index;
        }
    }
}