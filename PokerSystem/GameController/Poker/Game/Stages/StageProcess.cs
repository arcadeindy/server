using CoinPokerCommonLib;
using CoinPokerCommonLib.Models.OfferAction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Timers;
using CoinPokerServer.PokerSystem.CommonExtensions;
using System.Threading.Tasks;

namespace CoinPokerServer.PokerSystem.GameController.Poker.Game.Stages
{
    public class StageProcess
    {
        System.Timers.Timer ActionPlayerTimer { get; set; }
        Game Game { get; set; }

        bool IsAllInProcessStarted = false;

        /// <summary>
        /// Tworzy proces przebicia/rozdania/gry
        /// </summary>
        /// <param name="game"></param>
        public StageProcess(Game game)
        {
            Console.WriteLine("StageProcess()");
            Game = game;

            Game.OnPlayerGameActionEvent += Game_OnPlayerGameActionEvent;
            OnAllInProcessStartedEvent += StageProcess_OnAllInProcessStartedEvent;
        }

        void StageProcess_OnAllInProcessStartedEvent()
        {
            //Pobieramy wszystkich graczy aktywnych w rozdaniu
            //i pokazujemy karty od razu
            foreach (PlayerModel player in this.Game.GameTableModel.PlayerHavingPlayStatus())
            {
                Task.Factory.StartNew(() =>
                {
                    CardShowupAction cardActionPlayer = new CardShowupAction()
                    {
                        Cards = player.Cards,
                        Stage = Game.GameTableModel.Stage,
                        CreatedAt = DateTime.Now,
                        Player = player
                    };

                    Game.GameTableModel.ActionHistory.Add(cardActionPlayer);
                });
            }
        }

        void Game_OnPlayerGameActionEvent(UserModel user, Enums.ActionPokerType action, decimal actionValue)
        {
            Console.WriteLine("Game_OnPlayerGameActionEvent()");
            //Sprawdzamy czy gracz jest aktywny w tym momencie
            //lub czy w ogole jest mozliwosc podjecia akcji na tym stole
            var table = Game.GameTableModel;

            lock (table.ActionPlayer)
            {
                if (table.ActionPlayer == null ||
                    (
                        table.ActionPlayer != null &&
                        user.ID != table.ActionPlayer.User.ID)
                    )
                {
                    Console.WriteLine("ActinPlayer is null");
                    return;
                }

                if (action == Enums.ActionPokerType.BigBlind || action == Enums.ActionPokerType.SmallBlind)
                {
                    return; //Nie mozna wywołać bigblind, smallblind z poziomu użytkownika
                }

                actionValue = this.ParseBetActionValue(action, actionValue);

                if (ActionPlayerTimer != null)
                {
                    ActionPlayerTimer.Elapsed -= ActionPlayerNoAction;
                    ActionPlayerTimer = null;
                }

                //Ukrywamy dostępne akcje jako że wykonano akcję betaction
                //Ukrywamy je tylko dla osob ktore wykonaly akcje, jesli zostala wykonana akacja autoamtyczna to znaczy
                //ze gracz otrzymal flage DONTPLAY
                //wiec umozliwiamy mu powrot co zostalo juz wczesniej mu wyslane
                Task.Factory.StartNew(() =>
                {
                    if (table.ActionPlayer != null && table.ActionPlayer.User.IsOnline() && !table.ActionPlayer.Status.HasFlag(PlayerModel.PlayerStatus.DONTPLAY))
                    {
                        var _c = table.ActionPlayer.User.GetClient();
                        _c.OnGameActionOffer(table, new HideOfferAction() { Timestamp = DateTime.Now });
                    }
                });

                BetAction stageAction = new BetAction()
                {
                    Action = action,
                    Bet = actionValue,
                    CreatedAt = DateTime.Now,
                    Stage = table.Stage,
                    Player = table.ActionPlayer
                };

                table.ActionHistory.Add(stageAction);

                string message;
                string action_str;
                switch (action)
                {
                    case Enums.ActionPokerType.Fold:
                        action_str = "pasuje";
                        break;
                    case Enums.ActionPokerType.Call:
                        action_str = "sprawdza";
                        break;
                    case Enums.ActionPokerType.Raise:
                        action_str = "podbija do " + CurrencyFormat.Get(table.Currency, table.ActionHistory.OfType<BetAction>().Last().Bet);
                        break;
                    default:
                        action_str = "--bład--";
                        break;
                }
                message = "Gracz " + table.ActionPlayer.User.Username + " " + action_str + ".";

                Task.Factory.StartNew(() =>
                {
                    Game.SendDealerMessage(message);
                });


                Task.Factory.StartNew(() =>
                {
                    StageLoop();
                });
            }
        }

