using CoinPokerCommonLib;
using CoinPokerCommonLib.Models.Game.TournamentOption;
using CoinPokerServer.ModelExtensions;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading;
using CoinPokerServer.PokerSystem.CommonExtensions;
using System.Threading.Tasks;
using CoinPokerCommonLib.Models;
using CoinPokerCommonLib.Models.Game.Tournament.WinningPotModel;

namespace CoinPokerServer.PokerSystem.GameController.Poker.Game
{
    public class TournamentGameController : IGameController
    {
        /// <summary>
        /// Zawiera liste stołów dla turnieju
        /// </summary>
        public List<Game> GameList { get; set; }

        /// <summary>
        /// Posiada główne informacje na temat turnieju
        /// </summary>
        public ITournamentGameModel GameModel { get; set; }

        private bool Initialized { get; set; }

        /// <summary>
        /// Pot model
        /// </summary>
        private StaticWinningPotModel PotModel { get; set; }

        public enum TournamentPlayingType
        {
            Default,
            HandForHand,
        }

        private TournamentPlayingType Type { get; set; }

        public TournamentGameController(ITournamentGameModel gameModel)
        {
            this.GameList = new List<Game>();
            this.Type = TournamentPlayingType.Default;
            this.PotModel = new StaticWinningPotModel(gameModel.TournamentModel);
            this.SetGameModel(gameModel);
        }

        public void SetGameModel(ITournamentGameModel gameModel)
        {
            //Tworzy turniej 
            this.GameModel = gameModel;

            //Eventy na listy i akcje gry
            this.GameModel.TournamentModel.PlayersList.CollectionChanged += (object sender, NotifyCollectionChangedEventArgs e) =>
            {
                this.GameModel.TournamentModel.Registered = this.GameModel.TournamentModel.PlayersList.Count();

                foreach (ClientModel user in PokerService.Instance.Clients)
                {
                    user.ClientProxy.OnTournamentGameModelUpdate(this.GameModel);
                }

                if (e.NewItems != null && this.GameModel.IsStarted())
                {
                    //Dołączamy użytkownika do jakiegoś stołu jesli gra sie rozpoczela
                    foreach (Object item in e.NewItems)
                    {
                        var newPlayer = item as TournamentPlayerModel;
                        TournamentPlacemenet(newPlayer);
                    }
                }
            };
        }

        /// <summary>
        /// Modyfikuje status turnieju
        /// </summary>
        public void TournamentStatus()
        {
            if (GameModel is SitAndGoTournamentGameModel)
            {
                if (GameModel.IsStarted())
                {
                    GameModel.TournamentModel.State = Enums.TournamentState.Running;
                }
                else
                {
                    GameModel.TournamentModel.State = Enums.TournamentState.Registration;
                }
            }
            else if (GameModel is NormalTournamentGameModel)
            {
                var model = GameModel as NormalTournamentGameModel;

                if ((model.StartAt - DateTime.Now) >= model.Registration)
                {
                    GameModel.TournamentModel.State = Enums.TournamentState.Registration;
                }
                else if ((DateTime.Now - model.StartAt) <= model.LateReg)
                {
                    GameModel.TournamentModel.State = Enums.TournamentState.LateRegistration;
                }
                else
                {
                    GameModel.TournamentModel.State = Enums.TournamentState.Running;
                }
            }

            //Jeśli turniej się zakończy
            if (GameModel.TournamentModel.PlayersList.Where(e => e.Player.Stack != 0).Count() == 1)
            {
                GameModel.TournamentModel.State = Enums.TournamentState.Completed;
            }
        }

        /// <summary>
        /// Funkcja otryzmuje gracza ktorego musi umiescic na odpowiednim stole dziala tylko gdy gracz stolu NIE POSIADA
        /// </summary>
        public void TournamentPlacemenet(TournamentPlayerModel player)
        {
            //Sprawdzamy czy gracz jest umieszczony juz na stole, jesli jest to nie dodajemy go nigdzie
            if (GameList.Select(c => c.GameTableModel).Any(c => c.PlayersList.Any(p => p.User.ID == player.Player.User.ID))) return;

            //Sprawdzamy czy gdzies jest wolne miejsce, jesli nie tworzymy nowy stol
            var playerTable = GameList.Select(c => c.GameTableModel).FirstOrDefault(c => c.PlayersList.Count() < c.Seats);

            if (playerTable == null)
            {
                //Tworzy nowy stol na liscie stolow do gry
                playerTable = new TableModel()
                {
                    Blind = 2m,
                    Currency = Enums.CurrencyType.VIRTUAL,
                    Game = Enums.PokerGameType.Holdem,
                    Limit = Enums.LimitType.NoLimit,
                    Name = GameModel.TournamentModel.Name + " #" + GameModel.TournamentModel.TableList.Count(),
                    Type = Enums.TableType.Tournament,
                    Seats = 9,
                    ActionTimer = 5000,
                };

                this.CreateGame(playerTable);

                GameModel.TournamentModel.TableList.Add(playerTable);
            }

            //Zajmujemy miejsce
            player.Player.Table = playerTable;
            player.Player.Seat = playerTable.FreeSeats().First();
            playerTable.PlayersList.Add(player.Player);

            //Otwieramy nowe okno z gra
            if (player.Player.User.IsOnline())
                player.Player.User.GetClient().OnTableOpenWindow(playerTable);
        }


        /// <summary>
        /// Funkcja kontroluje tok gry /start/koniec
        /// </summary>
        private void GameController()
        {
            while (true)
            {
                foreach (Game game in this.GameList)
                {
                    if (game.IsFinished && game.GameTableModel.PlayersList.Count() > 1)
                    {
                        var thread = new Thread(game.Start);
                        thread.Start();
                    }
                }
                Thread.Sleep(1000);
            }
        }

