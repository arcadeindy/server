using CoinPokerCommonLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using CoinPokerServer.PokerSystem.CommonExtensions;

namespace CoinPokerServer.PokerSystem.GameController.Poker.Game.Stages
{
    public class StageShowdown
    {
        Game Game { get; set; }
        Evaluator Evaluator { get; set; }

        public StageShowdown(Game game)
        {
            Game = game;
            Console.WriteLine("StageShowdown()");
        }

        public event Action<EvaluatorStrength> OnShowdownWinnerEvent;
        public void OnShowdownWinner(EvaluatorStrength evaluatorElement)
        {
            if (OnShowdownWinnerEvent != null)
                OnShowdownWinnerEvent(evaluatorElement);
        }

        /// <summary>
        /// Rozpoczecie pokazywania kart i wygranych
        /// </summary>
        public void ShowdownStart()
        {
            var gameTable = Game.GameTableModel;

            var showingPlayer = GetShowdownPlayerStart();
            var showingPlayerLoop = true;

            Evaluator = new Evaluator(Game);
            Evaluator.CalculateStrength();

            // Showdown rozpoczyna się wraz z graczem, który jako pierwszy wrzucił ostatnią stawkę do puli.
            // Jest nim ten gracz, który ustanowił stawkę do której wszyscy inni gracze dorównali.

            //Inicializacyjana wartosc, stack ktory pozostal do rozdania jest rowny sumie
            EvaluatorStrength bestStrenghtNow = null;
            do
            {
                if (showingPlayer.Status.HasFlag(PlayerModel.PlayerStatus.INGAME) || showingPlayer.Status.HasFlag(PlayerModel.PlayerStatus.DONTPLAY))
                {
                    //Obliczamy sile rozdan obecnych graczy
                    EvaluatorStrength playerStrenght = Evaluator.GetStrenght(showingPlayer);

                    if (
                        bestStrenghtNow == null ||
                        bestStrenghtNow.Points <= playerStrenght.Points ||
                        playerStrenght.Player.Stack == 0
                    )
                    {
                        if (playerStrenght.Player.Stack != 0)
                            bestStrenghtNow = playerStrenght;

                        //Pokazanie kart dla innych graczy
                        if (!PlayerShowedCardsEarlier(playerStrenght.Player))
                        {
                            //Pokazanie kart
                            CardShowupAction cardActionPlayer = new CardShowupAction()
                            {
                                Cards = playerStrenght.Player.Cards,
                                Stage = Game.GameTableModel.Stage,
                                CreatedAt = DateTime.Now,
                                Player = playerStrenght.Player
                            };

                            Game.GameTableModel.ActionHistory.Add(cardActionPlayer);

                            //Podswietlamy wygrany układ
                            CardHighlightAction cardHighlight = new CardHighlightAction()
                            {
                                Cards = playerStrenght.WinCardList,
                                Stage = Game.GameTableModel.Stage,
                                CreatedAt = DateTime.Now,
                                Player = playerStrenght.Player
                            };

                            Game.GameTableModel.ActionHistory.Add(cardHighlight);
                        }


                        string message = "Gracz " + playerStrenght.Player.User.Username + " pokazuje " + Evaluator.CardHandName(playerStrenght);

                        Console.Write(message);
                        Game.SendDealerMessage(message);

                        Thread.Sleep(1000);
                    }
                    else
                    {
                        //Ukrycie kart
                        CardHideupAction cardActionPlayer = new CardHideupAction()
                        {
                            Stage = Game.GameTableModel.Stage,
                            CreatedAt = DateTime.Now,
                            Player = playerStrenght.Player
                        };

                        Game.GameTableModel.ActionHistory.Add(cardActionPlayer);

                        //Gracz nie musi pokazywać tych kart
                        Console.Write("Gracz " + showingPlayer.User.Username + " zrzuca karty");
                        Game.SendDealerMessage("Gracz " + showingPlayer.User.Username + " zrzuca karty");
                    }

                }

                showingPlayer = Game.NextPlayer(showingPlayer);
                if (showingPlayer == GetShowdownPlayerStart())
                    showingPlayerLoop = false;

            } while (showingPlayerLoop);
        }

        /// <summary>
        /// Sprawdza czy gracz już pokazał wcześniej karty
        /// np: podczas allin
        /// </summary>
        /// <returns></returns>
        private bool PlayerShowedCardsEarlier(PlayerModel player)
        {
            var gameTable = Game.GameTableModel;
            return gameTable.ActionHistory.OfType<CardShowupAction>().
                Where(p => p.Player.User.ID == player.User.ID).Any();
        }

