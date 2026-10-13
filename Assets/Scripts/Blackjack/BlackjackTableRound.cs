using System;
using System.Collections.Generic;
using UnityEngine;

public enum BlackjackTableState
{
    WaitingForCustomers,
    TakingBets,
    InitialDeal,
    CustomerTurns,
    DealerTurn,
    Settlement,
    ClearingTable
}

public enum BlackjackHandStatus
{
    Waiting,
    Playing,
    Standing,
    Bust,
    Blackjack,
    Win,
    Lose,
    Push
}

public sealed class BlackjackHand
{
    private readonly List<BlackjackCard> cards = new List<BlackjackCard>();

    public CasinoCustomerAI Customer { get; }
    public CasinoSeat Seat { get; }
    public IReadOnlyList<BlackjackCard> Cards => cards;
    public int BetPence { get; }
    public int Total => BlackjackRules.CalculateBestTotal(cards);
    public bool IsNatural => cards.Count == 2 && Total == 21;
    public BlackjackHandStatus Status { get; internal set; }
    public BlackjackOutcome Outcome { get; internal set; }
    public int PayoutPence { get; internal set; }
    public bool PayoutPaid { get; internal set; }

    internal BlackjackHand(CasinoCustomerAI customer, CasinoSeat seat, int betPence)
    {
        Customer = customer;
        Seat = seat;
        BetPence = betPence;
        Status = BlackjackHandStatus.Playing;
    }

    internal void AddCard(BlackjackCard card)
    {
        cards.Add(card);
    }
}

public sealed class BlackjackTableRound
{
    private readonly List<BlackjackHand> customerHands = new List<BlackjackHand>(3);
    private readonly List<BlackjackCard> dealerCards = new List<BlackjackCard>(12);
    private bool cardsDealt;

    public BlackjackShoe Shoe { get; }
    public IReadOnlyList<BlackjackHand> CustomerHands => customerHands;
    public IReadOnlyList<BlackjackCard> DealerCards => dealerCards;
    public int DealerTotal => BlackjackRules.CalculateBestTotal(dealerCards);
    public bool DealerHasNatural => dealerCards.Count == 2 && DealerTotal == 21;
    public bool IsSettled { get; private set; }

    public BlackjackTableRound(int betPence, BlackjackShoe shoe = null)
    {
        if (betPence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(betPence));
        }

        Shoe = shoe ?? new BlackjackShoe();
        BetPence = betPence;
    }

    public int BetPence { get; }

    public BlackjackHand AddCustomer(CasinoCustomerAI customer, CasinoSeat seat)
    {
        if (cardsDealt)
        {
            throw new InvalidOperationException("Customers joining after the deal starts must wait for the next round.");
        }

        if (customerHands.Count >= 3)
        {
            throw new InvalidOperationException("A blackjack table supports at most three customer hands.");
        }

        if (customer != null && customerHands.Exists(hand => hand.Customer == customer))
        {
            throw new InvalidOperationException("A customer cannot join the same round twice.");
        }

        if (seat != null && customerHands.Exists(hand => hand.Seat == seat))
        {
            throw new InvalidOperationException("A seat cannot participate in the same round more than once.");
        }

        BlackjackHand hand = new BlackjackHand(customer, seat, BetPence);
        customerHands.Add(hand);
        return hand;
    }

    public BlackjackCard DealTo(BlackjackHand hand)
    {
        if (hand == null || !customerHands.Contains(hand) || IsSettled)
        {
            throw new InvalidOperationException("Cannot deal to a hand that is not active in this round.");
        }

        cardsDealt = true;
        BlackjackCard card = Shoe.Draw();
        hand.AddCard(card);
        return card;
    }

    public BlackjackCard DealToDealer()
    {
        if (IsSettled)
        {
            throw new InvalidOperationException("Cannot deal to the dealer after the round is settled.");
        }

        cardsDealt = true;
        BlackjackCard card = Shoe.Draw();
        dealerCards.Add(card);
        return card;
    }

    public void Settle()
    {
        if (IsSettled)
        {
            throw new InvalidOperationException("This blackjack round has already been settled.");
        }

        bool dealerNatural = DealerHasNatural;
        foreach (BlackjackHand hand in customerHands)
        {
            BlackjackOutcome outcome = BlackjackRules.ResolveOutcome(
                hand.Total,
                DealerTotal,
                hand.IsNatural,
                dealerNatural);
            hand.Outcome = outcome;
            hand.PayoutPence = BlackjackRules.GetPayoutPence(outcome, hand.BetPence);
            hand.Status = outcome == BlackjackOutcome.Blackjack
                ? BlackjackHandStatus.Blackjack
                : outcome == BlackjackOutcome.Win
                    ? BlackjackHandStatus.Win
                    : outcome == BlackjackOutcome.Push
                        ? BlackjackHandStatus.Push
                        : hand.Total > 21
                            ? BlackjackHandStatus.Bust
                            : BlackjackHandStatus.Lose;
            hand.PayoutPaid = outcome == BlackjackOutcome.Lose;
        }

        IsSettled = true;
    }

    public bool TryMarkPayoutPaid(BlackjackHand hand)
    {
        if (!IsSettled || hand == null || !customerHands.Contains(hand) ||
            hand.PayoutPence <= 0 || hand.PayoutPaid)
        {
            return false;
        }

        hand.PayoutPaid = true;
        return true;
    }
}
