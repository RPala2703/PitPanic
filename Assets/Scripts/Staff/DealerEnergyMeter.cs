using UnityEngine;
using UnityEngine.UI;

// Small energy bar above a working dealer's head, kept separate from the table's task timer.
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class DealerEnergyMeter : MonoBehaviour
{
    private const float BarWidth = 76f;
    public const float HeadClearance = .38f;

    private AIDealer dealer;
    private PixelCharacterVisual visual;
    private Canvas canvas;
    private RectTransform root;
    private RectTransform fill;
    private Image fillImage;
    private Image frame;
    private Camera targetCamera;

    public bool IsShowing => canvas != null && canvas.enabled;
    public float DisplayedFraction { get; private set; }

    private void Start()
    {
        dealer = GetComponent<AIDealer>();
        visual = GetComponentInChildren<PixelCharacterVisual>();
        if (dealer == null)
        {
            enabled = false;
            return;
        }
        GameObject meter = new GameObject("Dealer Energy Meter", typeof(RectTransform), typeof(Canvas));
        meter.transform.SetParent(transform, false);
        canvas = meter.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 69;
        root = meter.GetComponent<RectTransform>();
        root.sizeDelta = new Vector2(BarWidth + 34, 22);
        root.localScale = Vector3.one * .017f;
        frame = CasinoMinigamePresentation.CreateImage("Frame", root, new Vector2(8, 0),
            new Vector2(BarWidth + 10, 20), CasinoHudStyle.Ink).GetComponent<Image>();
        CasinoMinigamePresentation.CreateImage("Track", root, new Vector2(8, 0),
            new Vector2(BarWidth, 10), new Color(.18f, .14f, .16f)).GetComponent<Image>().raycastTarget = false;
        GameObject bar = CasinoMinigamePresentation.CreateImage("Energy", root, new Vector2(8, 0),
            new Vector2(BarWidth, 10), Color.green);
        fill = bar.GetComponent<RectTransform>();
        fill.pivot = new Vector2(0, .5f);
        fill.anchoredPosition = new Vector2(8 - BarWidth * .5f, 0);
        fillImage = bar.GetComponent<Image>();
        frame.raycastTarget = fillImage.raycastTarget = false;
        // A tiny pixel lightning bolt marks this as energy rather than a task timer.
        GameObject icon = CasinoMinigamePresentation.CreateImage("Bolt Badge", root, new Vector2(-(BarWidth + 10) * .5f - 4, 0),
            new Vector2(20, 20), CasinoHudStyle.Ink);
        icon.GetComponent<Image>().raycastTarget = false;
        foreach ((Vector2 position, Vector2 size) in new[]
                 {
                     (new Vector2(2, 5), new Vector2(4, 6)), (new Vector2(0, 1), new Vector2(8, 3)),
                     (new Vector2(-2, -4), new Vector2(4, 6))
                 })
            CasinoMinigamePresentation.CreateImage("Bolt", icon.transform, position, size, CasinoHudStyle.Gold)
                .GetComponent<Image>().raycastTarget = false;
        canvas.enabled = false;
    }

    private void LateUpdate()
    {
        if (canvas == null) return;
        bool show = dealer.IsAtStation && dealer.Energy > 0 && Time.timeScale > 0 && !AudioListener.pause &&
            !CasinoMinigamePresentation.IsOpen && !CasinoPlayerInteractionRouter.IsAnyInspectionOpen();
        canvas.enabled = show;
        if (!show) return;
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) return;

        float value = Mathf.Clamp01(dealer.Energy / 100f);
        DisplayedFraction = value;
        fill.sizeDelta = new Vector2(BarWidth * value, 10);
        fillImage.color = value * 100f > dealer.TiredThreshold ? new Color(.3f, .95f, .45f)
            : value * 100f > dealer.ExhaustedThreshold ? CasinoHudStyle.Gold : CasinoHudStyle.Coral;
        bool tired = value * 100f <= dealer.TiredThreshold;
        frame.color = tired
            ? Color.Lerp(CasinoHudStyle.Ink, CasinoHudStyle.Coral, .5f + .5f * Mathf.Sin(Time.unscaledTime * 6f))
            : CasinoHudStyle.Ink;

        Vector3 head = visual != null ? visual.HeadPosition : transform.position + Vector3.up * 1.8f;
        canvas.transform.SetPositionAndRotation(head + targetCamera.transform.up * HeadClearance,
            targetCamera.transform.rotation);
    }
}
