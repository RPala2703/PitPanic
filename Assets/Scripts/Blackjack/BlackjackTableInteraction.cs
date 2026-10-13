using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BlackjackTableInteraction : MonoBehaviour
{
    [SerializeField] private CasinoPlayerController playerController;
    [SerializeField] private Transform interactionPosition;
    [SerializeField, Min(0.1f)] private float interactionDistance = 2f;

    private Canvas canvas;
    private GameObject prompt;
    private GameObject panel;
    private Text dealerCardsText;
    private Text dealerTotalText;
    private Text playerCardsText;
    private Text playerTotalText;
    private Text resultText;
    private Button hitButton;
    private Button standButton;
    private Button newRoundButton;
    private BlackjackRound round;
    private bool panelOpen;

    public bool IsConfigured => playerController != null && interactionPosition != null;

    public void Configure(CasinoPlayerController player, Transform dealerPosition)
    {
        playerController = player;
        interactionPosition = dealerPosition;
    }

    private void Start()
    {
        EnsureInputEventSystem();
        BuildInterface();
        RefreshInterface();
    }

    private void Update()
    {
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
            else if (keyboard.hKey.wasPressedThisFrame)
            {
                OnHit();
            }
            else if (keyboard.sKey.wasPressedThisFrame)
            {
                OnStand();
            }

            return;
        }

        if (playerController == null || interactionPosition == null)
        {
            return;
        }

        Vector3 playerPosition = playerController.transform.position;
        Vector3 dealerPosition = interactionPosition.position;
        float distance = Vector2.Distance(
            new Vector2(playerPosition.x, playerPosition.z),
            new Vector2(dealerPosition.x, dealerPosition.z));
        bool inRange = distance <= interactionDistance;
        prompt.SetActive(inRange);

        if (inRange && keyboard != null && keyboard.eKey.wasPressedThisFrame)
        {
            OpenPanel();
        }
    }

    private void EnsureInputEventSystem()
    {
        EventSystem eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
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
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            throw new InvalidOperationException("Unity's LegacyRuntime.ttf font is required to render the blackjack interface.");
        }

        prompt = CreateUiObject("Interaction Prompt", canvas.transform);
        SetRect(prompt, new Vector2(0.5f, 0f), new Vector2(0f, 72f), new Vector2(460f, 58f));
        prompt.AddComponent<Image>().color = new Color(0.035f, 0.055f, 0.06f, 0.92f);
        CreateText("Prompt Text", prompt.transform, font, "Press E to Deal Blackjack", 25, Color.white, Vector2.zero, new Vector2(440f, 52f), TextAnchor.MiddleCenter);

        panel = CreateUiObject("Blackjack Panel", canvas.transform);
        SetRect(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 560f));
        panel.AddComponent<Image>().color = new Color(0.035f, 0.075f, 0.065f, 0.97f);

        CreateText("Title", panel.transform, font, "BLACKJACK", 34, new Color(0.95f, 0.75f, 0.35f), new Vector2(0f, 238f), new Vector2(560f, 48f), TextAnchor.MiddleCenter);
        CreateText("Dealer Label", panel.transform, font, "DEALER", 18, new Color(0.78f, 0.86f, 0.82f), new Vector2(0f, 174f), new Vector2(560f, 30f), TextAnchor.MiddleCenter);
        dealerCardsText = CreateText("Dealer Cards", panel.transform, font, "", 24, Color.white, new Vector2(0f, 128f), new Vector2(650f, 40f), TextAnchor.MiddleCenter);
        dealerTotalText = CreateText("Dealer Total", panel.transform, font, "", 18, Color.white, new Vector2(0f, 91f), new Vector2(560f, 30f), TextAnchor.MiddleCenter);
        CreateText("Player Label", panel.transform, font, "YOUR HAND", 18, new Color(0.78f, 0.86f, 0.82f), new Vector2(0f, 37f), new Vector2(560f, 30f), TextAnchor.MiddleCenter);
        playerCardsText = CreateText("Player Cards", panel.transform, font, "", 24, Color.white, new Vector2(0f, -8f), new Vector2(650f, 42f), TextAnchor.MiddleCenter);
        playerTotalText = CreateText("Player Total", panel.transform, font, "", 19, Color.white, new Vector2(0f, -48f), new Vector2(560f, 30f), TextAnchor.MiddleCenter);
        resultText = CreateText("Round Result", panel.transform, font, "", 26, new Color(0.98f, 0.78f, 0.38f), new Vector2(0f, -103f), new Vector2(560f, 42f), TextAnchor.MiddleCenter);
        CreateText("Instructions", panel.transform, font, "H = Hit    S = Stand    Esc = Leave table", 16, new Color(0.72f, 0.78f, 0.74f), new Vector2(0f, -250f), new Vector2(560f, 28f), TextAnchor.MiddleCenter);

        hitButton = CreateButton("Hit Button", panel.transform, font, "Hit (H)", new Vector2(-170f, -184f), new Vector2(145f, 48f), OnHit);
        standButton = CreateButton("Stand Button", panel.transform, font, "Stand (S)", new Vector2(0f, -184f), new Vector2(145f, 48f), OnStand);
        newRoundButton = CreateButton("New Round Button", panel.transform, font, "New Round", new Vector2(170f, -184f), new Vector2(145f, 48f), StartRound);
        CreateButton("Close Button", panel.transform, font, "Close", new Vector2(310f, 238f), new Vector2(82f, 38f), ClosePanel);

        panel.SetActive(false);
    }

    private void OpenPanel()
    {
        panelOpen = true;
        prompt.SetActive(false);
        panel.SetActive(true);
        playerController.SetMovementEnabled(false);
        StartRound();
    }

    private void ClosePanel()
    {
        panelOpen = false;
        panel.SetActive(false);
        playerController.SetMovementEnabled(true);
    }

    private void StartRound()
    {
        round = new BlackjackRound();
        RefreshInterface();
    }

    private void OnHit()
    {
        if (round != null)
        {
            round.Hit();
            RefreshInterface();
        }
    }

    private void OnStand()
    {
        if (round != null)
        {
            round.Stand();
            RefreshInterface();
        }
    }

    private void RefreshInterface()
    {
        if (round == null || dealerCardsText == null)
        {
            return;
        }

        bool revealDealerHand = round.IsComplete;
        dealerCardsText.text = FormatCards(round.DealerCards, revealDealerHand);
        dealerTotalText.text = revealDealerHand ? $"Dealer total: {round.DealerTotal}" : "Dealer total: ?";
        playerCardsText.text = FormatCards(round.PlayerCards, true);
        playerTotalText.text = $"Your total: {round.PlayerTotal}";
        resultText.text = round.IsComplete ? GetOutcomeText(round.Outcome) : "";

        hitButton.interactable = !round.IsComplete;
        standButton.interactable = !round.IsComplete;
        newRoundButton.gameObject.SetActive(round.IsComplete);
    }

    private static string FormatCards(IReadOnlyList<BlackjackCard> cards, bool revealAll)
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

    private Button CreateButton(string name, Transform parent, Font font, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = CreateUiObject(name, parent);
        SetRect(buttonObject, new Vector2(0.5f, 0.5f), position, size);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.38f, 0.23f, 0.09f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);
        CreateText("Label", buttonObject.transform, font, label, 19, Color.white, Vector2.zero, size, TextAnchor.MiddleCenter);
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
