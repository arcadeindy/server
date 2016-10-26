using CoinPokerCommonLib;
using CoinPokerServer.PokerSystem.GameController.Poker.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace CoinPokerServer.Collections
{
    [Serializable]
    public class GameCollection
    {
        [XmlElement("NormalGameController")]
        public List<IGameController> GameControllerList { get; set; }

        public GameCollection()
        {
            GameControllerList = new List<IGameController>();
        }

        public List<IGameController> Get()
        {
            return this.GameControllerList;
        }

        public List<T> Get<T>()
        {
            return this.GameControllerList.OfType<T>().ToList();
        }
    }
}