        public class PlayerContributed
        {
            public PlayerModel Player { get; set; }
            public decimal Contributed { get; set; }
        }

        /// <summary>
        /// Rozdzielanie puli według układów
        /// </summary>
        public void ShowdownPotDistribution()
        {
            var gameTable = Game.GameTableModel;

            //Sortujemy od najsilniejszych układów
            var evaluatorGroupedList = Evaluator.EvaluatorStrenghtList.
                GroupBy(c=>c.FullPoints).
                OrderByDescending(e => e.First().FullPoints).ThenBy(e => e.OrderBy(f=>f.Contributed)).
                ToList();

            //Suma pinieniędzy
            decimal totalPot = gameTable.ActionHistory.OfType<BetAction>().Sum(c => c.Bet);

            //Wszystkie wyniki na jednym poziomie
            var contributionFlatList = gameTable.ActionHistory.OfType<BetAction>().GroupBy(c => c.Player).Select(c => new PlayerContributed
            {
                Player = c.First().Player,
                Contributed = c.Sum(e=>e.Bet)
            }).ToList();

            foreach (var evaluatorItem in evaluatorGroupedList)
            {
                foreach (var evaluatorPlayer in evaluatorItem)
                {
                    decimal winPot = 0;
                    decimal evaluatorPlayerContributed = evaluatorPlayer.Contributed;

                    foreach (var evaluatorContributedPlayer in contributionFlatList)
                    {
                        decimal takenPot = 0;
                        if (evaluatorContributedPlayer.Contributed >= evaluatorPlayerContributed)
                            takenPot = evaluatorPlayerContributed;
                        else
                            takenPot = evaluatorContributedPlayer.Contributed;

                        evaluatorContributedPlayer.Contributed -= takenPot;
                        winPot += takenPot;
                    }

                    winPot = winPot / (evaluatorItem.Count());

                    if (winPot == 0) break;
                    
                    //Infomracja o wygranej puli
                    string message;
                    if (evaluatorPlayer.IsBest == true)
                    {
                        message = "Gracz " + evaluatorPlayer.Player.User.Username + " wygrywa główną pulę " + CurrencyFormat.Get(gameTable.Currency, winPot);
                    }
                    else
                    {
                        message = "Gracz " + evaluatorPlayer.Player.User.Username + " wygrywa boczną pulę " + CurrencyFormat.Get(gameTable.Currency, winPot);
                    }

                    if (evaluatorPlayer.IsKickerWin)
                    {
                        message += " kickerem " + CardModel.GetNormalizeNominal(evaluatorPlayer.KickerCards.FirstOrDefault().Face, CardModel.NormalizeNominalSize.ONE);
                    }

                    OnShowdownWinnerEvent(evaluatorPlayer);

                    TablePotAction tablePotAction = new TablePotAction()
                    {
                        Stage = Game.GameTableModel.Stage,
                        CreatedAt = DateTime.Now,
                        Player = evaluatorPlayer.Player,
                        Pot = winPot
                    };
                    Game.GameTableModel.ActionHistory.Add(tablePotAction);

                    Game.SendDealerMessage(message);
                    Console.WriteLine(message);
                    gameTable.ShowSystemMessage("Wygrana " + evaluatorPlayer.Player.User.Username, message);
                    Thread.Sleep(2000);
                    gameTable.HideSystemMessage();
                }
            }
        }


        /// <summary>
        /// Pobiera akcje od ktorej zaczyna sie showdown
        /// </summary>
        /// <returns></returns>
        private PlayerModel GetShowdownPlayerStart()
        {
            var gameTable = Game.GameTableModel;

            //Szukamy gracza który jako ostatni przebił/podjął akcję i nie spasował
            var lastBetUser = gameTable.ActionHistory.OfType<BetAction>().
                 //Where(b => b.Player.Status == PlayerModel.PlayerStatus.INGAME).
                 Where(b => b.Action == Enums.ActionPokerType.Raise || b.Action == Enums.ActionPokerType.BigBlind).
                 OrderBy(b => b.CreatedAt.Date).ThenBy(c => c.CreatedAt.TimeOfDay).Select(c=>c.Player).
                 FirstOrDefault();

            //jesli spasował to na lewo od tego gracza
            while (!lastBetUser.Status.HasFlag(PlayerModel.PlayerStatus.INGAME))
            {
                lastBetUser = Game.NextPlayer(lastBetUser);
            }

            return lastBetUser;
        }
    }
}
