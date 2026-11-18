using UnityEngine;

[DisallowMultipleComponent]
public sealed class CasinoDealerStation : MonoBehaviour
{
    [SerializeField] private string stationName;
    [SerializeField] private Transform dealerPosition;
    [SerializeField] private BlackjackTableInteraction blackjackTable;
    [SerializeField] private CasinoTableAttention attention;
    [SerializeField] private AIDealer initialDealer;
    private SpriteRenderer tableSurface;

    public string StationName => string.IsNullOrWhiteSpace(stationName) ? name : stationName;
    public Transform DealerPosition => dealerPosition;
    public BlackjackTableInteraction BlackjackTable => blackjackTable;
    public CasinoTableAttention Attention => attention;
    public AIDealer AssignedDealer { get; private set; }
    public AIDealer PendingReplacement { get; private set; }
    public bool IsAvailable => AssignedDealer == null && PendingReplacement == null;
    public bool IsConfigured => dealerPosition != null && attention != null;
    public AIDealer InitialDealer => initialDealer;
    public bool CanAcceptReplacement => CanReplaceDealer(PendingReplacement != null);

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
            attention.RequestAttentionOnce(TableAttentionReason.StaffRotationRequired);
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
        string surfaceName = blackjackTable != null ? "Pixel BlackjackV2" : "Pixel Roulette";
        Transform surface = transform.Find("VisualRoot/" + surfaceName);
        if (surface != null) tableSurface = surface.GetComponent<SpriteRenderer>();

        if (initialDealer != null)
        {
            AssignedDealer = initialDealer;
            if (blackjackTable != null)
            {
                blackjackTable.SetAssignedDealer(initialDealer);
            }

        }
    }

    public int DealerBackdropOrder(int defaultOrder) =>
        tableSurface != null && tableSurface.enabled
            ? Mathf.Min(defaultOrder, tableSurface.sortingOrder - 1)
            : defaultOrder;

    public int PlayerBackdropOrder(Vector3 feet, Bounds playerBounds, Camera camera, int defaultOrder)
    {
        if (tableSurface == null || !tableSurface.enabled || !tableSurface.gameObject.activeInHierarchy)
            return defaultOrder;
        Vector3 groundForward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
        if (Vector3.Dot(feet - transform.position, groundForward) <= 0f) return defaultOrder;
        Bounds tableBounds = tableSurface.bounds;
        return ProjectedOverlap(playerBounds, tableBounds, camera.transform.right) &&
            ProjectedOverlap(playerBounds, tableBounds, camera.transform.up)
            ? DealerBackdropOrder(defaultOrder) : defaultOrder;
    }

    private static bool ProjectedOverlap(Bounds a, Bounds b, Vector3 axis)
    {
        Vector3 absoluteAxis = new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z));
        return Mathf.Abs(Vector3.Dot(a.center - b.center, axis)) <
            Vector3.Dot(a.extents + b.extents, absoluteAxis);
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
        // Hand-over courtesy: the fresh dealer greets the one heading off for a break.
        if (outgoingDealer != null)
            replacement.GetComponentInChildren<PixelCharacterVisual>()?.PlayAction(
                CasinoAnimationPose.Present, outgoingDealer.transform.position, .8f);

        ResolveAttention(TableAttentionReason.TableUnstaffed);
        ResolveAttention(TableAttentionReason.DealerBreakRequired);
        ResolveAttention(TableAttentionReason.DealerExhausted);
        ResolveAttention(TableAttentionReason.StaffRotationRequired);
        return outgoingDealer;
    }

    public bool CancelReplacement(AIDealer replacement)
    {
        if (PendingReplacement != replacement)
        {
            return false;
        }

        if (AssignedDealer != null && !AssignedDealer.IsAtStation)
        {
            AssignedDealer = null;
            if (blackjackTable != null) blackjackTable.SetAssignedDealer(null);
            attention.RequestAttentionOnce(TableAttentionReason.TableUnstaffed);
        }
        else
        {
            AssignedDealer?.CancelReplacementRequest();
        }
        PendingReplacement = null;
        ResolveAttention(TableAttentionReason.StaffRotationRequired);
        return true;
    }

    public void RequestDealerBreakAttention()
    {
        attention.RequestAttentionOnce(TableAttentionReason.DealerBreakRequired);
    }

    private void ResolveAttention(TableAttentionReason reason)
    {
        while (attention.ResolveAttention(reason))
        {
        }
    }

    public static bool CanReplaceDealer(bool hasPendingReplacement)
    {
        return !hasPendingReplacement;
    }
}
