using CoinPokerCommonLib;
using Hik.Communication.ScsServices.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoinPokerServer.ModelExtensions
{
    public class ClientModel
    {
        /// <summary>
        /// Scs client reference.
        /// </summary>
        public IScsServiceClient Client { get; set; }

        /// <summary>
        /// Proxy object to call remote methods of chat client.
        /// </summary>
        public IPokerClient ClientProxy { get; set; }

        public UserModel User { get; set; }

        public ClientModel(UserModel user)
        {
            this.User = user;
        }

        public static ClientModel GetUser(IScsServiceClient Client)
        {
            return PokerService.Instance.Clients.FirstOrDefault(c=>c.Client.ClientId == Client.ClientId);
        }

        public static explicit operator UserModel(ClientModel o)
        {
            return o.User;
        }
    }
}
