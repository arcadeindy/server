using CoinPokerCommonLib;
using NHibernate;
using NHibernate.Proxy;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace CoinPokerServer
{

    public class PrefixedWriter : TextWriter
    {
        private TextWriter originalOut;

        public PrefixedWriter()
        {
            originalOut = Console.Out;
        }

        public override Encoding Encoding
        {
            get { return new System.Text.ASCIIEncoding(); }
        }
        public override void WriteLine(string message)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            originalOut.WriteLine(String.Format("[{0}] {1}", DateTime.Now, message));
        }
        public override void Write(string message)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            originalOut.Write(String.Format("[{0}] {1}", DateTime.Now, message));
        }

    }

    public static class Helper
    {
        public static T RandomElement<T>(this IQueryable<T> q, Expression<Func<T, bool>> e)
        {
            var r = new Random();
            q = q.Where(e);
            return q.Skip(r.Next(q.Count())).FirstOrDefault();
        }

        public static List<List<T>> GetPerms<T>(List<T> list, int chainLimit)
        {
            if (list.Count() == 1)
                return new List<List<T>> { list };
            return list
                .Select((outer, outerIndex) =>
                            GetPerms(list.Where((inner, innerIndex) => innerIndex != outerIndex).ToList(), chainLimit)
                .Select(perms => (new List<T> { outer }).Union(perms).Take(chainLimit)))
                .SelectMany<IEnumerable<IEnumerable<T>>, List<T>>(sub => sub.Select<IEnumerable<T>, List<T>>(s => s.ToList()))
                .Distinct(new PermComparer<T>()).ToList();
        }

        class PermComparer<T> : IEqualityComparer<List<T>>
        {
            public bool Equals(List<T> x, List<T> y)
            {
                return x.SequenceEqual(y);
            }

            public int GetHashCode(List<T> obj)
            {
                return (int)obj.Average(o => o.GetHashCode());
            }
        }

        /// <summary>
        /// Usuwa proxy do bazy danych z obiektu Nhibernate
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <param name="session"></param>
        /// <returns></returns>
        public static T Unproxy<T>(this T obj, ISession session)
        {
            if (!NHibernateUtil.IsInitialized(obj))
            {
                NHibernateUtil.Initialize(obj);
            }

            if (obj is INHibernateProxy)
            {
                return (T)session.GetSessionImplementation().PersistenceContext.Unproxy(obj);
            }

            return obj;
        }

        /// <summary>
        /// Relatywna data
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public static string ToRelativeDate(this DateTime dateTime)
        {
            var timeSpan = DateTime.Now - dateTime;

            if (timeSpan <= TimeSpan.FromMinutes(-60))
                return timeSpan.Minutes > 1 ? String.Format("za {0} minut", timeSpan.Minutes) : "za minutę";

            if (timeSpan <= TimeSpan.FromHours(-24))
                return timeSpan.Hours > 1 ? String.Format("za {0} godziny", timeSpan.Hours) : "za godzinę";

            if (timeSpan <= TimeSpan.FromMinutes(60))
                return timeSpan.Minutes > 1 ? String.Format("{0} minut temu", timeSpan.Minutes) : "minutę temu";

            if (timeSpan <= TimeSpan.FromHours(24))
                return timeSpan.Hours > 1 ? String.Format("{0} godzin temu", timeSpan.Hours) : "godzinę temu";

            return dateTime.ToString();
        }

        /// <summary>
        /// Data kompilacji
        /// </summary>
        /// <returns></returns>
        public static DateTime RetrieveLinkerTimestamp()
        {
            string filePath = System.Reflection.Assembly.GetCallingAssembly().Location;
            const int c_PeHeaderOffset = 60;
            const int c_LinkerTimestampOffset = 8;
            byte[] b = new byte[2048];
            System.IO.Stream s = null;

            try
            {
                s = new System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read);
                s.Read(b, 0, 2048);
            }
            finally
            {
                if (s != null)
                {
                    s.Close();
                }
            }

            int i = System.BitConverter.ToInt32(b, c_PeHeaderOffset);
            int secondsSince1970 = System.BitConverter.ToInt32(b, i + c_LinkerTimestampOffset);
            DateTime dt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            dt = dt.AddSeconds(secondsSince1970);
            dt = dt.ToLocalTime();
            return dt;
        }


        /// <summary>
        /// Losowo wstawia obiekty w liśćie
        /// http://en.wikipedia.org/wiki/Fisher–Yates_shuffle
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        public static void Shuffle<T>(this IList<T> list)
        {
            Random rng = new Random();
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        /// <summary>
        /// Serialziacja
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string Serialize<T>(this T value)
        {
            if (value == null)
            {
                return null;
            }

            XmlSerializer serializer = new XmlSerializer(typeof(T));

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Encoding = new UnicodeEncoding(false, false);
            settings.Indent = true;
            settings.OmitXmlDeclaration = false;

            using (StringWriter textWriter = new StringWriter())
            {
                using (XmlWriter xmlWriter = XmlWriter.Create(textWriter, settings))
                {
                    serializer.Serialize(xmlWriter, value);
                }
                return textWriter.ToString();
            }
        }

        /// <summary>
        /// Deserializacja
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="xml"></param>
        /// <returns></returns>
        public static T Deserialize<T>(string xml)
        {

            if (string.IsNullOrEmpty(xml))
            {
                return default(T);
            }

            XmlSerializer serializer = new XmlSerializer(typeof(T));

            XmlReaderSettings settings = new XmlReaderSettings();

            using (StringReader textReader = new StringReader(xml))
            {
                using (XmlReader xmlReader = XmlReader.Create(textReader, settings))
                {
                    return (T)serializer.Deserialize(xmlReader);
                }
            }
        }

        /// <summary>
        /// Grupowanie według funkcji
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public static IEnumerable<IEnumerable<T>> GroupAdjacentBy<T>(
            this IEnumerable<T> source, Func<T, T, bool> predicate)
        {
            using (var e = source.GetEnumerator())
            {
                if (e.MoveNext())
                {
                    var list = new List<T> { e.Current };
                    var pred = e.Current;
                    while (e.MoveNext())
                    {
                        if (predicate(pred, e.Current))
                        {
                            list.Add(e.Current);
                        }
                        else
                        {
                            yield return list;
                            list = new List<T> { e.Current };
                        }
                        pred = e.Current;
                    }
                    yield return list;
                }
            }
        }

        /// <summary>
        /// Serializacja do pliku
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="filename"></param>
        public static void SerializeToFile<T>(this T value, string filename)
        {
            if (value == null)
            {
                return;
            }
            using (var writer = new System.IO.StreamWriter(filename))
            {
                var serializer = new XmlSerializer(value.GetType());
                serializer.Serialize(writer, value);
                writer.Flush();
            }
        }

        /// <summary>
        /// Deserialziacja z pliku
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static T DeserializeToFile<T>(this T value, string filename)
        {
            var serializer = new XmlSerializer(value.GetType());
            StreamReader reader = new StreamReader(filename);
            T obj = (T)serializer.Deserialize(reader);
            reader.Close();
            return obj;
        }

        /// <summary>
        /// Wyznacza nastepnego gracza w grze
        /// </summary>
        /// <param name="list"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        public static PlayerModel NextPlayer(this List<PlayerModel> list, PlayerModel item)
        {
            var seat = item.Seat;
            PlayerModel player = null;

            //Brak aktywnych graczy
            if (list.Count() == 0) return null;

            while (player == null)
            {
                if (list.Max(p => p.Seat) > seat)
                    seat++;
                else
                    seat = list.Min(p => p.Seat);
                player = list.FirstOrDefault(p => p.Seat == seat);
            }

            return player;
        }

        /// <summary>
        /// Pobiera ostatni element
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="theList"></param>
        /// <returns></returns>
        public static T Pop<T>(this List<T> theList)
        {
            var local = theList[theList.Count - 1];
            theList.RemoveAt(theList.Count - 1);
            return local;
        }

        /// <summary>
        /// Pobranie hashu md5
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string GetMd5Hash(string input)
        {
            MD5 md5Hasher = MD5.Create();
            byte[] data = md5Hasher.ComputeHash(Encoding.Default.GetBytes(input));
            StringBuilder sBuilder = new StringBuilder();

            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            return sBuilder.ToString();
        }

        /// <summary>
        /// Sprawdzenie hasła w systemie
        /// </summary>
        /// <param name="passwordFromDatabase"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public static bool CheckPassword(string passwordFromDatabase, string password)
        {
            Console.WriteLine("CheckPassword => " + passwordFromDatabase + " - " + password);
            string[] databaseSplitHash = passwordFromDatabase.Split('$');
            string generatedPwd = Helper.GetMd5Hash(databaseSplitHash[1] + password);

            Console.WriteLine("CheckPassword => " + databaseSplitHash[2] + " - " + generatedPwd);
            if (generatedPwd == databaseSplitHash[2])
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
