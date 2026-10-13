using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[DisallowMultipleComponent]
public sealed class AIDealer : MonoBehaviour
{
    private enum DealerState
    {
        Unassigned,
        TravelingToTable,
        Working,
        TravelingToBreak,
        Resting
    }

    [SerializeField] private string dealerId;
    [SerializeField, Range(0f, 100f)] private float energy = 100f;
    [SerializeField] private DealerAssignmentManager manager;
    [SerializeField] private Transform breakRoomPosition;
    [SerializeField] private CasinoDealerStation initialAssignedStation;
    [SerializeField, Min(0f)] private float energyDrainPerSecond = 0.12f;
    [SerializeField, Min(0f)] private float energyRecoveryPerSecond = 0.38f;
    [SerializeField, Range(0f, 100f)] private float tiredThreshold = 30f;
    [SerializeField, Range(0f, 100f)] private float exhaustedThreshold = 12f;

    private NavMeshAgent agent;
    private CasinoDealerStation pendingStation;
    private DealerState state;
    private Vector3 originalScale;
    private bool hasRequestedBreak;
    private bool hasRequestedExhaustion;

    public string DealerId => string.IsNullOrWhiteSpace(dealerId) ? name : dealerId;
    public float Energy => energy;
    public float TiredThreshold => tiredThreshold;
    public float ExhaustedThreshold => exhaustedThreshold;
    public CasinoDealerStation InitialAssignedStation => initialAssignedStation;
    public CasinoDealerStation AssignedStation { get; private set; }
    public bool IsAtStation => state == DealerState.Working && AssignedStation != null;
    public bool IsResting => state == DealerState.Resting;
    public bool IsAvailable => state == DealerState.Resting && energy > exhaustedThreshold;
    public float CurrentStationTime { get; private set; }
    public float WorkSpeedMultiplier => 1f + Mathf.InverseLerp(100f, tiredThreshold, energy) * 1.8f;
    public string StatusLabel => state switch
    {
        DealerState.TravelingToTable => "WALKING TO TABLE",
        DealerState.Working => "WORKING",
        DealerState.TravelingToBreak => "WALKING TO BREAK",
        DealerState.Resting => "BREAK ROOM",
        _ => "AVAILABLE"
    };

    public void Configure(
        DealerAssignmentManager assignmentManager,
        Transform breakPosition,
        CasinoDealerStation initialStation,
        float startingEnergy)
    {
        manager = assignmentManager;
        breakRoomPosition = breakPosition;
        initialAssignedStation = initialStation;
        energy = Mathf.Clamp(startingEnergy, 0f, 100f);
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
        else
        {
            state = DealerState.Resting;
            if (breakRoomPosition != null)
            {
                transform.SetPositionAndRotation(breakRoomPosition.position, breakRoomPosition.rotation);
            }
        }
    }

    private void Start()
    {
        if (manager == null || breakRoomPosition == null)
        {
            Debug.LogError($"{nameof(AIDealer)} on {name} is missing its manager or break-room destination.", this);
            enabled = false;
            return;
        }

        Configure(manager, breakRoomPosition, initialAssignedStation, energy);
    }

    public bool BeginAssignment(CasinoDealerStation station)
    {
        if (!IsAvailable || station == null || station.DealerPosition == null || !agent.isOnNavMesh)
        {
            return false;
        }

        pendingStation = station;
        state = DealerState.TravelingToTable;
        agent.isStopped = false;
        if (!agent.SetDestination(station.DealerPosition.position))
        {
            pendingStation = null;
            state = DealerState.Resting;
            return false;
        }

        return true;
    }

    public void GoToBreakRoom()
    {
        AssignedStation = null;
        pendingStation = null;
        CurrentStationTime = 0f;
        hasRequestedBreak = false;
        hasRequestedExhaustion = false;
        state = DealerState.TravelingToBreak;
        agent.updateRotation = true;
        if (!agent.isOnNavMesh || breakRoomPosition == null || !agent.SetDestination(breakRoomPosition.position))
        {
            Debug.LogError($"{name} could not navigate to the dealer break room.", this);
            return;
        }

        agent.isStopped = false;
    }

    public void SetStation(CasinoDealerStation station)
    {
        AssignedStation = station;
        CurrentStationTime = 0f;
        pendingStation = null;
        hasRequestedBreak = false;
        hasRequestedExhaustion = false;
        state = station != null ? DealerState.Working : DealerState.TravelingToBreak;
        if (station != null)
        {
            if (agent != null)
            {
                if (agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                }

                agent.updateRotation = false;
            }

            transform.rotation = station.DealerPosition.rotation;
        }
    }

    public void SetBreakPosition(Transform position)
    {
        breakRoomPosition = position;
    }

    private void Update()
    {
        if (state == DealerState.Working)
        {
            CurrentStationTime += Time.deltaTime;
            bool activelyWorking = AssignedStation.BlackjackTable == null ||
                                   AssignedStation.BlackjackTable.IsActivelyServing;
            if (activelyWorking)
            {
                energy = CalculateEnergyAfterWork(energy, energyDrainPerSecond, Time.deltaTime);
            }
            if (!hasRequestedBreak && energy <= tiredThreshold)
            {
                hasRequestedBreak = true;
                AssignedStation?.RequestDealerBreakAttention();
            }
            if (!hasRequestedExhaustion && energy <= exhaustedThreshold)
            {
                hasRequestedExhaustion = true;
                AssignedStation?.Attention.RequestAttention(TableAttentionReason.DealerExhausted);
            }

            if (transform.localScale != originalScale)
            {
                transform.localScale = originalScale;
            }
        }
        else if (state == DealerState.TravelingToTable && agent.isOnNavMesh &&
                 !agent.pathPending && agent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            manager?.NotifyDealerCouldNotReach(this, pendingStation);
        }
        else if (state == DealerState.TravelingToTable && HasReachedDestination())
        {
            agent.isStopped = true;
            CasinoDealerStation arrivedAt = pendingStation;
            manager?.NotifyDealerArrived(this, arrivedAt);
        }
        else if (state == DealerState.TravelingToBreak && agent.isOnNavMesh &&
                 !agent.pathPending && agent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            Debug.LogError($"{name} has no complete NavMesh path to the dealer break room.", this);
            state = DealerState.Unassigned;
            agent.isStopped = true;
        }
        else if (state == DealerState.TravelingToBreak && HasReachedDestination())
        {
            state = DealerState.Resting;
            agent.isStopped = true;
        }
        else if (state == DealerState.Resting)
        {
            energy = CalculateEnergyAfterRest(energy, energyRecoveryPerSecond, Time.deltaTime);
        }

        float tired = Mathf.InverseLerp(100f, exhaustedThreshold, energy);
        transform.localScale = originalScale * (1f - tired * 0.06f);
    }

    private bool HasReachedDestination()
    {
        return agent != null && agent.isOnNavMesh && !agent.pathPending &&
               agent.remainingDistance <= agent.stoppingDistance + 0.08f;
    }

    public static float CalculateEnergyAfterWork(float currentEnergy, float drainPerSecond, float elapsedSeconds)
    {
        return Mathf.Max(0f, currentEnergy - Mathf.Max(0f, drainPerSecond) * Mathf.Max(0f, elapsedSeconds));
    }

    public static float CalculateEnergyAfterRest(float currentEnergy, float recoveryPerSecond, float elapsedSeconds)
    {
        return Mathf.Min(100f, currentEnergy + Mathf.Max(0f, recoveryPerSecond) * Mathf.Max(0f, elapsedSeconds));
    }
}
