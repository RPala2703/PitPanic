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
        tray.transform.localPosition = Vector3.zero;
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
        if (Time.timeScale == 0) return;
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

    private void LateUpdate()
    {
        if (!IsCarrying || Time.timeScale == 0) return;
        Camera camera = Camera.main;
        PixelCharacterVisual visual = GetComponentInChildren<PixelCharacterVisual>();
        if (camera == null || visual == null) return;
        SpriteRenderer actorSprite = visual.GetComponentInChildren<SpriteRenderer>();
        if (actorSprite == null) return;
        Vector3 forward = transform.forward;
        bool side = Mathf.Abs(forward.x) > Mathf.Abs(forward.z);
        bool away = !side && forward.z > 0;
        float x = side ? Mathf.Sign(forward.x) * .58f : 0;
        float height = away ? 1.31f : .82f;
        if (tray.transform.parent != actorSprite.transform) tray.transform.SetParent(actorSprite.transform, false);
        tray.transform.localPosition = new Vector3(x, height, away ? .06f : -.06f);
        tray.transform.localRotation = Quaternion.identity;
        tray.transform.localScale = Vector3.one;
        foreach (SpriteRenderer sprite in tray.GetComponentsInChildren<SpriteRenderer>())
        {
            sprite.sortingLayerID = actorSprite.sortingLayerID;
            sprite.sortingOrder = actorSprite.sortingOrder + (away ? -2 : 2) +
                (sprite.gameObject.name.StartsWith("Float Chip") ? 1 : 0);
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
        foreach (MeshRenderer renderer in trayRoot.GetComponentsInChildren<MeshRenderer>())
            renderer.enabled = false;
        foreach (Collider collider in trayRoot.GetComponentsInChildren<Collider>())
            collider.enabled = false;
        foreach (SpriteRenderer renderer in trayRoot.GetComponentsInChildren<SpriteRenderer>())
            renderer.enabled = false;
        SpriteRenderer traySurface = CreateHeldSprite(trayRoot.transform, "Pixel Float Tray",
            PixelWorldProps.FloatTray, new Vector2(.94f, .30f));
        traySurface.color = Color.white;

        for (int denomination = 0; denomination < 4; denomination++)
        {
            for (int i = 0; i < refillOrder.CountFor(denomination); i++)
            {
                GameObject chip = new GameObject($"Float Chip {refillOrder.GetDenominationPence(denomination)}p");
                chip.transform.SetParent(trayRoot.transform, false);
                chip.transform.localPosition = new Vector3(
                    -.32f + denomination * .21f,
                    .06f + i * .012f, -.01f);
                SpriteRenderer sprite = CreateHeldSprite(chip.transform, $"Float Chip {denomination}",
                    CasinoPixelArt.ChipForValue(refillOrder.GetDenominationPence(denomination)), new Vector2(.16f, .065f));
                sprite.color = Color.white;
            }
        }

        return trayRoot;
    }

    private static SpriteRenderer CreateHeldSprite(Transform parent, string label, Sprite art, Vector2 size)
    {
        GameObject root = new GameObject(label, typeof(SpriteRenderer));
        root.transform.SetParent(parent, false);
        SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
        renderer.sprite = art;
        root.transform.localScale = new Vector3(size.x / art.bounds.size.x, size.y / art.bounds.size.y, 1);
        return renderer;
    }

    private static string GetShortDestinationLabel(CasinoFloatRefillOrder refillOrder)
    {
        return $"{refillOrder.DestinationId}  •  {refillOrder.Destination.DisplayName.ToUpperInvariant()}";
    }
}
