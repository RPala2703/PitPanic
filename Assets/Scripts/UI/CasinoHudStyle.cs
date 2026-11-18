using UnityEngine;
using UnityEngine.UI;

public static class CasinoHudStyle
{
    public static readonly Color Ink = new Color(0.10f, 0.07f, 0.15f, 0.97f);
    public static readonly Color Gold = new Color(1f, 0.78f, 0.34f);
    public static readonly Color Mint = new Color(0.56f, 0.94f, 0.74f);
    public static readonly Color Coral = new Color(1f, 0.55f, 0.44f);
    private static Sprite panelSprite;

    public static void Panel(Image image, Color color)
    {
        image.color = color;
        if (Application.isPlaying)
        {
            image.sprite = GetPanelSprite();
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 0.35f;
            Outline outline = image.GetComponent<Outline>();
            if (outline == null) outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.75f, .47f, .2f, .7f);
            outline.effectDistance = new Vector2(2, -2);
        }
    }

    public static void Button(Button button)
    {
        if (Application.isPlaying && button.GetComponent<CasinoButtonFeedback>() == null)
            button.gameObject.AddComponent<CasinoButtonFeedback>();
        if (button.targetGraphic is Image image)
            Panel(image, image.color);
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.91f, 0.69f);
        colors.pressedColor = new Color(0.8f, 0.6f, 0.7f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.65f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    private static Sprite GetPanelSprite()
    {
        if (panelSprite != null)
            return panelSprite;
        const int size = 24;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Original casino velvet enamel frame",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int left = x, right = size - 1 - x, bottom = y, top = size - 1 - y;
                int edge = Mathf.Min(left, right, bottom, top);
                bool cutCorner = left + bottom < 3 || right + bottom < 3 ||
                    left + top < 3 || right + top < 3;
                byte shade = edge switch
                {
                    0 => 48,
                    1 => 104,
                    2 => 222,
                    3 => 171,
                    4 => 255,
                    _ => 255
                };
                pixels[y * size + x] = cutCorner
                    ? new Color32(0, 0, 0, 0)
                    : new Color32(shade, shade, shade, 255);
            }
        }

        DrawDiamond(pixels, size, 3, 3, 1, 150);
        DrawDiamond(pixels, size, size - 4, 3, 1, 150);
        DrawDiamond(pixels, size, 3, size - 4, 1, 150);
        DrawDiamond(pixels, size, size - 4, size - 4, 1, 150);
        texture.SetPixels32(pixels);
        texture.Apply();
        panelSprite = Sprite.Create(texture, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(6, 6, 6, 6));
        return panelSprite;
    }

    private static void DrawDiamond(Color32[] pixels, int size, int cx, int cy, int radius, byte shade)
    {
        for (int y = -radius; y <= radius; y++)
        {
            int width = radius - Mathf.Abs(y);
            for (int x = -width; x <= width; x++)
            {
                int px = cx + x;
                int py = cy + y;
                if (px >= 0 && px < size && py >= 0 && py < size)
                    pixels[py * size + px] = new Color32(shade, shade, shade, 255);
            }
        }
    }
}
