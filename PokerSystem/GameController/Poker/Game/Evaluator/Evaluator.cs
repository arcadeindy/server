using CoinPokerCommonLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CoinPokerServer.PokerSystem.CommonExtensions;

namespace CoinPokerServer.PokerSystem.GameController.Poker.Game
{
    public class EvaluatorStrength
    {
        public PlayerModel Player { get; set; }
        public int Points { get; set; }               //Punkty za karty
        public int KickerPoints { get; set; }         //Punkty za kickery
        public int FullPoints
        {
            get
            {
                return Points + KickerPoints;
            }
        }
        public bool IsKickerWin { get; set; }
        public bool IsBest { get; set; }
        public decimal Contributed { get; set; }
        public List<CardModel> KickerCards { get; set; }
        public List<CardModel> WinCardList { get; set; } //Karty które wygrały układ
        public Enums.HandType HandType { get; set; } //Rodzaj ręki
    }

    public class Evaluator
    {
        Game Game { get; set; }
        public List<EvaluatorStrength> EvaluatorStrenghtList { get; set; }

        public Evaluator(Game game)
        {
            Game = game;
            EvaluatorStrenghtList = new List<EvaluatorStrength>();
        }

        public void CalculateStrength()
        {
            var gameTable = Game.GameTableModel;

            //Obliczamy siły układów
            foreach (PlayerModel player in gameTable.PlayerHavingPlayStatus())
            {
                EvaluatorStrenghtList.Add(CalculateCardsStrenght(player));
            }

            //Obliczamy wygrane układy i dzielniki wygranych
            EvaluatorDivisionHand();
        }

        public EvaluatorStrength GetStrenght(PlayerModel showingPlayer)
        {
            return EvaluatorStrenghtList.FirstOrDefault(p => p.Player == showingPlayer);
        }

        /// <summary>
        /// Dzielimy wygrane na części, obliczmay DivisionStackCounter
        /// </summary>
        public void EvaluatorDivisionHand()
        {
            var divisionList = EvaluatorStrenghtList.GroupBy(g => g.Points).Select(g => new
                {
                    DivisionCounter = g.Count(),
                    Points = g.FirstOrDefault().Points,
                    HandType = g.FirstOrDefault().HandType,
                    List = g.ToList()
                });

            //Obliczamy sumy punktowe dla kickerów i wyłaniamy je z puli 
            foreach (var divisioned in divisionList)
            {
                foreach (var es in divisioned.List)
                {
                    //Lista zawierająca wszystkie kickery z danej grupy punktowej oprócz [es]
                    var kickerCmp = divisioned.List.SelectMany(c => c.KickerCards).Except(es.KickerCards);

                    //Wyłaniamy kickera
                    //[kickery sprawdzanego]-[wszystkie kickery z danej puli punktowej oprocz sprawdzanego] : sort : take(1)
                    es.KickerCards = es.KickerCards.Except(kickerCmp).OrderByDescending(c => c.Face).Take(1).ToList();
                    if (es.KickerCards.FirstOrDefault() != null)
                        es.KickerPoints = (int)es.KickerCards.FirstOrDefault().Face;
                }
            }

            //Posiadając już kickery sprawdzamy układy i dzielimy według wygranych
            foreach (var divisioned in divisionList)
            {
                foreach (var es in divisioned.List)
                {
                    if (divisionList.Max(c => c.List.Max(d => d.FullPoints)) == es.FullPoints)
                    {
                        es.IsBest = true;
                    }
                }

                var divisonedListKickers = divisioned.List.Where(d=>d.KickerPoints == (divisioned.List.Max(e=>e.KickerPoints)));
                foreach (var es in divisonedListKickers)
                {
                    if (divisioned.List.Where(e=>e.KickerPoints!=es.KickerPoints).Any())
                        es.IsKickerWin = true;
                }
            }

        }

