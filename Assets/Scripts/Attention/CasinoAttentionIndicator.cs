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
    private Image urgencyFill;
    private GameObject urgencyMeter;
    private Text urgencyTime;
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
        urgencyMeter = CasinoMinigamePresentation.CreateImage("Real task deadline", indicatorCanvas.transform,
            new Vector2(0, -108), new Vector2(230, 32), CasinoHudStyle.Ink);
        urgencyFill = CasinoMinigamePresentation.CreateImage("Remaining time", urgencyMeter.transform,
            Vector2.zero, new Vector2(220, 24), Color.green).GetComponent<Image>();
        urgencyFill.rectTransform.pivot = new Vector2(0, .5f);
        urgencyFill.rectTransform.anchoredPosition = new Vector2(-110, 0);
        urgencyTime = CasinoMinigamePresentation.CreateText(indicatorCanvas.transform, "Deadline seconds", "",
            new Vector2(0, -146), new Vector2(260, 35), 25, Color.white);
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
        if (indicatorCanvas == null || attention == null) return;
        if (!attention.HasVisibleTasks)
        {
            SetVisible(false);
            return;
        }
        if (AudioListener.pause) return;
        bool timed = CasinoTaskFeedback.TryGetDeadline(attention, out float remaining, out float duration);
        urgencyMeter.SetActive(timed);
        urgencyTime.gameObject.SetActive(timed);
        if (timed)
        {
            float fraction = Mathf.Clamp01(remaining / duration);
            urgencyFill.rectTransform.sizeDelta = new Vector2(220 * fraction, 24);
            urgencyFill.color = CasinoTaskFeedback.UrgencyColor(fraction);
            urgencyTime.text = $"{Mathf.CeilToInt(remaining)}s";
            urgencyTime.color = urgencyFill.color;
        }
        indicatorCanvas.enabled = true;
        badge.gameObject.SetActive(true);
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
            indicatorCanvas.enabled = visible && attention != null && attention.HasVisibleTasks;
            if (!indicatorCanvas.enabled)
            {
                if (urgencyMeter != null) urgencyMeter.SetActive(false);
                if (urgencyTime != null) urgencyTime.gameObject.SetActive(false);
            }
        }
    }
}
