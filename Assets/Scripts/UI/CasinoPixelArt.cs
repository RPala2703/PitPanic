using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public static class CasinoPixelArt
{
    public static readonly int[] WheelOrder =
    {
        0, 32, 15, 19, 4, 21, 2, 25, 17, 34, 6, 27, 13, 36, 11, 30, 8, 23, 10,
        5, 24, 16, 33, 1, 20, 14, 31, 9, 22, 18, 29, 7, 28, 12, 35, 3, 26
    };

    private static Sprite wheel;
    private static Sprite chip;
    private static Sprite ball;
    private static Sprite card;
    private static Sprite sparkle;
    private static Sprite attention;
    private static Sprite burst;
    private static Sprite interactionBackdrop;
    private static readonly Dictionary<int, Sprite> denominationChips = new Dictionary<int, Sprite>();
    public static Sprite ChipForAmount(int amountPence) => ChipForValue(
        amountPence >= 10000 ? 10000 : amountPence >= 2500 ? 2500 : amountPence >= 500 ? 500 : 250);

    public static Sprite ChipForValue(int valuePence)
    {
        if (denominationChips.TryGetValue(valuePence, out Sprite sprite)) return sprite;
        Color face = BlackjackChipView.GetChipColor(valuePence);
        Color32[] pixels = new Color32[32 * 32];
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
        {
            float dx = x - 15.5f, dy = y - 15.5f;
            float radius = Mathf.Sqrt(dx * dx + dy * dy);
            if (radius > 15) continue;
            float angle = Mathf.Atan2(dy, dx);
            float bevel = Mathf.Clamp01((15 - radius) / 3f);
            bool stripe = Mathf.Cos(angle * 8) > .35f;
            bool tick = radius > 10 && radius < 14 && Mathf.Abs(Mathf.Sin(angle * 8)) < .19f;
            Color color = radius > 14 ? new Color(.07f, .045f, .055f) :
                radius > 12 && (stripe || tick) ? new Color(1f, .88f, .59f) :
                radius > 10 ? Color.Lerp(face, new Color(.22f, .13f, .09f), .45f) :
                radius > 8 ? Color.Lerp(face, Color.white, .46f) :
                radius > 3 ? Color.Lerp(face, dy > 0 ? Color.white : Color.black, .08f + bevel * .2f) :
                new Color(.1f, .075f, .075f);
            pixels[y * 32 + x] = color;
        }
        for (int x = 13; x <= 18; x++)
        for (int y = 13; y <= 18; y++)
        {
            float dx = x - 15.5f, dy = y - 15.5f;
            if (dx * dx + dy * dy <= 4f) pixels[y * 32 + x] = Color.Lerp(face, Color.white, .34f);
        }
        sprite = MakeSprite($"Chip {valuePence}p", 32, pixels);
        denominationChips.Add(valuePence, sprite);
        return sprite;
    }

    public static Sprite Wheel => wheel != null ? wheel : wheel = MakeWheel();
    public static Sprite Chip => chip != null ? chip : chip = MakeDisc(false);
    public static Sprite Ball => ball != null ? ball : ball = MakeDisc(true);
    public static Sprite Card => card != null ? card : card = MakeCard();
    public static Sprite Sparkle => sparkle != null ? sparkle : sparkle = MakeSparkle();
    public static Sprite Attention => attention != null ? attention : attention = MakeAttention();
    public static Sprite Burst => burst != null ? burst : burst = MakeBurst();
    public static Sprite InteractionBackdrop => interactionBackdrop != null
        ? interactionBackdrop : interactionBackdrop = MakeInteractionBackdrop();

    public static void SetSprite(Image image, Sprite sprite, Color color)
    {
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = color;
        image.preserveAspect = true;
    }

    public static void AddSparkle(Transform parent, Vector2 position, float size)
    {
        GameObject sparkleObject = CasinoMinigamePresentation.CreateImage(
            "Pixel Sparkle", parent, position, new Vector2(size, size), CasinoHudStyle.Gold);
        SetSprite(sparkleObject.GetComponent<Image>(), Sparkle, CasinoHudStyle.Gold);
        sparkleObject.AddComponent<CasinoPixelSparkle>();
    }

    public static GameObject AddInteractionBackdrop(Transform parent)
    {
        GameObject backdrop = CasinoMinigamePresentation.CreateImage(
            "Original casino salon backdrop", parent, Vector2.zero, Vector2.one, Color.white);
        RectTransform rect = backdrop.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        SetSprite(backdrop.GetComponent<Image>(), InteractionBackdrop, Color.white);
        backdrop.transform.SetAsFirstSibling();
        return backdrop;
    }

    private static Sprite MakeInteractionBackdrop()
    {
        const int width = 320;
        const int height = 180;
        Color32[] pixels = new Color32[width * height];
        Color32 ink = new Color32(35, 18, 35, 255);
        Color32 plum = new Color32(69, 28, 54, 255);
        Color32 wall = new Color32(91, 39, 61, 255);
        Color32 rose = new Color32(137, 47, 70, 255);
        Color32 gold = new Color32(245, 169, 57, 255);
        Color32 light = new Color32(255, 222, 129, 255);
        Color32 emerald = new Color32(31, 99, 69, 255);
        Color32 greenLight = new Color32(72, 143, 82, 255);
        Fill(pixels, width, height, ink);
        Rect(pixels, width, 0, 0, width, 31, new Color32(61, 27, 47, 255));
        Rect(pixels, width, 0, 32, width, height - 32, wall);
        for (int y = 42; y < 176; y += 14)
        {
            Rect(pixels, width, 0, y, width, 1, new Color32(167, 73, 78, 255));
            int offset = (y / 14 & 1) * 20;
            for (int x = offset; x < width; x += 40)
                Rect(pixels, width, x, y + 1, 1, 12, new Color32(48, 24, 43, 255));
        }
        Rect(pixels, width, 0, 31, width, 3, gold);
        Rect(pixels, width, 0, 35, width, 2, new Color32(105, 46, 58, 255));

        for (int x = 56; x <= 264; x += 52)
        {
            Rect(pixels, width, x, 47, 42, 3, ink);
            Rect(pixels, width, x + 3, 50, 36, 36, gold);
            Rect(pixels, width, x + 5, 52, 32, 32, new Color32(67, 32, 54, 255));
            Rect(pixels, width, x + 8, 55, 26, 26, new Color32(106, 52, 73, 255));
            Rect(pixels, width, x + 10, 58, 22, 1, new Color32(220, 127, 91, 255));
            Rect(pixels, width, x + 10, 75, 22, 1, new Color32(220, 127, 91, 255));
            Diamond(pixels, width, x + 21, 67, 5, gold);
            Diamond(pixels, width, x + 21, 67, 2, new Color32(190, 84, 83, 255));
        }

        DrawCurtain(pixels, width, true, ink, plum, rose, gold);
        DrawCurtain(pixels, width, false, ink, plum, rose, gold);
        DrawSconce(pixels, width, 36, 107, ink, gold, light);
        DrawSconce(pixels, width, 284, 107, ink, gold, light);
        DrawPalm(pixels, width, 24, 28, emerald, greenLight, gold, ink);
        DrawPalm(pixels, width, 296, 28, emerald, greenLight, gold, ink);

        Rect(pixels, width, 0, 0, width, 29, new Color32(52, 22, 43, 255));
        for (int x = 0; x < width; x += 24)
        {
            Rect(pixels, width, x, 1, 20, 1, new Color32(128, 45, 70, 255));
            int patternX = x + 12;
            Diamond(pixels, width, patternX, 10, 5, new Color32(111, 40, 69, 255));
            Pixel(pixels, width, patternX, 10, new Color32(199, 98, 79, 255));
        }
        Rect(pixels, width, 0, 0, width, 3, gold);
        Rect(pixels, width, 0, 3, width, 1, new Color32(255, 214, 116, 255));
        for (int x = 6; x < width; x += 8)
        {
            Rect(pixels, width, x, 3, 2, 4, light);
            Rect(pixels, width, x - 1, 4, 4, 2, new Color32(255, 196, 74, 255));
        }

        Rect(pixels, width, 0, 24, width, 2, gold);
        Rect(pixels, width, 0, 27, width, 1, new Color32(151, 62, 69, 255));
        for (int x = 0; x < width; x += 32)
        {
            Diamond(pixels, width, x + 16, 15, 5, new Color32(115, 49, 66, 255));
            Diamond(pixels, width, x + 16, 15, 2, new Color32(174, 76, 80, 255));
            Pixel(pixels, width, x + 16, 15, light);
        }
        Rect(pixels, width, 0, 29, width, 2, gold);
        for (int x = 9; x < width; x += 32)
            Rect(pixels, width, x, 25, 2, 2, light);

        // Deep red velvet, gold footlights and a restrained damask floor.
        Rect(pixels, width, 0, 0, width, 22, new Color32(69, 25, 47, 255));
        Rect(pixels, width, 0, 20, width, 2, gold);
        for (int y = 2; y < 20; y += 9)
        for (int x = 0; x < width; x += 18)
        {
            int cx = x + ((y / 9) & 1) * 9;
            Diamond(pixels, width, cx, y + 2, 3, new Color32(100, 36, 59, 255));
            Pixel(pixels, width, cx, y + 2, new Color32(184, 75, 75, 255));
        }
        Rect(pixels, width, 0, 0, 2, 32, gold);
        Rect(pixels, width, width - 2, 0, 2, 32, gold);
        Pixel(pixels, width, 3, 24, light);
        Pixel(pixels, width, width - 4, 24, light);

        return MakeSprite("Original velvet casino salon", width, height, pixels);
    }

    private static void DrawCurtain(Color32[] pixels, int width, bool left, Color32 ink,
        Color32 plum, Color32 rose, Color32 gold)
    {
        for (int y = 30; y < 180; y++)
        {
            int drop = y - 30;
            int depth = 22 + drop / 7;
            int x = left ? depth : width - 1 - depth;
            for (int fold = -1; fold <= 1; fold++)
            {
                int foldX = x + fold * 7 * (left ? 1 : -1);
                Rect(pixels, width, foldX, y, 4, 1,
                    fold == 0 ? rose : fold == 1 ? plum : new Color32(104, 35, 65, 255));
                Pixel(pixels, width, foldX + 1, y, new Color32(178, 61, 82, 255));
            }
            if (drop > 90 && drop % 8 == 0)
                Rect(pixels, width, left ? 1 : width - 4, y, 3, 2, new Color32(151, 57, 72, 255));
        }
        for (int x = 0; x < 42; x += 7)
        {
            int px = left ? x : width - 1 - x;
            Rect(pixels, width, px, 0, 5, 31, x % 14 == 0 ? rose : plum);
            Rect(pixels, width, px, 26, 5, 4, gold);
        }
        Rect(pixels, width, left ? 33 : width - 35, 29, 2, 131, gold);
        Rect(pixels, width, left ? 35 : width - 37, 29, 2, 131, ink);
    }

    private static void DrawSconce(Color32[] pixels, int width, int x, int y,
        Color32 ink, Color32 gold, Color32 light)
    {
        Rect(pixels, width, x - 4, y - 13, 8, 26, ink);
        Rect(pixels, width, x - 3, y - 11, 6, 20, gold);
        Rect(pixels, width, x - 2, y - 8, 4, 14, light);
        Rect(pixels, width, x - 8, y - 14, 16, 3, gold);
        Rect(pixels, width, x - 6, y + 8, 12, 3, gold);
        Rect(pixels, width, x - 2, y - 19, 4, 6, ink);
        Pixel(pixels, width, x - 1, y - 17, light);
    }

    private static void DrawPalm(Color32[] pixels, int width, int x, int y,
        Color32 darkLeaf, Color32 brightLeaf, Color32 gold, Color32 ink)
    {
        Rect(pixels, width, x - 5, y, 10, 15, ink);
        Rect(pixels, width, x - 4, y + 1, 8, 12, new Color32(117, 55, 43, 255));
        Rect(pixels, width, x - 3, y + 1, 2, 9, gold);
        Rect(pixels, width, x - 1, y + 13, 2, 28, new Color32(120, 81, 49, 255));
        for (int i = 0; i < 7; i++)
        {
            int side = i % 2 == 0 ? -1 : 1;
            int leafY = y + 24 + i * 3;
            int leafX = x + side * (8 + i * 2);
            Rect(pixels, width, Mathf.Clamp(leafX, 0, width - 7), leafY, 8, 3, darkLeaf);
            Rect(pixels, width, Mathf.Clamp(leafX + (side < 0 ? 1 : 4), 0, width - 5),
                leafY + 1, 5, 1, brightLeaf);
            Pixel(pixels, width, x + side * 4, leafY + 1, new Color32(142, 194, 101, 255));
        }
        Rect(pixels, width, x - 16, y + 40, 32, 2, new Color32(48, 30, 45, 255));
    }

    private static void Fill(Color32[] pixels, int width, int height, Color32 color)
    {
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
    }

    private static void Rect(Color32[] pixels, int width, int x, int y, int w, int h, Color32 color)
    {
        int maxX = Mathf.Min(width, x + w);
        int maxY = Mathf.Min(pixels.Length / width, y + h);
        for (int py = Mathf.Max(0, y); py < maxY; py++)
        for (int px = Mathf.Max(0, x); px < maxX; px++) pixels[py * width + px] = color;
    }

    private static void Pixel(Color32[] pixels, int width, int x, int y, Color32 color)
    {
        int height = pixels.Length / width;
        if (x >= 0 && x < width && y >= 0 && y < height) pixels[y * width + x] = color;
    }

    private static void Diamond(Color32[] pixels, int width, int cx, int cy, int radius, Color32 color)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            int span = radius - Mathf.Abs(dy);
            Rect(pixels, width, cx - span, cy + dy, span * 2 + 1, 1, color);
        }
    }

    private static Sprite MakeWheel()
    {
        const int size = 192;
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - size * 0.5f;
                float dy = y + 0.5f - size * 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                Color32 color = new Color32(0, 0, 0, 0);
                if (r < 94f)
                {
                    if (r >= 90f) color = new Color32(51, 30, 32, 255);
                    else if (r >= 84f) color = new Color32(222, 164, 73, 255);
                    else if (r >= 79f) color = new Color32(106, 57, 48, 255);
                    else if (r >= 50f)
                    {
                        float angle = Mathf.Repeat(90f - Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 180f / 37f, 360f);
                        int index = Mathf.FloorToInt(angle / (360f / 37f));
                        int number = WheelOrder[index];
                        float within = Mathf.Repeat(angle, 360f / 37f);
                        color = within < 0.6f
                            ? new Color32(238, 194, 110, 255)
                            : number == 0 ? new Color32(41, 143, 102, 255)
                            : EuropeanRouletteRules.IsRed(number) ? new Color32(193, 57, 66, 255)
                            : new Color32(42, 48, 61, 255);
                    }

                    else if (r >= 46f) color = new Color32(236, 186, 98, 255);
                    else if (r >= 39f) color = new Color32(111, 58, 50, 255);
                    else if (r >= 12f) color = new Color32(43, 123, 98, 255);
                    else if (r >= 8f) color = new Color32(235, 184, 90, 255);
                    else color = new Color32(255, 230, 160, 255);
                }
                pixels[y * size + x] = color;
            }
        }
        return MakeSprite("Roulette wheel pixel art", size, pixels);
    }

    private static Sprite MakeDisc(bool isBall)
    {
        const int size = 32;
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x + 0.5f - 16f;
            float dy = y + 0.5f - 16f;
            float radius = Mathf.Sqrt(dx * dx + dy * dy);
            if (radius >= 15f) continue;
            byte shade = radius > 13f ? (byte)100 : radius > 10f ? (byte)205 : (byte)245;
            if (!isBall && radius > 10f && ((x / 4 + y / 4) % 3 == 0)) shade = 255;
            if (!isBall && radius < 8 && (Mathf.Abs(dx) < 2 || Mathf.Abs(dy) < 2)) shade = 210;
            if (dy < -5 && radius < 13) shade = (byte)Mathf.Max(100, shade - 25);
            if (dx < -3f && dy > 3f && radius < 10f) shade = 255;
            pixels[y * size + x] = new Color32(shade, shade, shade, 255);
        }
        return MakeSprite(isBall ? "Roulette ball pixel art" : "Casino chip pixel art", size, pixels);
    }

    private static Sprite MakeCard()
    {
        const int size = 32;
        Color32[] pixels = new Color32[size * size];
        Color32 ink = new Color32(42, 27, 38, 255);
        Color32 gold = new Color32(222, 169, 79, 255);
        Color32 cream = new Color32(255, 242, 207, 255);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int edge = Mathf.Min(x, y, size - 1 - x, size - 1 - y);
            if (edge == 0 && (x < 3 || x > 28) && (y < 3 || y > 28)) continue;
            pixels[y * size + x] = edge switch
            {
                0 => ink,
                1 => gold,
                2 => cream,
                3 => new Color32(237, 219, 174, 255),
                _ => new Color32(255, 250, 229, 255)
            };
        }
        for (int i = 0; i < 4; i++)
        {
            int x = i < 2 ? 7 : 24;
            int y = i % 2 == 0 ? 7 : 24;
            pixels[y * size + x] = gold;
            pixels[y * size + x + 1] = gold;
            pixels[(y + 1) * size + x] = gold;
            pixels[(y + 1) * size + x + 1] = cream;
        }
        for (int i = 0; i < 5; i++)
        {
            pixels[(13 + i) * size + (14 + i)] = gold;
            pixels[(18 - i) * size + (14 + i)] = gold;
        }
        return MakeSprite("Playing card pixel art", size, pixels);
    }

    private static Sprite MakeSparkle()
    {
        const int size = 16;
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int dx = Mathf.Abs(x * 2 - 15);
            int dy = Mathf.Abs(y * 2 - 15);
            if ((dx <= 3 && dy <= 13) || (dy <= 3 && dx <= 13) || (dx <= 5 && dy <= 5))
                pixels[y * size + x] = new Color32(255, 255, 255, 255);
        }
        return MakeSprite("Casino sparkle pixel art", size, pixels);
    }

    private static Sprite MakeAttention()
    {
        const int size = 32;
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int dx = Mathf.Abs(x * 2 - 31);
            int dy = Mathf.Abs(y * 2 - 31);
            if (Mathf.Max(dx, dy) > 27 || dx + dy > 42) continue;
            bool edge = Mathf.Max(dx, dy) >= 24 || dx + dy >= 38;
            bool mark = x >= 14 && x <= 17 && ((y >= 12 && y <= 24) || (y >= 6 && y <= 9));
            pixels[y * size + x] = edge ? new Color32(92, 40, 50, 255)
                : mark ? new Color32(255, 255, 240, 255)
                : new Color32(244, 148, 42, 255);
        }
        return MakeSprite("Attention badge pixel art", size, pixels);
    }

    private static Sprite MakeBurst()
    {
        const int size = 64;
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x + 0.5f - size * 0.5f;
            float dy = y + 0.5f - size * 0.5f;
            float angle = Mathf.Atan2(dy, dx);
            float radius = Mathf.Sqrt(dx * dx + dy * dy);
            float edge = 22f + 8f * Mathf.Abs(Mathf.Cos(angle * 6f));
            if (radius < edge)
                pixels[y * size + x] = new Color32(255, 255, 255, 255);
        }
        return MakeSprite("Arcade burst pixel art", size, pixels);
    }

    private static Sprite MakeSprite(string spriteName, int size, Color32[] pixels)
    {
        return MakeSprite(spriteName, size, size, pixels);
    }

    private static Sprite MakeSprite(string spriteName, int width, int height, Color32[] pixels)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = spriteName,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
    }
}

public sealed class CasinoPixelSparkle : MonoBehaviour
{
    private RectTransform rect;
    private float phase;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        phase = rect.anchoredPosition.x < 0f ? 0f : Mathf.PI;
    }

    private void Update()
    {
        if (CasinoShiftController.GameplayFrozen) return;
        if (AudioListener.pause) return;
        rect.localScale = Vector3.one * (0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 3f + phase));
    }
}
