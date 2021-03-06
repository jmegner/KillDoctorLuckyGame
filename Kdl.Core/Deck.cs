using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Kdl.Core
{
    public class Deck
    {
        public List<Card> DrawPile { get; init; }
        public List<Card> DiscardPile { get; init; }

        public Deck(IEnumerable<Card> cards)
        {
            DrawPile = new List<Card>(cards);
            DiscardPile = new();
        }

        public static Deck FromJson(string cardsPath)
        {
            var cardsJson = File.ReadAllText(cardsPath);
            var cards = JsonHelper.Deserialize<List<Card>>(cardsJson);
            var deck = new Deck(cards);
            return deck;
        }
    }
}
