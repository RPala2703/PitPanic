using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CasinoSecurityEvidence
{
    public long RoundId { get; }
    public int WinningNumber { get; }
    public RouletteCustomerBet Bet { get; }
    public bool IsCheating { get; }
    public int AttemptedNumber { get; }
    public string CustomerName { get; }
    public Sprite[] CustomerFrames { get; }

    private CasinoSecurityEvidence(long roundId, int winningNumber, RouletteCustomerBet bet,
        bool cheating, int attemptedNumber)
    {
        RoundId = roundId;
        WinningNumber = winningNumber;
        Bet = bet;
        IsCheating = cheating;
        AttemptedNumber = attemptedNumber;
        CustomerName = bet.Customer != null ? bet.Customer.name : "CUSTOMER";
        CustomerFrames = new Sprite[180];
        PixelCharacterVisual visual = bet.Customer != null ? bet.Customer.GetComponentInChildren<PixelCharacterVisual>() : null;
        if (visual != null)
            for (int i = 0; i < 60; i++)
            {
                CustomerFrames[i] = visual.ReplayFrame(CasinoAnimationPose.SeatedIdle, i);
                CustomerFrames[60 + i] = visual.ReplayFrame(CasinoAnimationPose.Anticipate, i);
                CustomerFrames[120 + i] = visual.ReplayFrame(CasinoAnimationPose.Present, i);
            }
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
