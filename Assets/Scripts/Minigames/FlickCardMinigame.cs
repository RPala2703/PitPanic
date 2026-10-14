using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class FlickCardMinigame : CasinoMinigame
{
    private static readonly string[] SuitSymbols = { "♠", "♥", "♦", "♣" };
    private static readonly Color RedInk = new Color(0.76f, 0.08f, 0.08f);
    private static readonly Color BlackInk = new Color(0.07f, 0.09f, 0.12f);

    [SerializeField, Min(0.2f)] private float timeAllowance = 20f;
    [SerializeField, Min(0.05f)] private float cardFlightSeconds = 0.24f;
    [SerializeField] private BlackjackTableInteraction table;
    [SerializeField] private CasinoPlayerController player;
    [SerializeField] private Transform tableRoot;

    private sealed class CardVisual
    {
        public RectTransform Rect;
        public Text Face;
        public Image Background;
    }

    private sealed class HandTarget
    {
        public int Id;
        public RectTransform Rect;
        public Outline Outline;
        public Text Label;
        public readonly List<CardVisual> Cards = new List<CardVisual>();
    }

    private readonly List<HandTarget> handTargets = new List<HandTarget>();
    private Transform gameArea;
    private RectTransform shoeRect;
    private RectTransform heldCardRect;
    private CardVisual heldVisual;
    private Text progressText;
    private Text timerText;
    private Text feedbackText;
    private CardVisual flyingCard;
    private Vector2 flightStart;
    private Vector2 flightEnd;
    private float flightElapsed;
    private float timeRemaining;
    private CursorLockMode savedCursorLockMode;
    private bool savedCursorVisible;
    private bool controlsCaptured;
    private bool isDragging;
    private bool waitingForFlight;
    private int successfulFlicks;
    private int missedFlicks;
    private int requiredFlicks;

    public override bool CanBegin => table != null && table.IsFlickReady &&
                                     table.ActiveRound != null && table.RemainingFlickCardCount > 0 &&
                                     player != null && tableRoot != null && Mouse.current != null;
    protected override string PresentationTitle =>
        table != null ? $"FLICK!  •  {table.TableDisplayName}" : "FLICK! DEAL THE OPENING HANDS";
    protected override string PresentationInstructions =>
        "Click and hold the card shoe, drag to the glowing hand, then release to deal.";
    public int SuccessfulFlicks => successfulFlicks;
    public int MissedFlicks => missedFlicks;
    public bool IsConfigured => table != null && player != null && tableRoot != null;
    public bool IsDraggingCard => isDragging;
    public bool IsWaitingForCardFlight => waitingForFlight;

    public static int RemainingOpeningCards(int activeHands) =>
        activeHands <= 0 ? 0 : activeHands + 1;

    public static int RemainingOpeningCards(int activeHands, int dealStep) =>
        activeHands <= 0 || dealStep < 0
            ? 0
            : Mathf.Clamp(activeHands * 2 + 2 - dealStep, 0, activeHands * 2 + 2);

    public static int OpeningTargetForStep(int activeHands, int dealStep)
    {
        if (activeHands <= 0 || dealStep < 0 || dealStep >= activeHands * 2 + 2)
        {
            return -2;
        }

        if (dealStep < activeHands)
        {
            return dealStep;
        }

        if (dealStep == activeHands || dealStep > activeHands * 2)
        {
            return -1;
        }

        return dealStep - activeHands - 1;
    }

    public static bool IsValidFlickTarget(int targetIndex, int expectedTarget, bool cardInFlight) =>
        !cardInFlight && expectedTarget != -2 && targetIndex == expectedTarget;

    public static bool CanCompleteChallenge(int successfulDeals, int requiredDeals, int remainingDeals) =>
        requiredDeals > 0 && successfulDeals == requiredDeals && remainingDeals == 0;

    public void Configure(
        BlackjackTableInteraction blackjackTable,
        CasinoPlayerController casinoPlayer,
        Transform blackjackTableRoot)
    {
        table = blackjackTable;
        player = casinoPlayer;
        tableRoot = blackjackTableRoot;
    }

    protected override void OnBegin()
    {
        if (!CanBegin || !table.TryPrepareFlickAssistance() || Presentation == null ||
            Presentation.GameArea == null)
        {
            Complete(false, 0);
            return;
        }

        savedCursorLockMode = Cursor.lockState;
        savedCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        controlsCaptured = true;
        player.SetMovementEnabled(false);
        timeRemaining = timeAllowance;
        successfulFlicks = 0;
        missedFlicks = 0;
        requiredFlicks = table.RemainingFlickCardCount;
        isDragging = false;
        waitingForFlight = false;
        heldCardRect = null;
        heldVisual = null;
        flyingCard = null;
        handTargets.Clear();
        gameArea = Presentation.GameArea;

        BuildPopup();
        RenderHands();
        RefreshHud();
        SetFeedback("GRAB THE SHOE, AIM AT THE GLOWING HAND, THEN RELEASE.");
    }

    protected override void OnTick(float deltaTime)
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (table != null && table.RemainingFlickCardCount == 0)
            {
                Finish(true);
            }
            else
            {
                Abandon();
            }

            return;
        }

        if (table == null || player == null || gameArea == null)
        {
            Debug.LogError($"{name}: FLICK lost its blackjack table, player, or popup and is exiting safely.", this);
            table?.AbortFlickAssistance();
            Complete(false, 0);
            return;
        }

        timeRemaining = Mathf.Max(0f, timeRemaining - deltaTime);
        UpdatePointerInput();
        if (waitingForFlight)
        {
            AnimateCardFlight(deltaTime);
        }

        RefreshHud();
        if (timeRemaining <= 0f && !waitingForFlight)
        {
            SetFeedback($"TIME! {successfulFlicks}/{requiredFlicks} CARDS. THE TASK IS READY TO RETRY.");
            Complete(false, Mathf.Max(0, successfulFlicks * 100 - missedFlicks * 25));
        }
    }

    protected override void OnAbandon()
    {
        DestroyHeldCard();
        waitingForFlight = false;
        flyingCard = null;
        RestorePlayerAndCursor();
        handTargets.Clear();
        if (table != null)
        {
            table.EndFlickAssistance(cancelled: true);
            table.NotifyFlickAttemptFailed();
        }
    }

    protected override void OnComplete(bool success)
    {
        DestroyHeldCard();
        waitingForFlight = false;
        flyingCard = null;
        RestorePlayerAndCursor();
        handTargets.Clear();
        if (table != null)
        {
            if (success)
            {
                table.EndFlickAssistance();
            }
            else
            {
                table.NotifyFlickAttemptFailed();
            }
        }
    }

    private void OnDestroy()
    {
        if (IsRunning)
        {
            Abandon();
        }
        else
        {
            RestorePlayerAndCursor();
        }
    }

    private void BuildPopup()
    {
        GameObject statusPlate = CasinoMinigamePresentation.CreateImage(
            "FLICK Status Plate", gameArea, new Vector2(0f, 250f), new Vector2(1300f, 64f),
            new Color(0.025f, 0.12f, 0.09f, 0.96f));
        progressText = CasinoMinigamePresentation.CreateText(
            statusPlate.transform, "FLICK Progress", "FLICK!  0/0  •  0 REMAINING",
            new Vector2(-300f, 0f), new Vector2(560f, 54f), 24, Color.white);
        timerText = CasinoMinigamePresentation.CreateText(
            statusPlate.transform, "FLICK Timer", "TIME  20.0",
            new Vector2(390f, 0f), new Vector2(260f, 54f), 23,
            new Color(1f, 0.86f, 0.35f));

        GameObject shoe = CasinoMinigamePresentation.CreateImage(
            "FLICK Clickable Card Shoe", gameArea, new Vector2(-535f, 6f), new Vector2(168f, 238f),
            new Color(0.07f, 0.15f, 0.25f));
        shoeRect = shoe.GetComponent<RectTransform>();
        Outline shoeOutline = shoe.AddComponent<Outline>();
        shoeOutline.effectColor = new Color(0.91f, 0.70f, 0.24f);
        shoeOutline.effectDistance = new Vector2(4f, -4f);
        for (int i = 0; i < 3; i++)
        {
            CasinoMinigamePresentation.CreateImage(
                $"Shoe Card Stack {i + 1}", shoe.transform,
                new Vector2((i - 1) * 7f, -5f - i * 4f), new Vector2(78f, 104f),
                i == 2 ? new Color(0.92f, 0.92f, 0.84f) : new Color(0.16f, 0.30f, 0.44f));
        }
        CasinoMinigamePresentation.CreateText(shoe.transform, "Shoe Label", "CARD SHOE",
            new Vector2(0f, 88f), new Vector2(148f, 42f), 20, Color.white);
        CasinoMinigamePresentation.CreateText(shoe.transform, "Shoe Action", "CLICK + HOLD",
            new Vector2(0f, -90f), new Vector2(148f, 32f), 13,
            new Color(1f, 0.85f, 0.42f));

        CasinoMinigamePresentation.CreateText(gameArea, "FLICK Feedback", "",
            new Vector2(0f, -270f), new Vector2(1260f, 42f), 19,
            new Color(1f, 0.88f, 0.43f));
        feedbackText = gameArea.Find("FLICK Feedback").GetComponent<Text>();
        CreateHandTargets();
    }

    private void CreateHandTargets()
    {
        BlackjackTableRound round = table.ActiveRound;
        int count = round.CustomerHands.Count;
        float[] customerX = count switch
        {
            1 => new[] { 255f },
            2 => new[] { 85f, 425f },
            _ => new[] { -85f, 245f, 575f }
        };

        CreateTarget(-1, "DEALER", new Vector2(245f, 122f));
        for (int i = 0; i < count; i++)
        {
            BlackjackHand hand = round.CustomerHands[i];
            string name = hand.Customer != null ? hand.Customer.name : $"CUSTOMER {i + 1}";
            CreateTarget(i, $"SEAT {i + 1}  •  {name.ToUpperInvariant()}", new Vector2(customerX[i], -106f));
        }
    }

    private void CreateTarget(int targetId, string label, Vector2 position)
    {
        GameObject targetObject = CasinoMinigamePresentation.CreateImage(
            targetId < 0 ? "Dealer Card Target" : $"Customer {targetId + 1} Card Target",
            gameArea, position, new Vector2(280f, 205f), new Color(0.02f, 0.14f, 0.10f, 0.95f));
        Outline outline = targetObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.24f, 0.64f, 0.44f);
        outline.effectDistance = new Vector2(2f, -2f);
        CasinoMinigamePresentation.CreateText(targetObject.transform, "Hand Label", label,
            new Vector2(0f, 76f), new Vector2(264f, 32f), 14, Color.white);
        Text handStatus = CasinoMinigamePresentation.CreateText(
            targetObject.transform, "Hand Status", targetId < 0 ? "DEALER" : "TOTAL  0",
            new Vector2(0f, -80f), new Vector2(264f, 27f), 15,
            new Color(1f, 0.86f, 0.40f));
        handTargets.Add(new HandTarget
        {
            Id = targetId,
            Rect = targetObject.GetComponent<RectTransform>(),
            Outline = outline,
            Label = handStatus
        });
    }

    private void RenderHands()
    {
        BlackjackTableRound round = table.ActiveRound;
        if (round == null)
        {
            return;
        }

        foreach (HandTarget target in handTargets)
        {
            foreach (CardVisual visual in target.Cards)
            {
                if (visual.Rect != null)
                {
                    Destroy(visual.Rect.gameObject);
                }
            }

            target.Cards.Clear();
            IReadOnlyList<BlackjackCard> cards = target.Id < 0
                ? round.DealerCards
                : round.CustomerHands[target.Id].Cards;
            for (int i = 0; i < cards.Count; i++)
            {
                bool hidden = target.Id < 0 && i == 1;
                CardVisual visual = CreateCardVisual(gameArea, cards[i], hidden, new Vector2(70f, 96f));
                visual.Rect.anchoredPosition = GetCardPosition(target, i, cards.Count);
                target.Cards.Add(visual);
            }

            if (target.Id >= 0)
            {
                target.Label.text = $"TOTAL  {round.CustomerHands[target.Id].Total}";
            }
            else
            {
                target.Label.text = cards.Count > 1 ? "HOLE CARD HIDDEN" : "UP-CARD";
            }
        }

        UpdateTargetHighlight();
    }

    private static Vector2 GetCardPosition(HandTarget target, int cardIndex, int cardCount)
    {
        float offset = (cardIndex - (cardCount - 1) * 0.5f) * 53f;
        return target.Rect.anchoredPosition + new Vector2(offset, 4f);
    }

    private CardVisual CreateCardVisual(Transform parent, BlackjackCard card, bool hidden, Vector2 size)
    {
        GameObject cardObject = CasinoMinigamePresentation.CreateImage(
            hidden ? "Face Down Playing Card" : $"Playing Card {FormatCard(card)}",
            parent, Vector2.zero, size, hidden ? new Color(0.12f, 0.25f, 0.39f) : new Color(0.98f, 0.97f, 0.91f));
        Outline outline = cardObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.07f, 0.10f, 0.09f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);
        Text face = CasinoMinigamePresentation.CreateText(
            cardObject.transform, "Card Rank And Suit", hidden ? "PIT" : FormatCard(card),
            new Vector2(0f, 0f), new Vector2(size.x - 8f, size.y - 8f), hidden ? 14 : 25,
            hidden ? new Color(1f, 0.84f, 0.37f) : GetSuitColor(card.Suit));
        return new CardVisual
        {
            Rect = cardObject.GetComponent<RectTransform>(),
            Face = face,
            Background = cardObject.GetComponent<Image>()
        };
    }

    private void UpdatePointerInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || waitingForFlight)
        {
            return;
        }

        Vector2 pointer = mouse.position.ReadValue();
        if (mouse.leftButton.wasPressedThisFrame && !isDragging &&
            CasinoMinigamePresentation.ContainsScreenPoint(shoeRect, pointer))
        {
            isDragging = true;
            heldVisual = CreateCardVisual(gameArea, default, true, new Vector2(76f, 105f));
            heldCardRect = heldVisual.Rect;
            MoveToPointer(heldCardRect, pointer);
            SetFeedback("AIM AT THE GLOWING HAND AND RELEASE.");
        }

        if (isDragging && heldCardRect != null && mouse.leftButton.isPressed)
        {
            MoveToPointer(heldCardRect, pointer);
        }

        if (isDragging && mouse.leftButton.wasReleasedThisFrame)
        {
            isDragging = false;
            int target = FindTargetAtScreenPoint(pointer);
            int expected = table.GetNextFlickTarget();
            if (!IsValidFlickTarget(target, expected, waitingForFlight))
            {
                missedFlicks++;
                DestroyHeldCard();
                SetFeedback(target == -2
                    ? "MISS! RELEASE THE CARD INSIDE THE GLOWING HAND."
                    : "WRONG HAND! TRY THE HIGHLIGHTED TARGET.");
                UpdateTargetHighlight();
                return;
            }

            AcceptCard(target);
        }
    }

    private void AcceptCard(int targetId)
    {
        HandTarget target = FindTarget(targetId);
        if (heldVisual == null || heldCardRect == null || target == null ||
            !table.TryDealFlickCard(targetId, out BlackjackCard card, out int cardIndex, out bool holeCard))
        {
            DestroyHeldCard();
            SetFeedback("THE DEAL IS PAUSED. PICK UP ANOTHER CARD TO RETRY.");
            return;
        }

        successfulFlicks++;
        CardVisual visual = heldVisual;
        SetCardVisual(visual, card, holeCard);

        heldCardRect = null;
        heldVisual = null;
        int handCardCount = targetId < 0
            ? table.ActiveRound.DealerCards.Count
            : table.ActiveRound.CustomerHands[targetId].Cards.Count;
        flyingCard = visual;
        flightStart = visual.Rect.anchoredPosition;
        flightEnd = GetCardPosition(target, cardIndex, handCardCount);
        flightElapsed = 0f;
        waitingForFlight = true;
        SetFeedback(targetId < 0 ? "DEALER CARD!" : "NICE DEAL!");
        UpdateTargetHighlight();
    }

    private int FindTargetAtScreenPoint(Vector2 screenPoint)
    {
        foreach (HandTarget target in handTargets)
        {
            if (CasinoMinigamePresentation.ContainsScreenPoint(target.Rect, screenPoint))
            {
                return target.Id;
            }
        }

        return -2;
    }

    private HandTarget FindTarget(int targetId)
    {
        return handTargets.Find(target => target.Id == targetId);
    }

    private void MoveToPointer(RectTransform rect, Vector2 screenPoint)
    {
        if (rect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                gameArea as RectTransform, screenPoint, null, out Vector2 localPoint))
        {
            rect.anchoredPosition = localPoint;
        }
    }

    private void AnimateCardFlight(float deltaTime)
    {
        if (flyingCard == null || flyingCard.Rect == null)
        {
            waitingForFlight = false;
            flyingCard = null;
            RenderHands();
            return;
        }

        flightElapsed += deltaTime;
        float t = Mathf.Clamp01(flightElapsed / Mathf.Max(0.05f, cardFlightSeconds));
        float eased = 1f - Mathf.Pow(1f - t, 3f);
        flyingCard.Rect.anchoredPosition = Vector2.Lerp(flightStart, flightEnd, eased);
        flyingCard.Rect.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.12f);
        if (t < 1f)
        {
            return;
        }

        if (flyingCard.Rect != null)
        {
            Destroy(flyingCard.Rect.gameObject);
        }

        waitingForFlight = false;
        flyingCard = null;
        RenderHands();
        if (table.RemainingFlickCardCount == 0)
        {
            SetFeedback("ALL OPENING CARDS DEALT!");
            Finish(true);
        }
    }

    private void UpdateTargetHighlight(int expected = int.MinValue)
    {
        if (expected == int.MinValue && table != null)
        {
            expected = table.GetNextFlickTarget();
        }

        foreach (HandTarget target in handTargets)
        {
            bool highlighted = target.Id == expected;
            target.Outline.effectColor = highlighted
                ? new Color(1f, 0.84f, 0.22f, 1f)
                : new Color(0.24f, 0.64f, 0.44f, 1f);
            target.Outline.effectDistance = highlighted
                ? new Vector2(5f, -5f)
                : new Vector2(2f, -2f);
            Image image = target.Rect.GetComponent<Image>();
            image.color = highlighted
                ? new Color(0.11f, 0.25f, 0.14f, 0.99f)
                : new Color(0.02f, 0.14f, 0.10f, 0.95f);
        }
    }

    private void RefreshHud()
    {
        if (progressText != null && table != null)
        {
            progressText.text =
                $"FLICK!  {successfulFlicks}/{requiredFlicks}  •  {table.RemainingFlickCardCount} CARDS LEFT  •  {missedFlicks} MISSES";
        }

        if (timerText != null)
        {
            timerText.text = $"TIME  {timeRemaining:0.0}";
            timerText.color = timeRemaining <= 5f
                ? new Color(1f, 0.44f, 0.32f)
                : new Color(1f, 0.86f, 0.35f);
        }
    }

    private void SetFeedback(string value)
    {
        if (feedbackText == null)
        {
            return;
        }

        feedbackText.text = value;
        feedbackText.color = value.StartsWith("MISS") || value.StartsWith("WRONG") ||
                             value.StartsWith("TIME")
            ? new Color(1f, 0.47f, 0.36f)
            : new Color(1f, 0.88f, 0.43f);
    }

    private void Finish(bool success)
    {
        if (success && (table == null || table.RemainingFlickCardCount != 0))
        {
            Debug.LogError($"{name}: FLICK attempted to complete before all cards were dealt.", this);
            success = false;
        }

        Complete(success, success
            ? Mathf.Max(0, successfulFlicks * 100 + Mathf.RoundToInt(timeRemaining * 10f) - missedFlicks * 25)
            : Mathf.Max(0, successfulFlicks * 100 - missedFlicks * 25));
    }

    private void DestroyHeldCard()
    {
        if (heldCardRect != null)
        {
            Destroy(heldCardRect.gameObject);
            heldCardRect = null;
        }

        heldVisual = null;
        isDragging = false;
    }

    private void RestorePlayerAndCursor()
    {
        if (!controlsCaptured)
        {
            return;
        }

        controlsCaptured = false;
        if (player != null)
        {
            player.SetMovementEnabled(true);
        }

        Cursor.lockState = savedCursorLockMode;
        Cursor.visible = savedCursorVisible;
    }

    public void ClearPhysicalCards()
    {
    }

    public static bool TryCreateCardFaceText(Transform cardTransform, BlackjackCard card, out TextMesh face)
    {
        face = null;
        if (cardTransform == null || card.Rank < 1 || card.Rank > 13 ||
            card.Suit < 0 || card.Suit >= SuitSymbols.Length)
        {
            return false;
        }

        Transform faceTransform = cardTransform.Find("Card Face");
        if (faceTransform == null)
        {
            GameObject faceObject = new GameObject("Card Face");
            faceObject.transform.SetParent(cardTransform, false);
            faceTransform = faceObject.transform;
        }

        face = faceTransform.GetComponent<TextMesh>();
        if (face == null)
        {
            face = faceTransform.gameObject.AddComponent<TextMesh>();
        }

        face.anchor = TextAnchor.MiddleCenter;
        face.alignment = TextAlignment.Center;
        face.fontSize = 80;
        face.characterSize = 0.015f;
        face.text = FormatCard(card);
        face.color = GetSuitColor(card.Suit);
        face.transform.localPosition = new Vector3(0f, 0.53f, 0f);
        face.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        return true;
    }

    private static void SetCardVisual(CardVisual visual, BlackjackCard card, bool hidden)
    {
        visual.Background.color = hidden
            ? new Color(0.12f, 0.25f, 0.39f)
            : new Color(0.98f, 0.97f, 0.91f);
        visual.Face.text = hidden ? "PIT" : FormatCard(card);
        visual.Face.fontSize = hidden ? 14 : 25;
        visual.Face.color = hidden ? new Color(1f, 0.84f, 0.37f) : GetSuitColor(card.Suit);
    }

    private static Color GetSuitColor(int suit) => suit == 1 || suit == 2 ? RedInk : BlackInk;

    private static string FormatCard(BlackjackCard card)
    {
        if (card.Rank < 1 || card.Rank > 13 || card.Suit < 0 || card.Suit >= SuitSymbols.Length)
        {
            return "?";
        }

        string rank = card.Rank == 1 ? "A" :
            card.Rank > 10 ? "JQK"[card.Rank - 11].ToString() : card.Rank.ToString();
        return $"{rank}{SuitSymbols[card.Suit]}";
    }
}
