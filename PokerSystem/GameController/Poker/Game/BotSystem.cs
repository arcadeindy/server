using CoinPokerCommonLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;

namespace CoinPokerServer.PokerSystem.GameController.Poker.Game
{
    public class BotSystem
    {
        public Timer ActionPlayerTimer { get; set; }
        public Game Game { get; set; }
        private static Random random;

        public BotSystem()
        {
            random = new Random();
        }

        public void Initial(Game game)
        {
            this.Game = game;
        }

        public double GetRandomNumber(double minimum, double maximum)
        {
            return random.NextDouble() * (maximum - minimum) + minimum;
        }

        public void ActionFor(double interval)
        {
            var actionIn = GetRandomNumber(1000, interval-1000);

            ActionPlayerTimer = new Timer(actionIn);
            ActionPlayerTimer.Elapsed += ActionPlayerTimer_Elapsed;
            ActionPlayerTimer.AutoReset = false;
            ActionPlayerTimer.Enabled = true;
        }

        private void ActionPlayerTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (Game.GameTableModel.ActionPlayer == null) return;
            
            //Pobieramy mozliwe przebicia [etc]
            switch (random.Next(1,7))
            {
                case 1:
                case 2:
                case 3:
                case 4:
                    Game.OnPlayerGameAction(Game.GameTableModel.ActionPlayer.User, Enums.ActionPokerType.Call, 0);
                    break;
                case 5:
                    Game.OnPlayerGameAction(Game.GameTableModel.ActionPlayer.User, Enums.ActionPokerType.Fold, 0);
                    break;
                case 6:
                    var stackDiv = random.Next(6, 18);
                    var raiseTo = GetRandomNumber((double)0.0, (double)Game.GameTableModel.ActionPlayer.Stack / stackDiv);
                    Game.OnPlayerGameAction(Game.GameTableModel.ActionPlayer.User, Enums.ActionPokerType.Raise, (decimal)raiseTo);
                    break;
            }

            ActionPlayerTimer.Enabled = false;
        }

    }
}
