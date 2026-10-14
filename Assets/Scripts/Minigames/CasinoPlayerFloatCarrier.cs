using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CasinoPlayerFloatCarrier : MonoBehaviour
{
    private CasinoCashDeskInteraction manager;
    private CasinoFloatRefillOrder order;
    private GameObject trayPrefab;
    private GameObject tray;
    private Canvas promptCanvas;
    private Text promptText;
    private bool ownsCursor;
    private CursorLockMode savedCursorLock;
    private bool savedCursorVisible;
    private Canvas notificationCanvas;
    private Text notificationText;
    private float notificationRemaining;

    public bool IsCarrying => order != null && tray != null;
    public bool IsAtCorrectDestination => IsCarrying && order.Destination != null &&
        order.Destination.InteractionZone.IsPlayerInRange(transform);
    public CasinoFloatRefillOrder CurrentOrder => order;

    public void Configure(CasinoCashDeskInteraction refillManager, GameObject carriedTrayPrefab = null)
    {
        manager = refillManager;
        if (carriedTrayPrefab != null)
        {
            trayPrefab = carriedTrayPrefab;
        }
    }

    public bool BeginCarrying(CasinoFloatRefillOrder refillOrder)
    {
        if (refillOrder == null || manager == null || IsCarrying)
        {
            return false;
        }

        if (trayPrefab == null)
        {
            Debug.LogError("Cannot carry a float because the casino has no carried-tray art prefab.", this);
            return false;
        }

        tray = CreateTray(refillOrder);
        if (tray == null)
        {
            return false;
        }

        order = refillOrder;
        tray.transform.SetParent(transform, false);
        tray.transform.localPosition = new Vector3(0f, 1.5f, 0.28f);
        tray.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
        CreatePrompt();
        return true;
    }

    public void FinishDelivery(string confirmation = null)
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
        if (!string.IsNullOrWhiteSpace(confirmation))
        {
            ShowDeliveryConfirmation(confirmation);
        }
    }

    public bool TryDeliverFromPlayerInput()
    {
        if (!IsAtCorrectDestination || manager == null)
        {
            return false;
        }

        return manager.TryDeliver(this, order.Destination);
    }

    private void Update()
    {
        if (notificationRemaining > 0f)
        {
            notificationRemaining -= Time.unscaledDeltaTime;
            if (notificationRemaining <= 0f && notificationCanvas != null)
            {
                Destroy(notificationCanvas.gameObject);
                notificationCanvas = null;
                notificationText = null;
            }
        }

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

        if (notificationCanvas != null)
        {
            Destroy(notificationCanvas.gameObject);
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

    private void ShowDeliveryConfirmation(string confirmation)
    {
        if (notificationCanvas != null)
        {
            Destroy(notificationCanvas.gameObject);
        }

        notificationCanvas = CasinoMinigameHud.Create("Float Delivery Confirmation", 90);
        CanvasGroup group = notificationCanvas.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        notificationText = CasinoMinigameHud.CreateText(
            notificationCanvas.transform,
            "Float Delivered",
            new Vector2(0f, -72f),
            new Vector2(900f, 58f),
            30);
        notificationText.text = confirmation;
        notificationText.color = new Color(0.54f, 1f, 0.67f);
        notificationRemaining = 1.8f;
    }

    private GameObject CreateTray(CasinoFloatRefillOrder refillOrder)
    {
        GameObject trayRoot = Object.Instantiate(trayPrefab);
        trayRoot.name = "Carried Casino Float Tray";

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
