using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CashDeskPackMinigame : CasinoMinigame, IReadOnlyIntList
{
    private static Sprite chipSprite;
    private sealed class PackChip
    {
        public RectTransform Rect;
        public int DenominationIndex;
        public Vector2 Home;
        public bool Packed;
    }

    private static readonly int[] FloatDenominationsPence = { 500, 1000, 2500, 10000 };
    private static readonly int[] CustomerDenominationsPence = { 2500, 1000, 500 };
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
    private readonly List<PackChip> chips = new List<PackChip>();
    private readonly int[] requiredCounts = new int[4];
    private readonly int[] packedCounts = new int[4];
    private CasinoCashDeskInteraction refillManager;
    private CasinoFloatRefillOrder selectedOrder;
    private Canvas hud;
    private RectTransform canvasRect;
    private RectTransform rackRect;
    private RectTransform trayRect;
    private Text statusText;
    private Text instructionText;
    private Text totalText;
    private Text progressText;
    private PackChip heldChip;
    private int cashOutAmountPence = 4000;
    private int totalRequired;
    private int packedCount;
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
        "Choose a refill order, then drag the exact chips from the register into its tray.";

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
        cashOutReady = false;
        selectedOrder = null;
        heldChip = null;
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

        Mouse mouse = Mouse.current;
        if (mouse == null || hud == null || selectedOrder == null && !cashOutMode)
        {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                mouse.position.ReadValue(),
                null,
                out Vector2 pointer))
        {
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame && heldChip == null && !cashOutReady)
        {
            BeginChipDrag(pointer);
        }

        if (heldChip != null && mouse.leftButton.isPressed)
        {
            heldChip.Rect.anchoredPosition = pointer;
        }

        if (heldChip != null && mouse.leftButton.wasReleasedThisFrame)
        {
            PlaceChip(pointer);
        }
    }

    protected override void OnAbandon()
    {
        RestorePlayer();
        CleanUp();
    }

    protected override void OnComplete(bool success)
    {
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
        CreatePanel("Available Table Floats", new Vector2(0f, 72f), new Vector2(1040f, 500f));
        int columns = 2;
        for (int i = 0; i < availableOrders.Count; i++)
        {
            CasinoFloatRefillOrder order = availableOrders[i];
            int index = i;
            Vector2 position = new Vector2(
                (i % columns == 0 ? -250f : 250f),
                150f - (i / columns) * 145f);
            CreateButton(
                $"Float Order {order.Destination.DisplayName}",
                $"PACK FOR {order.Destination.DisplayName.ToUpperInvariant()}\n£{order.TotalPence / 100}  •  {order.TotalChips} CHIPS",
                position,
                new Vector2(440f, 108f),
                () => SelectFloatOrder(index));
        }

        CreateText("Order Instructions", "Select an order to reveal its chip-by-chip packing layout.",
            new Vector2(0f, -175f), new Vector2(900f, 55f), 20, Color.white);
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
            int denominationIndex = GetFloatDenominationIndex(composition[i]);
            requiredCounts[denominationIndex]++;
        }

        cashOutMode = true;
        selectedOrder = null;
        BuildPackingLayout();
        SetStatus($"CUSTOMER CASH-OUT  •  £{cashOutAmountPence / 100}.{cashOutAmountPence % 100:00}");
        SetInstruction("Drag the chips from the register into the green customer tray.");
    }

    private void StartPacking(int[] counts)
    {
        if (counts == null || counts.Length != requiredCounts.Length)
        {
            Complete(false, 0);
            return;
        }

        System.Array.Copy(counts, requiredCounts, counts.Length);
        cashOutMode = false;
        BuildPackingLayout();
        SetStatus($"PACK FOR {selectedOrder.Destination.DisplayName.ToUpperInvariant()}  •  £{selectedOrder.TotalPence / 100}");
        SetInstruction("Drag every listed chip into the highlighted tray. The order must match exactly.");
    }

    private void BuildPackingLayout()
    {
        ClearDynamicLayout();
        CreatePanel("Chip Rack", new Vector2(-405f, 10f), new Vector2(590f, 550f));
        rackRect = CreateRect("Rack Interior", hud.transform, new Vector2(0.5f, 0.5f), new Vector2(-405f, 0f), new Vector2(550f, 500f));
        Image rackImage = rackRect.gameObject.AddComponent<Image>();
        rackImage.color = new Color(0.04f, 0.13f, 0.14f, 0.92f);
        rackImage.raycastTarget = false;
        CreateText("Rack Label", "REGISTER RACK", new Vector2(-405f, 250f), new Vector2(480f, 40f), 21, new Color(1f, 0.83f, 0.44f));

        CreatePanel("Target Chip Tray", new Vector2(380f, 10f), new Vector2(700f, 550f));
        trayRect = CreateRect("Float Tray Target", hud.transform, new Vector2(0.5f, 0.5f), new Vector2(380f, -5f), new Vector2(650f, 440f));
        Image trayImage = trayRect.gameObject.AddComponent<Image>();
        trayImage.color = new Color(0.09f, 0.34f, 0.21f, 0.35f);
        Outline outline = trayRect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.47f, 1f, 0.6f, 0.95f);
        outline.effectDistance = new Vector2(4f, -4f);
        trayImage.raycastTarget = false;
        CreateText("Tray Label", "PACKING TRAY  •  DROP THE ORDERED CHIPS HERE",
            new Vector2(380f, 215f), new Vector2(620f, 50f), 20, new Color(0.63f, 1f, 0.7f));
        CreateText("Float Order Slip", BuildOrderSlip(),
            new Vector2(380f, 167f), new Vector2(600f, 45f), 17, Color.white);
        totalText = CreateText("Order Total", "", new Vector2(380f, -255f), new Vector2(620f, 42f), 22, Color.white);
        progressText = CreateText("Pack Progress", "", new Vector2(380f, -297f), new Vector2(620f, 38f), 18, new Color(1f, 0.84f, 0.48f));

        List<int> supplyValues = new List<int>();
        for (int denomination = 0; denomination < requiredCounts.Length; denomination++)
        {
            for (int count = 0; count < requiredCounts[denomination]; count++)
            {
                supplyValues.Add(FloatDenominationsPence[denomination]);
            }
        }

        totalRequired = supplyValues.Count;
        for (int i = 0; i < supplyValues.Count; i++)
        {
            int denominationIndex = GetFloatDenominationIndex(supplyValues[i]);
            int column = i % 4;
            int row = i / 4;
            Vector2 home = new Vector2(-180f + column * 120f, 160f - row * 100f);
            PackChip packChip = CreateChip(
                $"Supply Chip {i + 1}",
                denominationIndex,
                new Vector2(-405f, 0f) + home,
                90f);
            packChip.Home = new Vector2(-405f, 0f) + home;
            chips.Add(packChip);
        }

        UpdatePackProgress();
        CreateText("Rack Legend", "RED £5     BLUE £10     GREEN £25     BLACK £100",
            new Vector2(-405f, -264f), new Vector2(530f, 40f), 16, Color.white);
        if (cashOutMode)
        {
            CreateButton(
                "Finish Cash Out",
                "FINISH CUSTOMER CASH-OUT",
                new Vector2(380f, -360f),
                new Vector2(520f, 70f),
                CompleteCashOut);
        }
    }

    private void BeginChipDrag(Vector2 pointer)
    {
        for (int i = chips.Count - 1; i >= 0; i--)
        {
            PackChip chip = chips[i];
            if (chip.Packed)
            {
                continue;
            }

            if (Vector2.Distance(pointer, chip.Rect.anchoredPosition) <= 57f)
            {
                heldChip = chip;
                chip.Rect.SetAsLastSibling();
                SetInstruction($"Dragging {FormatPounds(FloatDenominationsPence[chip.DenominationIndex])}. Drop it inside the tray.");
                return;
            }
        }
    }

    private void PlaceChip(Vector2 pointer)
    {
        PackChip chip = heldChip;
        heldChip = null;
        if (chip == null)
        {
            return;
        }

        if (!RectTransformUtility.RectangleContainsScreenPoint(trayRect, Mouse.current.position.ReadValue(), null))
        {
            chip.Rect.anchoredPosition = chip.Home;
            SetInstruction("Missed the tray. The chip is back on the rack; try again.");
            return;
        }

        if (packedCounts[chip.DenominationIndex] >= requiredCounts[chip.DenominationIndex])
        {
            chip.Rect.anchoredPosition = chip.Home;
            SetInstruction($"The order needs no more {FormatPounds(FloatDenominationsPence[chip.DenominationIndex])} chips.");
            return;
        }

        chip.Packed = true;
        packedCounts[chip.DenominationIndex]++;
        chip.Rect.anchoredPosition = GetPackedPosition(chip);
        chip.Rect.localScale = Vector3.one * 0.86f;
        packedCount++;
        UpdatePackProgress();
        if (packedCount == totalRequired)
        {
            OnPackingComplete();
        }
        else
        {
            SetInstruction("Correct chip. Match the remaining quantities shown on the order slip.");
        }
    }

    private void OnPackingComplete()
    {
        if (selectedOrder != null)
        {
            SetStatus($"FLOAT PREPARED  •  {selectedOrder.Destination.DisplayName.ToUpperInvariant()}");
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

    private void UpdatePackProgress()
    {
        if (totalText != null)
        {
            int totalPence = selectedOrder != null ? selectedOrder.TotalPence : cashOutAmountPence;
            totalText.text = $"ORDER TOTAL  {FormatPounds(totalPence)}";
        }

        if (progressText != null)
        {
            progressText.text = $"CHIPS IN TRAY  {packedCount}/{totalRequired}";
        }
    }

    private Vector2 GetPackedPosition(PackChip chip)
    {
        int denominationIndex = chip.DenominationIndex;
        int withinType = packedCounts[denominationIndex] - 1;
        int precedingTypes = 0;
        for (int i = 0; i < denominationIndex; i++)
        {
            precedingTypes += packedCounts[i];
        }

        int index = precedingTypes + withinType;
        return new Vector2(130f + (index % 5) * 110f, 160f - (index / 5) * 100f);
    }

    private PackChip CreateChip(string objectName, int denominationIndex, Vector2 position, float diameter)
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
            new Vector2(diameter - 12f, diameter - 12f),
            denominationIndex == 3 ? 18 : 20,
            Color.white);
        label.transform.SetParent(chipRect, false);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(4f, 4f);
        labelRect.offsetMax = new Vector2(-4f, -4f);
        label.raycastTarget = false;
        return new PackChip { Rect = chipRect, DenominationIndex = denominationIndex, Home = position };
    }

    private void CreateHud()
    {
        hud = CasinoMinigameHud.Create("Cash Desk Float Packing", 80);
        hud.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1600f, 900f);
        canvasRect = hud.GetComponent<RectTransform>();
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
        backdrop.color = new Color(0.015f, 0.065f, 0.075f, 0.96f);
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
        CreateText($"{objectName} Label", label, Vector2.zero, size - new Vector2(16f, 10f), 22, Color.white)
            .transform.SetParent(rect, false);
        Text buttonText = rect.GetComponentInChildren<Text>();
        RectTransform buttonTextRect = buttonText.rectTransform;
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.offsetMin = new Vector2(12f, 8f);
        buttonTextRect.offsetMax = new Vector2(-12f, -8f);
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
        chips.Clear();
        heldChip = null;
        if (hud != null)
        {
            Destroy(hud.gameObject);
        }

        hud = null;
        canvasRect = null;
        rackRect = null;
        trayRect = null;
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

        chips.Clear();
        heldChip = null;
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

    private string BuildOrderSlip()
    {
        string[] colors = { "RED", "BLUE", "GREEN", "BLACK" };
        System.Text.StringBuilder text = new System.Text.StringBuilder("ORDER  ");
        for (int i = 0; i < requiredCounts.Length; i++)
        {
            if (requiredCounts[i] == 0)
            {
                continue;
            }

            if (text.Length > 7)
            {
                text.Append("   •   ");
            }

            text.Append(requiredCounts[i]).Append(' ').Append(colors[i]);
        }

        return text.ToString();
    }
}
