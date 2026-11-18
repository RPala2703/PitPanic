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
    public static bool GameplayFrozen => Active != null && Active.ending;
    public bool IsTraining => running && tutorial;
    public bool IsTrainingCashOut => IsTraining && TutorialStage == 5;
    public bool IsTrainingFloatRefill => IsTraining && TutorialStage == 6;

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
            CasinoMinigameType.Shuffle => TutorialStage <= 2 && source.gameObject == blackjack.gameObject,
            CasinoMinigameType.Cover => TutorialStage <= 4 && source.gameObject == roulette.gameObject,
            CasinoMinigameType.Spin => source.gameObject == roulette.gameObject,
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
    private AIDealer tutorialRelievedDealer;
    private Canvas canvas;
    private GameObject menu;
    private GameObject levelSelect;
    private CasinoEmployeeHandbook handbook;
    private GameObject hud;
    private GameObject results;
    private CasinoResultsScreen resultsScreen;
    private CasinoShiftFinale finale;
    private GameObject pausePanel;
    private Button[] levelButtons;
    private Text[] levelButtonLabels;
    private bool newGameRequested;
    private bool passed;
    private Text objectiveText;
    private Text timerText;
    private Text tipsText;
    private float tipPulse;
    private Text arrow;
    private Transform arrowTarget;
    private Button skipButton;
    private Vector3 movementStart;
    private float remaining;
    private float nextGuidedSpawn;
    private float nextShufflePreparation;
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
    private int serviceStart;
    private int shift;
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
        {
            station.TaskSucceeded += OnTaskSucceeded;
            station.TaskMissed += OnTaskMissed;
        }
        CasinoCustomerAI.ServiceTimedOut += OnServiceTimedOut;
        foreach (CasinoMinigame game in minigames)
        {
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (TryStartRequestedDevelopmentLevel()) return;
#endif
        if (skipMenu)
        {
            skipMenu = false;
            BeginShift(requestedShift, requestedTutorial);
            requestedTutorial = false;
        }
        else
            menu.SetActive(true);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static bool developmentLevelConsumed;

    private bool TryStartRequestedDevelopmentLevel()
    {
        if (developmentLevelConsumed) return false;
        string[] args = Environment.GetCommandLineArgs();
        int previewIndex = Array.IndexOf(args, "--pit-results-preview");
        if (previewIndex >= 0)
        {
            developmentLevelConsumed = true;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            int previewLevel = ReadPreviewArgument(args, "--pit-results-preview-level", 1);
            int previewStars = ReadPreviewArgument(args, "--pit-results-preview-stars", 2);
            string previewOutcome = ReadPreviewString(args, "--pit-results-preview-outcome", null);
            if (previewLevel < 1 || previewLevel > CasinoDemoProgression.LastShift ||
                previewStars < 0 || previewStars > 3 ||
                previewOutcome != null && previewOutcome != "pass" && previewOutcome != "fail")
                throw new ArgumentException("Results preview requires level 1-3, outcome pass|fail, and stars 0-3.");

            BeginShift(previewLevel, false);
            DismissLesson();
            if (previewOutcome != null)
            {
                ShowSyntheticResultsPreview(previewLevel, previewOutcome == "pass", previewStars);
                Debug.Log($"Opened a visual-only Level {previewLevel} {previewOutcome} results fixture; no shift data or campaign progress was saved.");
                return true;
            }
            spawner.SetAcceptingArrivals(false);
            FinishShift();
            Debug.Log("Opened the failure results preview without awarding or changing campaign progress.");
            return true;
        }
        int index = Array.IndexOf(args, "--pit-start-level");
        if (index < 0) return false;
        developmentLevelConsumed = true;
        if (index + 1 >= args.Length || !int.TryParse(args[index + 1], out int level) ||
            level < 1 || level > CasinoDemoProgression.LastShift)
        {
            Debug.LogError("--pit-start-level requires a valid campaign level number.");
            return false;
        }
        Time.timeScale = 1f;
        AudioListener.pause = false;
        BeginShift(level, false);
        Debug.Log($"Started development level {level}.");
        return true;
    }

    private static int ReadPreviewArgument(string[] args, string name, int fallback)
    {
        int index = Array.IndexOf(args, name);
        if (index < 0) return fallback;
        if (index + 1 >= args.Length || !int.TryParse(args[index + 1], out int value))
            throw new ArgumentException($"{name} requires an integer value.");
        return value;
    }

    private static string ReadPreviewString(string[] args, string name, string fallback)
    {
        int index = Array.IndexOf(args, name);
        if (index < 0) return fallback;
        if (index + 1 >= args.Length) throw new ArgumentException($"{name} requires a value.");
        return args[index + 1].ToLowerInvariant();
    }

    private void ShowSyntheticResultsPreview(int level, bool previewPassed, int stars)
    {
        running = false;
        passed = previewPassed;
        router.enabled = false;
        player.SetMovementEnabled(false);
        dealers.SetPaused(true);
        hud.SetActive(false);
        results.SetActive(true);
        CasinoSoundscape.SetFocus(results.transform);
        CasinoSoundscape.PlayResultsMusic(previewPassed);
        CasinoSoundscape.Play(previewPassed ? CasinoSoundCue.Success : CasinoSoundCue.Disappointed,
            player.transform.position, results.transform);
        int customers = 8;
        int satisfaction = previewPassed ? 88 : 42;
        int tasks = previewPassed ? 7 : 2;
        int misses = previewPassed ? 1 : 7;
        int tips = previewPassed ? 1250 : 200;
        int tipCount = previewPassed ? 3 : 1;
        int tipBonus = CasinoTipPolicy.Default.StarBonus(tips);
        string explanation = previewPassed
            ? "PREVIEW ONLY | Shift passed; casino goals met."
            : $"PREVIEW ONLY | Satisfaction {satisfaction}% (need 60%); tasks {tasks}/4; missed {misses} (max 5).";
        resultsScreen.Show(level, previewPassed, previewPassed ? stars : 0,
            Mathf.Max(CasinoDemoProgression.BestStars(level), previewPassed ? stars : 0),
            customers, satisfaction, tasks, misses, 0, tips, tipCount, tipBonus,
            explanation,
            RequiredResultSprite($"Results/Backgrounds/Level{level}_{(previewPassed ? "Pass" : "Fail")}_Background"),
            RequiredResultSprite("Results/UI/Results_Passed"),
            RequiredResultSprite("Results/UI/Results_Failed"),
            RequiredResultSprite("Results/UI/Results_Star_Filled"),
            RequiredResultSprite("Results/UI/Results_Star_Hollow"),
            RequiredResultSprite("Results/UI/Results_Info_Panel"),
            previewPassed && level < CasinoDemoProgression.LastShift);
    }
#endif

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
            int stage = TutorialStage;
            if (stage <= 1) PrepareTutorialLesson(2);
            if (stage <= 3) PrepareTutorialLesson(4);
            if (stage == 0 && explainedLessons[0] && (player.transform.position - movementStart).sqrMagnitude >= 4f) moved = true;
            UpdateTutorial();
        }

        if (!ending)
        {
            remaining = Mathf.Max(tutorial ? 180f : 0f, remaining - Time.deltaTime);
            CasinoSoundscape.SetIntensity(1 - remaining / shiftSeconds);
            spawner.SetShiftProgress(1 - remaining / shiftSeconds);
            spawner.SetFinale(remaining <= 60f);
            if (!tutorial) finale.Tick(remaining);
            if (remaining <= 0f) FinishShift();
            if (!tutorial && !ending)
                objectiveText.text = $"TASKS COMPLETED {completedTasks}  •  MISSED {failedAttempts}";
        }
        tipsText.text = $"TIPS {CasinoTipJar.Format(CasinoTipJar.TotalPence)}";
        tipPulse = Mathf.Max(0, tipPulse - Time.deltaTime * 2.5f);
        tipsText.rectTransform.localScale = Vector3.one * (1 + .25f * tipPulse);
        tipsText.color = Color.Lerp(new Color(.55f, 1f, .55f), new Color(1f, .86f, .25f), tipPulse);
        int seconds = Mathf.CeilToInt(remaining);
        timerText.text = $"SHIFT {shift}  •  {seconds / 60:00}:{seconds % 60:00}";
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
        ending = false;
        finale.ResetPresentation();
        CasinoSoundscape.SetMusicTrack(CasinoMusicTrack.Casino);
        CasinoSoundscape.SetFocus(null);
        newGameRequested = false;
        shift = Mathf.Clamp(number, 1, CasinoDemoProgression.LastShift);
        CasinoCampaignPresentation.Apply(shift);
        tutorial = guided;
        arrowTarget = null;
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
        serviceStart = CustomerSatisfaction.ServiceStartCount;
        CasinoTipJar.ResetShift();
        CasinoPerformanceDirector.ResetForShift();
        if (pitDesk.GetComponent<CasinoTipJarProp>() == null) pitDesk.gameObject.AddComponent<CasinoTipJarProp>();
        movementStart = player.transform.position;
        menu.SetActive(false);
        levelSelect.SetActive(false);
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
        if (ending) return;
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
        CasinoSoundscape.SetMusicTrack(CasinoMusicTrack.Title);
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
        CasinoSoundscape.SetMusicTrack(CasinoMusicTrack.Casino);
        CasinoSoundscape.SetFocus(null);
        dealers.SetPaused(false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void UpdateTutorial()
    {
        int stage = TutorialStage;
        if (stage < explainedLessons.Length && !explainedLessons[stage])
        {
            if (AnyInteractionOpen() && !IsGuidedInspectionLesson(stage)) return;
            if (!PrepareTutorialLesson(stage))
            {
                if ((stage == 2 && !blackjack.IsDealerStaffed) || (stage == 4 && !roulette.IsDealerStaffed))
                    ShowDealerRecovery(stage == 2 ? "BJ1" : "ROULETTE");
                else if ((stage == 7 || stage == 11) && !HasReadyDealer())
                {
                    objectiveText.text = "PIT DESK: SEND EXHAUSTED DEALERS ON BREAK, THEN ASSIGN COVER.";
                    UpdateArrow(pitDesk.transform);
                }
                else
                {
                    objectiveText.text = "PREPARING THE NEXT LESSON...";
                    UpdateArrow(null);
                }
                return;
            }
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

    private bool PrepareTutorialLesson(int stage)
        {
            switch (stage)
            {
                case 1:
                    return true;
                case 3:
                    return true;
                case 2:
                    if (!blackjack.IsDealerStaffed) return false;
                    if (!blackjack.IsShuffleReady && !blackjack.GetComponent<CasinoMinigameStation>().IsTaskPending &&
                        Time.time >= nextShufflePreparation)
                    {
                        blackjack.QueueShuffleAssistanceTest();
                        queuedShuffle = true;
                        nextShufflePreparation = Time.time + 8f;
                    }
                    EnsureGuidedCustomer(CasinoCustomerGame.Blackjack1);
                    return blackjack.IsShuffleReady;
                case 4:
                    if (!roulette.IsDealerStaffed) return false;
                    if (!roulette.IsCoverReady && !rouletteScheduler.HasTutorialRequest)
                    {
                        rouletteScheduler.RequestTutorialAssistance();
                        queuedRoulette = true;
                    }
                    EnsureGuidedCustomer(CasinoCustomerGame.Roulette, 2);
                    return roulette.IsCoverReady;
                case 5:
                    if (cashDesk.ActiveCustomer == null && Time.time >= nextGuidedSpawn)
                    {
                        nextGuidedSpawn = Time.time + 2f;
                        spawner.TrySpawnGuidedCashOut();
                    }
                    cashDesk.PrepareTutorialTask();
                    return cashDesk.ActiveCustomer != null &&
                           cashDesk.GetComponent<CasinoMinigameStation>().IsTaskPending &&
                           cashDesk.GetComponent<CashDeskPackMinigame>().IsCashOutMode;
                case 6:
                    if (!requestedFloat)
                    {
                        trainingDestination = blackjack.GetComponent<CasinoFloatRefillDestination>();
                        if (trainingDestination == null)
                        {
                            Debug.LogError("Tutorial float destination BJ1 is missing.", this);
                            return false;
                        }
                        trainingDestination.RequestAttention();
                        requestedFloat = true;
                    }
                    cashDesk.PrepareTutorialTask();
                    foreach (CasinoFloatRefillOrder order in cashDesk.GetRequestedOrders())
                        if (order.Destination == trainingDestination)
                            return cashDesk.GetComponent<CasinoMinigameStation>().IsTaskPending;
                    return false;
                case 7:
                    if (!HasReadyDealer()) return false;
                    tutorialRelievedDealer = blackjack.AssignedDealer;
                    if (blackjack.AssignedDealer == null) return true;
                    if (!blackjack.AssignedDealer.IsAtStation) return false;
                    if (!requestedDealerBreak && blackjack.IsDealerStaffed)
                    {
                        blackjack.AssignedDealer.RequestTutorialBreak();
                        requestedDealerBreak = true;
                    }
                    return !blackjack.IsDealerStaffed || blackjack.AssignedDealer.NeedsBreak;
                case 8:
                    if (tutorialRelievedDealer != null && tutorialRelievedDealer.IsReadyAtPitDesk) return true;
                    bool relievedUnavailable = tutorialRelievedDealer == null ||
                        tutorialRelievedDealer.IsResting ||
                        tutorialRelievedDealer.State == DealerDutyState.TravelingToBreak ||
                        tutorialRelievedDealer.AssignedStation != null &&
                        tutorialRelievedDealer.AssignedStation.BlackjackTable != blackjack;
                    if (!relievedUnavailable) return false;
                    foreach (AIDealer dealer in dealers.Dealers)
                        if (dealer != null && dealer.IsReadyAtPitDesk) return true;
                    return false;
                case 9:
                    EnsureGuidedCustomer(CasinoCustomerGame.Blackjack1);
                    if (blackjack.IsDealerStaffed && blackjack.ActiveRound == null &&
                        blackjack.State == BlackjackTableState.WaitingForCustomers)
                        return false;
                    if (blackjack.AssignedDealer != null && blackjack.AssignedDealer.IsAtStation)
                    {
                        blackjack.AssignedDealer.RequestTutorialTakeover();
                        preparedManualBlackjack = true;
                    }
                    else if (blackjack.AssignedDealer == null) preparedManualBlackjack = true;
                    return !blackjack.IsDealerStaffed && preparedManualBlackjack;
                case 10:
                    EnsureGuidedCustomer(CasinoCustomerGame.Roulette, 2);
                    if (roulette.IsDealerStaffed &&
                        (roulette.Phase != RouletteRoundPhase.AcceptingBets &&
                         roulette.Phase != RouletteRoundPhase.AwaitingAssistance || roulette.CurrentBets.Count == 0))
                        return false;
                    if (roulette.AssignedDealer != null && roulette.AssignedDealer.IsAtStation)
                    {
                        roulette.AssignedDealer.RequestTutorialTakeover();
                        preparedManualRoulette = true;
                    }
                    else if (roulette.AssignedDealer == null) preparedManualRoulette = true;
                    return !roulette.IsDealerStaffed && preparedManualRoulette;
                case 11:
                    return HasReadyDealer();
                default:
                    return true;
            }
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
        arrowTarget = target;
        RefreshObjectiveArrow();
    }

    private void LateUpdate()
    {
        if (running && tutorial && !paused && !lessonOpen)
        {
            int stage = TutorialStage;
            if (stage == 1 && explainedLessons[1] && blackjack.IsInspectionOpen)
            {
                inspectedBlackjack = true;
                UpdateTutorial();
            }
            else if (stage == 3 && explainedLessons[3] && rouletteInspection.IsOpen)
            {
                inspectedRoulette = true;
                UpdateTutorial();
            }
        }
        if (arrow != null) RefreshObjectiveArrow();
    }

    private void RefreshObjectiveArrow()
    {
        bool visible = running && tutorial && !ending && !paused && !lessonOpen &&
            Time.timeScale > 0 && arrowTarget != null && !AnyInteractionOpen();
        arrow.gameObject.SetActive(visible);
        Transform target = arrowTarget;
        if (visible && Camera.main != null)
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
        ending = true;
        remaining = 0;
        spawner.SetAcceptingArrivals(false);
        rouletteScheduler.CancelTutorialAssistance();
        CasinoSoundscape.SetIntensity(0);
        dealers.SetPaused(true);
        router.enabled = false;
        player.SetMovementEnabled(false);
        arrow.gameObject.SetActive(false);
        timerText.text = $"SHIFT {shift}  •  00:00";
        CountUnfinishedTasks(out int pending, out int pendingMisses);
        int misses = failedAttempts + pendingMisses;
        int penalties = Mathf.Max(0, CustomerSatisfaction.PenaltyCount - penaltyStart);
        int serviceStarts = Mathf.Max(0, CustomerSatisfaction.ServiceStartCount - serviceStart);
        int customers = spawner.TotalSpawned;
        int satisfaction = customers > 0
            ? Mathf.Clamp(Mathf.RoundToInt(100f * (customers - penalties) / customers), 0, 100)
            : 100;
        int tips = CasinoTipJar.TotalPence;
        int tipBonus = CasinoTipPolicy.Default.StarBonus(tips);
        int performanceStars = CasinoDemoProgression.PerformanceStars(satisfaction, misses, tips);
        passed = CasinoDemoProgression.Passed(completedTasks, satisfaction, misses);
        int rating = passed ? performanceStars : 0;
        if (passed) CasinoDemoProgression.AwardShift(shift, rating);
        string explanation = BuildResultExplanation(passed, satisfaction, completedTasks,
            misses, serviceStarts);
        int finalCompleted = completedTasks;
        int finalTipCount = CasinoTipJar.TipCount;
        int bestStars = CasinoDemoProgression.BestStars(shift);
        bool hasNextLevel = passed && shift < CasinoDemoProgression.LastShift &&
            CasinoDemoProgression.UnlockedShift >= shift + 1;
        foreach (CasinoMinigame game in minigames)
            game.StopAllCoroutines();
        finale.ShowTimeUp(() =>
        {
            CasinoSoundscape.SetFocus(results.transform);
            CasinoSoundscape.PlayResultsMusic(passed);
            CasinoSoundscape.Play(passed ? CasinoSoundCue.Success : CasinoSoundCue.Disappointed,
                player.transform.position, results.transform);
            hud.SetActive(false);
            results.SetActive(true);
            resultsScreen.Show(shift, passed, rating, bestStars, serviceStarts,
            satisfaction, finalCompleted, misses, pending, tips, finalTipCount,
            tipBonus, explanation,
            RequiredResultSprite($"Results/Backgrounds/Level{shift}_{(passed ? "Pass" : "Fail")}_Background"),
            RequiredResultSprite("Results/UI/Results_Passed"),
            RequiredResultSprite("Results/UI/Results_Failed"),
            RequiredResultSprite("Results/UI/Results_Star_Filled"),
            RequiredResultSprite("Results/UI/Results_Star_Hollow"),
            RequiredResultSprite("Results/UI/Results_Info_Panel"),
            hasNextLevel);
        });
    }

    private void CountUnfinishedTasks(out int pending, out int pendingMisses)
    {
        pending = pendingMisses = 0;
        foreach (CasinoMinigameStation station in stations)
        {
            if (station == null || !station.IsTaskPending)
                continue;
            if (station.MinigameType == CasinoMinigameType.Pack &&
                cashDesk.ActiveCustomer == null && cashDesk.GetRequestedOrders().Count > 0)
                continue;
            pending++;
            if (!station.HasRecordedMiss) pendingMisses++;
        }
        foreach (CasinoFloatRefillDestination destination in cashDesk.Destinations)
            if (destination != null && destination.NeedsRefill())
            {
                pending++;
                pendingMisses++;
            }
        pending += pitDesk.PendingSecurityCases;
        pendingMisses += pitDesk.PendingSecurityCases;
    }

    private static Sprite RequiredResultSprite(string resourcePath)
    {
        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite == null)
            throw new InvalidOperationException($"Required results artwork was not imported: Resources/{resourcePath}");
        return sprite;
    }

    private static string BuildResultExplanation(bool shiftPassed, int satisfaction,
        int tasks, int misses, int serviceStarts)
    {
        if (shiftPassed) return "SHIFT PASSED! The casino goals are met.";

        System.Collections.Generic.List<string> reasons = new System.Collections.Generic.List<string>();
        if (serviceStarts == 0)
        {
            reasons.Add(satisfaction < 60
                ? "No customers were served; satisfaction is N/A and patience penalties missed the 60% target."
                : "No customers were served; customer satisfaction is N/A.");
        }
        else if (satisfaction < 60)
            reasons.Add($"Customer satisfaction was {satisfaction}%; 60% is required.");
        if (tasks < 4) reasons.Add($"Complete at least 4 tasks; only {tasks} were completed.");
        if (misses > 5) reasons.Add($"There were {misses} missed tasks; no more than 5 are allowed.");
        if (reasons.Count == 0) reasons.Add("The shift did not meet the casino's service goals.");
        return string.Join("  ", reasons);
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
                if (customer.IsVip && flavor.TryShow(DialogueSpeaker.Fox, DialogueExpression.Smug, "The gold one has arrived. So have the demands."))
                { vipRemark = true; return; }
        if (!floatRemark)
            foreach (CasinoFloatRefillDestination destination in FindObjectsByType<CasinoFloatRefillDestination>(FindObjectsSortMode.None))
                if (destination.NeedsRefill() && flavor.TryShow(DialogueSpeaker.Pig, DialogueExpression.Shocked, "We've got plenty of money. Just none in the right shapes."))
                { floatRemark = true; return; }
        if (!exhaustedRemark)
            foreach (AIDealer dealer in dealers.Dealers)
                if (dealer != null && dealer.IsAtStation && dealer.Energy <= 0 &&
                    flavor.TryShow(DialogueSpeaker.Bear, DialogueExpression.Neutral, "The dealer is out of energy. Apparently that's a finite resource."))
                { exhaustedRemark = true; return; }
        if (!securityRemark && pitDesk.PendingSecurityCases > 0 &&
            flavor.TryShow(DialogueSpeaker.Tiger, DialogueExpression.Determined, "CCTV wants a word. It doesn't speak, so you'll have to look."))
        { securityRemark = true; return; }
        if (payoutRemarkPending &&
            flavor.TryShow(DialogueSpeaker.Pig, DialogueExpression.Shocked, "Well. That used to be our money."))
        { payoutRemark = true; payoutRemarkPending = false; return; }
        if (tipRemarkPending &&
            flavor.TryShow(DialogueSpeaker.Pig, DialogueExpression.Happy, "A tip. Finally, a transaction I approve of."))
        { tipRemark = true; tipRemarkPending = false; }
    }
    private void OnTaskSucceeded(CasinoMinigameResult result) { if (running) completedTasks++; }
    private void OnTaskMissed() { if (running) failedAttempts++; }
    private void OnCashOutCompleted(CasinoMinigameResult result)
    {
        if (running && tutorial && TutorialStage == 5 && result.Success &&
            cashDesk.GetComponent<CashDeskPackMinigame>().IsCashOutMode) cashOutDone = true;
    }
    private void OnServiceTimedOut() { if (running) failedAttempts++; }
    private void OnShuffleCompleted(CasinoMinigameResult result) { if (running && tutorial && TutorialStage == 2 && result.Success) shuffleDone = true; }
    private void OnRouletteCompleted(CasinoMinigameResult result)
    {
        if (running && tutorial && TutorialStage == 4 && result.Success) { rouletteDone = true; queuedRoulette = false; }
    }
    private void OnFloatDelivered(CasinoFloatRefillDestination destination)
    {
        if (!running || destination == null) return;
        completedTasks++;
        if (tutorial && TutorialStage == 6 && destination.DestinationId == "BJ1") delivered = true;
    }

    private void OnDealerAssigned(AIDealer dealer, CasinoDealerStation station)
    {
        if (!running) return;
        completedTasks++;
        if (tutorial && TutorialStage == 7 && station != null && station.BlackjackTable == blackjack) assigned = true;
    }

    private void OnDealerBreak(AIDealer dealer)
    {
        if (!running) return;
        completedTasks++;
        if (tutorial && TutorialStage == 8) breakSent = true;
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
        Time.timeScale = 1f;
        AudioListener.pause = false;
        foreach (BlackjackTableInteraction table in FindObjectsByType<BlackjackTableInteraction>(FindObjectsSortMode.None))
            table.RoundSettled -= OnBlackjackSettled;
        if (roulette != null) roulette.BetSettled -= OnRouletteSettled;
        if (stations != null)
            foreach (CasinoMinigameStation station in stations)
                if (station != null)
                {
                    station.TaskSucceeded -= OnTaskSucceeded;
                    station.TaskMissed -= OnTaskMissed;
                }
        if (minigames != null)
            foreach (CasinoMinigame game in minigames)
                if (game != null)
                {
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
        if (!running || AnyInteractionOpen() && (reviewing || !IsGuidedInspectionLesson(lesson))) return;
        shownLesson = Mathf.Clamp(lesson, 0, 12);
        reviewLesson = reviewing;
        campaignBriefing = false;
        dialogueSkipLabel.text = reviewing ? "SKIP" : "SKIP TRAINING";
        lessonOpen = true;
        timeScaleBeforePause = Time.timeScale;
        Time.timeScale = 0f;
        dealers.SetPaused(true);
        AudioListener.pause = true;
        CasinoSoundscape.SetDialogueMusic(true);
        router.enabled = false;
        player.SetMovementEnabled(false);
        lessonPanel.SetActive(true);
        hud.SetActive(false);
        arrow.gameObject.SetActive(false);
        dialogue.Begin(LessonConversation(shownLesson), DismissLesson);
    }

    private bool IsGuidedInspectionLesson(int lesson) =>
        tutorial && !CasinoMinigamePresentation.IsOpen &&
        (lesson == 2 && blackjack.IsInspectionOpen || lesson == 4 && rouletteInspection.IsOpen);

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
        CasinoSoundscape.SetDialogueMusic(true);
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
        CasinoSoundscape.SetDialogueMusic(false);
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

    private static DialogueLine Line(DialogueSpeaker speaker, DialogueExpression expression, string text, float pauseBefore = 0f) =>
        new DialogueLine(speaker, expression, text, pauseBefore);

    private static DialogueLine[] LessonConversation(int stage)
    {
        if (stage == 0) return new[]
        {
            Line(DialogueSpeaker.Tiger, DialogueExpression.Happy, "Ah. There you are. Welcome to the casino."),
            Line(DialogueSpeaker.Penguin, DialogueExpression.Neutral, "Hello. I'm here for the interview."),
            Line(DialogueSpeaker.Tiger, DialogueExpression.Smug, "Excellent news. You've got the job."),
            Line(DialogueSpeaker.Penguin, DialogueExpression.Shocked, "We haven't had the interview."),
            Line(DialogueSpeaker.Tiger, DialogueExpression.Neutral, "No. We've saved quite a bit of time."),
            Line(DialogueSpeaker.Penguin, DialogueExpression.Determined, "Right. And what exactly is the job?", .5f),
            Line(DialogueSpeaker.Tiger, DialogueExpression.Happy, "You're our new pit boss."),
            Line(DialogueSpeaker.Penguin, DialogueExpression.Neutral, "What happened to the old one?"),
            Line(DialogueSpeaker.Tiger, DialogueExpression.Shocked, "Oh."),
            Line(DialogueSpeaker.Tiger, DialogueExpression.Smug, "We don't use that word anymore.", .7f),
            Line(DialogueSpeaker.Penguin, DialogueExpression.Shocked, "What word?"),
            Line(DialogueSpeaker.Tiger, DialogueExpression.Neutral, "Old."),
            Line(DialogueSpeaker.Bear, DialogueExpression.Neutral, "We've been advised not to discuss the incident.", .8f),
            Line(DialogueSpeaker.Penguin, DialogueExpression.Angry, "What incident?"),
            Line(DialogueSpeaker.Bear, DialogueExpression.Happy, "Exactly."),
            Line(DialogueSpeaker.Tiger, DialogueExpression.Determined, "Anyway. Shall we begin your training?", .6f),
            Line(DialogueSpeaker.Penguin, DialogueExpression.Smug, "Is it comprehensive?"),
            Line(DialogueSpeaker.Tiger, DialogueExpression.Happy, "You can walk, can't you?"),
            Line(DialogueSpeaker.Tiger, DialogueExpression.Determined, "Good. Use WASD or the arrow keys to move.")
        };
        DialogueLine guidance = stage switch
        {
            1 => Line(DialogueSpeaker.Tiger, DialogueExpression.Happy, "Walk to Blackjack 1 and press E to inspect the table."),
            2 => Line(DialogueSpeaker.Tiger, DialogueExpression.Determined, "Choose SHUFFLE!, then follow the card sequence."),
            3 => Line(DialogueSpeaker.Fox, DialogueExpression.Smug, "Go to Roulette and press E to inspect the bets."),
            4 => Line(DialogueSpeaker.Fox, DialogueExpression.Happy, "The wheel's stopped. In COVER!, click every winning bet."),
            5 => Line(DialogueSpeaker.Pig, DialogueExpression.Determined, "A customer needs cash. Click chips until the total matches exactly."),
            6 => Line(DialogueSpeaker.Pig, DialogueExpression.Neutral, "BJ1 needs more chips. Pack its refill, then carry the tray over."),
            7 => Line(DialogueSpeaker.Bear, DialogueExpression.Neutral, "That dealer's tired. Go to the Pit Desk and assign a replacement."),
            8 => Line(DialogueSpeaker.Bear, DialogueExpression.Determined, "The relieved dealer's back at the Pit Desk. Send them on break."),
            9 => Line(DialogueSpeaker.Tiger, DialogueExpression.Happy, "No replacement available. Choose TAKE OVER at BJ1 and deal the round."),
            10 => Line(DialogueSpeaker.Fox, DialogueExpression.Determined, "Roulette has no dealer. Choose TAKE OVER to run the table."),
            11 => Line(DialogueSpeaker.Bear, DialogueExpression.Happy, "A dealer's rested. Assign them to relieve you at the table."),
            _ => Line(DialogueSpeaker.Pig, DialogueExpression.Determined, "Watch task timers and keep customers happy until the shift ends.")
        };
        DialogueLine aside = stage switch
        {
            1 => Line(DialogueSpeaker.Penguin, DialogueExpression.Neutral, "What's wrong with it?"),
            2 => Line(DialogueSpeaker.Penguin, DialogueExpression.Shocked, "You hired a dealer who can't shuffle?"),
            3 => Line(DialogueSpeaker.Penguin, DialogueExpression.Determined, "Anything I should look for?"),
            4 => Line(DialogueSpeaker.Penguin, DialogueExpression.Neutral, "What if I miss one?"),
            5 => Line(DialogueSpeaker.Penguin, DialogueExpression.Smug, "Is approximate change allowed?"),
            6 => Line(DialogueSpeaker.Penguin, DialogueExpression.Shocked, "We're running out of money?"),
            7 => Line(DialogueSpeaker.Penguin, DialogueExpression.Shocked, "Are they still dealing?"),
            8 => Line(DialogueSpeaker.Penguin, DialogueExpression.Neutral, "And if I don't?"),
            9 => Line(DialogueSpeaker.Penguin, DialogueExpression.Shocked, "Am I qualified?"),
            10 => Line(DialogueSpeaker.Penguin, DialogueExpression.Angry, "Do I know how to run it?"),
            11 => Line(DialogueSpeaker.Penguin, DialogueExpression.Smug, "Do I get a break?"),
            _ => Line(DialogueSpeaker.Penguin, DialogueExpression.Neutral, "And then?")
        };
        DialogueLine answer = stage switch
        {
            1 => Line(DialogueSpeaker.Tiger, DialogueExpression.Smug, "We'll know when you get there.", .25f),
            2 => Line(DialogueSpeaker.Tiger, DialogueExpression.Neutral, "He's very good at the other bits.", .25f),
            3 => Line(DialogueSpeaker.Fox, DialogueExpression.Neutral, "Money, ideally.", .25f),
            4 => Line(DialogueSpeaker.Fox, DialogueExpression.Smug, "The customer will certainly notice.", .25f),
            5 => Line(DialogueSpeaker.Pig, DialogueExpression.Angry, "No.", .35f),
            6 => Line(DialogueSpeaker.Pig, DialogueExpression.Smug, "Only the conveniently shaped money.", .25f),
            7 => Line(DialogueSpeaker.Bear, DialogueExpression.Smug, "In the broadest sense.", .25f),
            8 => Line(DialogueSpeaker.Bear, DialogueExpression.Angry, "They'll continue being tired.", .25f),
            9 => Line(DialogueSpeaker.Tiger, DialogueExpression.Smug, "You're wearing the badge.", .25f),
            10 => Line(DialogueSpeaker.Fox, DialogueExpression.Happy, "We'll find out together.", .25f),
            11 => Line(DialogueSpeaker.Bear, DialogueExpression.Neutral, "You're management.", .35f),
            _ => Line(DialogueSpeaker.Pig, DialogueExpression.Happy, "We count the tips. Carefully.", .25f)
        };
        return new[] { guidance, aside, answer };
    }

    private static DialogueLine[] CampaignConversation(int level) => level switch
    {
        1 => new[] {
            Line(DialogueSpeaker.Tiger, DialogueExpression.Happy, "Welcome back. Keep the tables running and the customers happy."),
            Line(DialogueSpeaker.Penguin, DialogueExpression.Smug, "Any changes since last time?"),
            Line(DialogueSpeaker.Tiger, DialogueExpression.Shocked, "Several. None authorised.", .25f) },
        2 => new[] {
            Line(DialogueSpeaker.Fox, DialogueExpression.Smug, "Our VIPs are arriving. Very important people."),
            Line(DialogueSpeaker.Penguin, DialogueExpression.Neutral, "How will I recognise them?"),
            Line(DialogueSpeaker.Pig, DialogueExpression.Happy, "They're wearing gold."),
            Line(DialogueSpeaker.Penguin, DialogueExpression.Smug, "Subtle."),
            Line(DialogueSpeaker.Pig, DialogueExpression.Determined, "We suggested more gold.", .25f),
            Line(DialogueSpeaker.Fox, DialogueExpression.Neutral, "They bet more, wait less, and expect excellent service.") },
        _ => new[] {
            Line(DialogueSpeaker.Bear, DialogueExpression.Neutral, "Someone's been moving chips after the result."),
            Line(DialogueSpeaker.Penguin, DialogueExpression.Shocked, "Isn't that cheating?"),
            Line(DialogueSpeaker.Bear, DialogueExpression.Neutral, "Potentially.", .25f),
            Line(DialogueSpeaker.Penguin, DialogueExpression.Angry, "What else could it be?"),
            Line(DialogueSpeaker.Bear, DialogueExpression.Smug, "An unusually profitable misunderstanding.", .25f),
            Line(DialogueSpeaker.Tiger, DialogueExpression.Determined, "Review the footage at the Pit Desk. Then choose CHEATING or CLEAR."),
            Line(DialogueSpeaker.Penguin, DialogueExpression.Neutral, "Glad we've narrowed it down.") }
    };

    private void BuildInterface()
    {
        canvas = CasinoMinigameHud.Create("Shift UI", 100);
        canvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1600f, 900f);
        handbook = CasinoEmployeeHandbook.Create(canvas.transform);
        menu = Panel("Welcome", new Vector2(780f, 430f));
        menu.AddComponent<CasinoTitleScreen>().Build(() =>
        {
            newGameRequested = true;
            menu.SetActive(false);
            OpenLevelSelect();
        }, () => OpenLevelSelect(),
            CasinoDemoProgression.UnlockedShift > 1 || PlayerPrefs.GetInt(SaveTutorial, 0) != 0, handbook);

        levelSelect = Panel("Level Select", new Vector2(1040f, 640f));
        Label(levelSelect.transform, "Level Select Title", "LEVEL SELECT", new Vector2(0, 260),
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
        MakeButton(levelSelect.transform, "Level Select Back", "BACK", new Vector2(-145, -270),
            () => { newGameRequested = false; levelSelect.SetActive(false); menu.SetActive(true); },
            new Vector2(250, 48));
        Button levelHandbook = null;
        levelHandbook = MakeButton(levelSelect.transform, "Level Select Handbook", "HANDBOOK", new Vector2(145, -270),
            () => handbook.Open(() =>
            {
                if (UnityEngine.EventSystems.EventSystem.current != null)
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(levelHandbook.gameObject);
            }), new Vector2(250, 48));
        handbook.RegisterLauncher(levelHandbook);
        levelSelect.SetActive(false);

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

        results = new GameObject("Results screen root", typeof(RectTransform));
        results.transform.SetParent(canvas.transform, false);
        RectTransform resultsRect = (RectTransform)results.transform;
        resultsRect.anchorMin = Vector2.zero;
        resultsRect.anchorMax = Vector2.one;
        resultsRect.offsetMin = resultsRect.offsetMax = Vector2.zero;
        resultsScreen = results.AddComponent<CasinoResultsScreen>();
        resultsScreen.Build(results.transform, RestartShift,
            () => SelectLevel(shift + 1), OpenLevelSelect, ReturnToMainMenu);
        results.SetActive(false);
        finale = gameObject.AddComponent<CasinoShiftFinale>();
        finale.Build();
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
        CasinoSoundscape.StopMusic();
        menu.SetActive(false);
        resultsScreen.Hide();
        results.SetActive(false);
        levelSelect.SetActive(true);
        RefreshLevelSelect();
    }

    private void RefreshLevelSelect()
    {
        int unlocked = newGameRequested ? 1 : CasinoDemoProgression.UnlockedShift;
        string[] descriptions =
        {
            "The doors are open. Nobody checked why.",
            "Gold outfits. Short fuses. Large wagers.",
            "The cameras work. Unfortunately, so do the customers."
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
        handbook.RefreshLaunchers();
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
        CasinoSoundscape.SetMusicTrack(CasinoMusicTrack.Casino);
        requestedShift = level;
        requestedTutorial = level == 1 && PlayerPrefs.GetInt(SaveTutorial, 0) == 0;
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
