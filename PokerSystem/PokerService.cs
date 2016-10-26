using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CoinPokerCommonLib;
using Hik.Communication.ScsServices.Service;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using CryptSharp;
using CoinPokerServer.Database;
using System.Security.Cryptography;
using System.Timers;
using CoinPokerServer.ModelExtensions;
using CoinPokerServer.PokerSystem.GameController.Poker.Game;
using System.Xml.Serialization;
using CoinPokerServer.Collections;
using CoinPokerCommonLib.GameModes;
using CoinPokerServer.PokerSystem.CommonExtensions;
using NHibernate;
using CoinPokerCommonLib.Models;
using CoinPokerCommonLib.Models.Game.TournamentOption;
using System.Net;
using System.Collections.Specialized;
using CoinPokerServer;

namespace CoinPokerServer
{
    public class PokerService : ScsService, IPokerService
    {
        /// <summary>
        /// Zalogowani użytkownicy do aplikacji
        /// Key: Identyfikator połączenia
        /// Value: UserClient
        /// </summary>
        public readonly List<ClientModel> Clients;

        /// <summary>
        /// Obsługa gier
        /// </summary>
        public GameCollection GameList { get; set; }

        /// <summary>
        /// Czy serwer jest dostępny dla użytkowników
        /// </summary>
        private bool ServerOpened = false;

        /// <summary>
        /// This class is used to store informations for a connected client.
        /// </summary>
        private sealed class PokerClient
        {
            /// <summary>
            /// Scs client reference.
            /// </summary>
            public IScsServiceClient Client { get; private set; }

            /// <summary>
            /// Proxy object to call remote methods of chat client.
            /// </summary>
            public IPokerClient ClientProxy { get; private set; }

            /// <summary>
            /// User informations of client.
            /// </summary>
            public UserModel User { get; private set; }

            /// <summary>
            /// Creates a new ChatClient object.
            /// </summary>
            /// <param name="client">Scs client reference</param>
            /// <param name="clientProxy">Proxy object to call remote methods of chat client</param>
            /// <param name="userInfo">User informations of client</param>
            public PokerClient(IScsServiceClient client, IPokerClient clientProxy, UserModel userInfo)
            {
                Client = client;
                ClientProxy = clientProxy;
                User = userInfo;
            }
        }

        #region PokerService instance
        private static PokerService instance;

        public static PokerService Instance
        {
            get
            {
                if (PokerService.instance == null)
                {
                    PokerService.instance = new PokerService();
                    PokerService.instance.InitialPokerService();
                }
                return PokerService.instance;
            }
        }
        #endregion

        /// <summary>
        /// Tworzy nowy obiekt PokerService
        /// </summary>
        private PokerService()
        {
            //Inicjalizacja zmiennych
            Console.WriteLine("PokerService()");

            Clients = new List<ClientModel>();
            GameList = new GameCollection();
        }

