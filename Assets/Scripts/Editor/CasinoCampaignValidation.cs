using System;
using System.Collections.Generic;
using UnityEngine;

public static class CasinoCampaignValidation
{
    public static void Run()
    {
        int[] oldValues =
        {
            PlayerPrefs.GetInt(CasinoDemoProgression.UnlockedLevelKey, -1),
            PlayerPrefs.GetInt(CasinoDemoProgression.CreditsKey, -1),
            PlayerPrefs.GetInt(CasinoDemoProgression.UpgradeKeys[0], -1),
            PlayerPrefs.GetInt(CasinoDemoProgression.UpgradeKeys[1], -1),
            PlayerPrefs.GetInt(CasinoDemoProgression.UpgradeKeys[2], -1),
            PlayerPrefs.GetInt("PitPanic.TutorialComplete", -1),
            PlayerPrefs.GetInt(CasinoDemoProgression.BestStarsKey(1), -1),
            PlayerPrefs.GetInt(CasinoDemoProgression.BestStarsKey(2), -1),
            PlayerPrefs.GetInt(CasinoDemoProgression.BestStarsKey(3), -1)
        };
        try
        {
            PlayerPrefs.SetInt(CasinoDemoProgression.UnlockedLevelKey, 1);
            PlayerPrefs.SetInt(CasinoDemoProgression.CreditsKey, 7);
            for (int i = 0; i < CasinoDemoProgression.UpgradeKeys.Length; i++)
                PlayerPrefs.SetInt(CasinoDemoProgression.UpgradeKeys[i], 1);
            for (int level = 1; level <= CasinoDemoProgression.LevelCount; level++)
                PlayerPrefs.SetInt(CasinoDemoProgression.BestStarsKey(level), level);
            PlayerPrefs.SetInt("PitPanic.TutorialComplete", 1);
            CasinoDemoProgression.AwardShift(1, 2);
            if (CasinoDemoProgression.BestStars(1) != 2 || CasinoDemoProgression.UnlockedShift != 2 ||
                CasinoDemoProgression.Credits != 7)
                throw new InvalidOperationException("Campaign star records or legacy upgrade-save compatibility failed.");
            bool rejectedLockedLevel = false;
            try
            {
                CasinoDemoProgression.AwardShift(3, 1);
            }
            catch (InvalidOperationException)
            {
                rejectedLockedLevel = true;
            }
            if (!rejectedLockedLevel)
                throw new InvalidOperationException("Campaign progression awarded stars for a locked level.");
            CasinoDemoProgression.StartNewGame();
            if (CasinoDemoProgression.UnlockedShift != 1 || CasinoDemoProgression.BestStars(1) != 0 ||
                PlayerPrefs.GetInt(CasinoDemoProgression.UpgradeKeys[0], 0) != 1 ||
                PlayerPrefs.GetInt(CasinoDemoProgression.CreditsKey, 0) != 7)
                throw new InvalidOperationException("New Game must reset campaign progress without corrupting legacy save data.");
        }
        finally
        {
            Restore(CasinoDemoProgression.UnlockedLevelKey, oldValues[0]);
            Restore(CasinoDemoProgression.CreditsKey, oldValues[1]);
            for (int i = 0; i < CasinoDemoProgression.UpgradeKeys.Length; i++)
                Restore(CasinoDemoProgression.UpgradeKeys[i], oldValues[2 + i]);
            Restore("PitPanic.TutorialComplete", oldValues[5]);
            for (int level = 1; level <= CasinoDemoProgression.LevelCount; level++)
                Restore(CasinoDemoProgression.BestStarsKey(level), oldValues[5 + level]);
            PlayerPrefs.Save();
        }

        if (!CasinoDemoProgression.Passed(4, 60, 5) || CasinoDemoProgression.Passed(3, 100, 0) ||
            CasinoDemoProgression.Passed(10, 59, 0) || CasinoDemoProgression.Passed(10, 100, 6) ||
            !CasinoCampaignPresentation.LayoutsAreSeparated())
            throw new InvalidOperationException("Shift results must depend only on tasks, satisfaction and misses.");
        ValidateSecurityEvidence();
        Debug.Log("Casino campaign validation passed: 3-level unlocks, stars, legacy saves, layouts and round-derived security evidence.");
    }

    private static void ValidateSecurityEvidence()
    {
        var zeroBet = new RouletteCustomerBet(3, 1, null,
            new RouletteBet(RouletteBetType.StraightUp, 0, 500));
        if (!CasinoSecurityEvidence.TryCreate(3, 0, new[] { zeroBet }, out CasinoSecurityEvidence zeroEvidence) ||
            zeroEvidence.IsCheating || zeroEvidence.WinningNumber != 0 ||
            !ReferenceEquals(zeroEvidence.Bet, zeroBet))
            throw new InvalidOperationException("Security evidence must preserve the real zero result and actual customer bet.");

        var losingBlack = new RouletteCustomerBet(6, 1, null,
            new RouletteBet(RouletteBetType.Black, 0, 500));
        var winningSplit = new RouletteCustomerBet(6, 2, null,
            new RouletteBet(RouletteBetType.Split, new[] { 1, 2 }, 500));
        var actualBets = new List<RouletteCustomerBet> { losingBlack, winningSplit };
        if (!CasinoSecurityEvidence.TryCreate(6, 1, actualBets, out CasinoSecurityEvidence cheatingEvidence) ||
            !cheatingEvidence.IsCheating || !ReferenceEquals(cheatingEvidence.Bet, losingBlack) ||
            cheatingEvidence.AttemptedNumber != 1 || cheatingEvidence.Bet.Bet.StakePence != 500)
            throw new InvalidOperationException("Security evidence failed to link a suspicious chip movement to the actual losing wager.");

        var winningRed = new RouletteCustomerBet(6, 3, null,
            new RouletteBet(RouletteBetType.Red, 0, 500));
        if (!CasinoSecurityEvidence.TryCreate(6, 1, new[] { winningRed }, out CasinoSecurityEvidence cleanEvidence) ||
            cleanEvidence.IsCheating || !ReferenceEquals(cleanEvidence.Bet, winningRed))
            throw new InvalidOperationException("Security review should report no cheating when the actual bets provide no losing chip to move.");

        if (CasinoSecurityEvidence.TryCreate(4, 1, new[] { zeroBet }, out _))
            throw new InvalidOperationException("Security evidence was accepted from an unscheduled or mismatched round.");
    }

    private static void Restore(string key, int value)
    {
        if (value < 0) PlayerPrefs.DeleteKey(key);
        else PlayerPrefs.SetInt(key, value);
    }
}
