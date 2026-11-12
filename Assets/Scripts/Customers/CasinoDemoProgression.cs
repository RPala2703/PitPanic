using System;
using UnityEngine;

public enum CasinoDemoUpgrade { Patience, DealerRest, Packing }

public static class CasinoDemoProgression
{
    public const int LevelCount = 3;
    public const int LastShift = LevelCount;
    public const int UpgradeCost = 3;
    public const string CreditsKey = "PitPanic.UpgradeCredits";
    public const string UnlockedLevelKey = "PitPanic.UnlockedShift";
    public static readonly string[] UpgradeKeys =
        { "PitPanic.UpgradePatience", "PitPanic.UpgradeDealerRest", "PitPanic.UpgradePacking" };
    public static int Credits => Mathf.Max(0, PlayerPrefs.GetInt(CreditsKey, 0));
    public static int UnlockedShift => Mathf.Clamp(PlayerPrefs.GetInt(UnlockedLevelKey, 1), 1, LastShift);
    public static string BestStarsKey(int level)
    {
        ValidateLevel(level);
        return $"PitPanic.Level{level}.BestStars";
    }
    public static int BestStars(int level) => Mathf.Clamp(PlayerPrefs.GetInt(BestStarsKey(level), 0), 0, 3);
    public static bool Owns(CasinoDemoUpgrade upgrade) => PlayerPrefs.GetInt(Key(upgrade), 0) == 1;
    public static bool Passed(int tasks, int satisfaction, int failures) =>
        tasks >= 4 && satisfaction >= 60 && failures <= 5;

    public static void StartNewGame()
    {
        PlayerPrefs.DeleteKey("PitPanic.TutorialComplete");
        PlayerPrefs.DeleteKey(UnlockedLevelKey);
        for (int level = 1; level <= LevelCount; level++)
            PlayerPrefs.DeleteKey(BestStarsKey(level));
        PlayerPrefs.Save();
    }

    public static void AwardShift(int shift, int stars)
    {
        if (stars < 1 || stars > 3) throw new ArgumentOutOfRangeException(nameof(stars));
        ValidateLevel(shift);
        if (shift > UnlockedShift)
            throw new InvalidOperationException("A locked campaign level cannot award stars.");
        PlayerPrefs.SetInt(BestStarsKey(shift), Mathf.Max(BestStars(shift), stars));
        PlayerPrefs.SetInt(UnlockedLevelKey, Mathf.Max(UnlockedShift, Mathf.Min(shift + 1, LastShift)));
        PlayerPrefs.Save();
    }

    public static void ValidateLevel(int level)
    {
        if (level < 1 || level > LevelCount)
            throw new ArgumentOutOfRangeException(nameof(level), "Campaign level must be 1, 2 or 3.");
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
