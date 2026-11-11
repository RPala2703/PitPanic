using UnityEngine;

public static class PixelWorldProps
{
    private static Sprite cardFront;
    private static Sprite cardBack;
    private static Sprite chip;

    public static Sprite CardFront => cardFront != null ? cardFront : cardFront = MakeCard(false);
    public static Sprite CardBack => cardBack != null ? cardBack : cardBack = MakeCard(true);
    public static Sprite Chip => chip != null ? chip : chip = MakeChip();

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
                bool border = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                bool detail = back && x > 2 && x < 9 && y > 2 && y < 13 && (x + y) % 4 == 0;
                texture.SetPixel(x, y, border ? ink : detail ? Color.white : face);
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
                texture.SetPixel(x, y, mark ? new Color32(255, 245, 220, 255) :
                    rim ? new Color32(122, 122, 122, 255) : Color.white);
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
