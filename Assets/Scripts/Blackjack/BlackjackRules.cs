using System.Collections.Generic;

public readonly struct BlackjackCard
{
    public readonly int Rank;
    public readonly int Suit;

    public BlackjackCard(int rank, int suit)
    {
        Rank = rank;
        Suit = suit;
    }

    public int Value => Rank == 1 ? 11 : Rank > 10 ? 10 : Rank;

    public override string ToString()
    {
        string[] ranks = { "", "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };
        string[] suits = { "S", "H", "D", "C" };
        return $"{ranks[Rank]}-{suits[Suit]}";
    }
}

public enum BlackjackOutcome
{
    None,
    Blackjack,
    Win,
    Lose,
    Push
}

public static class BlackjackRules
{
    public static int CalculateBestTotal(IReadOnlyList<BlackjackCard> cards)
    {
        int total = 0;
        int aces = 0;

        for (int i = 0; i < cards.Count; i++)
        {
            total += cards[i].Value;
            if (cards[i].Rank == 1)
            {
                aces++;
            }
        }

        while (total > 21 && aces > 0)
        {
            total -= 10;
            aces--;
        }

        return total;
    }

    public static BlackjackOutcome ResolveOutcome(int playerTotal, int dealerTotal, bool playerNatural, bool dealerNatural)
    {
        if (playerNatural)
        {
            return dealerNatural ? BlackjackOutcome.Push : BlackjackOutcome.Blackjack;
        }

        if (playerTotal > 21 || dealerNatural)
        {
            return BlackjackOutcome.Lose;
        }

        if (dealerTotal > 21)
        {
            return BlackjackOutcome.Win;
        }

        if (playerTotal == dealerTotal)
        {
            return BlackjackOutcome.Push;
        }

        return playerTotal > dealerTotal ? BlackjackOutcome.Win : BlackjackOutcome.Lose;
    }
}

public sealed class BlackjackRound
{
    private readonly List<BlackjackCard> playerCards = new List<BlackjackCard>();
    private readonly List<BlackjackCard> dealerCards = new List<BlackjackCard>();
    private readonly BlackjackCard[] deck = new BlackjackCard[52];
    private int nextCardIndex;

    public IReadOnlyList<BlackjackCard> PlayerCards => playerCards;
    public IReadOnlyList<BlackjackCard> DealerCards => dealerCards;
    public int PlayerTotal => BlackjackRules.CalculateBestTotal(playerCards);
    public int DealerTotal => BlackjackRules.CalculateBestTotal(dealerCards);
    public bool IsComplete { get; private set; }
    public BlackjackOutcome Outcome { get; private set; }

    public BlackjackRound()
    {
        for (int suit = 0; suit < 4; suit++)
        {
            for (int rank = 1; rank <= 13; rank++)
            {
                deck[suit * 13 + rank - 1] = new BlackjackCard(rank, suit);
            }
        }

        ShuffleDeck();
        playerCards.Add(DrawCard());
        dealerCards.Add(DrawCard());
        playerCards.Add(DrawCard());
        dealerCards.Add(DrawCard());

        bool playerNatural = playerCards.Count == 2 && PlayerTotal == 21;
        bool dealerNatural = dealerCards.Count == 2 && DealerTotal == 21;
        if (playerNatural || dealerNatural)
        {
            Complete(BlackjackRules.ResolveOutcome(PlayerTotal, DealerTotal, playerNatural, dealerNatural));
        }
    }

    public void Hit()
    {
        if (IsComplete)
        {
            return;
        }

        playerCards.Add(DrawCard());
        if (PlayerTotal > 21)
        {
            Complete(BlackjackOutcome.Lose);
        }
        else if (PlayerTotal == 21)
        {
            Stand();
        }
    }

    public void Stand()
    {
        if (IsComplete)
        {
            return;
        }

        while (DealerTotal < 17)
        {
            dealerCards.Add(DrawCard());
        }

        Complete(BlackjackRules.ResolveOutcome(PlayerTotal, DealerTotal, false, false));
    }

    private void ShuffleDeck()
    {
        for (int i = deck.Length - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            (deck[i], deck[swapIndex]) = (deck[swapIndex], deck[i]);
        }
    }

    private BlackjackCard DrawCard()
    {
        if (nextCardIndex >= deck.Length)
        {
            throw new System.InvalidOperationException("The blackjack deck is empty.");
        }

        return deck[nextCardIndex++];
    }

    private void Complete(BlackjackOutcome outcome)
    {
        Outcome = outcome;
        IsComplete = true;
    }
}
