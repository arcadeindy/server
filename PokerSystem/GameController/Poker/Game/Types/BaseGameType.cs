using CoinPokerCommonLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CoinPokerServer.PokerSystem.GameController.Poker.Game.Types
{
    class BaseGameType
    {
        public Game Game { get; set; }
        public List<CardModel> CardList {get;set;}

        public void Initialize(Game game)
        {
            this.Game = game;
        }

        protected void InitializeCards()
        {
            CardList = new List<CardModel>();

            for (int n = 0; n <= 12; n++)
            {
                for (int s = 0; s <= 3; s++)
                {
                    CardList.Add(new CardModel() { Suit = (CardModel.CardSuit)s, Face = (CardModel.CardNominalValue)n });
                }
            }

            Helper.Shuffle<CardModel>(CardList);
        }
    }
}
