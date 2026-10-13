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
    [SerializeField, Min(0.24f)] private float shoeHitHalfWidth = 0.55f;
    [SerializeField, Min(0.34f)] private float shoeHitHalfDepth = 0.68f;
    [SerializeField, Min(0.05f)] private float cardFlightSeconds = 0.24f;
    [SerializeField, Min(0f)] private float flickArcHeight = 0.42f;
    [SerializeField] private bool showInputDiagnostics;

    private static readonly string[] SuitSymbols = { "♠", "♥", "♦", "♣" };
    private const float ShoeSurfaceHeight = 1.53f;
    private const float ShoeLocalDepth = 0.96f;
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
    private GameObject pointerDebugMarker;
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
    private bool hasMouseProjection;
    private bool controlsCaptured;
    private int requiredFlicks;
    private Vector3 savedCursorPosition;
    private CursorLockMode savedCursorLockMode;
    private bool savedCursorVisible;
    private Material cardMaterial;
    private Material guideMaterial;

    public override bool CanBegin => table != null && table.IsFlickReady &&
                                     table.ActiveRound != null && table.RemainingFlickCardCount > 0 &&
                                     player != null && tableRoot != null &&
                                     Mouse.current != null && Camera.main != null;
    protected override string PresentationTitle => "FLICK! DEAL THE OPENING HANDS";
    protected override string PresentationInstructions =>
        "Grab a card from the shoe, aim at the glowing hand, and release to deal.";
    public int SuccessfulFlicks => successfulFlicks;
    public int MissedFlicks => missedFlicks;
    public bool IsConfigured => table != null && player != null && tableRoot != null;
    public bool IsDraggingCard => isDragging;
    public bool IsWaitingForCardFlight => waitingForFlight;
    public static int RemainingOpeningCards(int activeHands) => activeHands <= 0 ? 0 : activeHands + 1;
    public static int RemainingOpeningCards(int activeHands, int dealStep) =>
        activeHands <= 0 || dealStep < 0
            ? 0
            : Mathf.Clamp(activeHands * 2 + 2 - dealStep, 0, activeHands * 2 + 2);

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

    public static bool CanCompleteChallenge(int successfulDeals, int requiredDeals, int remainingDeals)
    {
        return requiredDeals > 0 && successfulDeals == requiredDeals && remainingDeals == 0;
    }

    public static bool IsPointerWithinShoe(
        Vector3 localPointer,
        Vector3 localShoeCenter,
        float halfWidth,
        float halfDepth)
    {
        return halfWidth > 0f && halfDepth > 0f &&
               Mathf.Abs(localPointer.x - localShoeCenter.x) <= halfWidth &&
               Mathf.Abs(localPointer.z - localShoeCenter.z) <= halfDepth;
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

        Vector3 shoeSurface = table.TransformPoint(new Vector3(0f, ShoeSurfaceHeight, ShoeLocalDepth));
        Plane tablePlane = new Plane(table.up, shoeSurface);
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
            if (HasCompletedRequiredDeals())
            {
                CompleteCommittedChallenge();
            }
            else
            {
                Abandon();
            }
        }
        else
        {
            DestroyHeldCard();
            CleanUpStaging();
        }

        ClearPhysicalCards();
    }

    private void OnDisable()
    {
        if (IsRunning)
        {
            if (HasCompletedRequiredDeals())
            {
                CompleteCommittedChallenge();
            }
            else
            {
                Abandon();
            }
        }
    }

    protected override void OnBegin()
    {
        if (!CanBegin || !table.TryPrepareFlickAssistance())
        {
            Abandon();
            return;
        }

        savedCursorPosition = Mouse.current.position.ReadValue();
        savedCursorLockMode = Cursor.lockState;
        savedCursorVisible = Cursor.visible;
        try
        {
            requiredFlicks = table.RemainingFlickCardCount;
            timeRemaining = timeAllowance;
            successfulFlicks = 0;
            missedFlicks = 0;
            isDragging = false;
            waitingForFlight = false;
            completing = false;
            completionSuccess = false;
            flightIsDeal = false;
            flightRoutine = null;
            hasMouseProjection = false;
            completionDelay = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            controlsCaptured = true;
            player.SetMovementEnabled(false);

            ClearPhysicalCards();
            CreateCardMaterials();
            CreateShoesAndTargets();
            CreateHud();
            RenderExistingCards();
            UpdateTargetHighlight();
            SetFeedback("GRAB THE SHOE, AIM AT THE GLOWING BOX, THEN FLICK!  ESC TO CANCEL");
            if (aimGuide == null)
            {
                throw new System.InvalidOperationException("FLICK could not create the aiming guide.");
            }

            aimGuide.enabled = true;
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"{name}: FLICK could not initialize its visuals and will resume automated dealing. {exception}", this);
            RestorePlayerAndCursor();
            CleanUpVisuals();
            ClearPhysicalCards();
            table.AbortFlickAssistance();
            Complete(false, 0);
        }
    }

    protected override void OnTick(float deltaTime)
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (HasCompletedRequiredDeals())
            {
                CompleteCommittedChallenge();
            }
            else
            {
                Abandon();
            }

            return;
        }

        if (table == null || player == null || tableRoot == null)
        {
            AbortUnrecoverableChallenge("the table, player, or table root was destroyed");
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
            if (aimGuide != null)
            {
                aimGuide.enabled = false;
            }
        }
    }

    protected override void OnAbandon()
    {
        if (flightRoutine != null)
        {
            StopCoroutine(flightRoutine);
            flightRoutine = null;
        }

        if (flightIsDeal && flyingCard != null && tableRoot != null)
        {
            if (!TrySetCardPose(flyingCard, flightDestination, tableRoot.rotation))
            {
                Debug.LogWarning($"{name}: A dealt card visual was lost during cancellation; the blackjack round still retains the dealt card.", this);
            }
        }
        else if (flyingCard != null)
        {
            Destroy(flyingCard);
        }

        flyingCard = null;
        waitingForFlight = false;
        flightIsDeal = false;
        flightRoutine = null;
        DestroyHeldCard();
        RestorePlayerAndCursor();
        CleanUpVisuals();
        if (table != null)
        {
            table.NotifyFlickAttemptFailed();
        }
    }

    public void AimAtScreenPosition(Vector2 screenPosition)
    {
        Camera camera = Camera.main;
        if (camera == null || tableRoot == null)
        {
            hasMouseProjection = false;
            return;
        }

        if (TryProjectScreenPoint(camera, tableRoot, screenPosition, out Vector3 projectedPosition))
        {
            mouseWorld = projectedPosition;
            hasMouseProjection = true;
        }
        else
        {
            hasMouseProjection = false;
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

        if (showInputDiagnostics && pointerDebugMarker != null && hasMouseProjection)
        {
            pointerDebugMarker.transform.position = mouseWorld + tableRoot.up * 0.045f;
        }

        if (mouse.leftButton.wasPressedThisFrame && !isDragging)
        {
            bool pointerOnShoe = hasMouseProjection && IsPointerOnShoe(mouseWorld);
            if (pointerOnShoe)
            {
                heldCard = CreatePhysicalCard(new BlackjackCard(1, 0), faceDown: true);
                if (heldCard != null)
                {
                    isDragging = true;
                    heldCard.transform.position = ShoePosition + tableRoot.up * 0.055f;
                    SetFeedback("FLICK TO THE LIT TARGET!");
                }
                else
                {
                    SetFeedback("CARD VISUAL UNAVAILABLE — TRY AGAIN.");
                }
            }

            if (showInputDiagnostics)
            {
                Vector3 localPointer = hasMouseProjection
                    ? tableRoot.InverseTransformPoint(mouseWorld)
                    : Vector3.zero;
                Debug.Log(
                    $"{name}: FLICK mouse press {(pointerOnShoe ? "grabbed the shoe" : "missed the shoe")}; " +
                    $"projected={hasMouseProjection}, local=({localPointer.x:0.00}, {localPointer.z:0.00}).",
                    this);
            }
        }

        if (isDragging && heldCard != null && hasMouseProjection)
        {
            heldCard.transform.position = mouseWorld + tableRoot.up * 0.07f;
        }

        if (mouse.leftButton.wasReleasedThisFrame && isDragging)
        {
            isDragging = false;
            if (hasMouseProjection)
            {
                TryReleaseCard(mouseWorld);
            }
            else
            {
                ReturnHeldCard();
                SetFeedback("AIM LOST — GRAB A CARD AND RETRY.");
            }
        }
    }

    private void TryReleaseCard(Vector3 releasePosition)
    {
        if (!IsRunning || waitingForFlight || completing || table == null ||
            tableRoot == null || heldCard == null)
        {
            ReturnHeldCard();
            return;
        }

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

            Vector3 direction = releasePosition - ShoePosition;
            direction = Vector3.ProjectOnPlane(direction, tableRoot.up);
            if (direction.sqrMagnitude < 0.01f)
            {
                direction = tableRoot.forward;
            }

            flightDestination = releasePosition + direction.normalized * (targetIndex >= 0 ? 0.18f : 0.42f);
            GameObject missedCard = heldCard;
            heldCard = null;
            if (!TryStartFlight(missedCard, ShoePosition, flightDestination, returningMiss: true))
            {
                RecoverFlightVisualFailure(missedCard, returningMiss: true, "starting a missed-card animation");
            }

            return;
        }

        if (!IsCardVisualReady(heldCard))
        {
            ReturnHeldCard();
            SetFeedback("CARD VISUAL UNAVAILABLE — RETRY THE DEAL.");
            return;
        }

        if (!table.TryDealFlickCard(targetIndex, out BlackjackCard card, out int cardIndex, out bool holeCard))
        {
            SetFeedback("DEAL PAUSED — RETRY THE FLICK.");
            ReturnHeldCard();
            return;
        }

        successfulFlicks++;
        GameObject dealtCard = heldCard;
        heldCard = null;
        if (!TrySetCardFace(dealtCard, card, faceDown: holeCard))
        {
            Debug.LogWarning($"{name}: The dealt card face could not be rendered; keeping the card back while preserving the dealt card.", this);
        }

        flightDestination = GetCardDestination(targetIndex, cardIndex);
        if (!dealtCards.Contains(dealtCard))
        {
            dealtCards.Add(dealtCard);
        }

        SetFeedback(targetIndex < 0 ? "DEALER!" : $"NICE DEAL — SEAT {targetIndex + 1}!");
        if (!TryStartFlight(dealtCard, ShoePosition, flightDestination, returningMiss: false))
        {
            RecoverFlightVisualFailure(dealtCard, returningMiss: false, "starting a dealt-card animation");
        }
    }

    private bool IsPointerOnShoe(Vector3 position)
    {
        Vector3 localPointer = tableRoot.InverseTransformPoint(position);
        Vector3 localShoeCenter = new Vector3(0f, ShoeSurfaceHeight, ShoeLocalDepth);
        return IsPointerWithinShoe(localPointer, localShoeCenter, shoeHitHalfWidth, shoeHitHalfDepth);
    }

    private void ReturnHeldCard()
    {
        DestroyHeldCard();
    }

    private bool TryStartFlight(GameObject card, Vector3 start, Vector3 end, bool returningMiss)
    {
        if (card == null || !card.activeInHierarchy || !isActiveAndEnabled ||
            !gameObject.activeInHierarchy || tableRoot == null)
        {
            return false;
        }

        flyingCard = card;
        flightDestination = end;
        flightIsDeal = !returningMiss;
        waitingForFlight = true;
        try
        {
            Coroutine startedRoutine = StartCoroutine(AnimateFlight(card, start, end, returningMiss));
            if (startedRoutine == null)
            {
                waitingForFlight = false;
                flyingCard = null;
                flightIsDeal = false;
                return false;
            }

            flightRoutine = waitingForFlight ? startedRoutine : null;
            return true;
        }
        catch (System.InvalidOperationException exception)
        {
            waitingForFlight = false;
            flyingCard = null;
            flightRoutine = null;
            flightIsDeal = false;
            Debug.LogWarning($"{name}: Could not start the FLICK card animation; resolving the card without animation. {exception.Message}", this);
            return false;
        }
    }

    private IEnumerator AnimateFlight(GameObject card, Vector3 start, Vector3 end, bool returningMiss)
    {
        float elapsed = 0f;
        if (card == null)
        {
            RecoverFlightVisualFailure(card, returningMiss, "starting a card flight");
            yield break;
        }

        Vector3 initial = card.transform.position;
        float duration = returningMiss ? cardFlightSeconds * 1.5f : cardFlightSeconds;
        while (elapsed < duration)
        {
            if (tableRoot == null)
            {
                RecoverFlightVisualFailure(card, returningMiss, "animating after the table root was destroyed");
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float arc = Mathf.Sin(t * Mathf.PI) * flickArcHeight;
            Vector3 destination = returningMiss ? end : Vector3.Lerp(start, end, t);
            Vector3 position = returningMiss
                ? Vector3.Lerp(initial, end, t)
                : destination + tableRoot.up * arc;
            if (!TrySetCardPose(card, position, Quaternion.Slerp(
                Quaternion.Euler(0f, 0f, -12f),
                tableRoot.rotation,
                t)))
            {
                RecoverFlightVisualFailure(card, returningMiss, "moving a card in flight");
                yield break;
            }

            yield return null;
        }

        if (returningMiss)
        {
            elapsed = 0f;
            if (card == null)
            {
                RecoverFlightVisualFailure(card, returningMiss: true, "returning a missed card");
                yield break;
            }

            Vector3 missPosition = card.transform.position;
            Vector3 shoe = ShoePosition + tableRoot.up * 0.045f;
            while (elapsed < 0.14f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.14f);
                if (!TrySetCardPose(card, Vector3.Lerp(missPosition, shoe, t), tableRoot.rotation))
                {
                    RecoverFlightVisualFailure(card, returningMiss: true, "returning a missed card to the shoe");
                    yield break;
                }

                yield return null;
            }

            CompleteFlight(card, returningMiss: true);
        }
        else
        {
            if (!TrySetCardPose(card, end, tableRoot.rotation))
            {
                RecoverFlightVisualFailure(card, returningMiss: false, "placing a dealt card");
                yield break;
            }

            CompleteFlight(card, returningMiss: false);
        }
    }

    private void CompleteFlight(GameObject card, bool returningMiss)
    {
        waitingForFlight = false;
        flightRoutine = null;
        flyingCard = null;
        flightIsDeal = false;
        if (returningMiss && card != null)
        {
            Destroy(card);
        }

        if (!returningMiss && CanCompleteChallenge(
                successfulFlicks,
                requiredFlicks,
                table != null ? table.RemainingFlickCardCount : 1))
        {
            completing = true;
            completionSuccess = true;
            completionDelay = 0.55f;
            if (aimGuide != null)
            {
                aimGuide.enabled = false;
            }

            SetFeedback($"PERFECT DEAL!  {successfulFlicks} FLICKS");
        }

        UpdateTargetHighlight();
    }

    private void RecoverFlightVisualFailure(GameObject card, bool returningMiss, string operation)
    {
        Debug.LogWarning($"{name}: Recovered from a missing or invalid card visual while {operation}. The blackjack card/deal state was preserved.", this);
        if (!returningMiss && card != null && tableRoot != null)
        {
            TrySetCardPose(card, flightDestination, tableRoot.rotation);
            if (!dealtCards.Contains(card))
            {
                dealtCards.Add(card);
            }
        }

        CompleteFlight(card, returningMiss);
    }

    private bool TrySetCardPose(GameObject card, Vector3 position, Quaternion rotation)
    {
        if (card == null || !card.activeInHierarchy)
        {
            return false;
        }

        try
        {
            card.transform.SetPositionAndRotation(position, rotation);
            return true;
        }
        catch (MissingReferenceException exception)
        {
            Debug.LogWarning($"{name}: The FLICK card visual disappeared during animation. {exception.Message}", this);
            return false;
        }
    }

    private static bool IsCardVisualReady(GameObject card)
    {
        return card != null && card.activeInHierarchy && card.GetComponent<Renderer>() != null;
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
        if (showInputDiagnostics)
        {
            pointerDebugMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pointerDebugMarker.name = "FLICK Projected Pointer Debug";
            pointerDebugMarker.transform.SetParent(tableRoot, false);
            pointerDebugMarker.transform.localScale = Vector3.one * 0.12f;
            Destroy(pointerDebugMarker.GetComponent<Collider>());
            pointerDebugMarker.GetComponent<Renderer>().sharedMaterial = guideMaterial;
            SetColor(pointerDebugMarker.GetComponent<Renderer>(), new Color(1f, 0.25f, 0.2f));
        }

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
        hud.sortingOrder = 70;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.matchWidthOrHeight = 0.5f;
        progressText = CreateHudText(canvasObject.transform, "FLICK  0/0", new Vector2(0f, -42f), new Vector2(620f, 52f), 30);
        timerText = CreateHudText(canvasObject.transform, "TIME  10.0", new Vector2(0f, -96f), new Vector2(420f, 38f), 22);
        feedbackText = CreateHudText(canvasObject.transform, "", new Vector2(0f, -145f), new Vector2(1000f, 48f), 22);
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
        if (round == null)
        {
            throw new System.InvalidOperationException("FLICK cannot render cards without an active blackjack round.");
        }

        for (int handIndex = 0; handIndex < round.CustomerHands.Count; handIndex++)
        {
            BlackjackHand hand = round.CustomerHands[handIndex];
            for (int cardIndex = 0; cardIndex < hand.Cards.Count; cardIndex++)
            {
                GameObject card = CreatePhysicalCard(hand.Cards[cardIndex], faceDown: false);
                if (card == null)
                {
                    continue;
                }

                card.transform.position = GetCardDestination(handIndex, cardIndex);
                card.transform.rotation = tableRoot.rotation;
                dealtCards.Add(card);
            }
        }

        for (int cardIndex = 0; cardIndex < round.DealerCards.Count; cardIndex++)
        {
            bool hidden = cardIndex == 1;
            GameObject card = CreatePhysicalCard(round.DealerCards[cardIndex], hidden);
            if (card == null)
            {
                continue;
            }

            card.transform.position = GetCardDestination(-1, cardIndex);
            card.transform.rotation = tableRoot.rotation;
            dealtCards.Add(card);
        }
    }

    private GameObject CreatePhysicalCard(BlackjackCard card, bool faceDown)
    {
        if (tableRoot == null || cardMaterial == null)
        {
            Debug.LogWarning($"{name}: FLICK cannot create a card without its table root and card material.", this);
            return null;
        }

        GameObject cardObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cardObject.name = faceDown ? "FLICK Card Back" : $"FLICK {card}";
        cardObject.transform.SetParent(tableRoot, true);
        cardObject.transform.localScale = new Vector3(0.48f, 0.035f, 0.68f);
        Destroy(cardObject.GetComponent<Collider>());
        Renderer renderer = cardObject.GetComponent<Renderer>();
        if (renderer == null)
        {
            Destroy(cardObject);
            return null;
        }

        renderer.sharedMaterial = cardMaterial;
        SetColor(renderer, faceDown ? new Color(0.10f, 0.19f, 0.30f) : new Color(0.97f, 0.96f, 0.89f));
        if (!faceDown && !TryCreateCardFaceText(cardObject.transform, card, out _))
        {
            Debug.LogWarning($"{name}: FLICK created a card back because its face text could not be created.", this);
        }

        return cardObject;
    }

    private bool TrySetCardFace(GameObject cardObject, BlackjackCard card, bool faceDown)
    {
        if (cardObject == null || !cardObject.activeInHierarchy)
        {
            return false;
        }

        Renderer renderer = cardObject.GetComponent<Renderer>();
        if (renderer == null)
        {
            return false;
        }

        Transform faceTransform = cardObject.transform.Find("Card Face");
        if (faceDown)
        {
            if (faceTransform != null)
            {
                faceTransform.gameObject.SetActive(false);
                Destroy(faceTransform.gameObject);
            }

            SetColor(renderer, new Color(0.10f, 0.19f, 0.30f));
            return true;
        }

        if (!TryCreateCardFaceText(cardObject.transform, card, out TextMesh face))
        {
            SetColor(renderer, new Color(0.10f, 0.19f, 0.30f));
            return false;
        }

        face.gameObject.SetActive(true);
        SetColor(renderer, new Color(0.97f, 0.96f, 0.89f));
        face.color = card.Suit == 1 || card.Suit == 2
            ? new Color(0.75f, 0.08f, 0.08f)
            : new Color(0.08f, 0.10f, 0.12f);
        return true;
    }

    public static bool TryCreateCardFaceText(Transform cardTransform, BlackjackCard card, out TextMesh face)
    {
        face = null;
        if (cardTransform == null || card.Rank < 1 || card.Rank > 13 || card.Suit < 0 || card.Suit >= SuitSymbols.Length)
        {
            return false;
        }

        try
        {
            Transform faceTransform = cardTransform.Find("Card Face");
            if (faceTransform == null)
            {
                GameObject faceObject = new GameObject("Card Face");
                faceObject.transform.SetParent(cardTransform, false);
                faceTransform = faceObject.transform;
            }

            face = faceTransform.GetComponent<TextMesh>();
            if (face == null)
            {
                face = faceTransform.gameObject.AddComponent<TextMesh>();
            }

            face.anchor = TextAnchor.MiddleCenter;
            face.alignment = TextAlignment.Center;
            face.fontSize = 80;
            face.characterSize = 0.015f;
            face.text = FormatCard(card);
            face.transform.localPosition = new Vector3(0f, 0.53f, 0f);
            face.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            face.transform.localScale = new Vector3(1f / 0.48f, 1f / 0.035f, 1f / 0.68f);
            return true;
        }
        catch (MissingReferenceException exception)
        {
            Debug.LogWarning($"FLICK could not create a playing-card face because its visual was destroyed. {exception.Message}");
            face = null;
            return false;
        }
        catch (System.InvalidOperationException exception)
        {
            Debug.LogWarning($"FLICK could not create a playing-card face. {exception.Message}");
            face = null;
            return false;
        }
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

        Vector3 start = ShoePosition + tableRoot.up * 0.09f;
        Vector3 end = mouseWorld;
        for (int i = 0; i < aimGuide.positionCount; i++)
        {
            float t = i / (float)(aimGuide.positionCount - 1);
            aimGuide.SetPosition(i, Vector3.Lerp(start, end, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * flickArcHeight));
        }
    }

    private void UpdateTargetHighlight()
    {
        if (table == null || targetMarkers.Count == 0 || table.ActiveRound == null)
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
        if (success && (table == null || table.RemainingFlickCardCount != 0))
        {
            Debug.LogWarning($"{name}: FLICK could not confirm that every required blackjack card was dealt; preserving the task for retry.", this);
            success = false;
        }

        StopAndSettleFlight();
        if (success)
        {
            table.EndFlickAssistance();
        }
        else if (table != null)
        {
            table.NotifyFlickAttemptFailed();
        }

        RestorePlayerAndCursor();
        CleanUpStaging();
        int score = Mathf.Max(0, successfulFlicks * 100 + Mathf.RoundToInt(timeRemaining * 10f) - missedFlicks * 25);
        Complete(success, score);
    }

    private bool HasCompletedRequiredDeals()
    {
        return CanCompleteChallenge(
            successfulFlicks,
            requiredFlicks,
            table != null ? table.RemainingFlickCardCount : 1);
    }

    private void CompleteCommittedChallenge()
    {
        Finish(success: true);
    }

    private void StopAndSettleFlight()
    {
        if (flightRoutine != null)
        {
            StopCoroutine(flightRoutine);
            flightRoutine = null;
        }

        if (flightIsDeal && flyingCard != null && tableRoot != null)
        {
            TrySetCardPose(flyingCard, flightDestination, tableRoot.rotation);
        }
        else if (flyingCard != null)
        {
            Destroy(flyingCard);
        }

        flyingCard = null;
        waitingForFlight = false;
        flightIsDeal = false;
    }

    private void RestorePlayerAndCursor()
    {
        if (!controlsCaptured)
        {
            return;
        }

        controlsCaptured = false;
        if (player != null)
        {
            player.SetMovementEnabled(true);
        }

        Cursor.lockState = savedCursorLockMode;
        Cursor.visible = savedCursorVisible;
    }

    private void AbortUnrecoverableChallenge(string reason)
    {
        Debug.LogError($"{name}: FLICK cannot continue because {reason}; resolving the task and resuming automated dealing.", this);
        if (flightRoutine != null)
        {
            StopCoroutine(flightRoutine);
            flightRoutine = null;
        }

        DestroyHeldCard();
        flyingCard = null;
        waitingForFlight = false;
        flightIsDeal = false;
        RestorePlayerAndCursor();
        CleanUpVisuals();
        ClearPhysicalCards();
        if (table != null)
        {
            table.AbortFlickAssistance();
        }

        Complete(false, 0);
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

        if (pointerDebugMarker != null)
        {
            Destroy(pointerDebugMarker);
            pointerDebugMarker = null;
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

    private Vector3 ShoePosition => tableRoot.TransformPoint(new Vector3(0f, ShoeSurfaceHeight + 0.15f, ShoeLocalDepth));

    private static string FormatCard(BlackjackCard card)
    {
        if (card.Rank < 1 || card.Rank > 13 || card.Suit < 0 || card.Suit >= SuitSymbols.Length)
        {
            throw new System.ArgumentOutOfRangeException(nameof(card), "FLICK card rank or suit is outside the supported range.");
        }

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
