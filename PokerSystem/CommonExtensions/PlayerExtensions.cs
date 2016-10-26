using CoinPokerCommonLib;
using CoinPokerCommonLib.Models;
using CoinPokerCommonLib.Models.Game.TournamentOption;
using CoinPokerServer.PokerSystem.GameController.Poker.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoinPokerServer.PokerSystem.CommonExtensions
{
    public static class PlayerExtensions
    {
        public static void JoinToGame(this PlayerModel player, NormalGameModel gameModel)
        {
            var _gameModel = gameModel;

            //Tranzakcja dołączenia do gry
            player.User.GameJoinTransaction(gameModel.Currency, player.Stack);

            //Dołączenie do listy graczy, jesli gracz nie mial rezerwacji i jest to mozliwe
            _gameModel.Table.PlayersList.Add(player);
        }


        public static void JoinToGame(this PlayerModel player, ITournamentGameModel gameModel)
        {
            var _gameModel = gameModel;

            //Tranzakcja dołączenia do gry
            player.User.GameJoinTransaction(gameModel.TournamentModel.EntryCurrency, gameModel.TournamentModel.EntryPayment);

            var tournamentPlayer = new TournamentPlayerModel()
            {
                PlaceID = 0,
                Player = player,
            };

            //Dołączenie do listy graczy, jesli gracz nie mial rezerwacji i jest to mozliwe
            _gameModel.TournamentModel.PlayersList.Add(tournamentPlayer);
        }


        public static void LeaveGame(this PlayerModel player, ITournamentGameModel gameModel)
        {
            var _gameModel = gameModel;

            //Tranzakcja dołączenia do gry
            player.User.GameLeaveTransaction(gameModel.TournamentModel.EntryCurrency, gameModel.TournamentModel.EntryPayment);

            var tournamentPlayer = _gameModel.TournamentModel.PlayersList.FirstOrDefault(p => p.Player.User.ID == player.User.ID);
            _gameModel.TournamentModel.PlayersList.Remove(tournamentPlayer);
        }

        public static void LeaveGame(this PlayerModel player, NormalGameModel gameModel)
        {
            var _gameModel = gameModel;

            //Tranzakcja dołączenia do gry
            player.User.GameLeaveTransaction(gameModel.Currency, player.Stack);

            //Dołączenie do listy graczy, jesli gracz nie mial rezerwacji i jest to mozliwe
            //_player_book != null && 
            _gameModel.Table.PlayersList.Remove(player);

            //Wysłanie informacji o nowym graczu
            Task.Factory.StartNew(() =>
            {
                foreach (UserModel user in _gameModel.Table.WatchingList)
                {
                    //Pobieramy _client dla user
                    var _c = user.GetClient();

                    //Wysylamy wiadomosc o nowym graczu na danym miejscu 
                    //Do wszystkich uzytkownikow obserwujacych stol
                    _c.OnGamePlayerStandup(_gameModel.Table, player);
                }
            });
        }
    }
}
