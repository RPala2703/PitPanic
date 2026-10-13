using System;
using System.Collections.Generic;

public sealed class BlackjackShoe
{
    private readonly List<BlackjackCard> cards = new List<BlackjackCard>(52);
    private int nextCardIndex;

    public int RemainingCards => cards.Count - nextCardIndex;

    public BlackjackShoe(int deckCount = 1, int? seed = null)
    {
        if (deckCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(deckCount));
        }

        for (int deck = 0; deck < deckCount; deck++)
        {
            for (int suit = 0; suit < 4; suit++)
            {
                for (int rank = 1; rank <= 13; rank++)
                {
                    cards.Add(new BlackjackCard(rank, suit));
                }
            }
        }

        System.Random random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            (cards[i], cards[swapIndex]) = (cards[swapIndex], cards[i]);
        }
    }

    public BlackjackShoe(IEnumerable<BlackjackCard> drawOrder)
    {
        if (drawOrder == null)
        {
            throw new ArgumentNullException(nameof(drawOrder));
        }

        HashSet<int> distinctCards = new HashSet<int>();
        foreach (BlackjackCard card in drawOrder)
        {
            if (card.Rank < 1 || card.Rank > 13 || card.Suit < 0 || card.Suit > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(drawOrder), "A shoe contains an invalid card.");
            }

            int cardId = card.Suit * 13 + card.Rank - 1;
            if (!distinctCards.Add(cardId))
            {
                throw new ArgumentException("A shoe cannot contain the same physical card more than once.", nameof(drawOrder));
            }

            cards.Add(card);
        }

        if (cards.Count == 0)
        {
            throw new ArgumentException("A shoe must contain at least one card.", nameof(drawOrder));
        }
    }

    public BlackjackCard Draw()
    {
        if (nextCardIndex >= cards.Count)
        {
            throw new InvalidOperationException("The blackjack shoe is empty.");
        }

        return cards[nextCardIndex++];
    }
}
