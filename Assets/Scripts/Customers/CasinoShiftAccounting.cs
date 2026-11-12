using System;
using System.Collections.Generic;

/// <summary>Accounts for settled rounds, not physical float movements.</summary>
public sealed class CasinoShiftAccounting
{
    private readonly HashSet<BlackjackTableRound> blackjackRounds = new HashSet<BlackjackTableRound>();
    private readonly HashSet<RouletteCustomerBet> rouletteBets = new HashSet<RouletteCustomerBet>();

    public long StakesPence { get; private set; }
    public long ReturnsPence { get; private set; }
    public long ProfitPence => StakesPence - ReturnsPence;
    public int SettledHands { get; private set; }

    public bool RecordBlackjack(BlackjackTableRound round)
    {
        if (round == null || !round.IsSettled)
            throw new ArgumentException("Only a settled blackjack round can be recorded.", nameof(round));
        if (blackjackRounds.Contains(round))
            return false;

        long stakes = 0;
        long returns = 0;
        foreach (BlackjackHand hand in round.CustomerHands)
        {
            stakes = checked(stakes + hand.BetPence);
            returns = checked(returns + hand.PayoutPence);
        }
        RecordAmounts(stakes, returns, round.CustomerHands.Count);
        blackjackRounds.Add(round);
        return true;
    }

    public bool RecordRoulette(RouletteCustomerBet bet)
    {
        if (bet == null || !bet.IsSettled)
            throw new ArgumentException("Only a settled roulette bet can be recorded.", nameof(bet));
        if (rouletteBets.Contains(bet))
            return false;

        long returnedStake = bet.IsWinning ? bet.Bet.StakePence : 0;
        RecordAmounts(bet.Bet.StakePence, returnedStake + bet.WinningsPence, 1);
        rouletteBets.Add(bet);
        return true;
    }

    private void RecordAmounts(long stake, long winnings, int hands)
    {
        checked
        {
            long nextStakes = StakesPence + stake;
            long nextReturns = ReturnsPence + winnings;
            int nextHands = SettledHands + hands;
            StakesPence = nextStakes;
            ReturnsPence = nextReturns;
            SettledHands = nextHands;
        }
    }
}