        public static string CardHandName(EvaluatorStrength es)
        {
            switch (es.HandType)
            {
                case Enums.HandType.StraightFlush:
                    return "poker od " + CardModel.GetNormalize(es.WinCardList.First(), CardModel.NormalizeNominalSize.ONE);
                case Enums.HandType.Straight:
                    return "strit od " + CardModel.GetNormalizeNominal(es.WinCardList.First().Face, CardModel.NormalizeNominalSize.ONE);
                case Enums.HandType.Flush:
                    return "kolor " + CardModel.GetNormalizeSuit(es.WinCardList.First().Suit) + " do " + CardModel.GetNormalizeNominal(es.WinCardList.First().Face, CardModel.NormalizeNominalSize.ONE);
                case Enums.HandType.FullHouse:
                    var kList = es.WinCardList.
                        GroupBy(g => g.Face).
                        OrderBy(g => g.Count()).
                        Select(g => new
                        {
                            Face = g.First().Face,
                            Count = g.Count()
                        });
                    return "fula " + CardModel.GetNormalizeNominal(kList.First().Face, CardModel.NormalizeNominalSize.MANY) + " na " + CardModel.GetNormalizeNominal(kList.Last().Face, CardModel.NormalizeNominalSize.MANY);
                case Enums.HandType.FourOfAKind:
                    return "czwórkę " + CardModel.GetNormalizeNominal(es.WinCardList.First().Face, CardModel.NormalizeNominalSize.MANY);
                case Enums.HandType.ThreeOfAKind:
                    return "trójkę " + CardModel.GetNormalizeNominal(es.WinCardList.First().Face, CardModel.NormalizeNominalSize.MANY);
                case Enums.HandType.TwoPair:
                    var pList = es.WinCardList.
                        GroupBy(g => g.Face).
                        OrderBy(g => g.Count()).
                        Select(g => new
                        {
                            Face = g.First().Face,
                            Count = g.Count()
                        });
                    return "dwie pary, " + CardModel.GetNormalizeNominal(pList.First().Face, CardModel.NormalizeNominalSize.MANY) + " oraz " + CardModel.GetNormalizeNominal(pList.Last().Face, CardModel.NormalizeNominalSize.MANY);
                case Enums.HandType.Pair:
                    return "parę " + CardModel.GetNormalizeNominal(es.WinCardList.First().Face, CardModel.NormalizeNominalSize.MANY);
                case Enums.HandType.HighCard:
                    return "najwyższą kartę " + CardModel.GetNormalizeNominal(es.WinCardList.First().Face, CardModel.NormalizeNominalSize.ONE);
            }
            return "---Błąd---";
        }

