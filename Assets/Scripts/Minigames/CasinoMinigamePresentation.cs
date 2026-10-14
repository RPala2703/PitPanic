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
        presentation.canvasGroup.blocksRaycasts = false;
        presentation.canvasGroup.interactable = false;
        presentation.canvasGroup.alpha = 0f;
        presentation.targetAlpha = 1f;
        activePresentation = presentation;

        CreateText(root.transform, "Minigame Eyebrow", "PIT PANIC  •  LIVE CASINO SERVICE",
            new Vector2(-520f, 420f), new Vector2(520f, 24f), 14, new Color(1f, 0.8f, 0.37f));
        CreateText(root.transform, "Minigame Title", title,
            new Vector2(-520f, 389f), new Vector2(520f, 34f), 20, Color.white);
        CreateText(root.transform, "Minigame Instructions", instructions,
            new Vector2(-520f, 355f), new Vector2(520f, 48f), 15, new Color(0.88f, 0.96f, 0.92f));
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
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * 5f);
        if (closeWhenHidden && canvasGroup.alpha <= 0f)
        {
            Destroy(gameObject);
        }
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

}
