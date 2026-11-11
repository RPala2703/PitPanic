using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PitDeskInteraction : MonoBehaviour
{
    private float feedbackUntil;
    [SerializeField] private CasinoPlayerController player;
    [SerializeField] private TableInteractionZone interactionZone;
    [SerializeField] private DealerAssignmentManager assignmentManager;
    [SerializeField] private CasinoTableAttention attention;
    [SerializeField, Min(0f)] private float refreshInterval = 0.5f;

    private Canvas canvas;
    private GameObject panel;
    private Text statusText;
    private Text promptText;
    private AIDealer selectedDealer;
    private readonly List<GameObject> rows = new List<GameObject>();
    private bool isOpen;
    private float nextRefreshTime;

    public bool IsConfigured => player != null && interactionZone != null &&
                                interactionZone.IsConfigured && attention != null && assignmentManager != null &&
                                assignmentManager.IsConfigured;
    public bool IsOpen => isOpen;
    public bool IsPlayerInRange(Transform candidate) =>
        candidate != null && interactionZone != null &&
        interactionZone.IsPlayerInRange(candidate);

    public void Configure(
        CasinoPlayerController casinoPlayer,
        TableInteractionZone zone,
        DealerAssignmentManager manager,
        CasinoTableAttention tableAttention)
    {
        player = casinoPlayer;
        interactionZone = zone;
        assignmentManager = manager;
        attention = tableAttention;
    }

    private void Start()
    {
        if (!IsConfigured)
        {
            Debug.LogError($"{nameof(PitDeskInteraction)} on {name} is missing its player, desk interaction zone, or dealer manager.", this);
            enabled = false;
            return;
        }

        BuildInterface();
    }

    private void Update()
    {
        if (Time.timeScale == 0)
        {
            if (promptText != null) promptText.gameObject.SetActive(false);
            return;
        }
        if (!isOpen && (CasinoMinigamePresentation.IsOpen || CasinoPlayerInteractionRouter.IsAnyInspectionOpen()))
        {
            if (promptText != null)
            {
                promptText.gameObject.SetActive(false);
            }

            return;
        }

        if (isOpen)
        {
            if (promptText != null)
            {
                promptText.gameObject.SetActive(false);
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
            else if (Time.unscaledTime >= nextRefreshTime)
            {
                RenderDealerRows();
            }

            return;
        }

        bool inRange = interactionZone.IsPlayerInRange(player.transform);
        if (promptText != null)
        {
            promptText.gameObject.SetActive(inRange);
        }

    }

    public bool TryOpenFromPlayerInput()
    {
        if (isOpen || !IsPlayerInRange(player.transform) || CasinoMinigamePresentation.IsOpen ||
            CasinoPlayerInteractionRouter.IsAnyInspectionOpen())
        {
            return false;
        }

        Open();
        return isOpen;
    }

    private void BuildInterface()
    {
        GameObject canvasObject = new GameObject("Pit Desk UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        panel = CreateRect("Staff Management", canvas.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1040f, 620f));
        Image panelImage = panel.AddComponent<Image>();
        CasinoHudStyle.Panel(panelImage, CasinoHudStyle.Ink);
        Text title = CreateText("Desk Title", panel.transform, new Vector2(0f, 278f), new Vector2(700f, 40f), 25);
        title.text = "DEALER CREW!";
        title.color = CasinoHudStyle.Gold;
        statusText = CreateText("Assignment Status", panel.transform, new Vector2(0f, -276f), new Vector2(820f, 30f), 16);
        promptText = CreateText("Pit Desk Prompt", canvas.transform, new Vector2(0f, -314f), new Vector2(760f, 46f), 20);
        promptText.fontStyle = FontStyle.Bold;
        promptText.text = "E  •  DEALER CREW";
        promptText.gameObject.SetActive(false);
        CreateButton("Close", panel.transform, "ESC  •  CLOSE", new Vector2(0f, -310f), new Vector2(170f, 38f), Close);
        panel.SetActive(false);
    }

    private void Open()
    {
        CasinoSoundscape.SetFocus(transform);
        isOpen = true;
        feedbackUntil = 0f;
        panel.SetActive(true);
        player.SetMovementEnabled(false);
        RenderDealerRows();
    }

    private void Close()
    {
        CasinoSoundscape.ReleaseFocus(transform);
        isOpen = false;
        selectedDealer = null;
        if (panel != null) panel.SetActive(false);
        if (player != null) player.SetMovementEnabled(true);
    }

    private void RenderDealerRows()
    {
        nextRefreshTime = Time.unscaledTime + refreshInterval;
        if (selectedDealer != null && !selectedDealer.IsReadyAtPitDesk)
        {
            selectedDealer = null;
        }

        foreach (GameObject row in rows)
        {
            Destroy(row);
        }

        rows.Clear();
        float rowY = 220f;
        IReadOnlyList<AIDealer> dealers = assignmentManager.Dealers;
        IReadOnlyList<CasinoDealerStation> stations = assignmentManager.Stations;
        for (int i = 0; i < dealers.Count; i++)
        {
            AIDealer dealer = dealers[i];
            if (dealer == null) continue;
            float y = rowY - i * 98f;
            GameObject row = CreateRect($"Dealer Row {i + 1}", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(-260f, y), new Vector2(490f, 80f));
            Image rowImage = row.AddComponent<Image>();
            CasinoHudStyle.Panel(rowImage, dealer == selectedDealer
                ? new Color(0.24f, 0.39f, 0.28f, 0.95f)
                : new Color(0.08f, 0.23f, 0.20f, 0.92f));
            bool breakRecommended = dealer.NeedsBreak || dealer.Energy <= dealer.TiredThreshold;
            SpriteRenderer portraitSource = dealer.GetComponentInChildren<SpriteRenderer>();
            if (portraitSource != null && portraitSource.sprite != null)
            {
                GameObject portrait = CreateRect("Dealer Portrait", row.transform,
                    new Vector2(0.5f, 0.5f), new Vector2(-198f, 0f), new Vector2(62f, 70f));
                Image portraitImage = portrait.AddComponent<Image>();
                CasinoPixelArt.SetSprite(portraitImage, portraitSource.sprite, Color.white);
                portraitImage.raycastTarget = false;
            }
            Text details = CreateText("Dealer Details", row.transform, new Vector2(-58f, 7f), new Vector2(235f, 62f), 16);
            details.text =
                $"{dealer.DealerId}  •  {dealer.Energy:0}%\n{(breakRecommended ? "BREAK NEEDED!" : dealer.StatusLabel)}\n{dealer.CurrentAssignmentLabel}";
            if (breakRecommended)
            {
                details.color = new Color(1f, 0.76f, 0.35f);
            }

            if (dealer.IsReadyAtPitDesk)
            {
                int dealerIndex = i;
                CreateButton("Select Dealer", row.transform, dealer == selectedDealer ? "SELECTED" : "ASSIGN", new Vector2(165f, 24f), new Vector2(106f, 22f), () => SelectDealer(dealerIndex));
                CreateButton("Send On Break", row.transform, "BREAK", new Vector2(165f, 0f), new Vector2(106f, 22f), () => SendDealerOnBreak(dealerIndex));
                CreateButton("Leave Waiting", row.transform, "WAIT", new Vector2(165f, -24f), new Vector2(106f, 22f), () => LeaveDealerWaiting(dealerIndex));
            }

            rows.Add(row);
        }

        float stationY = 220f;
        for (int i = 0; i < stations.Count; i++)
        {
            CasinoDealerStation station = stations[i];
            float y = stationY - i * 98f;
            GameObject stationRow = CreateRect($"Table Row {i + 1}", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(275f, y), new Vector2(500f, 80f));
            CasinoHudStyle.Panel(stationRow.AddComponent<Image>(), new Color(0.28f, 0.18f, 0.20f, 0.95f));
            string currentName = station.AssignedDealer != null
                ? $"{station.AssignedDealer.DealerId}  {station.AssignedDealer.StatusLabel}"
                : "UNSTAFFED";
            string incomingName = station.PendingReplacement != null ? $"ON THE WAY: {station.PendingReplacement.DealerId}" : "";
            CreateText("Table Details", stationRow.transform, new Vector2(-80f, 10f), new Vector2(280f, 55f), 15).text =
                $"{station.StationName}  •  {currentName}\n{incomingName}";
            int stationIndex = i;
            Button assignmentButton = CreateButton(
                "Assign Selected Dealer",
                stationRow.transform,
                selectedDealer == null ? "PICK A DEALER" : "SEND HERE!",
                new Vector2(165f, 0f),
                new Vector2(150f, 36f),
                () => AssignSelectedDealer(stationIndex));
            assignmentButton.interactable = selectedDealer != null &&
                                            assignmentManager.CanAssignDealer(selectedDealer, station, out _);
            Text stationHint = CreateText("Availability Hint", stationRow.transform, new Vector2(0f, -27f), new Vector2(440f, 20f), 11);
            if (selectedDealer != null && !assignmentManager.CanAssignDealer(selectedDealer, station, out string assignmentReason))
            {
                stationHint.text = assignmentReason.ToUpperInvariant();
            }
            else
            {
                stationHint.text = station.CanAcceptReplacement
                    ? station.AssignedDealer == null ? "NEEDS A DEALER!" : "READY FOR COVER"
                    : station.PendingReplacement != null ? "COVER ON THE WAY" : "NOT AVAILABLE";
            }

            rows.Add(stationRow);
        }

        if (Time.unscaledTime >= feedbackUntil)
        {
            statusText.text = selectedDealer == null
                ? "PICK A READY DEALER, THEN CHOOSE A TABLE!"
                : $"{selectedDealer.DealerId} IS READY  •  PICK A TABLE!";
        }
    }

    private void SelectDealer(int index)
    {
        selectedDealer = assignmentManager.Dealers[index];
        feedbackUntil = 0f;
        RenderDealerRows();
    }

    private void SendDealerOnBreak(int index)
    {
        AIDealer dealer = assignmentManager.Dealers[index];
        string feedback;
        if (assignmentManager.TrySendDealerOnBreak(dealer, out string reason))
        {
            selectedDealer = null;
            feedback = $"{dealer.DealerId} IS OFF TO BREAK!";
        }
        else
        {
            feedback = reason;
        }

        RenderDealerRows();
        ShowFeedback(feedback);
    }

    private void LeaveDealerWaiting(int index)
    {
        AIDealer dealer = assignmentManager.Dealers[index];
        assignmentManager.AcknowledgeDealerAtPitDesk(dealer);
        selectedDealer = null;
        RenderDealerRows();
        ShowFeedback($"{dealer.DealerId} WILL WAIT HERE.");
    }

    private void AssignSelectedDealer(int stationIndex)
    {
        if (selectedDealer == null)
        {
            statusText.text = "SELECT A READY DEALER FIRST.";
            return;
        }

        AIDealer assignedDealer = selectedDealer;
        CasinoDealerStation station = assignmentManager.Stations[stationIndex];
        bool assigned = assignmentManager.TryAssignDealer(assignedDealer, station, out string reason);
        string feedback = assigned
            ? $"{assignedDealer.DealerId} IS WALKING TO {station.StationName}"
            : reason;
        selectedDealer = null;
        RenderDealerRows();
        ShowFeedback(feedback);
    }

    private void ShowFeedback(string message)
    {
        statusText.text = message;
        feedbackUntil = Time.unscaledTime + 3f;
    }

    private Button CreateButton(string objectName, Transform parent, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = CreateRect(objectName, parent, new Vector2(0.5f, 0.5f), position, size);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.57f, 0.39f, 0.15f, 0.98f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        CasinoHudStyle.Button(button);
        button.onClick.AddListener(action);
        Text text = CreateText("Label", buttonObject.transform, Vector2.zero, size, 13);
        text.text = label;
        return button;
    }

    private static Text CreateText(string objectName, Transform parent, Vector2 position, Vector2 size, int fontSize)
    {
        GameObject textObject = CreateRect(objectName, parent, new Vector2(0.5f, 0.5f), position, size);
        Text text = textObject.AddComponent<Text>();
        text.font = CasinoPixelFont.Default;
        text.fontSize = fontSize;
        CasinoPixelFont.Fit(text, fontSize);
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject CreateRect(string objectName, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return gameObject;
    }
}
