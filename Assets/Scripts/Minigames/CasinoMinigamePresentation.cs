using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CasinoMinigamePresentation : MonoBehaviour
{
    private static CasinoMinigamePresentation activePresentation;
    private CanvasGroup canvasGroup;
    private RectTransform presentationRect;
    private float targetAlpha;
    private float targetScale;
    private bool closeWhenHidden;
    private Image[] sparkles;
    private Text titleText;
    private Text instructionsText;
    private RectTransform titleRect;
    private float titlePop;

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
        presentation.presentationRect = root.transform as RectTransform;
        presentation.presentationRect.localScale = Vector3.one * 0.92f;
        presentation.targetScale = 1f;
        activePresentation = presentation;

        (Color accent, Color boardColor, Color gameColor) = GetTheme(title);
        GameObject dimmer = CreateImage("Casino Dimmer", root.transform, Vector2.zero,
            new Vector2(2000f, 1200f), new Color(0.015f, 0.025f, 0.025f, 0.78f), true);
        Stretch(dimmer.GetComponent<RectTransform>());

        GameObject board = CreateImage("Minigame Board", root.transform, Vector2.zero,
            new Vector2(1510f, 820f), boardColor, false);
        CasinoHudStyle.Panel(board.GetComponent<Image>(), boardColor);
        GameObject titleBadge = CreateImage("Title Burst", board.transform, new Vector2(0f, 342f),
            new Vector2(560f, 72f), accent);
        CasinoPixelArt.SetSprite(titleBadge.GetComponent<Image>(), CasinoPixelArt.Burst, accent);
        titleBadge.GetComponent<Image>().preserveAspect = false;
        presentation.sparkles = new Image[2];
        for (int i = 0; i < presentation.sparkles.Length; i++)
        {
            GameObject sparkle = CreateImage("Arcade Sparkle", board.transform,
                new Vector2(i == 0 ? -310f : 310f, 342f), new Vector2(26f, 26f), accent);
            presentation.sparkles[i] = sparkle.GetComponent<Image>();
            CasinoPixelArt.SetSprite(presentation.sparkles[i], CasinoPixelArt.Sparkle, accent);
        }
        GameObject card = CreateImage("Arcade Card", board.transform,
            new Vector2(-620f, 342f), new Vector2(34f, 46f), Color.white);
        CasinoPixelArt.SetSprite(card.GetComponent<Image>(), CasinoPixelArt.Card, Color.white);
        card.transform.localRotation = Quaternion.Euler(0f, 0f, 15f);
        GameObject chip = CreateImage("Arcade Chip", board.transform,
            new Vector2(620f, 342f), new Vector2(45f, 45f), accent);
        CasinoPixelArt.SetSprite(chip.GetComponent<Image>(), CasinoPixelArt.Chip, accent);
        chip.AddComponent<CasinoPixelSparkle>();
        presentation.titleText = CreateText(board.transform, "Minigame Title", title,
            new Vector2(0f, 342f), new Vector2(480f, 50f), 32, CasinoHudStyle.Ink);
        presentation.titleRect = presentation.titleText.rectTransform;
        presentation.instructionsText = CreateText(board.transform, "Minigame Instructions", instructions,
            new Vector2(0f, 292f), new Vector2(1390f, 38f), 19, new Color(0.94f, 0.97f, 0.92f));
        GameObject area = CreateImage("Minigame Game Area", board.transform, new Vector2(0f, -38f),
            new Vector2(1430f, 620f), gameColor, false);
        area.GetComponent<Image>().color = new Color(gameColor.r, gameColor.g, gameColor.b, 0.55f);
        presentation.GameArea = area.transform;
        return presentation;
    }

    public void SetTitle(string title)
    {
        if (titleText == null || titleText.text == title) return;
        titleText.text = title;
        titlePop = 1f;
    }

    public void SetInstructions(string instructions)
    {
        if (instructionsText != null) instructionsText.text = instructions;
    }

    public void Close()
    {
        if (closeWhenHidden)
        {
            return;
        }

        targetAlpha = 0f;
        targetScale = 0.92f;
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
        if (sparkles != null)
        {
            for (int i = 0; i < sparkles.Length; i++)
            {
                float pulse = 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 3f + i * Mathf.PI);
                sparkles[i].rectTransform.localScale = Vector3.one * pulse;
            }
        }
        if (presentationRect != null)
        {
            float scale = Mathf.MoveTowards(presentationRect.localScale.x, targetScale, Time.unscaledDeltaTime * 0.8f);
            presentationRect.localScale = Vector3.one * scale;
        }
        if (titleRect != null && titlePop > 0f)
        {
            titlePop = Mathf.MoveTowards(titlePop, 0f, Time.unscaledDeltaTime * 4f);
            titleRect.localScale = Vector3.one * (1f + 0.18f * titlePop);
        }

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
        text.font = CasinoPixelFont.Default;
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Normal;
        CasinoPixelFont.Fit(text, fontSize);
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

    private static (Color accent, Color board, Color game) GetTheme(string title)
    {
        if (title.Contains("FLICK"))
        {
            return (new Color(0.46f, 0.86f, 0.82f), new Color(0.12f, 0.09f, 0.17f, 0.99f),
                new Color(0.18f, 0.24f, 0.27f, 1f));
        }

        if (title.Contains("SPIN") || title.Contains("SWEEP") || title.Contains("PAY"))
        {
            return (new Color(0.97f, 0.72f, 0.28f), new Color(0.12f, 0.09f, 0.17f, 0.99f),
                new Color(0.27f, 0.14f, 0.22f, 1f));
        }

        if (title.Contains("PACK"))
        {
            return (new Color(0.49f, 0.84f, 0.61f), new Color(0.12f, 0.09f, 0.17f, 0.99f),
                new Color(0.12f, 0.29f, 0.24f, 1f));
        }

        return (new Color(0.83f, 0.64f, 0.30f), new Color(0.035f, 0.12f, 0.105f, 0.99f),
            new Color(0.055f, 0.22f, 0.16f, 1f));
    }
}
