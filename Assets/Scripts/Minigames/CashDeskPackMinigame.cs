using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CashDeskPackMinigame : CasinoMinigame
{
    [SerializeField] private CasinoPlayerController player;
    [SerializeField] private Transform counterRoot;
    [SerializeField, Min(1)] private int packAmountPence = 4000;
    [SerializeField] private BlackjackChipDenomination[] denominations = BlackjackChipDenomination.CreateDefaults();

    private readonly List<GameObject> supplyChips = new List<GameObject>();
    private readonly List<GameObject> packedChips = new List<GameObject>();
    private readonly Dictionary<GameObject, int> chipValues = new Dictionary<GameObject, int>();
    private int[] expectedChips;
    private Canvas hud;
    private Text statusText;
    private Text instructionText;
    private Material chipMaterial;
    private GameObject heldChip;
    private int heldValue;
    private int nextChipIndex;
    private Vector3 pointerWorld;
    private Vector3 heldHomePosition;
    private CursorLockMode savedCursorLockMode;
    private bool savedCursorVisible;
    private bool deliveryReady;

    public bool IsConfigured => player != null && counterRoot != null &&
                                denominations != null && denominations.Length > 0;
    public override bool CanBegin => IsConfigured && Mouse.current != null && Camera.main != null;
    public int PackAmountPence => packAmountPence;
    public int PackedChipCount => nextChipIndex;
    public bool IsReadyToDeliver => deliveryReady;

    public void Configure(CasinoPlayerController casinoPlayer, Transform root)
    {
        player = casinoPlayer;
        counterRoot = root;
    }

    public void SetPackAmount(int amountPence)
    {
        if (amountPence <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(amountPence));
        }

        packAmountPence = amountPence;
    }

    public static int[] GetExactChipComposition(int amountPence, IReadOnlyList<int> chipDenominations)
    {
        if (amountPence <= 0 || chipDenominations == null || chipDenominations.Count == 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(amountPence));
        }

        List<int> orderedDenominations = new List<int>(chipDenominations.Count);
        for (int i = 0; i < chipDenominations.Count; i++)
        {
            orderedDenominations.Add(chipDenominations[i]);
        }

        orderedDenominations.Sort((left, right) => right.CompareTo(left));
        List<int> result = new List<int>();
        int remaining = amountPence;
        for (int i = 0; i < orderedDenominations.Count; i++)
        {
            int denomination = orderedDenominations[i];
            if (denomination <= 0)
            {
                throw new System.ArgumentException("Chip denominations must be positive.", nameof(chipDenominations));
            }

            while (remaining >= denomination)
            {
                remaining -= denomination;
                result.Add(denomination);
            }
        }

        if (remaining != 0)
        {
            throw new System.InvalidOperationException($"The chip denominations cannot represent {amountPence} pence exactly.");
        }

        return result.ToArray();
    }

    protected override void OnBegin()
    {
        if (!IsConfigured || Mouse.current == null || Camera.main == null)
        {
            Complete(false, 0);
            return;
        }

        List<int> denominationValues = new List<int>(denominations.Length);
        foreach (BlackjackChipDenomination denomination in denominations)
        {
            denominationValues.Add(denomination.ValuePence);
        }

        expectedChips = GetExactChipComposition(packAmountPence, denominationValues);
        nextChipIndex = 0;
        deliveryReady = false;
        heldChip = null;
        savedCursorLockMode = Cursor.lockState;
        savedCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        player.SetMovementEnabled(false);
        chipMaterial = CreateMaterial();
        CreateHud();
        CreateCounterVisuals();
        CreateSupplyChips();
        UpdateInstructions();
    }

    protected override void OnTick(float deltaTime)
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Abandon();
            return;
        }

        if (!TryGetPointerPosition(out pointerWorld))
        {
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame && heldChip == null && !deliveryReady)
        {
            TryPickUpChip();
        }

        if (heldChip != null && mouse.leftButton.isPressed)
        {
            heldChip.transform.position = pointerWorld + Vector3.up * 0.08f;
        }

        if (heldChip != null && mouse.leftButton.wasReleasedThisFrame)
        {
            PlaceChip();
        }

        if (deliveryReady && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            SetStatus($"DELIVERED  •  £{packAmountPence / 100}.{packAmountPence % 100:00}");
            SetInstruction("The chip pack has been handed to the customer.");
            Complete(true, 100 + expectedChips.Length * 25);
        }
    }

    protected override void OnAbandon()
    {
        RestorePlayer();
        CleanUp();
    }

    private void TryPickUpChip()
    {
        Vector3 localPointer = counterRoot.InverseTransformPoint(pointerWorld);
        for (int i = 0; i < supplyChips.Count; i++)
        {
            GameObject chip = supplyChips[i];
            if (chip == null || packedChips.Contains(chip))
            {
                continue;
            }

            Vector3 localChip = chip.transform.localPosition;
            if (new Vector2(localPointer.x - localChip.x, localPointer.z - localChip.z).sqrMagnitude <= 0.42f * 0.42f)
            {
                heldChip = chip;
                heldValue = chipValues[chip];
                heldHomePosition = chip.transform.position;
                SetInstruction($"Drag the {_FormatPounds(heldValue)} chip into the highlighted pack tray.");
                return;
            }
        }
    }

    private void PlaceChip()
    {
        Vector3 localPointer = counterRoot.InverseTransformPoint(pointerWorld);
        bool insideTray = localPointer.x >= 1.12f && localPointer.x <= 2.0f &&
                          localPointer.z >= -0.35f && localPointer.z <= 0.55f;
        int expectedValue = expectedChips[nextChipIndex];
        if (!insideTray)
        {
            heldChip.transform.position = heldHomePosition;
            SetInstruction("Missed the pack tray. Pick the chip up and try again.");
            heldChip = null;
            return;
        }

        if (heldValue != expectedValue)
        {
            heldChip.transform.position = heldHomePosition;
            SetInstruction($"Wrong denomination. The next chip must be {_FormatPounds(expectedValue)}.");
            heldChip = null;
            return;
        }

        heldChip.transform.localPosition = GetPackedChipPosition(nextChipIndex);
        packedChips.Add(heldChip);
        heldChip = null;
        nextChipIndex++;
        if (nextChipIndex >= expectedChips.Length)
        {
            deliveryReady = true;
            SetStatus($"PACK READY  •  £{packAmountPence / 100}.{packAmountPence % 100:00}");
            SetInstruction("Press E to physically deliver the completed chip pack.");
        }
        else
        {
            UpdateInstructions();
        }
    }

    private void UpdateInstructions()
    {
        SetStatus($"PACK CUSTOMER CHIPS  •  {_FormatPounds(packAmountPence)}");
        SetInstruction($"Drag the next {_FormatPounds(expectedChips[nextChipIndex])} chip into the highlighted tray.");
    }

    private void CreateCounterVisuals()
    {
        GameObject tray = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tray.name = "Customer Chip Pack Tray";
        tray.transform.SetParent(counterRoot, false);
        tray.transform.localPosition = new Vector3(1.55f, 1.39f, 0.1f);
        tray.transform.localScale = new Vector3(0.9f, 0.04f, 0.95f);
        SetColor(tray.GetComponent<Renderer>(), new Color(0.18f, 0.76f, 0.39f, 0.72f));
        Destroy(tray.GetComponent<Collider>());
        packedChips.Add(tray);
    }

    private void CreateSupplyChips()
    {
        for (int i = 0; i < expectedChips.Length; i++)
        {
            int value = expectedChips[i];
            GameObject chip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            chip.name = $"Supply Chip {_FormatPounds(value)}";
            chip.transform.SetParent(counterRoot, false);
            chip.transform.localScale = new Vector3(0.3f, 0.055f, 0.3f);
            chip.transform.localPosition = new Vector3(-1.45f + (i % 3) * 0.62f, 1.39f + i * 0.01f, -0.15f - (i / 3) * 0.55f);
            chip.GetComponent<Renderer>().sharedMaterial = chipMaterial;
            SetColor(chip.GetComponent<Renderer>(), FindDenomination(value).Color);
            Destroy(chip.GetComponent<Collider>());
            supplyChips.Add(chip);
            chipValues.Add(chip, value);
        }
    }

    private Vector3 GetPackedChipPosition(int index)
    {
        return new Vector3(
            1.25f + (index % 2) * 0.38f,
            1.43f + index * 0.014f,
            -0.22f + (index / 2) * 0.38f);
    }

    private BlackjackChipDenomination FindDenomination(int value)
    {
        foreach (BlackjackChipDenomination denomination in denominations)
        {
            if (denomination.ValuePence == value)
            {
                return denomination;
            }
        }

        throw new System.InvalidOperationException($"No chip denomination is configured for {value} pence.");
    }

    private bool TryGetPointerPosition(out Vector3 worldPosition)
    {
        worldPosition = default;
        if (Mouse.current == null || Camera.main == null)
        {
            return false;
        }

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane plane = new Plane(Vector3.up, new Vector3(0f, counterRoot.position.y + 1.34f, 0f));
        if (!plane.Raycast(ray, out float distance))
        {
            return false;
        }

        worldPosition = ray.GetPoint(distance);
        return true;
    }

    private void CreateHud()
    {
        hud = CasinoMinigameHud.Create("Cash Desk Pack HUD", 70);
        statusText = CasinoMinigameHud.CreateText(hud.transform, "Cash Desk Status", new Vector2(0f, -36f), new Vector2(800f, 42f), 24);
        instructionText = CasinoMinigameHud.CreateText(hud.transform, "Cash Desk Instructions", new Vector2(0f, -82f), new Vector2(900f, 40f), 19);
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
        foreach (GameObject chip in supplyChips)
        {
            if (chip != null)
            {
                Destroy(chip);
            }
        }

        foreach (GameObject chip in packedChips)
        {
            if (chip != null)
            {
                Destroy(chip);
            }
        }

        supplyChips.Clear();
        packedChips.Clear();
        chipValues.Clear();
        if (heldChip != null)
        {
            Destroy(heldChip);
            heldChip = null;
        }

        if (hud != null)
        {
            Destroy(hud.gameObject);
            hud = null;
        }

        if (chipMaterial != null)
        {
            Destroy(chipMaterial);
            chipMaterial = null;
        }
    }

    private static Material CreateMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null)
        {
            throw new System.InvalidOperationException("Cash desk could not find a supported material shader.");
        }

        return new Material(shader);
    }

    private static void SetColor(Renderer renderer, Color color)
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        renderer.SetPropertyBlock(block);
    }

    private static string _FormatPounds(int amountPence)
    {
        return $"£{amountPence / 100}.{amountPence % 100:00}";
    }

}
