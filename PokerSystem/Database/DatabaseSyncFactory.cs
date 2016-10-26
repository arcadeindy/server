using CoinPokerCommonLib;
using CoinPokerCommonLib.Models.Game.TournamentOption;
using CoinPokerServer.PokerSystem.GameController.Poker.Game;
using Hik.Communication.ScsServices.Service;
using NHibernate;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;

namespace CoinPokerServer.Database
{
    class DatabaseSyncFactory
    {
        private PokerService pokerServiceInstance;

        public DatabaseSyncFactory(PokerService pokerServiceInstance)
        {
            this.pokerServiceInstance = pokerServiceInstance;
        }

        public void Sync()
        {
            Console.WriteLine("Synchronizacja stołów do gry normalnej");

            //Wyczytwanie stołow normalgamemode z bd
            using (ISession session = DatabaseSession.Open())
            {
                //Gry stołowe
                var _normalgamemodelist = session.QueryOver<NormalGameModel>().List();
                foreach (NormalGameModel gameModel in _normalgamemodelist)
                {
                    var realModel = gameModel.Unproxy(session);

                    NormalGameController gameController = new NormalGameController(realModel);

                    this.pokerServiceInstance.GameList.Get().Add(gameController);
                }

                //Turnieje
                var tournamentSitList = session.QueryOver<SitAndGoTournamentGameModel>().List();
                foreach (SitAndGoTournamentGameModel gameModel in tournamentSitList)
                {
                    var realModel = gameModel.Unproxy(session);
                    realModel.TournamentModel = realModel.TournamentModel.Unproxy(session);

                    TournamentGameController gameController = new TournamentGameController(realModel);

                    this.pokerServiceInstance.GameList.Get().Add(gameController);
                }

                var tournamentNormalList = session.QueryOver<NormalTournamentGameModel>().List();
                foreach (NormalTournamentGameModel gameModel in tournamentNormalList)
                {
                    var realModel = gameModel.Unproxy(session);
                    realModel.TournamentModel = realModel.TournamentModel.Unproxy(session);

                    TournamentGameController gameController = new TournamentGameController(realModel);

                    this.pokerServiceInstance.GameList.Get().Add(gameController);
                }

                foreach (IGameController controller in pokerServiceInstance.GameList.Get())
                {
                    var threadController = new Thread(new ThreadStart(controller.Worker));
                    threadController.IsBackground = true;
                    threadController.Start();
                }

            }
        }
    }
}
