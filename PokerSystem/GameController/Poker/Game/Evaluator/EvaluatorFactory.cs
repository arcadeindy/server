using CoinPokerCommonLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using CoinPokerServer;

namespace CoinPokerServer.PokerSystem.GameController.Poker.Game
{
    public static class EvaluatorFactory
    {
        //Funkcje odpowiedzialne za okreslenie ukladow
        public static readonly Func<IEnumerable<CardModel>, bool> Pair = HasGroup(2);
        public static readonly Func<IEnumerable<CardModel>, bool> TwoPair = HasGroupMore(2, 2);
        public static readonly Func<IEnumerable<CardModel>, bool> ThreeOfAKind = HasGroup(3);
        public static readonly Func<IEnumerable<CardModel>, bool> FourOfAKind = HasGroup(4);
        public static readonly Func<IEnumerable<CardModel>, bool> FullHouse =
            h => (
                FullHouseElements(h).Count() == 2 &&
                FullHouseElements(h).FirstOrDefault().Count() == 3
                );

        public static readonly Func<IEnumerable<CardModel>, bool> Flush =
            h => h.GroupBy(d => d.Suit).Where(d => d.Count() >= 5).Any();

        public static readonly Func<IEnumerable<CardModel>, bool> Straight =
            h => StraightNormalQuery(h);

        public static readonly Func<IEnumerable<CardModel>, bool> StraightFlush =
            h => StraightFlushQuery(h);

        //Pobiera grupe co najmniej dwoch elementow
        private static readonly Func<IEnumerable<CardModel>, IEnumerable<IEnumerable<CardModel>>> FullHouseElements =
            h => h.GroupBy(c => c.Face).
                Where(g => g.Count() == 2 || g.Count() == 3).OrderByDescending(i => i.Count()).Take(2).ToList();

        private static readonly Func<IEnumerable<CardModel>, bool> StraightNormalQuery =
            h => h.Select(c => c).Distinct()
                                    .OrderBy(c => c.Face)
                                    .GroupAdjacentBy((x, y) => x.Face + 1 == y.Face)
                                    .OrderByDescending(g => g.Count())
                                    .Where(g => 
                                        g.Count() >= 5 ||
                                        (g.Count() >= 4 && g.OrderBy(c=>c.Face).FirstOrDefault().Face == CardModel.CardNominalValue.Two && g.Any(c=>c.Face == CardModel.CardNominalValue.Ace))
                                     )
                                    .Any();

        private static readonly Func<IEnumerable<CardModel>, bool> StraightFlushQuery =
            h => h.Select(c => c).Distinct()
                                    .OrderBy(c => c.Face)
                                    .GroupAdjacentBy((x, y) => x.Face + 1 == y.Face)
                                    .OrderByDescending(g => g.Count())
                                    .Where(g =>
                                        (
                                        g.Count() >= 5 ||
                                        (g.Count() >= 4 && g.OrderBy(c => c.Face).FirstOrDefault().Face == CardModel.CardNominalValue.Two && g.Any(c => c.Face == CardModel.CardNominalValue.Ace))
                                        ) && g.GroupBy(d => d.Suit).Where(d => d.Count() >= 5).Any()
                                     )
                                    .Any();

        private static Func<IEnumerable<CardModel>, bool> HasGroupMore(int size, int count = 1)
        {
            return hand => hand.GroupBy(c => c.Face).Where(g => g.Count() == size).Count() >= count;
        }

        private static Func<IEnumerable<CardModel>, bool> HasGroup(int size, int count = 1)
        {
            return hand => hand.GroupBy(c => c.Face).Where(g => g.Count() == size).Count() == count;
        }

        public class HandTypeSolved
        {
            public List<CardModel> CardList { get; set; }
            public Enums.HandType HandType { get; set; }

            public int Points { get; set; }

            public HandTypeSolved()
            {
                CardList = new List<CardModel>();
                HandType = Enums.HandType.HighCard;
            }
        }

        public static HandTypeSolved GetHandType(Enums.PokerGameType gameType, IEnumerable<CardModel> playerHand, IEnumerable<CardModel> tableCards)
        {
            List<List<CardModel>> playerHandPermutationList;
            List<List<CardModel>> tableHandPermutationList;

            switch (gameType)
            {
                default:
                case Enums.PokerGameType.Holdem:
                    //Pobieramy wyłącznie dwie karty gracza i tworzymy permutacje list
                    playerHandPermutationList = Helper.GetPerms<CardModel>(playerHand.ToList(), 2);
                    //Pobieramy wyłącznie trzy karty ze stoły i tworzymy permutacje list
                    tableHandPermutationList = Helper.GetPerms<CardModel>(tableCards.ToList(), 5);
                    break;
                case Enums.PokerGameType.Omaha:
                    //Pobieramy wyłącznie dwie karty gracza i tworzymy permutacje list
                    playerHandPermutationList = Helper.GetPerms<CardModel>(playerHand.ToList(), 2);
                    //Pobieramy wyłącznie trzy karty ze stoły i tworzymy permutacje list
                    tableHandPermutationList = Helper.GetPerms<CardModel>(tableCards.ToList(), 3);
                    break;
            }

            HandTypeSolved bestHand = new HandTypeSolved();

            foreach (List<CardModel> cardPlayerList in playerHandPermutationList)
            {
                foreach (List<CardModel> tableList in tableHandPermutationList)
                {
                    var hand = cardPlayerList.Concat(tableList);
                    var result = Types.Where(t => t.Value(hand)).Select(t => t.Key).ToList();
                    if (result.Count() == 0)
                        result.Add(Enums.HandType.HighCard);

                    var handType = result.First();
                    var cardsPoints = (int)(Enum.GetNames(typeof(Enums.HandType)).Length - handType) * 10000 + hand.ToList().Sum(c => (int)c.Face);

                    if (cardsPoints >= bestHand.Points)
                    {
                        bestHand.HandType = handType;
                        bestHand.CardList = hand.ToList();
                        bestHand.Points = cardsPoints;
                    }
                }
            }

            return bestHand;
        }

        static IEnumerable<KeyValuePair<Enums.HandType, Func<IEnumerable<CardModel>, bool>>> Types
        {
            get
            {
                var pokerType = typeof(EvaluatorFactory);
                foreach (var type in Enum.GetValues(typeof(Enums.HandType)).Cast<Enums.HandType>())
                {
                    var field = pokerType.GetField(type.ToString(), BindingFlags.Static | BindingFlags.Public);
                    if (field != null)
                    {
                        var test = (Func<IEnumerable<CardModel>, bool>)field.GetValue(null);
                        yield return new KeyValuePair<Enums.HandType, Func<IEnumerable<CardModel>, bool>>(type, test);
                    }
                }
            }
        }
    }
}