        private void InitialPokerService()
        {
            //Połączenie z postgresql
            Task.Factory.StartNew(() =>
            {
                Console.WriteLine("Łączenie z bazą danych...");

                Console.WriteLine("Synchronizacja danych z bazą danych...");
                DatabaseSyncFactory syncFactory = new DatabaseSyncFactory(this);
                syncFactory.Sync();

                var rand = new Random();

                //Dodajemy testowe boty do gier normalnych
                foreach (var game in this.GameList.Get<NormalGameController>().ToList())
                {
                    if (game.Game.GameTableModel.Players > game.Game.GameTableModel.Seats) break;

                    var id = rand.Next(1000, 1100);

                    for (int i = 0; i < 6; i++)
                    {
                        if (game.GameModel.Table.PlayersList.Count() + 1 >= game.GameModel.Table.Seats) break;
                        while (game.GameModel.Table.PlayersList.Any(c => c.User.ID == id))
                        {
                            id = rand.Next(1000, 1100);
                        }

                        game.GameModel.Table.PlayersList.Add(
                            new PlayerModel()
                            {
                                Table = game.GameModel.Table,
                                User = new UserModel()
                                {
                                    ID = id,
                                    Username = "Bot_" + id,
                                    WalletList = new List<WalletModel>(),
                                },
                                Stack = game.GameModel.Maximum,
                                Seat = game.GameModel.Table.FreeSeats().First()
                            }
                        );
                    }
                }


                foreach(var game in this.GameList.Get<TournamentGameController>().ToList())
                {
                    var id = rand.Next(1000, 1100);

                    for (int i = 0; i < 100; i++)
                    {
                        if (game.GameModel.TournamentModel.PlayersList.Count() + 1 >= game.GameModel.TournamentModel.MaxPlayers) break;

                        while (game.GameModel.TournamentModel.PlayersList.Any(c => c.Player.User.ID == id))
                        {
                            id = rand.Next(1000, 1999);
                        }

                        game.GameModel.TournamentModel.PlayersList.Add(
                            new TournamentPlayerModel(){
                                PlaceID = 0,
                                Player = new PlayerModel()
                                {
                                    User = new UserModel()
                                    {
                                        ID = id,
                                        Username = "BotTurniejowy_" + id,
                                        WalletList = new List<WalletModel>(),
                                    },
                                    Stack = game.GameModel.TournamentModel.StartStack,
                                    Seat = PlayerModel.AUTO_SEAT
                                }
                            }
                        );
                    }
                }


            });

            Task.Factory.StartNew(() =>
            {
                //Uruchamiamy timer do aktualizacji statystyk uzytkownika
                System.Timers.Timer statTimer = new System.Timers.Timer(2000);
                statTimer.Elapsed += new ElapsedEventHandler(SendStatInfo);
                statTimer.AutoReset = true;
                statTimer.Enabled = true;
            });

            this.OnProcessStarted();
        }

        /// <summary>
        /// Wysyła statystyki do głównego lobby
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SendStatInfo(object sender, ElapsedEventArgs e)
        {
            StatsModel stat = new StatsModel()
            {
                ServerTime = DateTime.Now.Add(new TimeSpan(0, 0, 0)),
                UsersOnline = Clients.Count(),
                TablesToPlay = GameList.Get().ToList().Count(),
            };

            foreach (ClientModel client in Clients)
            {
                if (client.User.IsOnline())
                {
                    client.ClientProxy.OnGetStatsInfo(stat);
                }
            }
        }

        /// <summary>
        /// Logowanie użytkownika
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        public void DoLogin(string username, string password)
        {
            Console.WriteLine("Logowanie -> " + username);

            if (!ServerOpened)
            {
                throw new ApplicationException("Serwer został chwilowo zamknięty");
            }

            //Sprawdzamy czy użytkownik jest już zalogowany
            var _userLogged = (from c in Clients
                               where c.User.Username == username
                               select c).FirstOrDefault();

            if (_userLogged != null)
            {
                throw new ApplicationException("Użytkownik jest już zalogowany");
            }

            using (var session = DatabaseSession.Open())
            {
                var user = session.QueryOver<UserModel>().
                    Where(u => u.Username == username).SingleOrDefault();

                if (user == null || (user != null && !Helper.CheckPassword(user.Password, password)))
                {
                    throw new ApplicationException("Złe dane do logowania, popraw dane i spróbuj ponownie.");
                }

                //Tworzenie modelu użytkownika
                ClientModel client = new ClientModel(
                    user
                )
                {
                    Client = CurrentClient,
                    ClientProxy = CurrentClient.GetClientProxy<IPokerClient>()
                };

                //Dodajemy uchwyt
                client.Client.Disconnected += ClientDisconnected;

                //Dodawanie do listy zalogowanych klientów
                Clients.Add(client);

                //Wysyłanie informacji do użytkownika o poprawnym zalogowaniu
                client.ClientProxy.OnLoginSuccessfull(CurrentClient.ClientId, (UserModel)user);

                Console.WriteLine("Aktualnie zalogowanych -> " + Clients.Count());

                DailyVirtualBonus(user);
            }
        }

