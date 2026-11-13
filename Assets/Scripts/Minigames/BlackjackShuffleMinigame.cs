using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BlackjackShuffleMinigame : CasinoMinigame
{
    private const int CardsToShuffle = 8;
    private const float CardFlightSeconds = 0.42f;
    private static readonly Color Felt = new Color(0.035f, 0.20f, 0.15f);
    private static readonly Color CardCream = new Color(0.98f, 0.96f, 0.86f);

    [SerializeField, Min(8f)] private float timeAllowance = 20f;
    [SerializeField] private BlackjackTableInteraction table;
    [SerializeField] private CasinoPlayerController player;

    private readonly List<RectTransform> shuffledCards = new List<RectTransform>();
    private Transform gameArea;
    private RectTransform leftStackRect;
    private RectTransform rightStackRect;
    private Button leftStackButton;
    private Button rightStackButton;
    private Outline leftStackOutline;
    private Outline rightStackOutline;
    private Text leftCountText;
    private Text rightCountText;
    private Text progressText;
    private Text timerText;
    private Text promptText;
    private Text feedbackText;
    private Image flyingCard;
    private Vector2 flightStart;
    private Vector2 flightEnd;
    private float flightElapsed;
    private float timeRemaining;
    private int[] stackSequence = Array.Empty<int>();
    private int completedCards;
    private int misses;
    private int leftCardsRemaining;
    private int rightCardsRemaining;
    private bool waitingForFlight;
    private bool controlsCaptured;
    private CursorLockMode savedCursorLockMode;
    private bool savedCursorVisible;

    public override bool CanBegin => table != null && table.CanBeginShuffleAssistance() &&
                                     player != null;
    protected override string PresentationTitle => $"SHUFFLE!  •  {table.TableDisplayName}";
    protected override string PresentationInstructions => "CLICK THE GLOWING STACK. A WRONG STACK MEANS STARTING AGAIN!";
    public bool IsConfigured => table != null && player != null;
    public int CompletedCards => completedCards;
    public int RequiredCards => stackSequence.Length;
    public bool IsAnimatingCard => waitingForFlight;
    public int ExpectedStack => completedCards < stackSequence.Length ? stackSequence[completedCards] : -1;
    public float TimeRemaining => timeRemaining;

    protected override bool CarriesDeadlineBetweenAttempts => true;

    public override bool TryGetDeadline(out float remaining, out float duration)
    {
        remaining = timeRemaining;
        duration = timeAllowance;
        return IsRunning && remaining > 0;
    }

    public void Configure(BlackjackTableInteraction blackjackTable, CasinoPlayerController casinoPlayer)
    {
        table = blackjackTable;
        player = casinoPlayer;
    }

    public static int[] CreateStackSequence(int count, int seed)
    {
        if (count < 2 || count % 2 != 0)
            throw new ArgumentOutOfRangeException(nameof(count), "A shuffle needs an even number of cards.");

        System.Random random = new System.Random(seed);
        int[] sequence;
        do
        {
            sequence = new int[count];
            Array.Copy(new int[count / 2], 0, sequence, 0, count / 2);
            for (int i = count / 2; i < count; i++)
                sequence[i] = 1;
            for (int i = sequence.Length - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (sequence[i], sequence[swapIndex]) = (sequence[swapIndex], sequence[i]);
            }
        }
        while (HasTripleRepeat(sequence));

        return sequence;
    }

    private static bool HasTripleRepeat(IReadOnlyList<int> sequence)
    {
        for (int i = 2; i < sequence.Count; i++)
            if (sequence[i] == sequence[i - 1] && sequence[i] == sequence[i - 2])
                return true;
        return false;
    }

    public static bool IsCorrectStack(int expectedStack, int selectedStack) =>
        expectedStack >= 0 && expectedStack <= 1 && expectedStack == selectedStack;

    public static bool CanCompleteSequence(int successfulPicks, int requiredPicks, bool cardInFlight) =>
        requiredPicks > 0 && successfulPicks == requiredPicks && !cardInFlight;

    protected override void OnBegin()
    {
        if (!CanBegin || !table.TryPrepareShuffleAssistance() || Presentation?.GameArea == null)
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
        table.PlayShuffleAssistanceAction();

        stackSequence = CreateStackSequence(CardsToShuffle, UnityEngine.Random.Range(0, int.MaxValue));
        // A retry restarts the cards but keeps the task's original countdown.
        timeRemaining = ConsumeCarriedDeadline(timeAllowance);
        completedCards = 0;
        misses = 0;
        leftCardsRemaining = CardsToShuffle / 2;
        rightCardsRemaining = CardsToShuffle / 2;
        waitingForFlight = false;
        flyingCard = null;
        shuffledCards.Clear();
        gameArea = Presentation.GameArea;
        BuildPopup();
        RefreshHud();
        UpdateHighlights();
        SetFeedback("A little left, a little right. Follow the glow!");
    }

    protected override void OnTick(float deltaTime)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            Abandon();
            return;
        }

        if (table == null || player == null || gameArea == null)
        {
            Debug.LogError($"{name}: SHUFFLE lost its table, player, or popup and is exiting safely.", this);
            table?.AbortShuffleAssistance();
            Complete(false, 0);
            return;
        }

        timeRemaining = Mathf.Max(0f, timeRemaining - deltaTime);
        if (!waitingForFlight && keyboard != null)
        {
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
                SelectStack(0);
            else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
                SelectStack(1);
        }

        if (waitingForFlight)
            AnimateCardFlight(deltaTime);

        RefreshHud();
        if (timeRemaining <= 0f && !waitingForFlight)
        {
            SetFeedback("No worries! The dealer can try again.");
            Complete(false, completedCards * 100);
        }
    }

    protected override void OnAbandon()
    {
        DestroyFlyingCard();
        RestorePlayerAndCursor();
        if (table != null)
            table.EndShuffleAssistance(cancelled: true);
    }

    protected override void OnComplete(bool success)
    {
        DestroyFlyingCard();
        RestorePlayerAndCursor();
        if (table == null)
            return;

        if (success)
        {
            table.EndShuffleAssistance();
        }
        else
        {
            table.NotifyShuffleAttemptFailed();
        }
    }

    private void OnDestroy()
    {
        if (IsRunning)
            Abandon();
        else
            RestorePlayerAndCursor();
    }

    private void BuildPopup()
    {
        GameObject statusPlate = CasinoMinigamePresentation.CreateImage(
            "SHUFFLE Status Plate", gameArea, new Vector2(0f, 238f), new Vector2(1280f, 62f),
            new Color(0.025f, 0.12f, 0.09f, 0.96f));
        CasinoHudStyle.Panel(statusPlate.GetComponent<Image>(), new Color(0.10f, 0.30f, 0.31f));
        progressText = CasinoMinigamePresentation.CreateText(
            statusPlate.transform, "SHUFFLE Progress", "CARDS  0/8",
            new Vector2(-310f, 0f), new Vector2(520f, 50f), 23, Color.white);
        timerText = CasinoMinigamePresentation.CreateText(
            statusPlate.transform, "SHUFFLE Timer", "TIME  20.0",
            new Vector2(390f, 0f), new Vector2(250f, 50f), 22, new Color(1f, 0.86f, 0.35f));

        (leftStackRect, leftStackButton, leftStackOutline, leftCountText) =
            CreateStack(0, new Vector2(-355f, 0f), "LEFT STACK");
        (rightStackRect, rightStackButton, rightStackOutline, rightCountText) =
            CreateStack(1, new Vector2(355f, 0f), "RIGHT STACK");

        GameObject deck = CasinoMinigamePresentation.CreateImage(
            "Interleaved Shuffle Deck", gameArea, Vector2.zero, new Vector2(250f, 300f), Felt);
        CasinoHudStyle.Panel(deck.GetComponent<Image>(), new Color(0.90f, 0.72f, 0.28f));
        Outline deckOutline = deck.AddComponent<Outline>();
        deckOutline.effectColor = new Color(1f, 0.84f, 0.30f);
        deckOutline.effectDistance = new Vector2(3f, -3f);
        CasinoMinigamePresentation.CreateText(deck.transform, "Interleaved Deck Label", "SHUFFLED DECK",
            new Vector2(0f, 112f), new Vector2(230f, 38f), 18, Color.white);
        CasinoMinigamePresentation.CreateText(deck.transform, "Interleaved Deck Hint", "INTERLEAVE HERE",
            new Vector2(0f, -120f), new Vector2(230f, 32f), 14, new Color(1f, 0.86f, 0.45f));

        promptText = CasinoMinigamePresentation.CreateText(
            gameArea, "SHUFFLE Stack Prompt", "", new Vector2(0f, 174f), new Vector2(620f, 42f), 23,
            new Color(1f, 0.88f, 0.43f));
        feedbackText = CasinoMinigamePresentation.CreateText(
            gameArea, "SHUFFLE Feedback", "", new Vector2(0f, -235f), new Vector2(950f, 42f), 19,
            new Color(1f, 0.88f, 0.43f));
    }

    private (RectTransform rect, Button button, Outline outline, Text countText) CreateStack(
        int stackIndex, Vector2 position, string label)
    {
        GameObject root = CasinoMinigamePresentation.CreateImage(
            label, gameArea, position, new Vector2(310f, 300f), Felt, true);
        CasinoHudStyle.Panel(root.GetComponent<Image>(), new Color(0.12f, 0.36f, 0.30f));
        Button button = root.AddComponent<Button>();
        button.targetGraphic = root.GetComponent<Image>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => SelectStack(stackIndex));
        Outline outline = root.AddComponent<Outline>();
        outline.effectColor = new Color(0.25f, 0.55f, 0.43f);
        outline.effectDistance = new Vector2(3f, -3f);

        for (int i = 0; i < 5; i++)
        {
            GameObject card = CasinoMinigamePresentation.CreateImage(
                $"{label} Card {i + 1}", root.transform,
                new Vector2((i - 2) * 5f, (i - 2) * 7f), new Vector2(150f, 200f),
                i == 4 ? CardCream : new Color(0.13f, 0.26f, 0.39f));
            Image cardImage = card.GetComponent<Image>();
            CasinoPixelArt.SetSprite(cardImage, CasinoPixelArt.Card, cardImage.color);
            cardImage.preserveAspect = true;
            cardImage.raycastTarget = false;
            card.transform.SetAsFirstSibling();
        }

        Text stackLabel = CasinoMinigamePresentation.CreateText(
            root.transform, $"{label} Label", label, new Vector2(0f, -101f), new Vector2(280f, 34f), 18,
            Color.white);
        stackLabel.raycastTarget = false;
        Text count = CasinoMinigamePresentation.CreateText(
            root.transform, $"{label} Count", "4 CARDS", new Vector2(0f, -133f), new Vector2(260f, 28f), 14,
            new Color(1f, 0.87f, 0.47f));
        count.raycastTarget = false;
        return (root.GetComponent<RectTransform>(), button, outline, count);
    }

    public bool SelectStack(int stackIndex)
    {
        if (!IsRunning || waitingForFlight || stackIndex < 0 || stackIndex > 1)
            return false;

        if (!IsCorrectStack(ExpectedStack, stackIndex))
        {
            misses++;
            CasinoSoundscape.Play(CasinoSoundCue.Mistake, transform.position, transform);
            // A wrong stack ruins the shuffle: exit and start again while the task clock keeps running.
            Complete(false, completedCards * 100);
            return false;
        }

        completedCards++;
        if (stackIndex == 0) leftCardsRemaining--;
        else rightCardsRemaining--;
        waitingForFlight = true;
        flyingCard = CreateFlyingCard(stackIndex);
        flightStart = flyingCard.rectTransform.anchoredPosition;
        flightEnd = new Vector2((completedCards % 3 - 1) * 7f, (completedCards / 3) * 3f);
        flightElapsed = 0f;
        SetFeedback(stackIndex == 0 ? "Nice! Left side in." : "Nice! Right side in.");
        CasinoSoundscape.Play(CasinoSoundCue.Card, transform.position, transform);
        UpdateHighlights();
        RefreshHud();
        return true;
    }

    private Image CreateFlyingCard(int stackIndex)
    {
        GameObject card = CasinoMinigamePresentation.CreateImage(
            "Shuffling Card In Motion", gameArea,
            (stackIndex == 0 ? leftStackRect : rightStackRect).anchoredPosition +
            new Vector2(stackIndex == 0 ? 75f : -75f, 18f), new Vector2(76f, 108f), CardCream);
        Image image = card.GetComponent<Image>();
        CasinoPixelArt.SetSprite(image, CasinoPixelArt.Card, CardCream);
        image.preserveAspect = true;
        image.raycastTarget = false;
        Outline outline = card.AddComponent<Outline>();
        outline.effectColor = new Color(0.09f, 0.16f, 0.13f);
        outline.effectDistance = new Vector2(2f, -2f);
        card.transform.SetAsLastSibling();
        return image;
    }

    private void AnimateCardFlight(float deltaTime)
    {
        if (flyingCard == null)
        {
            waitingForFlight = false;
            return;
        }

        flightElapsed += deltaTime;
        float t = Mathf.Clamp01(flightElapsed / CardFlightSeconds);
        float liftEnd = 0.18f;
        float interleaveStart = 0.72f;
        float settleStart = 0.88f;
        Vector2 startLift = flightStart + new Vector2(0f, 58f);
        Vector2 targetLift = flightEnd + new Vector2(0f, 42f);
        Vector3 scale;
        float angle;

        if (t < liftEnd)
        {
            float phase = Mathf.SmoothStep(0f, 1f, t / liftEnd);
            flyingCard.rectTransform.anchoredPosition = Vector2.Lerp(flightStart, startLift, phase);
            scale = Vector3.one * Mathf.Lerp(1f, 1.12f, phase);
            angle = Mathf.Lerp(0f, -8f, phase);
        }
        else if (t < interleaveStart)
        {
            float phase = (t - liftEnd) / (interleaveStart - liftEnd);
            float eased = Mathf.SmoothStep(0f, 1f, phase);
            Vector2 position = Vector2.Lerp(startLift, targetLift, eased);
            position.y += Mathf.Sin(phase * Mathf.PI) * 38f;
            flyingCard.rectTransform.anchoredPosition = position;
            scale = Vector3.one * (1.12f + Mathf.Sin(phase * Mathf.PI) * 0.08f);
            angle = Mathf.Lerp(-8f, completedCards % 2 == 0 ? 11f : -11f, eased);
        }
        else if (t < settleStart)
        {
            float phase = (t - interleaveStart) / (settleStart - interleaveStart);
            flyingCard.rectTransform.anchoredPosition = Vector2.Lerp(targetLift, flightEnd, phase);
            float width = Mathf.Lerp(1f, 0.12f, Mathf.Sin(phase * Mathf.PI));
            scale = new Vector3(width, 1f, 1f);
            angle = Mathf.Lerp(completedCards % 2 == 0 ? 11f : -11f, 0f, phase);
        }
        else
        {
            float phase = (t - settleStart) / (1f - settleStart);
            flyingCard.rectTransform.anchoredPosition =
                flightEnd + new Vector2(0f, Mathf.Sin((1f - phase) * Mathf.PI) * 8f);
            scale = Vector3.one * (1f + Mathf.Sin((1f - phase) * Mathf.PI) * 0.06f);
            angle = Mathf.Lerp(0f, 0f, phase);
        }

        flyingCard.rectTransform.localScale = scale;
        flyingCard.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        if (t < 1f)
            return;

        flyingCard.rectTransform.anchoredPosition = flightEnd;
        flyingCard.rectTransform.localScale = Vector3.one;
        flyingCard.rectTransform.localRotation = Quaternion.identity;
        shuffledCards.Add(flyingCard.rectTransform);
        flyingCard = null;
        waitingForFlight = false;
        UpdateStackCounts();
        CasinoSoundscape.Play(CasinoSoundCue.Card, transform.position, transform);

        if (CanCompleteSequence(completedCards, stackSequence.Length, waitingForFlight))
        {
            SetFeedback("Perfect shuffle! The dealer is ready.");
            Complete(true, Mathf.Max(0, completedCards * 100 + Mathf.RoundToInt(timeRemaining * 10f) - misses * 25));
        }
    }

    private void UpdateHighlights()
    {
        int expected = ExpectedStack;
        SetStackHighlight(leftStackButton, leftStackOutline, expected == 0);
        SetStackHighlight(rightStackButton, rightStackOutline, expected == 1);
        if (promptText != null)
            promptText.text = expected == 0 ? "TAKE FROM THE LEFT STACK" : "TAKE FROM THE RIGHT STACK";
    }

    private static void SetStackHighlight(Button button, Outline outline, bool highlighted)
    {
        if (button == null || outline == null)
            return;

        button.transform.localScale = Vector3.one * (highlighted ? 1.04f : 1f);
        outline.effectColor = highlighted
            ? new Color(1f, 0.82f, 0.23f)
            : new Color(0.25f, 0.55f, 0.43f);
        outline.effectDistance = highlighted ? new Vector2(6f, -6f) : new Vector2(3f, -3f);
    }

    private void UpdateStackCounts()
    {
        if (leftCountText != null) leftCountText.text = $"{leftCardsRemaining} CARDS";
        if (rightCountText != null) rightCountText.text = $"{rightCardsRemaining} CARDS";
    }

    private void RefreshHud()
    {
        if (progressText != null)
            progressText.text = $"INTERLEAVED  {completedCards}/{stackSequence.Length}  •  MISSES  {misses}";
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
            return;
        feedbackText.text = value;
        feedbackText.color = value.StartsWith("Oops", StringComparison.Ordinal)
            ? new Color(1f, 0.50f, 0.39f)
            : new Color(1f, 0.88f, 0.43f);
    }

    private void DestroyFlyingCard()
    {
        if (flyingCard != null)
            Destroy(flyingCard.gameObject);
        flyingCard = null;
        waitingForFlight = false;
    }

    private void RestorePlayerAndCursor()
    {
        if (!controlsCaptured)
            return;
        controlsCaptured = false;
        if (player != null)
            player.SetMovementEnabled(true);
        Cursor.lockState = savedCursorLockMode;
        Cursor.visible = savedCursorVisible;
    }
}
