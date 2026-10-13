using UnityEngine;
using UnityEngine.AI;

public enum DealerDutyState
{
    Working,
    NeedsBreak,
    ReplacementRequested,
    TravelingToBreak,
    Resting,
    TravelingToPitDesk,
    ReadyAtPitDesk,
    TravelingToTable
}

[RequireComponent(typeof(NavMeshAgent))]
[DisallowMultipleComponent]
public sealed class AIDealer : MonoBehaviour
{
    [SerializeField] private string dealerId;
    [SerializeField, Range(0f, 100f)] private float energy = 100f;
    [SerializeField] private DealerAssignmentManager manager;
    [SerializeField] private Transform breakRoomPosition;
    [SerializeField] private Transform pitDeskPosition;
    [SerializeField] private CasinoDealerStation initialAssignedStation;
    [SerializeField, Min(0f)] private float energyDrainPerSecond = 0.12f;
    [SerializeField, Min(0f)] private float energyRecoveryPerSecond = 0.38f;
    [SerializeField, Range(0f, 100f)] private float tiredThreshold = 30f;
    [SerializeField, Range(0f, 100f)] private float exhaustedThreshold = 12f;
    [SerializeField, Range(90f, 100f)] private float fullRestThreshold = 97f;

    private NavMeshAgent agent;
    private CasinoDealerStation pendingStation;
    private DealerDutyState state;
    private Vector3 originalScale;
    private bool hasRequestedExhaustion;
    private bool pathFailureReported;
    private bool restAtPitDeskOnArrival;

    public string DealerId => string.IsNullOrWhiteSpace(dealerId) ? name : dealerId;
    public float Energy => energy;
    public float TiredThreshold => tiredThreshold;
    public float ExhaustedThreshold => exhaustedThreshold;
    public float FullRestThreshold => fullRestThreshold;
    public CasinoDealerStation InitialAssignedStation => initialAssignedStation;
    public CasinoDealerStation AssignedStation { get; private set; }
    public DealerDutyState State => state;
    public bool IsAtStation => AssignedStation != null &&
                               (state == DealerDutyState.Working || state == DealerDutyState.NeedsBreak ||
                                state == DealerDutyState.ReplacementRequested);
    public bool IsResting => state == DealerDutyState.Resting;
    public bool IsReadyAtPitDesk => state == DealerDutyState.ReadyAtPitDesk;
    public bool IsAvailable => CanBeAssignedFromState(state);
    public bool NeedsBreak => state == DealerDutyState.NeedsBreak || state == DealerDutyState.ReplacementRequested;
    public Transform ReservedPitPosition { get; private set; }
    public Transform Destination => state switch
    {
        DealerDutyState.TravelingToTable => pendingStation != null ? pendingStation.DealerPosition : null,
        DealerDutyState.TravelingToBreak => breakRoomPosition,
        DealerDutyState.TravelingToPitDesk => ReservedPitPosition,
        _ => null
    };
    public float CurrentStationTime { get; private set; }
    public float WorkSpeedMultiplier => 1f + Mathf.InverseLerp(100f, tiredThreshold, energy) * 1.8f;
    public string StatusLabel => state switch
    {
        DealerDutyState.Working => "WORKING",
        DealerDutyState.NeedsBreak => "NEEDS BREAK",
        DealerDutyState.ReplacementRequested => "REPLACEMENT REQUESTED",
        DealerDutyState.TravelingToBreak or DealerDutyState.TravelingToPitDesk or DealerDutyState.TravelingToTable => "IN TRANSIT",
        DealerDutyState.Resting => "RESTING",
        DealerDutyState.ReadyAtPitDesk => "READY",
        _ => "UNASSIGNED"
    };

