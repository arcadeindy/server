using CoinPokerCommonLib;
using CoinPokerCommonLib.Models.Action;
using CoinPokerServer.ModelExtensions;
using CoinPokerServer.PokerSystem.GameController.Poker.Game.GameElement;
using CoinPokerServer.PokerSystem.GameController.Poker.Game.Stages;
using CoinPokerServer.PokerSystem.GameController.Poker.Game.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using CoinPokerServer.PokerSystem.CommonExtensions;

namespace CoinPokerServer.PokerSystem.GameController.Poker.Game
{
    public class Game : BaseGame
    {
        public Game()
        {
            IsFinished = true;
        }

        public void Initialize(TableModel gameTableModel)
        {
            Console.WriteLine("Initialize()");
            GameTableModel = gameTableModel;

            InitializeStage();
            BaseInitialization();
        }

        /// <summary>
        /// Rozpoczyna proces rozdania
        /// </summary>
        public event Action<Game> OnBeforeGameStartEvent;
        public event Action<Game> OnAfterBlindsEvent;
        public event Action<Game> OnAfterBaseStartEvent;
        public event Action<Game> OnGameFinishedEvent;
        public void Start()
        {
            Console.WriteLine("Game.Start()");
            Console.WriteLine("Rozpoczęcie gry " + GameTableModel.Name);

            IsFinished = false;

            this.SendDealerMessage("Rozpoczynam nowe rozdanie.");

            //W zależności od trybu
            //sprawdzamy graczy etc.
            if (OnBeforeGameStartEvent != null)
                OnBeforeGameStartEvent(this);

            BaseStart();

            if (OnAfterBaseStartEvent != null)
                OnAfterBaseStartEvent(this);

            //Rozpoczynamy rozdanie
            //Element tworzony jest w tym miejscu aby turniej miał możliwość zmiany typu gry 
            //kiedy mu się zechce, dzięki czemu można tworzyć mieszne turnieje
            switch (this.GameTableModel.Game)
            {
                case Enums.PokerGameType.Omaha:
                    GameTypeHandler = new OmahaGameType();
                    break;

                case Enums.PokerGameType.Holdem:
                default:
                    GameTypeHandler = new HoldemGameType();
                    break;
            }

            GameTypeHandler.Initialize(this);
            GameTypeHandler.OnGameStart();

            //Wpłata ciemnych która jest zawsze
            BlindPayment();
            this.SendDealerMessage("Gracze wpłacają ciemne.");

            //Rozpoczynamy etap przebijania
            if (OnAfterBlindsEvent != null)
                OnAfterBlindsEvent(this);

            StageGameProcess.Initialize();

            StageGameProcess.StageLoop();
        }

        private void InitializeStage()
        {
            Console.WriteLine("InitializeStage()");

            StageGameProcess = new StageProcess(this);
            StageGameProcess.Initialize();

            StageGameProcess.OnStageChangeEvent += (stage) =>
            {
                    Console.WriteLine("OnStageChangeEvent()");
                    Thread.Sleep(100);
                    List<CardModel> cards = new List<CardModel>();
                    switch (stage)
                    {
                        case Enums.Stage.Flop:
                            this.SendDealerMessage("Rozkładam flopa.");
                            cards.Add(Helper.Pop<CardModel>(GameTypeHandler.CardList));
                            cards.Add(Helper.Pop<CardModel>(GameTypeHandler.CardList));
                            cards.Add(Helper.Pop<CardModel>(GameTypeHandler.CardList));
                            break;
                        case Enums.Stage.Turn:
                            this.SendDealerMessage("Rozkładam turn.");
                            cards.Add(Helper.Pop<CardModel>(GameTypeHandler.CardList));
                            break;
                        case Enums.Stage.River:
                            this.SendDealerMessage("Rozkładam river.");
                            cards.Add(Helper.Pop<CardModel>(GameTypeHandler.CardList));
                            break;
                    }

                    Console.WriteLine("Rozdanie kart na stole = " + stage.ToString());

                    //Zapisuje zebrane karty do pamieci
                    GameTableModel.TableCardList = GameTableModel.TableCardList.Concat(cards).ToList();

                    //Przekazujemy karty publice
                    CardTableAction cardAction = new CardTableAction()
                    {
                        CreatedAt = DateTime.Now,
                        Cards = cards,
                        Stage = GameTableModel.Stage
                    };

                    GameTableModel.ActionHistory.Add(cardAction);
                    Thread.Sleep(1500);
            };
            StageGameProcess.OnStageProcessFinishEvent += () =>
            {
                Console.WriteLine("OnStageProcessFinishEvent");
                if (GameTableModel.PlayerHavingPlayStatus().Count == 1)
                {
                    //Wygrywa jeden gracz
                    var playerWinner = GameTableModel.PlayerHavingPlayStatus().FirstOrDefault();
                    playerWinner.Stack += GameTableModel.TablePot;
                    var message = "Gracz " + playerWinner.User.Username + " wygrywa główną pulę " + CurrencyFormat.Get(GameTableModel.Currency, GameTableModel.TablePot) + " ponieważ reszta graczy spasowała karty.";
                    Console.WriteLine(message);

                    this.SendDealerMessage(message);

                    GameTableModel.ShowSystemMessage("Wygrywa " + playerWinner.User.Username, message);
                    Thread.Sleep(2000);
                    GameTableModel.HideSystemMessage();
                }
                else
                {
                    //Showdown
                    StageShowdown stageShowdown = new StageShowdown(this);
                    stageShowdown.OnShowdownWinnerEvent += (evaluatorItem) =>
                    {
                        Console.WriteLine("points: " + evaluatorItem.Points + " (kicker points: " + evaluatorItem.KickerPoints + ")");

                    };
                    stageShowdown.ShowdownStart();
                    stageShowdown.ShowdownPotDistribution();
                }

                Thread.Sleep(100);
                GameTableModel.ShowSystemMessage("Zakończenie rozdania", "Nowe rozdanie zostanie rozpoczęte za 4 sekundy");
                Console.WriteLine("Zakończenie gry na stole " + this.GameTableModel.Name);
                Thread.Sleep(3600);
                GameTableModel.HideSystemMessage();

                BaseFinished();

                if (OnGameFinishedEvent != null)
                    OnGameFinishedEvent(this);
                
                IsFinished = true;
            };
        }

    }
}
