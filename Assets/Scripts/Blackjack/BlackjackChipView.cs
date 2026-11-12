using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[DisallowMultipleComponent]
public sealed class BlackjackChipView : MonoBehaviour
{
    private Image image;
    private Text label;

    public void Configure(int valuePence, Font font, Vector2 size, bool showValue = true)
    {
        Configure(FindDefaultDenomination(valuePence), font, size, showValue);
    }

    public void Configure(BlackjackChipDenomination denomination, Font font, Vector2 size, bool showValue = true)
    {
        image = gameObject.AddComponent<Image>();
        image.sprite = CasinoPixelArt.ChipForValue(denomination.ValuePence);
        image.raycastTarget = false;
        RectTransform rect = GetComponent<RectTransform>();
        rect.sizeDelta = size;
        SetValue(denomination);

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
        if (image != null)
        {
            image.sprite = CasinoPixelArt.ChipForValue(denomination.ValuePence);
            image.color = Color.white;
        }
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

}