        /// <summary>
        /// Bonus wirtualnych zetonow za zalogowanie sie
        /// </summary>
        /// <param name="user"></param>
        private void DailyVirtualBonus(UserModel user)
        {
            if (user.IsOnline())
            {
                var wallet = user.GetWallet(Enums.CurrencyType.VIRTUAL);

                using (ISession session = DatabaseSession.Open())
                {
                    var transferList = session.QueryOver<WalletModel>().
                        Where(w => w.ID == wallet.ID).
                        SingleOrDefault().
                        TransferList;

                    if (!transferList.Any(c => c.Timestamp.Date == DateTime.Now.Date && c.Flag == "LOGIN_BONUS"))
                    {
                        Task.Factory.StartNew(() =>
                        {
                            System.Timers.Timer timer = new System.Timers.Timer(2000);
                            timer.Elapsed += (o, e) =>
                            {
                                user.Deposit(
                                    Enums.CurrencyType.VIRTUAL,
                                    1000.00m,
                                    "Bonus wirtualny za zalogowanie się!",
                                    "LOGIN_BONUS"
                                );
                            };
                            timer.AutoReset = false;
                            timer.Enabled = true;
                        });
                    }
                }

            }
        }


        /// <summary>
        /// Rejestruje gracza
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public bool DoRegister(UserModel user)
        {
            using (ISession session = DatabaseSession.Open())
            {
                var userExists = session.QueryOver<UserModel>().
                    Where(u => u.Username == user.Username).SingleOrDefault();

                if (userExists == null)
                {
                    using (var transaction = session.BeginTransaction())
                    {
                        string generatedSalt = "VF2aqaOy4fEt";
                        string md5pwd = Helper.GetMd5Hash(generatedSalt + user.Password);

                        string newpasswordfield = "md5" + "$" + generatedSalt + "$" + md5pwd;

                        var newUser = new UserModel()
                        {
                            Username = user.Username,
                            Password = newpasswordfield,
                            Email = user.Email,
                            LastLogin = DateTime.Now,
                            DateJoined = DateTime.Now,
                            IsStaff = false,
                            IsActive = true,
                            IsSuperuser = false,
                            Firstname = "",
                            Lastname = ""
                        };

                        session.Save(newUser);

                        transaction.Commit();
                    }
                    return true;
                }
                else
                {
                    throw new ApplicationException("Nazwa użytkownika jest już zajęta.");
                }
            }
        }

        /// <summary>
        /// Wylogowanie klienta
        /// </summary>
        public void DoLogout()
        {
            ClientLogout(CurrentClient.ClientId);
        }

        /// <summary>
        /// Zmienia avatar użytkownika
        /// </summary>
        /// <param name="newAvatar"></param>
        /// <returns></returns>
        public string DoChangeUserAvatar(byte[] newAvatar)
        {
            var user = ClientModel.GetUser(CurrentClient).User;
            user.SetAvatar(newAvatar);

            using (WebClient client = new WebClient())
            {
                try
                {
                    byte[] response =
                    client.UploadValues("http://www.unitypoker.eu:8000/account/set_avatar/", new NameValueCollection()
                    {
                       { "user_id", user.ID.ToString() },
                       { "avatar", Convert.ToBase64String(newAvatar) },
                       //{ "secret", Convert.ToBase64String(secret) },
                    });

                    string result = System.Text.Encoding.UTF8.GetString(response);

                    //Wysyłamy do użytkownika zmieniajacego avatar informacje ze avatar zostal zmieniony
                    user.GetClient().OnUserAvatarChanged(user);

                    //Wysyłamy informacje do stolow w ktorych gracz gra
                    foreach (TableModel table in user.GetTablePlaying())
                    {
                        foreach(UserModel userwatching in table.WatchingList)
                        {
                            userwatching.GetClient().OnUserAvatarChanged(user);
                        }
                    }

                    return "Poprawnie zmieniono Twój avatar";
                }
                catch (Exception ex)
                {
                    return "Wystąpił błąd podczas zmiany avatara. Możesz wysyłac tylko pliki JPG o rozmiarze nie mniejszym niż 128x128.";
                }
            }

        }

