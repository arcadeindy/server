using CoinPokerCommonLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CoinPokerServer.PokerSystem.GameController.Poker.Game.Types
{
    public interface IGameType
    {
        List<CardModel> CardList { get; set; }

        void Initialize(Game game);
        void OnGameStart();
    }
}
