using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hik.Communication.ScsServices.Service;
using CoinPokerCommonLib;
using Hik.Communication.Scs.Communication.EndPoints.Tcp;
using System.Threading;
using System.Xml;
using CoinPokerServer.Database;
using System.IO;
using System.Net;
using CoinPokerServer.PokerSystem.GameController.Poker.Game;
using System.Runtime.InteropServices;

namespace CoinPokerServer
{
    class Program
    {

        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Serwer: UnityPoker");
            Console.WriteLine("Data kompilacji: " + Helper.RetrieveLinkerTimestamp().ToString());

            //Startujemy interfejs www
            //var listeningOn = "http://*:1337/";
            //var appHost = new AppHost()
            //    .Init()
            //    .Start(listeningOn);

            //Uruchomienie serwera SCS
            var server = ScsServiceBuilder.CreateService(new ScsTcpEndPoint(10048));
                server.AddService<IPokerService, PokerService>(PokerService.Instance);
                server.Start();

            //Startujemy apliakcje konsolową
            Console.SetOut(new PrefixedWriter());
            Console.ReadLine();
        }

    }

}
