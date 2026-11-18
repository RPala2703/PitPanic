using System;
using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.AI;

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
    public static Sprite CarpetArtwork(int level)
    {
        CasinoDemoProgression.ValidateLevel(level);
        return FloorSprite("Carpet");
    }

    public static readonly string[] FloorVisualNames =
    {
        "Burgundy Gold Runner",
        "Burgundy Border NW", "Burgundy Border NE", "Burgundy Border SW", "Burgundy Border SE",
        "Burgundy Border North", "Burgundy Border South", "Burgundy Border West", "Burgundy Border East"
    };

    private static Sprite FloorSprite(string name) =>
        Resources.Load<Sprite>("Art/BurgundyFloor/" + name) ??
        throw new InvalidOperationException($"Missing supplied burgundy flooring crop: {name}.");

    public static void ApplyCarpet(SpriteRenderer carpet, int level)
    {
        carpet.sprite = CarpetArtwork(level);
        carpet.drawMode = SpriteDrawMode.Tiled;
        carpet.tileMode = SpriteTileMode.Continuous;
        carpet.size = new Vector2(38, 22);
        carpet.transform.localScale = Vector3.one;
        carpet.color = Color.white;
        carpet.GetComponent<PixelSpriteTextureBinding>()?.RefreshSprite();
        Transform floor = carpet.transform.parent;
        CasinoCashDeskInteraction cashDesk = UnityEngine.Object.FindFirstObjectByType<CasinoCashDeskInteraction>();
        RouletteRoundController roulette = UnityEngine.Object.FindFirstObjectByType<RouletteRoundController>();
        if (cashDesk == null || roulette == null)
            throw new InvalidOperationException("The carpet aisle requires the actual cashier and Roulette.");
        float start = cashDesk.transform.position.z;
        float end = roulette.transform.position.z;
        ConfigureFloorVisual(floor, FloorVisualNames[0], FloorSprite("Runner"),
            new Vector2(103f / 32, Mathf.Abs(end - start)),
            new Vector3(0, .02f, (start + end) * .5f), -98);
        Sprite corner = FloorSprite("Corner");
        const float trim = 14f / 32;
        for (int i = 0; i < 4; i++)
            ConfigureFloorVisual(floor, FloorVisualNames[i + 1], corner, new Vector2(trim, trim),
                new Vector3((i % 2 == 0 ? -1 : 1) * (19 - trim * .5f),
                    .015f, (i < 2 ? 1 : -1) * (11 - trim * .5f)),
                -99, i % 2 != 0, i >= 2);
        Sprite horizontal = FloorSprite("BorderHorizontal");
        Sprite vertical = FloorSprite("BorderVertical");
        ConfigureFloorVisual(floor, FloorVisualNames[5], horizontal, new Vector2(38 - trim * 2, trim),
            new Vector3(0, .015f, 11 - trim * .5f), -99);
        ConfigureFloorVisual(floor, FloorVisualNames[6], horizontal, new Vector2(38 - trim * 2, trim),
            new Vector3(0, .015f, -11 + trim * .5f), -99, false, true);
        ConfigureFloorVisual(floor, FloorVisualNames[7], vertical, new Vector2(trim, 22 - trim * 2),
            new Vector3(-19 + trim * .5f, .015f, 0), -99);
        ConfigureFloorVisual(floor, FloorVisualNames[8], vertical, new Vector2(trim, 22 - trim * 2),
            new Vector3(19 - trim * .5f, .015f, 0), -99, true);
        SpriteRenderer wall = carpet.transform.parent.Find("Casino Back Wall")?.GetComponent<SpriteRenderer>();
        if (wall == null) throw new InvalidOperationException("The saved casino backdrop requires its north wall.");
        wall.enabled = false;
    }

    private static void ConfigureFloorVisual(Transform floor, string name, Sprite sprite, Vector2 size,
        Vector3 position, int order, bool flipX = false, bool flipY = false)
    {
        SpriteRenderer renderer = floor.Find(name)?.GetComponent<SpriteRenderer>();
        if (renderer == null) throw new InvalidOperationException($"The saved floor is missing {name}.");
        renderer.transform.localPosition = position;
        renderer.transform.localRotation = Quaternion.Euler(90, 0, 0);
        renderer.transform.localScale = Vector3.one;
        renderer.sprite = sprite;
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.tileMode = SpriteTileMode.Continuous;
        renderer.size = size;
        renderer.flipX = flipX;
        renderer.flipY = flipY;
        renderer.sortingOrder = order;
        renderer.color = Color.white;
        renderer.GetComponent<PixelSpriteTextureBinding>().RefreshSprite();
    }
    private static readonly Color[] SkyColors =
    {
        new Color(.48f, .55f, .62f),
        new Color(.62f, .42f, .26f),
        new Color(.20f, .25f, .48f)
    };
    private static readonly Color[] SceneryTints =
    {
        Color.white,
        new Color(1f, .91f, .74f),
        new Color(.78f, .84f, 1f)
    };
    private static readonly Color[] TableRugTints =
    {
        Color.white,
        new Color(.80f, .96f, .83f),
        new Color(.84f, .81f, 1f)
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
        // The saved mesh belongs to Opening Night; moved table footprints need fresh paths.
        NavMeshSurface navigation = UnityEngine.Object.FindFirstObjectByType<NavMeshSurface>();
        if (navigation == null) throw new InvalidOperationException("Campaign layout requires a navigation surface.");
        navigation.BuildNavMesh();
        foreach (AIDealer dealer in UnityEngine.Object.FindObjectsByType<AIDealer>(FindObjectsSortMode.None))
        {
            if (!dealer.IsAtStation || dealer.AssignedStation == null) continue;
            Transform station = dealer.AssignedStation.DealerPosition;
            NavMeshAgent agent = dealer.GetComponent<NavMeshAgent>();
            if (!agent.Warp(station.position))
                throw new InvalidOperationException($"Cannot move {dealer.name} with its campaign table.");
            dealer.SnapToStation();
        }
        foreach (NavMeshAgent agent in UnityEngine.Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
        {
            if (!agent.enabled || agent.isOnNavMesh) continue;
            if (!NavMesh.SamplePosition(agent.transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas) ||
                !agent.Warp(hit.position))
                throw new InvalidOperationException($"Cannot place {agent.name} on the selected casino layout.");
        }
        SpriteRenderer carpet = GameObject.Find("Pixel Carpet")?.GetComponent<SpriteRenderer>();
        if (carpet != null)
        {
            ApplyCarpet(carpet, level);
        }
        ApplySceneryPalette(SceneryTints[level - 1], TableRugTints[level - 1]);
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

    private static void ApplySceneryPalette(Color tint, Color rugTint)
    {
        foreach (SpriteRenderer renderer in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
        {
            string name = renderer.gameObject.name;
            if (name == "Pixel Column" || name == "Pixel Window" || name == "Pixel Lamp" ||
                name == "Pixel Plant" || name == "Pixel Curtain" || name == "Pixel Sofa")
                renderer.color = tint;
            else if (name == "Table Rug")
                renderer.color = rugTint;
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
