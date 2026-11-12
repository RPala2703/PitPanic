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
            bool stripe = Mathf.Cos(angle * 8) > .35f;
            Color color = radius > 14 ? CasinoHudStyle.Ink :
                radius > 10 && stripe ? new Color(1f, .94f, .8f) :
                radius > 8 && radius < 10 ? Color.Lerp(face, Color.white, .4f) :
                Color.Lerp(face, dy > 0 ? Color.white : Color.black, .12f);
            pixels[y * 32 + x] = color;
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
        for (int y = 1; y < size - 1; y++)
        for (int x = 1; x < size - 1; x++)
        {
            if ((x == 1 || x == size - 2) && (y == 1 || y == size - 2)) continue;
            bool edge = x < 3 || y < 3 || x >= size - 3 || y >= size - 3;
            bool inset = x == 5 || y == 5 || x == size - 6 || y == size - 6;
            pixels[y * size + x] = edge
                ? new Color32(110, 110, 110, 255)
                : inset ? new Color32(215, 215, 215, 255)
                : new Color32(255, 255, 255, 255);
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
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = spriteName,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
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
        if (AudioListener.pause) return;
        rect.localScale = Vector3.one * (0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 3f + phase));
    }
}
