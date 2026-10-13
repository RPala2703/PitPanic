using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[DisallowMultipleComponent]
public sealed class BlackjackChipView : MonoBehaviour
{
    private const int ChipTextureSize = 64;
    private static Sprite chipSprite;

    private Image image;
    private Text label;

    public void Configure(int valuePence, Font font, Vector2 size, bool showValue = true)
    {
        Configure(FindDefaultDenomination(valuePence), font, size, showValue);
    }

    public void Configure(BlackjackChipDenomination denomination, Font font, Vector2 size, bool showValue = true)
    {
        image = gameObject.AddComponent<Image>();
        image.sprite = GetChipSprite();
        image.raycastTarget = false;
        RectTransform rect = GetComponent<RectTransform>();
        rect.sizeDelta = size;
        SetValue(denomination);
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
        SetValue(denomination);
    }

    public void SetValue(int valuePence)
    {
        SetValue(FindDefaultDenomination(valuePence));
    }

    public void SetValue(int valuePence, IReadOnlyList<BlackjackChipDenomination> denominations)
    {
        SetValue(FindDenomination(valuePence, denominations));
    }

    public void SetValue(BlackjackChipDenomination denomination)
    {
        if (image == null) image = GetComponent<Image>();
        if (image != null) image.color = denomination.Color;
        if (label != null) label.text = denomination.Label.Replace("£", "");
    }

    public static Color GetChipColor(int valuePence)
    {
        return FindDefaultDenomination(valuePence).Color;
    }

    public static Color GetChipColor(int valuePence, IReadOnlyList<BlackjackChipDenomination> denominations)
    {
        return FindDenomination(valuePence, denominations).Color;
    }

    private static BlackjackChipDenomination FindDefaultDenomination(int valuePence)
    {
        return FindDenomination(valuePence, BlackjackChipDenomination.CreateDefaults());
    }

    private static BlackjackChipDenomination FindDenomination(int valuePence, IReadOnlyList<BlackjackChipDenomination> denominations)
    {
        if (denominations != null)
        {
            for (int i = 0; i < denominations.Count; i++)
            {
                if (denominations[i].ValuePence == valuePence)
                {
                    return denominations[i];
                }
            }
        }

        throw new System.ArgumentOutOfRangeException(nameof(valuePence), valuePence, "No chip denomination is configured for this amount.");
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
