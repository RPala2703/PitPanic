using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class FlickCardMinigame : CasinoMinigame
{
    [SerializeField, Min(0.2f)] private float timeAllowance = 10f;
    [SerializeField, Min(0.2f)] private float targetHalfWidth = 0.55f;
    [SerializeField, Min(0.2f)] private float targetHalfDepth = 0.48f;
    [SerializeField, Min(0.05f)] private float cardFlightSeconds = 0.24f;
    [SerializeField, Min(0f)] private float flickArcHeight = 0.42f;

    private static readonly string[] SuitSymbols = { "♠", "♥", "♦", "♣" };
    private readonly List<GameObject> targetMarkers = new List<GameObject>();
    private readonly List<GameObject> dealtCards = new List<GameObject>();
    [SerializeField] private BlackjackTableInteraction table;
    [SerializeField] private CasinoPlayerController player;
    [SerializeField] private Transform tableRoot;
    private Canvas hud;
    private Text progressText;
    private Text timerText;
    private Text feedbackText;
    private LineRenderer aimGuide;
    private GameObject shoeVisual;
    private GameObject heldCard;
    private GameObject flyingCard;
    private Vector3 mouseWorld;
    private Vector3 flightDestination;
    private Coroutine flightRoutine;
    private float timeRemaining;
    private float completionDelay;
    private int successfulFlicks;
    private int missedFlicks;
    private bool isDragging;
    private bool waitingForFlight;
    private bool completing;
    private bool completionSuccess;
    private bool flightIsDeal;
    private int requiredFlicks;
    private Vector3 savedCursorPosition;
    private CursorLockMode savedCursorLockMode;
    private bool savedCursorVisible;
    private Material cardMaterial;
    private Material guideMaterial;

    public override bool CanBegin => table != null && table.IsFlickReady &&
                                     Mouse.current != null && Camera.main != null;
    public int SuccessfulFlicks => successfulFlicks;
    public int MissedFlicks => missedFlicks;
    public bool IsConfigured => table != null && player != null && tableRoot != null;
    public bool IsDraggingCard => isDragging;
    public bool IsWaitingForCardFlight => waitingForFlight;
    public static int RemainingOpeningCards(int activeHands) => activeHands <= 0 ? 0 : activeHands + 1;
    public static int OpeningTargetForStep(int activeHands, int dealStep)
    {
        if (activeHands <= 0 || dealStep < 0 || dealStep >= activeHands * 2 + 2)
        {
            return -2;
        }

        if (dealStep < activeHands)
        {
            return dealStep;
        }

        if (dealStep == activeHands || dealStep > activeHands * 2)
        {
            return -1;
        }

        return dealStep - activeHands - 1;
    }

    public static bool IsValidFlickTarget(int targetIndex, int expectedTarget, bool cardInFlight)
    {
        return !cardInFlight && expectedTarget != -2 && targetIndex == expectedTarget;
    }

    public static bool TryProjectScreenPoint(
        Camera camera,
        Transform table,
        Vector2 screenPosition,
        out Vector3 worldPosition)
    {
        worldPosition = default;
        if (camera == null || table == null)
        {
            return false;
        }

        Plane tablePlane = new Plane(Vector3.up, new Vector3(0f, table.position.y + 1.53f, 0f));
        Ray ray = camera.ScreenPointToRay(screenPosition);
        if (!tablePlane.Raycast(ray, out float distance))
        {
            return false;
        }

        worldPosition = ray.GetPoint(distance);
        return true;
    }

    public void Configure(
        BlackjackTableInteraction blackjackTable,
        CasinoPlayerController casinoPlayer,
        Transform blackjackTableRoot)
    {
        table = blackjackTable;
        player = casinoPlayer;
        tableRoot = blackjackTableRoot;
    }

    private void OnDestroy()
    {
        if (IsRunning)
        {
            Abandon();
        }
        else
        {
            DestroyHeldCard();
            CleanUpStaging();
        }

        ClearPhysicalCards();
    }

    protected override void OnBegin()
    {
        if (!table.TryBeginFlickAssistance())
        {
            Abandon();
            return;
        }

        requiredFlicks = RemainingOpeningCards(table.ActiveRound.CustomerHands.Count);
        timeRemaining = timeAllowance;
        successfulFlicks = 0;
        missedFlicks = 0;
        isDragging = false;
        waitingForFlight = false;
        completing = false;
        completionSuccess = false;
        completionDelay = 0f;
        savedCursorPosition = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        savedCursorLockMode = Cursor.lockState;
        savedCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        player.SetMovementEnabled(false);

        CreateCardMaterials();
        CreateShoesAndTargets();
        CreateHud();
        RenderExistingCards();
        UpdateTargetHighlight();
        SetFeedback("GRAB THE SHOE, AIM AT THE GLOWING BOX, THEN FLICK!  ESC TO CANCEL");
        aimGuide.enabled = true;
    }

    protected override void OnTick(float deltaTime)
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Abandon();
            return;
        }

        if (completing)
        {
            completionDelay -= deltaTime;
            if (completionDelay <= 0f)
            {
                Finish(completionSuccess);
            }

            return;
        }

        if (!waitingForFlight)
        {
            timeRemaining -= deltaTime;
            UpdateMouseAim();
            UpdateDragInput();
            UpdateAimGuide();
            UpdateTargetHighlight();
        }

        UpdateHud();
        if (timeRemaining <= 0f && !waitingForFlight)
        {
            SetFeedback($"TIME! {successfulFlicks}/{requiredFlicks} CARDS");
            completing = true;
            completionSuccess = false;
            completionDelay = 0.85f;
            aimGuide.enabled = false;
        }
    }

    protected override void OnAbandon()
    {
        if (flightRoutine != null)
        {
            StopCoroutine(flightRoutine);
            flightRoutine = null;
        }

        if (flightIsDeal && flyingCard != null)
        {
            flyingCard.transform.position = flightDestination;
            flyingCard.transform.rotation = tableRoot.rotation;
            flyingCard = null;
        }
        else if (flyingCard != null)
        {
            Destroy(flyingCard);
            flyingCard = null;
        }

        DestroyHeldCard();
        RestorePlayerAndCursor();
        CleanUpVisuals();
        if (table != null)
        {
            table.EndFlickAssistance(cancelled: true);
        }
    }

    public void AimAtScreenPosition(Vector2 screenPosition)
    {
        Camera camera = Camera.main;
        if (camera == null || tableRoot == null)
        {
            return;
        }

        if (TryProjectScreenPoint(camera, tableRoot, screenPosition, out Vector3 projectedPosition))
        {
            mouseWorld = projectedPosition;
        }
    }

    private void UpdateMouseAim()
    {
        if (Mouse.current == null)
        {
            return;
        }

        AimAtScreenPosition(Mouse.current.position.ReadValue());
    }

    private void UpdateDragInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame && !isDragging &&
            IsPointerOnShoe(mouseWorld))
        {
            isDragging = true;
            heldCard = CreatePhysicalCard(new BlackjackCard(1, 0), faceDown: true);
            heldCard.transform.position = ShoePosition + Vector3.up * 0.055f;
            SetFeedback("FLICK TO THE LIT TARGET!");
        }

        if (isDragging && heldCard != null)
        {
            heldCard.transform.position = mouseWorld + Vector3.up * 0.07f;
        }

        if (mouse.leftButton.wasReleasedThisFrame && isDragging)
        {
            isDragging = false;
            TryReleaseCard(mouseWorld);
        }
    }

    private void TryReleaseCard(Vector3 releasePosition)
    {
        int expectedIndex = table.GetNextFlickTarget();
        int targetIndex = FindTarget(releasePosition);
        if (!IsValidFlickTarget(targetIndex, expectedIndex, waitingForFlight || completing))
        {
            missedFlicks++;
            timeRemaining = Mathf.Max(0f, timeRemaining - 1.15f);
            if (targetIndex >= 0)
            {
                SetFeedback("WRONG BOX! AIM AT THE GLOWING TARGET.");
            }
            else
            {
                SetFeedback("WIDE! BRING IT BACK AND FLICK AGAIN.");
            }

            waitingForFlight = true;
            flightIsDeal = false;
            flyingCard = heldCard;
            heldCard = null;
            Vector3 direction = releasePosition - ShoePosition;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
            {
                direction = Vector3.forward;
            }

            flightDestination = releasePosition + direction.normalized * (targetIndex >= 0 ? 0.18f : 0.42f);
            flightRoutine = StartCoroutine(AnimateFlight(ShoePosition, flightDestination, returningMiss: true));
            return;
        }

        if (!table.TryDealFlickCard(targetIndex, out BlackjackCard card, out int cardIndex, out bool holeCard))
        {
            SetFeedback("DEAL PAUSED — RETRY THE FLICK.");
            ReturnHeldCard();
            return;
        }

        successfulFlicks++;
        waitingForFlight = true;
        flightIsDeal = true;
        flyingCard = heldCard;
        heldCard = null;
        SetCardFace(flyingCard, card, faceDown: holeCard);
        flightDestination = GetCardDestination(targetIndex, cardIndex);
        dealtCards.Add(flyingCard);
        SetFeedback(targetIndex < 0 ? "DEALER!" : $"NICE DEAL — SEAT {targetIndex + 1}!");
        flightRoutine = StartCoroutine(AnimateFlight(ShoePosition, flightDestination, returningMiss: false));
    }

    private bool IsPointerOnShoe(Vector3 position)
    {
        Vector3 localPointer = tableRoot.InverseTransformPoint(position);
        Vector3 localShoe = tableRoot.InverseTransformPoint(ShoePosition);
        return new Vector2(localPointer.x - localShoe.x, localPointer.z - localShoe.z).sqrMagnitude <= 0.95f * 0.95f;
    }

    private void ReturnHeldCard()
    {
        DestroyHeldCard();
    }

    private IEnumerator AnimateFlight(Vector3 start, Vector3 end, bool returningMiss)
    {
        float elapsed = 0f;
        Vector3 initial = flyingCard.transform.position;
        float duration = returningMiss ? cardFlightSeconds * 1.5f : cardFlightSeconds;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float arc = Mathf.Sin(t * Mathf.PI) * flickArcHeight;
            Vector3 destination = returningMiss ? end : Vector3.Lerp(start, end, t);
            Vector3 position = returningMiss
                ? Vector3.Lerp(initial, end, t)
                : destination + Vector3.up * arc;
            flyingCard.transform.position = position;
            flyingCard.transform.rotation = Quaternion.Slerp(
                Quaternion.Euler(0f, 0f, -12f),
                tableRoot.rotation,
                t);
            yield return null;
        }

        if (returningMiss)
        {
            elapsed = 0f;
            Vector3 missPosition = flyingCard.transform.position;
            Vector3 shoe = ShoePosition + Vector3.up * 0.045f;
            while (elapsed < 0.14f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.14f);
                flyingCard.transform.position = Vector3.Lerp(missPosition, shoe, t);
                yield return null;
            }

            Destroy(flyingCard);
            flyingCard = null;
        }
        else
        {
            flyingCard.transform.position = end;
            flyingCard.transform.rotation = tableRoot.rotation;
            if (successfulFlicks >= requiredFlicks)
            {
                completing = true;
                completionSuccess = true;
                completionDelay = 0.55f;
                aimGuide.enabled = false;
                SetFeedback($"PERFECT DEAL!  {successfulFlicks} FLICKS");
            }

            flyingCard = null;
        }

        waitingForFlight = false;
        flightRoutine = null;
        UpdateTargetHighlight();
    }

    private void CreateShoesAndTargets()
    {
        shoeVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shoeVisual.name = "FLICK Card Shoe";
        shoeVisual.transform.SetParent(tableRoot, false);
        shoeVisual.transform.SetLocalPositionAndRotation(new Vector3(0f, 1.53f, 0.96f), Quaternion.identity);
        shoeVisual.transform.localScale = new Vector3(0.48f, 0.32f, 0.68f);
        Destroy(shoeVisual.GetComponent<Collider>());
        shoeVisual.GetComponent<Renderer>().sharedMaterial = cardMaterial;
        SetColor(shoeVisual.GetComponent<Renderer>(), new Color(0.08f, 0.16f, 0.28f));
        GameObject shoeTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shoeTop.name = "Shoe Slot";
        shoeTop.transform.SetParent(shoeVisual.transform, false);
        shoeTop.transform.localPosition = new Vector3(0f, 0.52f, -0.08f);
        shoeTop.transform.localScale = new Vector3(0.72f, 0.035f, 0.26f);
        Destroy(shoeTop.GetComponent<Collider>());
        shoeTop.GetComponent<Renderer>().sharedMaterial = cardMaterial;
        SetColor(shoeTop.GetComponent<Renderer>(), new Color(0.77f, 0.62f, 0.29f));

        int targetCount = table.ActiveRound.CustomerHands.Count + 1;
        for (int i = 0; i < targetCount; i++)
        {
            int targetId = i == targetCount - 1 ? -1 : i;
            Vector3 position = table.GetFlickTargetPosition(targetId);
            GameObject marker = new GameObject(targetId < 0 ? "FLICK Dealer Target" : $"FLICK Seat Target {targetId + 1}");
            marker.transform.SetParent(tableRoot, false);
            marker.transform.position = position + Vector3.up * 0.055f;
            CreateTargetEdge(marker.transform, new Vector3(0f, 0f, targetHalfDepth), new Vector3(targetHalfWidth * 2f, 0.025f, 0.055f));
            CreateTargetEdge(marker.transform, new Vector3(0f, 0f, -targetHalfDepth), new Vector3(targetHalfWidth * 2f, 0.025f, 0.055f));
            CreateTargetEdge(marker.transform, new Vector3(targetHalfWidth, 0f, 0f), new Vector3(0.055f, 0.025f, targetHalfDepth * 2f));
            CreateTargetEdge(marker.transform, new Vector3(-targetHalfWidth, 0f, 0f), new Vector3(0.055f, 0.025f, targetHalfDepth * 2f));
            targetMarkers.Add(marker);
        }

        GameObject guideObject = new GameObject("FLICK Aim Trajectory");
        guideObject.transform.SetParent(tableRoot, false);
        aimGuide = guideObject.AddComponent<LineRenderer>();
        aimGuide.positionCount = 13;
        aimGuide.useWorldSpace = true;
        aimGuide.startWidth = 0.035f;
        aimGuide.endWidth = 0.012f;
        aimGuide.material = guideMaterial;
        aimGuide.startColor = new Color(1f, 0.86f, 0.24f, 0.95f);
        aimGuide.endColor = new Color(0.42f, 1f, 0.58f, 0.25f);
        aimGuide.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        aimGuide.receiveShadows = false;
    }

    private void CreateTargetEdge(Transform parent, Vector3 localPosition, Vector3 localScale)
    {
        GameObject edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
        edge.name = "Target Highlight";
        edge.transform.SetParent(parent, false);
        edge.transform.localPosition = localPosition;
        edge.transform.localScale = localScale;
        Destroy(edge.GetComponent<Collider>());
        edge.GetComponent<Renderer>().sharedMaterial = cardMaterial;
        SetColor(edge.GetComponent<Renderer>(), new Color(0.20f, 0.85f, 0.48f));
    }

    private void CreateHud()
    {
        GameObject canvasObject = new GameObject(
            "FLICK HUD",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        hud = canvasObject.GetComponent<Canvas>();
        hud.renderMode = RenderMode.ScreenSpaceOverlay;
        hud.sortingOrder = 60;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        progressText = CreateHudText(canvasObject.transform, "FLICK  0/0", new Vector2(0f, -34f), new Vector2(420f, 36f), 25);
        timerText = CreateHudText(canvasObject.transform, "TIME  10.0", new Vector2(0f, -70f), new Vector2(340f, 28f), 19);
        feedbackText = CreateHudText(canvasObject.transform, "", new Vector2(0f, -112f), new Vector2(650f, 34f), 20);
    }

    private static Text CreateHudText(Transform parent, string value, Vector2 position, Vector2 size, int fontSize)
    {
        GameObject textObject = new GameObject(value.Length == 0 ? "FLICK Feedback" : "FLICK Status", typeof(RectTransform), typeof(Text), typeof(Outline));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = rect.anchorMin;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.color = value.StartsWith("TIME") ? new Color(1f, 0.86f, 0.35f) : Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        Outline outline = textObject.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.88f);
        outline.effectDistance = new Vector2(2f, -2f);
        return text;
    }

    private void RenderExistingCards()
    {
        BlackjackTableRound round = table.ActiveRound;
        for (int handIndex = 0; handIndex < round.CustomerHands.Count; handIndex++)
        {
            BlackjackHand hand = round.CustomerHands[handIndex];
            for (int cardIndex = 0; cardIndex < hand.Cards.Count; cardIndex++)
            {
                GameObject card = CreatePhysicalCard(hand.Cards[cardIndex], faceDown: false);
                card.transform.position = GetCardDestination(handIndex, cardIndex);
                card.transform.rotation = tableRoot.rotation;
                dealtCards.Add(card);
            }
        }

        for (int cardIndex = 0; cardIndex < round.DealerCards.Count; cardIndex++)
        {
            bool hidden = cardIndex == 1;
            GameObject card = CreatePhysicalCard(round.DealerCards[cardIndex], hidden);
            card.transform.position = GetCardDestination(-1, cardIndex);
            card.transform.rotation = tableRoot.rotation;
            dealtCards.Add(card);
        }
    }

    private GameObject CreatePhysicalCard(BlackjackCard card, bool faceDown)
    {
        GameObject cardObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cardObject.name = faceDown ? "FLICK Card Back" : $"FLICK {card}";
        cardObject.transform.SetParent(tableRoot, true);
        cardObject.transform.localScale = new Vector3(0.48f, 0.035f, 0.68f);
        Destroy(cardObject.GetComponent<Collider>());
        cardObject.GetComponent<Renderer>().sharedMaterial = cardMaterial;
        SetColor(cardObject.GetComponent<Renderer>(), faceDown ? new Color(0.10f, 0.19f, 0.30f) : new Color(0.97f, 0.96f, 0.89f));
        if (!faceDown)
        {
            TextMesh face = cardObject.AddComponent<TextMesh>();
            face.text = FormatCard(card);
            face.anchor = TextAnchor.MiddleCenter;
            face.alignment = TextAlignment.Center;
            face.fontSize = 80;
            face.characterSize = 0.015f;
            face.color = card.Suit == 1 || card.Suit == 2 ? new Color(0.75f, 0.08f, 0.08f) : new Color(0.08f, 0.10f, 0.12f);
            face.transform.localPosition = new Vector3(0f, 0.53f, 0f);
            face.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            face.transform.localScale = new Vector3(1f / 0.48f, 1f / 0.035f, 1f / 0.68f);
        }

        return cardObject;
    }

    private void SetCardFace(GameObject cardObject, BlackjackCard card, bool faceDown)
    {
        Renderer renderer = cardObject.GetComponent<Renderer>();
        SetColor(renderer, faceDown ? new Color(0.10f, 0.19f, 0.30f) : new Color(0.97f, 0.96f, 0.89f));
        TextMesh face = cardObject.GetComponent<TextMesh>();
        if (faceDown)
        {
            if (face != null)
            {
                Destroy(face);
            }

            return;
        }

        if (face == null)
        {
            face = cardObject.AddComponent<TextMesh>();
            face.anchor = TextAnchor.MiddleCenter;
            face.alignment = TextAlignment.Center;
            face.fontSize = 80;
            face.characterSize = 0.015f;
            face.transform.localPosition = new Vector3(0f, 0.53f, 0f);
            face.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            face.transform.localScale = new Vector3(1f / 0.48f, 1f / 0.035f, 1f / 0.68f);
        }

        face.text = FormatCard(card);
        face.color = card.Suit == 1 || card.Suit == 2 ? new Color(0.75f, 0.08f, 0.08f) : new Color(0.08f, 0.10f, 0.12f);
    }

    private Vector3 GetCardDestination(int targetId, int cardIndex)
    {
        Vector3 center = table.GetFlickTargetPosition(targetId);
        int cardCount = targetId < 0 ? table.ActiveRound.DealerCards.Count : table.ActiveRound.CustomerHands[targetId].Cards.Count;
        float offset = (cardIndex - (cardCount - 1) * 0.5f) * 0.34f;
        center += tableRoot.right * offset;
        center.y = tableRoot.position.y + 1.53f + cardIndex * 0.005f;
        return center;
    }

    private int FindTarget(Vector3 position)
    {
        BlackjackTableRound round = table.ActiveRound;
        for (int i = 0; i <= round.CustomerHands.Count; i++)
        {
            int targetId = i == round.CustomerHands.Count ? -1 : i;
            Vector3 target = table.GetFlickTargetPosition(targetId);
            Vector3 delta = position - target;
            delta.y = 0f;
            Vector3 localDelta = tableRoot.InverseTransformVector(delta);
            if (Mathf.Abs(localDelta.x) <= targetHalfWidth && Mathf.Abs(localDelta.z) <= targetHalfDepth)
            {
                return targetId;
            }
        }

        return -2;
    }

    private void UpdateAimGuide()
    {
        if (aimGuide == null)
        {
            return;
        }

        Vector3 start = ShoePosition + Vector3.up * 0.09f;
        Vector3 end = mouseWorld;
        for (int i = 0; i < aimGuide.positionCount; i++)
        {
            float t = i / (float)(aimGuide.positionCount - 1);
            aimGuide.SetPosition(i, Vector3.Lerp(start, end, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * flickArcHeight));
        }
    }

    private void UpdateTargetHighlight()
    {
        if (targetMarkers.Count == 0 || table.ActiveRound == null)
        {
            return;
        }

        int expected = table.GetNextFlickTarget();
        float pulse = 1f + Mathf.Sin(Time.time * 8f) * 0.04f;
        for (int i = 0; i < targetMarkers.Count; i++)
        {
            GameObject marker = targetMarkers[i];
            int targetId = i == targetMarkers.Count - 1 ? -1 : i;
            bool active = targetId == expected;
            marker.transform.localScale = Vector3.one * (active ? pulse : 1f);
            Color color = active ? new Color(1f, 0.84f, 0.24f) : new Color(0.18f, 0.70f, 0.38f);
            foreach (Renderer renderer in marker.GetComponentsInChildren<Renderer>())
            {
                SetColor(renderer, color);
            }
        }
    }

    private void UpdateHud()
    {
        if (progressText == null)
        {
            return;
        }

        progressText.text = $"FLICK!   {successfulFlicks}/{requiredFlicks}     MISSES {missedFlicks}";
        timerText.text = $"TIME  {Mathf.Max(0f, timeRemaining):0.0}";
    }

    private void SetFeedback(string value)
    {
        if (feedbackText != null)
        {
            feedbackText.text = value;
            feedbackText.color = value.StartsWith("WRONG") || value.StartsWith("WIDE") || value.StartsWith("TIME")
                ? new Color(1f, 0.48f, 0.36f)
                : new Color(1f, 0.88f, 0.43f);
        }
    }

    private void Finish(bool success)
    {
        if (success)
        {
            table.EndFlickAssistance();
        }
        else
        {
            table.NotifyFlickAttemptFailed();
        }

        RestorePlayerAndCursor();
        CleanUpStaging();
        int score = Mathf.Max(0, successfulFlicks * 100 + Mathf.RoundToInt(timeRemaining * 10f) - missedFlicks * 25);
        Complete(success, score);
    }

    private void RestorePlayerAndCursor()
    {
        if (player != null)
        {
            player.SetMovementEnabled(true);
        }

        Cursor.lockState = savedCursorLockMode;
        Cursor.visible = savedCursorVisible;
    }

    private void CleanUpVisuals()
    {
        CleanUpStaging();
    }

    private void CleanUpStaging()
    {
        CleanUpTargetsAndHud();
        if (shoeVisual != null)
        {
            Destroy(shoeVisual);
            shoeVisual = null;
        }

        if (aimGuide != null)
        {
            Destroy(aimGuide.gameObject);
            aimGuide = null;
        }
    }

    public void ClearPhysicalCards()
    {
        foreach (GameObject card in dealtCards)
        {
            if (card != null)
            {
                Destroy(card);
            }
        }

        dealtCards.Clear();
        if (cardMaterial != null)
        {
            Destroy(cardMaterial);
            cardMaterial = null;
        }

        if (guideMaterial != null)
        {
            Destroy(guideMaterial);
            guideMaterial = null;
        }
    }

    private void CleanUpTargetsAndHud()
    {
        foreach (GameObject marker in targetMarkers)
        {
            if (marker != null)
            {
                Destroy(marker);
            }
        }

        targetMarkers.Clear();
        if (hud != null)
        {
            Destroy(hud.gameObject);
            hud = null;
            progressText = null;
            timerText = null;
            feedbackText = null;
        }
    }

    private void DestroyHeldCard()
    {
        if (heldCard != null)
        {
            Destroy(heldCard);
            heldCard = null;
        }

        isDragging = false;
    }

    private Vector3 ShoePosition => tableRoot.TransformPoint(new Vector3(0f, 1.68f, 0.96f));

    private static string FormatCard(BlackjackCard card)
    {
        string rank = card.Rank == 1 ? "A" : card.Rank > 10 ? "JQK"[card.Rank - 11].ToString() : card.Rank.ToString();
        return $"{rank}{SuitSymbols[card.Suit]}";
    }

    private static void SetColor(Renderer renderer, Color color)
    {
        if (renderer == null)
        {
            return;
        }

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        renderer.SetPropertyBlock(block);
    }

    private void CreateCardMaterials()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null)
        {
            throw new System.InvalidOperationException("FLICK could not find a supported 3D material shader.");
        }

        cardMaterial = new Material(shader) { color = Color.white };
        guideMaterial = new Material(Shader.Find("Sprites/Default") ?? shader);
    }
}
