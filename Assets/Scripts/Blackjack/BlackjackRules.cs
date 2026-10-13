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
        string[] suits = { "♠", "♥", "♦", "♣" };
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

    public static bool IsSoft17(IReadOnlyList<BlackjackCard> cards)
    {
        int rawTotal = 0;
        int aces = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            rawTotal += cards[i].Value;
            if (cards[i].Rank == 1)
            {
                aces++;
            }
        }

        return aces > 0 && rawTotal - 10 * (aces - 1) == 17;
    }

    public static bool DealerMustHit(IReadOnlyList<BlackjackCard> dealerCards)
    {
        return CalculateBestTotal(dealerCards) < 17;
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

    public static int GetPayoutPence(BlackjackOutcome outcome, int betPence)
    {
        if (betPence < 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(betPence));
        }

        switch (outcome)
        {
            case BlackjackOutcome.Blackjack:
                return checked(betPence + betPence * 3 / 2);
            case BlackjackOutcome.Win:
                return checked(betPence * 2);
            case BlackjackOutcome.Push:
                return betPence;
            default:
                return 0;
        }
    }
}
