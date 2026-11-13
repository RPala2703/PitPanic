using UnityEngine;
using UnityEngine.UI;

// Overhead patience bar shown once regulars grow impatient. VIPs get a always-visible gold, pulsing "HURRY!" meter so their faster drain reads at a glance.
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class CustomerPatienceMeter : MonoBehaviour
{
    private const float BarWidth = 84f;
    private static readonly Color Calm = new Color(.36f, .86f, .42f);
    private static readonly Color Worried = new Color(1f, .78f, .2f);
    private static readonly Color Angry = new Color(.95f, .2f, .16f);
    private static readonly Color VipGold = new Color(1f, .82f, .18f);

    private CasinoCustomerAI customer;
    private CustomerPatience patience;
    private PixelCharacterVisual visual;
    private Canvas canvas;
    private RectTransform root;
    private RectTransform fill;
    private Image fillImage;
    private Image frame;
    private Text label;
    private Camera targetCamera;

    public bool IsShowing => canvas != null && canvas.enabled;
    public bool ShowsVipWarning => IsShowing && label.enabled;

    private void Start()
    {
        customer = GetComponent<CasinoCustomerAI>();
        patience = GetComponent<CustomerPatience>();
        visual = GetComponentInChildren<PixelCharacterVisual>();
        if (customer == null || patience == null)
        {
            enabled = false;
            return;
        }
        GameObject meter = new GameObject("Patience Meter", typeof(RectTransform), typeof(Canvas));
        meter.transform.SetParent(transform, false);
        canvas = meter.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 70;
        root = meter.GetComponent<RectTransform>();
        root.sizeDelta = new Vector2(BarWidth + 12, 60);
        root.localScale = Vector3.one * .01f;
        frame = CasinoMinigamePresentation.CreateImage("Frame", root, new Vector2(0, -12),
            new Vector2(BarWidth + 12, 24), CasinoHudStyle.Ink).GetComponent<Image>();
        CasinoMinigamePresentation.CreateImage("Track", root, new Vector2(0, -12),
            new Vector2(BarWidth, 14), new Color(.18f, .14f, .16f)).GetComponent<Image>().raycastTarget = false;
        GameObject bar = CasinoMinigamePresentation.CreateImage("Fill", root, new Vector2(0, -12),
            new Vector2(BarWidth, 14), Calm);
        fill = bar.GetComponent<RectTransform>();
        fill.pivot = new Vector2(0, .5f);
        fill.anchoredPosition = new Vector2(-BarWidth * .5f, -12);
        fillImage = bar.GetComponent<Image>();
        frame.raycastTarget = fillImage.raycastTarget = false;
        label = CasinoMinigamePresentation.CreateText(root, "VIP Warning", "HURRY!", new Vector2(0, 14),
            new Vector2(BarWidth + 16, 26), 18, VipGold);
        label.raycastTarget = false;
        Outline outline = label.gameObject.AddComponent<Outline>();
        outline.effectColor = CasinoHudStyle.Ink;
        outline.effectDistance = new Vector2(2, -2);
        canvas.enabled = false;
    }

    private void LateUpdate()
    {
        if (canvas == null) return;
        bool show = patience.IsWaiting && (customer.IsVip || patience.Normalized <= .5f) && Time.timeScale > 0 && !CasinoMinigamePresentation.IsOpen &&
            !CasinoPlayerInteractionRouter.IsAnyInspectionOpen();
        canvas.enabled = show;
        if (!show) return;
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) return;

        float value = Mathf.Clamp01(patience.Normalized);
        bool vip = customer.IsVip;
        fill.sizeDelta = new Vector2(BarWidth * value, 14);
        fillImage.color = value > .55f ? Calm : value > .25f ? Worried : Angry;
        label.enabled = vip;
        float pulseRate = vip ? 9f : 6f;
        bool pulsing = vip || value <= .25f;
        float pulse = pulsing ? .5f + .5f * Mathf.Sin(Time.unscaledTime * pulseRate) : 0f;
        frame.color = vip ? Color.Lerp(VipGold, Color.white, pulse * .45f) : CasinoHudStyle.Ink;
        if (vip) label.color = Color.Lerp(VipGold, Angry, value <= .25f ? pulse : pulse * .3f);
        float scale = .01f * (vip ? 1.12f + pulse * .06f : 1f);
        root.localScale = Vector3.one * scale;

        Vector3 head = visual != null ? visual.HeadPosition : transform.position + Vector3.up * 1.8f;
        root.SetPositionAndRotation(head + targetCamera.transform.up * (24f * scale + .12f),
            targetCamera.transform.rotation);
    }
}
