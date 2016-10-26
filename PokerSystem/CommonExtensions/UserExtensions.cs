using CoinPokerCommonLib;
using CoinPokerServer.Database;
using CoinPokerServer.ModelExtensions;
using CoinPokerServer.PokerSystem.GameController.Poker.Game;
using NHibernate;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace CoinPokerServer.PokerSystem.CommonExtensions
{
    public static class UserExtensions
    {
        public static void SetAvatar(this UserModel user, byte[] avatar)
        {
            //Zapisujemy nowy avatar i zmieniamy url na nowszy
            user.Avatar = "_";
        }

        public static bool IsOnline(this UserModel user)
        {
            var client = PokerService.Instance.Clients.FirstOrDefault(u => u.User.ID == user.ID);
            if (client == null)
                return false;
            else
                return true;
        }

        public static void WatchTable(this UserModel user, TableModel table)
        {
            var _table = table.FromMemory();
            if (!_table.WatchingList.Any(u=>u.ID == user.ID))
                _table.WatchingList.Add(user);
        }

        public static void UnwatchTable(this UserModel user, TableModel table)
        {
            var _table = table.FromMemory();
            _table.WatchingList.Remove(user);
        }

        public static void MoveToTable(this UserModel user, TableModel table, TableModel newTable)
        {
            //Wysyłamy pakiet ajko pierwszy inaczej elementy takie jak historia akcji moga dotrzec przed zmiana stolu
            user.GetClient().OnGameTableUserMove(user, table, newTable);

            //Usuwamy ogladanie starego stolu, podczas inicializacji stolu klienta automatycznie jest dodawany obserwator
            user.UnwatchTable(table);
        }

        public static IPokerClient GetClient(this UserModel user)
        {
            var client = PokerService.Instance.Clients.FirstOrDefault(u => u.User.ID == user.ID);
            if (client != null)
                return client.ClientProxy;
            else
                return null;
        }

        public static UserModel FromMemory(this UserModel user)
        {
            var _user = PokerService.Instance.Clients.Select(u => u.User).FirstOrDefault(u => u.ID == user.ID);
            if (_user != null)
                return _user;
            else
                return null;
        }

        public static void Deposit(this UserModel user, Enums.CurrencyType currency, decimal value, string comment = "", string flag = "")
        {
            using (var session = DatabaseSession.Open())
            {
                var wallet = session.QueryOver<WalletModel>().
                    Where(u => u.User.ID == user.ID).
                    Where(u => u.Type == currency).
                    SingleOrDefault();

                if (wallet == null) return;

                using (var transaction = session.BeginTransaction())
                {
                    TransferModel transfer = new TransferModel()
                    {
                        Wallet = wallet,
                        Amount = value,
                        Timestamp = DateTime.Now.ToUniversalTime(),
                        Type = Enums.TransferType.DEPOSIT,
                        Comment = comment,
                        Flag = flag,
                        Currency = wallet.Type,
                        WalletAmountBefore = wallet.Available
                    };

                    wallet.Available += value;

                    session.Save(transfer);
                    session.Update(wallet);

                    transaction.Commit();

                    if (user.IsOnline())
                    {
                        var client = user.GetClient();

                        if (transfer.Comment == "")
                        {
                            transfer.Comment = "Wpłata na konto w wysokości " + CurrencyFormat.Get(wallet.Type, transfer.Amount);
                        }

                        client.OnDepositInfo(transfer);
                    }
                }
            }
        }

        /// <summary>
        /// Pobiera kwote na poczet gry
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool GameJoinTransaction(this UserModel user, Enums.CurrencyType currency, decimal value)
        {
            var wallet = user.GetWallet(currency);
            using (ISession session = DatabaseSession.Open())
            {
                if (wallet.Available >= value)
                {
                    using (var transaction = session.BeginTransaction())
                    {
                        wallet.Available -= value;
                        session.Update(wallet);
                        transaction.Commit();
                        return true;
                    }
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Oddaje kwote dla gracza, w wypadku turniejow jest to depozyt
        /// </summary>
        /// <param name="user"></param>
        /// <param name="currency"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool GameLeaveTransaction(this UserModel user, Enums.CurrencyType currency, decimal value)
        {
            var wallet = user.GetWallet(currency);
            using (ISession session = DatabaseSession.Open())
            {
                using (var transaction = session.BeginTransaction())
                {
                    wallet.Available += value;
                    session.Update(wallet);
                    transaction.Commit();
                    return true;
                }
            }
        }


        public static bool ChangePassword(this UserModel user, string newHash)
        {
            using (ISession session = DatabaseSession.Open())
            {
                var userDB = session.QueryOver<UserModel>().
                       Where(u => u.ID == user.ID).SingleOrDefault();

                if (userDB != null)
                {
                    using (var transaction = session.BeginTransaction())
                    {
                        userDB.Password = newHash;

                        session.Update(userDB);
                        transaction.Commit();
                    }
                }
                return true;
            }
        }

        public static List<TableModel> GetTablePlaying(this UserModel user)
        {
            //Dane gdzie gracz gra
            List<TableModel> playingOnTables = new List<TableModel>();

            if (user.IsOnline())
            {
                List<TableModel> normalTablesPlaying = PokerService.Instance.GameList.Get<NormalGameController>().
                    Where(g => (
                        g.GameModel.Table.PlayersList.FirstOrDefault(
                            p => p.User.ID == user.ID) != null
                        )
                    ).
                    Select(t => t.Game.GameTableModel).ToList();

                playingOnTables = playingOnTables.Concat(normalTablesPlaying).ToList();
            }

            return playingOnTables;
        }

        public static List<WalletModel> GetWalletList(this UserModel user)
        {
            List<WalletModel> walletList = new List<WalletModel>();

            using (ISession session = DatabaseSession.Open())
            {
                walletList = session.QueryOver<WalletModel>().
                    Where(u => u.User.ID == user.ID).List().ToList();

                //Dodajemy automatycznie wirtualną walutę gdy jej nie ma
                if (walletList != null && walletList.Where(w=>w.Type == Enums.CurrencyType.VIRTUAL).Count() == 0)
                {
                    using (var transaction = session.BeginTransaction())
                    {
                        WalletModel wallet = new WalletModel()
                        {
                            Type = Enums.CurrencyType.VIRTUAL,
                            User = user,
                            TransferList = new List<TransferModel>()
                        };
                        walletList.Add(wallet);

                        session.Save(wallet);

                        transaction.Commit();
                    }
                }

                //Modyfikujemy kwoty "InGame"
                foreach (var wallet in walletList)
                {
                    var tableList = wallet.User.GetTablePlaying().Where(c => c.Currency == wallet.Type);
                    wallet.InGame = tableList.Sum(
                        c => c.PlayersList.Where(
                        d => d.User.ID == wallet.User.ID
                        ).FirstOrDefault().Stack
                   );
                }

                return walletList;
            }
        }

        public static WalletModel GetWallet(this UserModel user, Enums.CurrencyType currency)
        {
            return user.GetWalletList().Select(c => c).Where(c => c.Type == currency).FirstOrDefault();
        }
    }
}
