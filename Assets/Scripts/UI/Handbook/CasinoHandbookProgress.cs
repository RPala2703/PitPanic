using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persistent handbook unlock/read state, stored in PlayerPrefs beside the existing campaign keys.
/// Unlocks are latched once earned so starting a New Game never hides entries again.
/// </summary>
public static class CasinoHandbookProgress
{
    private const string Prefix = "PitPanic.Handbook.";
    public static string UnlockKey(string id) => Prefix + "Unlocked." + id;
    public static string ReadKey(string id) => Prefix + "Read." + id;

    public static IEnumerable<string> AllKeys()
    {
        foreach (HandbookEntry entry in CasinoHandbookContent.Load().entries)
        {
            yield return UnlockKey(entry.id);
            yield return ReadKey(entry.id);
        }
    }

    public static bool ConditionMet(HandbookEntry entry) => entry.unlock switch
    {
        HandbookUnlock.Always => true,
        HandbookUnlock.TutorialComplete => PlayerPrefs.GetInt("PitPanic.TutorialComplete", 0) != 0 ||
            CasinoDemoProgression.UnlockedShift > 1,
        HandbookUnlock.LevelUnlocked => CasinoDemoProgression.UnlockedShift >= entry.level,
        HandbookUnlock.LevelCleared => entry.level >= 1 && entry.level <= CasinoDemoProgression.LevelCount &&
            CasinoDemoProgression.BestStars(entry.level) > 0,
        _ => false
    };

    public static bool IsUnlocked(HandbookEntry entry) =>
        entry.unlock == HandbookUnlock.Always || PlayerPrefs.GetInt(UnlockKey(entry.id), 0) == 1 || ConditionMet(entry);

    public static bool IsUnread(HandbookEntry entry) => IsUnlocked(entry) && PlayerPrefs.GetInt(ReadKey(entry.id), 0) == 0;

    /// <summary>Latches every entry whose demo-progression condition is currently met.</summary>
    public static void Sync()
    {
        bool changed = false;
        foreach (HandbookEntry entry in CasinoHandbookContent.Load().entries)
        {
            if (entry.unlock == HandbookUnlock.Always || PlayerPrefs.GetInt(UnlockKey(entry.id), 0) == 1 ||
                !ConditionMet(entry)) continue;
            PlayerPrefs.SetInt(UnlockKey(entry.id), 1);
            changed = true;
        }
        if (changed) PlayerPrefs.Save();
    }

    public static void MarkRead(HandbookEntry entry)
    {
        if (!IsUnread(entry)) return;
        PlayerPrefs.SetInt(ReadKey(entry.id), 1);
        PlayerPrefs.Save();
    }

    public static bool AnyUnread(HandbookCategory? category = null)
    {
        foreach (HandbookEntry entry in CasinoHandbookContent.Load().entries)
            if ((category == null || entry.category == category) && IsUnread(entry)) return true;
        return false;
    }

    public static string LockedHint(HandbookEntry entry) => entry.unlock switch
    {
        HandbookUnlock.TutorialComplete => "Finish the training shift to unlock.",
        HandbookUnlock.LevelUnlocked => $"Unlock Level {entry.level} to read this entry.",
        HandbookUnlock.LevelCleared => $"Pass Level {entry.level} to read this entry.",
        _ => ""
    };
}
