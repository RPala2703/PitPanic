using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CasinoMinigamePresentation : MonoBehaviour
{
    private static CasinoMinigamePresentation activePresentation;
    private CanvasGroup canvasGroup;
    private RectTransform frame;
    private float targetAlpha;
    private bool closeWhenHidden;
    public static bool IsOpen => activePresentation != null && !activePresentation.closeWhenHidden;

    public static CasinoMinigamePresentation Open(string title, string instructions)
    {
        if (IsOpen)
        {
            throw new System.InvalidOperationException("A casino minigame presentation is already active.");
        }

        GameObject root = new GameObject(
            "Casino Minigame Presentation",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup),
            typeof(CasinoMinigamePresentation));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        CasinoMinigamePresentation presentation = root.GetComponent<CasinoMinigamePresentation>();
        presentation.canvasGroup = root.GetComponent<CanvasGroup>();
        presentation.canvasGroup.blocksRaycasts = false;
        presentation.canvasGroup.interactable = false;
        presentation.canvasGroup.alpha = 0f;
        presentation.targetAlpha = 1f;
        activePresentation = presentation;

        Image scrim = CreateImage("Casino Dimmer", root.transform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
            new Color(0.015f, 0.035f, 0.04f, 0.72f));
        Stretch(scrim.rectTransform);

        GameObject panel = CreateRect("Minigame Frame", root.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1450f, 790f));
        presentation.frame = panel.GetComponent<RectTransform>();
        presentation.frame.localScale = Vector3.one * 0.96f;
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.025f, 0.12f, 0.105f, 0.34f);
        panelImage.raycastTarget = false;
        Shadow shadow = panel.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(0f, -18f);

        Image border = CreateImage("Gold Frame", panel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1460f, 800f),
            new Color(0.88f, 0.63f, 0.22f, 0.9f));
        border.raycastTarget = false;
        border.transform.SetAsFirstSibling();
        Image innerEdge = CreateImage("Inner Frame", panel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1448f, 788f),
            new Color(0.015f, 0.12f, 0.1f, 0.22f));
        innerEdge.raycastTarget = false;
        innerEdge.transform.SetAsLastSibling();

        CreateText(panel.transform, "Minigame Eyebrow", "PIT PANIC  •  LIVE CASINO SERVICE",
            new Vector2(0f, 348f), new Vector2(900f, 32f), 18, new Color(1f, 0.8f, 0.37f));
        CreateText(panel.transform, "Minigame Title", title,
            new Vector2(0f, 300f), new Vector2(1120f, 72f), 38, Color.white);
        CreateText(panel.transform, "Minigame Instructions", instructions,
            new Vector2(0f, 242f), new Vector2(1200f, 54f), 22, new Color(0.88f, 0.96f, 0.92f));
        CreateText(panel.transform, "Minigame Footer",
            "THE CASINO STAYS LIVE  •  ESC TO CANCEL  •  MOUSE TO PLAY",
            new Vector2(0f, -365f), new Vector2(1000f, 34f), 16, new Color(1f, 0.82f, 0.42f));
        return presentation;
    }

    public void Close()
    {
        if (closeWhenHidden)
        {
            return;
        }

        targetAlpha = 0f;
        closeWhenHidden = true;
        if (activePresentation == this)
        {
            activePresentation = null;
        }
    }

    private void OnDestroy()
    {
        if (activePresentation == this)
        {
            activePresentation = null;
        }
    }

    private void Update()
    {
        if (canvasGroup == null || frame == null)
        {
            return;
        }

        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * 5f);
        float scale = Mathf.Lerp(0.96f, 1f, canvasGroup.alpha);
        frame.localScale = Vector3.one * scale;
        if (closeWhenHidden && canvasGroup.alpha <= 0f)
        {
            Destroy(gameObject);
        }
    }

    private static Image CreateImage(
        string objectName,
        Transform parent,
        Vector2 anchor,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        GameObject imageObject = CreateRect(objectName, parent, anchor, position, size);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Text CreateText(
        Transform parent,
        string objectName,
        string value,
        Vector2 position,
        Vector2 size,
        int fontSize,
        Color color)
    {
        GameObject textObject = CreateRect(objectName, parent, new Vector2(0.5f, 0.5f), position, size);
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        Outline outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);
        return text;
    }

    private static GameObject CreateRect(
        string objectName,
        Transform parent,
        Vector2 anchor,
        Vector2 position,
        Vector2 size)
    {
        GameObject result = new GameObject(objectName, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        RectTransform rect = result.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return result;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
