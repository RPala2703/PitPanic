using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class CasinoMinigameStation : MonoBehaviour
{
    [SerializeField] private CasinoPlayerController player;
    [SerializeField] private TableInteractionZone interactionZone;
    [SerializeField] private CasinoTableAttention attention;
    [SerializeField] private CasinoMinigame minigame;
    [SerializeField] private CasinoMinigameType minigameType;
    [SerializeField] private TableAttentionReason attentionReason;

    public bool IsTaskPending { get; private set; }
    public bool IsMinigameRunning => minigame != null && minigame.IsRunning;

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
    }

    public bool RequestMinigame(CasinoMinigameType type, TableAttentionReason reason)
    {
        if (IsTaskPending || attention == null)
        {
            return false;
        }

        minigameType = type;
        attentionReason = reason;
        IsTaskPending = true;
        attention.RequestAttention(reason);
        return true;
    }

    private void OnEnable()
    {
        if (minigame != null)
        {
            minigame.Completed += OnMinigameCompleted;
        }
    }

    private void OnDisable()
    {
        if (minigame != null)
        {
            minigame.Completed -= OnMinigameCompleted;
        }
    }

    private void Update()
    {
        if (!IsTaskPending || IsMinigameRunning || player == null || interactionZone == null ||
            !interactionZone.IsPlayerInRange(player.transform) || Keyboard.current == null ||
            !Keyboard.current.eKey.wasPressedThisFrame)
        {
            return;
        }

        if (minigame == null)
        {
            Debug.LogError($"{name} has a pending {minigameType} task but no minigame implementation is attached.", this);
            return;
        }

        minigame.Begin();
    }

    private void OnMinigameCompleted(CasinoMinigameResult result)
    {
        if (!result.Success)
        {
            return;
        }

        if (attention != null)
        {
            attention.ResolveAttention(attentionReason);
        }

        IsTaskPending = false;
    }
}
