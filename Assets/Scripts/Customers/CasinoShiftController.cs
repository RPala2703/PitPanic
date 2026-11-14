using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class CasinoShiftController : MonoBehaviour
{
    public static CasinoShiftController Active { get; private set; }
    public bool IsTraining => running && tutorial;

    public bool AllowsTrainingAttention(CasinoTableAttention source, TableAttentionReason reason)
    {
        if (!IsTraining) return true;
        if (source == null) return false;
        bool bj = source.gameObject == blackjack.gameObject;
        bool rt = source.gameObject == roulette.gameObject;
        bool cash = source.gameObject == cashDesk.gameObject;
        bool pit = source.gameObject == pitDesk.gameObject;
        return TutorialStage switch
        {
            2 => bj && reason == TableAttentionReason.ShuffleAssistanceRequired,
            4 => rt && (reason == TableAttentionReason.RouletteCoverRequired ||
                        reason == TableAttentionReason.FloatRefillRequired) ||
                 cash && roulette.FloatDestination.NeedsRefill(),
            5 => cash,
            6 => cash || bj && reason == TableAttentionReason.FloatRefillRequired,
            7 or 8 => pit || bj && reason == TableAttentionReason.DealerBreakRequired,
            9 => bj && reason != TableAttentionReason.ShuffleAssistanceRequired ||
                 cash && blackjack.GetComponent<CasinoFloatRefillDestination>().NeedsRefill(),
            10 => rt && reason != TableAttentionReason.RouletteCoverRequired ||
                  cash && roulette.FloatDestination.NeedsRefill(),
            11 => pit || (bj || rt) && (reason == TableAttentionReason.TableUnstaffed ||
                    reason == TableAttentionReason.DealerExhausted || reason == TableAttentionReason.DealerBreakRequired),
            _ => false
        };
    }

    public bool AllowsTrainingTask(CasinoTableAttention source, CasinoMinigameType type) =>
        !IsTraining || type switch
        {
            CasinoMinigameType.Shuffle => TutorialStage == 2 && source.gameObject == blackjack.gameObject,
            CasinoMinigameType.Cover => TutorialStage == 4 && source.gameObject == roulette.gameObject,
            CasinoMinigameType.Spin => TutorialStage == 10 && source.gameObject == roulette.gameObject,
            CasinoMinigameType.Pack => TutorialStage == 5 || TutorialStage == 6 ||
                TutorialStage == 9 && blackjack.GetComponent<CasinoFloatRefillDestination>().NeedsRefill() ||
                (TutorialStage == 4 || TutorialStage == 10) && roulette.FloatDestination.NeedsRefill(),
            _ => false
        };
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
    private GameObject levelSelect;
    private GameObject intro;
    private GameObject hud;
    private GameObject results;
    private GameObject pausePanel;
    private Button nextButton;
    private Button[] levelButtons;
    private Text[] levelButtonLabels;
    private Text introTitle;
    private Text introText;
    private Button skipTrainingButton;
    private bool newGameRequested;
    private bool passed;
    private Text objectiveText;
    private Text timerText;
    private Text tipsText;
    private float tipPulse;
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
    private bool shuffleDone;
    private bool rouletteDone;
    private bool delivered;
    private bool assigned;
    private bool breakSent;
    private bool queuedShuffle;
    private bool queuedRoulette;
    private bool requestedFloat;
    private bool requestedDealerBreak;
    private int completedTasks;
    private int failedAttempts;
    private int penaltyStart;
    private int shift;
    private int selectedLevel = 1;
    private int lastStage = -1;
    private GameObject lessonPanel;
    private Text lessonTitle;
    private Text lessonText;
    private CasinoDialogue dialogue;
    private CasinoFlavorRemark flavor;
    private bool vipRemark, floatRemark, exhaustedRemark, securityRemark, payoutRemark, tipRemark;
    private bool payoutRemarkPending, tipRemarkPending;
    private Text dialogueSkipLabel;
    private float nextFlavorCheck;
    private bool lessonOpen;
    private bool campaignBriefing;
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
    public int TutorialStage => !moved ? 0 : !inspectedBlackjack ? 1 : !shuffleDone ? 2 :
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
        Active = this;
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
        CasinoCustomerAI.ServiceTimedOut += OnServiceTimedOut;
        foreach (AIDealer dealer in FindObjectsByType<AIDealer>(FindObjectsSortMode.None))
            dealer.EnergyDepleted += OnServiceTimedOut;
        foreach (CasinoMinigame game in minigames)
        {
            game.Completed += OnMinigameCompleted;
            game.Abandoned += OnMinigameAbandoned;
            if (game is BlackjackShuffleMinigame && game.GetComponent<BlackjackTableInteraction>() == blackjack)
                game.Completed += OnShuffleCompleted;
            if (game is RouletteTableMinigame)
                game.Completed += OnRouletteCompleted;
            if (game is CashDeskPackMinigame) game.Completed += OnCashOutCompleted;
        }
        cashDesk.FloatDelivered += OnFloatDelivered;
        dealers.DealerAssigned += OnDealerAssigned;
        dealers.DealerSentOnBreak += OnDealerBreak;
        pitDesk.SecurityCaseResolved += OnSecurityCaseResolved;
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
            if (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame))
                dialogue.Advance();
            else if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                DismissLesson();
            return;
        }

        if (Keyboard.current != null && (Keyboard.current.pKey.wasPressedThisFrame ||
            Keyboard.current.escapeKey.wasPressedThisFrame && (paused || !AnyInteractionOpen())))
            SetPaused(!paused);
        if (paused)
            return;
        UpdateFlavorRemarks();
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
                objectiveText.text = $"TASKS COMPLETED {completedTasks}  •  MISSED {failedAttempts}";
        }
        tipsText.text = $"TIPS {CasinoTipJar.Format(CasinoTipJar.TotalPence)}";
        tipPulse = Mathf.Max(0, tipPulse - Time.deltaTime * 2.5f);
        tipsText.rectTransform.localScale = Vector3.one * (1 + .25f * tipPulse);
        tipsText.color = Color.Lerp(new Color(.55f, 1f, .55f), new Color(1f, .86f, .25f), tipPulse);
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
        newGameRequested = false;
        shift = Mathf.Clamp(number, 1, CasinoDemoProgression.LastShift);
        CasinoCampaignPresentation.Apply(shift);
        tutorial = guided;
        spawner.SetGuidedPractice(guided);
        spawner.ConfigureShift(shift, false);
        foreach (AIDealer dealer in dealers.Dealers)
            if (dealer != null)
            {
                dealer.ConfigureShift(shift, false);
                dealer.TrainingProtected = guided;
            }
        foreach (CasinoMinigame game in minigames)
            if (game is CashDeskPackMinigame pack)
                pack.PackingBonusSeconds = 0;
        remaining = shiftSeconds;
        penaltyStart = CustomerSatisfaction.PenaltyCount;
        CasinoTipJar.ResetShift();
        CasinoPerformanceDirector.ResetForShift();
        if (pitDesk.GetComponent<CasinoTipJarProp>() == null) pitDesk.gameObject.AddComponent<CasinoTipJarProp>();
        movementStart = player.transform.position;
        menu.SetActive(false);
        levelSelect.SetActive(false);
        intro.SetActive(false);
        results.SetActive(false);
        hud.SetActive(true);
        skipButton.gameObject.SetActive(tutorial);
        router.enabled = true;
        player.SetMovementEnabled(true);
        dealers.SetPaused(false);
        spawner.SetAcceptingArrivals(!tutorial);
        running = true;
        if (guided) ShowLesson(0);
        else ShowCampaignBriefing();
    }

    private void SetPaused(bool value)
    {
        if (paused == value) return;
        if (value) timeScaleBeforePause = Time.timeScale;
        Time.timeScale = value ? 0f : Mathf.Max(.01f, timeScaleBeforePause);
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
        Time.timeScale = 1f;
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
        Time.timeScale = 1f;
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
                if (!queuedShuffle) { blackjack.QueueShuffleAssistanceTest(); queuedShuffle = true; }
                EnsureGuidedCustomer(CasinoCustomerGame.Blackjack1);
                bool ready = blackjack.IsShuffleReady;
                objectiveText.text = ready ? "BJ1: OPEN WITH E, THEN SHUFFLE!" : "BJ1: WAIT FOR THE DEALER'S SHUFFLE! CALL";
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
                    ? "ROULETTE: OPEN WITH E, THEN COVER!"
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
        pending += pitDesk.PendingSecurityCases;
        int penalties = Mathf.Max(0, CustomerSatisfaction.PenaltyCount - penaltyStart);
        int customers = spawner.TotalSpawned;
        int satisfaction = customers > 0
            ? Mathf.Clamp(Mathf.RoundToInt(100f * (customers - penalties) / customers), 0, 100)
            : 100;
        int tips = CasinoTipJar.TotalPence;
        int tipBonus = CasinoTipPolicy.Default.StarBonus(tips);
        int performanceStars = CasinoDemoProgression.PerformanceStars(satisfaction, failedAttempts + pending, tips);
        passed = CasinoDemoProgression.Passed(completedTasks, satisfaction, failedAttempts + pending);
        int rating = passed ? performanceStars : 0;
        if (passed) CasinoDemoProgression.AwardShift(shift, rating);
        nextButton.gameObject.SetActive(passed);
        nextButton.GetComponentInChildren<Text>().text =
            shift == CasinoDemoProgression.LastShift ? "REPLAY LEVELS" : "NEXT LEVEL";
        CasinoSoundscape.SetFocus(results.transform);
        CasinoSoundscape.Play(passed ? CasinoSoundCue.Success : CasinoSoundCue.Warning, player.transform.position, results.transform);
        string progressionMessage = passed
            ? shift == CasinoDemoProgression.LastShift ? "CAMPAIGN COMPLETE! REPLAY ANY LEVEL." :
                $"LEVEL {shift + 1} UNLOCKED!"
            : "GOAL: 4 HELPS, 60% HAPPY, AT MOST 5 MISSES.";
        resultText.text = $"{CasinoCampaignPresentation.LevelName(shift)}  {(passed ? "CLEARED!" : "TRY AGAIN!")}  " +
            $"{(passed ? new string('★', rating) : "NO STARS")}\nBEST  {new string('★', CasinoDemoProgression.BestStars(shift))}\n\n" +
            $"SATISFACTION  {(customers > 0 ? satisfaction + "%" : "NO CUSTOMERS")}\n" +
            $"TASKS COMPLETED  {completedTasks}\n" +
            $"TASKS MISSED  {failedAttempts + pending}  •  UNFINISHED  {pending}\n" +
            $"TIPS  {CasinoTipJar.Format(tips)}  ({CasinoTipJar.TipCount}){(tipBonus > 0 ? $"  ★ BONUS +{tipBonus}" : "")}\n" +
            (passed ? "\nPIG: A successful shift. The money agrees.\n" : "") +
            "\n" + progressionMessage;
        hud.SetActive(false);
        results.SetActive(true);
    }

    private void OnBlackjackSettled(BlackjackTableRound round)
    {
        if (!running) return;
        accounting.RecordBlackjack(round);
        if (!payoutRemark)
            foreach (BlackjackHand hand in round.CustomerHands)
                if (hand.WinningsPence >= 10000) { payoutRemarkPending = true; break; }
        if (blackjack.IsPlayerOperating && preparedManualBlackjack && blackjack.ActiveRound == round) manualBlackjackDone = true;
    }
    private void OnRouletteSettled(RouletteCustomerBet bet)
    {
        if (!running) return;
        accounting.RecordRoulette(bet);
        if (!payoutRemark && bet.WinningsPence >= 10000) payoutRemarkPending = true;
        if (roulette.IsPlayerOperating && preparedManualRoulette) manualRouletteDone = true;
    }
    private void OnTipDelivered(CasinoTipRecord tip)
    {
        tipPulse = 1;
        if (running && !tipRemark) tipRemarkPending = true;
    }

    private void UpdateFlavorRemarks()
    {
        if (tutorial || ending || AnyInteractionOpen() || Time.unscaledTime < nextFlavorCheck) return;
        nextFlavorCheck = Time.unscaledTime + 2f;
        if (!vipRemark)
            foreach (CasinoCustomerAI customer in FindObjectsByType<CasinoCustomerAI>(FindObjectsSortMode.None))
                if (customer.IsVip && flavor.TryShow(DialogueSpeaker.Fox, DialogueExpression.Smug, "A VIP has arrived. Please remain affordable."))
                { vipRemark = true; return; }
        if (!floatRemark)
            foreach (CasinoFloatRefillDestination destination in FindObjectsByType<CasinoFloatRefillDestination>(FindObjectsSortMode.None))
                if (destination.NeedsRefill() && flavor.TryShow(DialogueSpeaker.Pig, DialogueExpression.Shocked, "The chips are becoming theoretical."))
                { floatRemark = true; return; }
        if (!exhaustedRemark)
            foreach (AIDealer dealer in dealers.Dealers)
                if (dealer != null && dealer.IsAtStation && dealer.Energy <= 0 &&
                    flavor.TryShow(DialogueSpeaker.Bear, DialogueExpression.Neutral, "The dealer has stopped. This is a clue."))
                { exhaustedRemark = true; return; }
        if (!securityRemark && pitDesk.PendingSecurityCases > 0 &&
            flavor.TryShow(DialogueSpeaker.Tiger, DialogueExpression.Determined, "Security has noticed something. How observant."))
        { securityRemark = true; return; }
        if (payoutRemarkPending &&
            flavor.TryShow(DialogueSpeaker.Pig, DialogueExpression.Shocked, "That is a substantial amount of money."))
        { payoutRemark = true; payoutRemarkPending = false; return; }
        if (tipRemarkPending &&
            flavor.TryShow(DialogueSpeaker.Pig, DialogueExpression.Happy, "The jar has received a compliment."))
        { tipRemark = true; tipRemarkPending = false; }
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
    private void OnServiceTimedOut() { if (running) failedAttempts++; }
    private void OnShuffleCompleted(CasinoMinigameResult result) { if (running && result.Success) shuffleDone = true; }
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

    private void OnSecurityCaseResolved(bool correct)
    {
        if (!running) return;
        if (correct) completedTasks++;
        else failedAttempts++;
    }

    private void OnDestroy()
    {
        if (Active == this) Active = null;
        CasinoPerformanceDirector.TipDelivered -= OnTipDelivered;
        CasinoCustomerAI.ServiceTimedOut -= OnServiceTimedOut;
        foreach (AIDealer dealer in FindObjectsByType<AIDealer>(FindObjectsSortMode.None))
            dealer.EnergyDepleted -= OnServiceTimedOut;
        Time.timeScale = 1f;
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
                    game.Completed -= OnShuffleCompleted;
                    game.Completed -= OnRouletteCompleted;
                    game.Completed -= OnCashOutCompleted;
                }
        if (cashDesk != null) cashDesk.FloatDelivered -= OnFloatDelivered;
        if (pitDesk != null) pitDesk.SecurityCaseResolved -= OnSecurityCaseResolved;
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
        campaignBriefing = false;
        dialogueSkipLabel.text = reviewing ? "SKIP" : "SKIP TRAINING";
        lessonOpen = true;
        timeScaleBeforePause = Time.timeScale;
        Time.timeScale = 0f;
        dealers.SetPaused(true);
        AudioListener.pause = true;
        router.enabled = false;
        player.SetMovementEnabled(false);
        lessonPanel.SetActive(true);
        hud.SetActive(false);
        arrow.gameObject.SetActive(false);
        dialogue.Begin(LessonConversation(shownLesson), DismissLesson);
    }

    private float timeScaleBeforePause = 1f;

    private void ShowCampaignBriefing()
    {
        campaignBriefing = true;
        dialogueSkipLabel.text = "SKIP";
        lessonOpen = true;
        timeScaleBeforePause = Time.timeScale;
        Time.timeScale = 0f;
        dealers.SetPaused(true);
        AudioListener.pause = true;
        router.enabled = false;
        player.SetMovementEnabled(false);
        lessonPanel.SetActive(true);
        hud.SetActive(false);
        arrow.gameObject.SetActive(false);
        dialogue.Begin(CampaignConversation(shift), DismissLesson);
    }

    public void DismissLesson()
    {
        if (!lessonOpen) return;
        if (!reviewLesson)
        {
            if (!campaignBriefing)
            {
                explainedLessons[shownLesson] = true;
                if (shownLesson == 12) objectivesExplained = true;
            }
        }
        campaignBriefing = false;
        dialogue.Stop();
        lessonOpen = false;
        lessonPanel.SetActive(false);
        Time.timeScale = Mathf.Max(.01f, timeScaleBeforePause);
        AudioListener.pause = paused;
        dealers.SetPaused(paused);
        StartCoroutine(ResumeAfterDialogue());
        hud.SetActive(!paused && !AnyInteractionOpen());
    }

    private System.Collections.IEnumerator ResumeAfterDialogue()
    {
        yield return null;
        while (Keyboard.current != null && (Keyboard.current.enterKey.isPressed || Keyboard.current.spaceKey.isPressed ||
               Keyboard.current.eKey.isPressed || Keyboard.current.escapeKey.isPressed) ||
               Mouse.current != null && Mouse.current.leftButton.isPressed)
            yield return null;
        if (!running || lessonOpen || paused) yield break;
        router.enabled = true;
        player.SetMovementEnabled(!AnyInteractionOpen());
    }

    private static DialogueLine Line(DialogueSpeaker speaker, DialogueExpression expression, string text) =>
        new DialogueLine(speaker, expression, text);

    private static DialogueLine[] LessonConversation(int stage)
    {
        DialogueLine guidance = stage switch
        {
            0 => Line(DialogueSpeaker.Tiger, DialogueExpression.Happy, "Welcome, boss. Move with WASD or the arrow keys."),
            1 => Line(DialogueSpeaker.Tiger, DialogueExpression.Determined, "Walk to Blackjack 1 and press E to inspect the table."),
            2 => Line(DialogueSpeaker.Tiger, DialogueExpression.Determined, "At BJ1, press E, then SHUFFLE! Follow the card sequence."),
            3 => Line(DialogueSpeaker.Fox, DialogueExpression.Smug, "Walk to Roulette and press E to inspect the bets."),
            4 => Line(DialogueSpeaker.Fox, DialogueExpression.Determined, "For COVER!, mark every winning bet after the wheel stops."),
            5 => Line(DialogueSpeaker.Pig, DialogueExpression.Happy, "A customer needs cash. Click chips to make the exact change."),
            6 => Line(DialogueSpeaker.Pig, DialogueExpression.Shocked, "Tables run low on chips. Pack BJ1's refill and carry it over."),
            7 => Line(DialogueSpeaker.Bear, DialogueExpression.Neutral, "Tired dealers slow down. Assign a ready dealer at the Pit Desk."),
            8 => Line(DialogueSpeaker.Bear, DialogueExpression.Determined, "Send the tired dealer on break. They will return when rested."),
            9 => Line(DialogueSpeaker.Tiger, DialogueExpression.Shocked, "No replacement? Take over BJ1 and deal the hand yourself."),
            10 => Line(DialogueSpeaker.Fox, DialogueExpression.Determined, "Take over Roulette when its dealer needs a break."),
            11 => Line(DialogueSpeaker.Bear, DialogueExpression.Happy, "A rested teammate can take your place at the table."),
            _ => Line(DialogueSpeaker.Pig, DialogueExpression.Determined, "Watch task timers and keep customers satisfied until the shift ends.")
        };
        DialogueLine aside = stage switch
        {
            0 => Line(DialogueSpeaker.Penguin, DialogueExpression.Smug, "A promising amount of floor."),
            1 => Line(DialogueSpeaker.Penguin, DialogueExpression.Neutral, "The table appears to be doing its best."),
            2 => Line(DialogueSpeaker.Penguin, DialogueExpression.Shocked, "I assumed the cards came shuffled."),
            3 => Line(DialogueSpeaker.Fox, DialogueExpression.Neutral, "The wheel is entirely authorised."),
            4 => Line(DialogueSpeaker.Penguin, DialogueExpression.Determined, "I will count carefully."),
            5 => Line(DialogueSpeaker.Pig, DialogueExpression.Smug, "Customers appreciate receiving their money."),
            6 => Line(DialogueSpeaker.Penguin, DialogueExpression.Angry, "We should probably keep some."),
            7 => Line(DialogueSpeaker.Bear, DialogueExpression.Smug, "This is what breaks are for."),
            8 => Line(DialogueSpeaker.Penguin, DialogueExpression.Neutral, "A radical scheduling strategy."),
            9 => Line(DialogueSpeaker.Tiger, DialogueExpression.Smug, "The cards will respect your authority."),
            10 => Line(DialogueSpeaker.Penguin, DialogueExpression.Shocked, "I was not told about the wheel."),
            11 => Line(DialogueSpeaker.Bear, DialogueExpression.Neutral, "They have returned. That is useful."),
            _ => Line(DialogueSpeaker.Tiger, DialogueExpression.Happy, "If everyone is happy, we have done a shift.")
        };
        return new[] { guidance, aside };
    }

    private static DialogueLine[] CampaignConversation(int level) => level switch
    {
        1 => new[] {
            Line(DialogueSpeaker.Tiger, DialogueExpression.Happy, "Welcome back. Keep the tables running and the customers happy."),
            Line(DialogueSpeaker.Penguin, DialogueExpression.Smug, "I see the floor is still here.") },
        2 => new[] {
            Line(DialogueSpeaker.Fox, DialogueExpression.Smug, "Our VIPs wear gold and have a subtle glow."),
            Line(DialogueSpeaker.Pig, DialogueExpression.Happy, "They bet more and expect quick service. Keep their tables stocked."),
            Line(DialogueSpeaker.Penguin, DialogueExpression.Shocked, "Their patience is also expensive, I assume.") },
        _ => new[] {
            Line(DialogueSpeaker.Bear, DialogueExpression.Neutral, "Someone may be moving chips after the wheel stops."),
            Line(DialogueSpeaker.Tiger, DialogueExpression.Determined, "Review CCTV alerts at the Pit Desk before deciding."),
            Line(DialogueSpeaker.Penguin, DialogueExpression.Angry, "I had hoped the cameras were decorative.") }
    };

    private void BuildInterface()
    {
        canvas = CasinoMinigameHud.Create("Shift UI", 100);
        canvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1600f, 900f);
        menu = Panel("Welcome", new Vector2(780f, 430f));
        menu.AddComponent<CasinoTitleScreen>().Build(() =>
        {
            newGameRequested = true;
            menu.SetActive(false);
            OpenLevelSelect();
        }, () => OpenLevelSelect(),
            CasinoDemoProgression.UnlockedShift > 1 || PlayerPrefs.GetInt(SaveTutorial, 0) != 0);

        levelSelect = Panel("Level Select", new Vector2(1040f, 640f));
        Label(levelSelect.transform, "Level Select Title", "CHOOSE YOUR CASINO", new Vector2(0, 260),
            new Vector2(920, 60), 34).color = CasinoHudStyle.Gold;
        Label(levelSelect.transform, "Level Select Subtitle",
            "Replay any unlocked level. Earn stars by keeping the floor running smoothly.",
            new Vector2(0, 205), new Vector2(900, 45), 19);
        levelButtons = new Button[CasinoDemoProgression.LevelCount];
        levelButtonLabels = new Text[levelButtons.Length];
        for (int i = 0; i < levelButtons.Length; i++)
        {
            int level = i + 1;
            levelButtons[i] = MakeButton(levelSelect.transform, "Level " + level, "", new Vector2(0, 115 - i * 120),
                () => SelectLevel(level), new Vector2(850, 92));
            levelButtonLabels[i] = levelButtons[i].GetComponentInChildren<Text>();
        }
        MakeButton(levelSelect.transform, "Level Select Back", "BACK", new Vector2(0, -270),
            () => { newGameRequested = false; levelSelect.SetActive(false); menu.SetActive(true); },
            new Vector2(250, 48));
        levelSelect.SetActive(false);

        intro = Panel("Introduction", new Vector2(780f, 450f));
        SpriteRenderer portraitSource = dealers.Dealers.Count > 0 && dealers.Dealers[0] != null
            ? dealers.Dealers[0].GetComponentInChildren<SpriteRenderer>() : null;
        if (portraitSource != null && portraitSource.sprite != null)
        {
            GameObject portrait = CasinoMinigamePresentation.CreateImage("Friendly dealer", intro.transform,
                new Vector2(-270f, 55f), new Vector2(120f, 145f), Color.white);
            CasinoPixelArt.SetSprite(portrait.GetComponent<Image>(), portraitSource.sprite, Color.white);
        }
        introTitle = Label(intro.transform, "Introduction title", "", new Vector2(75, 145),
            new Vector2(560, 50), 27);
        introTitle.color = CasinoHudStyle.Gold;
        introText = Label(intro.transform, "Introduction text", "", new Vector2(75f, 55f),
            new Vector2(560f, 160f), 21);
        introText.horizontalOverflow = HorizontalWrapMode.Wrap;
        MakeButton(intro.transform, "Begin", "LET'S GO!", new Vector2(0f, -125f),
            () => BeginSelectedLevel());
        MakeButton(intro.transform, "Back", "BACK", new Vector2(280f, -120f), () =>
        {
            intro.SetActive(false);
            levelSelect.SetActive(true);
        }, new Vector2(150, 40));
        skipTrainingButton = MakeButton(intro.transform, "Skip", "SKIP TRAINING", new Vector2(0f, -180f), () =>
        {
            PlayerPrefs.SetInt(SaveTutorial, 1);
            PlayerPrefs.Save();
            BeginSelectedLevel(false);
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
        tipsText = Label(hud.transform, "Tip jar total", "TIPS £0", new Vector2(40f, -20f), new Vector2(170f, 30f), 17);
        CasinoPerformanceDirector.TipDelivered += OnTipDelivered;
        skipButton = MakeButton(hud.transform, "Skip tutorial", "SKIP", new Vector2(275f, -20f), CompleteTutorial, new Vector2(110f, 32f));
        arrow = Label(canvas.transform, "Objective arrow", "▼", Vector2.zero, new Vector2(50f, 50f), 34);
        arrow.font = CasinoPixelFont.Symbols;
        arrow.color = CasinoHudStyle.Gold;
        arrow.gameObject.SetActive(false);
        hud.SetActive(false);

        results = Panel("Shift Results", new Vector2(800f, 660f));
        resultText = Label(results.transform, "Results", "", new Vector2(0f, 75f), new Vector2(740f, 440f), 22);
        resultText.font = CasinoPixelFont.Symbols;
        nextButton = MakeButton(results.transform, "Next shift", "NEXT LEVEL", new Vector2(0f, -180f),
            OpenLevelSelect);
        MakeButton(results.transform, "Retry", "RETRY SHIFT", new Vector2(-190f, -250f), RestartShift);
        MakeButton(results.transform, "Main Menu", "MAIN MENU", new Vector2(190f, -250f), ReturnToMainMenu);
        results.SetActive(false);
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
        GameObject lessonCard = Panel("Tiger's lesson", new Vector2(1000, 340));
        lessonCard.transform.SetParent(lessonPanel.transform, false);
        lessonTitle = Label(lessonCard.transform, "Lesson title", "", new Vector2(-60, 116), new Vector2(600, 42), 26);
        lessonTitle.color = CasinoHudStyle.Gold;
        lessonText = Label(lessonCard.transform, "Lesson instructions", "", new Vector2(90, 28), new Vector2(620, 130), 24);
        lessonText.horizontalOverflow = HorizontalWrapMode.Wrap;
        lessonText.verticalOverflow = VerticalWrapMode.Truncate;
        GameObject guide = CasinoMinigamePresentation.CreateImage("Speaker portrait", lessonCard.transform,
            new Vector2(-360, 12), new Vector2(235, 220), Color.white);
        Image speakerPortrait = guide.GetComponent<Image>();
        speakerPortrait.preserveAspect = true;
        Text nextIndicator = Label(lessonCard.transform, "Continue indicator", "▼", new Vector2(380, -38), new Vector2(36, 30), 20);
        nextIndicator.font = CasinoPixelFont.Symbols;
        dialogue = lessonCard.AddComponent<CasinoDialogue>();
        dialogue.Configure((RectTransform)lessonCard.transform, speakerPortrait, lessonTitle, lessonText, nextIndicator);
        MakeButton(lessonCard.transform, "Practise", "NEXT", new Vector2(-40, -125), () => dialogue.Advance(), new Vector2(240, 48));
        Button dialogueSkip = MakeButton(lessonCard.transform, "Skip training", "SKIP", new Vector2(235, -125),
            () => { if (campaignBriefing || reviewLesson) DismissLesson(); else CompleteTutorial(); }, new Vector2(240, 48));
        dialogueSkipLabel = dialogueSkip.GetComponentInChildren<Text>();
        Button backdrop = lessonPanel.AddComponent<Button>();
        backdrop.transition = Selectable.Transition.None;
        backdrop.onClick.AddListener(() => dialogue.Advance());
        lessonPanel.SetActive(false);
        flavor = hud.AddComponent<CasinoFlavorRemark>();
        flavor.Configure(hud.transform);
        MakeButton(hud.transform, "Review training", "HELP", new Vector2(140, -20),
            () => ShowLesson(Mathf.Min(TutorialStage, 12), true), new Vector2(100, 32));
    }

    private void OpenLevelSelect()
    {
        menu.SetActive(false);
        results.SetActive(false);
        levelSelect.SetActive(true);
        RefreshLevelSelect();
    }

    private void RefreshLevelSelect()
    {
        int unlocked = newGameRequested ? 1 : CasinoDemoProgression.UnlockedShift;
        string[] descriptions =
        {
            "Learn the floor: Blackjack, Roulette, cash desk and dealer breaks.",
            "Luxury tables, bigger VIP bets and customers with less patience.",
            "Late-night casino, VIP guests and evidence-based security reviews."
        };
        for (int i = 0; i < levelButtons.Length; i++)
        {
            int level = i + 1;
            bool available = level <= unlocked;
            string stars = new string('★', CasinoDemoProgression.BestStars(level)) +
                new string('☆', 3 - CasinoDemoProgression.BestStars(level));
            levelButtonLabels[i].text = $"{level}. {CasinoCampaignPresentation.LevelName(level)}    {stars}\n" +
                (available ? descriptions[i] : "LOCKED  •  Clear the previous level to unlock");
            levelButtons[i].interactable = available;
        }
        CasinoSoundscape.SetFocus(levelSelect.transform);
    }

    private void SelectLevel(int level)
    {
        if (newGameRequested)
        {
            CasinoDemoProgression.StartNewGame();
            newGameRequested = false;
        }
        if (level > CasinoDemoProgression.UnlockedShift) return;
        CasinoDemoProgression.ValidateLevel(level);
        selectedLevel = level;
        levelSelect.SetActive(false);
        introTitle.text = CasinoCampaignPresentation.LevelName(level);
        introText.text = level switch
        {
            1 => "Opening Night. The staff are ready, in a manner of speaking.",
            2 => "High Rollers. The guests have arrived with expectations.",
            _ => "Shady Business. The cameras have started paying attention."
        };
        introText.color = Color.white;
        intro.transform.Find("Begin").GetComponentInChildren<Text>().text =
            level == 1 ? "LET'S GO!" : level == 2 ? "MEET THE VIPS!" : "START THE SHIFT!";
        skipTrainingButton.gameObject.SetActive(level == 1);
        intro.SetActive(true);
        CasinoSoundscape.SetFocus(intro.transform);
    }

    private void BeginSelectedLevel(bool? forceGuided = null)
    {
        intro.SetActive(false);
        bool guided = forceGuided ?? (selectedLevel == 1 && PlayerPrefs.GetInt(SaveTutorial, 0) == 0);
        requestedShift = selectedLevel;
        requestedTutorial = guided;
        skipMenu = true;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        CasinoSoundscape.SetFocus(null);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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
