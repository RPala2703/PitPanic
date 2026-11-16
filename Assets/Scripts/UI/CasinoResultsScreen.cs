using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class CasinoResultsScreen : MonoBehaviour
{
    private CanvasGroup screenGroup;
    private CanvasGroup signGroup;
    private CanvasGroup starsGroup;
    private CanvasGroup informationGroup;
    private Image backgroundImage;
    private Image signImage;
    private readonly List<Image> starImages = new List<Image>(3);
    private Button nextButton;

    public void Build(Transform parent, Action retry, Action next, Action levelSelect, Action mainMenu)
    {
        GameObject screen = new GameObject("Full-screen shift results", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        screen.transform.SetParent(parent, false);
        RectTransform screenRect = screen.GetComponent<RectTransform>();
        Stretch(screenRect);
        Image backing = screen.GetComponent<Image>();
        backing.color = new Color(.07f, .045f, .11f, 1f);
        backing.raycastTarget = true;
        screenGroup = screen.GetComponent<CanvasGroup>();

        GameObject art = ImageObject("Illustrated background", screen.transform);
        backgroundImage = art.GetComponent<Image>();
        backgroundImage.preserveAspect = true;
        backgroundImage.raycastTarget = false;
        AspectRatioFitter aspect = art.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspect.aspectRatio = 16f / 9f;

        GameObject sign = ImageObject("Shift result sign", screen.transform);
        signImage = sign.GetComponent<Image>();
        signImage.preserveAspect = true;
        signImage.raycastTarget = false;
        SetRect(sign.GetComponent<RectTransform>(), new Vector2(.5f, 1f), new Vector2(.5f, 1f),
            new Vector2(.5f, 1f), new Vector2(0f, -24f), new Vector2(560f, 262f));
        signGroup = sign.AddComponent<CanvasGroup>();

        GameObject stars = new GameObject("Shift rating", typeof(RectTransform), typeof(CanvasGroup));
        stars.transform.SetParent(screen.transform, false);
        SetRect(stars.GetComponent<RectTransform>(), new Vector2(.5f, 1f), new Vector2(.5f, 1f),
            new Vector2(.5f, 1f), new Vector2(0f, -285f), new Vector2(360f, 96f));
        starsGroup = stars.GetComponent<CanvasGroup>();
        for (int i = 0; i < 3; i++)
        {
            GameObject star = ImageObject($"Star {i + 1}", stars.transform);
            RectTransform starRect = star.GetComponent<RectTransform>();
            starRect.anchoredPosition = new Vector2((i - 1) * 62f, 18f);
            starRect.sizeDelta = new Vector2(54f, 54f);
            Image image = star.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            starImages.Add(image);
        }
        GameObject panel = ImageObject("Results information panel", screen.transform);
        Image panelImage = panel.GetComponent<Image>();
        panelImage.preserveAspect = true;
        panelImage.raycastTarget = false;
        SetRect(panel.GetComponent<RectTransform>(), new Vector2(.5f, 0f), new Vector2(.5f, 0f),
            new Vector2(.5f, 0f), new Vector2(0f, 10f), new Vector2(1080f, 320f));
        informationGroup = panel.AddComponent<CanvasGroup>();

        CasinoMinigamePresentation.CreateText(panel.transform, "Level and satisfaction", "",
            new Vector2(0f, 72f), new Vector2(920f, 34f), 18, Color.white);
        CasinoMinigamePresentation.CreateText(panel.transform, "Earned and best stars", "",
            new Vector2(0f, 38f), new Vector2(920f, 28f), 16, CasinoHudStyle.Gold);
        CasinoMinigamePresentation.CreateText(panel.transform, "Task totals", "",
            new Vector2(0f, 4f), new Vector2(920f, 30f), 17, Color.white);
        CasinoMinigamePresentation.CreateText(panel.transform, "Tips", "",
            new Vector2(0f, -30f), new Vector2(920f, 30f), 17, CasinoHudStyle.Gold);
        CasinoMinigamePresentation.CreateText(panel.transform, "Shift explanation", "",
            new Vector2(0f, -65f), new Vector2(920f, 48f), 15, Color.white);

        Button retryButton = MakeButton(panel.transform, "Retry Shift", "RETRY SHIFT", retry);
        nextButton = MakeButton(panel.transform, "Next Level", "NEXT LEVEL", next);
        Button levelSelectButton = MakeButton(panel.transform, "Level Select", "LEVEL SELECT", levelSelect);
        Button mainMenuButton = MakeButton(panel.transform, "Main Menu", "MAIN MENU", mainMenu);
        ArrangeButtons(retryButton, nextButton, levelSelectButton, mainMenuButton, false);
        screen.SetActive(false);
    }

    public void Show(int level, bool passed, int earnedStars, int bestStars, int serviceStarts, int satisfaction,
        int tasksCompleted, int tasksMissed, int unfinishedTasks, int tipsPence, int tipCount, int tipBonus,
        string explanation, Sprite background, Sprite passedSign, Sprite failedSign, Sprite filledStar,
        Sprite hollowStar, Sprite informationPanel, bool hasNextLevel)
    {
        if (background == null || passedSign == null || failedSign == null || filledStar == null ||
            hollowStar == null || informationPanel == null)
            throw new InvalidOperationException("Results screen artwork is missing. Import it from Pit Panic/Import Results Screen Artwork.");

        GameObject screen = transform.Find("Full-screen shift results")?.gameObject;
        if (screen == null)
            throw new InvalidOperationException("The results screen must be built before it can be shown.");

        backgroundImage.sprite = background;
        signImage.sprite = passed ? passedSign : failedSign;
        Transform panel = screen.transform.Find("Results information panel");
        panel.GetComponent<Image>().sprite = informationPanel;
        for (int i = 0; i < starImages.Count; i++)
            starImages[i].sprite = i < earnedStars ? filledStar : hollowStar;

        Text[] lines = panel.GetComponentsInChildren<Text>(true);
        FindText(lines, "Level and satisfaction").text =
            $"{CasinoCampaignPresentation.LevelName(level).ToUpperInvariant()}   •   CUSTOMER SATISFACTION   " +
            (serviceStarts > 0 ? $"{satisfaction}%" : "N/A");
        FindText(lines, "Earned and best stars").text = $"STARS EARNED  {earnedStars}/3   •   BEST  {bestStars}/3";
        FindText(lines, "Task totals").text =
            $"TASKS COMPLETED  {tasksCompleted}   •   MISSED  {tasksMissed}" +
            (unfinishedTasks > 0 ? $"  ({unfinishedTasks} UNFINISHED)" : "");
        FindText(lines, "Tips").text =
            $"TIPS  {CasinoTipJar.Format(tipsPence)}  ({tipCount})" +
            (tipBonus > 0 ? $"   •   STAR BONUS +{tipBonus}" : "");
        FindText(lines, "Shift explanation").text = explanation;

        nextButton.gameObject.SetActive(passed && hasNextLevel);
        Button[] buttons = panel.GetComponentsInChildren<Button>(true);
        Button retry = FindButton(buttons, "Retry Shift");
        Button levelSelect = FindButton(buttons, "Level Select");
        Button mainMenu = FindButton(buttons, "Main Menu");
        retry.gameObject.SetActive(true);
        levelSelect.gameObject.SetActive(true);
        mainMenu.gameObject.SetActive(true);
        screen.SetActive(true);
        Canvas.ForceUpdateCanvases();
        float textWidth = Mathf.Max(220f, ((RectTransform)panel).rect.width - 100f);
        foreach (Text text in lines)
            if (text.name != "Label")
            {
                text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth);
                CasinoPixelFont.Fit(text, text.fontSize);
            }
        ArrangeButtons(retry, nextButton, levelSelect, mainMenu, passed && hasNextLevel);
        StopAllCoroutines();
        StartCoroutine(Reveal());
    }

    public void Hide()
    {
        StopAllCoroutines();
        Transform screen = transform.Find("Full-screen shift results");
        if (screen != null) screen.gameObject.SetActive(false);
    }

    private IEnumerator Reveal()
    {
        screenGroup.alpha = 0f;
        signGroup.alpha = 0f;
        starsGroup.alpha = 0f;
        informationGroup.alpha = 0f;
        yield return Fade(screenGroup, 0f, 1f, .4f);
        yield return new WaitForSecondsRealtime(.12f);
        yield return Fade(signGroup, 0f, 1f, .34f);
        yield return new WaitForSecondsRealtime(.1f);
        yield return Fade(starsGroup, 0f, 1f, .3f);
        yield return new WaitForSecondsRealtime(.22f);
        yield return Fade(informationGroup, 0f, 1f, .42f);
    }

    private static IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        group.alpha = to;
    }

    private static GameObject ImageObject(string name, Transform parent)
    {
        GameObject image = CasinoMinigamePresentation.CreateImage(name, parent, Vector2.zero, Vector2.one, Color.white);
        image.GetComponent<Image>().raycastTarget = false;
        return image;
    }

    private static Button MakeButton(Transform parent, string name, string label, Action action)
    {
        GameObject root = CasinoMinigamePresentation.CreateImage(name, parent, Vector2.zero, new Vector2(252f, 36f),
            new Color(.34f, .16f, .29f, 1f), true);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = root.GetComponent<Image>();
        CasinoHudStyle.Button(button);
        button.onClick.AddListener(() => action());
        CasinoMinigamePresentation.CreateText(root.transform, "Label", label, Vector2.zero,
            new Vector2(240f, 30f), 16, Color.white);
        return button;
    }

    private static void ArrangeButtons(Button retry, Button next, Button levelSelect, Button mainMenu, bool showNext)
    {
        List<Button> visible = new List<Button> { retry };
        if (showNext) visible.Add(next);
        visible.Add(levelSelect);
        visible.Add(mainMenu);
        RectTransform panel = (RectTransform)retry.transform.parent;
        float gap = 12f;
        float available = Mathf.Max(0f, panel.rect.width - 72f);
        float width = Mathf.Min(252f, (available - (visible.Count - 1) * gap) / visible.Count);
        float x = -((visible.Count * width + (visible.Count - 1) * gap) * .5f) + width * .5f;
        foreach (Button button in visible)
        {
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(x, -100f);
            rect.sizeDelta = new Vector2(width, 36f);
            button.GetComponentInChildren<Text>().rectTransform.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal, Mathf.Max(80f, width - 12f));
            button.GetComponentInChildren<Text>().rectTransform.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical, 30f);
            CasinoPixelFont.Fit(button.GetComponentInChildren<Text>(), 16);
            x += width + gap;
        }
    }

    private static Text FindText(IEnumerable<Text> texts, string name)
    {
        foreach (Text text in texts)
            if (text.name == name) return text;
        throw new InvalidOperationException($"Results text element was not found: {name}");
    }

    private static Button FindButton(IEnumerable<Button> buttons, string name)
    {
        foreach (Button button in buttons)
            if (button.name == name) return button;
        throw new InvalidOperationException($"Results button was not found: {name}");
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
