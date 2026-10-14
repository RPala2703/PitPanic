using UnityEngine;

[DisallowMultipleComponent]
public sealed class RouletteAssistanceScheduler : MonoBehaviour
{
    [SerializeField] private CasinoDealerStation rouletteDealerStation;
    [SerializeField] private CasinoMinigameStation minigameStation;
    [SerializeField] private RouletteTableMinigame minigame;
    [SerializeField] private Transform physicalWheel;
    [SerializeField, Min(1f)] private float firstTaskMinSeconds = 25f;
    [SerializeField, Min(1f)] private float firstTaskMaxSeconds = 50f;
    [SerializeField, Min(1f)] private float checkIntervalMinSeconds = 40f;
    [SerializeField, Min(1f)] private float checkIntervalMaxSeconds = 90f;
    [SerializeField, Range(0f, 1f)] private float taskChancePerCheck = 0.35f;
    [SerializeField, Min(0f)] private float completionCooldownSeconds = 60f;
    [SerializeField, Min(1f)] private float normalSpinMinSeconds = 6f;
    [SerializeField, Min(1f)] private float normalSpinMaxSeconds = 13f;

    private float nextTaskCheckTime;
    private float nextNormalSpinTime;
    private float normalSpinElapsed;
    private float normalSpinDuration;
    private float normalSpinStartAngle;
    private float normalSpinEndAngle;
    private bool normalSpinRunning;
    private bool subscribed;

    public bool IsConfigured => rouletteDealerStation != null && rouletteDealerStation.IsConfigured &&
                                minigameStation != null && minigameStation.IsConfigured &&
                                minigame != null && minigame.IsConfigured && physicalWheel != null;

    public void Configure(
        CasinoDealerStation dealerStation,
        CasinoMinigameStation station,
        RouletteTableMinigame rouletteMinigame,
        Transform wheel)
    {
        rouletteDealerStation = dealerStation;
        minigameStation = station;
        minigame = rouletteMinigame;
        physicalWheel = wheel;
        BindStationEvents();
    }

    public static bool CanRequestTask(
        bool schedulerConfigured,
        bool dealerActiveAtTable,
        bool stationTaskPending,
        bool rouletteMinigameRunning,
        bool anotherMinigameRunning,
        bool tableSafe)
    {
        return schedulerConfigured && dealerActiveAtTable && !stationTaskPending &&
               !rouletteMinigameRunning && !anotherMinigameRunning && tableSafe;
    }

    public static float GetNextCheckDelay(float randomInterval, float cooldown)
    {
        return Mathf.Max(0f, randomInterval, cooldown);
    }

    private void OnEnable()
    {
        BindStationEvents();
    }

    private void Start()
    {
        if (!IsConfigured)
        {
            Debug.LogError($"{nameof(RouletteAssistanceScheduler)} on {name} needs its dealer station, roulette task station, minigame, and physical wheel.", this);
            enabled = false;
            return;
        }

        nextTaskCheckTime = Time.time + Random.Range(firstTaskMinSeconds, Mathf.Max(firstTaskMinSeconds, firstTaskMaxSeconds));
        nextNormalSpinTime = Time.time + Random.Range(normalSpinMinSeconds, Mathf.Max(normalSpinMinSeconds, normalSpinMaxSeconds));
        BindStationEvents();
    }

    private void Update()
    {
        UpdateNormalDealerActivity();
        if (Time.time < nextTaskCheckTime)
        {
            return;
        }

        nextTaskCheckTime = Time.time + Random.Range(
            checkIntervalMinSeconds, Mathf.Max(checkIntervalMinSeconds, checkIntervalMaxSeconds));
        if (CanRequestAssistance() && Random.value < taskChancePerCheck)
        {
            minigameStation.RequestMinigame(
                CasinoMinigameType.Spin,
                TableAttentionReason.RouletteSpinRequired);
        }
    }

    private void OnDisable()
    {
        if (subscribed && minigameStation != null)
        {
            minigameStation.TaskSucceeded -= OnTaskSucceeded;
            subscribed = false;
        }
    }

    private void BindStationEvents()
    {
        if (!isActiveAndEnabled || minigameStation == null || subscribed)
        {
            return;
        }

        minigameStation.TaskSucceeded += OnTaskSucceeded;
        subscribed = true;
    }

    private void OnTaskSucceeded(CasinoMinigameResult result)
    {
        if (!result.Success)
        {
            return;
        }

        float interval = Random.Range(
            checkIntervalMinSeconds, Mathf.Max(checkIntervalMinSeconds, checkIntervalMaxSeconds));
        nextTaskCheckTime = Time.time + GetNextCheckDelay(interval, completionCooldownSeconds);
    }

    private bool CanRequestAssistance()
    {
        AIDealer dealer = rouletteDealerStation != null ? rouletteDealerStation.AssignedDealer : null;
        bool dealerActive = dealer != null && dealer.isActiveAndEnabled &&
                            dealer.AssignedStation == rouletteDealerStation && dealer.IsAtStation;
        bool rouletteRunning = minigameStation != null && minigameStation.IsMinigameRunning;
        bool anotherMinigameRunning = CasinoMinigamePresentation.IsOpen;
        bool tableSafe = dealerActive && rouletteDealerStation.PendingReplacement == null &&
                         minigame != null && !minigame.IsRunning;
        return CanRequestTask(
            IsConfigured,
            dealerActive,
            minigameStation != null && minigameStation.IsTaskPending,
            rouletteRunning,
            anotherMinigameRunning,
            tableSafe);
    }

    private void UpdateNormalDealerActivity()
    {
        AIDealer dealer = rouletteDealerStation != null ? rouletteDealerStation.AssignedDealer : null;
        bool dealerOperating = dealer != null && dealer.isActiveAndEnabled &&
                               dealer.AssignedStation == rouletteDealerStation && dealer.IsAtStation;
        if (!dealerOperating || minigameStation == null ||
            minigameStation.IsTaskPending || minigameStation.IsMinigameRunning ||
            CasinoMinigamePresentation.IsOpen || physicalWheel == null)
        {
            return;
        }

        if (!normalSpinRunning)
        {
            if (Time.time < nextNormalSpinTime)
            {
                return;
            }

            normalSpinRunning = true;
            normalSpinElapsed = 0f;
            normalSpinDuration = Random.Range(1.2f, 2.2f);
            normalSpinStartAngle = physicalWheel.localEulerAngles.y;
            normalSpinEndAngle = normalSpinStartAngle + Random.Range(900f, 1500f);
        }

        normalSpinElapsed = Mathf.Min(normalSpinDuration, normalSpinElapsed + Time.deltaTime);
        float progress = normalSpinElapsed / normalSpinDuration;
        float eased = 1f - Mathf.Pow(1f - progress, 3f);
        Vector3 angles = physicalWheel.localEulerAngles;
        angles.y = Mathf.Lerp(normalSpinStartAngle, normalSpinEndAngle, eased);
        physicalWheel.localEulerAngles = angles;
        if (normalSpinElapsed >= normalSpinDuration)
        {
            normalSpinRunning = false;
            nextNormalSpinTime = Time.time + Random.Range(
                normalSpinMinSeconds, Mathf.Max(normalSpinMinSeconds, normalSpinMaxSeconds));
        }
    }

#if UNITY_EDITOR
    public bool ForceTaskForDevelopment()
    {
        return CanRequestAssistance() &&
               minigameStation.RequestMinigame(
                   CasinoMinigameType.Spin,
                   TableAttentionReason.RouletteSpinRequired);
    }
#endif
}
