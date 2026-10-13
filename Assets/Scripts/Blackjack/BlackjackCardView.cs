using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BlackjackCardView : MonoBehaviour
{
    private static readonly string[] SuitSymbols = { "♠", "♥", "♦", "♣" };
    private Image background;
    private Text rankText;
    private Text suitText;
    private Canvas rootCanvas;
    private Coroutine cardAnimation;
    private CanvasGroup animationGroup;
    private Vector2 finalPosition;
    private bool isAnimating;

    public void Configure(Font font, Canvas canvas)
    {
        rootCanvas = canvas;
        background = gameObject.AddComponent<Image>();
        background.color = Color.white;

        rankText = CreateText("Rank", new Vector2(0f, 1f), new Vector2(8f, -7f), new Vector2(27f, 28f), 19, TextAnchor.UpperLeft, font);
        suitText = CreateText("Suit", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(50f, 46f), 32, TextAnchor.MiddleCenter, font);
        RectTransform rect = GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(58f, 82f);
    }

    public void SetCard(BlackjackCard card, bool faceUp)
    {
        rankText.gameObject.SetActive(faceUp);
        suitText.gameObject.SetActive(faceUp);
        if (!faceUp)
        {
            background.color = new Color(0.10f, 0.18f, 0.25f);
            return;
        }

        string rank = card.Rank == 1 ? "A" : card.Rank > 10 ? "JQK"[card.Rank - 11].ToString() : card.Rank.ToString();
        if (card.Suit < 0 || card.Suit >= SuitSymbols.Length)
        {
            throw new System.ArgumentOutOfRangeException(nameof(card), "Playing card suit is outside the supported range.");
        }

        string suit = SuitSymbols[card.Suit];
        bool red = card.Suit == 1 || card.Suit == 2;
        Color ink = red ? new Color(0.72f, 0.08f, 0.08f) : new Color(0.08f, 0.10f, 0.12f);
        background.color = new Color(0.97f, 0.96f, 0.89f);
        rankText.text = rank;
        suitText.text = suit;
        rankText.color = ink;
        suitText.color = ink;
    }

    public void AnimateFrom(RectTransform source, int staggerIndex)
    {
        if (source == null || rootCanvas == null || rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            return;
        }

        finalPosition = GetComponent<RectTransform>().anchoredPosition;
        isAnimating = true;
        cardAnimation = StartCoroutine(AnimateFromRoutine(source, staggerIndex));
    }

    public void CompleteAnimation()
    {
        if (!isAnimating)
        {
            return;
        }

        isAnimating = false;
        if (cardAnimation != null)
        {
            StopCoroutine(cardAnimation);
            cardAnimation = null;
        }

        RectTransform rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchoredPosition = finalPosition;
            rect.localScale = Vector3.one;
        }

        if (animationGroup != null)
        {
            animationGroup.alpha = 1f;
            Destroy(animationGroup);
            animationGroup = null;
        }
    }

    private IEnumerator AnimateFromRoutine(RectTransform source, int staggerIndex)
    {
        RectTransform rect = GetComponent<RectTransform>();
        RectTransform parent = rect.parent as RectTransform;
        Vector2 target = finalPosition;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, source.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, null, out Vector2 start);
        rect.anchoredPosition = start;
        rect.localScale = Vector3.one * 0.75f;
        animationGroup = gameObject.AddComponent<CanvasGroup>();
        animationGroup.alpha = 0f;
        yield return new WaitForSecondsRealtime(staggerIndex * 0.045f);
        float elapsed = 0f;
        while (elapsed < 0.22f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 0.22f);
            t = 1f - Mathf.Pow(1f - t, 3f);
            rect.anchoredPosition = Vector2.LerpUnclamped(start, target, t);
            rect.localScale = Vector3.LerpUnclamped(Vector3.one * 0.75f, Vector3.one, t);
            animationGroup.alpha = t;
            yield return null;
        }

        rect.anchoredPosition = target;
        rect.localScale = Vector3.one;
        animationGroup.alpha = 1f;
        Destroy(animationGroup);
        animationGroup = null;
        cardAnimation = null;
        isAnimating = false;
    }

    private Text CreateText(string objectName, Vector2 anchor, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment, Font font)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform));
        textObject.transform.SetParent(transform, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        return text;
    }
}
