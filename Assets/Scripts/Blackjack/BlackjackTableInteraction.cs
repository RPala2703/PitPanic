using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BlackjackTableInteraction : MonoBehaviour
{
    private enum ServiceStage
    {
        None,
        CustomerDecision,
        DealRequested,
        CompleteService
    }

    [SerializeField] private CasinoPlayerController playerController;
    [SerializeField] private Transform interactionPosition;
    [SerializeField] private CasinoTableAttention attention;
    [SerializeField] private CasinoSeat[] seats;
    [SerializeField, Min(0.1f)] private float interactionDistance = 2f;

    private readonly Queue<CasinoCustomerAI> waitingCustomers = new Queue<CasinoCustomerAI>();
    private readonly HashSet<CasinoCustomerAI> waitingSet = new HashSet<CasinoCustomerAI>();
    private readonly Dictionary<CasinoCustomerAI, GameObject> customerBets = new Dictionary<CasinoCustomerAI, GameObject>();
    private GameObject prompt;
    private Text promptText;
    private GameObject panel;
    private Text customerNameText;
    private Text dealerCardsText;
    private Text dealerTotalText;
    private Text customerCardsText;
    private Text customerTotalText;
    private Text statusText;
    private Button actionButton;
    private BlackjackRound round;
    private CasinoCustomerAI activeCustomer;
    private ServiceStage stage;
    private bool panelOpen;
    private float nextCustomerDecisionTime;

    public bool IsConfigured =>
        playerController != null &&
        interactionPosition != null &&
        attention != null &&
        seats != null &&
        seats.Length == 3;

    public int WaitingCustomerCount => waitingSet.Count;

    public void Configure(CasinoPlayerController player, Transform dealerPosition, CasinoTableAttention tableAttention, CasinoSeat[] tableSeats)
    {
        playerController = player;
        interactionPosition = dealerPosition;
        attention = tableAttention;
        seats = tableSeats;
    }

    private void Start()
    {
        if (!IsConfigured)
        {
            Debug.LogError($"{nameof(BlackjackTableInteraction)} on {name} is missing its player, dealer position, attention system, or seats.", this);
            enabled = false;
            return;
        }

        foreach (CasinoSeat seat in seats)
        {
            if (seat == null)
            {
                Debug.LogError($"{nameof(BlackjackTableInteraction)} on {name} has an unassigned customer seat.", this);
                enabled = false;
                return;
            }

            seat.CustomerSeated += OnCustomerSeated;
            seat.CustomerReleased += OnCustomerReleased;
        }

        EnsureInputEventSystem();
        BuildInterface();
        RefreshInterface();
    }

    private void OnDestroy()
    {
        if (seats == null)
        {
            return;
        }

        foreach (CasinoSeat seat in seats)
        {
            if (seat != null)
            {
                seat.CustomerSeated -= OnCustomerSeated;
                seat.CustomerReleased -= OnCustomerReleased;
            }
        }
    }

    private void Update()
    {
        if (activeCustomer != null && stage == ServiceStage.CustomerDecision && Time.time >= nextCustomerDecisionTime)
        {
            ResolveCustomerDecision();
        }

        Keyboard keyboard = Keyboard.current;
        if (panelOpen)
        {
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                ClosePanel();
            }
            else if (keyboard.eKey.wasPressedThisFrame)
            {
                PerformServiceAction();
            }

            return;
        }

        if (playerController == null || interactionPosition == null)
        {
            return;
        }

        Vector3 playerPosition = playerController.transform.position;
        Vector3 dealerPosition = interactionPosition.position;
        float distance = Vector2.Distance(new Vector2(playerPosition.x, playerPosition.z), new Vector2(dealerPosition.x, dealerPosition.z));
        bool inRange = distance <= interactionDistance;
        bool hasAction = waitingSet.Count > 0 || stage == ServiceStage.DealRequested || stage == ServiceStage.CompleteService;
        prompt.SetActive(inRange && hasAction);
        if (inRange && hasAction)
        {
            promptText.text = GetActionPrompt();
        }

        if (inRange && hasAction && keyboard != null && keyboard.eKey.wasPressedThisFrame)
        {
            ServiceStage promptedStage = stage;
            OpenPanel();
            if (promptedStage == ServiceStage.DealRequested || promptedStage == ServiceStage.CompleteService)
            {
                PerformServiceAction();
            }
        }
    }

    private void OnCustomerSeated(CasinoCustomerAI customer)
    {
        if (customer == null || !waitingSet.Add(customer))
        {
            return;
        }

        waitingCustomers.Enqueue(customer);
        attention.RequestAttention(TableAttentionReason.CustomersWaiting);
        PlaceSimpleBet(customer);
        RefreshInterface();
    }

    private void OnCustomerReleased(CasinoCustomerAI customer)
    {
        if (customer == null)
        {
            return;
        }

        if (waitingSet.Remove(customer))
        {
            attention.ResolveAttention(TableAttentionReason.CustomersWaiting);
        }

        if (customerBets.TryGetValue(customer, out GameObject betMarker))
        {
            customerBets.Remove(customer);
            if (betMarker != null)
            {
                Destroy(betMarker);
            }
        }

        if (activeCustomer == customer)
        {
            ClearServiceTasks();
            activeCustomer = null;
            round = null;
            stage = ServiceStage.None;
            if (panelOpen)
            {
                ClosePanel();
            }
        }

        RefreshInterface();
    }

    private void BeginService()
    {
        while (waitingCustomers.Count > 0)
        {
            CasinoCustomerAI candidate = waitingCustomers.Dequeue();
            if (!waitingSet.Remove(candidate) || candidate == null || !candidate.BeginService())
            {
                continue;
            }

            attention.ResolveAttention(TableAttentionReason.CustomersWaiting);
            activeCustomer = candidate;
            round = new BlackjackRound();
            stage = ServiceStage.CustomerDecision;
            nextCustomerDecisionTime = Time.time + 0.8f;
            statusText.text = $"{activeCustomer.name} is betting. The customer will choose Hit or Stand.";
            RefreshInterface();
            return;
        }

        ClosePanel();
    }

    private void ResolveCustomerDecision()
    {
        if (activeCustomer == null || round == null || round.IsComplete)
        {
            FinishHand();
            return;
        }

        if (activeCustomer.WantsAnotherCard(round.PlayerTotal))
        {
            stage = ServiceStage.DealRequested;
            attention.RequestAttention(TableAttentionReason.DealCards);
            statusText.text = $"{activeCustomer.name} asks for another card. Press E or Deal Card.";
        }
        else
        {
            round.Stand();
            FinishHand();
        }

        RefreshInterface();
    }

    private void PerformServiceAction()
    {
        if (stage == ServiceStage.None)
        {
            BeginService();
        }
        else if (stage == ServiceStage.DealRequested)
        {
            attention.ResolveAttention(TableAttentionReason.DealCards);
            round.Hit();
            if (round.IsComplete)
            {
                FinishHand();
            }
            else
            {
                stage = ServiceStage.CustomerDecision;
                nextCustomerDecisionTime = Time.time + 0.8f;
                statusText.text = $"{activeCustomer.name} is considering the next move.";
            }
        }
        else if (stage == ServiceStage.CompleteService)
        {
            attention.ResolveAttention(TableAttentionReason.PayoutRequired);
            CasinoCustomerAI completedCustomer = activeCustomer;
            activeCustomer = null;
            round = null;
            stage = ServiceStage.None;
            completedCustomer.CompleteVisit();
            if (waitingSet.Count > 0)
            {
                statusText.text = "Another customer is waiting.";
            }
            else
            {
                statusText.text = "Service complete.";
            }

            ClosePanel();
        }

        RefreshInterface();
    }

    private void PlaceSimpleBet(CasinoCustomerAI customer)
    {
        if (customer.Seat == null)
        {
            return;
        }

        GameObject chip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        chip.name = $"{customer.name} Bet";
        chip.transform.SetParent(transform, false);
        chip.transform.localPosition = new Vector3(customer.Seat.transform.localPosition.x, 1.46f, -0.72f);
        chip.transform.localScale = new Vector3(0.34f, 0.055f, 0.34f);
        Destroy(chip.GetComponent<Collider>());
        Renderer chipRenderer = chip.GetComponent<Renderer>();
        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        chipRenderer.GetPropertyBlock(properties);
        Color chipColor = new Color(0.98f, 0.72f, 0.16f);
        properties.SetColor("_BaseColor", chipColor);
        properties.SetColor("_Color", chipColor);
        chipRenderer.SetPropertyBlock(properties);
        customerBets[customer] = chip;
    }

    private void FinishHand()
    {
        stage = ServiceStage.CompleteService;
        attention.RequestAttention(TableAttentionReason.PayoutRequired);
        statusText.text = $"Hand complete: {GetOutcomeText(round.Outcome)}. Press E to complete service.";
        RefreshInterface();
    }

    private void ClearServiceTasks()
    {
        attention.ResolveAttention(TableAttentionReason.DealCards);
        attention.ResolveAttention(TableAttentionReason.PayoutRequired);
    }

    private void OpenPanel()
    {
        panelOpen = true;
        prompt.SetActive(false);
        panel.SetActive(true);
        playerController.SetMovementEnabled(false);
        if (stage == ServiceStage.None)
        {
            BeginService();
        }
        else
        {
            activeCustomer?.BeginService();
            RefreshInterface();
        }
    }

    private void ClosePanel()
    {
        panelOpen = false;
        if (panel != null)
        {
            panel.SetActive(false);
        }

        activeCustomer?.ResumeWaiting();
        playerController.SetMovementEnabled(true);
    }

    private void RefreshInterface()
    {
        if (panel == null)
        {
            return;
        }

        if (activeCustomer != null && round != null)
        {
            customerNameText.text = activeCustomer.name;
            customerCardsText.text = FormatCards(round.PlayerCards, true);
            customerTotalText.text = $"Customer total: {round.PlayerTotal}";
            dealerCardsText.text = FormatCards(round.DealerCards, round.IsComplete);
            dealerTotalText.text = round.IsComplete ? $"Dealer total: {round.DealerTotal}" : "Dealer total: ?";
            actionButton.gameObject.SetActive(stage == ServiceStage.DealRequested || stage == ServiceStage.CompleteService);
            actionButton.interactable = true;
            actionButton.GetComponentInChildren<Text>().text = stage == ServiceStage.DealRequested ? "Deal Card (E)" : "Complete Service (E)";
            statusText.text = stage == ServiceStage.CompleteService
                ? $"Hand complete: {GetOutcomeText(round.Outcome)}. Press E to complete service."
                : statusText.text;
        }
        else
        {
            customerNameText.text = waitingSet.Count > 0 ? $"{waitingSet.Count} customer(s) waiting" : "No active customer";
            customerCardsText.text = "";
            customerTotalText.text = "";
            dealerCardsText.text = "";
            dealerTotalText.text = "";
            actionButton.gameObject.SetActive(waitingSet.Count > 0);
            actionButton.GetComponentInChildren<Text>().text = "Begin Service (E)";
            actionButton.interactable = waitingSet.Count > 0;
            statusText.text = waitingSet.Count > 0 ? "Deal the next customer's opening hand." : "Wait for customers to take a seat.";
        }
    }

    private string GetActionPrompt()
    {
        if (activeCustomer != null && stage == ServiceStage.CustomerDecision)
        {
            return $"In service with {activeCustomer.name}";
        }

        if (stage == ServiceStage.DealRequested) return "Press E to deal the requested card";
        if (stage == ServiceStage.CompleteService) return "Press E to complete customer service";
        return activeCustomer != null ? "Press E to return to table service" : "Press E to serve a waiting customer";
    }

    private void EnsureInputEventSystem()
    {
        EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
        }

        if (eventSystem.GetComponent<BaseInputModule>() == null)
        {
            InputSystemUIInputModule inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
        }
    }

    private void BuildInterface()
    {
        GameObject canvasObject = new GameObject("Blackjack UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            Debug.LogError("Unity's LegacyRuntime.ttf font is required to render the blackjack interface.", this);
            enabled = false;
            return;
        }

        prompt = CreateUiObject("Interaction Prompt", canvas.transform);
        SetRect(prompt, new Vector2(0.5f, 0f), new Vector2(0f, 72f), new Vector2(520f, 58f));
        prompt.AddComponent<Image>().color = new Color(0.035f, 0.055f, 0.06f, 0.92f);
        promptText = CreateText("Prompt Text", prompt.transform, font, "", 22, Color.white, Vector2.zero, new Vector2(500f, 52f), TextAnchor.MiddleCenter);

        panel = CreateUiObject("Blackjack Service Panel", canvas.transform);
        SetRect(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 560f));
        panel.AddComponent<Image>().color = new Color(0.035f, 0.075f, 0.065f, 0.97f);
        CreateText("Title", panel.transform, font, "BLACKJACK TABLE SERVICE", 31, new Color(0.95f, 0.75f, 0.35f), new Vector2(0f, 238f), new Vector2(600f, 48f), TextAnchor.MiddleCenter);
        customerNameText = CreateText("Customer", panel.transform, font, "", 18, new Color(0.8f, 0.9f, 0.84f), new Vector2(0f, 192f), new Vector2(600f, 32f), TextAnchor.MiddleCenter);
        CreateText("Customer Label", panel.transform, font, "CUSTOMER", 17, new Color(0.78f, 0.86f, 0.82f), new Vector2(0f, 142f), new Vector2(560f, 28f), TextAnchor.MiddleCenter);
        customerCardsText = CreateText("Customer Cards", panel.transform, font, "", 23, Color.white, new Vector2(0f, 102f), new Vector2(650f, 38f), TextAnchor.MiddleCenter);
        customerTotalText = CreateText("Customer Total", panel.transform, font, "", 18, Color.white, new Vector2(0f, 67f), new Vector2(560f, 28f), TextAnchor.MiddleCenter);
        CreateText("Dealer Label", panel.transform, font, "DEALER", 17, new Color(0.78f, 0.86f, 0.82f), new Vector2(0f, 20f), new Vector2(560f, 28f), TextAnchor.MiddleCenter);
        dealerCardsText = CreateText("Dealer Cards", panel.transform, font, "", 23, Color.white, new Vector2(0f, -20f), new Vector2(650f, 38f), TextAnchor.MiddleCenter);
        dealerTotalText = CreateText("Dealer Total", panel.transform, font, "", 18, Color.white, new Vector2(0f, -55f), new Vector2(560f, 28f), TextAnchor.MiddleCenter);
        statusText = CreateText("Service Status", panel.transform, font, "", 18, new Color(0.98f, 0.78f, 0.38f), new Vector2(0f, -115f), new Vector2(650f, 46f), TextAnchor.MiddleCenter);
        CreateText("Instructions", panel.transform, font, "The customer chooses Hit or Stand. E deals requested cards.", 15, new Color(0.72f, 0.78f, 0.74f), new Vector2(0f, -250f), new Vector2(650f, 28f), TextAnchor.MiddleCenter);

        actionButton = CreateButton(panel.transform, font, "Begin Service (E)", new Vector2(0f, -184f), new Vector2(240f, 50f), PerformServiceAction);
        CreateButton(panel.transform, font, "Close", new Vector2(310f, 238f), new Vector2(82f, 38f), ClosePanel);
        panel.SetActive(false);
    }

    private static string FormatCards(System.Collections.Generic.IReadOnlyList<BlackjackCard> cards, bool revealAll)
    {
        List<string> labels = new List<string>(cards.Count);
        for (int i = 0; i < cards.Count; i++)
        {
            labels.Add(i == 0 && !revealAll ? "[Hidden]" : $"[{cards[i]}]");
        }

        return string.Join("   ", labels);
    }

    private static string GetOutcomeText(BlackjackOutcome outcome)
    {
        switch (outcome)
        {
            case BlackjackOutcome.Blackjack: return "Blackjack!";
            case BlackjackOutcome.Win: return "Win";
            case BlackjackOutcome.Lose: return "Lose";
            case BlackjackOutcome.Push: return "Push";
            default: return "";
        }
    }

    private Button CreateButton(Transform parent, Font font, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = CreateUiObject(label, parent);
        SetRect(buttonObject, new Vector2(0.5f, 0.5f), position, size);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.38f, 0.23f, 0.09f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);
        CreateText("Label", buttonObject.transform, font, label, 18, Color.white, Vector2.zero, size, TextAnchor.MiddleCenter);
        return button;
    }

    private Text CreateText(string name, Transform parent, Font font, string value, int fontSize, Color color, Vector2 position, Vector2 size, TextAnchor alignment)
    {
        GameObject textObject = CreateUiObject(name, parent);
        SetRect(textObject, new Vector2(0.5f, 0.5f), position, size);
        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject uiObject = new GameObject(name, typeof(RectTransform));
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    private static void SetRect(GameObject uiObject, Vector2 anchor, Vector2 position, Vector2 size)
    {
        RectTransform rect = uiObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
