using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CasinoMinigamePresentation : MonoBehaviour
{
    private static CasinoMinigamePresentation activePresentation;
    private CanvasGroup canvasGroup;
    private float targetAlpha;
    private bool closeWhenHidden;

    public static bool IsOpen => activePresentation != null && !activePresentation.closeWhenHidden;
    public Transform GameArea { get; private set; }

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
        canvas.sortingOrder = 40;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        CasinoMinigamePresentation presentation = root.GetComponent<CasinoMinigamePresentation>();
        presentation.canvasGroup = root.GetComponent<CanvasGroup>();
        presentation.canvasGroup.blocksRaycasts = true;
        presentation.canvasGroup.interactable = true;
        presentation.canvasGroup.alpha = 0f;
        presentation.targetAlpha = 1f;
        activePresentation = presentation;

        GameObject dimmer = CreateImage("Casino Dimmer", root.transform, Vector2.zero,
            new Vector2(2000f, 1200f), new Color(0.015f, 0.025f, 0.025f, 0.78f), true);
        Stretch(dimmer.GetComponent<RectTransform>());

        GameObject board = CreateImage("Minigame Board", root.transform, Vector2.zero,
            new Vector2(1510f, 820f), new Color(0.035f, 0.12f, 0.105f, 0.99f), false);
        Outline border = board.AddComponent<Outline>();
        border.effectColor = new Color(0.83f, 0.62f, 0.24f, 0.95f);
        border.effectDistance = new Vector2(3f, -3f);
        CreateText(board.transform, "Minigame Eyebrow", "PIT PANIC  •  LIVE CASINO SERVICE",
            new Vector2(0f, 381f), new Vector2(760f, 25f), 14, new Color(1f, 0.8f, 0.37f));
        CreateText(board.transform, "Minigame Title", title,
            new Vector2(0f, 340f), new Vector2(1350f, 48f), 30, Color.white);
        CreateText(board.transform, "Minigame Instructions", instructions,
            new Vector2(0f, 292f), new Vector2(1390f, 38f), 18, new Color(0.88f, 0.96f, 0.92f));
        GameObject area = CreateImage("Minigame Game Area", board.transform, new Vector2(0f, -38f),
            new Vector2(1430f, 620f), new Color(0.055f, 0.22f, 0.16f, 1f), false);
        presentation.GameArea = area.transform;
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
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

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
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * 7f);
        if (closeWhenHidden && canvasGroup.alpha <= 0f)
        {
            Destroy(gameObject);
        }
    }

    public static GameObject CreateImage(
        string objectName,
        Transform parent,
        Vector2 position,
        Vector2 size,
        Color color,
        bool raycastTarget = false)
    {
        GameObject result = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        result.transform.SetParent(parent, false);
        RectTransform rect = result.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = result.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return result;
    }

    public static Text CreateText(
        Transform parent,
        string objectName,
        string value,
        Vector2 position,
        Vector2 size,
        int fontSize,
        Color color,
        TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text), typeof(Outline));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        Outline outline = textObject.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        return text;
    }

    public static bool ContainsScreenPoint(RectTransform rect, Vector2 screenPoint)
    {
        return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, null);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