        public void Initialize()
        {
            IsAllInProcessStarted = false;
        }

        /// <summary>
        /// Pobiera wartość "sprawdzenia" lub "czekania" dla danego gracza w aktualnym momencie gry
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        private decimal GetCallValue(PlayerModel player)
        {
            var gameTable = Game.GameTableModel;
            decimal to = gameTable.GetStageBet(gameTable.Stage);
            decimal call = gameTable.ActionHistory.ToList().OfType<BetAction>().Where(s => s.Stage == gameTable.Stage).
                                  Where(p => p.Player.User.ID == player.User.ID).
                                  Sum(s => s.Bet);
            return to - call;
        }

        /// <summary>
        /// Zmiana stage
        /// </summary>
        public event Action<Enums.Stage> OnStageChangeEvent;
        public void OnStageChange(Enums.Stage stage)
        {
            if (OnStageChangeEvent != null)
                OnStageChangeEvent(stage);
        }

        /// <summary>
        /// Koniec rozdania/gry
        /// </summary>
        public event Action OnStageProcessFinishEvent;
        public void OnStageProcessFinish()
        {
            if (OnStageProcessFinishEvent != null)
                OnStageProcessFinishEvent();
        }

        /// <summary>
        /// Pobiera akcje przebić w danym stagu gry od graczy ktorzy sa w grze
        /// </summary>
        /// <returns></returns>
        private List<BetAction> GetInGameBetActionList()
        {
            var gameTable = Game.GameTableModel;
            return gameTable.ActionHistory.OfType<BetAction>().
                ToList().
                Where(b => b.Player.Status.HasFlag(PlayerModel.PlayerStatus.INGAME)).
                ToList();
        }

        /// <summary>
        /// Sprawdza czy etap przebic sie skonczyl biorac pod uwage aktualny stage
        /// </summary>
        /// <returns></returns>
        private bool IsStageBettingFinished()
        {
            //Jeśli gracz którego sprawdzamy doszedł do kwoty na stole 
            //Oraz jeśli liczba akcji na stole w danym stage jest równa liczbie graczy(?)
            //Oraz wszyscy gracze ktorzy maja status INGAME wplacili w tej turze kwote ktora obecnie jest na stole
            var gameTable = Game.GameTableModel;
            var InGameActionList = GetInGameBetActionList();
            var allInPlayerList = gameTable.PlayerHavingPlayStatus().Where(e => e.Stack == 0).ToList();

            bool isStageBettingFinished = (
             InGameActionList.
             Where(b => b.Stage == gameTable.Stage).            //i jeśli gracze którzy wykonali akcję w danym stage
             GroupBy(p => p.Player).                            //dorównali sumą najwyższej stawce na stole
             Select(g => new
             {                                                  //lub grają all-in
                 player = g.First(),
                 sum = g.Sum(p => p.Bet),
                 lastaction = InGameActionList.
                              Where(b => b.Stage == gameTable.Stage).OrderByDescending(a => a.CreatedAt).
                              FirstOrDefault(p => p.Player.User.ID == g.First().Player.User.ID).Action
             }).
             //Nie bierzemy pod uwage graczy allin
             Where(p => !allInPlayerList.Contains(p.player.Player)).
             //Gracz bigblind takze musi wykonac jakas akcje
             Where(p => p.lastaction != Enums.ActionPokerType.BigBlind).
             Where(p => p.sum == gameTable.GetStageBet(gameTable.Stage)/* || p.allin*/).
             //Liczba graczy wykoinujacych akcje minus gracze allin odjac graczy ktorzy allinuja sie w danej rundzie
             Count() == gameTable.PlayerHavingPlayStatus().Except(allInPlayerList).Count()
            );

            if (Game.GameTableModel.Name == "Fun Almar I")
            {
                var x = gameTable.PlayerHavingPlayStatus();
                var s = gameTable.GetStageBet(gameTable.Stage);

                Console.WriteLine("X");
            }

            return isStageBettingFinished;
        }

