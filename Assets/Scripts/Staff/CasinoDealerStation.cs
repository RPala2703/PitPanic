using UnityEngine;

[DisallowMultipleComponent]
public sealed class CasinoDealerStation : MonoBehaviour
{
    [SerializeField] private string stationName;
    [SerializeField] private Transform dealerPosition;
    [SerializeField] private BlackjackTableInteraction blackjackTable;
    [SerializeField] private CasinoTableAttention attention;
    [SerializeField] private AIDealer initialDealer;

    public string StationName => string.IsNullOrWhiteSpace(stationName) ? name : stationName;
    public Transform DealerPosition => dealerPosition;
    public BlackjackTableInteraction BlackjackTable => blackjackTable;
    public CasinoTableAttention Attention => attention;
    public AIDealer AssignedDealer { get; private set; }
    public AIDealer PendingReplacement { get; private set; }
    public bool IsAvailable => AssignedDealer == null && PendingReplacement == null;
    public bool IsConfigured => dealerPosition != null && attention != null;
    public AIDealer InitialDealer => initialDealer;
    public bool CanAcceptReplacement => CanReplaceDealer(
        PendingReplacement != null,
        AssignedDealer != null,
        AssignedDealer != null && AssignedDealer.NeedsBreak);

    public void Configure(string displayName, Transform position, CasinoTableAttention tableAttention, BlackjackTableInteraction table)
    {
        stationName = displayName;
        dealerPosition = position;
        attention = tableAttention;
        blackjackTable = table;
    }

    public bool BeginReplacement(AIDealer replacement)
    {
        if (replacement == null || !CanAcceptReplacement || AssignedDealer == replacement)
        {
            return false;
        }

        PendingReplacement = replacement;
        if (AssignedDealer != null)
        {
            AssignedDealer.MarkReplacementRequested();
            attention.RequestAttention(TableAttentionReason.StaffRotationRequired);
        }

        return true;
    }

    public void SetInitialDealer(AIDealer dealer)
    {
        if (dealer == null)
        {
            throw new System.ArgumentNullException(nameof(dealer));
        }

        initialDealer = dealer;
        AssignedDealer = dealer;
        dealer.SetStation(this);
        if (blackjackTable != null)
        {
            blackjackTable.SetAssignedDealer(dealer);
        }

        ResolveAttention(TableAttentionReason.TableUnstaffed);
    }

    private void Awake()
    {
        if (initialDealer != null)
        {
            AssignedDealer = initialDealer;
            if (blackjackTable != null)
            {
                blackjackTable.SetAssignedDealer(initialDealer);
            }
        }
    }

    public AIDealer CompleteReplacement(AIDealer replacement)
    {
        if (replacement == null || PendingReplacement != replacement)
        {
            return null;
        }

        AIDealer outgoingDealer = AssignedDealer;
        AssignedDealer = replacement;
        PendingReplacement = null;
        replacement.SetStation(this);
        if (blackjackTable != null)
        {
            blackjackTable.SetAssignedDealer(replacement);
        }

        ResolveAttention(TableAttentionReason.TableUnstaffed);
        ResolveAttention(TableAttentionReason.DealerBreakRequired);
        ResolveAttention(TableAttentionReason.StaffRotationRequired);
        return outgoingDealer;
    }

    public bool CancelReplacement(AIDealer replacement)
    {
        if (PendingReplacement != replacement)
        {
            return false;
        }

        AssignedDealer?.CancelReplacementRequest();
        PendingReplacement = null;
        ResolveAttention(TableAttentionReason.StaffRotationRequired);
        return true;
    }

    public void RequestDealerBreakAttention()
    {
        attention.RequestAttention(TableAttentionReason.DealerBreakRequired);
    }

    private void ResolveAttention(TableAttentionReason reason)
    {
        while (attention.ResolveAttention(reason))
        {
        }
    }

    public static bool CanReplaceDealer(bool hasPendingReplacement, bool hasAssignedDealer, bool assignedDealerNeedsBreak)
    {
        return !hasPendingReplacement && (!hasAssignedDealer || assignedDealerNeedsBreak);
    }
}
