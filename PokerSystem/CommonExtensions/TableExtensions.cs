using CoinPokerCommonLib;
using CoinPokerServer.ModelExtensions;
using CoinPokerServer.PokerSystem.GameController.Poker.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoinPokerServer.PokerSystem.CommonExtensions
{
    public static class TableExtensions
    {
        /// <summary>
        /// Pobiera listę wolnych miejsc w postaci identyfikatorów
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        public static IEnumerable<int> FreeSeats(this TableModel table)
        {
            var takenPlaces = table.PlayersList.Select(p => p.Seat).ToArray();
            int[] possiblePlaces = Enumerable.Range(0, table.Seats).ToArray();
            var freePlaces = possiblePlaces.Except(takenPlaces);
            return freePlaces;
        }

        /// <summary>
        /// Lista graczy którzy uczestniczą w grze / filtrowanie przez status
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        public static List<PlayerModel> PlayerHavingPlayStatus(this TableModel table)
        {
            PlayerModel.PlayerStatus playerStatusFilter = PlayerModel.PlayerStatus.INGAME;
            return table.PlayersList.ToList().
                Where(b => (b.Status & playerStatusFilter) != 0).
                ToList();
        }

        /// <summary>
        /// Pobieranie sotłu z pamięci
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        public static TableModel FromMemory(this TableModel table)
        {
            //Pobieramy stol z pamieci
            TableModel _table = null;
            //Sprawdzamy czy to gra normalna czy turniejowa
            switch (table.Type)
            {
                case Enums.TableType.Normal:
                    _table = (from t in PokerService.Instance.GameList.Get<NormalGameController>()
                              where t.GameModel.Table.ID == table.ID
                              select t.GameModel.Table).FirstOrDefault();
                    break;
                case Enums.TableType.Tournament:
                    _table = PokerService.Instance.GameList.Get<TournamentGameController>().
                        Select(e=>e.GameModel.TournamentModel.TableList).
                        Where(t => t.Any(e=>e.ID == table.ID)).FirstOrDefault().
                        FirstOrDefault(e=>e.ID == table.ID);
                    break;
            }
            return _table;
        }

        /// <summary>
        /// Pobieramy liste graczy obserwujacych nieuczestniczacych w grze
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        public static List<UserModel> GetWatchingExceptPlayers(this TableModel table)
        {
            return table.WatchingList.ToList().Except(table.PlayersList.Select(p => p.User).ToList()).ToList();
        }

        // Pobieramy wartość przebić danego gracza w danej rundzie
        public static decimal GetPlayerStageBet(this TableModel table, PlayerModel player, Enums.Stage stage)
        {
            var playerStageBet = table.ActionHistory.ToList().OfType<BetAction>().Where(s => s.Stage == stage).
                                 Where(p => p.Player.User.ID == player.User.ID).
                                 Sum(s => s.Bet);
            return playerStageBet;
        }

        // Pobieramy wartość maksymalnego przebicia gracza w danej rundzie
        public static decimal GetStageBet(this TableModel table, Enums.Stage stage)
        {
            var stageBid = table.ActionHistory.ToList().OfType<BetAction>().Where(s => s.Stage == stage).
                                  GroupBy(p => p.Player).
                                  Select(group => group.Sum(p => p.Bet)).OrderByDescending(p => p).FirstOrDefault();
            return stageBid;
        }
        
        /// <summary>
        /// Pobiera gracza ze stołu
        /// </summary>
        /// <param name="table"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public static PlayerModel GetPlayer(this TableModel table, UserModel user)
        {
            return table.PlayersList.ToList().FirstOrDefault(p => p.User.ID == user.ID);
        }

        /// <summary>
        /// Wysyłanie wiadomości do stołu
        /// </summary>
        /// <param name="table"></param>
        /// <param name="user"></param>
        /// <param name="message"></param>
        public static void SendMessage(this TableModel table, UserModel user, string message)
        {
            var _table = table.FromMemory();
            var _user = user.FromMemory();

            Task.Factory.StartNew(() =>
            {
                foreach (var _userWatching in _table.WatchingList.ToList())
                {
                    _userWatching.GetClient().OnGameTableUserMessage(_table, _user, message);
                }
            });
        }

        /// <summary>
        /// Pokazanie wiadomości systemowej na określony czas
        /// </summary>
        /// <param name="table"></param>
        /// <param name="title"></param>
        /// <param name="message"></param>
        /// <param name="ms"></param>
        public static void ShowSystemMessageTimer(this TableModel table, string title, string message, int ms)
        {

        }

        /// <summary>
        /// Pokazanie wiadomości systemowej
        /// </summary>
        /// <param name="table"></param>
        /// <param name="title"></param>
        /// <param name="message"></param>
        public static void ShowSystemMessage(this TableModel table, string title, string message)
        {
            var _table = table.FromMemory();
            var _message = new TableMessageModel()
            {
                Message = message,
                Title = title
            };
            _table.Message = _message;

            Task.Factory.StartNew(() =>
            {
                foreach (var _userWatching in _table.WatchingList.ToList())
                {
                    _userWatching.GetClient().OnGameTableSystemMessage(_table, TableMessageModel.Status.SHOW, _message);
                }
            });
        }

        /// <summary>
        /// Ukrycie wiadomości systemowej
        /// </summary>
        /// <param name="table"></param>
        public static void HideSystemMessage(this TableModel table)
        {
            var _table = table.FromMemory();
            _table.Message = null;

            Task.Factory.StartNew(() =>
            {
                foreach (var _userWatching in _table.WatchingList.ToList())
                {
                    _userWatching.GetClient().OnGameTableSystemMessage(_table, TableMessageModel.Status.HIDE, null);
                }
            });
        }
    }
}
