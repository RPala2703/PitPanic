using UnityEngine;
using System;

[DisallowMultipleComponent]
public sealed class CasinoMinigameStation : MonoBehaviour
{
    [SerializeField] private CasinoPlayerController player;
    [SerializeField] private TableInteractionZone interactionZone;
    [SerializeField] private CasinoTableAttention attention;
    [SerializeField] private CasinoMinigame minigame;
    [SerializeField] private CasinoMinigameType minigameType;
    [SerializeField] private TableAttentionReason attentionReason;
    private CasinoMinigame subscribedMinigame;

    public bool IsTaskPending { get; private set; }
    public bool HasRecordedMiss { get; private set; }
    public bool IsMinigameRunning => minigame != null && minigame.IsRunning;
    public bool IsPlayerInRange(Transform candidate) =>
        IsConfigured && candidate != null && interactionZone.IsPlayerInRange(candidate);
    public CasinoMinigameType MinigameType => minigameType;
    public event Action<CasinoMinigameResult> TaskSucceeded;
    public event Action TaskAbandoned;
    public event Action TaskMissed;
    public static bool ShouldResolveTaskOnAbandon(CasinoMinigameType type) =>
        type != CasinoMinigameType.Shuffle && type != CasinoMinigameType.Spin && type != CasinoMinigameType.Cover;

    public bool IsConfigured => player != null && interactionZone != null &&
                                interactionZone.IsConfigured && attention != null &&
                                minigame != null;

    public void Configure(
        CasinoPlayerController casinoPlayer,
        TableInteractionZone zone,
        CasinoTableAttention tableAttention,
        CasinoMinigame game,
        CasinoMinigameType type,
        TableAttentionReason reason)
    {
        player = casinoPlayer;
        interactionZone = zone;
        attention = tableAttention;
        minigame = game;
        minigameType = type;
        attentionReason = reason;
        BindMinigame();
    }

    public bool RequestMinigame(CasinoMinigameType type, TableAttentionReason reason)
    {
        if (CasinoShiftController.Active != null &&
            !CasinoShiftController.Active.AllowsTrainingTask(attention, type))
            return false;
        if (IsTaskPending)
        {
            return false;
        }

        if (!IsConfigured)
        {
            Debug.LogError($"{nameof(CasinoMinigameStation)} on {name} cannot request a task before it is configured.", this);
            return false;
        }

        minigameType = type;
        attentionReason = reason;
        IsTaskPending = true;
        HasRecordedMiss = false;
        attention.RequestAttentionOnce(reason);
        BindMinigame();
        return true;
    }

    public bool TryBegin()
    {
        if (CasinoShiftController.GameplayFrozen) return false;
        if (!IsTaskPending || IsMinigameRunning || !IsConfigured ||
            CasinoMinigamePresentation.IsOpen ||
            CasinoPlayerInteractionRouter.IsAnyInspectionOpen() ||
            player.GetComponent<CasinoPlayerFloatCarrier>()?.IsCarrying == true ||
            !interactionZone.IsPlayerInRange(player.transform) || !minigame.CanBegin)
        {
            return false;
        }

        minigame.Begin();
        return true;
    }

    private void OnEnable()
    {
        BindMinigame();
    }

    private void Start()
    {
        if (!IsConfigured)
        {
            Debug.LogError($"{nameof(CasinoMinigameStation)} on {name} is missing its player, interaction zone, attention system, or minigame.", this);
            enabled = false;
        }
    }

    private void OnDisable()
    {
        if (subscribedMinigame != null)
        {
            subscribedMinigame.Completed -= OnMinigameCompleted;
            subscribedMinigame.Abandoned -= OnMinigameAbandoned;
            subscribedMinigame = null;
        }
    }

    private void OnMinigameCompleted(CasinoMinigameResult result)
    {
        CasinoSoundscape.Play(result.Success ? CasinoSoundCue.Success : CasinoSoundCue.TaskFailed, transform.position);
        if (result.Success)
        {
            ResolveTask();
            TaskSucceeded?.Invoke(result);
        }

        // A retryable attempt keeps the same task and deadline, not another miss.
        else if (!minigame.HasCarriedDeadline)
        {
            RecordMiss();
        }
    }

    private void OnMinigameAbandoned()
    {
        if (ShouldResolveTaskOnAbandon(minigameType))
        {
            RecordMiss();
            ResolveTask();
        }

        TaskAbandoned?.Invoke();
    }

    private void RecordMiss()
    {
        if (!IsTaskPending || HasRecordedMiss) return;
        HasRecordedMiss = true;
        TaskMissed?.Invoke();
    }

    private void BindMinigame()
    {
        if (subscribedMinigame == minigame)
        {
            return;
        }

        if (subscribedMinigame != null)
        {
            subscribedMinigame.Completed -= OnMinigameCompleted;
            subscribedMinigame.Abandoned -= OnMinigameAbandoned;
        }

        subscribedMinigame = minigame;
        if (subscribedMinigame != null && isActiveAndEnabled)
        {
            subscribedMinigame.Completed += OnMinigameCompleted;
            subscribedMinigame.Abandoned += OnMinigameAbandoned;
        }
    }

    private void ResolveTask()
    {
        minigame?.ClearCarriedDeadline();
        if (attention != null)
        {
            attention.ResolveAttention(attentionReason);
        }

        IsTaskPending = false;
    }

    public void ResolveTaskAfterFatalFailure()
    {
        if (IsTaskPending)
        {
            ResolveTask();
        }
    }
}
