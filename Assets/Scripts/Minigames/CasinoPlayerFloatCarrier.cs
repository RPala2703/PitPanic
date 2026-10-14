using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CasinoPlayerFloatCarrier : MonoBehaviour
{
    private CasinoCashDeskInteraction manager;
    private CasinoFloatRefillOrder order;
    private GameObject tray;
    private Canvas promptCanvas;
    private Text promptText;
    private bool ownsCursor;
    private CursorLockMode savedCursorLock;
    private bool savedCursorVisible;

    public bool IsCarrying => order != null && tray != null;
    public CasinoFloatRefillOrder CurrentOrder => order;

    public void Configure(CasinoCashDeskInteraction refillManager)
    {
        manager = refillManager;
    }

    public bool BeginCarrying(CasinoFloatRefillOrder refillOrder)
    {
        if (refillOrder == null || manager == null || IsCarrying)
        {
            return false;
        }

        order = refillOrder;
        tray = CreateTray(refillOrder);
        tray.transform.SetParent(transform, false);
        tray.transform.localPosition = new Vector3(0f, 1.5f, 0.28f);
        tray.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
        CreatePrompt();
        return true;
    }

    public void FinishDelivery()
    {
        if (tray != null)
        {
            Destroy(tray);
        }

        tray = null;
        order = null;
        if (promptCanvas != null)
        {
            Destroy(promptCanvas.gameObject);
        }

        promptCanvas = null;
        promptText = null;
    }

    private void Update()
    {
        if (!IsCarrying || manager == null)
        {
            return;
        }

        bool nearCorrect = order.Destination.InteractionZone.IsPlayerInRange(transform);
        bool nearOther = false;
        IReadOnlyList<CasinoFloatRefillDestination> destinations = manager.Destinations;
        for (int i = 0; i < destinations.Count; i++)
        {
            CasinoFloatRefillDestination destination = destinations[i];
            if (destination != null && destination != order.Destination &&
                destination.InteractionZone.IsPlayerInRange(transform))
            {
                nearOther = true;
                break;
            }
        }

        if (promptCanvas != null)
        {
            promptCanvas.enabled = nearCorrect || nearOther;
        }

        if (promptText != null)
        {
            promptText.text = nearCorrect
                ? $"E  •  DELIVER FLOAT TO {GetShortDestinationLabel(order)}  •  £{order.TotalPence / 100}.{order.TotalPence % 100:00}"
                : $"FLOAT FOR {GetShortDestinationLabel(order)}  •  DELIVER TO THAT TABLE";
        }

        if (nearCorrect && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            manager.TryDeliver(this, order.Destination);
        }
    }

    private void OnDestroy()
    {
        if (ownsCursor)
        {
            Cursor.lockState = savedCursorLock;
            Cursor.visible = savedCursorVisible;
        }

        if (tray != null)
        {
            Destroy(tray);
        }

        if (promptCanvas != null)
        {
            Destroy(promptCanvas.gameObject);
        }
    }

    private void CreatePrompt()
    {
        promptCanvas = CasinoMinigameHud.Create("Float Delivery Prompt", 75);
        promptText = CasinoMinigameHud.CreateText(
            promptCanvas.transform,
            "Float Delivery Instructions",
            new Vector2(0f, -26f),
            new Vector2(1100f, 44f),
            23);
        promptText.color = new Color(1f, 0.84f, 0.28f);
        promptCanvas.enabled = false;
    }

    private static GameObject CreateTray(CasinoFloatRefillOrder refillOrder)
    {
        GameObject trayRoot = new GameObject("Carried Casino Float Tray");
        GameObject baseTray = GameObject.CreatePrimitive(PrimitiveType.Cube);
        baseTray.name = "Tray";
        baseTray.transform.SetParent(trayRoot.transform, false);
        baseTray.transform.localScale = new Vector3(1.45f, 0.09f, 1.75f);
        SetColor(baseTray.GetComponent<Renderer>(), new Color(0.24f, 0.13f, 0.065f));
        Object.Destroy(baseTray.GetComponent<Collider>());

        Color[] colors =
        {
            new Color(0.81f, 0.16f, 0.13f),
            new Color(0.16f, 0.43f, 0.78f),
            new Color(0.20f, 0.62f, 0.34f),
            new Color(0.08f, 0.09f, 0.11f)
        };
        for (int denomination = 0; denomination < 4; denomination++)
        {
            for (int i = 0; i < refillOrder.CountFor(denomination); i++)
            {
                GameObject chip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                chip.name = $"Float Chip {refillOrder.GetDenominationPence(denomination)}p";
                chip.transform.SetParent(trayRoot.transform, false);
                chip.transform.localScale = new Vector3(0.22f, 0.035f, 0.22f);
                chip.transform.localPosition = new Vector3(
                    -0.46f + (i % 4) * 0.3f,
                    0.075f + (i / 4) * 0.074f + denomination * 0.004f,
                    -0.61f + (i / 4) * 0.3f);
                SetColor(chip.GetComponent<Renderer>(), colors[denomination]);
                Object.Destroy(chip.GetComponent<Collider>());
            }
        }

        return trayRoot;
    }

    private static void SetColor(Renderer renderer, Color color)
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        renderer.SetPropertyBlock(block);
    }

    private static string GetShortDestinationLabel(CasinoFloatRefillOrder refillOrder)
    {
        return $"{refillOrder.DestinationId}  •  {refillOrder.Destination.DisplayName.ToUpperInvariant()}";
    }
}