        /// <summary>
        /// Zmienia etapy/konczy proces gry
        /// </summary>
        /// <returns></returns>
        public bool Process()
        {
            Console.WriteLine("Process()");
            var gameTable = Game.GameTableModel;

            //Sprawdzamy czy dany stage zakończył się
            //lub czy został tylko jeden gracz w grze
            //lub czy nie nastapic praces allin
            var isBettingFinished = IsStageBettingFinished();
            var isAllInShowdown = IsPlayersStartedAllInShowdown();
            var isFoldedAction = gameTable.PlayerHavingPlayStatus().Count == 1;
        
            if (isBettingFinished || isAllInShowdown || isFoldedAction)
            {
                //Następny stage
                Enums.Stage nextStage;
                switch (gameTable.Stage)
                {
                    case Enums.Stage.Preflop:
                        nextStage = Enums.Stage.Flop;
                        break;
                    case Enums.Stage.Flop:
                        nextStage = Enums.Stage.Turn;
                        break;
                    default:
                    case Enums.Stage.Turn:
                        nextStage = Enums.Stage.River;
                        break;
                }

                //Przy zmianie stage, lub koncu gry usuwamy aktywnego gracza
                Console.WriteLine("Set actionplayer = null in process()");
                gameTable.ActionPlayer = null;
                if (ActionPlayerTimer != null)
                    ActionPlayerTimer.Enabled = false;

                //Czy proces przebijania sie zakonczyl
                if (gameTable.Stage == nextStage || isFoldedAction)
                {
                    OnStageProcessFinish();
                }
                else //Zmiana stage
                {
                    gameTable.Stage = nextStage;
                    OnStageChange(nextStage);

                    Task.Factory.StartNew(() =>
                    {
                        StageLoop();
                    });
                }
                return false;
            }
            else //Proces przebijania trwa
            {
                GetNextStageActionPlayer();
                return true;
            }
        }

        /// <summary>
        /// Pobiera gracza akcji
        /// </summary>
        private void GetNextStageActionPlayer()
        {
            var gameTable = Game.GameTableModel;
            if (gameTable.ActionPlayer == null)
            {
                if (gameTable.Stage == Enums.Stage.Preflop)
                {
                    gameTable.ActionPlayer = Game.NextPlayer(Game.NextPlayer(Game.NextPlayer(gameTable.Dealer)));
                }
                else
                {
                    gameTable.ActionPlayer = Game.NextPlayer(gameTable.Dealer);
                }
            }
            else
            {
                gameTable.ActionPlayer = Game.NextPlayer(gameTable.ActionPlayer);
            }

            if (gameTable.ActionPlayer != null && this.IsPlayerAllin(gameTable.ActionPlayer))
            {
                GetNextStageActionPlayer();
            }

            Console.WriteLine("set actionplayer = " + gameTable.ActionPlayer.User.Username + " in GetNextStageActionPlayer()");
        }

        /// <summary>
        /// Pętla przebić graczy
        /// </summary>
        public void StageLoop()
        {
            var gameTable = Game.GameTableModel;

            Console.WriteLine("StageLoop()");

            if (Process())
            {
                RunActionTimer();
            }
        }

        /// <summary>
        /// Sprawdza czy gracz wszedł "allin"
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        private bool IsPlayerAllin(PlayerModel player)
        {
            return (player.Stack == 0);
        }


        /// <summary>
        /// Rozpoczęto proces all-in
        /// </summary>
        public event Action OnAllInProcessStartedEvent;
        public void OnAllInProcessStarted()
        {
            if (OnAllInProcessStartedEvent != null)
                OnAllInProcessStartedEvent();
        }