        /// <summary>
        /// Pobiera liste graczy danego stolu
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        public List<PlayerModel> GetTablePlayers(TableModel table)
        {
            var _table = table.FromMemory();
            return _table.PlayersList.ToList();
        }

        /// <summary>
        /// Wysyła akcję gry (Call, Gold, Raise etc) oraz wartość jeśli potrzebna
        /// </summary>
        /// <param name="table"></param>
        /// <param name="action"></param>
        /// <param name="actionValue"></param>
        public void DoGameAction(TableModel table, Enums.ActionPokerType action, decimal actionValue)
        {
            var _table = table.FromMemory();

            Game _game = null;

            switch (table.Type)
            {
                case Enums.TableType.Normal:
                    _game = GameList.Get<NormalGameController>().FirstOrDefault(p => p.Game.GameTableModel.ID == _table.ID).Game;
                    break;
                case Enums.TableType.Tournament:
                    _game = GameList.Get<TournamentGameController>().Select(e => e.GameList).
                        FirstOrDefault(t => t.Any(f => f.GameTableModel.ID == table.ID)).
                        FirstOrDefault(t => t.GameTableModel.ID == table.ID);
                    break;
                default:
                    return;
            }

            if (_game != null)
            {
                var _user = ClientModel.GetUser(CurrentClient);
                _game.OnPlayerGameAction(_user.User, action, actionValue);
            }
        }

        /// <summary>
        /// Wysyła wiaodmość dla stołu
        /// </summary>
        /// <param name="table"></param>
        /// <param name="message"></param>
        public void SendMessageToTable(TableModel table, string message)
        {
            var _table = table.FromMemory();
            var _user = ClientModel.GetUser(CurrentClient);
            _table.SendMessage(_user.User, message);
        }

