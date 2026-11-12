using System;
using UnityEngine;

public static class CasinoCampaignPresentation
{
    private static readonly Vector3[,] BlackjackPositions =
    {
        { new Vector3(-6.2f, 0f, .6f), new Vector3(6.2f, 0f, .6f) },
        { new Vector3(-8f, 0f, .6f), new Vector3(8f, 0f, .6f) },
        { new Vector3(-8f, 0f, 2.8f), new Vector3(8f, 0f, 2.8f) }
    };
    private static readonly Vector3[] RoulettePositions =
    {
        new Vector3(0f, 0f, 6.4f),
        new Vector3(0f, 0f, 6.4f),
        new Vector3(0f, 0f, -3f)
    };
    private static readonly Color32[] CarpetTints =
    {
        new Color32(255, 255, 255, 255),
        new Color32(255, 222, 153, 255),
        new Color32(177, 191, 255, 255)
    };
    private static readonly Color[] SkyColors =
    {
        new Color(.48f, .55f, .62f),
        new Color(.62f, .42f, .26f),
        new Color(.20f, .25f, .48f)
    };

    public static string LevelName(int level) => level switch
    {
        1 => "OPENING NIGHT",
        2 => "HIGH ROLLERS",
        3 => "SHADY BUSINESS",
        _ => throw new ArgumentOutOfRangeException(nameof(level))
    };

    public static void Apply(int level)
    {
        CasinoDemoProgression.ValidateLevel(level);
        BlackjackTableInteraction[] blackjack = UnityEngine.Object.FindObjectsByType<BlackjackTableInteraction>(FindObjectsSortMode.None);
        BlackjackTableInteraction first = Array.Find(blackjack, table => table.TableDisplayName == "BLACKJACK 1");
        BlackjackTableInteraction second = Array.Find(blackjack, table => table.TableDisplayName == "BLACKJACK 2");
        RouletteRoundController roulette = UnityEngine.Object.FindFirstObjectByType<RouletteRoundController>();
        if (first == null || second == null || roulette == null)
            throw new InvalidOperationException("Campaign layout requires both Blackjack tables and Roulette.");

        first.transform.position = BlackjackPositions[level - 1, 0];
        second.transform.position = BlackjackPositions[level - 1, 1];
        roulette.transform.position = RoulettePositions[level - 1];
        SpriteRenderer carpet = GameObject.Find("Pixel Carpet")?.GetComponent<SpriteRenderer>();
        if (carpet != null) carpet.color = CarpetTints[level - 1];
        RenderSettings.ambientSkyColor = SkyColors[level - 1];
        RenderSettings.ambientEquatorColor = Color.Lerp(SkyColors[level - 1], new Color(.3f, .34f, .35f), .45f);
        RenderSettings.ambientGroundColor = Color.Lerp(SkyColors[level - 1], Color.black, .78f);
        foreach (Light light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type != LightType.Point) continue;
            light.color = level switch
            {
                1 => new Color(1f, .82f, .62f),
                2 => new Color(1f, .72f, .38f),
                _ => new Color(.47f, .62f, 1f)
            };
        }
    }

    public static bool IsInsideFloor(Vector3 position, float margin = 0f) =>
        Mathf.Abs(position.x) <= 15.6f - margin && Mathf.Abs(position.z) <= 10.6f - margin;

    public static bool LayoutsAreSeparated()
    {
        for (int level = 0; level < CasinoDemoProgression.LevelCount; level++)
        {
            Vector3 first = BlackjackPositions[level, 0];
            Vector3 second = BlackjackPositions[level, 1];
            Vector3 roulette = RoulettePositions[level];
            if (!IsInsideFloor(first, 3f) || !IsInsideFloor(second, 3f) ||
                !IsInsideFloor(roulette, 3f) ||
                Vector2.Distance(new Vector2(first.x, first.z), new Vector2(second.x, second.z)) < 8f ||
                Vector2.Distance(new Vector2(first.x, first.z), new Vector2(roulette.x, roulette.z)) < 5f ||
                Vector2.Distance(new Vector2(second.x, second.z), new Vector2(roulette.x, roulette.z)) < 5f)
                return false;
        }
        return true;
    }
}