        /// <summary>
        /// Sprawdza czy nie zachodzi proces w którym każdy z graczy wszedł all in lub pozostał jeden gracz ze stackiem
        /// gdy reszta weszła allin
        /// </summary>
        /// <returns></returns>
        private bool IsPlayersStartedAllInShowdown()
        {
            var gameTable = Game.GameTableModel;
            //Sprawdzamy czy nie ma sytuacji w której pozostali gracze się all-inują
            //tj. czy nie ma dwóch lub więcej gracz all-in oraz jednego lub zero, z jakimkolwiek stackiem
            //W wypadku gdy pozostał jeden gracz ze stakiem a reszta się allinuje nie pozostaje nic innego jak dokończyć rozdanie
            var allInActionData = GetInGameBetActionList().
             GroupBy(p => p.Player).
             Select(g => new
             {
                 player = g.First(),
                 sum = g.Sum(s => s.Bet),
                 isAllIn = IsPlayerAllin(g.First().Player)
             }).ToList();

            if (
                //Sprawdzamy czy aktualny gracz to gracz wchodzacy all-in
                (
                //Sprawdzamy czy jest sytuacja w ktorej pozostla jeden gracz nie wchodzacy all-in gdy reszta wchodzi all in
                    allInActionData.Count(p => p.isAllIn) >= 1 &&
                    (
                    allInActionData.Count(p => !p.isAllIn) == 0) || 
                    (
                        allInActionData.Count(p => !p.isAllIn) == 1 &&
                        //i czy gracz nie wchodzacy jako all-in dorownal kwocie najwiekszego all-in na stole
                        allInActionData.Where(p => !p.isAllIn).Select(p => p.sum).FirstOrDefault() >=
                        allInActionData.Where(p => p.isAllIn).OrderByDescending(p => p.sum).Select(p => p.sum).FirstOrDefault()
                    ) &&
                //Proces ALLIN rozpoczyna się wyłącznie gdy mamy więcej niż dwóch graczy w grze
                Game.GameTableModel.PlayerHavingPlayStatus().Count() >= 2
                )
            )
            {
                if (IsAllInProcessStarted == false)
                {
                    IsAllInProcessStarted = true;
                    OnAllInProcessStarted();
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Uruchamia licznik czasowy w którym gracz może podjać decyzję
        /// </summary>
        public void RunActionTimer()
        {
            var gameTable = Game.GameTableModel;
            Console.WriteLine("RunActionTimer()");

            //Gracz poza grą, wykonujemy domyślną akcję
            if (Game.GameTableModel.ActionPlayer.Status.HasFlag(PlayerModel.PlayerStatus.DONTPLAY))
            {
                ActionPlayerNoAction(null, null);
                return;
            }

            //Gracz w tym momencie teoretycznie musi wykonać akcję
            //Jednak inicjujemy także moduł bota gdy wymagany
            if (gameTable.ActionPlayer.User.Username.Contains("Bot"))
            {
                Task.Factory.StartNew(() =>
                {
                    var bot = new BotSystem();
                    bot.Initial(Game);
                    bot.ActionFor(gameTable.ActionTimer);
                });
            }

            ActionPlayerTimer = new System.Timers.Timer(gameTable.ActionTimer);
            ActionPlayerTimer.Elapsed += ActionPlayerNoAction;
            ActionPlayerTimer.AutoReset = false;
            ActionPlayerTimer.Enabled = true;

            //Wysylamy wiadomosc do wszystkich ze gracz otrzymal taka akcje, ogranicza sie to tylko do akcji
            //BetOfferAction, reszta akcji pozostaje tylko do konkretnych graczy 
            var betOfferAction = this.GetBetOfferAction();

            foreach (UserModel user in gameTable.WatchingList)
            {
                if (user.IsOnline())
                {
                    Task.Factory.StartNew(() =>
                    {
                        user.GetClient().OnGameActionOffer(gameTable, betOfferAction);
                    });
                }
            }
        }

        /// <summary>
        /// Sprawdza sume ktora uzytkownik wprowadzil do akcji
        /// </summary>
        /// <param name="action"></param>
        /// <param name="actionValue"></param>
        /// <returns></returns>
        private decimal ParseBetActionValue(Enums.ActionPokerType action, decimal actionValue)
        {
            var table = Game.GameTableModel;

            //Sprawdzenie obliczamy ręcznie
            if (action == Enums.ActionPokerType.Call)
            {
                actionValue = GetCallValue(table.ActionPlayer);
            }
            else if (action == Enums.ActionPokerType.Raise)
            {
                //Sprawdzamy czy to allin
                var isPlayerAllIn = (this.Game.GameTableModel.ActionPlayer.Stack - actionValue <= 0);

                if (!isPlayerAllIn)
                {
                    //Raise moze byc co najmniej rowny ostatniemu przebiciu (lub big blind) x2
                    BetOfferAction betOffer = GetBetOfferAction();

                    if (actionValue < betOffer.MinBet)
                        actionValue = betOffer.MinBet;

                    if (actionValue > betOffer.MaxBet)
                        actionValue = betOffer.MaxBet;

                    if (actionValue % betOffer.BetTick != 0)
                    {
                        actionValue = betOffer.MinBet;
                    }
                }
            }

            return actionValue;
        }

        /// <summary>
        /// Pobiera ofertę akcji przebicia dla użytkownika według typu limitu stołu gry
        /// </summary>
        /// <returns></returns>
        private BetOfferAction GetBetOfferAction()
        {
            decimal minBet;
            decimal maxBet;
            decimal betTick;

            switch (this.Game.GameTableModel.Limit)
            {
                case Enums.LimitType.Fixed:
                    minBet = Game.GameTableModel.Blind * 2;
                    maxBet = minBet * 2;
                    betTick = minBet;
                    break;
                case Enums.LimitType.PotLimit:
                    minBet = Game.GameTableModel.GetStageBet(Game.GameTableModel.Stage);
                    if (minBet == 0.0m)
                        minBet = Game.GameTableModel.Blind * 2;
                    maxBet = Game.GameTableModel.TablePot + minBet + minBet;
                    betTick = Game.GameTableModel.Blind;
                    break;
                default:
                case Enums.LimitType.NoLimit:
                    minBet = Game.GameTableModel.GetStageBet(Game.GameTableModel.Stage);
                    if (minBet == 0.0m)
                        minBet = Game.GameTableModel.Blind * 2;
                    maxBet = Game.GameTableModel.ActionPlayer.Stack;
                    betTick = Game.GameTableModel.Blind;
                    break;
            }

            if (maxBet > Game.GameTableModel.ActionPlayer.Stack)
                maxBet = Game.GameTableModel.ActionPlayer.Stack;

            var betOfferAction = new BetOfferAction()
            {
                BetTick = betTick,
                MaxBet = maxBet,
                MinBet = minBet,
                Player = Game.GameTableModel.ActionPlayer,
                Time = Game.GameTableModel.ActionTimer,
                Timestamp = DateTime.Now,
                LastBet = Game.GameTableModel.GetStageBet(Game.GameTableModel.Stage)
            };

            return betOfferAction;
        }

        /// <summary>
        /// Brak akcji użytkownika w określonym czasie skutkuje akcją domyślną
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ActionPlayerNoAction(object sender, ElapsedEventArgs e)
        {
            Console.WriteLine("ActionPlayerNoAction()");
            var actionTable = Game.GameTableModel;

            if (actionTable.ActionPlayer == null) return;

            //Dodajemy NoActionCounter ktory zlicza ile razy gracz nie podjal akcji
            if (!actionTable.ActionPlayer.User.Username.Contains("Bot"))
            {
                actionTable.ActionPlayer.DontPlayCounter++;

                if (!actionTable.ActionPlayer.Status.HasFlag(PlayerModel.PlayerStatus.DONTPLAY))
                    actionTable.ActionPlayer.Status |= PlayerModel.PlayerStatus.DONTPLAY;

                Game.SendAvailableAction(actionTable.ActionPlayer.User);
            }

            //Wykonanie domyslnej akcji
            //jeśli użytkownik stracił połączenie, nadal może wygrać rundę
            //przez sprawdzenie (o ile podbił do sumy na stole w danym etapie gry)
            //lub poczekanie odpowiedniej ilości czasu
            //np: ma dodatkowy czas
            if (
                actionTable.GetStageBet(actionTable.Stage) == actionTable.GetPlayerStageBet(actionTable.ActionPlayer, actionTable.Stage)
            )
            {
                Game.OnPlayerGameAction(actionTable.ActionPlayer.User, Enums.ActionPokerType.Call, 0);
            }
            else
            {
                Game.OnPlayerGameAction(actionTable.ActionPlayer.User, Enums.ActionPokerType.Fold, 0);
            }
        }

    }
}
