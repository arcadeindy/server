using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CoinPokerServer.PokerSystem.Models
{
    public class HandStatistics
    {
        public DateTime Date {get;set;}
        public int HandID;
    }

    public class UserLoggedStatistics
    {
        public DateTime Date { get; set; }
        public int UserID;
    }

    public class GlobalStatistics
    {
        public List<HandStatistics> HandCount;
        public List<UserLoggedStatistics> UserLogged;
    }
}
