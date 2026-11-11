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
        const int size = 16;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Pixel HUD panel",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int edge = Mathf.Min(x, y, size - 1 - x, size - 1 - y);
                bool cutCorner = (x < 2 || x >= size - 2) && (y < 2 || y >= size - 2);
                byte shade = edge == 0 ? (byte)80 : edge == 1 ? (byte)190 : (byte)255;
                pixels[y * size + x] = cutCorner
                    ? new Color32(0, 0, 0, 0)
                    : new Color32(shade, shade, shade, 255);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        panelSprite = Sprite.Create(texture, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));
        return panelSprite;
    }
}