        /// <summary>
        /// Zlicza punkty układów
        /// </summary>
        /// <param name="handStrengthList"></param>
        /// <returns></returns>
        public EvaluatorStrength CalculateCardsStrenght(PlayerModel player)
        {
            var gameTable = Game.GameTableModel;

            //Sprawdzamy najsilniejszy układ jaki gracz posiada
            var handSolved = EvaluatorFactory.GetHandType(Game.GameTableModel.Game, player.Cards, gameTable.TableCardList);
            //Operacje rozwiązujemy na dane liście kart
            var cardList = handSolved.CardList;

            //Obliczamy jego punktację
            var cardsPoints = (int)(Enum.GetNames(typeof(Enums.HandType)).Length - handSolved.HandType) * 1000 + 0;
            var kickerPoints = 0;
            var winCardList = new List<CardModel>();
            var kickerCards = new List<CardModel>();

            List<CardModel> kickerList = new List<CardModel>();

            switch (handSolved.HandType)
            {
                case Enums.HandType.StraightFlush:
                case Enums.HandType.Straight:
                    //Pobieramy liste kart
                    var sList = cardList.Select(c => c)
                                    .OrderBy(c => c.Face)
                                    .GroupAdjacentBy((x, y) => (int)x.Face + 1 == (int)y.Face)
                                    .Where(g => g.Count() >= 5)
                                    .Select(g => g)
                                    .FirstOrDefault();

                    if (sList == null)
                    {
                        winCardList.Add(cardList.FirstOrDefault(c => c.Face == CardModel.CardNominalValue.Ace));
                        cardsPoints += (int)0;
                    }
                    else
                    {
                        winCardList = sList.OrderByDescending(s => (int)s.Face).Take(5).OrderBy(s => (int)s.Face).ToList();
                        cardsPoints += (int)winCardList.First().Face + 1;
                    }

                    break;
                case Enums.HandType.Flush:
                    var fList = cardList.
                                GroupBy(c => c.Suit).
                                Where(g => g.Count() >= 5).
                                Select(g => g.First()).OrderByDescending(c=>(int)c.Face).Take(5);

                    winCardList = fList.ToList();
                    cardsPoints += (int)winCardList.First().Face;
                    break;
                case Enums.HandType.FullHouse:
                case Enums.HandType.FourOfAKind:
                case Enums.HandType.ThreeOfAKind:
                case Enums.HandType.TwoPair:
                case Enums.HandType.Pair:
                    //Wczytujemy liste kart jako grupe
                    var kList = cardList.
                        GroupBy(g => g.Face).
                        Where(g => g.Count() > 1).
                        Select(g => new 
                        {
                            Face = g.First().Face,
                            Count = g.Count(),
                            List = g.ToList()
                        }).OrderByDescending(e => e.Count).ThenByDescending(e => e.Face).ToList();

                    if (handSolved.HandType == Enums.HandType.FullHouse)
                    {
                        kList = kList.Take(2).ToList();
                    }

                    if (handSolved.HandType == Enums.HandType.FourOfAKind)
                    {
                        kList = kList.Take(1).ToList();
                    }

                    if (handSolved.HandType == Enums.HandType.ThreeOfAKind)
                    {
                        kList = kList.Take(1).ToList();
                    }

                    if (handSolved.HandType == Enums.HandType.TwoPair)
                    {
                        kList = kList.Take(2).ToList();
                    }

                    if (handSolved.HandType == Enums.HandType.Pair)
                    {
                        kList = kList.Take(1).ToList();
                    }

                    foreach (var card in kList)
                    {
                        cardsPoints += (int)card.Face * card.Count;
                        winCardList = winCardList.Concat(card.List).ToList();
                    }

                    //Obliczamy sile kickerow gdyby byla potrzebna czyli 5 najwyzszych kart
                    kickerList = player.Cards.Concat(gameTable.TableCardList).Except(winCardList).OrderByDescending(c => (int)c.Face).ToList();

                    if (handSolved.HandType == Enums.HandType.FourOfAKind)
                        foreach (CardModel card in kickerList.Take(1))
                        {
                            kickerCards.Add(card);
                        }

                    if (handSolved.HandType == Enums.HandType.ThreeOfAKind)
                        foreach (CardModel card in kickerList.Take(2))
                        {
                            kickerCards.Add(card);
                        }

                    if (handSolved.HandType == Enums.HandType.TwoPair)
                        foreach (CardModel card in kickerList.Take(1))
                        {
                            kickerCards.Add(card);
                        }

                    if (handSolved.HandType == Enums.HandType.Pair)
                        foreach (CardModel card in kickerList.Take(3))
                        {
                            kickerCards.Add(card);
                        }

                    break;
                case Enums.HandType.HighCard:
                    //Sprawdzamy wszystkie karty
                    cardList = player.Cards.Concat(gameTable.TableCardList).ToList();

                    var hList = cardList.OrderByDescending(k => k.Face).ToList();
                    winCardList.Add(hList.First());
                    cardsPoints += (int)winCardList.First().Face;

                    //Obliczamy sile kickerow gdyby byla potrzebna czyli 5 najwyzszych kart
                    kickerList = cardList.OrderByDescending(c => (int)c.Face).Except(winCardList).ToList();

                    foreach (CardModel card in kickerList.Take(4))
                    {
                        kickerCards.Add(card);
                    }

                    break;
            }

            var contributed = gameTable.ActionHistory.OfType<BetAction>().Where(p => p.Player.User.ID == player.User.ID).Sum(c => c.Bet);

            return new EvaluatorStrength()
            {
                Player = player,
                HandType = handSolved.HandType,
                Points = cardsPoints,
                KickerPoints = kickerPoints,
                KickerCards = kickerCards,
                Contributed = contributed, 
                WinCardList = winCardList
            };
        }

    }
}
