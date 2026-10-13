using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BlackjackBettingSpotView : MonoBehaviour
{
    private Image background;
    private Text nameText;
    private Text statusText;
    private Text betText;
    private BlackjackHandView handView;
    private BlackjackChipView[] betChips;
    private string seatLabel;

    public void Configure(int seatNumber, Font font, Canvas canvas, RectTransform shoe)
    {
        background = gameObject.AddComponent<Image>();
        background.color = new Color(0.035f, 0.24f, 0.17f, 0.84f);
        background.raycastTarget = false;

        nameText = CreateText("Seat Name", transform, font, new Vector2(0f, 87f), new Vector2(270f, 28f), 16, TextAnchor.MiddleCenter);
        statusText = CreateText("Hand Status", transform, font, new Vector2(0f, -78f), new Vector2(270f, 26f), 15, TextAnchor.MiddleCenter);
        betText = CreateText("Stake", transform, font, new Vector2(0f, -99f), new Vector2(112f, 22f), 13, TextAnchor.MiddleCenter);

        GameObject handObject = CreateRectObject("Customer Hand", transform, new Vector2(0f, 8f), new Vector2(275f, 135f));
        handView = handObject.AddComponent<BlackjackHandView>();
        handView.Configure(font, canvas, shoe);

        GameObject betStack = CreateRectObject("Bet Chips", transform, new Vector2(88f, -98f), new Vector2(86f, 26f));
        betChips = new BlackjackChipView[4];
        for (int i = 0; i < betChips.Length; i++)
        {
            GameObject chip = CreateRectObject($"Bet Chip {i + 1}", betStack.transform, new Vector2((i - 1.5f) * 17f, 0f), new Vector2(21f, 21f));
            betChips[i] = chip.AddComponent<BlackjackChipView>();
            betChips[i].Configure(500, font, new Vector2(21f, 21f), false);
        }

        seatLabel = $"SEAT {seatNumber}";
        nameText.text = seatLabel;
    }

    public void Render(CasinoSeat seat, BlackjackHand hand, bool active, bool awaitingCard, bool queuedForNextRound, int hiddenCardIndex, bool revealAll)
    {
        background.color = active
            ? new Color(0.45f, 0.32f, 0.08f, 0.98f)
            : new Color(0.035f, 0.24f, 0.17f, 0.84f);

        CasinoCustomerAI customer = hand != null ? hand.Customer : seat != null ? seat.Occupant : null;
        nameText.text = customer != null ? customer.name.ToUpperInvariant() : seatLabel;

        if (hand != null)
        {
            handView.Render(hand.Cards, hiddenCardIndex, revealAll, hand.Status, true);
            statusText.text = StatusLabel(hand.Status, active && awaitingCard);
            statusText.color = StatusColor(hand.Status, active && awaitingCard);
            betText.text = hand.BetPence > 0 ? "BET" : "";
            for (int i = 0; i < betChips.Length; i++)
            {
                betChips[i].gameObject.SetActive(!hand.PayoutPaid && i < hand.BetPence / 500);
            }
        }
        else
        {
            handView.Render(System.Array.Empty<BlackjackCard>(), -1, true, BlackjackHandStatus.Waiting, false);
            statusText.text = customer == null ? "OPEN" : queuedForNextRound ? "NEXT ROUND" : "WAITING";
            statusText.color = Color.white;
            betText.text = customer == null ? "" : "READY";
            for (int i = 0; i < betChips.Length; i++)
            {
                betChips[i].gameObject.SetActive(false);
            }
        }
    }

    private static string StatusLabel(BlackjackHandStatus status, bool active)
    {
        if (active && status == BlackjackHandStatus.Playing) return "HIT!";
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

    private static Color StatusColor(BlackjackHandStatus status, bool awaitingCard)
    {
        if (awaitingCard) return new Color(1f, 0.82f, 0.30f);
        switch (status)
        {
            case BlackjackHandStatus.Bust:
            case BlackjackHandStatus.Lose:
                return new Color(1f, 0.40f, 0.34f);
            case BlackjackHandStatus.Blackjack:
            case BlackjackHandStatus.Win:
                return new Color(0.48f, 1f, 0.56f);
            case BlackjackHandStatus.Push:
                return new Color(1f, 0.84f, 0.38f);
            default:
                return Color.white;
        }
    }

    private static GameObject CreateRectObject(string objectName, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = rect.anchorMin;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return child;
    }

    private static Text CreateText(string objectName, Transform parent, Font font, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
    {
        GameObject child = CreateRectObject(objectName, parent, position, size);
        Text text = child.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }
}
