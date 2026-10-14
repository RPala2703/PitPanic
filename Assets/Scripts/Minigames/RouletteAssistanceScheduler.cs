using UnityEngine;

[DisallowMultipleComponent]
public sealed class RouletteAssistanceScheduler : MonoBehaviour
{
    [SerializeField] private CasinoDealerStation rouletteDealerStation;
    [SerializeField] private CasinoMinigameStation minigameStation;
    [SerializeField] private RouletteTableMinigame minigame;
    [SerializeField] private RouletteRoundController roundController;
    [SerializeField, Min(0f)] private float firstTaskMinActiveSeconds = 20f;
    [SerializeField, Min(0f)] private float firstTaskMaxActiveSeconds = 40f;
    [SerializeField, Range(0f, 1f)] private float taskChancePerEligibleRound = 0.35f;
    [SerializeField, Min(1)] private int guaranteedWithinEligibleRounds = 5;
    [SerializeField, Min(0f)] private float completionCooldownSeconds = 45f;

    private float activePlaySeconds;
    private float firstTaskDelaySeconds;
    private float nextAllowedTaskTime;
    private int eligibleRoundsSinceTask;
    private bool subscribed;

    public bool IsConfigured => rouletteDealerStation != null && rouletteDealerStation.IsConfigured &&
                                minigameStation != null && minigameStation.IsConfigured &&
                                minigame != null && minigame.IsConfigured &&
                                roundController != null && roundController.IsConfigured;

    public void Configure(
        CasinoDealerStation dealerStation,
        CasinoMinigameStation station,
        RouletteTableMinigame rouletteMinigame,
        RouletteRoundController rounds)
    {
        rouletteDealerStation = dealerStation;
        minigameStation = station;
        minigame = rouletteMinigame;
        roundController = rounds;
        BindEvents();
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

    public static bool ShouldRequestTaskAtEligibleRound(
        float activePlaySeconds,
        float firstTaskDelaySeconds,
        bool cooldownElapsed,
        int eligibleRoundCount,
        int guaranteedWithinEligibleRounds,
        float randomRoll,
        float chance)
    {
        if (activePlaySeconds < firstTaskDelaySeconds || !cooldownElapsed)
        {
            return false;
        }

        return eligibleRoundCount >= Mathf.Max(1, guaranteedWithinEligibleRounds) ||
               randomRoll < Mathf.Clamp01(chance);
    }

    private void Start()
    {
        if (!IsConfigured)
        {
            Debug.LogError($"{nameof(RouletteAssistanceScheduler)} on {name} needs its dealer station, task station, minigame, and round controller.", this);
            enabled = false;
            return;
        }

        firstTaskDelaySeconds = Random.Range(
            firstTaskMinActiveSeconds, Mathf.Max(firstTaskMinActiveSeconds, firstTaskMaxActiveSeconds));
        BindEvents();
    }

    private void Update()
    {
        if (roundController != null && roundController.HasActiveCustomersAndDealer)
        {
            activePlaySeconds += Time.deltaTime;
        }
    }

    private void OnDisable()
    {
        UnbindEvents();
    }

    private void OnEnable()
    {
        BindEvents();
    }

    private void BindEvents()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || subscribed)
        {
            return;
        }

        if (minigameStation != null)
        {
            minigameStation.TaskSucceeded += OnTaskSucceeded;
        }

        if (roundController != null)
        {
            roundController.AssistanceOpportunity += OnAssistanceOpportunity;
        }

        subscribed = minigameStation != null || roundController != null;
    }

    private void UnbindEvents()
    {
        if (!subscribed)
        {
            return;
        }

        if (minigameStation != null)
        {
            minigameStation.TaskSucceeded -= OnTaskSucceeded;
        }

        if (roundController != null)
        {
            roundController.AssistanceOpportunity -= OnAssistanceOpportunity;
        }

        subscribed = false;
    }

    private void OnTaskSucceeded(CasinoMinigameResult result)
    {
        if (!result.Success)
        {
            return;
        }

        activePlaySeconds = 0f;
        eligibleRoundsSinceTask = 0;
        nextAllowedTaskTime = Time.time + completionCooldownSeconds;
        firstTaskDelaySeconds = Random.Range(
            firstTaskMinActiveSeconds, Mathf.Max(firstTaskMinActiveSeconds, firstTaskMaxActiveSeconds));
    }

    private void OnAssistanceOpportunity(RouletteRoundController rounds)
    {
        if (rounds != roundController || !CanRequestAssistance() ||
            activePlaySeconds < firstTaskDelaySeconds || Time.time < nextAllowedTaskTime)
        {
            return;
        }

        eligibleRoundsSinceTask++;
        if (!ShouldRequestTaskAtEligibleRound(
                activePlaySeconds,
                firstTaskDelaySeconds,
                Time.time >= nextAllowedTaskTime,
                eligibleRoundsSinceTask,
                guaranteedWithinEligibleRounds,
                Random.value,
                taskChancePerEligibleRound))
        {
            return;
        }

        if (minigameStation.RequestMinigame(
                CasinoMinigameType.Spin,
                TableAttentionReason.RouletteSpinRequired))
        {
            if (!roundController.TryPauseForAssistance())
            {
                minigameStation.ResolveTaskAfterFatalFailure();
            }
        }
    }

    private bool CanRequestAssistance()
    {
        AIDealer dealer = rouletteDealerStation != null ? rouletteDealerStation.AssignedDealer : null;
        bool dealerActive = dealer != null && dealer.isActiveAndEnabled &&
                            dealer.AssignedStation == rouletteDealerStation && dealer.IsAtStation;
        return CanRequestTask(
            IsConfigured,
            dealerActive,
            minigameStation != null && minigameStation.IsTaskPending,
            minigameStation != null && minigameStation.IsMinigameRunning,
            CasinoMinigamePresentation.IsOpen,
            roundController != null && roundController.Phase == RouletteRoundPhase.AcceptingBets &&
            roundController.ParticipantCount > 0 &&
            rouletteDealerStation.PendingReplacement == null);
    }

#if UNITY_EDITOR
    public bool ForceTaskForDevelopment()
    {
        return CanRequestAssistance() &&
               minigameStation.RequestMinigame(CasinoMinigameType.Spin, TableAttentionReason.RouletteSpinRequired) &&
               roundController.TryPauseForAssistance();
    }
#endif
}