        /// <summary>
        /// Wykonuje operacje na stole
        /// </summary>
        /// <param name="actionType"></param>
        /// <param name="table"></param>
        public void DoAction(Enums.GameActionType actionType, TableModel table)
        {
            //Pobieramy elementy z pamieci
            var _table = table.FromMemory();
            var _user = ClientModel.GetUser(CurrentClient);
            var _player = _table.PlayersList.FirstOrDefault(p => p.User.ID == _user.User.ID);

            //Wykonywanie akcji
            switch (actionType)
            {
                case Enums.GameActionType.PlayerActivation:
                    if (_player == null) break;

                    if (_table.Type == Enums.TableType.Normal)
                    {
                        if (_player.Status.HasFlag(PlayerModel.PlayerStatus.LEAVED))
                            _player.Status &= ~PlayerModel.PlayerStatus.LEAVED;
                    }

                    if (_player.Status.HasFlag(PlayerModel.PlayerStatus.DONTPLAY))
                        _player.Status &= ~PlayerModel.PlayerStatus.DONTPLAY;

                    _user.ClientProxy.OnGamePlayerUpdate(table, _player);
                    break;
                case Enums.GameActionType.LeaveGame:
                    if (_player == null) break;

                    if (!_player.Status.HasFlag(PlayerModel.PlayerStatus.FOLDED))
                        _player.Status |= PlayerModel.PlayerStatus.FOLDED;

                    if (_table.Type == Enums.TableType.Normal)
                    {
                        if (!_player.Status.HasFlag(PlayerModel.PlayerStatus.LEAVED))
                            _player.Status |= PlayerModel.PlayerStatus.LEAVED;
                    }

                    if (_player.Status.HasFlag(PlayerModel.PlayerStatus.INGAME))
                        _player.Status &= ~PlayerModel.PlayerStatus.INGAME;

                    _user.ClientProxy.OnGamePlayerUpdate(table, _player);
                    break;
                case Enums.GameActionType.Unwatch:
                    _user.User.UnwatchTable(_table);
                    break;
                case Enums.GameActionType.Watch:
                    _user.User.WatchTable(_table);
                    break;
                case Enums.GameActionType.FindAnotherTable:
                    //Szukamy innego stołu tego typu (dotyczy tylko normalnych gier)
                    //Szukamy gier o takich samych blindach
                    var _oldTableModel = this.GameList.Get<NormalGameController>().
                        FirstOrDefault(c=>c.GameModel.Table.ID == _table.ID);

                    if (_oldTableModel == null){
                        throw new ApplicationException("Niepoprawny identyfikator stołu gry.");
                    }

                    var _newTableModelList = this.GameList.Get<NormalGameController>().
                        Where(c => c.GameModel.Game == _oldTableModel.GameModel.Game).
                        Where(c => c.GameModel.Limit == _oldTableModel.GameModel.Limit).
                        Where(c => c.GameModel.Minimum == _oldTableModel.GameModel.Minimum).
                        Where(c => c.GameModel.Table.PlayersList.Count() < c.GameModel.Seats).
                        Where(c => c.GameModel.Table.ID != _oldTableModel.GameModel.Table.ID).
                        Where(c => !c.GameModel.Table.WatchingList.Any(u=>u.ID == _user.User.ID)). //Szukamy stolow ktorych uzytkownik nie ma uruchomionych
                        OrderBy(c => c.GameModel.Table.PlayersList.Count());

                    if (_newTableModelList.Count() == 0)
                    {
                        _user.ClientProxy.OnMessage("Nie można znaleść wolnych stołów tego typu. Spróbuj ponownie później.");
                    }
                    else
                    {
                        //Wybieramy jeden z listy dostepnych
                        var rand = new Random();
                        var _newTableModel = _newTableModelList.ToList()[rand.Next(_newTableModelList.Count())];

                        _user.User.MoveToTable(_oldTableModel.GameModel.Table, _newTableModel.GameModel.Table);
                    }

                    break;
                default:
                    throw new ApplicationException("Nieznana operacja GameActionType");
            }
        }

        /// <summary>
        /// Dołącza do trybu normlanego
        /// </summary>
        /// <param name="table"></param>
        /// <param name="stack"></param>
        public void DoJoinNormalMode(NormalGameModel game, int placeID, decimal stack)
        {
            NormalGameModel gameModel = this.GameList.Get<NormalGameController>().Where(g => g.GameModel.ID == game.ID).
                                         Select(g => g.GameModel).
                                         FirstOrDefault();

            var user = ClientModel.GetUser(CurrentClient);
            var wallet_player = user.User.GetWallet(gameModel.Table.Currency);

            //Sprawdzamy czy użytgkownik ma minimum funduszy do wejścia do gry
            if (wallet_player.Available < gameModel.Minimum && stack > wallet_player.Available)
            {
                throw new ApplicationException("Brak środków na koncie.");
            }
            //Czy uzytkownik siedzi juz przy stole
            if (gameModel.Table.PlayersList.Where(c => c.User.ID == user.User.ID).Select(c => c).FirstOrDefault() != null)
            {
                throw new ApplicationException("Siedzisz już przy tym stole.");
            }

            //Za dużo graczy
            if (gameModel.Table.PlayersList.Count() >= gameModel.Table.Seats)
            {
                throw new ApplicationException("Stół jest pełny.");
            }

            //Wyznacza automatyczne miejsce przy stole
            if (placeID == PlayerModel.AUTO_SEAT)
            {
                placeID = gameModel.Table.FreeSeats().ElementAt(0);
            }

            //Czy numer miejsca jest poprawny
            if (placeID > gameModel.Table.Seats - 1 || placeID < 0)
            {
                throw new ApplicationException("Wybrano niepoprawną lokalizację miejsca.");
            }

            //Tworzymy gracza
            PlayerModel _player = new PlayerModel()
            {
                Table = gameModel.Table,
                User = (UserModel)user,
                Stack = stack,
                TimeBank = gameModel.Table.TimeBankOnStart,
                Seat = placeID
            };

            _player.JoinToGame(gameModel);
        }

