using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CashDeskPackMinigame : CasinoMinigame, IReadOnlyIntList
{
    private static readonly int[] FloatDenominationsPence = { 500, 250, 2500, 10000 };
    private static readonly int[] CustomerDenominationsPence = { 10000, 2500, 500, 250 };
    private static readonly string[] ChipNames = { "RED", "BLUE", "GREEN", "BLACK" };
    private static readonly int[] SourceDisplayOrder = { 1, 0, 2, 3 };
    private static readonly Color[] FloatChipColors =
    {
        new Color(0.81f, 0.16f, 0.13f),
        new Color(0.16f, 0.43f, 0.78f),
        new Color(0.20f, 0.62f, 0.34f),
        new Color(0.08f, 0.09f, 0.11f)
    };

    [SerializeField] private CasinoPlayerController player;
    [SerializeField] private Transform counterRoot;

    private readonly List<CasinoFloatRefillOrder> availableOrders = new List<CasinoFloatRefillOrder>();
    private readonly int[] requiredCounts = new int[4];
    private readonly int[] packedCounts = new int[4];
    private readonly Button[] chipButtons = new Button[4];
    private readonly Image[] sourceChipImages = new Image[4];
    private readonly Text[] stackCountTexts = new Text[4];
    private readonly List<RectTransform>[] trayChipViews =
    {
        new List<RectTransform>(),
        new List<RectTransform>(),
        new List<RectTransform>(),
        new List<RectTransform>()
    };

    private CasinoCashDeskInteraction refillManager;
    private CasinoFloatRefillOrder selectedOrder;
    private Canvas hud;
    private Text statusText;
    private Text instructionText;
    private Text totalText;
    private Text progressText;
    private Text sequenceText;
    private Text nextChipText;
    private RectTransform floatTrayRect;
    private int cashOutAmountPence = 4000;
    private int[] cashOutChipCounts;
    private int totalRequired;
    private int packedCount;
    private int sequenceIndex;
    private int[] packingSequence = System.Array.Empty<int>();
    private readonly List<int> selectionHistory = new List<int>();
    private float timeRemaining;
    public float PackingBonusSeconds { get; set; }
    private bool cashOutMode;
    private bool cashOutReady;
    private CursorLockMode savedCursorLockMode;
    private bool savedCursorVisible;

    public override bool CanBegin => IsConfigured && Mouse.current != null;
    public bool IsConfigured => player != null && counterRoot != null;
    public int PackAmountPence => selectedOrder != null ? selectedOrder.TotalPence : cashOutAmountPence;
    public int PackedChipCount => packedCount;
    public bool IsReadyToDeliver => cashOutReady;
    public bool IsCashOutMode => cashOutMode;
    public bool CanDragRackChip => IsRunning && !cashOutMode && !cashOutReady;
    public CasinoFloatRefillOrder PackedOrder => selectedOrder;
    public IReadOnlyIntList PackedCounts => this;
    public override bool TryGetDeadline(out float remaining, out float duration)
    {
        remaining = timeRemaining;
        duration = GetSequenceTimeLimit(totalRequired) + PackingBonusSeconds;
        return IsRunning && totalRequired > 0 && !cashOutReady && remaining > 0;
    }
    public int Count => packedCounts.Length;
    public int this[int index] => index >= 0 && index < packedCounts.Length ? packedCounts[index] : 0;

    protected override string PresentationTitle => "PACK!";
    protected override string PresentationInstructions =>
        "";

    public static int[] GetExactChipComposition(int amountPence, IReadOnlyList<int> chipDenominations)
    {
        List<BlackjackChipDenomination> composition = BlackjackChipDenomination.GetExactComposition(
            amountPence,
            ConvertDenominations(chipDenominations));
        int[] result = new int[composition.Count];
        for (int i = 0; i < composition.Count; i++)
        {
            result[i] = composition[i].ValuePence;
        }

        return result;
    }

    public static int[] BuildPackingSequence(IReadOnlyList<int> counts, int seed)
    {
        if (counts == null || counts.Count != FloatDenominationsPence.Length)
        {
            throw new System.ArgumentException("A packing sequence requires four denomination counts.", nameof(counts));
        }

        List<int> sequence = new List<int>();
        for (int denominationIndex = 0; denominationIndex < counts.Count; denominationIndex++)
        {
            if (counts[denominationIndex] < 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(counts), "Chip counts cannot be negative.");
            }

            for (int count = 0; count < counts[denominationIndex]; count++)
            {
                sequence.Add(denominationIndex);
            }
        }

        System.Random random = new System.Random(seed);
        for (int i = sequence.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            (sequence[i], sequence[swapIndex]) = (sequence[swapIndex], sequence[i]);
        }

        return sequence.ToArray();
    }

    public static float GetSequenceTimeLimit(int chipCount)
    {
        return 15f + Mathf.Max(0, chipCount) * 2f;
    }

    public static bool TryAdvancePackingSequence(
        IReadOnlyList<int> sequence,
        int currentIndex,
        int selectedDenomination,
        out int nextIndex)
    {
        nextIndex = currentIndex;
        if (sequence == null || currentIndex < 0 || currentIndex >= sequence.Count ||
            sequence[currentIndex] != selectedDenomination)
        {
            return false;
        }

        nextIndex = currentIndex + 1;
        return true;
    }

    public static string BuildPackingSequenceDisplay(IReadOnlyList<int> sequence, int itemsPerLine = 10)
    {
        if (sequence == null || itemsPerLine < 1)
        {
            throw new System.ArgumentException("A sequence and positive line length are required.", nameof(sequence));
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < sequence.Count; i++)
        {
            int denominationIndex = sequence[i];
            if (denominationIndex < 0 || denominationIndex >= FloatDenominationsPence.Length)
            {
                throw new System.ArgumentOutOfRangeException(nameof(sequence), "The sequence contains an invalid chip denomination.");
            }

            if (i > 0)
            {
                builder.Append(i % itemsPerLine == 0 ? "\n" : " • ");
            }

            builder.Append(ChipNames[denominationIndex]).Append(' ')
                .Append(FormatPounds(FloatDenominationsPence[denominationIndex]));
        }

        return builder.ToString();
    }

    public void Configure(CasinoPlayerController casinoPlayer, Transform root)
    {
        player = casinoPlayer;
        counterRoot = root;
    }

    public void SetRefillManager(CasinoCashDeskInteraction manager)
    {
        refillManager = manager;
    }

    public void SetAvailableFloatOrders(IReadOnlyList<CasinoFloatRefillOrder> orders)
    {
        availableOrders.Clear();
        if (orders == null)
        {
            return;
        }

        for (int i = 0; i < orders.Count; i++)
        {
            if (orders[i] != null)
            {
                availableOrders.Add(orders[i]);
            }
        }
    }

    public void SetFloatOrder(CasinoFloatRefillOrder order)
    {
        selectedOrder = order;
        cashOutMode = false;
    }

    public void SetCashOutAmount(int amountPence)
    {
        SetCashOutAmount(amountPence, null);
    }

    public void SetCashOutAmount(int amountPence, int[] chipCounts)
    {
        if (amountPence <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(amountPence));
        }
        if (chipCounts != null &&
            (chipCounts.Length != FloatDenominationsPence.Length ||
             !CountsMatchAmount(chipCounts, amountPence)))
            throw new System.ArgumentException("Cash-out chips must exactly match the requested amount.", nameof(chipCounts));

        availableOrders.Clear();
        selectedOrder = null;
        cashOutAmountPence = amountPence;
        cashOutChipCounts = chipCounts != null ? (int[])chipCounts.Clone() : null;
        cashOutMode = true;
    }

    protected override void OnBegin()
    {
        if (!IsConfigured || Mouse.current == null)
        {
            Complete(false, 0);
            return;
        }

        savedCursorLockMode = Cursor.lockState;
        savedCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        player.SetMovementEnabled(false);
        packedCount = 0;
        totalRequired = 0;
        sequenceIndex = 0;
        timeRemaining = 0f;
        packingSequence = System.Array.Empty<int>();
        cashOutReady = false;
        selectedOrder = null;
        System.Array.Clear(requiredCounts, 0, requiredCounts.Length);
        System.Array.Clear(packedCounts, 0, packedCounts.Length);
        CreateHud();

        if (availableOrders.Count > 0)
        {
            cashOutMode = false;
            ShowOrderSelection();
        }
        else if (cashOutMode)
        {
            BeginCashOutPacking();
        }
        else
        {
            Complete(false, 0);
        }
    }

    protected override void OnTick(float deltaTime)
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Abandon();
            return;
        }

        if (cashOutReady && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            CompleteCashOut();
            return;
        }

        if (cashOutReady || (!cashOutMode && selectedOrder == null))
        {
            return;
        }

        timeRemaining = Mathf.Max(0f, timeRemaining - deltaTime);
        RefreshTotals();
        if (timeRemaining <= 0f)
        {
            SetStatus("TIME'S UP  •  TRY THE ORDER AGAIN");
            Abandon();
        }
    }

    protected override void OnAbandon()
    {
        CancelSelectedOrder();
        RestorePlayer();
        CleanUp();
    }

    protected override void OnComplete(bool success)
    {
        if (!success)
        {
            CancelSelectedOrder();
        }

        RestorePlayer();
        CleanUp();
    }

    private void OnDestroy()
    {
        if (IsRunning)
        {
            Abandon();
        }
        else
        {
            CleanUp();
        }
    }

    private void ShowOrderSelection()
    {
        SetStatus("PICK A TABLE!");
        SetInstruction("Choose a float to pack.");
        CreatePanel("Available Table Floats", new Vector2(0f, -8f), new Vector2(1080f, 560f));
        for (int i = 0; i < availableOrders.Count; i++)
        {
            CasinoFloatRefillOrder order = availableOrders[i];
            int index = i;
            Vector2 position = new Vector2(i % 2 == 0 ? -270f : 270f, 120f - (i / 2) * 150f);
            CreateButton(
                $"Float Order {order.Destination.DisplayName}",
                $"PACK FOR {GetShortDestinationLabel(order)}\n{FormatPounds(order.TotalPence)}  •  {order.TotalChips} CHIPS",
                position,
                new Vector2(460f, 112f),
                () => SelectFloatOrder(index));
        }

    }

    private void SelectFloatOrder(int index)
    {
        if (index < 0 || index >= availableOrders.Count || refillManager == null)
        {
            SetInstruction("That float order is no longer available. Close and retry at the cash desk.");
            return;
        }

        CasinoFloatRefillOrder order = availableOrders[index];
        if (!refillManager.TryBeginPacking(order.Id))
        {
            SetInstruction("The register cannot reserve those chips right now. Choose another order.");
            return;
        }

        selectedOrder = order;
        StartPacking(order.Counts);
    }

    private void BeginCashOutPacking()
    {
        if (cashOutChipCounts != null)
        {
            System.Array.Copy(cashOutChipCounts, requiredCounts, requiredCounts.Length);
        }
        else
        {
            int[] composition = GetExactChipComposition(cashOutAmountPence, CustomerDenominationsPence);
            for (int i = 0; i < composition.Length; i++)
                requiredCounts[GetFloatDenominationIndex(composition[i])]++;
        }

        cashOutMode = true;
        selectedOrder = null;
        BuildPackingLayout();
        SetStatus($"CUSTOMER CASH-OUT  •  {FormatPounds(cashOutAmountPence)}");
        SetInstruction("Click the NEXT CHIP shown. UNDO removes the last chip. FINISH (E) pays the exact total.");
    }

    private static bool CountsMatchAmount(int[] counts, int amountPence)
    {
        int[] values = { 500, 250, 2500, 10000 };
        long total = 0;
        for (int i = 0; i < counts.Length; i++)
        {
            if (counts[i] < 0) return false;
            total += (long)counts[i] * values[i];
        }
        return total == amountPence;
    }

    private void StartPacking(int[] counts)
    {
        if (counts == null || counts.Length != requiredCounts.Length)
        {
            Abandon();
            return;
        }

        System.Array.Copy(counts, requiredCounts, counts.Length);
        cashOutMode = false;
        BuildPackingLayout();
        SetStatus($"PACK FOR {GetShortDestinationLabel(selectedOrder)}  •  {FormatPounds(selectedOrder.TotalPence)}");
        SetInstruction("Click chips to match every count. UNDO removes the last chip. Carry the full tray to the named table.");
    }

    private void BuildPackingLayout()
    {
        selectionHistory.Clear();
        ClearDynamicLayout();
        CreatePanel("Register Chip Choices", new Vector2(-515f, -5f), new Vector2(500f, 560f));
        CreateText("Rack Label", cashOutMode ? "CLICK THE NEXT CHIP!" : "CLICK CHIPS TO PACK!",
            new Vector2(-515f, 235f), new Vector2(460f, 38f), 21, new Color(1f, 0.83f, 0.44f));
        float[] chipX = { -625f, -405f, -625f, -405f };
        float[] chipY = { 95f, 95f, -120f, -120f };
        for (int i = 0; i < chipButtons.Length; i++)
        {
            int denominationIndex = SourceDisplayOrder[i];
            chipButtons[i] = CreateChipButton(denominationIndex, new Vector2(chipX[i], chipY[i]));
        }

        CreatePanel("Target Chip Tray", new Vector2(260f, -5f), new Vector2(990f, 590f));
        RectTransform trayRect = CreateRect("Float Tray Target", hud.transform, new Vector2(0.5f, 0.5f), new Vector2(260f, -5f), new Vector2(950f, 545f));
        floatTrayRect = trayRect;
        Image trayImage = trayRect.gameObject.AddComponent<Image>();
        trayImage.color = new Color(0.04f, 0.18f, 0.14f, 0.95f);
        Outline outline = trayRect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.47f, 1f, 0.6f, 0.95f);
        outline.effectDistance = new Vector2(4f, -4f);
        trayImage.raycastTarget = false;
        CreateText("Tray Label", cashOutMode ? "CUSTOMER TRAY" : "CLICKED CHIPS APPEAR HERE!",
            new Vector2(260f, 225f), new Vector2(880f, 38f), 20, new Color(0.63f, 1f, 0.7f));
        sequenceText = CreateText("Packing Sequence", "", new Vector2(260f, 160f), new Vector2(890f, 94f), 18, Color.white);
        sequenceText.horizontalOverflow = HorizontalWrapMode.Wrap;
        sequenceText.verticalOverflow = VerticalWrapMode.Truncate;
        nextChipText = CreateText("Next Chip", "", new Vector2(260f, 88f), new Vector2(800f, 34f), 21, new Color(1f, 0.88f, 0.45f));
        totalText = CreateText("Order Total", "", new Vector2(260f, -218f), new Vector2(850f, 35f), 19, Color.white);
        progressText = CreateText("Pack Progress", "", new Vector2(260f, -251f), new Vector2(850f, 30f), 17, new Color(1f, 0.84f, 0.48f));

        float[] stackX = { -75f, 145f, 365f, 585f };
        for (int i = 0; i < stackCountTexts.Length; i++)
        {
            CreateText($"Tray {ChipNames[i]} Label", $"{ChipNames[i]}  {FormatPounds(FloatDenominationsPence[i])}",
                new Vector2(stackX[i], 22f), new Vector2(205f, 30f), 16, Color.white);
            stackCountTexts[i] = CreateText($"Tray {ChipNames[i]} Count", "0/0",
                new Vector2(stackX[i], -145f), new Vector2(190f, 28f), 16, Color.white);
        }

        totalRequired = 0;
        for (int i = 0; i < requiredCounts.Length; i++)
        {
            totalRequired += requiredCounts[i];
        }

        packingSequence = cashOutMode
            ? BuildPackingSequence(requiredCounts, Random.Range(0, int.MaxValue))
            : System.Array.Empty<int>();
        timeRemaining = GetSequenceTimeLimit(totalRequired) + PackingBonusSeconds;
        sequenceIndex = 0;
        UpdateSequenceDisplay();
        RenderTrayStacks();
        UpdatePackProgress();
        CreateButton("Undo last chip", "UNDO LAST CHIP",
            new Vector2(-515, -320), new Vector2(300, 48), UndoLastChip);
        if (cashOutMode)
        {
            CreateButton("Finish Cash Out", "FINISH!  •  E",
                new Vector2(260f, -320f), new Vector2(300f, 48f), CompleteCashOut);
        }
    }

    private Button CreateChipButton(int denominationIndex, Vector2 position)
    {
        RectTransform rect = CreateRect(
            $"Register {ChipNames[denominationIndex]} Chip Choice",
            hud.transform,
            new Vector2(0.5f, 0.5f),
            position,
            new Vector2(158f, 168f));
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.035f, 0.14f, 0.13f, 0.98f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        CasinoHudStyle.Button(button);
        button.onClick.AddListener(() => SelectRackChip(denominationIndex));
        sourceChipImages[denominationIndex] = image;

        GameObject chipObject = new GameObject(
            $"{ChipNames[denominationIndex]} Chip",
            typeof(RectTransform),
            typeof(Image),
            typeof(Outline));
        chipObject.transform.SetParent(rect, false);
        RectTransform chipRect = chipObject.GetComponent<RectTransform>();
        chipRect.anchorMin = new Vector2(0.5f, 1f);
        chipRect.anchorMax = chipRect.anchorMin;
        chipRect.pivot = new Vector2(0.5f, 1f);
        chipRect.anchoredPosition = new Vector2(0f, -8f);
        chipRect.sizeDelta = new Vector2(100f, 100f);
        Image chipImage = chipObject.GetComponent<Image>();
        chipImage.sprite = CasinoPixelArt.Chip;
        chipImage.color = FloatChipColors[denominationIndex];
        chipImage.raycastTarget = false;
        Outline chipOutline = chipObject.GetComponent<Outline>();
        chipOutline.effectColor = new Color(1f, 0.91f, 0.62f, 0.9f);
        chipOutline.effectDistance = new Vector2(2f, -2f);

        Text amount = CreateText(
            $"{ChipNames[denominationIndex]} Chip Amount",
            FormatPounds(FloatDenominationsPence[denominationIndex]),
            Vector2.zero,
            new Vector2(90f, 38f),
            20,
            Color.white);
        amount.transform.SetParent(chipRect, false);
        amount.rectTransform.anchorMin = Vector2.zero;
        amount.rectTransform.anchorMax = Vector2.one;
        amount.rectTransform.offsetMin = new Vector2(3f, 3f);
        amount.rectTransform.offsetMax = new Vector2(-3f, -3f);
        CreateText($"{ChipNames[denominationIndex]} Chip Name", ChipNames[denominationIndex],
            new Vector2(position.x, position.y - 68f), new Vector2(150f, 28f), 16, Color.white);
        CreateText($"{ChipNames[denominationIndex]} Register Stock",
            $"IN REGISTER: {refillManager?.GetRegisterStock(denominationIndex) ?? 0}",
            new Vector2(position.x, position.y - 88f), new Vector2(158f, 24f), 11,
            new Color(0.75f, 0.9f, 0.82f));
        return button;
    }

    public void SelectCashOutChip(int denominationIndex)
    {
        if (!IsRunning || !cashOutMode || cashOutReady ||
            denominationIndex < 0 || denominationIndex >= requiredCounts.Length ||
            sequenceIndex >= packingSequence.Length)
        {
            return;
        }

        if (!TryAdvancePackingSequence(packingSequence, sequenceIndex, denominationIndex, out int nextIndex))
        {
            CasinoSoundscape.Play(CasinoSoundCue.Mistake, transform.position, transform);
            timeRemaining = Mathf.Max(0f, timeRemaining - 0.5f);
            int expectedDenomination = packingSequence[sequenceIndex];
            SetStatus($"WRONG CHIP  •  {timeRemaining:0.0}s LEFT");
            SetInstruction($"That order needs {ChipNames[expectedDenomination]}. Try again.");
            RefreshTotals();
            return;
        }

        packedCounts[denominationIndex]++;
        selectionHistory.Add(denominationIndex);
        CasinoSoundscape.Play(CasinoSoundCue.Chip, transform.position, transform);
        packedCount++;
        sequenceIndex = nextIndex;
        SetStatus("CORRECT CHIP  •  KEEP THE SEQUENCE MOVING");
        SetInstruction("Good. Select the next highlighted denomination.");
        RenderTrayStacks();
        UpdateSequenceDisplay();
        UpdatePackProgress();
        if (sequenceIndex == totalRequired)
        {
            OnPackingComplete();
        }
    }

    public void SelectRackChip(int denominationIndex)
    {
        if (!IsRunning || Time.timeScale == 0 || denominationIndex < 0 || denominationIndex >= requiredCounts.Length) return;
        if (cashOutMode) { SelectCashOutChip(denominationIndex); return; }
        if (packedCounts[denominationIndex] >= requiredCounts[denominationIndex])
        {
            CasinoSoundscape.Play(CasinoSoundCue.Mistake, transform.position, transform);
            timeRemaining = Mathf.Max(0, timeRemaining - .5f);
            SetInstruction("That chip is complete. Click a denomination still needed.");
            return;
        }
        AddPackedChip(denominationIndex);
    }

    public void UndoLastChip()
    {
        if (!IsRunning || Time.timeScale == 0 || selectionHistory.Count == 0) return;
        int index = selectionHistory[selectionHistory.Count - 1];
        selectionHistory.RemoveAt(selectionHistory.Count - 1);
        packedCounts[index]--;
        packedCount--;
        sequenceIndex--;
        cashOutReady = false;
        SetStatus($"UNDO  •  {FormatPounds(FloatDenominationsPence[index])} REMOVED");
        SetInstruction("Click the next required chip when ready.");
        RenderTrayStacks();
        UpdateSequenceDisplay();
        UpdatePackProgress();
        CasinoSoundscape.Play(CasinoSoundCue.Chip, transform.position, transform);
    }

    public void SetChipDragFeedback(int denominationIndex, bool isDragging)
    {
        if (denominationIndex < 0 || denominationIndex >= sourceChipImages.Length ||
            sourceChipImages[denominationIndex] == null)
        {
            return;
        }

        sourceChipImages[denominationIndex].color = isDragging
            ? new Color(0.16f, 0.48f, 0.32f, 1f)
            : new Color(0.035f, 0.14f, 0.13f, 0.98f);
    }

    public void TryDropRackChip(int denominationIndex, Vector2 screenPosition)
    {
        if (!CanDragRackChip || denominationIndex < 0 ||
            denominationIndex >= requiredCounts.Length)
        {
            return;
        }

        if (!CasinoMinigamePresentation.ContainsScreenPoint(floatTrayRect, screenPosition))
        {
            timeRemaining = Mathf.Max(0f, timeRemaining - 0.5f);
            SetStatus("CHIP MISSED THE TRAY");
            SetInstruction("Drop the chip onto the highlighted float tray and try again.");
            RefreshTotals();
            return;
        }

        if (packedCounts[denominationIndex] >= requiredCounts[denominationIndex])
        {
            timeRemaining = Mathf.Max(0f, timeRemaining - 0.5f);
            SetStatus($"WRONG CHIP  •  {ChipNames[denominationIndex]} NOT REQUIRED");
            SetInstruction("That denomination has reached its exact order quantity. Choose another chip.");
            RefreshTotals();
            return;
        }

        AddPackedChip(denominationIndex);
    }

    public void RemovePackedChip(int denominationIndex)
    {
        if (!IsRunning || cashOutMode || cashOutReady || denominationIndex < 0 ||
            denominationIndex >= packedCounts.Length || packedCounts[denominationIndex] <= 0)
        {
            return;
        }

        packedCounts[denominationIndex]--;
        selectionHistory.Remove(denominationIndex);
        packedCount--;
        sequenceIndex = packedCount;
        SetStatus($"REMOVED  •  {ChipNames[denominationIndex]}");
        SetInstruction("Click the correct denomination when ready.");
        RenderTrayStacks();
        UpdateSequenceDisplay();
        UpdatePackProgress();
    }

    private void AddPackedChip(int denominationIndex)
    {
        selectionHistory.Add(denominationIndex);
        packedCounts[denominationIndex]++;
        packedCount++;
        sequenceIndex = packedCount;
        SetStatus($"CHIP PACKED  •  {ChipNames[denominationIndex]}");
        SetInstruction("Keep packing the exact quantities shown. Click a tray chip to remove it.");
        RenderTrayStacks();
        UpdateSequenceDisplay();
        UpdatePackProgress();
        if (packedCount == totalRequired)
        {
            OnPackingComplete();
        }
    }

    private void OnPackingComplete()
    {
        if (!HasExactPackedContents())
        {
            SetStatus("PACKING MISMATCH");
            SetInstruction("The float does not match its reserved order; no chips were delivered.");
            Abandon();
            return;
        }

        if (selectedOrder != null)
        {
            SetStatus($"FLOAT PREPARED  •  {GetShortDestinationLabel(selectedOrder)}");
            SetInstruction("Float packed exactly. It is now on your tray; carry it to the named table to deliver.");
            Complete(true, 100 + totalRequired * 25);
            return;
        }

        cashOutReady = true;
        SetStatus($"CUSTOMER CASH-OUT READY  •  {FormatPounds(cashOutAmountPence)}");
        SetInstruction("Click FINISH! or press E.");
    }

    private void CompleteCashOut()
    {
        if (!cashOutReady)
        {
            return;
        }

        SetStatus($"CASH-OUT COMPLETE  •  {FormatPounds(cashOutAmountPence)}");
        Complete(true, 100 + totalRequired * 25);
    }

    private void UpdateSequenceDisplay()
    {
        if (sequenceText == null || nextChipText == null)
        {
            return;
        }

        if (cashOutMode)
        {
            int previewCount = Mathf.Min(4, packingSequence.Length - sequenceIndex);
            List<int> preview = new List<int>(previewCount);
            for (int i = 0; i < previewCount; i++)
                preview.Add(packingSequence[sequenceIndex + i]);
            sequenceText.text = previewCount > 0
                ? "COMING UP  •  " + BuildPackingSequenceDisplay(preview)
                : "ALL CHIPS PACKED!";
            sequenceText.fontSize = 19;
            nextChipText.text = sequenceIndex < packingSequence.Length
                ? $"NEXT  {ChipNames[packingSequence[sequenceIndex]]}  {FormatPounds(FloatDenominationsPence[packingSequence[sequenceIndex]])}"
                : "SEQUENCE COMPLETE";
            for (int i = 0; i < sourceChipImages.Length; i++)
            {
                if (sourceChipImages[i] != null)
                    sourceChipImages[i].color = sequenceIndex < packingSequence.Length && packingSequence[sequenceIndex] == i
                        ? new Color(0.42f, 0.36f, 0.12f, 1f)
                        : new Color(0.035f, 0.14f, 0.13f, 0.98f);
            }
            return;
        }

        sequenceText.text = BuildOrderRequirementsDisplay(requiredCounts);
        sequenceText.fontSize = 19;
        nextChipText.text = "DRAG CHIPS INTO THE FLOAT TRAY";
    }

    private static string BuildOrderRequirementsDisplay(IReadOnlyList<int> counts)
    {
        List<string> entries = new List<string>();
        for (int i = 0; i < counts.Count; i++)
        {
            if (counts[i] > 0)
            {
                entries.Add($"{ChipNames[i]}  {FormatPounds(FloatDenominationsPence[i])}  ×{counts[i]}");
            }
        }

        return string.Join("     •     ", entries);
    }

    private void RenderTrayStacks()
    {
        float[] stackX = { -75f, 145f, 365f, 585f };
        for (int denominationIndex = 0; denominationIndex < packedCounts.Length; denominationIndex++)
        {
            List<RectTransform> views = trayChipViews[denominationIndex];
            int visibleChipCount = packedCounts[denominationIndex];
            while (views.Count > visibleChipCount)
            {
                int last = views.Count - 1;
                Destroy(views[last].gameObject);
                views.RemoveAt(last);
            }

            while (views.Count < visibleChipCount)
            {
                int stackIndex = views.Count;
                views.Add(CreateChip(
                    $"Tray {ChipNames[denominationIndex]} Chip {stackIndex + 1}",
                    denominationIndex,
                    new Vector2(stackX[denominationIndex] + (stackIndex % 5 - 2) * 34f, -24f - (stackIndex / 5) * 42f),
                    30f,
                    removable: !cashOutMode));
            }

            if (stackCountTexts[denominationIndex] != null)
            {
                stackCountTexts[denominationIndex].text = $"{packedCounts[denominationIndex]}/{requiredCounts[denominationIndex]}";
            }
        }
    }

    private RectTransform CreateChip(
        string objectName,
        int denominationIndex,
        Vector2 position,
        float diameter,
        bool removable = false)
    {
        RectTransform chipRect = CreateRect(objectName, hud.transform, new Vector2(0.5f, 0.5f), position, Vector2.one * diameter);
        Image image = chipRect.gameObject.AddComponent<Image>();
        image.sprite = CasinoPixelArt.Chip;
        image.color = FloatChipColors[denominationIndex];
        image.raycastTarget = removable;
        if (removable)
        {
            chipRect.gameObject.AddComponent<CasinoPackedChipRemoveHandle>()
                .Configure(this, denominationIndex);
        }
        Outline outline = chipRect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.91f, 0.62f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);
        Text label = CreateText(
            $"{objectName} Value",
            FormatPounds(FloatDenominationsPence[denominationIndex]),
            position,
            new Vector2(diameter - 6f, diameter - 6f),
            9,
            Color.white);
        label.transform.SetParent(chipRect, false);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(3f, 3f);
        label.rectTransform.offsetMax = new Vector2(-3f, -3f);
        label.raycastTarget = false;
        return chipRect;
    }

    private void CreateHud()
    {
        hud = CasinoMinigameHud.Create("Cash Desk Float Packing", 80);
        hud.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1600f, 900f);
        CreateBackdrop();
        statusText = CreateText("Cash Desk Status", "PACK! CASINO FLOAT", new Vector2(0f, 372f), new Vector2(1200f, 58f), 34, new Color(1f, 0.82f, 0.41f));
        instructionText = CreateText("Cash Desk Instructions", "", new Vector2(0f, 325f), new Vector2(1400f, 42f), 20, Color.white);
    }

    private void CreateBackdrop()
    {
        RectTransform backdropRect = CreateRect("Packing Backdrop", hud.transform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;
        Image backdrop = backdropRect.gameObject.AddComponent<Image>();
        backdrop.color = new Color(0.015f, 0.065f, 0.075f, 0.28f);
        backdrop.raycastTarget = false;
        backdropRect.SetAsFirstSibling();
    }

    private void CreatePanel(string panelName, Vector2 position, Vector2 size)
    {
        RectTransform panel = CreateRect(panelName, hud.transform, new Vector2(0.5f, 0.5f), position, size);
        Image panelImage = panel.gameObject.AddComponent<Image>();
        CasinoHudStyle.Panel(panelImage, new Color(0.05f, 0.20f, 0.18f, 0.96f));
        panelImage.raycastTarget = false;
    }

    private Button CreateButton(string objectName, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rect = CreateRect(objectName, hud.transform, new Vector2(0.5f, 0.5f), position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.11f, 0.37f, 0.25f, 0.98f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        CasinoHudStyle.Button(button);
        button.onClick.AddListener(onClick);
        Text buttonText = CreateText($"{objectName} Label", label, Vector2.zero, size - new Vector2(16f, 10f), 22, Color.white);
        buttonText.transform.SetParent(rect, false);
        buttonText.rectTransform.anchorMin = Vector2.zero;
        buttonText.rectTransform.anchorMax = Vector2.one;
        buttonText.rectTransform.offsetMin = new Vector2(12f, 8f);
        buttonText.rectTransform.offsetMax = new Vector2(-12f, -8f);
        buttonText.alignment = TextAnchor.MiddleCenter;
        return button;
    }

    private Text CreateText(string objectName, string value, Vector2 position, Vector2 size, int fontSize, Color color)
    {
        Text text = CasinoMinigameHud.CreateText(hud.transform, objectName, position, size, fontSize);
        text.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        text.rectTransform.anchorMax = text.rectTransform.anchorMin;
        text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        text.rectTransform.anchoredPosition = position;
        text.rectTransform.sizeDelta = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.text = value;
        return text;
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

    private void UpdatePackProgress()
    {
        if (progressText != null)
        {
            progressText.text = $"CHIPS IN TRAY  {packedCount}/{totalRequired}";
        }

        RefreshTotals();
    }

    private void RefreshTotals()
    {
        if (totalText == null)
        {
            return;
        }

        int packedValuePence = 0;
        for (int i = 0; i < packedCounts.Length; i++)
        {
            packedValuePence += packedCounts[i] * FloatDenominationsPence[i];
        }

        int requiredValuePence = selectedOrder != null ? selectedOrder.TotalPence : cashOutAmountPence;
        totalText.text = $"TRAY VALUE  {FormatPounds(packedValuePence)} / ORDER  {FormatPounds(requiredValuePence)}  •  {timeRemaining:0.0}s";
    }

    private bool HasExactPackedContents()
    {
        if (selectedOrder != null)
        {
            return selectedOrder.HasExactContents(this);
        }

        for (int i = 0; i < requiredCounts.Length; i++)
        {
            if (packedCounts[i] != requiredCounts[i])
            {
                return false;
            }
        }

        return true;
    }

    private void CancelSelectedOrder()
    {
        if (selectedOrder != null && refillManager != null)
        {
            refillManager.CancelPacking(selectedOrder.Id);
        }
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
        if (player != null)
        {
            player.SetMovementEnabled(true);
        }

        Cursor.lockState = savedCursorLockMode;
        Cursor.visible = savedCursorVisible;
    }

    private void CleanUp()
    {
        if (hud != null)
        {
            Destroy(hud.gameObject);
        }

        hud = null;
        statusText = null;
        instructionText = null;
        totalText = null;
        progressText = null;
        sequenceText = null;
        nextChipText = null;
        floatTrayRect = null;
        for (int i = 0; i < chipButtons.Length; i++)
        {
            chipButtons[i] = null;
            sourceChipImages[i] = null;
            stackCountTexts[i] = null;
            trayChipViews[i].Clear();
        }
    }

    private void ClearDynamicLayout()
    {
        if (hud == null)
        {
            return;
        }

        List<GameObject> toRemove = new List<GameObject>();
        for (int i = 0; i < hud.transform.childCount; i++)
        {
            Transform child = hud.transform.GetChild(i);
            if (child != statusText?.transform && child != instructionText?.transform &&
                child.name != "Packing Backdrop")
            {
                toRemove.Add(child.gameObject);
            }
        }

        foreach (GameObject child in toRemove)
        {
            Destroy(child);
        }

        for (int i = 0; i < trayChipViews.Length; i++)
        {
            trayChipViews[i].Clear();
            stackCountTexts[i] = null;
            chipButtons[i] = null;
            sourceChipImages[i] = null;
        }
    }

    private static List<BlackjackChipDenomination> ConvertDenominations(IReadOnlyList<int> values)
    {
        if (values == null || values.Count == 0)
        {
            throw new System.ArgumentException("At least one chip denomination is required.", nameof(values));
        }

        List<BlackjackChipDenomination> result = new List<BlackjackChipDenomination>(values.Count);
        for (int i = 0; i < values.Count; i++)
        {
            result.Add(new BlackjackChipDenomination(
                FormatPounds(values[i]),
                values[i],
                FloatChipColors[GetFloatDenominationIndex(values[i])]));
        }

        return result;
    }

    private static int GetFloatDenominationIndex(int valuePence)
    {
        for (int i = 0; i < FloatDenominationsPence.Length; i++)
        {
            if (FloatDenominationsPence[i] == valuePence)
            {
                return i;
            }
        }

        throw new System.ArgumentException($"Unsupported chip denomination: {valuePence} pence.");
    }

    private static string GetShortDestinationLabel(CasinoFloatRefillOrder order)
    {
        return $"{order.DestinationId}  •  {order.Destination.DisplayName.ToUpperInvariant()}";
    }

    private static string FormatPounds(int amountPence)
    {
        return $"£{amountPence / 100}.{amountPence % 100:00}";
    }

}