        /// <summary>
        /// Tworzy nowy stol do gry i pilnuje rozgrywki, zamyka watek gdy liczba graczy wyniesie 0 na danym stole
        /// </summary>
        private void CreateGame(TableModel table)
        {
            //Nowy wątek gry
            var Game = new Game();
            Game.Initialize(table);

            Game.OnBeforeGameStartEvent += (game) =>
            {
                RemovePlayers(game);
            };

            Game.OnGameFinishedEvent += (game) =>
            {
                UpdateTournamentModel();
            };

            GameList.Add(Game);
        }

        /// <summary>
        /// Usuwa graczy ktorzy ukonczyli turniej
        /// </summary>
        /// <param name="game"></param>
        private void RemovePlayers(Game game)
        {
            var _leaved_players = game.GameTableModel.PlayersList.ToList().
                Where(c => c.Stack == 0.0m).
                Select(c => c).ToList();

            foreach (PlayerModel _leaved_player in _leaved_players)
            {
                //Usuwamy gracza ze stolu
                game.GameTableModel.PlayersList.Remove(_leaved_player);

                Task.Factory.StartNew(() =>
                {
                    foreach (UserModel user in game.GameTableModel.WatchingList)
                    {
                        //Pobieramy _client dla user
                        var _c = user.GetClient();

                        //Wysylamy wiadomosc o nowym graczu na danym miejscu 
                        //Do wszystkich uzytkownikow obserwujacych stol
                        _c.OnGamePlayerStandup(game.GameTableModel, _leaved_player);

                        //Nagradzamy jesli tego wymaga
                        this.PlayerFinishedTournament(_leaved_player);
                    }
                });
            }
        }

        private void PlayerFinishedTournament(PlayerModel player)
        {
            //Miejsce gracza
            var PlayerCounter = GameModel.TournamentModel.PlayersList.Count();
            var NonPlayingCounter = GameModel.TournamentModel.PlayersList.Where(e => e.Player.Stack == 0).Count();

            //Miejsce gracza wedlug przegranych
            var PlaceID = PlayerCounter - NonPlayingCounter;

            //Kwota wygranej o ile jest jakakolwiek
            decimal WinPot = 0;

            //Sprawdzamy aktualna liste wygranych
            var ListPotWin = PotModel.PrizeCalc();

            if (PlaceID <= ListPotWin.Count())
            {
                WinPot = ListPotWin.First(e=>e.PlaceID == PlaceID).Prize;
            }

            if (player.User.IsOnline())
            {
                string message = "Zakońyłeś turniej na " + PlaceID + " miejscu. ";

                if (WinPot != 0)
                {
                    message += "Twoja wygrana w turnieju wynosi " + CurrencyFormat.Get(GameModel.TournamentModel.EntryCurrency, WinPot);
                }

                player.User.GetClient().OnMessage(message);
            }
        }

        public void TournamentInitialize()
        {
            Initialized = true;

            if (GameModel.TournamentModel.PlayersList.Count() <= 1)
            {
                //Turniej nie rozpocznie się nie ma wystarczajacej ilosci graczy
                GameModel.TournamentModel.State = Enums.TournamentState.Completed;

                //TODO:
                //Zgloszenie dla uzytkownikow ze turniej nie rozpocznie sie (jesli tacy sa)
            }
            else
            {
                //Zajmujemy miejsca przy stolach
                foreach (var player in GameModel.TournamentModel.PlayersList.ToList())
                {
                    TournamentPlacemenet(player);
                }

                //Uruchamiamy tok gry
                //var thread = new Thread(GameController);
                //thread.Start();
                UpdateTableList();

                //Dajemy wiadomosc o tym ze gra sie rozpocznie niedlugo
                foreach (var game in GameList)
                {
                    game.GameTableModel.ShowSystemMessage("Gra turniejowa", "Gra rozpocznie się za 10 sekund. Prosimy o przygotowanie się.");
                }

                System.Timers.Timer timer = new System.Timers.Timer(10000);
                timer.Elapsed += (o, e) =>
                {
                    //Ukrywamy wiadomość
                    foreach (var game in GameList)
                    {
                        game.GameTableModel.HideSystemMessage();
                    }

                    //Startujemy grę
                    var thread = new Thread(GameController);
                    thread.Start();
                };
                timer.AutoReset = false;
                timer.Enabled = true;

            }
        }

        private void UpdateTableList()
        {
            var playerList = GameModel.TournamentModel.PlayersList;
            foreach (var player in playerList)
            {
                if (player.Player.User.IsOnline())
                    player.Player.User.GetClient().OnTournamentTableListUpdate(GameModel, GameModel.TournamentModel.TableList.ToList());
            }
        }

        private void UpdateTournamentModel()
        {
            var playerList = GameModel.TournamentModel.PlayersList;
            foreach (var player in playerList)
            {
                if (player.Player.User.IsOnline())
                    player.Player.User.GetClient().OnTournamentGameModelUpdate(GameModel);
            }
        }

        /// <summary>
        /// Metoda dla wątku obserwującego 
        /// </summary>
        public void Worker()
        {
            while (true)
            {
                if (GameModel.IsStarted() && !Initialized)
                {
                    //Sit and go
                    Console.WriteLine("Turniej się rozpoczął");

                    //Inicjalizacja turnieju
                    if (!Initialized)
                    {
                        TournamentInitialize();
                    }
                }
                else
                {
                    //Rozpoczyna się o określonymc czasie lub liczbie uzytkownikow wiec cos robimy
                    TournamentStatus();
                }

                //Jesli zakonczony to nie ruszamy
                if (GameModel.TournamentModel.State == Enums.TournamentState.Completed)
                {
                    return;
                }
                else
                {
                    Thread.Sleep(1000);
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