    public void Configure(
        DealerAssignmentManager assignmentManager,
        Transform breakPosition,
        Transform initialPitPosition,
        CasinoDealerStation initialStation,
        float startingEnergy,
        float restThreshold)
    {
        manager = assignmentManager;
        breakRoomPosition = breakPosition;
        pitDeskPosition = initialPitPosition;
        initialAssignedStation = initialStation;
        energy = Mathf.Clamp(startingEnergy, 0f, 100f);
        fullRestThreshold = Mathf.Clamp(restThreshold, 90f, 100f);
        agent = GetComponent<NavMeshAgent>();
        agent.radius = 0.28f;
        agent.height = 1.7f;
        agent.speed = 2.8f;
        agent.acceleration = 10f;
        agent.angularSpeed = 540f;
        agent.stoppingDistance = 0.1f;
        originalScale = transform.localScale;

        if (initialStation != null)
        {
            initialStation.SetInitialDealer(this);
            transform.rotation = initialStation.DealerPosition.rotation;
            agent.updateRotation = false;
        }
        else if (pitDeskPosition != null)
        {
            ReservedPitPosition = pitDeskPosition;
            transform.SetPositionAndRotation(pitDeskPosition.position, pitDeskPosition.rotation);
            state = DealerDutyState.ReadyAtPitDesk;
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }

            manager.NotifyDealerReady(this);
        }
    }

    private void Start()
    {
        if (manager == null || breakRoomPosition == null ||
            pitDeskPosition == null && initialAssignedStation == null)
        {
            Debug.LogError($"{nameof(AIDealer)} on {name} is missing its manager or a destination.", this);
            enabled = false;
            return;
        }

        Configure(manager, breakRoomPosition, pitDeskPosition, initialAssignedStation, energy, fullRestThreshold);
    }

    public bool BeginAssignment(CasinoDealerStation station)
    {
        if (!IsReadyAtPitDesk || station == null || station.DealerPosition == null || !agent.isOnNavMesh)
        {
            return false;
        }

        agent.updateRotation = true;
        if (!agent.SetDestination(station.DealerPosition.position))
        {
            return false;
        }

        pendingStation = station;
        ReservedPitPosition = null;
        pathFailureReported = false;
        state = DealerDutyState.TravelingToTable;
        agent.isStopped = false;
        return true;
    }

    public bool ReturnToPitDesk(Transform position)
    {
        return ReturnToPitDesk(position, false);
    }

    public bool ReturnToPitDesk(Transform position, bool restAtPitDesk)
    {
        if (position == null || !agent.isOnNavMesh)
        {
            return false;
        }

        agent.updateRotation = true;
        if (!agent.SetDestination(position.position))
        {
            return false;
        }

        AssignedStation = null;
        pendingStation = null;
        ReservedPitPosition = position;
        restAtPitDeskOnArrival = restAtPitDesk;
        pathFailureReported = false;
        state = DealerDutyState.TravelingToPitDesk;
        agent.isStopped = false;
        return true;
    }

    public bool GoToBreakRoom(Transform position)
    {
        if (position == null || !agent.isOnNavMesh)
        {
            return false;
        }

        agent.updateRotation = true;
        if (!agent.SetDestination(position.position))
        {
            return false;
        }

        AssignedStation = null;
        pendingStation = null;
        breakRoomPosition = position;
        restAtPitDeskOnArrival = false;
        CurrentStationTime = 0f;
        pathFailureReported = false;
        state = DealerDutyState.TravelingToBreak;
        agent.isStopped = false;
        return true;
    }

    public void SetStation(CasinoDealerStation station)
    {
        AssignedStation = station;
        CurrentStationTime = 0f;
        pendingStation = null;
        ReservedPitPosition = null;
        restAtPitDeskOnArrival = false;
        hasRequestedExhaustion = false;
        state = DealerDutyState.Working;
        if (agent != null)
        {
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }

            agent.updateRotation = false;
        }

        if (station != null)
        {
            transform.rotation = station.DealerPosition.rotation;
        }
    }

    public void MarkReadyAtPitDesk()
    {
        AssignedStation = null;
        pendingStation = null;
        restAtPitDeskOnArrival = false;
        state = DealerDutyState.ReadyAtPitDesk;
        if (ReservedPitPosition != null)
        {
            transform.rotation = ReservedPitPosition.rotation;
        }

        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.updateRotation = false;
        }
    }

    public void MarkResting()
    {
        state = DealerDutyState.Resting;
        restAtPitDeskOnArrival = false;
        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }
    }

    public void RequestBreak()
    {
        if (state != DealerDutyState.Working)
        {
            return;
        }

        state = DealerDutyState.NeedsBreak;
        AssignedStation?.RequestDealerBreakAttention();
    }

    public void MarkReplacementRequested()
    {
        if (state == DealerDutyState.NeedsBreak)
        {
            state = DealerDutyState.ReplacementRequested;
        }
    }

    public void CancelReplacementRequest()
    {
        if (state == DealerDutyState.ReplacementRequested)
        {
            state = energy <= tiredThreshold ? DealerDutyState.NeedsBreak : DealerDutyState.Working;
        }
    }

    public void NotifyAssignmentFailed()
    {
        if (pathFailureReported)
        {
            return;
        }

        pathFailureReported = true;
        manager?.NotifyDealerPathFailed(this);
    }

    private void Update()
    {
        if (IsAtStation)
        {
            CurrentStationTime += Time.deltaTime;
            if (AssignedStation.BlackjackTable == null || AssignedStation.BlackjackTable.IsActivelyServing)
            {
                energy = CalculateEnergyAfterWork(energy, energyDrainPerSecond, Time.deltaTime);
            }

            if (state == DealerDutyState.Working && energy <= tiredThreshold)
            {
                RequestBreak();
            }

            if (!hasRequestedExhaustion && energy <= exhaustedThreshold)
            {
                hasRequestedExhaustion = true;
                AssignedStation?.Attention.RequestAttention(TableAttentionReason.DealerExhausted);
            }
        }
        else if (state == DealerDutyState.Resting)
        {
            energy = CalculateEnergyAfterRest(energy, energyRecoveryPerSecond, Time.deltaTime);
            if (energy >= fullRestThreshold)
            {
                manager?.SendRestedDealerToPitDesk(this);
            }
        }
        else if ((state == DealerDutyState.TravelingToTable ||
                  state == DealerDutyState.TravelingToBreak ||
                  state == DealerDutyState.TravelingToPitDesk) &&
                 agent.isOnNavMesh && !agent.pathPending &&
                 agent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            NotifyAssignmentFailed();
        }
        else if (HasReachedDestination())
        {
            switch (state)
            {
                case DealerDutyState.TravelingToTable:
                    manager?.NotifyDealerArrived(this, pendingStation);
                    break;
                case DealerDutyState.TravelingToBreak:
                    MarkResting();
                    break;
                case DealerDutyState.TravelingToPitDesk:
                    if (restAtPitDeskOnArrival)
                    {
                        MarkResting();
                    }
                    else
                    {
                        MarkReadyAtPitDesk();
                        manager?.NotifyDealerReady(this);
                    }

                    break;
            }
        }

        float fatigue = Mathf.InverseLerp(100f, exhaustedThreshold, energy);
        transform.localScale = originalScale * (1f - fatigue * 0.06f);
    }

    private bool HasReachedDestination()
    {
        return !agent.isStopped && agent.isOnNavMesh && !agent.pathPending &&
               agent.hasPath && agent.remainingDistance <= agent.stoppingDistance + 0.08f;
    }

    public static float CalculateEnergyAfterWork(float currentEnergy, float drainPerSecond, float elapsedSeconds)
    {
        return Mathf.Max(0f, currentEnergy - Mathf.Max(0f, drainPerSecond) * Mathf.Max(0f, elapsedSeconds));
    }

    public static float CalculateEnergyAfterRest(float currentEnergy, float recoveryPerSecond, float elapsedSeconds)
    {
        return Mathf.Min(100f, currentEnergy + Mathf.Max(0f, recoveryPerSecond) * Mathf.Max(0f, elapsedSeconds));
    }

    public static bool CanBeAssignedFromState(DealerDutyState dealerState)
    {
        return dealerState == DealerDutyState.ReadyAtPitDesk;
    }
}
