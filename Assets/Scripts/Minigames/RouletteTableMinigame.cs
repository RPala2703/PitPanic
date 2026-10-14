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
        Sweep
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

    [SerializeField, Min(1)] private int chipsToSweep = 3;
    [SerializeField, Min(0.1f)] private float spinSeconds = 2.4f;
    [SerializeField] private CasinoPlayerController player;
    [SerializeField] private Transform tableRoot;
    [SerializeField] private Transform wheel;

    private sealed class SweepChip
    {
        public RectTransform Rect;
        public Vector2 Home;
        public bool Collected;
    }

    private readonly List<SweepChip> sweepChips = new List<SweepChip>();
    private Transform gameArea;
    private Transform spinStageRoot;
    private Transform sweepStageRoot;
    private RectTransform wheelHitArea;
    private RectTransform wheelRing;
    private RectTransform ball;
    private RectTransform trayHitArea;
    private Text statusText;
    private Text instructionText;
    private Text resultText;
    private Text sweepProgressText;
    private readonly Dictionary<int, Outline> bettingPocketOutlines = new Dictionary<int, Outline>();
    private readonly Dictionary<int, Outline> layoutPocketOutlines = new Dictionary<int, Outline>();
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
    private bool isDragging;
    private bool controlsCaptured;
    private CursorLockMode savedCursorLockMode;
    private bool savedCursorVisible;

    public bool IsConfigured => player != null && tableRoot != null && wheel != null;
    public override bool CanBegin => IsConfigured && Mouse.current != null;
    protected override string PresentationTitle => "SPIN!  •  SWEEP!";
    protected override string PresentationInstructions =>
        "Drag the wheel to spin. Then drag every loose chip into the dealer collection tray.";
    public int WinningNumber => winningNumber;
    public int SweptChipCount => sweptCount;
    public static bool CanCompleteSweep(int sweptChipCount, int requiredChipCount) =>
        requiredChipCount > 0 && sweptChipCount >= requiredChipCount;

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
        Transform wheelTransform)
    {
        player = casinoPlayer;
        tableRoot = root;
        wheel = wheelTransform;
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

        stage = RouletteStage.Spin;
        winningNumber = -1;
        sweptCount = 0;
        angularTravel = 0f;
        spinTimer = 0f;
        wheelStartAngle = 0f;
        wheelTargetAngle = 0f;
        ballAngle = 90f;
        isDragging = false;
        heldChip = null;
        sweepChips.Clear();
        bettingPocketOutlines.Clear();
        layoutPocketOutlines.Clear();
        spinStageRoot = null;
        sweepStageRoot = null;
        gameArea = Presentation.GameArea;
        BuildPopup();
        SetStatus("SPIN THE WHEEL");
        SetInstruction("Click and drag in a circle across the wheel. Release after a full turn.");
        UpdateBallPosition();
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
        RestorePlayer();
        ResetTemporaryInput();
        gameArea = null;
    }

    protected override void OnComplete(bool success)
    {
        RestorePlayer();
        ResetTemporaryInput();
        gameArea = null;
    }

    private void BuildPopup()
    {
        GameObject header = CasinoMinigamePresentation.CreateImage(
            "Roulette Popup Status", gameArea, new Vector2(0f, 252f), new Vector2(1310f, 62f),
            new Color(0.025f, 0.12f, 0.09f, 0.96f));
        statusText = CasinoMinigamePresentation.CreateText(
            header.transform, "Roulette Status", "SPIN THE WHEEL",
            new Vector2(0f, 0f), new Vector2(1250f, 54f), 25,
            new Color(1f, 0.87f, 0.44f));
        instructionText = CasinoMinigamePresentation.CreateText(
            gameArea, "Roulette Instructions", "",
            new Vector2(0f, -274f), new Vector2(1300f, 42f), 18, Color.white);
        BuildWheel();
        BuildBettingLayout();
        resultText = CasinoMinigamePresentation.CreateText(
            sweepStageRoot, "Roulette Result", "",
            new Vector2(-520f, 145f), new Vector2(190f, 100f), 28,
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
            bettingPocketOutlines[number] = pocketOutline;
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

    private void BuildBettingLayout()
    {
        GameObject stageRoot = new GameObject("Roulette Sweep Stage", typeof(RectTransform));
        stageRoot.transform.SetParent(gameArea, false);
        RectTransform stageRect = stageRoot.GetComponent<RectTransform>();
        stageRect.anchorMin = Vector2.zero;
        stageRect.anchorMax = Vector2.one;
        stageRect.offsetMin = Vector2.zero;
        stageRect.offsetMax = Vector2.zero;
        sweepStageRoot = stageRoot.transform;
        stageRoot.SetActive(false);

        GameObject panel = CasinoMinigamePresentation.CreateImage(
            "Roulette Betting Layout", sweepStageRoot, new Vector2(365f, -5f),
            new Vector2(680f, 398f), new Color(0.045f, 0.28f, 0.18f));
        Outline panelOutline = panel.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.64f, 0.53f, 0.27f);
        panelOutline.effectDistance = new Vector2(3f, -3f);
        CasinoMinigamePresentation.CreateText(panel.transform, "Betting Layout Label",
            "ROULETTE BETTING LAYOUT", new Vector2(0f, 166f), new Vector2(620f, 36f), 20,
            new Color(1f, 0.85f, 0.42f));

        for (int number = 0; number <= 36; number++)
        {
            int column = number == 0 ? -1 : (number - 1) % 12;
            int row = number == 0 ? 0 : (number - 1) / 12;
            Vector2 position = number == 0
                ? new Vector2(-294f, -6f)
                : new Vector2(-222f + column * 47f, 80f - row * 67f);
            Vector2 size = number == 0 ? new Vector2(54f, 190f) : new Vector2(43f, 56f);
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
            layoutPocketOutlines[number] = pocketOutline;

            CasinoMinigamePresentation.CreateText(pocket.transform, "Betting Number",
                number.ToString(), Vector2.zero, size, 13, Color.white);
        }

        GameObject tray = CasinoMinigamePresentation.CreateImage(
            "Roulette Dealer Collection Tray", sweepStageRoot, new Vector2(560f, 25f),
            new Vector2(270f, 268f), new Color(0.16f, 0.35f, 0.22f));
        trayHitArea = tray.GetComponent<RectTransform>();
        Outline trayOutline = tray.AddComponent<Outline>();
        trayOutline.effectColor = new Color(1f, 0.83f, 0.23f);
        trayOutline.effectDistance = new Vector2(5f, -5f);
        CasinoMinigamePresentation.CreateText(tray.transform, "Dealer Tray Label",
            "DEALER\nCOLLECTION TRAY", new Vector2(0f, 85f), new Vector2(240f, 68f), 19,
            new Color(1f, 0.88f, 0.44f));
        sweepProgressText = CasinoMinigamePresentation.CreateText(
            tray.transform, "Sweep Progress", "0 / 3 CHIPS",
            new Vector2(0f, -83f), new Vector2(240f, 42f), 18, Color.white);
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

        Vector2 localPoint = ScreenToGameArea(screenPosition);
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
            previousPointer = localPoint;
            SetInstruction("Drag the chip into the highlighted collection tray.");
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

        if (stage == RouletteStage.Sweep && heldChip != null)
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
            if (CasinoMinigamePresentation.ContainsScreenPoint(trayHitArea, screenPosition))
            {
                heldChip.Collected = true;
                heldChip.Rect.anchoredPosition =
                    trayHitArea.anchoredPosition + new Vector2(-58f + (sweptCount % 3) * 58f, -18f - (sweptCount / 3) * 40f);
                sweptCount++;
                if (sweepProgressText != null)
                {
                    sweepProgressText.text = $"{sweptCount} / {chipsToSweep} CHIPS";
                }

                if (CanCompleteSweep(sweptCount, chipsToSweep))
                {
                    SetStatus($"TABLE CLEARED  •  {winningNumber} {GetPocketColorName(winningNumber)}");
                    SetInstruction("Roulette service complete.");
                    Complete(true, 100 + sweptCount * 25);
                }
                else
                {
                    SetStatus($"SWEEP!  •  {sweptCount}/{chipsToSweep} CHIPS");
                    SetInstruction("Drag the next loose chip into the collection tray.");
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
        winningNumber = Random.Range(0, 37);
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
        stage = RouletteStage.Sweep;
        spinStageRoot.gameObject.SetActive(false);
        sweepStageRoot.gameObject.SetActive(true);
        CreateSweepChips();
        resultText.text = $"{winningNumber}\n{GetPocketColorName(winningNumber)}";
        resultText.color = winningNumber == 0
            ? new Color(0.42f, 1f, 0.57f)
            : IsRedPocket(winningNumber)
                ? new Color(1f, 0.47f, 0.41f)
                : Color.white;
        foreach (KeyValuePair<int, Outline> pocket in layoutPocketOutlines)
        {
            pocket.Value.effectDistance = pocket.Key == winningNumber
                ? new Vector2(4f, -4f)
                : new Vector2(1f, -1f);
            pocket.Value.effectColor = pocket.Key == winningNumber
                ? new Color(1f, 0.9f, 0.25f)
                : new Color(0.91f, 0.77f, 0.37f, 0.9f);
        }

        SetStatus($"WINNING POCKET  •  {winningNumber}  {GetPocketColorName(winningNumber)}");
        SetInstruction($"SWEEP! Drag all {chipsToSweep} chips into the dealer collection tray.");
    }

    private void CreateSweepChips()
    {
        sweepChips.Clear();
        for (int i = 0; i < chipsToSweep; i++)
        {
            int number = 1 + (i * 11 + 4) % 36;
            int column = (number - 1) % 12;
            int row = (number - 1) / 12;
            Vector2 home = new Vector2(365f - 222f + column * 47f, -5f + 80f - row * 67f);
            Color chipColor = i % 3 == 0
                ? new Color(0.83f, 0.16f, 0.13f)
                : i % 3 == 1
                    ? new Color(0.16f, 0.39f, 0.79f)
                    : new Color(0.19f, 0.64f, 0.35f);
            GameObject chipObject = CasinoMinigamePresentation.CreateImage(
                $"Loose Roulette Chip {i + 1}", sweepStageRoot, home, new Vector2(44f, 44f), chipColor);
            chipObject.AddComponent<Outline>().effectColor = new Color(1f, 0.88f, 0.45f);
            CasinoMinigamePresentation.CreateText(chipObject.transform, "Chip Value",
                i % 3 == 0 ? "£5" : i % 3 == 1 ? "£10" : "£25",
                Vector2.zero, new Vector2(40f, 32f), 12, Color.white);
            sweepChips.Add(new SweepChip
            {
                Rect = chipObject.GetComponent<RectTransform>(),
                Home = home
            });
        }

        if (sweepProgressText != null)
        {
            sweepProgressText.text = $"0 / {chipsToSweep} CHIPS";
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
        bettingPocketOutlines.Clear();
        layoutPocketOutlines.Clear();
    }
}
