using CoinPokerCommonLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;

namespace CoinPokerServer
{
    public class ServerBackup
    {
        private static ServerBackup instance = null;

        public static ServerBackup Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new ServerBackup();
                }
                return instance;
            }
        }

        public void Save()
        {
            Console.Write("Zapisuje ./Backup/NormalModeList.dat");

            try
            {
                Stream stream = File.Open("./Backup/NormalModeList.dat", FileMode.Create);
                BinaryFormatter bFormatter = new BinaryFormatter();
                //bFormatter.Serialize(stream, PokerService.Instance.NormalModeList);
                stream.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public void Load()
        {
            Console.WriteLine("Wczytuje ./Backup/NormalModeList.dat");

            Stream stream;
            BinaryFormatter bFormatter;

            /*if (File.Exists("./Backup/NormalModeList.dat"))
            {
                stream = File.Open("./Backup/NormalModeList.dat", FileMode.Open);
                bFormatter = new BinaryFormatter();
                PokerService.Instance.NormalGameList = (List<NormalModeModel>)bFormatter.Deserialize(stream);
                stream.Close();

                //Tworze odpowiednie kontrolery dla tych gier
                foreach (NormalModeModel normalMode in PokerService.Instance.NormalModeList)
                {
                    NormalModeController gameController = new NormalModeController(normalMode.Table);
                    PokerService.Instance.NormalGameList.Add(gameController);

                    //Nowy watek dla deleara
                    var threadController = new Thread(new ThreadStart(gameController.GameObservator));
                    threadController.IsBackground = true;
                    threadController.Start();
                }
            }*/
        }
    }
}
