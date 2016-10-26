using CoinPokerCommonLib;
using CoinPokerCommonLib.Models.Action;
using CoinPokerServer.ModelExtensions;
using CoinPokerServer.PokerSystem.GameController.Poker.Game.Stages;
using CoinPokerServer.PokerSystem.GameController.Poker.Game.Types;
using Hik.Communication.ScsServices.Service;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Timers;
using CoinPokerServer.PokerSystem.CommonExtensions;
using CoinPokerCommonLib.Models.OfferAction;
using System.Threading.Tasks;

namespace CoinPokerServer.PokerSystem.GameController.Poker.Game
{
    public class BaseGame
    {
        public TableModel GameTableModel { get; set; }
        public bool IsFinished { get; set; }

        protected IGameType GameTypeHandler { get; set; }
        public StageProcess StageGameProcess { get; set; }

        private List<decimal> TablePotList { get; set; }

        //Możliwość zatrzymania gry
        private bool _stop = false;
        public bool Stop
        {
            get
            {
                return _stop;
            }
            set
            {
                _stop = value;
                if (_stop)
                {
                    GameTableModel.ShowSystemMessage("Gra zatrzymana", "Gra została zatrzymana, w wypadku usunięcia ze stołu wszystkie środki zostaną zwrócone.");
                }
                else
                {
                    GameTableModel.HideSystemMessage();
                }
            }
        }

        public void BaseInitialization()
        {
            //Inicjalizacja zmiennych klasy
            TablePotList = new List<decimal>();

            //Inicjalizacja zmiennych dla stołu
            GameTableModel.ActionHistory = new ObservableCollection<BaseAction>();
            GameTableModel.TableCardList = new List<CardModel>();
            GameTableModel.PlayersList = new ObservableCollection<PlayerModel>();
            GameTableModel.WatchingList = new ObservableCollection<UserModel>();

            this.AfterInitialize();
        }

        public void BaseStart()
        {
            IsFinished = false;

            //Czyścimy historię akcji
            GameTableModel.ActionHistory.Clear();
            GameTableModel.TableCardList.Clear();

            //Nowy identyfikator rozdania
            GameTableModel.ActionHistoryID = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

            //Ustawiamy startowys tage
            GameTableModel.Stage = Enums.Stage.Preflop;
            GameTableModel.TablePot = 0;

            //Aktualizacja
            GameTableModel.Players = GameTableModel.PlayersList.Count();
            GameTableModel.Watching = GameTableModel.WatchingList.Count();

            //Dołączamy do gry osoby które na to czekają (folded i waiting)
            this.JoinWaiting();

            //Wybieramy dealera gry
            this.SelectNewDealer();

            //Czyści stół
            this.ClearTable();
        }

        /// <summary>
        /// Zakończenie rundy/gry
        /// </summary>
        public void BaseFinished()
        {
            TablePotList.Add(GameTableModel.TablePot);
            GameTableModel.AvgPot = TablePotList.Skip(Math.Max(0, TablePotList.Count() - 15)).Take(15).Average();

            this.ClearTable();
            /*
            foreach (PlayerModel player in GameTableModel.PlayersList)
            {
                player.Cards.Clear();
                GameTableModel.ActionHistory.Add(new CardShowupAction()
                {
                    Player = player,
                    CreatedAt = DateTime.Now,
                    Stage = GameTableModel.Stage,
                    Cards = player.Cards
                });
            }*/
        }

        /// <summary>
        /// Wydarzenie akcji gry gracza
        /// </summary>
        public event Action<UserModel, Enums.ActionPokerType, decimal> OnPlayerGameActionEvent;
        public void OnPlayerGameAction(UserModel user, Enums.ActionPokerType action, decimal actionValue)
        {
            if (OnPlayerGameActionEvent != null)
                OnPlayerGameActionEvent(user, action, actionValue);
        }