        /// <summary>
        /// Dołącza do gry rankingowej
        /// </summary>
        /// <param name="tournament"></param>
        public void DoJoinTournamentMode(ITournamentGameModel game)
        {
            ITournamentGameModel gameModel = this.GameList.Get<TournamentGameController>().Where(g => g.GameModel.TournamentModel.ID == game.TournamentModel.ID).
                                         Select(g => g.GameModel).
                                         FirstOrDefault();

            //Sprawdzamy czy użytkownika stać na wejście do turnieju
            var user = ClientModel.GetUser(CurrentClient).User;

            if (user.GetWallet(gameModel.TournamentModel.EntryCurrency).Available < gameModel.TournamentModel.EntryPayment)
            {
                throw new ApplicationException("Nie posiadasz wystarczającej ilości środków na koncie aby dołączyć do tego turnieju.");
            }

            //Sprawdzamy czy nie osiągnięto maksymalnej ilości graczy
            if (gameModel.TournamentModel.MaxPlayers <= gameModel.TournamentModel.PlayersList.ToList().Count())
            {
                throw new ApplicationException("Nie możesz się zarejestrować w tym turnieju ponieważ maksymalna liczba graczy już została osiągnięta.");
            }

            //Sprawdzamy czy rejestracja nadal jest otwarta
            if (gameModel.TournamentModel.State != Enums.TournamentState.Registration && gameModel.TournamentModel.State != Enums.TournamentState.LateRegistration)
            {
                throw new ApplicationException("Rejestracja do tego turnieju jest zamknięta.");
            }

            //Sprawdzamy czy gracz już uczestniczył w tym turnieju
            if (gameModel.TournamentModel.PlayersList.Any(c => c.Player.User.ID == user.ID))
            {
                throw new ApplicationException("Nie można ponownie zarejestrować się do tego turnieju.");
            }

            PlayerModel _player = new PlayerModel()
            {
                User = (UserModel)user,
                Stack = 1500,
                TimeBank = 0,
                Seat = PlayerModel.AUTO_SEAT
            };

            _player.JoinToGame(gameModel);

            user.GetClient().OnMessage("Zarejestrowano w turnieju " + gameModel.TournamentModel.Name + ", z Twojego konta została potrącona kwota " + gameModel.TournamentModel.EntryPaymentCurrency);
        }


        /// <summary>
        /// Opusczenie turnieju
        /// </summary>
        /// <param name="tournament"></param>
        public void DoLeftTournamentMode(ITournamentGameModel tournament)
        {
            ITournamentGameModel gameModel = this.GameList.Get<TournamentGameController>().Where(g => g.GameModel.TournamentModel.ID == tournament.TournamentModel.ID).
                                         Select(g => g.GameModel).
                                         FirstOrDefault();

            var user = ClientModel.GetUser(CurrentClient).User;

            var canLeftTournament = (gameModel.IsStarted() == false);
            if (canLeftTournament)
            {
                var player = gameModel.TournamentModel.PlayersList.FirstOrDefault(c => c.Player.User.ID == user.ID);
                if (player != null)
                {
                    player.Player.LeaveGame(gameModel);
                }
                else
                {
                    throw new ApplicationException("Nie zarejestrowano takiego użytkownika w turnieju.");
                }

                user.GetClient().OnMessage("Poprawnie wypisano z turnieju. Na Twoje konto została zwrócona kwota wpisowego.");
            }
            else
            {
                throw new ApplicationException("Nie można wypisac się z turnieju po jego starcie.");
            }
        }

        /// <summary>
        /// Klient wylogowuje się
        /// </summary>
        /// <param name="clientId"></param>
        private void ClientLogout(long clientId)
        {
            //Pobieramy klienta
            ClientModel client = PokerService.Instance.Clients.FirstOrDefault(c => c.Client.ClientId == clientId);

            //Usuwamy z listy klientów
            Clients.Remove(client);

            //Usuwamy uchwyt
            client.Client.Disconnected -= ClientDisconnected;
        }

