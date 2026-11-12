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
    private GameObject extraDetails;
    private Text detailsToggleLabel;
    private GameObject summaryArt;
    private Text summaryMessage;
    private Text summaryHint;
    private Transform bettingMarkerRoot;
    private readonly Dictionary<int, RectTransform> numberCells = new Dictionary<int, RectTransform>();
    private readonly Dictionary<RouletteBetType, RectTransform> outsideBetCells = new Dictionary<RouletteBetType, RectTransform>();
    private string lastBetSignature;
    private bool panelOpen;
    private float nextRefreshTime;
    private CursorLockMode savedCursorLockMode;
    private bool savedCursorVisible;
    private Button assistanceButton;
    private Button takeoverButton;
    private bool keepTakeoverOnClose;
    private GameObject dealerDetails;
    private Image dealerPortrait;
    private Text dealerName;
    private RectTransform inspectionWheel;
    private RectTransform inspectionBall;
    private Outline winningCellOutline;
    private int highlightedNumber = -1;

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
            CasinoPlayerInteractionRouter.IsAnyInspectionOpen() ||
            player.GetComponent<CasinoPlayerFloatCarrier>()?.IsCarrying == true)
        {
            return false;
        }

        panelOpen = true;
        CasinoSoundscape.SetFocus(transform);
        SetDetailsVisible(false);
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
        if (Time.timeScale == 0) return;
        if (panelOpen)
        {
            bool spinning = roundController.Phase == RouletteRoundPhase.Spinning;
            if (spinning)
            {
                inspectionWheel.Rotate(0, 0, -160f * Time.deltaTime);
                float angle = Time.time * 6f;
                inspectionBall.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 118f;
            }
            if (dealerPortrait != null && roundController.AssignedDealer != null)
                dealerPortrait.sprite = roundController.AssignedDealer.GetComponentInChildren<PixelCharacterVisual>()?.TitleFrame(Time.time);
            takeoverButton.gameObject.SetActive(!roundController.IsDealerStaffed);
            takeoverButton.GetComponentInChildren<Text>().text = roundController.IsPlayerOperating
                ? "Spin" : "NO DEALER? TAKE OVER!";
            if (roundController.IsPlayerOperating && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                StartManualRound();
            assistanceButton.gameObject.SetActive(roundController.IsCoverReady);
            assistanceButton.GetComponentInChildren<Text>().text = "COVER!";
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
                          !CasinoPlayerInteractionRouter.IsAnyInspectionOpen() &&
                          !CasinoMinigamePresentation.IsOpen &&
                          IsPlayerInRange(player.transform);
        if (prompt.activeSelf != showPrompt)
        {
            prompt.SetActive(showPrompt);
        }
    }

    private void Close()
    {
        if (!keepTakeoverOnClose) roundController.EndPlayerTakeover();
        keepTakeoverOnClose = false;
        CasinoSoundscape.ReleaseFocus(transform);
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

    private void StartManualRound()
    {
        if (!roundController.IsPlayerOperating)
        {
            roundController.BeginPlayerTakeover();
            player.GetComponentInChildren<PixelCharacterVisual>()?.PlayAction(CasinoAnimationPose.Deal, transform.position, .8f);
            Refresh();
            return;
        }
        if (!roundController.RunManualSpin()) return;
        keepTakeoverOnClose = true;
        Close();
        if (!roundController.GetComponent<CasinoMinigameStation>().TryBegin())
        {
            roundController.EndPlayerTakeover();
            TryOpenInspectionFromPlayerInput();
        }
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
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        prompt = CreateObject("Roulette Inspection Prompt", canvas.transform);
        SetRect(prompt, new Vector2(0.5f, 0f), new Vector2(0f, 104f), new Vector2(480f, 48f));
        Image promptBackground = prompt.AddComponent<Image>();
        promptBackground.color = new Color(0.025f, 0.06f, 0.05f, 0.92f);
        promptBackground.raycastTarget = false;
        CreateText(prompt.transform, "Prompt", "E  •  ROULETTE",
            Vector2.zero, new Vector2(460f, 42f), 19, Color.white);

        board = CreateObject("Roulette Inspection Board", canvas.transform);
        SetRect(board, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1480f, 800f));
        Image boardImage = board.AddComponent<Image>();
        CasinoHudStyle.Panel(boardImage, CasinoHudStyle.Ink);
        GameObject titleBadge = CreatePanel(board.transform, "Title Badge",
            new Vector2(0f, 352f), new Vector2(420f, 56f));
        CasinoHudStyle.Panel(titleBadge.GetComponent<Image>(), new Color(0.48f, 0.29f, 0.22f));
        CreateText(board.transform, "Heading", "ROULETTE",
            new Vector2(0f, 352f), new Vector2(390f, 50f), 32, new Color(1f, 0.86f, 0.42f));
        CasinoPixelArt.AddSparkle(board.transform, new Vector2(-260f, 352f), 24f);
        CasinoPixelArt.AddSparkle(board.transform, new Vector2(260f, 352f), 24f);
        closeButton = CreateButton(board.transform, "Close", "CLOSE  •  ESC",
            new Vector2(608f, 351f), new Vector2(190f, 48f), Close);
        takeoverButton = CreateButton(board.transform, "Take over", "NO DEALER? TAKE OVER!",
            new Vector2(-240, -357), new Vector2(490, 52), StartManualRound);
        assistanceButton = CreateButton(board.transform, "Start assistance", "COVER!",
            new Vector2(315f, -357f), new Vector2(420f, 52f), () =>
            {
                CasinoMinigameStation station = roundController.GetComponent<CasinoMinigameStation>();
                if (station == null || !station.IsTaskPending) return;
                Close();
                if (!station.TryBegin())
                    TryOpenInspectionFromPlayerInput();
            });
        assistanceButton.gameObject.SetActive(false);

        GameObject wheelPanel = CreatePanel(board.transform, "Wheel Panel",
            new Vector2(155f, 135f), new Vector2(440f, 380f));
        CreateText(wheelPanel.transform, "Wheel Title", "LUCKY WHEEL",
            new Vector2(0f, 162f), new Vector2(300f, 30f), 19, new Color(1f, 0.86f, 0.42f));
        CreateWheel(wheelPanel.transform);
        resultText = CreateText(wheelPanel.transform, "Winning Result", "NO RESULT YET",
            new Vector2(0f, -157f), new Vector2(360f, 42f), 28, Color.white);

        GameObject layoutPanel = CreatePanel(board.transform, "Layout Panel",
            new Vector2(-405f, -10f), new Vector2(590f, 660f));
        CreateText(layoutPanel.transform, "Layout Title", "LIVE BETS",
            new Vector2(0f, 290f), new Vector2(510f, 34f), 22, new Color(1f, 0.86f, 0.42f));
        CreateText(layoutPanel.transform, "Payout Guide",
            "Watch the chips. Help when COVER! lights up!",
            new Vector2(0f, 257f), new Vector2(550f, 36f), 15, Color.white);
        CreateBettingLayout(layoutPanel.transform);
        bettingMarkerRoot = layoutPanel.transform;

        GameObject detailsPanel = CreatePanel(board.transform, "Dealer and Round Details",
            new Vector2(540f, 165f), new Vector2(300f, 310f));
        dealerDetails = detailsPanel;
        GameObject portrait = CreateObject("Dealer Portrait", board.transform);
        SetRect(portrait, new Vector2(.5f, .5f), new Vector2(540, 140), new Vector2(170, 225));
        dealerPortrait = portrait.AddComponent<Image>();
        dealerPortrait.preserveAspect = true;
        dealerPortrait.raycastTarget = false;
        dealerName = CreateText(board.transform, "Dealer Name", "", new Vector2(540, -10),
            new Vector2(250, 40), 21, CasinoHudStyle.Gold);
        dealerText = CreateText(detailsPanel.transform, "Dealer Status", "",
            new Vector2(0f, 94f), new Vector2(270f, 80f), 17, Color.white, TextAnchor.MiddleLeft);
        phaseText = CreateText(detailsPanel.transform, "Round Phase", "",
            new Vector2(0f, 4f), new Vector2(270f, 70f), 17, Color.white, TextAnchor.MiddleLeft);
        participantsText = CreateText(detailsPanel.transform, "Participants", "",
            new Vector2(0f, -88f), new Vector2(270f, 80f), 16, Color.white, TextAnchor.MiddleLeft);
        assistanceText = CreateText(board.transform, "Assistance Status", "",
            new Vector2(315f, -78f), new Vector2(750f, 32f), 18, new Color(1f, 0.86f, 0.42f));

        extraDetails = CreateObject("More Table Details", board.transform);
        SetRect(extraDetails, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        GameObject betsPanel = CreatePanel(extraDetails.transform, "Current Bets",
            new Vector2(315f, -168f), new Vector2(750f, 170f));
        CreateText(betsPanel.transform, "Current Bets Title", "AT THE TABLE",
            new Vector2(0f, 65f), new Vector2(690f, 30f), 19, new Color(1f, 0.86f, 0.42f));
        betsText = CreateText(betsPanel.transform, "Bets", "No bets yet.",
            new Vector2(0f, -12f), new Vector2(690f, 110f), 16, Color.white, TextAnchor.UpperLeft);

        GameObject bottomPanel = CreatePanel(extraDetails.transform, "Results and Float",
            new Vector2(315f, -292f), new Vector2(750f, 74f));
        historyText = CreateText(bottomPanel.transform, "Recent Results", "",
            new Vector2(-175f, 0f), new Vector2(330f, 70f), 15, Color.white, TextAnchor.MiddleLeft);
        inventoryText = CreateText(bottomPanel.transform, "Float Inventory", "",
            new Vector2(185f, 0f), new Vector2(330f, 70f), 15, Color.white, TextAnchor.MiddleLeft);

        summaryArt = CreateObject("Table Summary", board.transform);
        SetRect(summaryArt, new Vector2(0.5f, 0.5f), new Vector2(315f, -192f), new Vector2(750f, 250f));
        CasinoPixelArt.AddSparkle(summaryArt.transform, new Vector2(-280f, 35f), 36f);
        CasinoPixelArt.AddSparkle(summaryArt.transform, new Vector2(280f, 35f), 36f);
        GameObject summaryChip = CreateObject("Lucky Chip", summaryArt.transform);
        SetRect(summaryChip, new Vector2(0.5f, 0.5f), new Vector2(0f, -90f), new Vector2(48f, 48f));
        CasinoPixelArt.SetSprite(summaryChip.AddComponent<Image>(), CasinoPixelArt.Chip, CasinoHudStyle.Coral);
        summaryChip.AddComponent<CasinoPixelSparkle>();
        summaryMessage = CreateText(summaryArt.transform, "Summary Message", "",
            new Vector2(0f, 25f), new Vector2(660f, 60f), 30, CasinoHudStyle.Gold);
        summaryHint = CreateText(summaryArt.transform, "Summary Hint", "",
            new Vector2(0f, -35f), new Vector2(500f, 35f), 17, Color.white);
        Button detailToggle = CreateButton(board.transform, "Toggle Details", "MORE INFO  +",
            new Vector2(395f, 351f), new Vector2(200f, 44f), ToggleDetails);
        detailsToggleLabel = detailToggle.GetComponentInChildren<Text>();
        SetDetailsVisible(false);
        prompt.SetActive(false);
        board.SetActive(false);
    }

    private void ToggleDetails() => SetDetailsVisible(!extraDetails.activeSelf);

    private void SetDetailsVisible(bool visible)
    {
        if (extraDetails == null) return;
        extraDetails.SetActive(visible);
        dealerDetails.SetActive(visible);
        dealerPortrait.gameObject.SetActive(!visible);
        dealerName.gameObject.SetActive(!visible);
        summaryArt.SetActive(!visible);
        participantsText.gameObject.SetActive(visible);
        if (detailsToggleLabel != null)
            detailsToggleLabel.text = visible ? "LESS INFO  -" : "MORE INFO  +";
    }

    private void Refresh()
    {
        if (!IsConfigured)
        {
            return;
        }

        AIDealer dealer = roundController.AssignedDealer;
        dealerText.text = roundController.IsPlayerOperating ? "PENGUIN BOSS DEALING!\nSPACE: RUN THE ROUND\nESC: LEAVE / WAIT FOR COVER" :
            dealer == null || !roundController.IsDealerStaffed
            ? "NEEDS A DEALER!"
            : $"DEALER  •  {dealer.DealerId}\nENERGY  •  {dealer.Energy:0}%\n{dealer.StatusLabel}";
        phaseText.text = $"{roundController.PhaseLabel}\n" +
                         $"PLAYERS  •  {roundController.ParticipantCount}";

        List<string> customerNames = new List<string>();
        foreach (CasinoCustomerAI customer in roundController.SeatedCustomers)
        {
            if (customer != null)
            {
                customerNames.Add(customer.name);
            }
        }

        participantsText.text = customerNames.Count == 0
            ? "NO PLAYERS YET"
            : $"PLAYING  •  {string.Join(", ", customerNames)}";
        dealerName.text = roundController.IsPlayerOperating ? "YOU'RE UP!" :
            roundController.IsDealerStaffed ? roundController.AssignedDealer.DealerId : "DEALER NEEDED";
        dealerPortrait.enabled = roundController.IsDealerStaffed;
        assistanceText.text = roundController.IsCoverReady ? "COVER THE WINNERS!" :
            roundController.FloatDestination.NeedsRefill() ? "CHIPS NEEDED!" : "";
        summaryMessage.text = roundController.HasPendingAssistance
            ? "YOUR HELP IS NEEDED!"
            : roundController.PhaseLabel.ToUpperInvariant();
        summaryHint.text = roundController.ParticipantCount == 1
            ? "1 PLAYER AT THE TABLE"
            : $"{roundController.ParticipantCount} PLAYERS AT THE TABLE";

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
                    line += customerBet.WinningsPence > 0
                        ? $"  >  WON {RouletteRoundController.FormatPounds(customerBet.WinningsPence)}"
                        : "  >  LOST";
                }

                if (customerBet.IsWinning)
                {
                    totalReturns += customerBet.WinningsPence;
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

        int winningNumber = roundController.IsCoverReady
            ? roundController.AssistanceWinningNumber : roundController.LatestWinningNumber;
        resultText.text = winningNumber < 0
            ? "READY TO ROLL!"
            : $"{winningNumber}  {GetColorName(winningNumber)}";
        if (highlightedNumber != winningNumber && winningNumber >= 0)
        {
            if (winningCellOutline != null) Destroy(winningCellOutline);
            winningCellOutline = numberCells[winningNumber].gameObject.AddComponent<Outline>();
            winningCellOutline.effectColor = CasinoHudStyle.Gold;
            winningCellOutline.effectDistance = new Vector2(3, -3);
            highlightedNumber = winningNumber;
            float angle = (90 - System.Array.IndexOf(CasinoPixelArt.WheelOrder, winningNumber) * 360f / 37f) * Mathf.Deg2Rad;
            inspectionBall.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 118f;
        }
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
                             $"BLACK {chipCounts[2]}  PINK {chipCounts[3]}\n" +
                             (destination.NeedsRefill() ? "REFILL REQUIRED" : "FLOAT OK");
    }

    private void CreateWheel(Transform parent)
    {
        GameObject wheel = CreateObject("Inspection Wheel", parent);
        SetRect(wheel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 300f));
        inspectionWheel = wheel.GetComponent<RectTransform>();
        Image wheelImage = wheel.AddComponent<Image>();
        CasinoPixelArt.SetSprite(wheelImage, CasinoPixelArt.Wheel, Color.white);
        wheelImage.raycastTarget = false;
        GameObject ball = CreateObject("Roulette Ball", wheel.transform);
        SetRect(ball, new Vector2(.5f, .5f), new Vector2(0, 118), new Vector2(13, 13));
        CasinoPixelArt.SetSprite(ball.AddComponent<Image>(), CasinoPixelArt.Ball, Color.white);
        inspectionBall = ball.GetComponent<RectTransform>();
    }

    private void CreateBettingLayout(Transform parent)
    {
        const float zeroX = -195f;
        Vector2 firstCell = new Vector2(-100f, 205f);
        const float columnSpacing = 100f;
        const float rowSpacing = 37f;
        for (int number = 0; number <= 36; number++)
        {
            Vector2 position = number == 0
                ? new Vector2(zeroX, firstCell.y - rowSpacing * 5.5f)
                : RouletteBettingLayout.GetNumberCellPosition(
                    number, firstCell, columnSpacing, rowSpacing);
            GameObject cell = CreateObject($"Layout Number {number}", parent);
            SetRect(cell, new Vector2(0.5f, 0.5f), position,
                number == 0 ? new Vector2(85f, 442f) : new Vector2(96f, 34f));
            Image image = cell.AddComponent<Image>();
            image.color = number == 0
                ? new Color(0.12f, 0.52f, 0.27f)
                : EuropeanRouletteRules.IsRed(number)
                    ? new Color(0.68f, 0.12f, 0.12f)
                    : new Color(0.04f, 0.05f, 0.055f);
            CreateText(cell.transform, "Number", number.ToString(), Vector2.zero,
                new Vector2(number == 0 ? 80f : 90f, 32f), 20, Color.white);
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
        Vector2 outsideStart = new Vector2(-195f, -260f);
        for (int i = 0; i < types.Length; i++)
        {
            Vector2 position = RouletteBettingLayout.GetOutsideBetPosition(
                types[i], outsideStart, 125f);
            GameObject area = CreateObject($"Outside Bet {labels[i]}", parent);
            SetRect(area, new Vector2(0.5f, 0.5f), position, new Vector2(120f, 40f));
            Image image = area.AddComponent<Image>();
            image.color = types[i] == RouletteBetType.Red
                ? new Color(0.68f, 0.12f, 0.12f)
                : types[i] == RouletteBetType.Black
                    ? new Color(0.04f, 0.05f, 0.055f)
                    : new Color(0.11f, 0.37f, 0.24f);
            CreateText(area.transform, "Outside Bet Label", labels[i], Vector2.zero,
                new Vector2(116f, 36f), 17, Color.white);
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
            Vector2 betPosition;
            if (bet.Bet.Type == RouletteBetType.StraightUp)
            {
                betPosition = numberCells[bet.Bet.Number].anchoredPosition;
            }
            else if (RouletteBettingLayout.IsOutsideBet(bet.Bet.Type))
            {
                betPosition = outsideBetCells[bet.Bet.Type].anchoredPosition;
            }
            else
            {
                betPosition = Vector2.zero;
                foreach (int number in bet.Bet.Numbers)
                    betPosition += numberCells[number].anchoredPosition;
                betPosition /= bet.Bet.Numbers.Length;
            }
            GameObject marker = CreateObject($"Current Bet Marker {bet.BetId}", bettingMarkerRoot);
            SetRect(marker, new Vector2(0.5f, 0.5f),
                betPosition + new Vector2(14f, 0f), new Vector2(30f, 18f));
            Image image = marker.AddComponent<Image>();
            CasinoPixelArt.SetSprite(image, CasinoPixelArt.ChipForAmount(bet.Bet.StakePence), Color.white);
            if (bet.IsWinning)
            {
                Outline outline = marker.AddComponent<Outline>();
                outline.effectColor = CasinoHudStyle.Gold;
                outline.effectDistance = new Vector2(2, -2);
            }
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
        CasinoHudStyle.Panel(image, new Color(0.055f, 0.24f, 0.17f, 0.98f));
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
        text.font = CasinoPixelFont.Default;
        text.text = value;
        text.fontSize = fontSize;
        CasinoPixelFont.Fit(text, fontSize);
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
        button.targetGraphic = image;
        CasinoHudStyle.Button(button);
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
