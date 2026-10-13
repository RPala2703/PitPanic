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

    [SerializeField, Min(1)] private int chipsToSweep = 3;
    [SerializeField, Min(0.1f)] private float spinSeconds = 1.4f;
    [SerializeField] private CasinoPlayerController player;
    [SerializeField] private Transform tableRoot;
    [SerializeField] private Transform wheel;
    [SerializeField] private CasinoTableAttention attention;

    private readonly List<GameObject> sweepChips = new List<GameObject>();
    private Canvas hud;
    private Text statusText;
    private Text instructionText;
    private Material chipMaterial;
    private Vector3 pointerWorld;
    private Vector2 previousPointer;
    private GameObject heldChip;
    private int winningNumber;
    private int sweptCount;
    private float spinTimer;
    private float angleDragged;
    private bool isDragging;
    private bool wheelAnimating;
    private bool sweepAttentionRequested;
    private CursorLockMode savedCursorLockMode;
    private bool savedCursorVisible;

    public bool IsConfigured => player != null && tableRoot != null && wheel != null && attention != null;
    public override bool CanBegin => IsConfigured && Mouse.current != null && Camera.main != null;
    protected override string PresentationTitle => "SPIN! THEN SWEEP THE TABLE";
    protected override string PresentationInstructions =>
        "Drag to spin the live roulette wheel, then clear every loose chip into the dealer tray.";
    public int WinningNumber => winningNumber;
    public int SweptChipCount => sweptCount;
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

    public void Configure(
        CasinoPlayerController casinoPlayer,
        Transform root,
        Transform wheelTransform,
        CasinoTableAttention tableAttention)
    {
        player = casinoPlayer;
        tableRoot = root;
        wheel = wheelTransform;
        attention = tableAttention;
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

    protected override void OnBegin()
    {
        if (!IsConfigured || Mouse.current == null || Camera.main == null)
        {
            Complete(false, 0);
            return;
        }

        savedCursorLockMode = Cursor.lockState;
        savedCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        player.SetMovementEnabled(false);
        sweptCount = 0;
        sweepAttentionRequested = false;
        wheelAnimating = false;
        isDragging = false;
        heldChip = null;
        wheel.localRotation = Quaternion.identity;
        chipMaterial = CreateMaterial();
        CreateHud();
        SetStatus("ROULETTE SERVICE");
        SetInstruction("Click and drag across the wheel to SPIN.");
    }

    protected override void OnTick(float deltaTime)
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Abandon();
            return;
        }

        if (wheelAnimating)
        {
            spinTimer -= deltaTime;
            wheel.Rotate(Vector3.up, 850f * deltaTime, Space.Self);
            if (spinTimer <= 0f)
            {
                wheelAnimating = false;
                EnterSweepStage();
            }

            return;
        }

        if (!TryGetPointerPosition(out pointerWorld))
        {
            return;
        }

        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                BeginPointerAction(Mouse.current.position.ReadValue());
            }

            if (isDragging && Mouse.current.leftButton.isPressed)
            {
                UpdatePointerAction(Mouse.current.position.ReadValue());
            }

            if (isDragging && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                EndPointerAction();
            }
        }
    }

    protected override void OnAbandon()
    {
        ResolveSweepAttention();
        RestorePlayer();
        CleanUp();
    }

    protected override void OnComplete(bool success)
    {
        ResolveSweepAttention();
        RestorePlayer();
        CleanUp();
    }

    private void BeginPointerAction(Vector2 screenPosition)
    {
        if (stage == RouletteStage.Spin)
        {
            Vector3 local = tableRoot.InverseTransformPoint(pointerWorld);
            if (new Vector2(local.x, local.z).sqrMagnitude <= 1.3f * 1.3f)
            {
                isDragging = true;
                angleDragged = 0f;
                previousPointer = screenPosition;
                SetInstruction("Keep dragging across the wheel, then release to SPIN.");
            }

            return;
        }

        if (stage != RouletteStage.Sweep || heldChip != null)
        {
            return;
        }

        GameObject chip = FindChipAt(pointerWorld);
        if (chip != null)
        {
            heldChip = chip;
            isDragging = true;
            previousPointer = screenPosition;
            SetInstruction("Drag each chip into the highlighted dealer tray.");
        }
    }

    private void UpdatePointerAction(Vector2 screenPosition)
    {
        if (stage == RouletteStage.Spin)
        {
            float horizontalDelta = screenPosition.x - previousPointer.x;
            angleDragged += Mathf.Abs(horizontalDelta);
            wheel.Rotate(Vector3.up, horizontalDelta * 1.2f, Space.Self);
            previousPointer = screenPosition;
            return;
        }

        if (stage == RouletteStage.Sweep && heldChip != null)
        {
            heldChip.transform.position = pointerWorld + Vector3.up * 0.06f;
        }
    }

    private void EndPointerAction()
    {
        isDragging = false;
        if (stage == RouletteStage.Spin)
        {
            if (angleDragged < 45f)
            {
                SetInstruction("Give the wheel a longer drag, then release.");
                return;
            }

            winningNumber = Random.Range(0, 37);
            stage = RouletteStage.Spinning;
            spinTimer = spinSeconds;
            wheelAnimating = true;
            SetStatus($"SPINNING  •  BETS SETTLE ON {winningNumber}");
            return;
        }

        if (stage == RouletteStage.Sweep && heldChip != null)
        {
            Vector3 local = tableRoot.InverseTransformPoint(pointerWorld);
            if (IsInsideDealerTray(local))
            {
                heldChip.transform.localPosition = GetTrayChipPosition(sweptCount);
                sweptCount++;
                if (sweptCount >= chipsToSweep)
                {
                    SetStatus($"TABLE CLEAR  •  RESULT {winningNumber}");
                    SetInstruction("Roulette round serviced.");
                    ResolveSweepAttention();
                    Complete(true, 100 + sweptCount * 25);
                }
                else
                {
                    SetStatus($"SWEEP CHIPS  •  {sweptCount}/{chipsToSweep}");
                    SetInstruction("Drag the next chip into the highlighted dealer tray.");
                }
            }
            else
            {
                heldChip.transform.localPosition = FindHomePosition(heldChip);
                SetInstruction("Chip missed the tray. Pick it up and try again.");
            }

            heldChip = null;
        }
    }

    private void EnterSweepStage()
    {
        stage = RouletteStage.Sweep;
        sweepAttentionRequested = attention != null &&
            attention.RequestAttentionOnce(TableAttentionReason.RouletteSweepRequired);
        CreateSweepChips();
        SetStatus($"WINNING POCKET  •  {winningNumber}  ({GetPocketColorName(winningNumber)})");
        SetInstruction("Drag all three chips into the glowing tray to SWEEP.");
    }

    private void ResolveSweepAttention()
    {
        if (sweepAttentionRequested && attention != null)
        {
            attention.ResolveAttention(TableAttentionReason.RouletteSweepRequired);
            sweepAttentionRequested = false;
        }
    }

    private void CreateSweepChips()
    {
        sweepChips.Clear();
        GameObject tray = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tray.name = "Roulette Dealer Chip Tray";
        tray.transform.SetParent(tableRoot, false);
        tray.transform.localPosition = new Vector3(1.72f, 1.59f, 0.55f);
        tray.transform.localScale = new Vector3(0.72f, 0.035f, 0.92f);
        tray.GetComponent<Renderer>().sharedMaterial = chipMaterial;
        SetColor(tray.GetComponent<Renderer>(), new Color(0.92f, 0.76f, 0.22f, 0.78f));
        Destroy(tray.GetComponent<Collider>());
        sweepChips.Add(tray);

        for (int i = 0; i < chipsToSweep; i++)
        {
            GameObject chip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            chip.name = $"Roulette Chip {i + 1}";
            chip.transform.SetParent(tableRoot, false);
            chip.transform.localScale = new Vector3(0.26f, 0.055f, 0.26f);
            chip.GetComponent<Renderer>().sharedMaterial = chipMaterial;
            SetColor(chip.GetComponent<Renderer>(), i % 2 == 0
                ? new Color(0.88f, 0.16f, 0.12f)
                : new Color(0.13f, 0.16f, 0.20f));
            Vector3 position = GetChipScatterPosition(i);
            chip.transform.localPosition = position;
            sweepChips.Add(chip);
        }
    }

    private Vector3 GetChipScatterPosition(int index)
    {
        float[] xPositions = { -1.55f, 0.05f, 1.15f };
        float[] zPositions = { -0.7f, 1.0f, -0.8f };
        return new Vector3(xPositions[index % xPositions.Length], 1.59f, zPositions[index % zPositions.Length]);
    }

    private Vector3 FindHomePosition(GameObject chip)
    {
        int index = sweepChips.IndexOf(chip) - 1;
        return GetChipScatterPosition(Mathf.Max(0, index));
    }

    private Vector3 GetTrayChipPosition(int index)
    {
        return new Vector3(1.52f + (index % 2) * 0.32f, 1.64f + index * 0.025f, 0.25f + (index / 2) * 0.3f);
    }

    private GameObject FindChipAt(Vector3 worldPosition)
    {
        Vector3 local = tableRoot.InverseTransformPoint(worldPosition);
        for (int i = 1; i < sweepChips.Count; i++)
        {
            GameObject chip = sweepChips[i];
            if (chip == null || chip == heldChip)
            {
                continue;
            }

            Vector3 position = chip.transform.localPosition;
            if (new Vector2(local.x - position.x, local.z - position.z).sqrMagnitude <= 0.38f * 0.38f)
            {
                return chip;
            }
        }

        return null;
    }

    private static bool IsInsideDealerTray(Vector3 local)
    {
        return local.x >= 1.3f && local.x <= 2.12f &&
               local.z >= 0.02f && local.z <= 1.05f;
    }

    private bool TryGetPointerPosition(out Vector3 worldPosition)
    {
        worldPosition = default;
        if (Mouse.current == null || Camera.main == null)
        {
            return false;
        }

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane plane = new Plane(Vector3.up, new Vector3(0f, tableRoot.position.y + 1.53f, 0f));
        if (!plane.Raycast(ray, out float distance))
        {
            return false;
        }

        worldPosition = ray.GetPoint(distance);
        return true;
    }

    private void CreateHud()
    {
        hud = CasinoMinigameHud.Create("Roulette Service HUD", 70);
        hud.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1600f, 900f);
        statusText = CasinoMinigameHud.CreateText(hud.transform, "Roulette Status", new Vector2(0f, -44f), new Vector2(1100f, 54f), 30);
        instructionText = CasinoMinigameHud.CreateText(hud.transform, "Roulette Instructions", new Vector2(0f, -100f), new Vector2(1250f, 48f), 22);
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
        foreach (GameObject chip in sweepChips)
        {
            if (chip != null)
            {
                Destroy(chip);
            }
        }

        sweepChips.Clear();
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
            throw new System.InvalidOperationException("Roulette could not find a supported material shader.");
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

    private static string GetPocketColorName(int number)
    {
        if (number == 0)
        {
            return "GREEN";
        }

        int[] redPockets =
        {
            1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36
        };
        return System.Array.IndexOf(redPockets, number) >= 0 ? "RED" : "BLACK";
    }

    private RouletteStage stage;
}