        /// <summary>
        /// Obsługa rozłączenia
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClientDisconnected(object sender, EventArgs e)
        {
            //Pobieramy kleinta
            var client = (IScsServiceClient)sender;

            //Wylogowujemy
            ClientLogout(client.ClientId);
        }

        /// <summary>
        /// Wystartował serwer wraz z wczytaną listą gier
        /// </summary>
        private void OnProcessStarted()
        {
            //Wczytujemy backupy do listy serwerów
            ServerOpened = true;
        }

        /// <summary>
        /// Serwer wyłącza się
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnProcessExit()
        {
            //Zapisujemy modele serwerów
            ServerOpened = false;
            Console.WriteLine("Zamykanie serwera...");
            Thread.Sleep(1000);

            foreach (NormalGameController controller in this.GameList.Get<NormalGameController>())
            {
                if (controller.Game != null)
                    controller.Game.Stop = true;
            }
            Thread.Sleep(3000);
        }

        /// <summary>
        /// Pobiera listę gier normalnych
        /// </summary>
        /// <returns></returns>
        public List<NormalGameModel> GetNormalModeList()
        {
            var list = this.GameList.Get<NormalGameController>().Select(c => c.GameModel).ToList();
            return list;
        }

        /// <summary>
        /// Pobiera listę gier turniejowych
        /// </summary>
        /// <returns></returns>
        public List<ITournamentGameModel> GetTournamentModeList()
        {
            var list = this.GameList.Get<TournamentGameController>().Select(c => c.GameModel).ToList();
            return list;
        }

        /// <summary>
        /// Pobiera reklame lobby
        /// </summary>
        public void GetAdsLobby()
        {
            using (ISession session = DatabaseSession.Open())
            {
                var advList = session.QueryOver<AdvertisingModel>().List().ToList();
                Random r = new Random();
                var adv = advList.ElementAt(r.Next(0, advList.Count() - 1));

                CurrentClient.GetClientProxy<IPokerClient>().OnGetAdvertising(adv);
            }
        }

        /// <summary>
        /// Pobiera portfele gracza
        /// </summary>
        /// <returns></returns>
        public List<WalletModel> GetWalletList()
        {
            var user = ClientModel.GetUser(CurrentClient);
            return user.User.GetWalletList();
        }

        /// <summary>
        /// Pobiera dany portfel wedlug waluty
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public WalletModel GetWallet(Enums.CurrencyType type)
        {
            var user = ClientModel.GetUser(CurrentClient);
            return user.User.GetWallet(type);
        }

        /// <summary>
        /// Pobiera liste operacji gracza
        /// </summary>
        /// <param name="wallet"></param>
        /// <returns></returns>
        public List<TransferModel> GetTransferOperations(WalletModel wallet)
        {
            using (ISession session = DatabaseSession.Open())
            {
                var transferList = session.QueryOver<WalletModel>().
                    Where(w => w.ID == wallet.ID).
                    SingleOrDefault().
                    TransferList;

                foreach (var transfer in transferList)
                {
                    if (transfer.Comment == "")
                    {
                        transfer.Comment = "Wpłata na konto w wysokości " + CurrencyFormat.Get(wallet.Type, transfer.Amount);
                    }
                }

                return transferList.OrderByDescending(d => d.Timestamp).Take(15).ToList();
            }
        }

        /// <summary>
        /// Operacja żądania kwoty pieniężnej, serwer sprawdza płatność etc
        /// </summary>
        /// <param name="type"></param>
        public void DoTransferRequest(Enums.CurrencyType type)
        {
            var loggedUser = ClientModel.GetUser(CurrentClient);

            switch (type)
            {
                case Enums.CurrencyType.VIRTUAL:
                    Task.Factory.StartNew(() =>
                    {
                        System.Timers.Timer timer = new System.Timers.Timer(10000);
                        timer.Elapsed += (o, e) =>
                        {
                            loggedUser.User.Deposit(
                                Enums.CurrencyType.VIRTUAL,
                                100.00m
                            );
                        };
                        timer.AutoReset = false;
                        timer.Enabled = true;
                    });

                    break;
                case Enums.CurrencyType.BTC:
                    break;
            }
        }

