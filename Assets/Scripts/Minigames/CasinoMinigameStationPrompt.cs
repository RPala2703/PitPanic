using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CasinoMinigameStationPrompt : MonoBehaviour
{
    [SerializeField] private CasinoPlayerController player;
    [SerializeField] private TableInteractionZone interactionZone;
    [SerializeField] private CasinoTableAttention attention;
    [SerializeField] private CasinoMinigameStation station;
    [SerializeField] private CasinoMinigameType minigameType;
    [SerializeField] private TableAttentionReason attentionReason;
    [SerializeField] private string promptText = "E  -  INTERACT";
    [SerializeField] private bool requestWhenIdle;

    private Canvas canvas;
    private Text prompt;

    public void Configure(
        CasinoPlayerController casinoPlayer,
        TableInteractionZone zone,
        CasinoTableAttention tableAttention,
        CasinoMinigameStation minigameStation,
        CasinoMinigameType type,
        TableAttentionReason reason,
        string interactionPrompt,
        bool allowTaskRequestWhenIdle)
    {
        player = casinoPlayer;
        interactionZone = zone;
        attention = tableAttention;
        station = minigameStation;
        minigameType = type;
        attentionReason = reason;
        promptText = interactionPrompt;
        requestWhenIdle = allowTaskRequestWhenIdle;
    }

    private void Start()
    {
        if (player == null || interactionZone == null || !interactionZone.IsConfigured ||
            attention == null || station == null || !station.IsConfigured)
        {
            Debug.LogError($"{nameof(CasinoMinigameStationPrompt)} on {name} is missing its interaction references.", this);
            enabled = false;
            return;
        }

        CreatePrompt();
    }

    private void Update()
    {
        if (canvas == null)
        {
            return;
        }

        if (player.GetComponent<CasinoPlayerFloatCarrier>()?.IsCarrying == true)
        {
            canvas.enabled = false;
            return;
        }

        bool inRange = interactionZone.IsPlayerInRange(player.transform);
        bool canRequest = requestWhenIdle && !station.IsTaskPending;
        bool canStart = station.IsTaskPending && !station.IsMinigameRunning;
        bool visible = inRange && !station.IsMinigameRunning && (canRequest || canStart);
        canvas.enabled = visible;
        if (!visible)
        {
            return;
        }

        prompt.text = station.IsTaskPending
            ? $"E  -  {promptText}"
            : $"E  -  REQUEST {promptText}";
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (!station.IsTaskPending)
            {
                station.RequestMinigame(minigameType, attentionReason);
            }

            station.TryBegin();
        }
    }

    private void CreatePrompt()
    {
        GameObject canvasObject = new GameObject(
            $"{name} Minigame Prompt",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 70;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject textObject = new GameObject(
            "Interaction Prompt",
            typeof(RectTransform),
            typeof(Text),
            typeof(Outline));
        textObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = rect.anchorMin;
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 48f);
        rect.sizeDelta = new Vector2(720f, 48f);
        prompt = textObject.GetComponent<Text>();
        prompt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        prompt.fontSize = 22;
        prompt.fontStyle = FontStyle.Bold;
        prompt.alignment = TextAnchor.MiddleCenter;
        prompt.color = new Color(1f, 0.88f, 0.48f);
        prompt.raycastTarget = false;
        Outline outline = textObject.GetComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);
        canvas.enabled = false;
    }
}
