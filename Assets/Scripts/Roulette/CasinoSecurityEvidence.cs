using System;
using System.Collections.Generic;

public sealed class CasinoSecurityEvidence
{
    public long RoundId { get; }
    public int WinningNumber { get; }
    public RouletteCustomerBet Bet { get; }
    public bool IsCheating { get; }
    public int AttemptedNumber { get; }
    public string CustomerName => Bet.Customer != null ? Bet.Customer.name : "UNKNOWN CUSTOMER";

    private CasinoSecurityEvidence(long roundId, int winningNumber, RouletteCustomerBet bet,
        bool cheating, int attemptedNumber)
    {
        RoundId = roundId;
        WinningNumber = winningNumber;
        Bet = bet;
        IsCheating = cheating;
        AttemptedNumber = attemptedNumber;
    }

    public static bool TryCreate(long roundId, int winningNumber,
        IReadOnlyList<RouletteCustomerBet> actualBets, out CasinoSecurityEvidence evidence)
    {
        evidence = null;
        if (roundId <= 0 || roundId % 3 != 0 ||
            !EuropeanRouletteRules.IsValidNumber(winningNumber) || actualBets == null || actualBets.Count == 0)
            return false;
        RouletteCustomerBet firstBet = null;
        RouletteCustomerBet losingBet = null;
        foreach (RouletteCustomerBet bet in actualBets)
        {
            if (bet == null || bet.RoundId != roundId ||
                !EuropeanRouletteRules.IsValidBet(bet.Bet)) return false;
            firstBet ??= bet;
            if (!EuropeanRouletteRules.Wins(bet.Bet, winningNumber))
                losingBet ??= bet;
        }
        bool cheating = roundId % 2 == 0 && losingBet != null;
        RouletteCustomerBet suspect = cheating ? losingBet : firstBet;
        evidence = new CasinoSecurityEvidence(roundId, winningNumber, suspect, cheating,
            cheating ? winningNumber : -1);
        return true;
    }
}
