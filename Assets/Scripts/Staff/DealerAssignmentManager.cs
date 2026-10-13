using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class DealerAssignmentManager : MonoBehaviour
{
    [SerializeField, Range(0.25f, 10f)] private float simulationTimeScale = 1f;
    [SerializeField, Range(90f, 100f)] private float fullRestThreshold = 97f;
    [SerializeField] private AIDealer[] dealers;
    [SerializeField] private CasinoDealerStation[] stations;
    [SerializeField] private Transform[] breakPositions;
    [SerializeField] private Transform[] pitDeskPositions;
    [SerializeField] private CasinoTableAttention pitDeskAttention;
    private readonly HashSet<Transform> reservedBreakPositions = new HashSet<Transform>();
    private readonly Dictionary<AIDealer, Transform> dealerBreakReservations = new Dictionary<AIDealer, Transform>();
    private float previousTimeScale = 1f;

    public IReadOnlyList<AIDealer> Dealers => dealers;
    public IReadOnlyList<CasinoDealerStation> Stations => stations;
    public IReadOnlyList<Transform> BreakPositions => breakPositions;
    public IReadOnlyList<Transform> PitDeskPositions => pitDeskPositions;
    public float FullRestThreshold => fullRestThreshold;
    public bool IsConfigured => dealers != null && dealers.Length > 0 && stations != null &&
                                stations.Length > 0 && breakPositions != null && breakPositions.Length > 0 &&
                                pitDeskPositions != null && pitDeskPositions.Length > 0 && pitDeskAttention != null;

    public void Configure(
        AIDealer[] casinoDealers,
        CasinoDealerStation[] casinoStations,
        Transform[] restPositions,
        Transform[] waitingPositions,
        CasinoTableAttention deskAttention)
    {
        dealers = casinoDealers;
        stations = casinoStations;
        breakPositions = restPositions;
        pitDeskPositions = waitingPositions;
        pitDeskAttention = deskAttention;
    }

    private void Awake()
    {
        previousTimeScale = Time.timeScale;
        simulationTimeScale = Mathf.Clamp(simulationTimeScale, 0.25f, 10f);
        fullRestThreshold = Mathf.Clamp(fullRestThreshold, 90f, 100f);
    }

    private void Start()
    {
        if (!IsConfigured)
        {
            Debug.LogError($"{nameof(DealerAssignmentManager)} on {name} needs dealers, stations, break positions, pit-desk positions, and desk attention.", this);
            enabled = false;
            return;
        }

        for (int i = 0; i < stations.Length; i++)
        {
            if (stations[i] == null || !stations[i].IsConfigured)
            {
                Debug.LogError($"{nameof(DealerAssignmentManager)} on {name} has an unconfigured dealer station at index {i}.", this);
                enabled = false;
                return;
            }

            if (stations[i].AssignedDealer == null)
            {
                stations[i].Attention.RequestAttentionOnce(TableAttentionReason.TableUnstaffed);
            }
        }

        SyncPitDeskAttention();
    }

    private void Update()
    {
        Time.timeScale = simulationTimeScale;
        foreach (AIDealer dealer in dealers)
        {
            if (dealer != null && dealer.IsResting && dealer.Energy >= fullRestThreshold)
            {
                SendRestedDealerToPitDesk(dealer);
            }
        }
    }

    private void OnDisable()
    {
        Time.timeScale = previousTimeScale;
    }

    public bool CanAssignDealer(AIDealer dealer, CasinoDealerStation station, out string reason)
    {
        if (!Contains(dealer))
        {
            reason = "Dealer is not managed by this casino.";
            return false;
        }

        if (!dealer.IsReadyAtPitDesk)
        {
            reason = $"{dealer.DealerId} must be physically ready at the pit desk.";
            return false;
        }

        if (!Contains(station))
        {
            reason = "Choose a configured casino table.";
            return false;
        }

        if (!station.CanAcceptReplacement)
        {
            reason = station.PendingReplacement != null
                ? "A dealer is already walking to this table."
                : "This table cannot accept a replacement.";
            return false;
        }

        if (!HasCompletePath(dealer.transform.position, station.DealerPosition.position))
        {
            reason = $"No safe NavMesh route to {station.StationName}.";
            return false;
        }

        if (station.AssignedDealer != null &&
            !TryGetOutgoingDestination(dealer, station, out _, out _, out string destinationError))
        {
            reason = destinationError;
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool TryAssignDealer(AIDealer dealer, CasinoDealerStation station, out string reason)
    {
        if (!CanAssignDealer(dealer, station, out reason))
        {
            return false;
        }

        AIDealer outgoingDealer = station.AssignedDealer;
        Transform outgoingDestination = null;
        bool outgoingNeedsBreak = false;
        if (outgoingDealer != null)
        {
            if (!TryGetOutgoingDestination(
                    dealer,
                    station,
                    out outgoingDestination,
                    out outgoingNeedsBreak,
                    out reason))
            {
                return false;
            }

            if (outgoingNeedsBreak)
            {
                reservedBreakPositions.Add(outgoingDestination);
                dealerBreakReservations[outgoingDealer] = outgoingDestination;
            }
            else
            {
                outgoingDealer.ReservePitDeskPosition(outgoingDestination);
            }
        }

        if (!station.BeginReplacement(dealer))
        {
            ReleaseOutgoingReservation(outgoingDealer, outgoingNeedsBreak);
            reason = "The table became unavailable before the assignment started.";
            return false;
        }

        if (!dealer.BeginAssignment(station))
        {
            station.CancelReplacement(dealer);
            ReleaseOutgoingReservation(outgoingDealer, outgoingNeedsBreak);
            reason = "The dealer could not begin the NavMesh route.";
            return false;
        }

        SyncPitDeskAttention();
        reason = string.Empty;
        return true;
    }

    public bool SendRestedDealerToPitDesk(AIDealer dealer)
    {
        if (!Contains(dealer) || !dealer.IsResting || dealer.Energy < fullRestThreshold ||
            !TryFindPitDeskPosition(dealer, dealer.transform.position, out Transform position))
        {
            return false;
        }

        if (!dealer.ReturnToPitDesk(position))
        {
            return false;
        }

        SyncPitDeskAttention();
        return true;
    }

    public void NotifyDealerReady(AIDealer dealer)
    {
        if (Contains(dealer) && dealer.IsReadyAtPitDesk)
        {
            ReleaseBreakReservation(dealer);
            SyncPitDeskAttention();
        }
    }

    public void NotifyDealerArrived(AIDealer dealer, CasinoDealerStation station)
    {
        if (!Contains(dealer) || !Contains(station) || station.PendingReplacement != dealer)
        {
            return;
        }

        AIDealer outgoingDealer = station.AssignedDealer;
        if (outgoingDealer != null)
        {
            bool needsBreak = dealerBreakReservations.TryGetValue(outgoingDealer, out Transform restPosition);
            Transform destination = needsBreak ? restPosition : outgoingDealer.ReservedPitPosition;
            if (destination == null || !HasCompletePath(outgoingDealer.transform.position, destination.position))
            {
                station.CancelReplacement(dealer);
                ReleaseOutgoingReservation(outgoingDealer, needsBreak);
                ReturnFailedAssigneeToPitDesk(dealer);
                SyncPitDeskAttention();
                return;
            }

            outgoingDealer = station.CompleteReplacement(dealer);
            if (needsBreak)
            {
                if (!outgoingDealer.GoToBreakRoom(destination))
                {
                    RouteDealerToBreak(outgoingDealer);
                }
            }
            else
            {
                RouteHealthyDealerToPitDesk(outgoingDealer, destination);
            }
        }
        else
        {
            station.CompleteReplacement(dealer);
        }

        SyncPitDeskAttention();
    }

    public void NotifyDealerPathFailed(AIDealer dealer)
    {
        if (!Contains(dealer))
        {
            return;
        }

        if (dealer.State == DealerDutyState.TravelingToTable)
        {
            CasinoDealerStation station = GetPendingStation(dealer);
            AIDealer outgoingDealer = station != null ? station.AssignedDealer : null;
            station?.CancelReplacement(dealer);
            ReleaseOutgoingReservation(
                outgoingDealer,
                outgoingDealer != null && dealerBreakReservations.ContainsKey(outgoingDealer));
            ReturnFailedAssigneeToPitDesk(dealer);
        }
        else if (dealer.State == DealerDutyState.TravelingToPitDesk)
        {
            Debug.LogError($"{dealer.name} could not reach the pit desk and will retry after its path is restored.", dealer);
            dealer.MarkResting();
        }
        else if (dealer.State == DealerDutyState.TravelingToBreak)
        {
            ReleaseBreakReservation(dealer);
            RouteDealerToBreak(dealer);
        }
        else
        {
            Debug.LogError($"{dealer.name} could not reach its reserved break-room position.", dealer);
        }

        SyncPitDeskAttention();
    }

    private void RouteDealerToBreak(AIDealer dealer)
    {
        ReleaseBreakReservation(dealer);
        if (TryFindReachableBreakPosition(dealer.transform.position, out Transform alternateBreakPosition) &&
            TryStartBreakRoute(dealer, alternateBreakPosition))
        {
            Debug.LogWarning($"{dealer.name} is taking an alternate route to the break room.", dealer);
            return;
        }

        if (TryFindPitDeskPosition(dealer, dealer.transform.position, out Transform deskPosition) &&
            dealer.ReturnToPitDesk(deskPosition, true))
        {
            Debug.LogWarning($"{dealer.name} could not reach a break position and will recover at the pit desk.", dealer);
            return;
        }

        Debug.LogError($"{dealer.name} has no safe route to an alternate break position or the pit desk.", dealer);
        dealer.MarkResting();
    }

    private void RouteHealthyDealerToPitDesk(AIDealer dealer, Transform preferredPosition)
    {
        if (dealer.ReturnToPitDesk(preferredPosition))
        {
            return;
        }

        dealer.ReservePitDeskPosition(null);
        if (TryFindPitDeskPosition(dealer, dealer.transform.position, out Transform alternatePosition) &&
            dealer.ReturnToPitDesk(alternatePosition))
        {
            Debug.LogWarning($"{dealer.name} is returning to an alternate pit-desk position.", dealer);
            return;
        }

        RouteDealerToBreak(dealer);
    }

    private bool TryStartBreakRoute(AIDealer dealer, Transform position)
    {
        if (position == null || !HasCompletePath(dealer.transform.position, position.position))
        {
            return false;
        }

        reservedBreakPositions.Add(position);
        dealerBreakReservations[dealer] = position;
        if (dealer.GoToBreakRoom(position))
        {
            return true;
        }

        ReleaseBreakReservation(dealer);
        return false;
    }

    private void ReturnFailedAssigneeToPitDesk(AIDealer dealer)
    {
        if (TryFindPitDeskPosition(dealer, dealer.transform.position, out Transform position) &&
            dealer.ReturnToPitDesk(position))
        {
            return;
        }

        Debug.LogError($"{dealer.name} has no safe route back to a pit-desk position.", dealer);
    }

    private CasinoDealerStation GetPendingStation(AIDealer dealer)
    {
        foreach (CasinoDealerStation station in stations)
        {
            if (station != null && station.PendingReplacement == dealer)
            {
                return station;
            }
        }

        return null;
    }

    private bool TryFindPitDeskPosition(
        AIDealer dealer,
        Vector3 start,
        out Transform availablePosition,
        AIDealer reservationReleasedAfterDispatch = null)
    {
        foreach (Transform position in pitDeskPositions)
        {
            if (position == null)
            {
                continue;
            }

            bool occupied = false;
            foreach (AIDealer other in dealers)
            {
                if (other != null && other != dealer && other != reservationReleasedAfterDispatch &&
                    other.ReservedPitPosition == position)
                {
                    occupied = true;
                    break;
                }
            }

            if (!occupied && HasCompletePath(start, position.position))
            {
                availablePosition = position;
                return true;
            }
        }

        availablePosition = null;
        return false;
    }

    private bool TryGetOutgoingDestination(
        AIDealer incomingDealer,
        CasinoDealerStation station,
        out Transform destination,
        out bool needsBreak,
        out string reason)
    {
        AIDealer outgoingDealer = station.AssignedDealer;
        needsBreak = outgoingDealer != null &&
                     AIDealer.ShouldRestAfterReplacement(outgoingDealer.Energy, outgoingDealer.TiredThreshold);
        Vector3 origin = station.DealerPosition.position;
        if (needsBreak)
        {
            if (TryFindReachableBreakPosition(origin, out destination))
            {
                reason = string.Empty;
                return true;
            }

            reason = "All dealer break-room positions are occupied or unreachable.";
            destination = null;
            return false;
        }

        if (TryFindPitDeskPosition(incomingDealer, origin, out destination, incomingDealer))
        {
            reason = string.Empty;
            return true;
        }

        reason = "No unreserved pit-desk position has a safe route from this table.";
        destination = null;
        return false;
    }

    private void ReleaseOutgoingReservation(AIDealer dealer, bool needsBreak)
    {
        if (needsBreak)
        {
            ReleaseBreakReservation(dealer);
        }
        else if (dealer != null)
        {
            dealer.ReservePitDeskPosition(null);
        }
    }

    private bool TryFindReachableBreakPosition(Vector3 start, out Transform availablePosition)
    {
        foreach (Transform position in breakPositions)
        {
            if (position != null && !reservedBreakPositions.Contains(position) &&
                HasCompletePath(start, position.position))
            {
                availablePosition = position;
                return true;
            }
        }

        availablePosition = null;
        return false;
    }

    private bool TryFindBreakPosition(out Transform availablePosition)
    {
        foreach (Transform position in breakPositions)
        {
            if (position != null && !reservedBreakPositions.Contains(position))
            {
                availablePosition = position;
                return true;
            }
        }

        availablePosition = null;
        return false;
    }

    private void ReleaseBreakReservation(AIDealer dealer)
    {
        if (dealer != null && dealerBreakReservations.TryGetValue(dealer, out Transform position))
        {
            dealerBreakReservations.Remove(dealer);
            reservedBreakPositions.Remove(position);
        }
    }

    private static bool HasCompletePath(Vector3 start, Vector3 destination)
    {
        if (!NavMesh.SamplePosition(start, out NavMeshHit startHit, 0.75f, NavMesh.AllAreas) ||
            Vector3.Distance(startHit.position, start) > 0.5f ||
            !NavMesh.SamplePosition(destination, out NavMeshHit destinationHit, 0.75f, NavMesh.AllAreas) ||
            Vector3.Distance(destinationHit.position, destination) > 0.5f)
        {
            return false;
        }

        NavMeshPath path = new NavMeshPath();
        return NavMesh.CalculatePath(startHit.position, destinationHit.position, NavMesh.AllAreas, path) &&
               path.status == NavMeshPathStatus.PathComplete;
    }

    private void SyncPitDeskAttention()
    {
        if (pitDeskAttention == null || dealers == null)
        {
            return;
        }

        int readyCount = 0;
        foreach (AIDealer dealer in dealers)
        {
            if (dealer != null && dealer.IsReadyAtPitDesk)
            {
                readyCount++;
            }
        }

        int pendingCount = pitDeskAttention.GetPendingCount(TableAttentionReason.DealerReadyForAssignment);
        while (pendingCount < readyCount)
        {
            pitDeskAttention.RequestAttention(TableAttentionReason.DealerReadyForAssignment);
            pendingCount++;
        }

        while (pendingCount > readyCount)
        {
            pitDeskAttention.ResolveAttention(TableAttentionReason.DealerReadyForAssignment);
            pendingCount--;
        }
    }

    private bool Contains(AIDealer dealer)
    {
        if (dealer == null || dealers == null)
        {
            return false;
        }

        foreach (AIDealer item in dealers)
        {
            if (item == dealer) return true;
        }

        return false;
    }

    private bool Contains(CasinoDealerStation station)
    {
        if (station == null || stations == null)
        {
            return false;
        }

        foreach (CasinoDealerStation item in stations)
        {
            if (item == station) return true;
        }

        return false;
    }
}
