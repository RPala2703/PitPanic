using System;
using UnityEngine;

public enum HandbookCategory { Staff, Guests, Equipment, HouseRules }

public enum HandbookUnlock
{
    Always,
    TutorialComplete,
    LevelUnlocked,
    LevelCleared
}

[Serializable]
public sealed class HandbookEntry
{
    [Tooltip("Stable save identifier. Changing it resets this entry's unlock and read state.")]
    public string id;
    public HandbookCategory category;
    public string title;
    [TextArea(3, 8)] public string description;
    [Tooltip("Existing character, prop or UI sprite shown beside the entry.")]
    public Sprite image;
    public HandbookUnlock unlock;
    [Tooltip("Campaign level for LevelUnlocked and LevelCleared rules.")]
    public int level;
}

/// <summary>Editable Employee Handbook copy. Lives in Resources/Handbook so content stays out of code.</summary>
[CreateAssetMenu(menuName = "Pit Panic/Employee Handbook Content", fileName = "EmployeeHandbook")]
public sealed class CasinoHandbookContent : ScriptableObject
{
    public const string ResourcePath = "Handbook/EmployeeHandbook";
    public HandbookEntry[] entries = Array.Empty<HandbookEntry>();

    private static CasinoHandbookContent cached;

    public static CasinoHandbookContent Load()
    {
        if (cached != null) return cached;
        cached = Resources.Load<CasinoHandbookContent>(ResourcePath);
        if (cached == null) throw new InvalidOperationException("Missing Employee Handbook content: Resources/" + ResourcePath);
        return cached;
    }

    public static string CategoryLabel(HandbookCategory category) => category switch
    {
        HandbookCategory.Staff => "STAFF",
        HandbookCategory.Guests => "GUESTS",
        HandbookCategory.Equipment => "EQUIPMENT",
        _ => "HOUSE RULES"
    };
}
