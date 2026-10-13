using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BlackjackHandView : MonoBehaviour
{
    private readonly List<BlackjackCardView> cardViews = new List<BlackjackCardView>();
    private RectTransform cardRow;
    private Text totalText;
    private Font font;
    private RectTransform shoe;
    private Canvas canvas;

    public void Configure(Font runtimeFont, Canvas rootCanvas, RectTransform shoePosition)
    {
        font = runtimeFont;
        canvas = rootCanvas;
        shoe = shoePosition;
        cardRow = CreateRect("Cards", transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(258f, 88f));
        totalText = CreateText("Hand Total", transform, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(150f, 24f), 15, TextAnchor.MiddleCenter);
    }

    public void Render(IReadOnlyList<BlackjackCard> cards, int hiddenCardIndex, bool revealAll, BlackjackHandStatus status, bool showStatus)
    {
        bool canAnimateCards = isActiveAndEnabled && gameObject.activeInHierarchy &&
                               cardRow != null && cardRow.gameObject.activeInHierarchy;
        while (cardViews.Count > cards.Count)
        {
            int lastIndex = cardViews.Count - 1;
            Destroy(cardViews[lastIndex].gameObject);
            cardViews.RemoveAt(lastIndex);
        }

        for (int i = 0; i < cards.Count; i++)
        {
            bool created = false;
            if (cardViews.Count == i)
            {
                GameObject cardObject = new GameObject("Playing Card", typeof(RectTransform));
                cardObject.transform.SetParent(cardRow, false);
                BlackjackCardView cardView = cardObject.AddComponent<BlackjackCardView>();
                cardView.Configure(font, canvas);
                cardViews.Add(cardView);
                created = true;
            }

            RectTransform cardRect = cardViews[i].GetComponent<RectTransform>();
            cardRect.anchoredPosition = new Vector2((i - (cards.Count - 1) * 0.5f) * 48f, 0f);
            cardViews[i].SetCard(cards[i], revealAll || i != hiddenCardIndex);
            if (created && canAnimateCards)
            {
                cardViews[i].AnimateFrom(shoe, i);
            }
        }

        if (cards.Count == 0)
        {
            totalText.text = showStatus ? StatusText(status) : "";
        }
        else
        {
            bool hiddenCardExists = !revealAll && hiddenCardIndex >= 0 && hiddenCardIndex < cards.Count;
            int visibleCount = hiddenCardExists ? cards.Count - 1 : cards.Count;
            totalText.text = visibleCount == 0
                ? ""
                : revealAll || hiddenCardIndex < 0
                    ? BlackjackRules.CalculateBestTotal(cards).ToString()
                    : $"Showing {BlackjackRules.CalculateBestTotal(VisibleCards(cards, hiddenCardIndex))}";
        }
    }

    public void SetTotalText(string value)
    {
        totalText.text = value;
    }

    private static List<BlackjackCard> VisibleCards(IReadOnlyList<BlackjackCard> cards, int hiddenCardIndex)
    {
        List<BlackjackCard> visible = new List<BlackjackCard>(cards.Count);
        for (int i = 0; i < cards.Count; i++)
        {
            if (i != hiddenCardIndex)
            {
                visible.Add(cards[i]);
            }
        }

        return visible;
    }

    private static string StatusText(BlackjackHandStatus status)
    {
        switch (status)
        {
            case BlackjackHandStatus.Bust: return "BUST";
            case BlackjackHandStatus.Blackjack: return "BLACKJACK";
            case BlackjackHandStatus.Win: return "WIN";
            case BlackjackHandStatus.Lose: return "LOSE";
            case BlackjackHandStatus.Push: return "PUSH";
            case BlackjackHandStatus.Standing: return "STAND";
            default: return "";
        }
    }

    private RectTransform CreateRect(string objectName, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        GameObject rectObject = new GameObject(objectName, typeof(RectTransform));
        rectObject.transform.SetParent(parent, false);
        RectTransform rect = rectObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private Text CreateText(string objectName, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
    {
        RectTransform rect = CreateRect(objectName, parent, anchor, position, size);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        return text;
    }
}
