using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class CasinoShiftController : MonoBehaviour
{
    private const string SaveTutorial = "PitPanic.TutorialComplete";
    private static int requestedShift;
    private static bool skipMenu;
    private static bool requestedTutorial;

    [SerializeField, Min(60f)] private float shiftSeconds = 540f;
    private CasinoPlayerController player;
    private CasinoPlayerInteractionRouter router;
    private CasinoCustomerSpawner spawner;
    private DealerAssignmentManager dealers;
    private CasinoCashDeskInteraction cashDesk;
    private PitDeskInteraction pitDesk;
    private BlackjackTableInteraction blackjack;
    private RouletteInspectionView rouletteInspection;
    private RouletteRoundController roulette;
    private RouletteAssistanceScheduler rouletteScheduler;
    private CasinoMinigameStation[] stations;
    private CasinoMinigame[] minigames;
    private CasinoShiftAccounting accounting;
    private CasinoFloatRefillDestination trainingDestination;
    private Canvas canvas;
    private GameObject menu;
    private GameObject intro;
    private GameObject hud;
    private GameObject results;
    private GameObject pausePanel;
    private GameObject upgrades;
    private Text upgradeText;
    private Button nextButton;
    private Button[] upgradeButtons;
    private bool newGameRequested;
    private bool passed;
    private Text objectiveText;
    private Text timerText;
    private Text resultText;
    private Text arrow;
    private Button skipButton;
    private Vector3 movementStart;
    private float remaining;
    private float nextGuidedSpawn;
    private bool running;
    private bool ending;
    private bool paused;
    private bool tutorial;
    private bool moved;
    private bool inspectedBlackjack;
    private bool inspectedRoulette;
    private bool flickDone;
    private bool rouletteDone;
    private bool delivered;
    private bool assigned;
    private bool breakSent;
    private bool queuedFlick;
    private bool queuedRoulette;
    private bool requestedFloat;
    private bool requestedDealerBreak;
    private int completedTasks;
    private int failedAttempts;
    private int penaltyStart;
    private int shift;
    private int lastStage = -1;
    private GameObject lessonPanel;
    private Text lessonTitle;
    private Text lessonText;
    private Text lessonDemo;
    private Image demonstrationProp;
    private Text demonstrationArrow;
    private Image guidePortrait;
    private PixelCharacterVisual guideVisual;
    private bool lessonOpen;
    private int shownLesson;
    private bool reviewLesson;
    private bool cashOutDone;
    private bool manualBlackjackDone;
    private bool manualRouletteDone;
    private bool preparedManualBlackjack;
    private bool preparedManualRoulette;
    private bool objectivesExplained;
    private readonly bool[] explainedLessons = new bool[13];
    public bool IsLessonOpen => lessonOpen;
    public int TutorialStage => !moved ? 0 : !inspectedBlackjack ? 1 : !flickDone ? 2 :
        !inspectedRoulette ? 3 : !rouletteDone ? 4 : !cashOutDone ? 5 : !delivered ? 6 :
        !assigned ? 7 : !breakSent ? 8 : !manualBlackjackDone ? 9 : !manualRouletteDone ? 10 :
        !blackjack.IsDealerStaffed || !roulette.IsDealerStaffed ? 11 : !objectivesExplained ? 12 : 13;

    public float RemainingSeconds => remaining;
    public bool IsRunning => running;
    public int CurrentShift => shift;
    public CasinoShiftAccounting Accounting => accounting;
    public bool PassedShift => passed;

    private void Awake()
    {
        player = FindFirstObjectByType<CasinoPlayerController>();
        spawner = FindFirstObjectByType<CasinoCustomerSpawner>();
        dealers = FindFirstObjectByType<DealerAssignmentManager>();
        cashDesk = FindFirstObjectByType<CasinoCashDeskInteraction>();
        pitDesk = FindFirstObjectByType<PitDeskInteraction>();
        roulette = FindFirstObjectByType<RouletteRoundController>();
        rouletteInspection = FindFirstObjectByType<RouletteInspectionView>();
        rouletteScheduler = FindFirstObjectByType<RouletteAssistanceScheduler>();
        foreach (BlackjackTableInteraction table in FindObjectsByType<BlackjackTableInteraction>(FindObjectsSortMode.None))
            if (table.TableDisplayName == "BLACKJACK 1") blackjack = table;
        stations = FindObjectsByType<CasinoMinigameStation>(FindObjectsSortMode.None);
        minigames = FindObjectsByType<CasinoMinigame>(FindObjectsSortMode.None);
        if (player == null || spawner == null || dealers == null || cashDesk == null ||
            pitDesk == null || blackjack == null || roulette == null ||
            rouletteInspection == null || rouletteScheduler == null)
        {
            Debug.LogError("Shift setup is missing a required casino system.", this);
            enabled = false;
            return;
        }

        router = player.GetComponent<CasinoPlayerInteractionRouter>();
        if (router == null)
        {
            Debug.LogError("Shift setup is missing the player interaction router.", player);
            enabled = false;
            return;
        }
        spawner.SetAcceptingArrivals(false);
        router.enabled = false;
        player.SetMovementEnabled(false);
        accounting = new CasinoShiftAccounting();
        foreach (BlackjackTableInteraction table in FindObjectsByType<BlackjackTableInteraction>(FindObjectsSortMode.None))
            table.RoundSettled += OnBlackjackSettled;
        roulette.BetSettled += OnRouletteSettled;
        foreach (CasinoMinigameStation station in stations)
            station.TaskSucceeded += OnTaskSucceeded;
        foreach (CasinoMinigame game in minigames)
        {
            game.Completed += OnMinigameCompleted;
            game.Abandoned += OnMinigameAbandoned;
            if (game is FlickCardMinigame && game.GetComponent<BlackjackTableInteraction>() == blackjack)
                game.Completed += OnFlickCompleted;
            if (game is RouletteTableMinigame)
                game.Completed += OnRouletteCompleted;
            if (game is CashDeskPackMinigame) game.Completed += OnCashOutCompleted;
        }
        cashDesk.FloatDelivered += OnFloatDelivered;
        dealers.DealerAssigned += OnDealerAssigned;
        dealers.DealerSentOnBreak += OnDealerBreak;
    }

    private void Start()
    {
        dealers.SetPaused(true);
        BuildInterface();
        if (skipMenu)
        {
            skipMenu = false;
            BeginShift(requestedShift, requestedTutorial);
            requestedTutorial = false;
        }
        else
            menu.SetActive(true);
    }

    private void Update()
    {
        if (!running)
            return;
        if (lessonOpen)
        {
            lessonDemo.rectTransform.localScale = Vector3.one * (1 + .025f * Mathf.Sin(Time.unscaledTime * 4));
            if (guideVisual != null) guidePortrait.sprite = guideVisual.TitleFrame(Time.unscaledTime);
            float demoProgress = Mathf.Repeat(Time.unscaledTime * .5f, 1);
            demonstrationProp.rectTransform.anchoredPosition = new Vector2(-190 + 380 * demoProgress, -235);
            demonstrationProp.rectTransform.localRotation = Quaternion.Euler(0, 0,
                shownLesson == 4 || shownLesson == 10 ? Time.unscaledTime * -90 : Mathf.Sin(demoProgress * Mathf.PI) * -15);
            demonstrationArrow.rectTransform.anchoredPosition = new Vector2(-70 + 12 * Mathf.Sin(Time.unscaledTime * 4), -235);
            if (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame))
                DismissLesson();
            return;
        }

        if (Keyboard.current != null && (Keyboard.current.pKey.wasPressedThisFrame ||
            Keyboard.current.escapeKey.wasPressedThisFrame && (paused || !AnyInteractionOpen())))
            SetPaused(!paused);
        if (paused)
            return;
        hud.SetActive(!AnyInteractionOpen());

        if (tutorial)
        {
            if ((player.transform.position - movementStart).sqrMagnitude >= 4f) moved = true;
            if (blackjack.IsInspectionOpen) inspectedBlackjack = true;
            if (rouletteInspection.IsOpen) inspectedRoulette = true;
            UpdateTutorial();
        }

        if (!ending)
        {
            remaining = Mathf.Max(tutorial ? 180f : 0f, remaining - Time.deltaTime);
            CasinoSoundscape.SetIntensity(1 - remaining / shiftSeconds);
            spawner.SetShiftProgress(1 - remaining / shiftSeconds);
            spawner.SetFinale(remaining <= 60f);
            if (remaining <= 0f)
            {
                ending = true;
                spawner.SetAcceptingArrivals(false);
            }
            if (!tutorial && !ending)
                objectiveText.text = $"HANDS {accounting.SettledHands}/{CasinoDemoProgression.ServiceGoal(shift)}  •  HELP {completedTasks}/4  •  KEEP 60% HAPPY";
        }
        int seconds = Mathf.CeilToInt(remaining);
        timerText.text = $"SHIFT {shift}  •  {seconds / 60:00}:{seconds % 60:00}";
        if (ending)
        {
            objectiveText.text = "SHIFT OVER! FINISH OR CLOSE THE CURRENT TASK.";
            arrow.gameObject.SetActive(false);
            if (!AnyInteractionOpen()) FinishShift();
        }
    }

    private bool AnyInteractionOpen()
    {
        if (CasinoMinigamePresentation.IsOpen || pitDesk.IsOpen || rouletteInspection.IsOpen)
            return true;
        foreach (CasinoMinigame game in minigames)
            if (game != null && game.IsRunning) return true;
        foreach (BlackjackTableInteraction table in FindObjectsByType<BlackjackTableInteraction>(FindObjectsSortMode.None))
            if (table.IsInspectionOpen) return true;
        return false;
    }

    private void BeginShift(int number, bool guided)
    {
        CasinoSoundscape.SetMusicTrack(CasinoMusicTrack.Casino);
        CasinoSoundscape.SetFocus(null);
        if (newGameRequested)
        {
            CasinoDemoProgression.StartNewGame();
            newGameRequested = false;
            if (!guided)
            {
                PlayerPrefs.SetInt(SaveTutorial, 1);
                PlayerPrefs.Save();
            }
        }
        shift = Mathf.Clamp(number, 1, CasinoDemoProgression.LastShift);
        tutorial = guided;
        spawner.SetGuidedPractice(guided);
        spawner.ConfigureShift(shift, CasinoDemoProgression.Owns(CasinoDemoUpgrade.Patience));
        foreach (AIDealer dealer in dealers.Dealers)
            if (dealer != null)
            {
                dealer.ConfigureShift(shift, CasinoDemoProgression.Owns(CasinoDemoUpgrade.DealerRest));
                dealer.TrainingProtected = guided;
            }
        foreach (CasinoMinigame game in minigames)
            if (game is CashDeskPackMinigame pack)
                pack.PackingBonusSeconds = CasinoDemoProgression.Owns(CasinoDemoUpgrade.Packing) ? 5 : 0;
        remaining = shiftSeconds;
        penaltyStart = CustomerSatisfaction.PenaltyCount;
        movementStart = player.transform.position;
        menu.SetActive(false);
        intro.SetActive(false);
        results.SetActive(false);
        upgrades.SetActive(false);
        hud.SetActive(true);
        skipButton.gameObject.SetActive(tutorial);
        router.enabled = true;
        player.SetMovementEnabled(true);
        dealers.SetPaused(false);
        spawner.SetAcceptingArrivals(!tutorial);
        running = true;
        if (guided) ShowLesson(0);
    }

    private void SetPaused(bool value)
    {
        paused = value;
        AudioListener.pause = value;
        dealers.SetPaused(value);
        router.enabled = !value;
        player.SetMovementEnabled(!value && !AnyInteractionOpen());
        pausePanel.SetActive(value);
    }

    private void ReturnToMainMenu()
    {
        foreach (CasinoMinigame game in minigames)
            if (game != null && game.IsRunning) game.Abandon();
        running = false;
        skipMenu = false;
        requestedShift = 0;
        requestedTutorial = false;
        AudioListener.pause = false;
        CasinoSoundscape.SetFocus(null);
        dealers.SetPaused(false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void RestartShift()
    {
        foreach (CasinoMinigame game in minigames)
            if (game != null && game.IsRunning) game.Abandon();
        requestedShift = shift;
        requestedTutorial = tutorial;
        skipMenu = true;
        AudioListener.pause = false;
        CasinoSoundscape.SetFocus(null);
        dealers.SetPaused(false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void UpdateTutorial()
    {
        int stage = TutorialStage;
        if (stage < explainedLessons.Length && !explainedLessons[stage] && !AnyInteractionOpen())
        {
            ShowLesson(stage);
            return;
        }
        if (stage != lastStage)
        {
            lastStage = stage;
            objectiveText.rectTransform.localScale = Vector3.one * 1.12f;
        }
        objectiveText.rectTransform.localScale = Vector3.MoveTowards(
            objectiveText.rectTransform.localScale, Vector3.one, Time.unscaledDeltaTime);
        CasinoPlayerFloatCarrier heldTray = player.GetComponent<CasinoPlayerFloatCarrier>();
        if (stage != 6 && heldTray != null && heldTray.IsCarrying)
        {
            objectiveText.text = $"E: DELIVER FLOAT TO {heldTray.CurrentOrder.DestinationId}, THEN KEEP LEARNING";
            UpdateArrow(heldTray.CurrentOrder.Destination.transform);
            return;
        }
        if ((stage == 1 || stage == 2) && !blackjack.IsDealerStaffed)
        {
            ShowDealerRecovery("BJ1");
            return;
        }
        if ((stage == 3 || stage == 4) && !roulette.IsDealerStaffed)
        {
            ShowDealerRecovery("ROULETTE");
            return;
        }
        Transform target = null;
        switch (stage)
        {
            case 0:
                objectiveText.text = "WASD: MOVE, BOSS!";
                break;
            case 1:
                objectiveText.text = "E: OPEN BLACKJACK 1";
                target = blackjack.transform;
                break;
            case 2:
                if (!queuedFlick) { blackjack.QueueFlickAssistanceTest(); queuedFlick = true; }
                EnsureGuidedCustomer(CasinoCustomerGame.Blackjack1);
                bool ready = blackjack.IsFlickReady;
                objectiveText.text = ready ? "BJ1: OPEN WITH E, THEN PLAY SHUFFLE!" : "BJ1: WAIT FOR THE DEALER'S SHUFFLE! CALL";
                target = blackjack.transform;
                break;
            case 3:
                objectiveText.text = "E: OPEN ROULETTE";
                target = roulette.transform;
                break;
            case 4:
                if (!queuedRoulette) { rouletteScheduler.RequestTutorialAssistance(); queuedRoulette = true; }
                EnsureGuidedCustomer(CasinoCustomerGame.Roulette, 2);
                objectiveText.text = roulette.HasPendingAssistance
                    ? "ROULETTE: OPEN WITH E, THEN PLAY!"
                    : "ROULETTE: WAIT FOR THE DEALER'S CALL";
                target = roulette.transform;
                break;
            case 5:
                if (cashDesk.ActiveCustomer == null && Time.time >= nextGuidedSpawn)
                {
                    nextGuidedSpawn = Time.time + 8;
                    spawner.TrySpawnGuidedCashOut();
                }
                objectiveText.text = "CASH DESK E: CLICK THE NEXT CHIP, THEN FINISH!";
                target = cashDesk.transform;
                break;
            case 6:
                if (!requestedFloat)
                {
                    trainingDestination = blackjack.GetComponent<CasinoFloatRefillDestination>();
                    if (trainingDestination != null) trainingDestination.RequestAttention();
                    else Debug.LogError("Tutorial float destination BJ1 is missing.", this);
                    requestedFloat = true;
                }
                CasinoPlayerFloatCarrier carrier = player.GetComponent<CasinoPlayerFloatCarrier>();
                if (carrier != null && carrier.IsCarrying)
                {
                    target = carrier.CurrentOrder.Destination.transform;
                    objectiveText.text = $"E: DELIVER FLOAT TO {carrier.CurrentOrder.DestinationId}";
                }
                else
                {
                    target = cashDesk.transform;
                    objectiveText.text = cashDesk.ActiveCustomer != null
                        ? "E: HELP THE CASH DESK, THEN PACK BJ1'S FLOAT"
                        : "E: PACK BJ1'S FLOAT AT THE CASH DESK";
                }
                break;
            case 7:
                if (!requestedDealerBreak)
                {
                    blackjack.AssignedDealer?.RequestTutorialBreak();
                    requestedDealerBreak = true;
                }
                objectiveText.text = HasReadyDealer()
                    ? "BJ1 NEEDS A BREAK! ASSIGN COVER AT THE PIT DESK"
                    : "WAIT FOR A DEALER TO REACH THE PIT DESK";
                target = pitDesk.transform;
                break;
            case 8:
                objectiveText.text = HasReadyDealer()
                    ? "PIT DESK: SEND A DEALER ON BREAK"
                    : "WAIT FOR A DEALER TO REACH THE PIT DESK";
                target = pitDesk.transform;
                break;
            case 9:
                if (!preparedManualBlackjack || blackjack.IsDealerStaffed)
                {
                    blackjack.AssignedDealer?.RequestTutorialTakeover();
                    preparedManualBlackjack = true;
                }
                EnsureGuidedCustomer(CasinoCustomerGame.Blackjack1);
                target = blackjack.transform;
                objectiveText.text = "BJ1 E: TAKE OVER! SPACE DEALS / PAYS. FINISH A HAND.";
                break;
            case 10:
                if (!preparedManualRoulette || roulette.IsDealerStaffed)
                {
                    roulette.AssignedDealer?.RequestTutorialTakeover();
                    preparedManualRoulette = true;
                }
                if (!queuedRoulette)
                {
                    rouletteScheduler.RequestTutorialAssistance();
                    queuedRoulette = true;
                }
                EnsureGuidedCustomer(CasinoCustomerGame.Roulette, 2);
                target = roulette.transform;
                objectiveText.text = "ROULETTE E: TAKE OVER! SPACE STARTS SPIN / SWEEP / PAY.";
                break;
            case 11:
                objectiveText.text = HasReadyDealer()
                    ? "PIT DESK E: ASSIGN RESTED COVER TO BOTH TABLES."
                    : "PIT DESK E: SEND RETURNED TIRED STAFF ON BREAK, THEN ASSIGN.";
                target = pitDesk.transform;
                break;
            default:
                CompleteTutorial();
                return;
        }

        UpdateArrow(target);
    }

    private void ShowDealerRecovery(string table)
    {
        objectiveText.text = HasReadyDealer()
            ? $"PIT DESK: ASSIGN A DEALER TO {table}"
            : $"WAIT FOR A DEALER TO COVER {table}";
        UpdateArrow(pitDesk.transform);
    }

    private void UpdateArrow(Transform target)
    {
        arrow.gameObject.SetActive(target != null && !AnyInteractionOpen());
        if (target != null && Camera.main != null)
        {
            Vector3 p = Camera.main.WorldToScreenPoint(target.position + Vector3.up * 3f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform, p, null, out Vector2 local);
            arrow.rectTransform.anchoredPosition = local + Vector2.up * (12f + 5f * Mathf.Sin(Time.unscaledTime * 4f));
        }
    }

    private void EnsureGuidedCustomer(CasinoCustomerGame game, int count = 1)
    {
        if (Time.time < nextGuidedSpawn) return;
        nextGuidedSpawn = Time.time + 8f;
        spawner.TrySpawnGuidedCustomer(game, count);
    }

    private bool HasReadyDealer()
    {
        foreach (AIDealer dealer in dealers.Dealers)
            if (dealer != null && dealer.IsReadyAtPitDesk && dealer.Energy > 0) return true;
        return false;
    }

    private void CompleteTutorial()
    {
        if (lessonOpen) DismissLesson();
        if (!tutorial) return;
        tutorial = false;
        spawner.SetGuidedPractice(false);
        foreach (AIDealer dealer in dealers.Dealers)
            if (dealer != null) dealer.TrainingProtected = false;
        rouletteScheduler.CancelTutorialAssistance();
        PlayerPrefs.SetInt(SaveTutorial, 1);
        PlayerPrefs.Save();
        spawner.SetAcceptingArrivals(!ending);
        if (!ending) remaining = Mathf.Max(remaining, 180);
        objectiveText.text = "YOU'VE GOT THIS! KEEP THE CASINO HAPPY!";
        arrow.gameObject.SetActive(false);
        skipButton.gameObject.SetActive(false);
    }

    private void FinishShift()
    {
        if (!running) return;
        running = false;
        rouletteScheduler.CancelTutorialAssistance();
        CasinoSoundscape.SetIntensity(0);
        dealers.SetPaused(true);
        router.enabled = false;
        player.SetMovementEnabled(false);
        int pending = 0;
        foreach (CasinoMinigameStation station in stations)
        {
            if (station == null || !station.IsTaskPending)
                continue;
            if (station.MinigameType == CasinoMinigameType.Pack &&
                cashDesk.ActiveCustomer == null && cashDesk.GetRequestedOrders().Count > 0)
                continue;
            pending++;
        }
        foreach (CasinoFloatRefillDestination destination in cashDesk.Destinations)
            if (destination != null && destination.NeedsRefill()) pending++;
        foreach (CasinoDealerStation station in dealers.Stations)
        {
            CasinoTableAttention attention = station != null ? station.Attention : null;
            if (attention != null && (attention.GetPendingCount(TableAttentionReason.DealerBreakRequired) > 0 ||
                attention.GetPendingCount(TableAttentionReason.DealerExhausted) > 0 ||
                attention.GetPendingCount(TableAttentionReason.TableUnstaffed) > 0))
                pending++;
        }
        int penalties = Mathf.Max(0, CustomerSatisfaction.PenaltyCount - penaltyStart);
        int customers = spawner.TotalSpawned;
        int satisfaction = customers > 0
            ? Mathf.Clamp(Mathf.RoundToInt(100f * (customers - penalties) / customers), 0, 100)
            : 100;
        int rating = satisfaction >= 85 && failedAttempts + pending <= 2 ? 3 :
            satisfaction >= 60 && failedAttempts + pending <= 5 ? 2 : 1;
        passed = CasinoDemoProgression.Passed(shift, accounting.SettledHands, completedTasks, satisfaction);
        if (passed) CasinoDemoProgression.AwardShift(shift, rating);
        nextButton.gameObject.SetActive(passed);
        CasinoSoundscape.SetFocus(results.transform);
        CasinoSoundscape.Play(passed ? CasinoSoundCue.Success : CasinoSoundCue.Warning, player.transform.position, results.transform);
        resultText.text = $"SHIFT {shift} {(passed ? "CLEARED!" : "TRY AGAIN!")}  {new string('★', rating)}\n\n" +
            $"TABLE PROFIT  {FormatMoney(accounting.ProfitPence)}\n" +
            $"SATISFACTION  {(customers > 0 ? satisfaction + "%" : "NO CUSTOMERS")}\n" +
            $"TASKS COMPLETED  {completedTasks}\n" +
            $"TASKS MISSED  {pending}  •  FAILED ATTEMPTS  {failedAttempts}\n" +
            $"HANDS SERVED  {accounting.SettledHands}/{CasinoDemoProgression.ServiceGoal(shift)}\n\n" +
            (passed ? $"EARNED {rating} UPGRADE STAMPS!" : "GOAL: 4 HELPS, HANDS TARGET, 60% HAPPY.") +
            "\nFLOAT TRANSFERS ARE NOT INCOME.";
        hud.SetActive(false);
        results.SetActive(true);
    }

    private static string FormatMoney(long amount)
    {
        decimal pounds = amount / 100m;
        return pounds.ToString("£0.00;−£0.00;£0.00");
    }

    private void OnBlackjackSettled(BlackjackTableRound round)
    {
        if (!running) return;
        accounting.RecordBlackjack(round);
        if (blackjack.IsPlayerOperating && preparedManualBlackjack && blackjack.ActiveRound == round) manualBlackjackDone = true;
    }
    private void OnRouletteSettled(RouletteCustomerBet bet)
    {
        if (!running) return;
        accounting.RecordRoulette(bet);
        if (roulette.IsPlayerOperating && preparedManualRoulette) manualRouletteDone = true;
    }
    private void OnTaskSucceeded(CasinoMinigameResult result) { if (running) completedTasks++; }
    private void OnMinigameCompleted(CasinoMinigameResult result)
    {
        if (!running) return;
        if (!result.Success) failedAttempts++;
    }
    private void OnCashOutCompleted(CasinoMinigameResult result)
    {
        if (running && result.Success && cashDesk.GetComponent<CashDeskPackMinigame>().IsCashOutMode) cashOutDone = true;
    }
    private void OnMinigameAbandoned() { if (running) failedAttempts++; }
    private void OnFlickCompleted(CasinoMinigameResult result) { if (running && result.Success) flickDone = true; }
    private void OnRouletteCompleted(CasinoMinigameResult result)
    {
        if (running && result.Success) { rouletteDone = true; queuedRoulette = false; }
    }
    private void OnFloatDelivered(CasinoFloatRefillDestination destination)
    {
        if (!running || destination == null) return;
        completedTasks++;
        if (destination.DestinationId == "BJ1") delivered = true;
    }

    private void OnDealerAssigned(AIDealer dealer, CasinoDealerStation station)
    {
        if (!running) return;
        completedTasks++;
        if (station != null && station.BlackjackTable == blackjack) assigned = true;
    }

    private void OnDealerBreak(AIDealer dealer)
    {
        if (!running) return;
        completedTasks++;
        breakSent = true;
    }

    private void OnDestroy()
    {
        AudioListener.pause = false;
        foreach (BlackjackTableInteraction table in FindObjectsByType<BlackjackTableInteraction>(FindObjectsSortMode.None))
            table.RoundSettled -= OnBlackjackSettled;
        if (roulette != null) roulette.BetSettled -= OnRouletteSettled;
        if (stations != null)
            foreach (CasinoMinigameStation station in stations)
                if (station != null) station.TaskSucceeded -= OnTaskSucceeded;
        if (minigames != null)
            foreach (CasinoMinigame game in minigames)
                if (game != null)
                {
                    game.Completed -= OnMinigameCompleted;
                    game.Abandoned -= OnMinigameAbandoned;
                    game.Completed -= OnFlickCompleted;
                    game.Completed -= OnRouletteCompleted;
                    game.Completed -= OnCashOutCompleted;
                }
        if (cashDesk != null) cashDesk.FloatDelivered -= OnFloatDelivered;
        if (dealers != null)
        {
            dealers.DealerAssigned -= OnDealerAssigned;
            dealers.DealerSentOnBreak -= OnDealerBreak;
            dealers.SetPaused(false);
        }
    }

    private void ShowLesson(int lesson, bool reviewing = false)
    {
        if (!running || AnyInteractionOpen()) return;
        shownLesson = Mathf.Clamp(lesson, 0, 12);
        reviewLesson = reviewing;
        lessonOpen = true;
        dealers.SetPaused(true);
        AudioListener.pause = true;
        router.enabled = false;
        player.SetMovementEnabled(false);
        lessonPanel.SetActive(true);
        hud.SetActive(false);
        arrow.gameObject.SetActive(false);
        string[] titles = { "WELCOME, PENGUIN BOSS!", "MEET YOUR BLACKJACK TABLE", "LET'S SHUFFLE!",
            "MEET YOUR ROULETTE TABLE", "COVER: SPIN, SWEEP, PAY!", "CUSTOMER CASH-OUT", "KEEP THE CHIPS FLOWING",
            "YOUR DEALERS GET TIRED", "TIME FOR A BREAK", "YOU CAN DEAL BLACKJACK!", "YOU CAN RUN ROULETTE!",
            "HAND BACK TO YOUR TEAM", "READ THE ROOM - WIN THE SHIFT!" };
        string[] instructions = {
            "I'm Tiger, your guide! Customers play while you keep the floor running.\n\nWASD or ARROW KEYS move you across the screen. Walk a few steps, then follow my GOLD ARROW. E opens the station you're near. ESC closes a task; P pauses everything.",
            "Go to BLACKJACK 1 and press E. This is one shared table screen: inspect the hands and use its task button when help is needed.\n\nCustomers place bets; the dealer deals. Your job is to keep them served, not to make their gambling decisions.",
            "The dealer needs help preparing the opening cards! I'll arrange a practice SHUFFLE! at BJ1.\n\nOpen BJ1 with E, then choose PLAY SHUFFLE! Click the shoe for each highlighted customer or dealer hand. Cards fly to the right place and become part of this real round. Finish every card before time runs out. ESC cancels; E lets you retry.",
            "Go to ROULETTE and press E. Inspect customer bets, the winning number and the table's finite chip stock.\n\nProfit odds: straight 35:1, split 17:1, street 11:1, corner 8:1, six-line 5:1; red/black, odd/even return 1:1. Gross payout includes your stake.",
            "I'll arrange a full practice round. E at Roulette, then play COVER!\n\nSPIN: drag around the wheel and release. SWEEP: move every losing bet to the tray; leave winning bets. PAY: select available chips to match the exact gross return, including the stake. Finish all winners. If the float is short, refill it and retry PAY.",
            "A customer is coming to the CASH DESK. Walk there and press E.\n\nClick the chip marked NEXT CHIP. Your selected chips appear in the customer tray. Wrong chips cost a little time. UNDO removes the last chip. When every chip is prepared, click FINISH or press E. This completes their cash-out.",
            "Tables need a FLOAT: their own supply of chips, not casino profit. I'll request a £200 refill for BJ1.\n\nCASH DESK E: choose PACK FOR BJ1. Click £2.50 / £5 / £25 / £100 chips to match the listed counts. UNDO corrects a selection. A complete tray is automatically carried. Walk to BJ1 and press E to DELIVER. Carrying a tray makes delivery your first interaction.",
            "Dealers lose energy as they work. When energy reaches ZERO, dealing STOPS and waiting customers lose patience!\n\nPIT DESK E: choose a READY dealer and ASSIGN to BJ1. The replacement walks there before taking over. The tired dealer returns to the desk. Never abandon an exhausted table: assign cover or take over yourself.",
            "At PIT DESK, a returned dealer is ready for a decision. Press E and choose SEND ON BREAK.\n\nThey walk to the Break Room, sit, recover energy and return to the desk rested. Only READY staff can be assigned. I'll protect the rest of the team during practice so you can learn calmly.",
            "I'll stop BJ1's dealer for practice. You can rescue the table even with no rested replacements!\n\nBJ1 E: choose TAKE OVER. SPACE or DEAL CARD advances cards and dealer draws. Customers still choose Hit / Stand. In payout, SPACE pays the exact return; the chip buttons also work. Complete ONE real hand. ESC leaves; dealing then waits for cover.",
            "Now try taking over Roulette. ROULETTE E: choose TAKE OVER. Keep the table open while customers arrive, then SPACE starts its existing SPIN / SWEEP / PAY game.\n\nFinish the real round: all losing bets cleared, all winners paid exactly. This uses the same bets and inventory - never a second parallel round.",
            "You're not meant to do every job forever! PIT DESK E: assign rested cover to BJ1 and Roulette.\n\nWhen a dealer reaches the table, service resumes automatically. Send the OUTGOING exhausted dealer on BREAK; they can then recover to cover the other table. If nobody is ready, keep operating a table while the team rests.",
            "An ! always means a real job. Walk near it to see WHAT, WHERE and E. The station meter and HUD seconds use actual customer patience or task time: GREEN safe, YELLOW hurry, RED nearly expired.\n\nNo deadline means no fake meter. An expired customer leaves and lowers happiness; a timed-out task must be retried.\n\nSHIFT 1: serve 8 hands, HELP 4 times, keep 60% HAPPY. Results show real profit; float isn't income. Clear a shift, buy upgrades with stamps, then start the next shift. HELP revisits any lesson."
        };
        lessonTitle.text = $"TIGER'S TRAINING  {shownLesson + 1}/13\n{titles[shownLesson]}";
        lessonText.text = instructions[shownLesson];
        string[] demo = { "W A S D   ->   WALK   ->   E", "GOLD ARROW   ->   BJ1   ->   E", "SHOE   ->   GLOWING HAND   ->   ALL CARDS",
            "E   ->   BETS   ->   RETURNS", "SPIN   ->   SWEEP LOSERS   ->   PAY WINNERS", "NEXT CHIP   ->   CLICK   ->   FINISH",
            "CLICK CHIPS   ->   CARRY   ->   BJ1 E", "TIRED   ->   PIT DESK   ->   ASSIGN", "DESK   ->   BREAK   ->   RESTED",
            "BJ1 E   ->   TAKE OVER   ->   SPACE", "ROULETTE E   ->   TAKE OVER   ->   SPACE", "RESTED COVER   ->   TABLE SERVICE",
            "GREEN   ->   YELLOW   ->   RED   |   HANDS + HELP + HAPPY" };
        lessonDemo.text = demo[shownLesson];
        demonstrationProp.sprite = shownLesson == 2 || shownLesson == 9 ? CasinoPixelArt.Card :
            shownLesson == 3 || shownLesson == 4 || shownLesson == 10 ? CasinoPixelArt.Wheel : CasinoPixelArt.Chip;
    }

    public void DismissLesson()
    {
        if (!lessonOpen) return;
        if (!reviewLesson)
        {
            explainedLessons[shownLesson] = true;
            if (shownLesson == 12) objectivesExplained = true;
        }
        lessonOpen = false;
        lessonPanel.SetActive(false);
        AudioListener.pause = paused;
        dealers.SetPaused(paused);
        router.enabled = !paused;
        player.SetMovementEnabled(!paused && !AnyInteractionOpen());
        hud.SetActive(!paused && !AnyInteractionOpen());
    }

    private void BuildInterface()
    {
        canvas = CasinoMinigameHud.Create("Shift UI", 100);
        canvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1600f, 900f);
        menu = Panel("Welcome", new Vector2(780f, 430f));
        menu.AddComponent<CasinoTitleScreen>().Build(() =>
        {
            newGameRequested = true;
            menu.SetActive(false);
            intro.SetActive(true);
        }, () => BeginShift(CasinoDemoProgression.UnlockedShift, PlayerPrefs.GetInt(SaveTutorial, 0) == 0),
            CasinoDemoProgression.UnlockedShift > 1 || PlayerPrefs.GetInt(SaveTutorial, 0) != 0);

        intro = Panel("Introduction", new Vector2(780f, 400f));
        SpriteRenderer portraitSource = dealers.Dealers.Count > 0 && dealers.Dealers[0] != null
            ? dealers.Dealers[0].GetComponentInChildren<SpriteRenderer>() : null;
        if (portraitSource != null && portraitSource.sprite != null)
        {
            GameObject portrait = CasinoMinigamePresentation.CreateImage("Friendly dealer", intro.transform,
                new Vector2(-270f, 55f), new Vector2(120f, 145f), Color.white);
            CasinoPixelArt.SetSprite(portrait.GetComponent<Image>(), portraitSource.sprite, Color.white);
        }
        Label(intro.transform, "Introduction text",
            "HEY, PIT BOSS!\n\nServe 8 hands, help 4 times and keep 60% happy!\nNew Game resets progress and upgrades.\nI'll show you the ropes as we go!",
            new Vector2(75f, 55f), new Vector2(560f, 200f), 23);
        MakeButton(intro.transform, "Begin", "LET'S GO!", new Vector2(0f, -120f), () => BeginShift(1, true));
        MakeButton(intro.transform, "Back", "BACK", new Vector2(280f, -120f), () =>
        {
            newGameRequested = false;
            intro.SetActive(false);
            menu.SetActive(true);
        }, new Vector2(150, 40));
        MakeButton(intro.transform, "Skip", "SKIP TRAINING", new Vector2(0f, -175f), () =>
        {
            PlayerPrefs.SetInt(SaveTutorial, 1);
            PlayerPrefs.Save();
            BeginShift(1, false);
        });
        intro.SetActive(false);

        hud = new GameObject("Shift objective", typeof(RectTransform));
        hud.transform.SetParent(canvas.transform, false);
        var rect = (RectTransform)hud.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(18f, -18f);
        rect.sizeDelta = new Vector2(700f, 92f);
        Image backing = hud.AddComponent<Image>();
        CasinoHudStyle.Panel(backing, CasinoHudStyle.Ink);
        backing.raycastTarget = false;
        objectiveText = Label(hud.transform, "Objective", "KEEP THE CASINO HAPPY!", new Vector2(0f, 15f), new Vector2(680f, 35f), 21);
        timerText = Label(hud.transform, "Shift timer", "", new Vector2(-210f, -20f), new Vector2(260f, 30f), 17);
        skipButton = MakeButton(hud.transform, "Skip tutorial", "SKIP", new Vector2(275f, -20f), CompleteTutorial, new Vector2(110f, 32f));
        arrow = Label(canvas.transform, "Objective arrow", "▼", Vector2.zero, new Vector2(50f, 50f), 34);
        arrow.font = CasinoPixelFont.Symbols;
        arrow.color = CasinoHudStyle.Gold;
        arrow.gameObject.SetActive(false);
        hud.SetActive(false);

        results = Panel("Shift Results", new Vector2(800f, 660f));
        resultText = Label(results.transform, "Results", "", new Vector2(0f, 75f), new Vector2(740f, 440f), 22);
        resultText.font = CasinoPixelFont.Symbols;
        nextButton = MakeButton(results.transform, "Next shift", "UPGRADES", new Vector2(0f, -180f), ShowUpgrades);
        MakeButton(results.transform, "Retry", "RETRY SHIFT", new Vector2(-190f, -250f), RestartShift);
        MakeButton(results.transform, "Main Menu", "MAIN MENU", new Vector2(190f, -250f), ReturnToMainMenu);
        results.SetActive(false);
        upgrades = Panel("Shift Upgrades", new Vector2(800, 610));
        upgradeText = Label(upgrades.transform, "Upgrade balance", "", new Vector2(0, 235), new Vector2(730, 75), 23);
        upgradeButtons = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            CasinoDemoUpgrade choice = (CasinoDemoUpgrade)i;
            upgradeButtons[i] = MakeButton(upgrades.transform, choice.ToString(), "", new Vector2(0, 120 - i * 85),
                () => { CasinoDemoProgression.TryBuy(choice); RefreshUpgrades(); }, new Vector2(710, 65));
        }
        MakeButton(upgrades.transform, "Next shift", "NEXT SHIFT / REPLAY DEMO", new Vector2(0, -150), () =>
        {
            requestedShift = Mathf.Min(shift + 1, CasinoDemoProgression.LastShift);
            requestedTutorial = false;
            skipMenu = true;
            CasinoSoundscape.SetFocus(null);
            dealers.SetPaused(false);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }, new Vector2(500, 50));
        MakeButton(upgrades.transform, "Main Menu", "MAIN MENU", new Vector2(0, -235), ReturnToMainMenu);
        upgrades.SetActive(false);
        GameObject pauseShade = CasinoMinigamePresentation.CreateImage("Pause backdrop", canvas.transform,
            Vector2.zero, new Vector2(1600, 900), new Color(0, 0, 0, .7f), true);
        RectTransform shadeRect = (RectTransform)pauseShade.transform;
        shadeRect.anchorMin = Vector2.zero; shadeRect.anchorMax = Vector2.one;
        shadeRect.offsetMin = shadeRect.offsetMax = Vector2.zero;
        pausePanel = pauseShade;
        GameObject pauseCard = Panel("Pause card", new Vector2(520f, 410f));
        pauseCard.transform.SetParent(pauseShade.transform, false);
        Label(pauseCard.transform, "Pause title", "TAKE A BREATHER!", new Vector2(0f, 90f), new Vector2(480f, 45f), 28);
        MakeButton(pauseCard.transform, "Resume", "RESUME  •  P / ESC", new Vector2(0f, 0f), () => SetPaused(false));
        MakeButton(pauseCard.transform, "Main Menu", "MAIN MENU", new Vector2(0f, -80f), ReturnToMainMenu);
        MakeButton(pauseCard.transform, "Restart", "RESTART SHIFT", new Vector2(0f, -155f), RestartShift);
        pausePanel.SetActive(false);
        lessonPanel = CasinoMinigamePresentation.CreateImage("Tutorial explanation", canvas.transform,
            Vector2.zero, new Vector2(1600, 900), new Color(0, 0, 0, .8f), true);
        RectTransform lessonShade = (RectTransform)lessonPanel.transform;
        lessonShade.anchorMin = Vector2.zero; lessonShade.anchorMax = Vector2.one;
        lessonShade.offsetMin = lessonShade.offsetMax = Vector2.zero;
        GameObject lessonCard = Panel("Tiger's lesson", new Vector2(1180, 740));
        lessonCard.transform.SetParent(lessonPanel.transform, false);
        lessonTitle = Label(lessonCard.transform, "Lesson title", "", new Vector2(100, 260), new Vector2(880, 95), 29);
        lessonTitle.color = CasinoHudStyle.Gold;
        lessonText = Label(lessonCard.transform, "Lesson instructions", "", new Vector2(105, 15), new Vector2(850, 365), 23);
        lessonText.horizontalOverflow = HorizontalWrapMode.Wrap;
        lessonText.verticalOverflow = VerticalWrapMode.Truncate;
        lessonDemo = Label(lessonCard.transform, "Animated demonstration", "", new Vector2(0, -200), new Vector2(1120, 60), 20);
        lessonDemo.color = CasinoHudStyle.Gold;
        GameObject demoProp = CasinoMinigamePresentation.CreateImage("Practice animation prop", lessonCard.transform,
            new Vector2(-190, -235), new Vector2(40, 40), Color.white);
        demonstrationProp = demoProp.GetComponent<Image>();
        demonstrationProp.preserveAspect = true;
        demonstrationArrow = Label(lessonCard.transform, "Practice animation arrow", "->",
            new Vector2(-70, -235), new Vector2(70, 35), 25);
        demonstrationArrow.color = CasinoHudStyle.Gold;
        GameObject guide = CasinoMinigamePresentation.CreateImage("Tiger guide portrait", lessonCard.transform,
            new Vector2(-450, 55), new Vector2(165, 230), Color.white);
        guidePortrait = guide.GetComponent<Image>();
        guidePortrait.preserveAspect = true;
        foreach (AIDealer dealer in dealers.Dealers)
            if (dealer != null && dealer.DealerId == "Tiger") guideVisual = dealer.GetComponentInChildren<PixelCharacterVisual>();
        Label(lessonCard.transform, "Guide name", "TIGER\nYOUR PIT PAL", new Vector2(-450, -130), new Vector2(200, 75), 20);
        MakeButton(lessonCard.transform, "Practise", "LET ME TRY!  ENTER", new Vector2(0, -290), DismissLesson, new Vector2(350, 54));
        MakeButton(lessonCard.transform, "Skip training", "SKIP TRAINING", new Vector2(375, -290), CompleteTutorial, new Vector2(270, 44));
        MakeButton(lessonCard.transform, "Previous lesson", "<", new Vector2(-490, -290),
            () => ShowLesson(shownLesson - 1, true), new Vector2(65, 44));
        MakeButton(lessonCard.transform, "Next lesson", ">", new Vector2(-380, -290),
            () => ShowLesson(shownLesson + 1, true), new Vector2(65, 44));
        lessonPanel.SetActive(false);
        MakeButton(hud.transform, "Review training", "HELP", new Vector2(140, -20),
            () => ShowLesson(Mathf.Min(TutorialStage, 12), true), new Vector2(100, 32));
    }

    private void ShowUpgrades()
    {
        if (!passed) return;
        results.SetActive(false);
        upgrades.SetActive(true);
        CasinoSoundscape.SetFocus(upgrades.transform);
        RefreshUpgrades();
    }

    private void RefreshUpgrades()
    {
        upgradeText.text = $"PIT PERKS!  •  {CasinoDemoProgression.Credits} STAMPS\n" +
            (shift == CasinoDemoProgression.LastShift ? "DEMO COMPLETE! THANKS FOR PLAYING." : "BUY A PERK OR SAVE FOR NEXT SHIFT.");
        string[] names = { "HAPPY CUSTOMERS: +5s PATIENCE", "COMFY BREAKS: 20% FASTER RECOVERY", "PACK PRACTICE: +5s PER ORDER" };
        for (int i = 0; i < upgradeButtons.Length; i++)
        {
            bool owned = CasinoDemoProgression.Owns((CasinoDemoUpgrade)i);
            upgradeButtons[i].interactable = !owned && CasinoDemoProgression.Credits >= CasinoDemoProgression.UpgradeCost;
            upgradeButtons[i].GetComponentInChildren<Text>().text = names[i] + (owned ? "  •  OWNED" : "  •  3 STAMPS");
        }
    }

    private GameObject Panel(string name, Vector2 size)
    {
        GameObject panel = CasinoMinigamePresentation.CreateImage(name, canvas.transform, Vector2.zero, size, CasinoHudStyle.Ink, true);
        CasinoHudStyle.Panel(panel.GetComponent<Image>(), CasinoHudStyle.Ink);
        return panel;
    }

    private static Text Label(Transform parent, string name, string text, Vector2 position, Vector2 size, int fontSize) =>
        CasinoMinigamePresentation.CreateText(parent, name, text, position, size, fontSize, Color.white);

    private static Button MakeButton(Transform parent, string name, string text, Vector2 position,
        UnityEngine.Events.UnityAction action, Vector2? size = null)
    {
        Vector2 dimensions = size ?? new Vector2(320f, 50f);
        GameObject root = CasinoMinigamePresentation.CreateImage(name, parent, position, dimensions, new Color(0.35f, 0.48f, 0.30f), true);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = root.GetComponent<Image>();
        CasinoHudStyle.Button(button);
        button.onClick.AddListener(action);
        Label(root.transform, "Label", text, Vector2.zero, dimensions - new Vector2(8f, 4f), 19);
        return button;
    }
}
