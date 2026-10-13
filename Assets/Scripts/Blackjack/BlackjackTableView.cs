using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BlackjackTableView : MonoBehaviour
{
    private const int ReferenceWidth = 1280;
    private const int ReferenceHeight = 720;

    private Canvas canvas;
    private GameObject board;
    private GameObject interactionPrompt;
    private Text promptText;
    private Text messageText;
    private Text dealerLabel;
    private Text accuracyText;
    private BlackjackHandView dealerHand;
    private BlackjackBettingSpotView[] bettingSpots;
    private readonly List<BlackjackChipView> selectedChipViews = new List<BlackjackChipView>();
    private BlackjackChipDenomination[] chipDenominations;
    private RectTransform selectedChipRow;
    private Button dealButton;
    private Button closeButton;
    private Button[] denominationButtons;
    private Button removeChipButton;
    private Button confirmPayoutButton;
    private Font font;
    private RectTransform shoePosition;
    private Action onDeal;
    private Action onClose;
    private Action<int> onAddChip;
    private Action onRemoveChip;
    private Action onConfirmPayout;
    private string tableDisplayName = "BLACKJACK";

    public string TableDisplayName => tableDisplayName;

    public void Configure(
        Action dealAction,
        Action closeAction,
        Action<int> addChipAction,
        Action removeChipAction,
        Action confirmPayoutAction,
        BlackjackChipDenomination[] denominations,
        string displayName = "BLACKJACK")
    {
        onDeal = dealAction;
        onClose = closeAction;
        onAddChip = addChipAction;
        onRemoveChip = removeChipAction;
        onConfirmPayout = confirmPayoutAction;
        chipDenominations = denominations;
        tableDisplayName = string.IsNullOrWhiteSpace(displayName) ? "BLACKJACK" : displayName.ToUpperInvariant();
        EnsureInputEventSystem();
        Build();
    }

    public void SetVisible(bool visible)
    {
        if (board == null) return;
        if (!visible)
        {
            BlackjackCardView[] cards = board.GetComponentsInChildren<BlackjackCardView>(true);
            foreach (BlackjackCardView card in cards) card.CompleteAnimation();
        }

        board.SetActive(visible);
    }

    public void SetPromptVisible(bool visible, string prompt)
    {
        if (interactionPrompt == null) return;
        interactionPrompt.SetActive(visible);
        promptText.text = prompt;
    }

    public void Refresh(
        BlackjackTableState state,
        CasinoSeat[] seats,
        BlackjackTableRound round,
        int activeHandIndex,
        bool revealDealerHole,
        int payoutIndex,
        IReadOnlyList<int> selectedPayoutChips,
        int accuracyScore,
        int penaltyCount,
        bool awaitingCustomerCard,
        bool dealerAutomated,
        string message)
    {
        if (board == null) return;

        dealerLabel.text = round == null || round.DealerCards.Count < 2
            ? "DEALER"
            : revealDealerHole
                ? $"DEALER  {round.DealerTotal}"
                : "DEALER  •  HOLE CARD";
        dealerHand.Render(
            round != null ? round.DealerCards : Array.Empty<BlackjackCard>(),
            hiddenCardIndex: 1,
            revealAll: revealDealerHole,
            BlackjackHandStatus.Waiting,
            showStatus: false);

        for (int seatIndex = 0; seatIndex < bettingSpots.Length; seatIndex++)
        {
            CasinoSeat seat = seats[seatIndex];
            BlackjackHand hand = FindHand(round, seat);
            bool active = hand != null && round.CustomerHands.IndexOf(hand) == activeHandIndex;
            bool queued = seat != null && seat.Occupant != null && hand == null && round != null;
            bool awaitingCard = active && state == BlackjackTableState.CustomerTurns && awaitingCustomerCard;
            bettingSpots[seatIndex].Render(seat, hand, active, awaitingCard, queued, -1, revealAll: true);
        }

        messageText.text = message;
        accuracyText.text = $"PAYOUT ACCURACY  {accuracyScore}    MISCOUNTS  {penaltyCount}";

        bool hasWaitingCustomer = seats != null && Array.Exists(seats, seat => seat != null && seat.Occupant != null);
        bool canDeal = state == BlackjackTableState.WaitingForCustomers && hasWaitingCustomer ||
                       state == BlackjackTableState.TakingBets ||
                       state == BlackjackTableState.InitialDeal ||
                       state == BlackjackTableState.CustomerTurns ||
                       state == BlackjackTableState.DealerTurn;
        dealButton.gameObject.SetActive(canDeal && !dealerAutomated);
        dealButton.GetComponentInChildren<Text>().text = dealerAutomated
            ? "DEALER WORKING"
            : state == BlackjackTableState.WaitingForCustomers
                ? "START ROUND"
                : state == BlackjackTableState.TakingBets
                    ? "DEAL OPENING CARDS"
                    : "DEAL CARD";

        bool isPayout = state == BlackjackTableState.Settlement && payoutIndex >= 0;
        foreach (Button denominationButton in denominationButtons) denominationButton.gameObject.SetActive(isPayout && !dealerAutomated);
        removeChipButton.gameObject.SetActive(isPayout && selectedPayoutChips.Count > 0 && !dealerAutomated);
        confirmPayoutButton.gameObject.SetActive(isPayout && !dealerAutomated);
        closeButton.gameObject.SetActive(true);

        RenderSelectedChips(selectedPayoutChips);
        closeButton.GetComponentInChildren<Text>().text = "ESC  •  CLOSE";
    }

    private void Build()
    {
        if (Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") is not Font builtinFont)
        {
            throw new InvalidOperationException("Unity's LegacyRuntime.ttf font is required to render the blackjack table.");
        }

        font = builtinFont;
        GameObject canvasObject = new GameObject("Blackjack Table UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.matchWidthOrHeight = 0.5f;

        CreatePrompt();
        BuildTableBoard();
    }

    private void BuildTableBoard()
    {
        board = CreateUiObject("Table Rim", canvas.transform);
        SetRect(board, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1050f, 590f));
        Image rimImage = board.AddComponent<Image>();
        rimImage.color = new Color(0.22f, 0.095f, 0.035f, 0.99f);
        rimImage.raycastTarget = false;

        GameObject felt = CreateUiObject("Green Felt Table", board.transform);
        SetRect(felt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1018f, 558f));
        Image feltImage = felt.AddComponent<Image>();
        feltImage.color = new Color(0.025f, 0.30f, 0.20f, 0.98f);
        feltImage.raycastTarget = false;
        CreateText("Title", felt.transform, tableDisplayName, new Vector2(0f, 245f), new Vector2(420f, 34f), 22, new Color(0.97f, 0.80f, 0.39f), TextAnchor.MiddleCenter);
        dealerLabel = CreateText("Dealer Label", felt.transform, "DEALER", new Vector2(0f, 210f), new Vector2(360f, 28f), 17, Color.white, TextAnchor.MiddleCenter);

        shoePosition = CreateRectObject("Shoe Position", felt.transform, new Vector2(-425f, 164f), new Vector2(54f, 78f)).GetComponent<RectTransform>();
        Image shoeImage = shoePosition.gameObject.AddComponent<Image>();
        shoeImage.color = new Color(0.10f, 0.16f, 0.24f);
        CreateText("Shoe Mark", shoePosition, "DECK", Vector2.zero, new Vector2(54f, 78f), 11, new Color(0.92f, 0.82f, 0.48f), TextAnchor.MiddleCenter);

        GameObject dealerHandObject = CreateRectObject("Dealer Hand", felt.transform, new Vector2(0f, 157f), new Vector2(300f, 105f));
        dealerHand = dealerHandObject.AddComponent<BlackjackHandView>();
        dealerHand.Configure(font, canvas, shoePosition);

        bettingSpots = new BlackjackBettingSpotView[3];
        float[] xPositions = { -326f, 0f, 326f };
        for (int i = 0; i < bettingSpots.Length; i++)
        {
            GameObject spotObject = CreateRectObject($"Betting Spot {i + 1}", felt.transform, new Vector2(xPositions[i], -42f), new Vector2(300f, 218f));
            bettingSpots[i] = spotObject.AddComponent<BlackjackBettingSpotView>();
            bettingSpots[i].Configure(i + 1, font, canvas, shoePosition, chipDenominations);
        }

        messageText = CreateText("Round Prompt", felt.transform, "", new Vector2(0f, -176f), new Vector2(840f, 34f), 17, new Color(1f, 0.86f, 0.47f), TextAnchor.MiddleCenter);
        accuracyText = CreateText("Accuracy", felt.transform, "", new Vector2(0f, -207f), new Vector2(650f, 22f), 12, new Color(0.78f, 0.86f, 0.81f), TextAnchor.MiddleCenter);

        selectedChipRow = CreateRectObject("Payout Chips", felt.transform, new Vector2(330f, 155f), new Vector2(220f, 35f)).GetComponent<RectTransform>();
        dealButton = CreateButton("Deal Button", felt.transform, "START ROUND  •  SPACE", new Vector2(367f, -238f), new Vector2(250f, 42f), new Color(0.78f, 0.55f, 0.16f), () => onDeal?.Invoke());
        closeButton = CreateButton("Close Button", felt.transform, "ESC  •  CLOSE", new Vector2(-423f, 245f), new Vector2(130f, 36f), new Color(0.20f, 0.25f, 0.23f), () => onClose?.Invoke());
        denominationButtons = new Button[chipDenominations.Length];
        float chipStartX = -((chipDenominations.Length + 1) * 45f);
        for (int i = 0; i < chipDenominations.Length; i++)
        {
            BlackjackChipDenomination chip = chipDenominations[i];
            int denominationValue = chip.ValuePence;
            string label = $"{chip.Label} CHIP";
            float positionX = chipStartX + i * 90f;
            denominationButtons[i] = CreateButton(
                $"Chip {label}",
                felt.transform,
                label,
                new Vector2(positionX, -238f),
                new Vector2(82f, 38f),
                chip.Color,
                () => onAddChip?.Invoke(denominationValue));
        }

        removeChipButton = CreateButton("Remove Chip", felt.transform, "REMOVE", new Vector2(91f, -238f), new Vector2(82f, 38f), new Color(0.25f, 0.29f, 0.28f), () => onRemoveChip?.Invoke());
        confirmPayoutButton = CreateButton("Confirm Payout", felt.transform, "CONFIRM PAYOUT", new Vector2(190f, -238f), new Vector2(106f, 38f), new Color(0.20f, 0.44f, 0.28f), () => onConfirmPayout?.Invoke());

        board.SetActive(false);
    }

    private void CreatePrompt()
    {
        interactionPrompt = CreateUiObject("Dealer Interaction Prompt", canvas.transform);
        SetRect(interactionPrompt, new Vector2(0.5f, 0f), new Vector2(0f, 45f), new Vector2(420f, 42f));
        interactionPrompt.AddComponent<Image>().color = new Color(0.025f, 0.06f, 0.05f, 0.90f);
        promptText = CreateText("Prompt", interactionPrompt.transform, $"E  •  DEAL {tableDisplayName}", Vector2.zero, new Vector2(410f, 40f), 18, Color.white, TextAnchor.MiddleCenter);
        interactionPrompt.SetActive(false);
    }

    private void RenderSelectedChips(IReadOnlyList<int> chips)
    {
        while (selectedChipViews.Count > chips.Count)
        {
            int last = selectedChipViews.Count - 1;
            Destroy(selectedChipViews[last].gameObject);
            selectedChipViews.RemoveAt(last);
        }

        for (int i = 0; i < chips.Count; i++)
        {
            if (selectedChipViews.Count == i)
            {
                GameObject chipObject = CreateRectObject("Selected Payout Chip", selectedChipRow, Vector2.zero, new Vector2(30f, 30f));
                selectedChipViews.Add(chipObject.AddComponent<BlackjackChipView>());
                selectedChipViews[i].Configure(FindChipDenomination(chips[i]), font, new Vector2(30f, 30f));
            }
            else
            {
                selectedChipViews[i].SetValue(chips[i], chipDenominations);
            }

            RectTransform rect = selectedChipViews[i].GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2((i - (chips.Count - 1) * 0.5f) * 33f, 0f);
        }
    }

    private void EnsureInputEventSystem()
    {
        EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
        }

        BaseInputModule inputModule = eventSystem.GetComponent<BaseInputModule>();
        if (!(inputModule is InputSystemUIInputModule))
        {
            if (inputModule != null)
            {
                Destroy(inputModule);
            }

            InputSystemUIInputModule inputSystemModule =
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            inputSystemModule.AssignDefaultActions();
        }

        if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
        {
            throw new InvalidOperationException("The EventSystem requires Unity's Input System UI Input Module.");
        }
    }

    private static BlackjackHand FindHand(BlackjackTableRound round, CasinoSeat seat)
    {
        if (round == null || seat == null) return null;
        for (int i = 0; i < round.CustomerHands.Count; i++)
        {
            if (round.CustomerHands[i].Seat == seat) return round.CustomerHands[i];
        }

        return null;
    }

    private BlackjackChipDenomination FindChipDenomination(int valuePence)
    {
        for (int i = 0; i < chipDenominations.Length; i++)
        {
            if (chipDenominations[i].ValuePence == valuePence)
            {
                return chipDenominations[i];
            }
        }

        throw new InvalidOperationException($"No blackjack chip denomination is configured for {valuePence} pence.");
    }

    private Button CreateButton(string objectName, Transform parent, string label, Vector2 position, Vector2 size, Color color, Action action)
    {
        GameObject buttonObject = CreateUiObject(objectName, parent);
        SetRect(buttonObject, new Vector2(0.5f, 0.5f), position, size);
        Image image = buttonObject.AddComponent<Image>();
        image.color = color;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => action?.Invoke());
        CreateText("Label", buttonObject.transform, label, Vector2.zero, size, 12, Color.white, TextAnchor.MiddleCenter);
        return button;
    }

    private Text CreateText(string objectName, Transform parent, string value, Vector2 position, Vector2 size, int fontSize, Color color, TextAnchor alignment)
    {
        GameObject textObject = CreateUiObject(objectName, parent);
        SetRect(textObject, new Vector2(0.5f, 0.5f), position, size);
        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject uiObject = new GameObject(objectName, typeof(RectTransform));
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    private static GameObject CreateRectObject(string objectName, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject uiObject = CreateUiObject(objectName, parent);
        SetRect(uiObject, new Vector2(0.5f, 0.5f), position, size);
        return uiObject;
    }

    private static void SetRect(GameObject uiObject, Vector2 anchor, Vector2 position, Vector2 size)
    {
        RectTransform rect = uiObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}

internal static class BlackjackReadOnlyListExtensions
{
    public static int IndexOf<T>(this IReadOnlyList<T> list, T item) where T : class
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (ReferenceEquals(list[i], item)) return i;
        }

        return -1;
    }
}