        /// <summary>
        /// Inicjalizuje handlery dla list i elementów modelu stołu etc.
        /// </summary>
        private void AfterInitialize()
        {
            GameTableModel.ActionHistory.CollectionChanged += new NotifyCollectionChangedEventHandler(
                (object sender, NotifyCollectionChangedEventArgs e) =>
                {
                    if (e.NewItems != null)
                    {
                        foreach (Object item in e.NewItems)
                        {
                            if (item is BetAction)
                            {
                                var betAction = (BetAction)item;
                                //Odejmujemy od salda gracza
                                PlayerModel _player = GameTableModel.PlayersList.FirstOrDefault(p => p.User.ID == betAction.Player.User.ID);

                                if (this.GameTableModel.Currency == Enums.CurrencyType.VIRTUAL)
                                {
                                    betAction.Bet = Math.Round(betAction.Bet, 0);
                                }
                                else
                                {
                                    betAction.Bet = Math.Round(betAction.Bet, 2);
                                }

                                //Sprawdzenie stacka usera, max. kwota == player.stack
                                if (betAction.Bet > _player.Stack)
                                {
                                    betAction.Bet = _player.Stack;
                                }

                                //Dodajemy do póli stołu
                                GameTableModel.TablePot += betAction.Bet;
                                //Odejmujemy saldo
                                _player.Stack -= betAction.Bet;

                                if (betAction.Action == Enums.ActionPokerType.Fold)
                                {
                                    if (_player.Status.HasFlag(PlayerModel.PlayerStatus.INGAME))
                                        _player.Status &= ~PlayerModel.PlayerStatus.INGAME;

                                    _player.Status |= PlayerModel.PlayerStatus.FOLDED;

                                    //Aktualizujemy mozliwosc akcji (powrot do gry)
                                    this.SendAvailableAction(_player.User);
                                }

                                Console.WriteLine("Gracz " + betAction.Player.User.Username + " wykonał " + betAction.Action.ToString() + " kwota " + betAction.Bet);
                            }
                            else if (item is CardTableAction)
                            {
                                var actionEntry = (CardTableAction)item;
                                Console.WriteLine("Nowe karty na stole (" + actionEntry.Cards.Count() + ")");
                            }
                            else if (item is CardShowupAction)
                            {
                                var actionEntry = (CardShowupAction)item;
                                Console.WriteLine("Gracz " + actionEntry.Player.User.Username + " pokazuje karty");
                            }
                            else if (item is CardHideupAction)
                            {
                                var actionEntry = (CardHideupAction)item;
                                Console.WriteLine("Gracz " + actionEntry.Player.User.Username + " chowa karty");
                            }
                            else if (item is CardBacksideAction)
                            {
                                //Wysyłamy do wszystkich przy stole
                                //if (_client != null)
                                //      _client.ClientProxy.OnPlayerShowcards(Game.GameTableModel, player, playerCards);
                                var actionEntry = (CardBacksideAction)item;
                                Console.WriteLine("Gracz " + actionEntry.Player.User.Username + " otrzymuje karty");
                            }
                            else if (item is TablePotAction)
                            {
                                var actionEntry = (TablePotAction)item;
                                PlayerModel _player = GameTableModel.PlayersList.FirstOrDefault(p => p.User.ID == actionEntry.Player.User.ID);
                                _player.Stack += actionEntry.Pot;
                                Console.WriteLine("Gracz otrzymuje kwote: " + actionEntry.Pot);
                            }

                            //Wysyłamy podjęcie akcji przez użytkownika do obserwatorów
                            foreach (UserModel user in GameTableModel.WatchingList.ToList())
                            {
                                //Pobieramy _client dla user
                                var _c = user.GetClient();

                                //Wysylamy wiadomosc do wszystkich o danej akcji
                                if (_c != null)
                                    _c.OnGameActionTrigger(GameTableModel, (BaseAction)item);
                            }
                        }
                    }
                }
            );

            GameTableModel.PlayersList.CollectionChanged += new NotifyCollectionChangedEventHandler(
                (object sender, NotifyCollectionChangedEventArgs e) =>
                {
                    Console.WriteLine("PlayersList.CollectionChanged");

                    foreach (UserModel user in GameTableModel.GetWatchingExceptPlayers())
                    {
                        this.SendAvailableAction(user);
                    }

                    //Wysłanie informacji o nowym graczu
                    if (e.NewItems != null)
                    {
                        foreach (Object item in e.NewItems)
                        {
                            var player = item as PlayerModel;

                            Task.Factory.StartNew(() =>
                            {
                                foreach (UserModel user in GameTableModel.WatchingList)
                                {
                                    user.GetClient().OnGamePlayerSitdown(GameTableModel, player);
                                }
                            });
                        }
                    }
                }
            );

            GameTableModel.WatchingList.CollectionChanged += new NotifyCollectionChangedEventHandler(
                (object sender, NotifyCollectionChangedEventArgs e) =>
                {
                    Console.WriteLine("WatchingList.CollectionChanged");

                    if (e.NewItems != null)
                    {
                        foreach (Object item in e.NewItems)
                        {
                            //Wysłanie obecnego stanu gry dla użytkownika czyli stołu wraz z danymi kart i listy graczy
                            this.DoSendTableHitory(item as UserModel);
                            //Wysyła możliwą akcję
                            this.SendAvailableAction(item as UserModel);

                            //Wysyła karty o ile gracz jest w grze
                            var player = GameTableModel.GetPlayer(item as UserModel);
                            if (player != null)
                            {
                                this.SendPlayerCards(player);
                            }
                        }
                    }
                }
            );
        }


