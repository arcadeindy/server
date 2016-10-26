using CoinPokerCommonLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CoinPokerServer.PokerSystem.GameController.Poker.Game
{
    public interface IGameController
    {
        /// <summary>
        /// Inicializuje
        /// </summary>
        void Initialize();

        /// <summary>
        /// Obserwator gry
        /// </summary>
        void Worker();
    }
}
