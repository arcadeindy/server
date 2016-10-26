using CoinPokerCommonLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoinPokerServer.PokerSystem.CurrencyController
{
    public interface ICurrencyController
    {
        void OnUserPayout(UserModel user);
        void OnUserPayIn(UserModel user);
    }
}
