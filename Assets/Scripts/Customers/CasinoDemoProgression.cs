using System;
using UnityEngine;

public enum CasinoDemoUpgrade { Patience, DealerRest, Packing }

public static class CasinoDemoProgression
{
    public const int LastShift = 3;
    public const int UpgradeCost = 3;
    public const string CreditsKey = "PitPanic.UpgradeCredits";
    public static readonly string[] UpgradeKeys =
        { "PitPanic.UpgradePatience", "PitPanic.UpgradeDealerRest", "PitPanic.UpgradePacking" };
    public static int Credits => Mathf.Max(0, PlayerPrefs.GetInt(CreditsKey, 0));
    public static int UnlockedShift => Mathf.Clamp(PlayerPrefs.GetInt("PitPanic.UnlockedShift", 1), 1, LastShift);
    public static bool Owns(CasinoDemoUpgrade upgrade) => PlayerPrefs.GetInt(Key(upgrade), 0) == 1;
    public static int ServiceGoal(int shift) => 8 + 4 * (Mathf.Clamp(shift, 1, LastShift) - 1);
    public static bool Passed(int shift, int hands, int tasks, int satisfaction) =>
        hands >= ServiceGoal(shift) && tasks >= 4 && satisfaction >= 60;

    public static void StartNewGame()
    {
        PlayerPrefs.DeleteKey("PitPanic.TutorialComplete");
        PlayerPrefs.DeleteKey("PitPanic.UnlockedShift");
        PlayerPrefs.DeleteKey(CreditsKey);
        foreach (string key in UpgradeKeys) PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }

    public static void AwardShift(int shift, int stars)
    {
        if (stars < 1 || stars > 3) throw new ArgumentOutOfRangeException(nameof(stars));
        PlayerPrefs.SetInt(CreditsKey, checked(Credits + stars));
        PlayerPrefs.SetInt("PitPanic.UnlockedShift", Mathf.Max(UnlockedShift, Mathf.Min(shift + 1, LastShift)));
        PlayerPrefs.Save();
    }

    public static bool TryBuy(CasinoDemoUpgrade upgrade)
    {
        string key = Key(upgrade);
        if (Owns(upgrade) || Credits < UpgradeCost) return false;
        PlayerPrefs.SetInt(CreditsKey, Credits - UpgradeCost);
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
        return true;
    }

    private static string Key(CasinoDemoUpgrade upgrade)
    {
        int index = (int)upgrade;
        if (index < 0 || index >= UpgradeKeys.Length) throw new ArgumentOutOfRangeException(nameof(upgrade));
        return UpgradeKeys[index];
    }
}
