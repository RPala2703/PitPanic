using UnityEngine;

public static class PixelWorldProps
{
    private static Sprite cardFront;
    private static Sprite cardBack;
    private static Sprite chip;
    private static Sprite floatTray;

    public static Sprite CardFront => cardFront != null ? cardFront : cardFront = MakeCard(false);
    public static Sprite CardBack => cardBack != null ? cardBack : cardBack = MakeCard(true);
    public static Sprite Chip => chip != null ? chip : chip = MakeChip();
    public static Sprite FloatTray => floatTray != null ? floatTray : floatTray = MakeFloatTray();

    private static Sprite MakeFloatTray()
    {
        Texture2D texture = NewTexture(32, 24);
        for (int y = 1; y < 23; y++)
        for (int x = 1; x < 31; x++)
        {
            if ((x <= 2 || x >= 29) && (y <= 2 || y >= 21)) continue;
            bool edge = x <= 2 || x >= 29 || y <= 2 || y >= 21;
            bool rail = x <= 4 || x >= 27 || y <= 4 || y >= 19;
            texture.SetPixel(x, y, edge ? new Color32(49, 30, 48, 255) :
                rail ? new Color32(235, 179, 71, 255) : new Color32(36, 101, 78, 255));
        }
        return Finish(texture, 32, 24);
    }

    public static SpriteRenderer CreateSurface(Transform parent, string name, Sprite sprite, Vector2 size, int order)
    {
        GameObject surface = new GameObject(name);
        surface.transform.SetParent(parent, false);
        surface.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        surface.transform.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer renderer = surface.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        surface.AddComponent<PixelSpriteTextureBinding>();
        return renderer;
    }

    private static Sprite MakeCard(bool back)
    {
        const int width = 12;
        const int height = 16;
        Color32 ink = new Color32(29, 24, 43, 255);
        Color32 face = back ? new Color32(151, 51, 74, 255) : new Color32(255, 242, 215, 255);
        Texture2D texture = NewTexture(width, height);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if ((x == 0 || x == width - 1) && (y == 0 || y == height - 1)) continue;
                bool border = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                bool detail = back && x > 2 && x < 9 && y > 2 && y < 13 && (x + y) % 4 == 0;
                bool emblem = back && Mathf.Abs(x - 6) + Mathf.Abs(y - 8) <= 2;
                bool shine = x == 1 || y == height - 2;
                texture.SetPixel(x, y, border ? ink : emblem ? new Color32(244, 192, 91, 255) :
                    detail ? new Color32(198, 98, 122, 255) : shine ? new Color32(255, 246, 218, 255) : face);
            }
        return Finish(texture, width, height);
    }

    private static Sprite MakeChip()
    {
        const int size = 16;
        Texture2D texture = NewTexture(size, size);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int dx = Mathf.Abs(x * 2 - 15);
                int dy = Mathf.Abs(y * 2 - 15);
                if (Mathf.Max(dx, dy) > 13 || dx + dy > 21) continue;
                bool rim = Mathf.Max(dx, dy) >= 11 || dx + dy >= 18;
                bool mark = rim && (dx <= 3 || dy <= 3);
                bool outline = Mathf.Max(dx, dy) >= 13 || dx + dy >= 20;
                bool center = dx + dy <= 5;
                texture.SetPixel(x, y, outline ? new Color32(68, 55, 79, 255) :
                    mark ? new Color32(255, 245, 220, 255) :
                    rim ? new Color32(152, 137, 163, 255) : center ? new Color32(210, 201, 221, 255) :
                    y > 9 ? Color.white : new Color32(235, 225, 245, 255));
            }
        return Finish(texture, size, size);
    }

    private static Texture2D NewTexture(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(new Color32[width * height]);
        return texture;
    }

    private static Sprite Finish(Texture2D texture, int width, int height)
    {
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), width);
    }
}