        /// <summary>
        /// Wysyła możliwe akcje do gracza w zaleznosci od styuacji, wykonuje sie
        /// gdy : gracz zaczyna obserwowac stol
        ///       usuwa gracza
        ///       dodaje nowego gracza
        /// </summary>
        public void SendAvailableAction(UserModel user)
        {
            //Wysyłanie oferty akcji w zależności od stanu gry i możliwości
            BaseOfferAction offerAction = new BaseOfferAction();

            //W grze normalnej
            if (this.GameTableModel.Type == Enums.TableType.Normal)
            {
                //Wolne miejsce
                if (this.GameTableModel.PlayersList.ToList().Count() < this.GameTableModel.Seats)
                {
                    offerAction = new SeatOfferAction()
                    {
                        Timestamp = DateTime.Now
                    };
                }
                else
                {
                    offerAction = new FindAnotherTableOfferAction()
                    {
                        Timestamp = DateTime.Now
                    };
                }

                if (this.GameTableModel.PlayersList.ToList().Where(p => p.User.ID == user.ID).Any())
                {
                    offerAction = new BackOfferAction()
                    {
                        Timestamp = DateTime.Now
                    };
                }

            }else if (this.GameTableModel.Type == Enums.TableType.Tournament)
            {
                //Możliwy jedynie powród do gry jeśli to nasz stół i uczestniczymy w turnieju
                if (this.GameTableModel.PlayersList.Where(p => p.User.ID == user.ID).Any())
                {
                    offerAction = new BackOfferAction()
                    {
                        Timestamp = DateTime.Now
                    };
                }
                else
                {
                    offerAction = new HideOfferAction()
                    {
                        Timestamp = DateTime.Now
                    };
                }
            }

            if (user.IsOnline())
            {
                user.GetClient().OnGameActionOffer(this.GameTableModel, offerAction);
            }
        }

        /// <summary>
        /// Wysyła wiadomosc dealaera
        /// </summary>
        /// <param name="message"></param>
        public void SendDealerMessage(string message)
        {
            foreach (UserModel user in GameTableModel.WatchingList.ToList())
            {
                if (user.IsOnline())
                {
                    user.GetClient().OnGameTableUserMessage(this.GameTableModel, null, message);
                }
            }
        }

