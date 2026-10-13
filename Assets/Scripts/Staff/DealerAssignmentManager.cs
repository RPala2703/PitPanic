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
    private readonly HashSet<AIDealer> pendingDeskDecisions = new HashSet<AIDealer>();
    private readonly Dictionary<AIDealer, float> nextRetryTime = new Dictionary<AIDealer, float>();
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

        foreach (AIDealer dealer in dealers)
        {
            if (dealer != null && dealer.IsReadyAtPitDesk)
            {
                pendingDeskDecisions.Add(dealer);
            }
        }

        SyncPitDeskAttention();
    }

    private void Update()
    {
        Time.timeScale = simulationTimeScale;
        foreach (AIDealer dealer in dealers)
        {
            if (dealer == null || !IsRetryReady(dealer))
            {
                continue;
            }

            if (dealer.IsWaitingForPitDeskPosition)
            {
                TryRouteWaitingDealerToPitDesk(dealer);
            }
            else if (dealer.HasReportedPathFailure)
            {
                dealer.RetryCurrentRoute();
            }
            else if (dealer.IsResting && dealer.Energy >= fullRestThreshold)
            {
                SendRestedDealerToPitDesk(dealer);
            }

            nextRetryTime[dealer] = Time.time + 1f;
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
            !TryGetOutgoingDestination(dealer, station, out _, out string destinationError))
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
        if (outgoingDealer != null)
        {
            if (!TryGetOutgoingDestination(
                    dealer,
                    station,
                    out outgoingDestination,
                    out reason))
            {
                return false;
            }

            outgoingDealer.ReservePitDeskPosition(outgoingDestination);
        }

        if (!station.BeginReplacement(dealer))
        {
            ReleaseOutgoingReservation(outgoingDealer);
            reason = "The table became unavailable before the assignment started.";
            return false;
        }

        if (!dealer.BeginAssignment(station))
        {
            station.CancelReplacement(dealer);
            ReleaseOutgoingReservation(outgoingDealer);
            reason = "The dealer could not begin the NavMesh route.";
            return false;
        }

        AcknowledgeDealerAtPitDesk(dealer);
        SyncPitDeskAttention();
        reason = string.Empty;
        return true;
    }

    public bool TrySendDealerOnBreak(AIDealer dealer, out string reason)
    {
        if (!Contains(dealer) || !dealer.IsReadyAtPitDesk)
        {
            reason = "Only a dealer physically waiting at the pit desk can be sent on break.";
            return false;
        }

        if (!TryFindReachableBreakPosition(dealer.transform.position, out Transform position))
        {
            reason = "No unoccupied break-room position has a safe NavMesh route.";
            return false;
        }

        if (!TryStartBreakRoute(dealer, position))
        {
            reason = "The dealer could not start the NavMesh route to the break room.";
            return false;
        }

        AcknowledgeDealerAtPitDesk(dealer);
        SyncPitDeskAttention();
        reason = string.Empty;
        return true;
    }

    public void AcknowledgeDealerAtPitDesk(AIDealer dealer)
    {
        if (Contains(dealer) && pendingDeskDecisions.Remove(dealer))
        {
            SyncPitDeskAttention();
        }
    }

    public bool SendRestedDealerToPitDesk(AIDealer dealer)
    {
        if (!Contains(dealer) || !dealer.IsResting || dealer.Energy < fullRestThreshold ||
            !TryFindPitDeskPosition(dealer, dealer.transform.position, out Transform position))
        {
            return false;
        }

        if (!dealer.ReturnFromBreakToPitDesk(position))
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
            pendingDeskDecisions.Add(dealer);
            nextRetryTime.Remove(dealer);
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
            Transform destination = outgoingDealer.ReservedPitPosition;
            if (destination == null || !HasCompletePath(outgoingDealer.transform.position, destination.position))
            {
                station.CancelReplacement(dealer);
                ReleaseOutgoingReservation(outgoingDealer);
                ReturnFailedAssigneeToPitDesk(dealer);
                SyncPitDeskAttention();
                return;
            }

            if (!outgoingDealer.ReturnToPitDesk(destination))
            {
                station.CancelReplacement(dealer);
                ReleaseOutgoingReservation(outgoingDealer);
                ReturnFailedAssigneeToPitDesk(dealer);
                SyncPitDeskAttention();
                return;
            }

            station.CompleteReplacement(dealer);
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
            ReleaseOutgoingReservation(outgoingDealer);
            ReturnFailedAssigneeToPitDesk(dealer);
        }
        else if (dealer.State == DealerDutyState.TravelingToPitDesk ||
                 dealer.State == DealerDutyState.ReturningFromBreak)
        {
            if (TryFindPitDeskPosition(dealer, dealer.transform.position, out Transform alternatePosition) &&
                alternatePosition != dealer.ReservedPitPosition &&
                (dealer.State == DealerDutyState.ReturningFromBreak
                    ? dealer.ReturnFromBreakToPitDesk(alternatePosition)
                    : dealer.ReturnToPitDesk(alternatePosition)))
            {
                Debug.LogWarning($"{dealer.name} is returning to an alternate pit-desk position.", dealer);
            }
            else
            {
                QueueRouteRetry(dealer);
            }
        }
        else if (dealer.State == DealerDutyState.TravelingToBreak)
        {
            if (TryFindReachableBreakPosition(dealer.transform.position, out Transform alternateBreakPosition) &&
                alternateBreakPosition != dealer.Destination &&
                TryStartBreakRoute(dealer, alternateBreakPosition))
            {
                Debug.LogWarning($"{dealer.name} is taking an alternate route to the break room.", dealer);
            }
            else if (TryFindPitDeskPosition(dealer, dealer.transform.position, out Transform deskPosition) &&
                     dealer.ReturnToPitDesk(deskPosition))
            {
                ReleaseBreakReservation(dealer);
                Debug.LogWarning($"{dealer.name} could not reach the break room and is returning to the pit desk.", dealer);
            }
            else
            {
                QueueRouteRetry(dealer);
            }
        }
        else if (dealer.IsWaitingForPitDeskPosition)
        {
            QueueRouteRetry(dealer);
        }
        else
        {
            Debug.LogError($"{dealer.name} could not reach its reserved break-room position.", dealer);
        }

        SyncPitDeskAttention();
    }

    private bool TryStartBreakRoute(AIDealer dealer, Transform position)
    {
        if (position == null || !HasCompletePath(dealer.transform.position, position.position))
        {
            return false;
        }

        bool hadReservation = dealerBreakReservations.TryGetValue(dealer, out Transform previousPosition);
        reservedBreakPositions.Add(position);
        dealerBreakReservations[dealer] = position;
        if (dealer.GoToBreakRoom(position))
        {
            if (hadReservation && previousPosition != position)
            {
                reservedBreakPositions.Remove(previousPosition);
            }

            return true;
        }

        if (hadReservation)
        {
            dealerBreakReservations[dealer] = previousPosition;
        }
        else
        {
            dealerBreakReservations.Remove(dealer);
        }

        if (previousPosition != position)
        {
            reservedBreakPositions.Remove(position);
        }

        return false;
    }

    private void ReturnFailedAssigneeToPitDesk(AIDealer dealer)
    {
        if (TryFindPitDeskPosition(dealer, dealer.transform.position, out Transform position) &&
            dealer.ReturnToPitDesk(position))
        {
            return;
        }

        dealer.WaitForPitDeskPosition();
        QueueRouteRetry(dealer);
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
        out string reason)
    {
        Vector3 origin = station.DealerPosition.position;
        if (TryFindPitDeskPosition(incomingDealer, origin, out destination, incomingDealer))
        {
            reason = string.Empty;
            return true;
        }

        reason = "No unreserved pit-desk position has a safe route from this table.";
        destination = null;
        return false;
    }

    private void ReleaseOutgoingReservation(AIDealer dealer)
    {
        if (dealer != null)
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

        int pendingCount = pitDeskAttention.GetPendingCount(TableAttentionReason.DealerReadyForAssignment);
        while (pendingCount < pendingDeskDecisions.Count)
        {
            pitDeskAttention.RequestAttention(TableAttentionReason.DealerReadyForAssignment);
            pendingCount++;
        }

        while (pendingCount > pendingDeskDecisions.Count)
        {
            pitDeskAttention.ResolveAttention(TableAttentionReason.DealerReadyForAssignment);
            pendingCount--;
        }
    }

    private bool TryRouteWaitingDealerToPitDesk(AIDealer dealer)
    {
        if (!TryFindPitDeskPosition(dealer, dealer.transform.position, out Transform position) ||
            !dealer.ReturnToPitDesk(position))
        {
            QueueRouteRetry(dealer);
            return false;
        }

        return true;
    }

    private bool IsRetryReady(AIDealer dealer)
    {
        return !nextRetryTime.TryGetValue(dealer, out float retryTime) || Time.time >= retryTime;
    }

    private void QueueRouteRetry(AIDealer dealer)
    {
        if (dealer != null)
        {
            nextRetryTime[dealer] = Time.time + 1f;
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
