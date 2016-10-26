using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

using NHibernate;
using NHibernate.Cfg;
using NHibernate.Linq;

using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using FluentNHibernate.Mapping;
using FluentNHibernate.Conventions.Helpers;
using FluentNHibernate.Automapping;
using FluentNHibernate;
using FluentNHibernate.Automapping.Alterations;
using CoinPokerCommonLib;
 
namespace CoinPokerServer.Database
{

    /// <summary>
    /// Budowanie fabryki sesji
    /// </summary>
    public static class DatabaseSession
    {
        static ISessionFactory sessionFactory = null;
        public static ISessionFactory GetSessionFactory()
        {
            if (sessionFactory != null)
                return sessionFactory;

            var dialects = new NHibernate.Dialect.Dialect[] {
                new NHibernate.Dialect.PostgreSQLDialect()           
            };

            try
            {
                sessionFactory = Fluently.Configure()
                    .ExposeConfiguration(cfg => cfg.Properties.Add("use_proxy_validator", "false"))
                    .Database(PostgreSQLConfiguration.Standard
                            .ConnectionString("Server=unitypoker.eu;Database=unitypoker;User ID=postgres;Password=unity123;"))
                    .Mappings
                        (m =>
                            m.FluentMappings.AddFromAssemblyOf<DatabaseMapping>()
                            .Conventions.Add(ConventionBuilder.Class.Always(x => x.Table(x.TableName.ToLower()))) 
                        )
                    .BuildSessionFactory();

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            return sessionFactory;
        }

        public static NHibernate.ISession Open(){
            return DatabaseSession.GetSessionFactory().OpenSession();
        }
    }
}