        /// <summary>
        /// Wysyła informacje dla gracza jakie karty on posiada
        /// </summary>
        public void SendPlayerCards(PlayerModel player)
        {
            //Pobieramy _client dla user
            var _player = GameTableModel.GetPlayer(player.User);

            if (_player.Cards != null)
            {
                CardShowupAction cardActionPlayer = new CardShowupAction()
                {
                    Cards = _player.Cards,
                    Stage = GameTableModel.Stage,
                    CreatedAt = DateTime.Now,
                    Player = _player
                };

                //Wysylamy wiadomosc tylko do niego
                if (player.User.IsOnline())
                {
                    player.User.GetClient().OnGameActionTrigger(GameTableModel, cardActionPlayer);
                }
            }
        }

        /// <summary>
        /// Wpłata dużych i małych ciemnych
        /// </summary>
        protected void BlindPayment()
        {
            BetAction smallBlind = new BetAction()
            {
                Action = Enums.ActionPokerType.SmallBlind,
                Bet = GameTableModel.Blind,
                CreatedAt = DateTime.Now,
                Stage = GameTableModel.Stage,
                Player = NextPlayer(GameTableModel.Dealer)
            };

            GameTableModel.ActionHistory.Add(smallBlind);

            BetAction bigBlind = new BetAction()
            {
                Action = Enums.ActionPokerType.BigBlind,
                Bet = GameTableModel.Blind * 2,
                CreatedAt = DateTime.Now,
                Stage = GameTableModel.Stage,
                Player = NextPlayer(smallBlind.Player)
            };

            GameTableModel.ActionHistory.Add(bigBlind);
        }

        /// <summary>
        /// Czyści stół
        /// </summary>
        protected void ClearTable()
        {
            foreach (UserModel user in GameTableModel.WatchingList.ToList())
            {
                if (user.IsOnline())
                {
                    user.GetClient().OnGameTableClear(GameTableModel);
                }
            }
        }

        /// <summary>
        /// Wysyła dla użytkownika aktualny stan rozgrywki
        /// </summary>
        private void DoSendTableHitory(UserModel user)
        {
            var client = user.FromMemory();
            client.GetClient().OnGameTableHitory(
                this.GameTableModel, this.GameTableModel.ActionHistory
            );
        }

        /// <summary>
        /// Wybiera nowego dealera stołu
        /// </summary>
        protected void SelectNewDealer()
        {
            var _player = GameTableModel.Dealer;
            if (_player == null)
            {
                GameTableModel.Dealer = NextPlayer(GameTableModel.PlayersList.First());
            }
            else
            {
                GameTableModel.Dealer = NextPlayer(_player);
            }
        }

        /// <summary>
        /// Dołącza do gry osoby które na to czekają
        /// </summary>
        protected void JoinWaiting()
        {
            PlayerModel.PlayerStatus playerStatusFilter = PlayerModel.PlayerStatus.WAITING | PlayerModel.PlayerStatus.FOLDED;
                
            foreach (
                PlayerModel player in
                this.GameTableModel.PlayersList.
                //Znajdujemy wymaganych graczy
                Where(b => (b.Status & playerStatusFilter) != 0).
                //Jeśli gracz wyszedł nie aktywujemy go i tak zostanie usuniety lub juz zostal
                Select(c => c).ToList()
            )
            {
                if (player.Status.HasFlag(PlayerModel.PlayerStatus.WAITING))
                    player.Status &= ~PlayerModel.PlayerStatus.WAITING;

                if (player.Status.HasFlag(PlayerModel.PlayerStatus.FOLDED))
                    player.Status &= ~PlayerModel.PlayerStatus.FOLDED;

                //Aktywujemy gracza
                if (!player.Status.HasFlag(PlayerModel.PlayerStatus.INGAME))
                    player.Status |= PlayerModel.PlayerStatus.INGAME;
            }
        }

        /// <summary>
        /// Następny gracz
        /// </summary>
        /// <param name="player"></param>
        /// <param name="InGame"></param>
        /// <returns></returns>
        public PlayerModel NextPlayer(PlayerModel player)
        {
            //Tworzymy listę graczy według numerów które posiadają co usprawni znalezienie następnego gracza
            List<PlayerModel> list = this.GameTableModel.PlayerHavingPlayStatus().OrderBy(p => p.Seat).ToList();
            return list.NextPlayer(player);
        }
    }
}
