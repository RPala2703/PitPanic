using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class RouletteInspectionView : MonoBehaviour
{
    private const int ReferenceWidth = 1600;
    private const int ReferenceHeight = 900;

    [SerializeField] private CasinoPlayerController player;
    [SerializeField] private TableInteractionZone interactionZone;
    [SerializeField] private RouletteRoundController roundController;

    private Canvas canvas;
    private GameObject prompt;
    private GameObject board;
    private Text dealerText;
    private Text phaseText;
    private Text participantsText;
    private Text betsText;
    private Text resultText;
    private Text historyText;
    private Text inventoryText;
    private Text assistanceText;
    private Button closeButton;
    private Transform bettingMarkerRoot;
    private readonly Dictionary<int, RectTransform> numberCells = new Dictionary<int, RectTransform>();
    private readonly Dictionary<RouletteBetType, RectTransform> outsideBetCells = new Dictionary<RouletteBetType, RectTransform>();
    private string lastBetSignature;
    private bool panelOpen;
    private float nextRefreshTime;
    private CursorLockMode savedCursorLockMode;
    private bool savedCursorVisible;

    public bool IsConfigured => player != null && interactionZone != null &&
                                interactionZone.IsConfigured && roundController != null &&
                                roundController.IsConfigured;
    public bool IsOpen => panelOpen;

    public void Configure(
        CasinoPlayerController casinoPlayer,
        TableInteractionZone zone,
        RouletteRoundController controller)
    {
        player = casinoPlayer;
        interactionZone = zone;
        roundController = controller;
        if (Application.isPlaying)
        {
            Build();
        }
    }

    public bool IsPlayerInRange(Transform candidate) =>
        IsConfigured && candidate != null && interactionZone.IsPlayerInRange(candidate);

    public bool TryOpenInspectionFromPlayerInput()
    {
        if (panelOpen || !IsConfigured || !IsPlayerInRange(player.transform) ||
            CasinoMinigamePresentation.IsOpen ||
            player.GetComponent<CasinoPlayerFloatCarrier>()?.IsCarrying == true)
        {
            return false;
        }

        panelOpen = true;
        savedCursorLockMode = Cursor.lockState;
        savedCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        player.SetMovementEnabled(false);
        prompt.SetActive(false);
        Refresh();
        board.SetActive(true);
        closeButton.gameObject.SetActive(true);
        return true;
    }

    private void Start()
    {
        if (!IsConfigured)
        {
            Debug.LogError($"{nameof(RouletteInspectionView)} on {name} is missing its player, roulette zone, or controller.", this);
            enabled = false;
            return;
        }

        Build();
    }

    private void Update()
    {
        if (panelOpen)
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (Time.unscaledTime >= nextRefreshTime)
            {
                nextRefreshTime = Time.unscaledTime + 0.25f;
                Refresh();
            }

            return;
        }

        bool carrying = player != null &&
                        player.GetComponent<CasinoPlayerFloatCarrier>()?.IsCarrying == true;
        bool showPrompt = IsConfigured && !carrying &&
                          !CasinoMinigamePresentation.IsOpen &&
                          IsPlayerInRange(player.transform);
        if (prompt.activeSelf != showPrompt)
        {
            prompt.SetActive(showPrompt);
        }
    }

    private void Close()
    {
        panelOpen = false;
        if (board != null)
        {
            board.SetActive(false);
        }

        if (player != null)
        {
            player.SetMovementEnabled(true);
        }

        Cursor.lockState = savedCursorLockMode;
        Cursor.visible = savedCursorVisible;
    }

    private void OnDisable()
    {
        if (panelOpen)
        {
            Close();
        }
    }

    private void Build()
    {
        if (canvas != null || player == null)
        {
            return;
        }

        GameObject root = new GameObject(
            "Roulette Inspection UI",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.matchWidthOrHeight = 0.5f;

        prompt = CreateObject("Roulette Inspection Prompt", canvas.transform);
        SetRect(prompt, new Vector2(0.5f, 0f), new Vector2(0f, 104f), new Vector2(480f, 48f));
        Image promptBackground = prompt.AddComponent<Image>();
        promptBackground.color = new Color(0.025f, 0.06f, 0.05f, 0.92f);
        promptBackground.raycastTarget = false;
        CreateText(prompt.transform, "Prompt", "Q  •  INSPECT ROULETTE",
            Vector2.zero, new Vector2(460f, 42f), 19, Color.white);

        board = CreateObject("Roulette Inspection Board", canvas.transform);
        SetRect(board, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1480f, 800f));
        board.AddComponent<Image>().color = new Color(0.025f, 0.12f, 0.09f, 0.99f);
        CreateText(board.transform, "Heading", "ROULETTE",
            new Vector2(0f, 352f), new Vector2(820f, 54f), 34, new Color(1f, 0.86f, 0.42f));
        closeButton = CreateButton(board.transform, "Close", "CLOSE  •  ESC",
            new Vector2(608f, 351f), new Vector2(190f, 48f), Close);

        GameObject wheelPanel = CreatePanel(board.transform, "Wheel Panel",
            new Vector2(-375f, 92f), new Vector2(650f, 430f));
        CreateText(wheelPanel.transform, "Wheel Title", "EUROPEAN SINGLE ZERO",
            new Vector2(0f, 180f), new Vector2(500f, 32f), 18, new Color(1f, 0.86f, 0.42f));
        CreateWheel(wheelPanel.transform);
        resultText = CreateText(wheelPanel.transform, "Winning Result", "NO RESULT YET",
            new Vector2(0f, -175f), new Vector2(550f, 36f), 20, Color.white);

        GameObject layoutPanel = CreatePanel(board.transform, "Layout Panel",
            new Vector2(-375f, -245f), new Vector2(650f, 260f));
        CreateText(layoutPanel.transform, "Layout Title", "BETTING LAYOUT",
            new Vector2(0f, 108f), new Vector2(510f, 26f), 16, new Color(1f, 0.86f, 0.42f));
        CreateBettingLayout(layoutPanel.transform);
        bettingMarkerRoot = layoutPanel.transform;

        GameObject detailsPanel = CreatePanel(board.transform, "Dealer and Round Details",
            new Vector2(340f, 196f), new Vector2(680f, 222f));
        dealerText = CreateText(detailsPanel.transform, "Dealer Status", "",
            new Vector2(0f, 68f), new Vector2(630f, 58f), 17, Color.white, TextAnchor.MiddleLeft);
        phaseText = CreateText(detailsPanel.transform, "Round Phase", "",
            new Vector2(0f, 0f), new Vector2(630f, 48f), 17, Color.white, TextAnchor.MiddleLeft);
        participantsText = CreateText(detailsPanel.transform, "Participants", "",
            new Vector2(0f, -68f), new Vector2(630f, 58f), 16, Color.white, TextAnchor.MiddleLeft);
        assistanceText = CreateText(board.transform, "Assistance Status", "",
            new Vector2(340f, 60f), new Vector2(640f, 34f), 15, new Color(1f, 0.86f, 0.42f), TextAnchor.MiddleLeft);

        GameObject betsPanel = CreatePanel(board.transform, "Current Bets",
            new Vector2(340f, -104f), new Vector2(680f, 170f));
        CreateText(betsPanel.transform, "Current Bets Title", "CURRENT / LAST ROUND BETS",
            new Vector2(0f, 58f), new Vector2(620f, 28f), 17, new Color(1f, 0.86f, 0.42f));
        betsText = CreateText(betsPanel.transform, "Bets", "No bets yet.",
            new Vector2(0f, -10f), new Vector2(620f, 110f), 15, Color.white, TextAnchor.UpperLeft);

        GameObject bottomPanel = CreatePanel(board.transform, "Results and Float",
            new Vector2(340f, -280f), new Vector2(680f, 140f));
        historyText = CreateText(bottomPanel.transform, "Recent Results", "",
            new Vector2(-140f, 0f), new Vector2(330f, 100f), 15, Color.white, TextAnchor.MiddleLeft);
        inventoryText = CreateText(bottomPanel.transform, "Float Inventory", "",
            new Vector2(170f, 0f), new Vector2(300f, 100f), 15, Color.white, TextAnchor.MiddleLeft);

        prompt.SetActive(false);
        board.SetActive(false);
    }

    private void Refresh()
    {
        if (!IsConfigured)
        {
            return;
        }

        AIDealer dealer = roundController.AssignedDealer;
        dealerText.text = dealer == null || !roundController.IsDealerStaffed
            ? "DEALER  •  UNSTAFFED\nRoulette is waiting for an assigned dealer."
            : $"DEALER  •  {dealer.DealerId}  •  {dealer.Energy:0}% ENERGY  •  FATIGUE {100f - dealer.Energy:0}%\nSTATUS  •  {dealer.StatusLabel}";
        phaseText.text = $"PHASE  •  {roundController.PhaseLabel}\n" +
                         $"PARTICIPANTS  •  {roundController.ParticipantCount}";

        List<string> customerNames = new List<string>();
        foreach (CasinoCustomerAI customer in roundController.SeatedCustomers)
        {
            if (customer != null)
            {
                customerNames.Add(customer.name);
            }
        }

        participantsText.text = customerNames.Count == 0
            ? "CUSTOMERS  •  None currently playing"
            : $"CUSTOMERS  •  {string.Join(", ", customerNames)}";
        assistanceText.text = roundController.HasPendingAssistance
            ? "ASSISTANCE  •  SPIN! / SWEEP! / PAY! TASK PENDING"
            : "ASSISTANCE  •  No pit boss task pending";

        IReadOnlyList<RouletteCustomerBet> bets = roundController.CurrentBets.Count > 0
            ? roundController.CurrentBets
            : roundController.PreviousBets;
        if (bets.Count == 0)
        {
            betsText.text = "No bets yet.";
        }
        else
        {
            List<string> lines = new List<string>();
            long totalReturns = 0;
            long totalPaid = 0;
            foreach (RouletteCustomerBet customerBet in bets)
            {
                string customer = customerBet.Customer != null ? customerBet.Customer.name : "Customer";
                string line = $"{customer}  •  {RouletteRoundController.GetBetLabel(customerBet.Bet)}  •  " +
                              $"{RouletteRoundController.FormatPounds(customerBet.Bet.StakePence)}";
                if (customerBet.IsSettled)
                {
                    line += customerBet.GrossReturnPence > 0
                        ? $"  →  WON {RouletteRoundController.FormatPounds(customerBet.GrossReturnPence)}"
                        : "  →  LOST";
                }

                if (customerBet.IsWinning)
                {
                    totalReturns += customerBet.GrossReturnPence;
                }

                if (customerBet.IsPaid)
                {
                    line += "  •  PAID";
                    totalPaid += customerBet.PaidOutPence;
                }

                lines.Add(line);
            }

            lines.Add($"WIN RETURNS  •  {RouletteRoundController.FormatPounds(totalReturns)}  •  PAID {RouletteRoundController.FormatPounds(totalPaid)}");
            betsText.text = string.Join("\n", lines);
        }
        RenderBetMarkers(bets);

        int winningNumber = roundController.LatestWinningNumber;
        resultText.text = winningNumber < 0
            ? "NO RESULT YET"
            : $"LAST RESULT  •  {winningNumber}  {GetColorName(winningNumber)}";
        resultText.color = winningNumber == 0
            ? new Color(0.55f, 1f, 0.58f)
            : EuropeanRouletteRules.IsRed(winningNumber)
                ? new Color(1f, 0.55f, 0.48f)
                : Color.white;

        IReadOnlyList<int> recentResults = roundController.RecentResults;
        historyText.text = recentResults.Count == 0
            ? "RECENT RESULTS\n—"
            : $"RECENT RESULTS\n{string.Join("  •  ", recentResults)}";

        CasinoFloatRefillDestination destination = roundController.FloatDestination;
        int[] chipCounts =
        {
            destination.GetChipCount(0),
            destination.GetChipCount(1),
            destination.GetChipCount(2),
            destination.GetChipCount(3)
        };
        inventoryText.text = $"TABLE FLOAT\nRED {chipCounts[0]}  BLUE {chipCounts[1]}\n" +
                             $"GREEN {chipCounts[2]}  BLACK {chipCounts[3]}\n" +
                             (destination.NeedsRefill() ? "REFILL REQUIRED" : "FLOAT OK");
    }

    private static void CreateWheel(Transform parent)
    {
        GameObject wheel = CreateObject("Inspection Wheel", parent);
        SetRect(wheel, new Vector2(0f, 5f), Vector2.zero, new Vector2(310f, 310f));
        wheel.AddComponent<Image>().color = new Color(0.08f, 0.15f, 0.10f);
        CreateText(wheel.transform, "Wheel Center", "0\nEUROPEAN",
            Vector2.zero, new Vector2(150f, 92f), 24, new Color(1f, 0.9f, 0.5f));

        for (int i = 0; i < 37; i++)
        {
            int number = (i + 1) % 37;
            float angle = (90f - i * 360f / 37f) * Mathf.Deg2Rad;
            Vector2 position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 125f;
            GameObject pocket = CreateObject($"Wheel Pocket {number}", wheel.transform);
            SetRect(pocket, new Vector2(0.5f, 0.5f), position, new Vector2(32f, 24f));
            Image image = pocket.AddComponent<Image>();
            image.color = number == 0
                ? new Color(0.12f, 0.52f, 0.27f)
                : EuropeanRouletteRules.IsRed(number)
                    ? new Color(0.68f, 0.12f, 0.12f)
                    : new Color(0.04f, 0.05f, 0.055f);
            CreateText(pocket.transform, "Number", number.ToString(),
                Vector2.zero, new Vector2(32f, 24f), 11, Color.white);
        }
    }

    private void CreateBettingLayout(Transform parent)
    {
        const float zeroX = -125f;
        Vector2 firstCell = new Vector2(-48f, 73f);
        const float columnSpacing = 46f;
        const float rowSpacing = 14f;
        for (int number = 0; number <= 36; number++)
        {
            Vector2 position = number == 0
                ? new Vector2(zeroX, -6f)
                : RouletteBettingLayout.GetNumberCellPosition(
                    number, firstCell, columnSpacing, rowSpacing);
            GameObject cell = CreateObject($"Layout Number {number}", parent);
            SetRect(cell, new Vector2(0.5f, 0.5f), position,
                number == 0 ? new Vector2(28f, 196f) : new Vector2(40f, 13f));
            Image image = cell.AddComponent<Image>();
            image.color = number == 0
                ? new Color(0.12f, 0.52f, 0.27f)
                : EuropeanRouletteRules.IsRed(number)
                    ? new Color(0.68f, 0.12f, 0.12f)
                    : new Color(0.04f, 0.05f, 0.055f);
            CreateText(cell.transform, "Number", number.ToString(), Vector2.zero,
                new Vector2(number == 0 ? 26f : 38f, 13f), 9, Color.white);
            numberCells[number] = cell.GetComponent<RectTransform>();
        }

        RouletteBetType[] types =
        {
            RouletteBetType.Red,
            RouletteBetType.Black,
            RouletteBetType.Odd,
            RouletteBetType.Even
        };
        string[] labels = { "RED", "BLACK", "ODD", "EVEN" };
        Vector2 outsideStart = new Vector2(-102f, -112f);
        for (int i = 0; i < types.Length; i++)
        {
            Vector2 position = RouletteBettingLayout.GetOutsideBetPosition(
                types[i], outsideStart, 70f);
            GameObject area = CreateObject($"Outside Bet {labels[i]}", parent);
            SetRect(area, new Vector2(0.5f, 0.5f), position, new Vector2(66f, 24f));
            Image image = area.AddComponent<Image>();
            image.color = types[i] == RouletteBetType.Red
                ? new Color(0.68f, 0.12f, 0.12f)
                : types[i] == RouletteBetType.Black
                    ? new Color(0.04f, 0.05f, 0.055f)
                    : new Color(0.11f, 0.37f, 0.24f);
            CreateText(area.transform, "Outside Bet Label", labels[i], Vector2.zero,
                new Vector2(64f, 22f), 10, Color.white);
            outsideBetCells[types[i]] = area.GetComponent<RectTransform>();
        }
    }

    private void RenderBetMarkers(IReadOnlyList<RouletteCustomerBet> bets)
    {
        if (bettingMarkerRoot == null || bets == null)
        {
            return;
        }

        System.Text.StringBuilder signatureBuilder = new System.Text.StringBuilder();
        foreach (RouletteCustomerBet bet in bets)
        {
            signatureBuilder.Append(bet.RoundId).Append(':').Append(bet.BetId).Append(':')
                .Append((int)bet.Bet.Type).Append(':').Append(bet.Bet.Number).Append(':')
                .Append(bet.Bet.StakePence).Append(':').Append(bet.IsWinning).Append(':')
                .Append(bet.IsPaid).Append('|');
        }

        string signature = signatureBuilder.ToString();
        if (signature == lastBetSignature)
        {
            return;
        }

        lastBetSignature = signature;
        for (int i = bettingMarkerRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = bettingMarkerRoot.GetChild(i);
            if (child.name.StartsWith("Current Bet Marker "))
            {
                Destroy(child.gameObject);
            }
        }

        foreach (RouletteCustomerBet bet in bets)
        {
            RectTransform cell = bet.Bet.Type == RouletteBetType.StraightUp
                ? numberCells[bet.Bet.Number]
                : outsideBetCells[bet.Bet.Type];
            GameObject marker = CreateObject($"Current Bet Marker {bet.BetId}", bettingMarkerRoot);
            Vector2 offset = bet.Bet.Type == RouletteBetType.StraightUp
                ? new Vector2(14f, 0f)
                : new Vector2(-22f, 0f);
            SetRect(marker, new Vector2(0.5f, 0.5f),
                cell.anchoredPosition + offset, new Vector2(24f, 16f));
            Image image = marker.AddComponent<Image>();
            image.color = bet.IsWinning
                ? new Color(0.93f, 0.72f, 0.16f)
                : new Color(0.21f, 0.22f, 0.22f);
            image.raycastTarget = false;
            CreateText(marker.transform, "Bet Marker Amount",
                RouletteRoundController.FormatPounds(bet.Bet.StakePence),
                Vector2.zero, new Vector2(24f, 14f), 7, Color.white);
        }
    }

    private static GameObject CreatePanel(Transform parent, string objectName, Vector2 position, Vector2 size)
    {
        GameObject panel = CreateObject(objectName, parent);
        SetRect(panel, new Vector2(0.5f, 0.5f), position, size);
        Image image = panel.AddComponent<Image>();
        image.color = new Color(0.055f, 0.24f, 0.17f, 0.98f);
        image.raycastTarget = false;
        return panel;
    }

    private static Text CreateText(
        Transform parent,
        string objectName,
        string value,
        Vector2 position,
        Vector2 size,
        int fontSize,
        Color color,
        TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        GameObject textObject = CreateObject(objectName, parent);
        SetRect(textObject, new Vector2(0.5f, 0.5f), position, size);
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(
        Transform parent,
        string objectName,
        string label,
        Vector2 position,
        Vector2 size,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = CreateObject(objectName, parent);
        SetRect(buttonObject, new Vector2(0.5f, 0.5f), position, size);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.42f, 0.26f, 0.10f);
        Button button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(onClick);
        CreateText(buttonObject.transform, "Label", label, Vector2.zero, size, 15, Color.white);
        return button;
    }

    private static GameObject CreateObject(string objectName, Transform parent)
    {
        GameObject result = new GameObject(objectName, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static void SetRect(GameObject target, Vector2 anchor, Vector2 position, Vector2 size)
    {
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static string GetColorName(int number) =>
        number == 0 ? "GREEN" : EuropeanRouletteRules.IsRed(number) ? "RED" : "BLACK";
}
