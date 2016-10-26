using CoinPokerCommonLib;
using CoinPokerServer.ModelExtensions;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading;
using CoinPokerServer.PokerSystem.CommonExtensions;
using System.Threading.Tasks;

namespace CoinPokerServer.PokerSystem.GameController.Poker.Game
{
    public class NormalGameController : IGameController
    {
        public Game Game { get; set; }
        public NormalGameModel GameModel { get; set; }

        public NormalGameController(NormalGameModel gameModel)
        {
            this.SetGameModel(gameModel);
        }

        private TableModel InitializeTable()
        {
            return new TableModel()
            {
                Blind = GameModel.Blind,
                Currency = GameModel.Currency,
                Game = GameModel.Game,
                Limit = GameModel.Limit,
                Name = GameModel.Name,
                Type = Enums.TableType.Normal,
                Seats = GameModel.Seats,
                ActionTimer = 5000,
            };
        }

        /// <summary>
        /// Ustawia model gry
        /// </summary>
        /// <param name="gameModel"></param>
        public void SetGameModel(NormalGameModel gameModel)
        {
            this.GameModel = gameModel;
            //Tworzymy stół do gry na podstawie normalgamemodel
            this.GameModel.Table = InitializeTable();

            //Tworzymy obiekt gry
            this.Game = new Game();
            this.Game.Initialize(GameModel.Table);

            //Zmiana graczy
            this.GameModel.Table.PlayersList.CollectionChanged += new NotifyCollectionChangedEventHandler(
                (object sender, NotifyCollectionChangedEventArgs e) =>
                {
                    GameModel.Table.Players = GameModel.Table.PlayersList.Count();

                    foreach (ClientModel user in PokerService.Instance.Clients)
                    {
                        if (GameModel is NormalGameModel)
                            user.ClientProxy.OnNormalGameModelUpdate((NormalGameModel)this.GameModel);
                    }
                });

            //Zmiana obserwatorów gry
            this.GameModel.Table.WatchingList.CollectionChanged += new NotifyCollectionChangedEventHandler(
                (object sender, NotifyCollectionChangedEventArgs e) =>
                {
                    GameModel.Table.Watching = GameModel.Table.WatchingList.Count();

                    foreach (ClientModel user in PokerService.Instance.Clients)
                    {
                        if (GameModel is NormalGameModel)
                            user.ClientProxy.OnNormalGameModelUpdate((NormalGameModel)GameModel);
                    }
                });


            //Przypisano nową rękę do gry
            this.Game.OnAfterBaseStartEvent += (game) =>
            {
                foreach (ClientModel user in PokerService.Instance.Clients)
                {
                    if (GameModel is NormalGameModel)
                    {
                        if (user.User.IsOnline())
                            user.ClientProxy.OnNormalGameModelUpdate((NormalGameModel)GameModel);
                    }
                }
            };

            //Inicjalizujemy wyrzucanie nieaktywnych graczy z gier normalnych
            this.Game.OnBeforeGameStartEvent += (game) =>
            {
                //Zmienia status graczy ktorzy stracili poieniadze na dontplay
                AllInRemove(game);

                //Usuwa osoby ktore wyszly z gry
                RemoveLeavers(game);

                //Aktualizujemy dane stołu dla obserwatorów
                foreach (UserModel user in this.Game.GameTableModel.WatchingList.ToList())
                {
                    var client = PokerService.Instance.Clients.FirstOrDefault(u => u.User.ID == user.ID);
                    client.ClientProxy.OnNormalGameModelUpdate((NormalGameModel)this.GameModel);
                }
            };
        }

        /// <summary>
        /// Usuwa z gry graczy allin, przenosi w tryb nie-gra
        /// </summary>
        protected void AllInRemove(Game game)
        {
            foreach (
                PlayerModel player in
                game.GameTableModel.PlayersList.ToList().
                Where(c => c.Stack == 0).
                Select(c => c).ToList()
            )
            {
                if (!player.User.Username.Contains("Bot"))
                {
                    player.LeaveGame(this.GameModel);
                }
                else
                {
                    player.Stack = GameModel.Maximum;
                }
            }
        }

        /// <summary>
        /// Usuwa graczy którzy wyszli z gry
        /// </summary>
        protected void RemoveLeavers(Game game)
        {
            PlayerModel.PlayerStatus playerStatusFilter = PlayerModel.PlayerStatus.LEAVED;
                
            //Jesli sa gracze ktorzy maja LEAVEGAME status, usuwamy ich ze stolu
            var _leaved_players = game.GameTableModel.PlayersList.ToList().
                Where(b => (b.Status & playerStatusFilter) != 0).
                Select(c => c).ToList();

            foreach (PlayerModel _leaved_player in _leaved_players)
            {
                _leaved_player.LeaveGame(this.GameModel);
            }
        }

        private int GetActivePlayerCounter()
        {
            PlayerModel.PlayerStatus playerStatusFilter = PlayerModel.PlayerStatus.INGAME | PlayerModel.PlayerStatus.WAITING | PlayerModel.PlayerStatus.FOLDED;

            var ActivePlayerCounter = GameModel.Table.PlayersList.ToList().
                Where(b => (b.Status & playerStatusFilter) != 0).
                Count();

            return ActivePlayerCounter;
        }

        private bool CanStartNewGame()
        {
            return (GameModel != null && GetActivePlayerCounter() > 1 &&
                    (
                        (Game != null && Game.IsFinished)
                    ));
        }

        /// <summary>
        /// Metoda dla wątku obserwującego 
        /// </summary>
        public void Worker()
        {
            while (true)
            {
                if (this.Game != null && Game.IsFinished)
                {
                    //Zmienia status graczy ktorzy stracili poieniadze na dontplay
                    AllInRemove(this.Game);
                    //Usuwa osoby ktore wyszly z gry
                    RemoveLeavers(this.Game);
                }

                if (CanStartNewGame() && Game != null && Game.Stop == false)
                {
                    Console.WriteLine("Rozpoczęcie nowej gry");
                    //Rozpoczynamy nową rundę
                    //gdy liczba graczy jest większa niż 1
                    //lub gdy ostatnia gra zakończy się
                    Game.Start();
                }
                else if (Game != null && Game.Stop)
                {
                    //Gra została zatrzymana
                    Game.SendDealerMessage("Gra została zatrzymana, w wypadku restartu wszelkie środki zostaną przywrócone na Twoje konto");

                    Thread.Sleep(10000);
                }
                else
                {
                    Console.WriteLine("Nie mozna rozpocząć gry: ActivePlayerCounter = " + GetActivePlayerCounter());
                    Thread.Sleep(5000);
                }

            }
        }

        /// <summary>
        /// Dodaje hooki które umożliwiają start lub przerwanie gry
        /// </summary>
        public void Initialize()
        {
            throw new NotImplementedException();
        }
    }
}
