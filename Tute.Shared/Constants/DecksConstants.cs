using System.Collections.Generic;

namespace Tute.Shared.Constants
{
    public readonly struct Deck
    {
        public Deck(string deckName)
        {
            DeckName = deckName;
        }

        public string DeckName { get; }

        public static implicit operator Deck(string deck) => new(deck);

        public static bool operator ==(Deck first, Deck second) =>
            first.DeckName == second.DeckName;

        public static bool operator !=(Deck first, Deck second) =>
            first.DeckName != second.DeckName;

        public override bool Equals(object obj) => obj is Deck deck && DeckName == deck.DeckName;

        public override int GetHashCode() =>
            1847699267 + EqualityComparer<string>.Default.GetHashCode(DeckName);
    }

    public static class DecksConstants
    {
        public static readonly Deck NormalDeck = "deck";
        public static readonly Deck ShortDeck = "short_deck";
        public static readonly Deck Cante20Deck = "cante_20_deck";
        public static readonly Deck Cante40Deck = "cante_40_deck";
        public static readonly Deck TuteKingsDeck = "tute_kings_deck";
        public static readonly Deck TutePrinceDeck = "tute_prince_deck";
    }
}
