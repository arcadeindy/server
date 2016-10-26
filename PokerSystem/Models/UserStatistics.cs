using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CoinPokerServer.PokerSystem.Models
{
    public class UserPotStatistics
    {
        public DateTime Timestamp;
        public decimal WinPot;
    }

    public class UserStatistics
    {
        public int HandWinCounter { get; set; }
        public int HandCounter { get; set; }
        public int FlopViewCounter { get; set; }
        public int RiverViewCounter { get; set; }

        public List<UserPotStatistics> WinPotHistory { get; set; }
    }
}
