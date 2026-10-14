using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CashDeskPackMinigame : CasinoMinigame, IReadOnlyIntList
{
    private static Sprite chipSprite;
    private static readonly int[] FloatDenominationsPence = { 500, 250, 2500, 10000 };
    private static readonly int[] CustomerDenominationsPence = { 2500, 250, 500 };
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
    private int cashOutAmountPence = 4000;
    private int totalRequired;
    private int packedCount;
    private int sequenceIndex;
    private int[] packingSequence = System.Array.Empty<int>();
    private float timeRemaining;
    private bool cashOutMode;
    private bool cashOutReady;
    private CursorLockMode savedCursorLockMode;
    private bool savedCursorVisible;

    public override bool CanBegin => IsConfigured && Mouse.current != null;
    public bool IsConfigured => player != null && counterRoot != null;
    public int PackAmountPence => selectedOrder != null ? selectedOrder.TotalPence : cashOutAmountPence;
    public int PackedChipCount => packedCount;
    public bool IsReadyToDeliver => cashOutReady;
    public CasinoFloatRefillOrder PackedOrder => selectedOrder;
    public IReadOnlyIntList PackedCounts => this;
    public int Count => packedCounts.Length;
    public int this[int index] => index >= 0 && index < packedCounts.Length ? packedCounts[index] : 0;

    protected override string PresentationTitle => "PACK! BUILD THE CASINO FLOAT";
    protected override string PresentationInstructions =>
        "Choose each exact denomination in order to build the float quickly.";

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
        return Mathf.Clamp(12f + chipCount * 1.25f, 15f, 50f);
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
        if (amountPence <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(amountPence));
        }

        availableOrders.Clear();
        selectedOrder = null;
        cashOutAmountPence = amountPence;
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

        if (packingSequence.Length == 0 || cashOutReady)
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
        SetStatus("FLOAT REQUESTS  •  SELECT A DESTINATION");
        SetInstruction("Each order is reserved at the register and must be delivered to its named table.");
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

        CreateText("Order Instructions", "Each order has its own destination and exact chip count.",
            new Vector2(0f, -245f), new Vector2(900f, 55f), 20, Color.white);
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
        int[] composition = GetExactChipComposition(cashOutAmountPence, CustomerDenominationsPence);
        for (int i = 0; i < composition.Length; i++)
        {
            requiredCounts[GetFloatDenominationIndex(composition[i])]++;
        }

        cashOutMode = true;
        selectedOrder = null;
        BuildPackingLayout();
        SetStatus($"CUSTOMER CASH-OUT  •  {FormatPounds(cashOutAmountPence)}");
        SetInstruction("Click the next required denomination. Incorrect choices cost time but can be retried.");
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
        SetInstruction("Click the next required denomination. Incorrect choices cost time but can be retried.");
    }

    private void BuildPackingLayout()
    {
        ClearDynamicLayout();
        CreatePanel("Register Chip Choices", new Vector2(-515f, -5f), new Vector2(500f, 560f));
        CreateText("Rack Label", "REGISTER  •  CLICK A CHIP", new Vector2(-515f, 235f), new Vector2(460f, 38f), 21, new Color(1f, 0.83f, 0.44f));
        float[] chipX = { -625f, -405f, -625f, -405f };
        float[] chipY = { 95f, 95f, -120f, -120f };
        for (int i = 0; i < chipButtons.Length; i++)
        {
            int denominationIndex = SourceDisplayOrder[i];
            chipButtons[i] = CreateChipButton(denominationIndex, new Vector2(chipX[i], chipY[i]), () => SelectChip(denominationIndex));
        }

        CreatePanel("Target Chip Tray", new Vector2(260f, -5f), new Vector2(990f, 590f));
        RectTransform trayRect = CreateRect("Float Tray Target", hud.transform, new Vector2(0.5f, 0.5f), new Vector2(260f, -5f), new Vector2(950f, 545f));
        Image trayImage = trayRect.gameObject.AddComponent<Image>();
        trayImage.color = new Color(0.09f, 0.34f, 0.21f, 0.56f);
        Outline outline = trayRect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.47f, 1f, 0.6f, 0.95f);
        outline.effectDistance = new Vector2(4f, -4f);
        trayImage.raycastTarget = false;
        CreateText("Tray Label", "PACKING TRAY  •  MATCH THE SHOWN SEQUENCE",
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
                new Vector2(stackX[i], 22f), new Vector2(205f, 30f), 16, FloatChipColors[i]);
            stackCountTexts[i] = CreateText($"Tray {ChipNames[i]} Count", "0/0",
                new Vector2(stackX[i], -145f), new Vector2(190f, 28f), 16, Color.white);
        }

        totalRequired = 0;
        for (int i = 0; i < requiredCounts.Length; i++)
        {
            totalRequired += requiredCounts[i];
        }

        packingSequence = BuildPackingSequence(requiredCounts, Random.Range(0, int.MaxValue));
        timeRemaining = GetSequenceTimeLimit(totalRequired);
        sequenceIndex = 0;
        UpdateSequenceDisplay();
        RenderTrayStacks();
        UpdatePackProgress();
        if (cashOutMode)
        {
            CreateButton("Finish Cash Out", "FINISH CUSTOMER CASH-OUT",
                new Vector2(260f, -282f), new Vector2(430f, 48f), CompleteCashOut);
        }
    }

    private Button CreateChipButton(int denominationIndex, Vector2 position, UnityEngine.Events.UnityAction action)
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
        button.onClick.AddListener(action);

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
        chipImage.sprite = GetChipSprite();
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
        return button;
    }

    private void SelectChip(int denominationIndex)
    {
        if (!IsRunning || cashOutReady || sequenceIndex >= packingSequence.Length)
        {
            return;
        }

        if (!TryAdvancePackingSequence(packingSequence, sequenceIndex, denominationIndex, out int nextIndex))
        {
            timeRemaining = Mathf.Max(0f, timeRemaining - 1.5f);
            int expectedDenomination = packingSequence[sequenceIndex];
            SetStatus($"WRONG CHIP  •  {timeRemaining:0.0}s LEFT");
            SetInstruction($"That order needs {ChipNames[expectedDenomination]}. Try again.");
            RefreshTotals();
            return;
        }

        packedCounts[denominationIndex]++;
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
        SetInstruction("Click FINISH CUSTOMER CASH-OUT or press E.");
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

        sequenceText.text = BuildPackingSequenceDisplay(packingSequence);
        sequenceText.fontSize = packingSequence.Length > 10 ? 14 : 17;
        nextChipText.text = sequenceIndex < packingSequence.Length
            ? $"NEXT  {ChipNames[packingSequence[sequenceIndex]]}  {FormatPounds(FloatDenominationsPence[packingSequence[sequenceIndex]])}"
            : "SEQUENCE COMPLETE";
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
                    30f));
            }

            if (stackCountTexts[denominationIndex] != null)
            {
                stackCountTexts[denominationIndex].text = $"{packedCounts[denominationIndex]}/{requiredCounts[denominationIndex]}";
            }
        }
    }

    private RectTransform CreateChip(string objectName, int denominationIndex, Vector2 position, float diameter)
    {
        RectTransform chipRect = CreateRect(objectName, hud.transform, new Vector2(0.5f, 0.5f), position, Vector2.one * diameter);
        Image image = chipRect.gameObject.AddComponent<Image>();
        image.sprite = GetChipSprite();
        image.color = FloatChipColors[denominationIndex];
        image.raycastTarget = false;
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
        panelImage.color = new Color(0.035f, 0.15f, 0.15f, 0.92f);
        panelImage.raycastTarget = false;
        Outline outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.8f, 0.59f, 0.25f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);
    }

    private Button CreateButton(string objectName, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rect = CreateRect(objectName, hud.transform, new Vector2(0.5f, 0.5f), position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.11f, 0.37f, 0.25f, 0.98f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
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
        for (int i = 0; i < chipButtons.Length; i++)
        {
            chipButtons[i] = null;
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

    private static Sprite GetChipSprite()
    {
        if (chipSprite != null)
        {
            return chipSprite;
        }

        const int resolution = 64;
        Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        texture.name = "Casino Float Chip Sprite";
        texture.filterMode = FilterMode.Bilinear;
        Vector2 center = new Vector2((resolution - 1) * 0.5f, (resolution - 1) * 0.5f);
        float radius = resolution * 0.48f;
        Color[] pixels = new Color[resolution * resolution];
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                pixels[y * resolution + x] = distance <= radius
                    ? distance >= radius - 4f || distance <= radius * 0.55f
                        ? Color.white
                        : Color.clear
                    : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        chipSprite = Sprite.Create(texture, new Rect(0f, 0f, resolution, resolution), new Vector2(0.5f, 0.5f), resolution);
        return chipSprite;
    }
}
