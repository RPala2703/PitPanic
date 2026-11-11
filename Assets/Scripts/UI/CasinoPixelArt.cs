using UnityEngine;
using UnityEngine.UI;

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

    public static Sprite Wheel => wheel != null ? wheel : wheel = MakeWheel();
    public static Sprite Chip => chip != null ? chip : chip = MakeDisc(false);
    public static Sprite Ball => ball != null ? ball : ball = MakeDisc(true);
    public static Sprite Card => card != null ? card : card = MakeCard();
    public static Sprite Sparkle => sparkle != null ? sparkle : sparkle = MakeSparkle();
    public static Sprite Attention => attention != null ? attention : attention = MakeAttention();

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
            byte shade = radius > 13f ? (byte)130 : radius > 10f ? (byte)240 : (byte)255;
            if (!isBall && radius > 10f && ((x / 4 + y / 4) % 3 == 0)) shade = 255;
            if (isBall && dx < -3f && dy > 3f && radius < 10f) shade = 255;
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
            pixels[y * size + x] = edge
                ? new Color32(150, 150, 150, 255)
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
        rect.localScale = Vector3.one * (0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 3f + phase));
    }
}