        /// <summary>
        /// Pobiera profil użytkownika
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public ProfileModel GetProfileUser(UserModel user)
        {
            //Tworzymy profil
            ProfileModel profil = new ProfileModel()
            {
                User = user,
                PlayingOnTables = user.GetTablePlaying(),
            };

            return profil;
        }


        /// <summary>
        /// Pobiera model gry na podstawie stołu
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        public IGameModel GetGameModelByTable(TableModel table)
        {
            if (table.Type == Enums.TableType.Normal)
            {
                return this.GameList.Get<NormalGameController>().
                    Where(c => c.Game.GameTableModel.ID == table.ID).
                    Select(c => c.GameModel).FirstOrDefault();
            }
            else
            {
                return this.GameList.Get<TournamentGameController>().
                    Where(c => c.GameList.Any(g => g.GameTableModel.ID == table.ID)).
                    Select(c => c.GameModel).
                    FirstOrDefault();
            }
        }

        /// <summary>
        /// Zmiana hasła
        /// </summary>
        /// <param name="oldPassword"></param>
        /// <param name="newPassword"></param>
        /// <returns></returns>
        public bool DoChangePassword(string oldPassword, string newPassword)
        {
            var user = ClientModel.GetUser(CurrentClient);

            if (Helper.CheckPassword(user.User.Password, oldPassword))
            {
                string[] databaseSplitHash = user.User.Password.Split('$');
                string md5pwd = Helper.GetMd5Hash(databaseSplitHash[1] + newPassword);

                string newpasswordfield = "md5" + "$" + databaseSplitHash[1] + "$" + md5pwd;
                user.User.ChangePassword(newpasswordfield);
                return true;
            }
            else
            {
                return false;
            }

        }

        /// <summary>
        /// Pobiera liste gracyz turniejowych wedlug modelu turnieju
        /// </summary>
        /// <param name="tournament"></param>
        /// <returns></returns>
        public List<TournamentPlayerModel> GetTournamentPlayers(ITournamentGameModel tournament)
        {
            var tournamentModel = GameList.Get<TournamentGameController>().ToList().FirstOrDefault(t => t.GameModel.TournamentModel.ID == tournament.TournamentModel.ID);
            if (tournamentModel == null)
            {
                throw new ApplicationException("Nieznany identyfikator turnieju.");
            }
            
            return tournamentModel.GameModel.TournamentModel.PlayersList.ToList();
        }

        /// <summary>
        /// Pobiera liste turniejow w ktorych zarejestrowany jest gracz
        /// </summary>
        /// <returns></returns>
        public List<ITournamentGameModel> RegisteredTournamentList()
        {
            var user = ClientModel.GetUser(CurrentClient);

            var tournamentModelList = GameList.Get<TournamentGameController>().ToList().
                Where(t => t.GameModel.TournamentModel.PlayersList.Any(p => p.Player.User.ID == user.User.ID)).Select(c=>c.GameModel).ToList();

            return tournamentModelList;
        }

        /// <summary>
        /// Pobiera stol na ktorym siedzi gracz
        /// </summary>
        /// <param name="tournament"></param>
        /// <returns></returns>
        public TableModel GetTournametTable(ITournamentGameModel tournament)
        {
            var user = ClientModel.GetUser(CurrentClient);

            return PokerService.Instance.GameList.Get<TournamentGameController>().
                        Select(e => e.GameModel.TournamentModel.TableList).
                        Where(t => t.Any(e => e.PlayersList.Any(f => f.User.ID == user.User.ID))).FirstOrDefault().
                        FirstOrDefault(e => e.PlayersList.Any(f => f.User.ID == user.User.ID));
        }
    }
}
