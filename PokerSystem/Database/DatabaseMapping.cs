using CoinPokerCommonLib;
using CoinPokerCommonLib.Models;
using CoinPokerCommonLib.Models.Game.TournamentOption;
using CoinPokerServer.PokerSystem.Database;
using FluentNHibernate.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CoinPokerServer.Database
{
    class DatabaseMapping
    {
        public class Map_Auth_User : ClassMap<UserModel>
        {
            public Map_Auth_User()
            {
                Table("auth_user");
                Id(x => x.ID, "id").GeneratedBy.Sequence("auth_user_id_seq");
                Map(x => x.Username, "username");
                Map(x => x.Password, "password");
                Map(x => x.LastLogin, "last_login");
                Map(x => x.DateJoined, "date_joined");
                Map(x => x.IsSuperuser, "is_superuser");
                Map(x => x.IsActive, "is_active");
                Map(x => x.IsStaff, "is_staff");
                Map(x => x.Firstname, "first_name");
                Map(x => x.Lastname, "last_name");
                Map(x => x.Email, "email");
            }
        }

        public class Map_Account_Wallet : ClassMap<WalletModel>
        {
            public Map_Account_Wallet()
            {
                Table("account_wallet");
                Id(x => x.ID, "id").GeneratedBy.Sequence("account_wallet_id_seq");
                Map(x => x.Available, "available");
                Map(x => x.Type, "currency").CustomType(typeof(Enums.CurrencyType));
                HasMany(x => x.TransferList).KeyColumn("wallet_id");
                References(x => x.User).Column("user_id");
            }
        }

        public class Map_Account_TransferOperation : ClassMap<TransferModel>
        {
            public Map_Account_TransferOperation()
            {
                Table("account_transferoperation");
                Id(x => x.ID, "id").GeneratedBy.Sequence("account_transferoperation_id_seq");
                Map(x => x.Type, "transfer_type").CustomType(typeof(Enums.TransferType));
                Map(x => x.Amount, "amount");
                Map(x => x.Timestamp, "timestamp");
                Map(x => x.Comment, "comment");
                Map(x => x.Flag, "flag");
                References(x => x.Wallet).Column("wallet_id");
            }
        }

        public class Map_Game_Normal : ClassMap<NormalGameModel>
        {
            public Map_Game_Normal()
            {
                Table("game_normal");
                Id(x => x.ID, "id").GeneratedBy.Sequence("game_normal_id_seq");
                Map(x => x.Name, "name");
                Map(x => x.Seats, "seats");
                Map(x => x.Limit, "limit").CustomType(typeof(Enums.LimitType));
                Map(x => x.Game, "game").CustomType(typeof(Enums.PokerGameType));
                Map(x => x.Blind, "blind");
                Map(x => x.Currency, "currency").CustomType(typeof(Enums.CurrencyType));
            }
        }

        public class Map_Game_Tournament_Base : ClassMap<BaseTournamentGameModel>
        {
            public Map_Game_Tournament_Base()
            {
                Table("game_basetournament");
                Id(x => x.ID, "id").GeneratedBy.Sequence("game_basetournament_id_seq");
                Map(x => x.Name, "name");
                Map(x => x.MaxPlayers, "max_players");
                Map(x => x.EntryPayment, "entry_payment");
                Map(x => x.EntryCurrency, "currency").CustomType(typeof(Enums.CurrencyType));
                Map(x => x.WinPotGuaranteed, "winpot_guaranteed");
            }
        }

        public class Map_Game_Tournament_Normal : ClassMap<NormalTournamentGameModel>
        {
            public Map_Game_Tournament_Normal()
            {
                Table("game_normaltournament");
                Id(x => x.ID, "basetournament_ptr_id");
                Map(x => x.StartAt, "start_at");
                Map(x => x.Registration, "registration").CustomType<NHibernate.Type.TimeAsTimeSpanType>();
                Map(x => x.LateReg, "late_reg").CustomType<NHibernate.Type.TimeAsTimeSpanType>();
                References(x => x.TournamentModel).Column("basetournament_ptr_id");
            }
        }

        public class Map_Game_Tournament_SitAndGo : ClassMap<SitAndGoTournamentGameModel>
        {
            public Map_Game_Tournament_SitAndGo()
            {
                Table("game_sitandgotournament");
                Id(x => x.ID, "basetournament_ptr_id");
                References(x => x.TournamentModel).Column("basetournament_ptr_id");
            }
        }

        public class Map_Application_Advertising : ClassMap<AdvertisingModel>
        {
            public Map_Application_Advertising()
            {
                Table("application_advertising");
                Id(x => x.ID, "id").GeneratedBy.Sequence("application_advertising_id_seq");
                Map(x => x.Url, "url");
                Map(x => x.Image, "image");
            }
        }
    }
}
