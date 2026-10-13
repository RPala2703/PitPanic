using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BlackjackChipView : MonoBehaviour
{
    private const int ChipTextureSize = 64;
    private static readonly Color[] ChipColors =
    {
        new Color(0.81f, 0.16f, 0.13f),
        new Color(0.16f, 0.43f, 0.78f),
        new Color(0.20f, 0.62f, 0.34f)
    };
    private static Sprite chipSprite;

    private Image image;
    private Text label;

    public void Configure(int valuePence, Font font, Vector2 size, bool showValue = true)
    {
        image = gameObject.AddComponent<Image>();
        image.sprite = GetChipSprite();
        image.raycastTarget = false;
        RectTransform rect = GetComponent<RectTransform>();
        rect.sizeDelta = size;
        SetValue(valuePence);
        AddChipMarks(size);

        if (!showValue)
        {
            return;
        }

        GameObject labelObject = new GameObject("Denomination", typeof(RectTransform));
        labelObject.transform.SetParent(transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        label = labelObject.AddComponent<Text>();
        label.font = font;
        label.fontSize = size.y < 36f ? 11 : 14;
        label.fontStyle = FontStyle.Bold;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        SetValue(valuePence);
    }

    public void SetValue(int valuePence)
    {
        if (image == null) image = GetComponent<Image>();
        if (image != null) image.color = GetChipColor(valuePence);
        if (label != null) label.text = valuePence == 500 ? "5" : valuePence == 1000 ? "10" : "25";
    }

    public static Color GetChipColor(int valuePence)
    {
        if (valuePence == 500) return ChipColors[0];
        if (valuePence == 1000) return ChipColors[1];
        return ChipColors[2];
    }

    private void AddChipMarks(Vector2 size)
    {
        Vector2[] positions =
        {
            new Vector2(0f, size.y * 0.32f),
            new Vector2(size.x * 0.32f, 0f),
            new Vector2(0f, -size.y * 0.32f),
            new Vector2(-size.x * 0.32f, 0f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            GameObject markObject = new GameObject($"Chip Mark {i + 1}", typeof(RectTransform));
            markObject.transform.SetParent(transform, false);
            RectTransform markRect = markObject.GetComponent<RectTransform>();
            markRect.anchorMin = new Vector2(0.5f, 0.5f);
            markRect.anchorMax = markRect.anchorMin;
            markRect.anchoredPosition = positions[i];
            markRect.sizeDelta = new Vector2(Mathf.Max(2f, size.x * 0.12f), Mathf.Max(2f, size.y * 0.12f));
            Image mark = markObject.AddComponent<Image>();
            mark.color = Color.white;
            mark.raycastTarget = false;
        }
    }

    private static Sprite GetChipSprite()
    {
        if (chipSprite != null)
        {
            return chipSprite;
        }

        Texture2D texture = new Texture2D(ChipTextureSize, ChipTextureSize, TextureFormat.RGBA32, false)
        {
            name = "Procedural Blackjack Chip",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Color32[] pixels = new Color32[ChipTextureSize * ChipTextureSize];
        float center = (ChipTextureSize - 1) * 0.5f;
        float radius = ChipTextureSize * 0.47f;
        for (int y = 0; y < ChipTextureSize; y++)
        {
            for (int x = 0; x < ChipTextureSize; x++)
            {
                float dx = x - center;
                float dy = y - center;
                if (dx * dx + dy * dy <= radius * radius)
                {
                    pixels[y * ChipTextureSize + x] = Color.white;
                }
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        chipSprite = Sprite.Create(texture, new Rect(0f, 0f, ChipTextureSize, ChipTextureSize), new Vector2(0.5f, 0.5f), ChipTextureSize);
        chipSprite.name = "Procedural Blackjack Chip Sprite";
        return chipSprite;
    }
}
