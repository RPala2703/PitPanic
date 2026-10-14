using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class RouletteTableMinigame : CasinoMinigame
{
    private enum RouletteStage
    {
        Spin,
        Spinning,
        Sweep,
        Pay
    }

    private static readonly int[] EuropeanWheelOrder =
    {
        0, 32, 15, 19, 4, 21, 2, 25, 17, 34, 6, 27, 13, 36, 11, 30, 8,
        23, 10, 5, 24, 16, 33, 1, 20, 14, 31, 9, 22, 18, 29, 7, 28, 12,
        35, 3, 26
    };

    private static readonly HashSet<int> RedPockets = new HashSet<int>
    {
        1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36
    };

    [SerializeField, Min(0.1f)] private float spinSeconds = 2.4f;
    [SerializeField] private CasinoPlayerController player;
    [SerializeField] private Transform tableRoot;
    [SerializeField] private Transform wheel;
    [SerializeField] private RouletteRoundController roundController;

    private sealed class SweepChip
    {
        public RectTransform Rect;
        public Vector2 Home;
        public bool Collected;
        public bool Protected;
        public RouletteCustomerBet CustomerBet;
    }

    private readonly List<SweepChip> sweepChips = new List<SweepChip>();
    private readonly List<int[]> payoutActions = new List<int[]>();
    private readonly int[] selectedPayoutCounts = new int[4];
    private static readonly int[] PayoutDenominationsPence = { 500, 250, 2500, 10000 };
    private static readonly string[] PayoutChipNames = { "RED", "BLUE", "GREEN", "BLACK" };
    private static readonly Color[] PayoutChipColors =
    {
        new Color(0.81f, 0.16f, 0.13f),
        new Color(0.16f, 0.43f, 0.78f),
        new Color(0.20f, 0.62f, 0.34f),
        new Color(0.08f, 0.09f, 0.11f)
    };
    private Transform gameArea;
    private Transform spinStageRoot;
    private Transform sweepStageRoot;
    private Transform payoutStageRoot;
    private GameObject statusHeader;
    private RectTransform wheelHitArea;
    private RectTransform wheelRing;
    private RectTransform ball;
    private RectTransform trayHitArea;
    private RectTransform payoutTray;
    private RectTransform payoutCustomerPanel;
    private readonly Dictionary<int, RectTransform> sweepNumberCells = new Dictionary<int, RectTransform>();
    private readonly Dictionary<RouletteBetType, RectTransform> sweepOutsideCells = new Dictionary<RouletteBetType, RectTransform>();
    private readonly Dictionary<int, RectTransform> payoutNumberCells = new Dictionary<int, RectTransform>();
    private readonly Dictionary<RouletteBetType, RectTransform> payoutOutsideCells = new Dictionary<RouletteBetType, RectTransform>();
    private readonly Dictionary<int, Outline> sweepPocketOutlines = new Dictionary<int, Outline>();
    private readonly Dictionary<int, Outline> payoutPocketOutlines = new Dictionary<int, Outline>();
    private Text statusText;
    private Text instructionText;
    private Text resultText;
    private Text sweepProgressText;
    private Text payoutCustomerText;
    private Text payoutAmountText;
    private Text selectedAmountText;
    private Text payoutFeedbackText;
    private Text payoutQuantityText;
    private Button payoutConfirmButton;
    private RouletteCustomerBet currentPayoutBet;
    private SweepChip heldChip;
    private RouletteStage stage;
    private Vector2 previousPointer;
    private float previousDragAngle;
    private float angularTravel;
    private float spinTimer;
    private float wheelStartAngle;
    private float wheelTargetAngle;
    private float ballAngle;
    private int winningNumber = -1;
    private int sweptCount;
    private int requiredSweepCount;
    private int payoutQuantity = 1;
    private int selectedPayoutPence;
    private long assistanceRoundId;
    private Coroutine paymentAnimation;
    private bool isDragging;
    private bool controlsCaptured;
    private CursorLockMode savedCursorLockMode;
    private bool savedCursorVisible;

    public bool IsConfigured => player != null && tableRoot != null && wheel != null && roundController != null;
    public override bool CanBegin => IsConfigured && Mouse.current != null &&
                                     roundController.Phase == RouletteRoundPhase.AwaitingAssistance &&
                                     roundController.AssistanceBets.Count > 0;
    protected override string PresentationTitle => "SPIN!  •  SWEEP!  •  PAY!";
    protected override string PresentationInstructions =>
        "Spin once, clear losing bets and pay the winners using the table float.";
    public int WinningNumber => winningNumber;
    public int SweptChipCount => sweptCount;
    public long AssistanceRoundId => assistanceRoundId;
    public int RequiredSweepCount => requiredSweepCount;
    public static int GetLosingBetCount(IReadOnlyList<RouletteCustomerBet> bets)
    {
        if (bets == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < bets.Count; i++)
        {
            if (bets[i] != null && !bets[i].IsWinning)
            {
                count++;
            }
        }

        return count;
    }

    public static int CountLosingBets(IReadOnlyList<RouletteBet> bets, int winningNumber)
    {
        if (bets == null || !EuropeanRouletteRules.IsValidNumber(winningNumber))
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < bets.Count; i++)
        {
            if (!EuropeanRouletteRules.Wins(bets[i], winningNumber))
            {
                count++;
            }
        }

        return count;
    }

    public static int CountWinningBets(IReadOnlyList<RouletteBet> bets, int winningNumber)
    {
        if (bets == null || !EuropeanRouletteRules.IsValidNumber(winningNumber))
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < bets.Count; i++)
        {
            if (EuropeanRouletteRules.Wins(bets[i], winningNumber))
            {
                count++;
            }
        }

        return count;
    }
    public static int CountUncollectedLosingBets(IReadOnlyList<RouletteCustomerBet> bets)
    {
        if (bets == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < bets.Count; i++)
        {
            if (bets[i] != null && !bets[i].IsWinning && !bets[i].IsSwept)
            {
                count++;
            }
        }

        return count;
    }
    public static bool CanCompleteSweep(int sweptChipCount, int requiredChipCount) =>
        requiredChipCount >= 0 && sweptChipCount >= requiredChipCount;
    public static bool CanCollectBet(RouletteBet bet, int winningNumber) =>
        EuropeanRouletteRules.IsValidNumber(winningNumber) &&
        !EuropeanRouletteRules.Wins(bet, winningNumber);
    public static bool CanConfirmPayout(int requiredPence, int selectedPence) =>
        requiredPence > 0 && selectedPence == requiredPence;

    public static int[] GetPayoutComposition(int amountPence)
    {
        if (amountPence <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(amountPence));
        }

        List<BlackjackChipDenomination> composition = BlackjackChipDenomination.GetExactComposition(
            amountPence, BlackjackChipDenomination.CreateDefaults());
        int[] counts = new int[PayoutDenominationsPence.Length];
        foreach (BlackjackChipDenomination denomination in composition)
        {
            for (int i = 0; i < PayoutDenominationsPence.Length; i++)
            {
                if (PayoutDenominationsPence[i] == denomination.ValuePence)
                {
                    counts[i]++;
                    break;
                }
            }
        }

        return counts;
    }

    public static bool TryGetWinningNumber(float normalizedSpin, out int number)
    {
        if (float.IsNaN(normalizedSpin) || float.IsInfinity(normalizedSpin) ||
            normalizedSpin < 0f || normalizedSpin >= 1f)
        {
            number = -1;
            return false;
        }

        number = Mathf.FloorToInt(normalizedSpin * 37f);
        return true;
    }

    public static bool IsRedPocket(int number) => RedPockets.Contains(number);
    public static bool IsValidPocket(int number) => number >= 0 && number <= 36;
    public static string GetPocketColorName(int number) =>
        number == 0 ? "GREEN" : IsRedPocket(number) ? "RED" : "BLACK";

    public void Configure(
        CasinoPlayerController casinoPlayer,
        Transform root,
        Transform wheelTransform,
        RouletteRoundController rounds = null)
    {
        player = casinoPlayer;
        tableRoot = root;
        wheel = wheelTransform;
        roundController = rounds != null ? rounds : root != null ? root.GetComponent<RouletteRoundController>() : null;
    }

    public void ConfigureRoundController(RouletteRoundController rounds)
    {
        roundController = rounds;
    }

    private void OnDisable()
    {
        if (IsRunning)
        {
            Abandon();
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
            RestorePlayer();
        }
    }

    protected override void OnBegin()
    {
        if (!IsConfigured || Mouse.current == null || Presentation == null ||
            Presentation.GameArea == null || !CanBegin)
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

        stage = RouletteStage.Spin;
        assistanceRoundId = roundController.AssistanceRoundId;
        winningNumber = roundController.AssistanceWinningNumber;
        sweptCount = 0;
        requiredSweepCount = 0;
        payoutQuantity = 1;
        selectedPayoutPence = 0;
        currentPayoutBet = null;
        angularTravel = 0f;
        spinTimer = 0f;
        wheelStartAngle = 0f;
        wheelTargetAngle = 0f;
        ballAngle = 90f;
        isDragging = false;
        heldChip = null;
        sweepChips.Clear();
        payoutActions.Clear();
        System.Array.Clear(selectedPayoutCounts, 0, selectedPayoutCounts.Length);
        sweepNumberCells.Clear();
        sweepOutsideCells.Clear();
        payoutNumberCells.Clear();
        payoutOutsideCells.Clear();
        sweepPocketOutlines.Clear();
        payoutPocketOutlines.Clear();
        spinStageRoot = null;
        sweepStageRoot = null;
        payoutStageRoot = null;
        gameArea = Presentation.GameArea;
        BuildPopup();
        if (winningNumber >= 0)
        {
            EnterSweepStage();
        }
        else
        {
            SetStatus("SPIN THE WHEEL");
            SetInstruction("Click and drag in a circle across the wheel. Release after a full turn.");
            UpdateBallPosition();
        }
    }

    protected override void OnTick(float deltaTime)
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Abandon();
            return;
        }

        if (stage == RouletteStage.Spinning)
        {
            UpdateSpinAnimation(deltaTime);
            return;
        }

        if (stage == RouletteStage.Pay)
        {
            return;
        }

        if (Mouse.current != null)
        {
            Vector2 pointer = Mouse.current.position.ReadValue();
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                BeginPointerAction(pointer);
            }

            if (isDragging && Mouse.current.leftButton.isPressed)
            {
                UpdatePointerAction(pointer);
            }

            if (isDragging && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                EndPointerAction(pointer);
            }
        }
    }

    protected override void OnAbandon()
    {
        StopPaymentAnimation();
        RestorePlayer();
        ResetTemporaryInput();
        gameArea = null;
    }

    protected override void OnComplete(bool success)
    {
        StopPaymentAnimation();
        RestorePlayer();
        ResetTemporaryInput();
        gameArea = null;
    }

    private void BuildPopup()
    {
        statusHeader = CasinoMinigamePresentation.CreateImage(
            "Roulette Popup Status", gameArea, new Vector2(0f, 252f), new Vector2(1310f, 62f),
            new Color(0.025f, 0.12f, 0.09f, 0.96f));
        statusText = CasinoMinigamePresentation.CreateText(
            statusHeader.transform, "Roulette Status", "SPIN THE WHEEL",
            new Vector2(0f, 0f), new Vector2(1250f, 54f), 25,
            new Color(1f, 0.87f, 0.44f));
        instructionText = CasinoMinigamePresentation.CreateText(
            gameArea, "Roulette Instructions", "",
            new Vector2(0f, -274f), new Vector2(1300f, 42f), 18, Color.white);
        BuildWheel();
        sweepStageRoot = CreateFullScreenStage("Roulette Sweep Stage");
        sweepStageRoot.gameObject.SetActive(false);
        BuildBettingLayout(sweepStageRoot, new Vector2(300f, -5f), 460f, 430f,
            sweepNumberCells, sweepOutsideCells, sweepPocketOutlines, false);
        BuildSweepTray();
        BuildPayoutStage();
        resultText = CasinoMinigamePresentation.CreateText(
            sweepStageRoot, "Roulette Result", "",
            new Vector2(660f, 190f), new Vector2(180f, 44f), 23,
            new Color(1f, 0.86f, 0.36f));
    }

    private void BuildWheel()
    {
        GameObject spinRoot = new GameObject("Roulette Spin Stage", typeof(RectTransform));
        spinRoot.transform.SetParent(gameArea, false);
        RectTransform spinRect = spinRoot.GetComponent<RectTransform>();
        spinRect.anchorMin = Vector2.zero;
        spinRect.anchorMax = Vector2.one;
        spinRect.offsetMin = Vector2.zero;
        spinRect.offsetMax = Vector2.zero;
        spinStageRoot = spinRoot.transform;

        GameObject wheelObject = CasinoMinigamePresentation.CreateImage(
            "Interactive Roulette Wheel", spinStageRoot, new Vector2(-350f, 0f),
            new Vector2(470f, 470f), new Color(0.11f, 0.075f, 0.035f));
        wheelHitArea = wheelObject.GetComponent<RectTransform>();
        Outline outerOutline = wheelObject.AddComponent<Outline>();
        outerOutline.effectColor = new Color(0.82f, 0.61f, 0.22f);
        outerOutline.effectDistance = new Vector2(6f, -6f);

        GameObject ring = CasinoMinigamePresentation.CreateImage(
            "Numbered European Pockets", wheelObject.transform, Vector2.zero,
            new Vector2(400f, 400f), new Color(0.07f, 0.10f, 0.08f));
        wheelRing = ring.GetComponent<RectTransform>();
        for (int i = 0; i < EuropeanWheelOrder.Length; i++)
        {
            int number = EuropeanWheelOrder[i];
            float angle = 90f - i * (360f / EuropeanWheelOrder.Length);
            float radians = angle * Mathf.Deg2Rad;
            Vector2 position = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * 166f;
            Color color = number == 0
                ? new Color(0.11f, 0.52f, 0.28f)
                : IsRedPocket(number)
                    ? new Color(0.70f, 0.13f, 0.12f)
                    : new Color(0.055f, 0.065f, 0.07f);
            GameObject pocket = CasinoMinigamePresentation.CreateImage(
                $"Pocket {number}", wheelRing, position, new Vector2(36f, 29f), color);
            Outline pocketOutline = pocket.AddComponent<Outline>();
            pocketOutline.effectColor = new Color(0.91f, 0.77f, 0.37f, 0.9f);
            pocketOutline.effectDistance = new Vector2(1f, -1f);
            CasinoMinigamePresentation.CreateText(pocket.transform, "Pocket Number",
                number.ToString(), Vector2.zero, new Vector2(34f, 27f), 13, Color.white);
        }

        GameObject center = CasinoMinigamePresentation.CreateImage(
            "Roulette Wheel Center", wheelObject.transform, Vector2.zero,
            new Vector2(142f, 142f), new Color(0.12f, 0.37f, 0.24f));
        Outline centerOutline = center.AddComponent<Outline>();
        centerOutline.effectColor = new Color(0.82f, 0.61f, 0.22f);
        centerOutline.effectDistance = new Vector2(4f, -4f);
        CasinoMinigamePresentation.CreateText(center.transform, "Wheel Center Label",
            "PIT\nPANIC", Vector2.zero, new Vector2(124f, 80f), 21,
            new Color(1f, 0.89f, 0.56f));

        GameObject ballObject = CasinoMinigamePresentation.CreateImage(
            "Roulette Ball", spinStageRoot, new Vector2(-350f, 218f),
            new Vector2(22f, 22f), Color.white);
        ball = ballObject.GetComponent<RectTransform>();
        ballObject.AddComponent<Outline>().effectColor = Color.black;
        CasinoMinigamePresentation.CreateText(spinStageRoot, "Wheel Drag Hint",
            "DRAG TO SPIN", new Vector2(-350f, -245f), new Vector2(360f, 36f), 17,
            new Color(1f, 0.86f, 0.36f));
    }

    private void BuildBettingLayout(
        Transform root,
        Vector2 center,
        float width,
        float height,
        Dictionary<int, RectTransform> numberCells,
        Dictionary<RouletteBetType, RectTransform> outsideCells,
        Dictionary<int, Outline> outlines,
        bool compact)
    {
        GameObject panel = CasinoMinigamePresentation.CreateImage(
            compact ? "Payout Betting Layout" : "Roulette Betting Layout",
            root, center, new Vector2(width, height), new Color(0.025f, 0.29f, 0.18f));
        Outline panelOutline = panel.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.64f, 0.53f, 0.27f);
        panelOutline.effectDistance = new Vector2(3f, -3f);
        CasinoMinigamePresentation.CreateText(panel.transform, "Betting Layout Label",
            "EUROPEAN ROULETTE  •  3 × 12", new Vector2(0f, height * 0.5f - 22f),
            new Vector2(width - 20f, 30f), 17,
            new Color(1f, 0.85f, 0.42f));

        float zeroX = compact ? -158f : -190f;
        float firstCellX = compact ? -104f : -120f;
        float firstCellY = compact ? 122f : 130f;
        float columnSpacing = compact ? 39f : 49f;
        float rowSpacing = compact ? 22f : 24f;
        Vector2 zeroPosition = new Vector2(zeroX, -8f);
        for (int number = 0; number <= 36; number++)
        {
            Vector2 position = number == 0
                ? zeroPosition
                : RouletteBettingLayout.GetNumberCellPosition(
                    number, new Vector2(firstCellX, firstCellY), columnSpacing, rowSpacing);
            Vector2 size = number == 0
                ? new Vector2(compact ? 38f : 46f, compact ? 292f : 320f)
                : new Vector2(compact ? 35f : 43f, compact ? 19f : 22f);
            Color color = number == 0
                ? new Color(0.11f, 0.52f, 0.28f)
                : IsRedPocket(number)
                    ? new Color(0.70f, 0.13f, 0.12f)
                    : new Color(0.055f, 0.065f, 0.07f);
            GameObject pocket = CasinoMinigamePresentation.CreateImage(
                $"Betting Pocket {number}", panel.transform, position, size, color);
            Outline pocketOutline = pocket.AddComponent<Outline>();
            pocketOutline.effectColor = new Color(0.91f, 0.77f, 0.37f, 0.7f);
            pocketOutline.effectDistance = new Vector2(1f, -1f);
            outlines[number] = pocketOutline;
            if (number == 0)
            {
                numberCells[0] = pocket.GetComponent<RectTransform>();
            }
            else
            {
                numberCells[number] = pocket.GetComponent<RectTransform>();
            }

            CasinoMinigamePresentation.CreateText(pocket.transform, "Betting Number",
                number.ToString(), Vector2.zero, size, compact ? 11 : 13, Color.white);
        }
        Vector2 outsideStart = new Vector2(compact ? -95f : -112f, -height * 0.5f + 30f);
        float outsideSpacing = compact ? 62f : 73f;
        RouletteBetType[] outsideTypes =
        {
            RouletteBetType.Red,
            RouletteBetType.Black,
            RouletteBetType.Odd,
            RouletteBetType.Even
        };
        string[] outsideLabels = { "RED", "BLACK", "ODD", "EVEN" };
        for (int i = 0; i < outsideTypes.Length; i++)
        {
            Vector2 position = RouletteBettingLayout.GetOutsideBetPosition(
                outsideTypes[i], outsideStart, outsideSpacing);
            GameObject outside = CasinoMinigamePresentation.CreateImage(
                $"Outside Bet {outsideLabels[i]}", panel.transform, position,
                new Vector2(compact ? 55f : 66f, compact ? 30f : 34f),
                GetBetAreaColor(outsideTypes[i]));
            Outline outsideOutline = outside.AddComponent<Outline>();
            outsideOutline.effectColor = new Color(1f, 0.84f, 0.36f);
            outsideOutline.effectDistance = new Vector2(1f, -1f);
            outsideCells[outsideTypes[i]] = outside.GetComponent<RectTransform>();
            CasinoMinigamePresentation.CreateText(outside.transform, "Outside Bet Label",
                outsideLabels[i], Vector2.zero, new Vector2(compact ? 54f : 64f, 30f),
                compact ? 9 : 11, Color.white);
        }
    }

    private void BuildSweepTray()
    {
        GameObject tray = CasinoMinigamePresentation.CreateImage(
            "Roulette Dealer Collection Tray", sweepStageRoot, new Vector2(660f, 30f),
            new Vector2(190f, 240f), new Color(0.16f, 0.35f, 0.22f));
        trayHitArea = tray.GetComponent<RectTransform>();
        Outline trayOutline = tray.AddComponent<Outline>();
        trayOutline.effectColor = new Color(1f, 0.83f, 0.23f);
        trayOutline.effectDistance = new Vector2(5f, -5f);
        CasinoMinigamePresentation.CreateText(tray.transform, "Dealer Tray Label",
            "DEALER\nCOLLECTION TRAY", new Vector2(0f, 85f), new Vector2(240f, 68f), 19,
            new Color(1f, 0.88f, 0.44f));
        sweepProgressText = CasinoMinigamePresentation.CreateText(
            tray.transform, "Sweep Progress", "0 / 0 BETS",
            new Vector2(0f, -70f), new Vector2(180f, 42f), 17, Color.white);
    }

    private void BuildPayoutStage()
    {
        GameObject stageRoot = new GameObject("Roulette Pay Stage", typeof(RectTransform));
        stageRoot.transform.SetParent(gameArea, false);
        RectTransform stageRect = stageRoot.GetComponent<RectTransform>();
        stageRect.anchorMin = Vector2.zero;
        stageRect.anchorMax = Vector2.one;
        stageRect.offsetMin = Vector2.zero;
        stageRect.offsetMax = Vector2.zero;
        payoutStageRoot = stageRoot.transform;
        stageRoot.SetActive(false);
        BuildBettingLayout(payoutStageRoot, new Vector2(-505f, 0f), 460f, 430f,
            payoutNumberCells, payoutOutsideCells, payoutPocketOutlines, true);

        GameObject panel = CasinoMinigamePresentation.CreateImage(
            "Roulette Payout Panel", payoutStageRoot, new Vector2(260f, 0f),
            new Vector2(1010f, 550f), new Color(0.025f, 0.15f, 0.11f, 0.99f));
        Outline panelOutline = panel.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.83f, 0.66f, 0.23f);
        panelOutline.effectDistance = new Vector2(3f, -3f);
        CasinoMinigamePresentation.CreateText(panel.transform, "Payout Heading",
            "PAY!  •  PAY THE WINNERS", new Vector2(0f, 244f),
            new Vector2(900f, 42f), 25, new Color(1f, 0.87f, 0.44f));
        payoutCustomerText = CasinoMinigamePresentation.CreateText(
            panel.transform, "Winning Customer", "", new Vector2(0f, 184f),
            new Vector2(900f, 36f), 21, Color.white);
        payoutCustomerPanel = payoutCustomerText.rectTransform;
        payoutAmountText = CasinoMinigamePresentation.CreateText(
            panel.transform, "Required Payout", "", new Vector2(0f, 144f),
            new Vector2(900f, 36f), 21, new Color(0.67f, 1f, 0.73f));

        float[] chipX = { -345f, -115f, 115f, 345f };
        for (int i = 0; i < PayoutDenominationsPence.Length; i++)
        {
            int denominationIndex = i;
            CreatePayoutChipButton(panel.transform, denominationIndex, new Vector2(chipX[i], 54f));
        }

        CasinoMinigamePresentation.CreateText(panel.transform, "Quantity Label",
            "ADD CHIP STACK", new Vector2(-255f, -65f), new Vector2(190f, 28f),
            15, new Color(1f, 0.87f, 0.44f));
        CreatePayButton(panel.transform, "Quantity One", "×1", new Vector2(-310f, -105f),
            new Vector2(70f, 42f), () => SetPayoutQuantity(1));
        CreatePayButton(panel.transform, "Quantity Five", "×5", new Vector2(-230f, -105f),
            new Vector2(70f, 42f), () => SetPayoutQuantity(5));
        CreatePayButton(panel.transform, "Quantity Ten", "×10", new Vector2(-150f, -105f),
            new Vector2(70f, 42f), () => SetPayoutQuantity(10));
        payoutQuantityText = CasinoMinigamePresentation.CreateText(
            panel.transform, "Selected Quantity", "ADDING ×1", new Vector2(120f, -105f),
            new Vector2(220f, 36f), 16, Color.white);

        payoutTray = CasinoMinigamePresentation.CreateImage(
            "Payout Chip Tray", panel.transform, new Vector2(190f, -185f),
            new Vector2(600f, 100f), new Color(0.07f, 0.3f, 0.19f, 0.98f))
            .GetComponent<RectTransform>();
        CasinoMinigamePresentation.CreateText(panel.transform, "Payout Tray Label",
            "PAYOUT TRAY", new Vector2(-170f, -160f), new Vector2(170f, 30f),
            16, new Color(0.68f, 1f, 0.72f));
        selectedAmountText = CasinoMinigamePresentation.CreateText(
            panel.transform, "Selected Payout Total", "SELECTED £0.00",
            new Vector2(215f, -155f), new Vector2(540f, 34f), 21, Color.white);
        payoutFeedbackText = CasinoMinigamePresentation.CreateText(
            panel.transform, "Payout Feedback", "", new Vector2(0f, -220f),
            new Vector2(760f, 32f), 16, new Color(1f, 0.83f, 0.4f));
        CreatePayButton(panel.transform, "Undo Payout Chip", "UNDO STACK",
            new Vector2(-310f, -225f), new Vector2(170f, 46f), UndoPayoutAction);
        payoutConfirmButton = CreatePayButton(panel.transform, "Confirm Payout",
            "CONFIRM PAYOUT", new Vector2(310f, -225f), new Vector2(260f, 46f),
            ConfirmPayout);
    }

    private void CreatePayoutChipButton(Transform parent, int denominationIndex, Vector2 position)
    {
        GameObject buttonObject = CasinoMinigamePresentation.CreateImage(
            $"{PayoutChipNames[denominationIndex]} Payout Chip Button",
            parent, position, new Vector2(180f, 118f), new Color(0.035f, 0.13f, 0.11f));
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();
        button.targetGraphic.raycastTarget = true;
        button.onClick.AddListener(() => AddPayoutChips(denominationIndex));
        GameObject chip = CasinoMinigamePresentation.CreateImage(
            $"{PayoutChipNames[denominationIndex]} Payout Chip",
            buttonObject.transform, new Vector2(0f, 12f), new Vector2(72f, 72f),
            PayoutChipColors[denominationIndex]);
        chip.GetComponent<Image>().raycastTarget = false;
        chip.AddComponent<Outline>().effectColor = new Color(1f, 0.9f, 0.55f);
        CasinoMinigamePresentation.CreateText(chip.transform, "Payout Chip Value",
            RouletteRoundController.FormatPounds(PayoutDenominationsPence[denominationIndex]),
            Vector2.zero, new Vector2(70f, 32f), 14, Color.white);
        CasinoMinigamePresentation.CreateText(buttonObject.transform, "Payout Chip Label",
            PayoutChipNames[denominationIndex], new Vector2(0f, -48f),
            new Vector2(172f, 26f), 13, Color.white);
    }

    private Button CreatePayButton(
        Transform parent,
        string objectName,
        string label,
        Vector2 position,
        Vector2 size,
        UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = CasinoMinigamePresentation.CreateImage(
            objectName, parent, position, size, new Color(0.19f, 0.39f, 0.27f));
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();
        button.targetGraphic.raycastTarget = true;
        button.onClick.AddListener(action);
        CasinoMinigamePresentation.CreateText(buttonObject.transform, "Button Label",
            label, Vector2.zero, size - new Vector2(8f, 6f), 15, Color.white);
        return button;
    }

    private void SetPayoutQuantity(int quantity)
    {
        payoutQuantity = Mathf.Max(1, quantity);
        if (payoutQuantityText != null)
        {
            payoutQuantityText.text = $"ADDING ×{payoutQuantity}";
        }
    }

    private void AddPayoutChips(int denominationIndex)
    {
        if (stage != RouletteStage.Pay || currentPayoutBet == null ||
            denominationIndex < 0 || denominationIndex >= PayoutDenominationsPence.Length)
        {
            return;
        }

        selectedPayoutCounts[denominationIndex] += payoutQuantity;
        selectedPayoutPence += PayoutDenominationsPence[denominationIndex] * payoutQuantity;
        payoutActions.Add(new[] { denominationIndex, payoutQuantity });
        RenderPayoutSelection();
        if (payoutFeedbackText != null)
        {
            payoutFeedbackText.text = selectedPayoutPence > currentPayoutBet.GrossReturnPence
                ? "TOO MUCH — UNDO A CHIP STACK"
                : $"{RouletteRoundController.FormatPounds(currentPayoutBet.GrossReturnPence - selectedPayoutPence)} LEFT";
        }
    }

    private void UndoPayoutAction()
    {
        if (payoutActions.Count == 0)
        {
            return;
        }

        int lastIndex = payoutActions.Count - 1;
        int[] action = payoutActions[lastIndex];
        payoutActions.RemoveAt(lastIndex);
        selectedPayoutCounts[action[0]] -= action[1];
        selectedPayoutPence -= PayoutDenominationsPence[action[0]] * action[1];
        RenderPayoutSelection();
        if (payoutFeedbackText != null)
        {
            payoutFeedbackText.text = "LAST CHIP STACK REMOVED";
        }
    }

    private void RenderPayoutSelection()
    {
        if (payoutTray == null)
        {
            return;
        }

        for (int i = payoutTray.childCount - 1; i >= 0; i--)
        {
            Destroy(payoutTray.GetChild(i).gameObject);
        }

        int chipTypeCount = 0;
        for (int i = 0; i < selectedPayoutCounts.Length; i++)
        {
            if (selectedPayoutCounts[i] <= 0)
            {
                continue;
            }

            float x = (chipTypeCount - 1.5f) * 128f;
            GameObject stack = CasinoMinigamePresentation.CreateImage(
                $"{PayoutChipNames[i]} Payout Stack", payoutTray, new Vector2(x, 4f),
                new Vector2(110f, 70f), PayoutChipColors[i]);
            stack.GetComponent<Image>().raycastTarget = false;
            stack.AddComponent<Outline>().effectColor = new Color(1f, 0.89f, 0.48f);
            CasinoMinigamePresentation.CreateText(stack.transform, "Payout Stack Amount",
                $"{RouletteRoundController.FormatPounds(PayoutDenominationsPence[i])}\n×{selectedPayoutCounts[i]}",
                Vector2.zero, new Vector2(105f, 62f), 14, Color.white);
            chipTypeCount++;
        }

        if (selectedAmountText != null)
        {
            string required = currentPayoutBet != null
                ? RouletteRoundController.FormatPounds(currentPayoutBet.GrossReturnPence)
                : RouletteRoundController.FormatPounds(0);
            selectedAmountText.text =
                $"SELECTED {RouletteRoundController.FormatPounds(selectedPayoutPence)}  /  {required}";
        }

        if (payoutConfirmButton != null)
        {
            payoutConfirmButton.interactable = currentPayoutBet != null &&
                CanConfirmPayout((int)currentPayoutBet.GrossReturnPence, selectedPayoutPence);
        }
    }

    private void ConfirmPayout()
    {
        if (stage != RouletteStage.Pay || currentPayoutBet == null)
        {
            return;
        }

        if (!CanConfirmPayout((int)currentPayoutBet.GrossReturnPence, selectedPayoutPence))
        {
            if (payoutFeedbackText != null)
            {
                payoutFeedbackText.text = "PAYOUT MUST MATCH THE EXACT GROSS RETURN";
            }

            return;
        }

        if (!roundController.TryRecordAssistancePayout(
                assistanceRoundId,
                currentPayoutBet.BetId,
                selectedPayoutPence,
                (int[])selectedPayoutCounts.Clone()))
        {
            roundController.FloatDestination.RequestAttention();
            if (payoutFeedbackText != null)
            {
                payoutFeedbackText.text = "TABLE FLOAT SHORT — ESCAPE, REFILL, THEN RETRY PAY!";
            }

            SetInstruction("The payout remains pending. Refill this table's float before retrying.");
            return;
        }

        payoutConfirmButton.interactable = false;
        string customerName = currentPayoutBet.Customer != null ? currentPayoutBet.Customer.name : "CUSTOMER";
        SetStatus($"PAID  •  {customerName}  •  {RouletteRoundController.FormatPounds(selectedPayoutPence)}");
        payoutFeedbackText.text = "PAYOUT CONFIRMED  •  CHIPS ON THEIR WAY";
        paymentAnimation = StartCoroutine(AnimatePayment(currentPayoutBet));
    }

    private System.Collections.IEnumerator AnimatePayment(RouletteCustomerBet paidBet)
    {
        Vector2 start = GetGameAreaPosition(payoutTray);
        Vector2 finish = GetGameAreaPosition(payoutCustomerPanel);
        List<RectTransform> flyingChips = new List<RectTransform>();
        for (int i = 0; i < selectedPayoutCounts.Length; i++)
        {
            if (selectedPayoutCounts[i] <= 0)
            {
                continue;
            }

            GameObject chip = CasinoMinigamePresentation.CreateImage(
                $"Paid {PayoutChipNames[i]} Chip Stack", payoutStageRoot,
                start + new Vector2((i - 1.5f) * 30f, 0f), new Vector2(56f, 40f),
                PayoutChipColors[i]);
            chip.GetComponent<Image>().raycastTarget = false;
            chip.AddComponent<Outline>().effectColor = new Color(1f, 0.91f, 0.53f);
            CasinoMinigamePresentation.CreateText(chip.transform, "Paid Chip Count",
                $"×{selectedPayoutCounts[i]}", Vector2.zero, new Vector2(52f, 34f), 12, Color.white);
            flyingChips.Add(chip.GetComponent<RectTransform>());
        }

        float elapsed = 0f;
        while (elapsed < 0.45f && IsRunning)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 0.45f);
            for (int i = 0; i < flyingChips.Count; i++)
            {
                if (flyingChips[i] != null)
                {
                    flyingChips[i].anchoredPosition = Vector2.Lerp(start, finish, t);
                }
            }

            yield return null;
        }

        for (int i = 0; i < flyingChips.Count; i++)
        {
            if (flyingChips[i] != null)
            {
                Destroy(flyingChips[i].gameObject);
            }
        }

        paymentAnimation = null;
        if (IsRunning && paidBet.IsPaid)
        {
            StartNextPayout();
        }
    }

    private Vector2 GetGameAreaPosition(RectTransform target)
    {
        Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(null, target.position);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            gameArea as RectTransform, screenPosition, null, out Vector2 local)
            ? local
            : Vector2.zero;
    }

    private void StartNextPayout()
    {
        currentPayoutBet = null;
        foreach (RouletteCustomerBet bet in roundController.AssistanceBets)
        {
            if (bet.IsWinning && !bet.IsPaid)
            {
                currentPayoutBet = bet;
                break;
            }
        }

        if (currentPayoutBet == null)
        {
            SetStatus("ALL WINNERS PAID");
            SetInstruction("Roulette assistance complete.");
            Complete(true, 200 + sweptCount * 25);
            return;
        }

        System.Array.Clear(selectedPayoutCounts, 0, selectedPayoutCounts.Length);
        payoutActions.Clear();
        selectedPayoutPence = 0;
        payoutCustomerText.text = $"WINNER  •  {currentPayoutBet.Customer.name}";
        payoutAmountText.text =
            $"{RouletteRoundController.GetBetLabel(currentPayoutBet.Bet)}  •  NUMBER {winningNumber}  •  PAY {RouletteRoundController.FormatPounds(currentPayoutBet.GrossReturnPence)}";
        payoutFeedbackText.text = "Build the exact total. Chip-stack buttons add up to ten chips.";
        if (payoutQuantityText != null)
        {
            payoutQuantityText.text = $"ADDING ×{payoutQuantity}";
        }

        SetStatus($"PAY!  •  {currentPayoutBet.Customer.name}");
        SetInstruction("Select chip denominations, choose a stack size, then confirm the exact payout.");
        RenderPayoutSelection();
    }

    private Transform CreateFullScreenStage(string objectName)
    {
        GameObject root = new GameObject(objectName, typeof(RectTransform));
        root.transform.SetParent(gameArea, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return root.transform;
    }

    private void BeginPointerAction(Vector2 screenPosition)
    {
        if (stage == RouletteStage.Spin)
        {
            if (!TryGetWheelLocalPointer(screenPosition, out Vector2 local) ||
                local.sqrMagnitude > 226f * 226f)
            {
                return;
            }

            isDragging = true;
            angularTravel = 0f;
            previousPointer = screenPosition;
            previousDragAngle = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
            SetInstruction("Keep dragging around the wheel, then release to spin.");
            return;
        }

        if (stage != RouletteStage.Sweep || heldChip != null)
        {
            return;
        }

        if (stage != RouletteStage.Sweep || heldChip != null)
        {
            return;
        }

        for (int i = 0; i < sweepChips.Count; i++)
        {
            SweepChip chip = sweepChips[i];
            if (chip.Collected || chip.Rect == null ||
                !CasinoMinigamePresentation.ContainsScreenPoint(chip.Rect, screenPosition))
            {
                continue;
            }

            heldChip = chip;
            isDragging = true;
            previousPointer = ScreenToGameArea(screenPosition);
            SetInstruction(chip.Protected
                ? "Winning bets stay on the felt. Only losing bets can be swept."
                : "Drag this losing bet into the collection tray.");
            return;
        }
    }

    private void UpdatePointerAction(Vector2 screenPosition)
    {
        if (stage == RouletteStage.Spin)
        {
            if (!TryGetWheelLocalPointer(screenPosition, out Vector2 local) ||
                local.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            float currentAngle = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
            float delta = Mathf.DeltaAngle(previousDragAngle, currentAngle);
            angularTravel += Mathf.Abs(delta);
            wheelRing.localRotation = Quaternion.Euler(0f, 0f, wheelRing.localEulerAngles.z + delta);
            previousDragAngle = currentAngle;
            previousPointer = screenPosition;
            return;
        }

        if (stage == RouletteStage.Sweep && heldChip != null && !heldChip.Protected)
        {
            heldChip.Rect.anchoredPosition = ScreenToGameArea(screenPosition);
        }
    }

    private void EndPointerAction(Vector2 screenPosition)
    {
        isDragging = false;
        if (stage == RouletteStage.Spin)
        {
            if (angularTravel < 100f)
            {
                SetInstruction("Spin not long enough. Drag around the wheel, then release.");
                return;
            }

            BeginSpin();
            return;
        }

        if (stage == RouletteStage.Sweep && heldChip != null)
        {
            if (heldChip.Protected)
            {
                heldChip.Rect.anchoredPosition = heldChip.Home;
                if (CasinoMinigamePresentation.ContainsScreenPoint(trayHitArea, screenPosition))
                {
                    SetStatus("THAT'S A WINNING BET!");
                    SetInstruction("Winning chips cannot be collected. They stay on the layout for PAY!.");
                    heldChip.Rect.localScale = Vector3.one * 1.2f;
                    StartCoroutine(ResetProtectedChipScale(heldChip.Rect));
                }
            }
            else if (CasinoMinigamePresentation.ContainsScreenPoint(trayHitArea, screenPosition))
            {
                if (!roundController.TryMarkAssistanceBetSwept(assistanceRoundId, heldChip.CustomerBet.BetId))
                {
                    SetStatus("BET NOT AVAILABLE");
                    heldChip.Rect.anchoredPosition = heldChip.Home;
                    heldChip = null;
                    return;
                }

                heldChip.Collected = true;
                heldChip.Rect.anchoredPosition =
                    trayHitArea.anchoredPosition + new Vector2(-40f + (sweptCount % 2) * 58f, -15f - (sweptCount / 2) * 40f);
                sweptCount++;
                if (sweepProgressText != null)
                {
                    sweepProgressText.text = $"{sweptCount} / {requiredSweepCount} LOSING BETS";
                }

                if (CanCompleteSweep(sweptCount, requiredSweepCount))
                {
                    AdvanceAfterSweep();
                }
                else
                {
                    SetStatus($"SWEEP!  •  {sweptCount}/{requiredSweepCount} LOSING BETS");
                    SetInstruction("Drag the next losing bet into the dealer collection tray.");
                }
            }
            else
            {
                heldChip.Rect.anchoredPosition = heldChip.Home;
                SetInstruction("Missed the tray. Pick up that chip and try again.");
            }

            heldChip = null;
        }
    }

    private void BeginSpin()
    {
        if (winningNumber < 0)
        {
            winningNumber = Random.Range(0, 37);
            if (!roundController.TrySetAssistanceWinningNumber(assistanceRoundId, winningNumber))
            {
                winningNumber = -1;
                SetStatus("ROULETTE ROUND UNAVAILABLE");
                SetInstruction("The round changed. Escape and ask the pit boss for help again.");
                return;
            }
        }

        stage = RouletteStage.Spinning;
        spinTimer = Mathf.Max(0.1f, spinSeconds);
        wheelStartAngle = wheelRing.localEulerAngles.z;
        int pocketIndex = System.Array.IndexOf(EuropeanWheelOrder, winningNumber);
        float step = 360f / EuropeanWheelOrder.Length;
        wheelTargetAngle = wheelStartAngle + 1800f + Mathf.Repeat(pocketIndex * step - wheelStartAngle, 360f);
        ballAngle = 90f;
        SetStatus("SPINNING  •  GOOD LUCK!");
        SetInstruction("The wheel is slowing down...");
    }

    private void UpdateSpinAnimation(float deltaTime)
    {
        spinTimer = Mathf.Max(0f, spinTimer - deltaTime);
        float duration = Mathf.Max(0.1f, spinSeconds);
        float t = 1f - spinTimer / duration;
        float eased = 1f - Mathf.Pow(1f - t, 4f);
        float angle = Mathf.Lerp(wheelStartAngle, wheelTargetAngle, eased);
        wheelRing.localRotation = Quaternion.Euler(0f, 0f, angle);
        ballAngle = 90f - 1440f * eased;
        UpdateBallPosition();
        if (spinTimer > 0f)
        {
            return;
        }

        int pocketIndex = System.Array.IndexOf(EuropeanWheelOrder, winningNumber);
        wheelRing.localRotation = Quaternion.Euler(
            0f, 0f, wheelStartAngle + 1800f +
            Mathf.Repeat(pocketIndex * (360f / EuropeanWheelOrder.Length) - wheelStartAngle, 360f));
        ballAngle = 90f;
        UpdateBallPosition();
        EnterSweepStage();
    }

    private void EnterSweepStage()
    {
        if (!roundController.TrySetAssistanceWinningNumber(assistanceRoundId, winningNumber))
        {
            SetStatus("ROULETTE ROUND UNAVAILABLE");
            Abandon();
            return;
        }

        stage = RouletteStage.Sweep;
        if (statusHeader != null)
        {
            statusHeader.SetActive(true);
        }

        instructionText.gameObject.SetActive(true);
        spinStageRoot.gameObject.SetActive(false);
        sweepStageRoot.gameObject.SetActive(true);
        CreateSweepChips();
        HighlightWinningNumber(sweepPocketOutlines);

        SetStatus($"WINNING POCKET  •  {winningNumber}  {GetPocketColorName(winningNumber)}");
        SetInstruction($"SWEEP! Drag {requiredSweepCount} losing bets into the dealer collection tray.");
        if (CanCompleteSweep(sweptCount, requiredSweepCount))
        {
            AdvanceAfterSweep();
        }
    }

    private void CreateSweepChips()
    {
        sweepChips.Clear();
        requiredSweepCount = 0;
        sweptCount = 0;
        Dictionary<string, int> stackCounts = new Dictionary<string, int>();
        foreach (RouletteCustomerBet customerBet in roundController.AssistanceBets)
        {
            if (!customerBet.IsWinning)
            {
                requiredSweepCount++;
            }

            string region = GetBetRegionKey(customerBet.Bet);
            stackCounts.TryGetValue(region, out int stackIndex);
            stackCounts[region] = stackIndex + 1;
            RectTransform cell = GetBetCell(customerBet.Bet, sweepNumberCells, sweepOutsideCells);
            Vector2 home = GetGameAreaPosition(cell) + new Vector2(
                ((stackIndex % 3) - 1) * 13f, (stackIndex / 3) * 14f);
            bool protectedChip = customerBet.IsWinning;
            if (protectedChip)
            {
                home += new Vector2(0f, 7f);
            }
            else if (customerBet.IsSwept)
            {
                sweptCount++;
                continue;
            }

            Color chipColor = protectedChip
                ? new Color(0.73f, 0.57f, 0.19f)
                : customerBet.Bet.Type == RouletteBetType.Red
                    ? new Color(0.81f, 0.16f, 0.13f)
                    : customerBet.Bet.Type == RouletteBetType.Black
                        ? new Color(0.10f, 0.11f, 0.13f)
                        : new Color(0.16f, 0.43f, 0.78f);
            GameObject chipObject = CasinoMinigamePresentation.CreateImage(
                $"{(protectedChip ? "Winning" : "Losing")} Roulette Bet {customerBet.BetId}",
                sweepStageRoot, home, new Vector2(40f, 38f), chipColor);
            Outline chipOutline = chipObject.AddComponent<Outline>();
            chipOutline.effectColor = protectedChip
                ? new Color(1f, 0.88f, 0.22f)
                : new Color(0.92f, 0.82f, 0.48f);
            chipOutline.effectDistance = protectedChip ? new Vector2(4f, -4f) : new Vector2(1f, -1f);
            string betName = customerBet.Customer != null ? GetCustomerLabel(customerBet.Customer.name) : "BET";
            CasinoMinigamePresentation.CreateText(chipObject.transform, "Bet Chip Label",
                $"{betName}\n{RouletteRoundController.FormatPounds(customerBet.Bet.StakePence)}",
                Vector2.zero, new Vector2(40f, 36f), 8, Color.white);
            sweepChips.Add(new SweepChip
            {
                Rect = chipObject.GetComponent<RectTransform>(),
                Home = home,
                Protected = protectedChip,
                CustomerBet = customerBet
            });
        }

        if (sweepProgressText != null)
        {
            sweepProgressText.text = $"{sweptCount} / {requiredSweepCount} LOSING BETS";
        }
    }

    private bool TryGetWheelLocalPointer(Vector2 screenPosition, out Vector2 local)
    {
        local = default;
        return wheelHitArea != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
            wheelHitArea, screenPosition, null, out local);
    }

    private Vector2 ScreenToGameArea(Vector2 screenPosition)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            gameArea as RectTransform, screenPosition, null, out Vector2 local)
            ? local
            : Vector2.zero;
    }

    private void AdvanceAfterSweep()
    {
        bool hasUnpaidWinner = false;
        foreach (RouletteCustomerBet customerBet in roundController.AssistanceBets)
        {
            if (customerBet.IsWinning && !customerBet.IsPaid)
            {
                hasUnpaidWinner = true;
                break;
            }
        }

        if (hasUnpaidWinner)
        {
            EnterPayStage();
            return;
        }

        SetStatus($"TABLE CLEARED  •  {winningNumber} {GetPocketColorName(winningNumber)}");
        SetInstruction("No winning bets. Roulette assistance complete.");
        Complete(true, 100 + sweptCount * 25);
    }

    private void EnterPayStage()
    {
        stage = RouletteStage.Pay;
        if (statusHeader != null)
        {
            statusHeader.SetActive(false);
        }

        instructionText.gameObject.SetActive(false);
        sweepStageRoot.gameObject.SetActive(false);
        payoutStageRoot.gameObject.SetActive(true);
        CreateWinningBetVisuals();
        HighlightWinningNumber(payoutPocketOutlines);
        StartNextPayout();
    }

    private void CreateWinningBetVisuals()
    {
        Dictionary<string, int> stackCounts = new Dictionary<string, int>();
        foreach (RouletteCustomerBet customerBet in roundController.AssistanceBets)
        {
            if (!customerBet.IsWinning)
            {
                continue;
            }

            string region = GetBetRegionKey(customerBet.Bet);
            stackCounts.TryGetValue(region, out int stackIndex);
            stackCounts[region] = stackIndex + 1;
            RectTransform cell = GetBetCell(customerBet.Bet, payoutNumberCells, payoutOutsideCells);
            Vector2 home = GetGameAreaPosition(cell) + new Vector2(
                ((stackIndex % 3) - 1) * 13f, (stackIndex / 3) * 14f + 7f);
            GameObject chipObject = CasinoMinigamePresentation.CreateImage(
                $"Winning Roulette Bet {customerBet.BetId}", payoutStageRoot,
                home, new Vector2(40f, 38f), new Color(0.73f, 0.57f, 0.19f));
            Outline outline = chipObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.91f, 0.28f);
            outline.effectDistance = new Vector2(4f, -4f);
            string betName = customerBet.Customer != null ? GetCustomerLabel(customerBet.Customer.name) : "BET";
            CasinoMinigamePresentation.CreateText(chipObject.transform, "Winning Bet Label",
                $"{betName}\n{RouletteRoundController.FormatPounds(customerBet.Bet.StakePence)}",
                Vector2.zero, new Vector2(40f, 36f), 8, Color.white);
        }
    }

    private RectTransform GetBetCell(
        RouletteBet bet,
        Dictionary<int, RectTransform> numberCells,
        Dictionary<RouletteBetType, RectTransform> outsideCells)
    {
        if (bet.Type == RouletteBetType.StraightUp)
        {
            return numberCells.TryGetValue(bet.Number, out RectTransform cell) ? cell : null;
        }

        return outsideCells.TryGetValue(bet.Type, out RectTransform outside) ? outside : null;
    }

    private static string GetBetRegionKey(RouletteBet bet) =>
        bet.Type == RouletteBetType.StraightUp ? $"NUMBER_{bet.Number}" : bet.Type.ToString();

    private static Color GetBetAreaColor(RouletteBetType betType) => betType switch
    {
        RouletteBetType.Red => new Color(0.70f, 0.13f, 0.12f),
        RouletteBetType.Black => new Color(0.055f, 0.065f, 0.07f),
        RouletteBetType.Odd => new Color(0.12f, 0.38f, 0.24f),
        RouletteBetType.Even => new Color(0.12f, 0.38f, 0.24f),
        _ => new Color(0.055f, 0.065f, 0.07f)
    };

    private void HighlightWinningNumber(Dictionary<int, Outline> outlines)
    {
        foreach (KeyValuePair<int, Outline> pocket in outlines)
        {
            pocket.Value.effectDistance = pocket.Key == winningNumber
                ? new Vector2(4f, -4f)
                : new Vector2(1f, -1f);
            pocket.Value.effectColor = pocket.Key == winningNumber
                ? new Color(1f, 0.9f, 0.25f)
                : new Color(0.91f, 0.77f, 0.37f, 0.9f);
        }
    }

    private static string GetCustomerLabel(string customerName)
    {
        if (string.IsNullOrEmpty(customerName))
        {
            return "BET";
        }

        return customerName.Length <= 6 ? customerName : customerName.Substring(0, 6);
    }

    private System.Collections.IEnumerator ResetProtectedChipScale(RectTransform chip)
    {
        yield return new WaitForSecondsRealtime(0.14f);
        if (chip != null)
        {
            chip.localScale = Vector3.one;
        }
    }

    private void StopPaymentAnimation()
    {
        if (paymentAnimation != null)
        {
            StopCoroutine(paymentAnimation);
            paymentAnimation = null;
        }
    }

    private void UpdateBallPosition()
    {
        if (ball == null)
        {
            return;
        }

        float radians = ballAngle * Mathf.Deg2Rad;
        ball.anchoredPosition = new Vector2(-350f, 0f) +
                                new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * 218f;
    }

    private void SetStatus(string value)
    {
        if (statusText != null)
        {
            statusText.text = value;
        }
    }

    private void SetInstruction(string value)
    {
        if (instructionText != null)
        {
            instructionText.text = value;
        }
    }

    private void RestorePlayer()
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

    private void ResetTemporaryInput()
    {
        isDragging = false;
        heldChip = null;
        sweepChips.Clear();
        sweepNumberCells.Clear();
        sweepOutsideCells.Clear();
        payoutNumberCells.Clear();
        payoutOutsideCells.Clear();
        sweepPocketOutlines.Clear();
        payoutPocketOutlines.Clear();
    }
}
