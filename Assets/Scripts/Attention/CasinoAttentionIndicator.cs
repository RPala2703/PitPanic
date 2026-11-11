using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CasinoAttentionIndicator : MonoBehaviour
{
    [SerializeField] private CasinoTableAttention attention;
    [SerializeField] private Canvas indicatorCanvas;

    private Camera targetCamera;
    private Text indicatorText;
    private RectTransform badge;
    public CasinoTableAttention Attention => attention;

    public void Configure(CasinoTableAttention tableAttention, Canvas worldCanvas, Text text)
    {
        attention = tableAttention;
        indicatorCanvas = worldCanvas;
        indicatorText = text;
    }

    private void Start()
    {
        if (attention == null)
        {
            attention = GetComponent<CasinoTableAttention>();
        }

        if (indicatorCanvas == null)
        {
            indicatorCanvas = GetComponentInChildren<Canvas>(true);
        }

        if (indicatorText == null && indicatorCanvas != null)
        {
            indicatorText = indicatorCanvas.GetComponentInChildren<Text>(true);
        }

        if (attention == null || indicatorCanvas == null || indicatorText == null)
        {
            Debug.LogError($"{nameof(CasinoAttentionIndicator)} on {name} is missing its attention, canvas, or text reference.", this);
            enabled = false;
            return;
        }

        targetCamera = Camera.main;
        indicatorText.enabled = false;
        GameObject badgeObject = new GameObject("Pixel Attention Badge", typeof(RectTransform), typeof(Image));
        badgeObject.transform.SetParent(indicatorCanvas.transform, false);
        badge = badgeObject.GetComponent<RectTransform>();
        badge.anchorMin = Vector2.zero;
        badge.anchorMax = Vector2.one;
        badge.offsetMin = badge.offsetMax = Vector2.zero;
        Image image = badgeObject.GetComponent<Image>();
        CasinoPixelArt.SetSprite(image, CasinoPixelArt.Attention, Color.white);
        image.raycastTarget = false;
        attention.AttentionChanged += SetVisible;
        SetVisible(attention.HasPendingTasks);
    }

    private void OnDestroy()
    {
        if (attention != null)
        {
            attention.AttentionChanged -= SetVisible;
        }
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera != null)
        {
            indicatorCanvas.transform.rotation = targetCamera.transform.rotation;
        }

        if (badge != null && indicatorCanvas.enabled)
        {
            float pulse = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 5f);
            badge.localScale = Vector3.one * pulse;
            badge.anchoredPosition = new Vector2(0f, 5f * Mathf.Sin(Time.unscaledTime * 4f));
        }
    }

    private void SetVisible(bool visible)
    {
        if (indicatorCanvas != null)
        {
            indicatorCanvas.enabled = visible;
        }
    }
}
