using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(CustomerPatience))]
[DisallowMultipleComponent]
public sealed class CasinoCustomerAI : MonoBehaviour
{
    private enum CustomerState
    {
        Wandering,
        TravellingToSeat,
        VisitingCashier,
        Seated,
        Leaving
    }

    public static int RoundsAfterCompletedHand(int roundsRemaining)
    {
        return Mathf.Max(0, roundsRemaining - 1);
    }

    private NavMeshAgent agent;
    private CustomerPatience patience;
    private CasinoCustomerSpawner spawner;
    private CasinoSeat[] seats;
    private CasinoSeat reservedSeat;
    private Transform exit;
    private Transform cashierWaypoint;
    private Transform visual;
    private Renderer bodyRenderer;
    private MaterialPropertyBlock colorBlock;
    private Color baseColor;
    private CustomerState state;
    private float wanderRadius;
    private float seatChoiceChance;
    private float nextDecisionTime;
    private float customerHitThreshold;
    private Vector3 seatNavPosition;
    private float leavingDelay;
    private bool serviceActive;
    private bool penaltyRecorded;
    private bool headingToExit;
    private int roundsRemaining;
    [SerializeField, Min(1)] private int minimumBetPence = 1000;
    [SerializeField, Min(1)] private int maximumBetPence = 5000;

    public event Action<CasinoCustomerAI> Seated;
    public event Action<CasinoCustomerAI> LeavingSeat;
    public bool IsSeated => state == CustomerState.Seated;
    public bool IsInService => serviceActive;
    public CasinoSeat Seat => reservedSeat;
    public float PatienceNormalized => patience != null ? patience.Normalized : 1f;
    public int RoundsRemaining => roundsRemaining;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        patience = GetComponent<CustomerPatience>();
        agent.radius = 0.3f;
        agent.height = 1.7f;
        agent.speed = UnityEngine.Random.Range(2.1f, 2.8f);
        agent.acceleration = 12f;
        agent.angularSpeed = 540f;
        agent.stoppingDistance = 0.12f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        agent.avoidancePriority = UnityEngine.Random.Range(25, 65);
    }

    public void Configure(
        CasinoCustomerSpawner customerSpawner,
        CasinoSeat[] casinoSeats,
        Transform exitPoint,
        Transform cashier,
        Transform characterVisual,
        Renderer coloredBody,
        Color color,
        float roamingRadius,
        float chooseSeatChance)
    {
        spawner = customerSpawner;
        seats = casinoSeats;
        exit = exitPoint;
        cashierWaypoint = cashier;
        visual = characterVisual;
        bodyRenderer = coloredBody;
        baseColor = color;
        wanderRadius = roamingRadius;
        seatChoiceChance = chooseSeatChance;
        roundsRemaining = UnityEngine.Random.Range(
            customerSpawner.MinimumRoundsPerVisit,
            customerSpawner.MaximumRoundsPerVisit + 1);
        customerHitThreshold = UnityEngine.Random.Range(16f, 18f);
        colorBlock = new MaterialPropertyBlock();
        patience.PatienceExpired += OnPatienceExpired;
        SetBodyColor(color);
        state = CustomerState.Wandering;
        SetNextDecisionTime();
    }

    private void Update()
    {
        UpdateImpatienceColor();

        if (state == CustomerState.TravellingToSeat && !agent.pathPending && agent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            ReleaseSeat();
            BeginWandering();
        }
        else if (state == CustomerState.TravellingToSeat && HasReachedDestination())
        {
            if (reservedSeat != null && reservedSeat.SeatCustomer(this))
            {
                transform.rotation = reservedSeat.Facing;
                state = CustomerState.Seated;
                agent.isStopped = true;
                patience.Begin(spawner.PatienceSeconds);
                Seated?.Invoke(this);
            }
            else
            {
                ReleaseSeat();
                BeginWandering();
            }
        }
        else if (state == CustomerState.Wandering && Time.time >= nextDecisionTime && agent.isOnNavMesh)
        {
            SetNextDecisionTime();
            if (UnityEngine.Random.value < seatChoiceChance && TryChooseSeat())
            {
                return;
            }

            SetWanderDestination();
        }
        else if (state == CustomerState.Leaving)
        {
            if (leavingDelay > 0f)
            {
                leavingDelay -= Time.deltaTime;
            }
            else if (!headingToExit)
            {
                headingToExit = true;
                agent.isStopped = false;
                if (exit == null || !agent.SetDestination(exit.position))
                {
                    Debug.LogError($"{name} could not find a NavMesh path to the casino exit.", this);
                }
            }
            else if (HasReachedDestination())
            {
                Despawn();
            }
        }
        else if (state == CustomerState.VisitingCashier && HasReachedDestination())
        {
            BeginWandering();
        }

        if (visual != null)
        {
            float targetHeight = state == CustomerState.Seated ? 0.22f : 0f;
            Vector3 position = visual.localPosition;
            Vector3 seatedOffset = state == CustomerState.Seated && reservedSeat != null
                ? reservedSeat.SeatedVisualOffset
                : Vector3.zero;
            position.x = Mathf.MoveTowards(position.x, seatedOffset.x, Time.deltaTime * 2f);
            position.y = Mathf.MoveTowards(position.y, targetHeight, Time.deltaTime * 1.2f);
            position.z = Mathf.MoveTowards(position.z, seatedOffset.z, Time.deltaTime * 2f);
            visual.localPosition = position;
            Vector3 scale = visual.localScale;
            scale.y = Mathf.MoveTowards(scale.y, state == CustomerState.Seated ? 0.82f : 1f, Time.deltaTime * 0.8f);
            visual.localScale = scale;
        }
    }

    public bool BeginService()
    {
        if (state != CustomerState.Seated || serviceActive)
        {
            return false;
        }

        serviceActive = true;
        patience.Pause();
        return true;
    }

    public void ResumeWaiting()
    {
        if (state == CustomerState.Seated && serviceActive)
        {
            serviceActive = false;
            patience.Resume();
        }
    }

    public bool WantsAnotherCard(int handTotal)
    {
        return handTotal < customerHitThreshold;
    }

    public int ChooseBetPence(IReadOnlyList<int> availableStakesPence)
    {
        if (!TryChooseBetPence(availableStakesPence, out int betPence))
        {
            throw new InvalidOperationException($"{name} has no available blackjack stake within their configured betting limits.");
        }

        return betPence;
    }

    public bool TryChooseBetPence(IReadOnlyList<int> availableStakesPence, out int betPence)
    {
        if (availableStakesPence == null)
        {
            throw new ArgumentNullException(nameof(availableStakesPence));
        }

        List<int> eligibleStakes = new List<int>();
        for (int i = 0; i < availableStakesPence.Count; i++)
        {
            int stake = availableStakesPence[i];
            if (stake >= minimumBetPence && stake <= maximumBetPence)
            {
                eligibleStakes.Add(stake);
            }
        }

        if (eligibleStakes.Count == 0)
        {
            betPence = 0;
            return false;
        }

        betPence = eligibleStakes[UnityEngine.Random.Range(0, eligibleStakes.Count)];
        return true;
    }

    public void CompleteBlackjackRound()
    {
        if (state != CustomerState.Seated || !serviceActive)
        {
            return;
        }

        serviceActive = false;
        roundsRemaining = RoundsAfterCompletedHand(roundsRemaining);
        if (roundsRemaining > 0)
        {
            patience.Restart(spawner.PatienceSeconds);
            reservedSeat.NotifyReadyForService(this);
            return;
        }

        float activity = UnityEngine.Random.value;
        if (activity < spawner.LeaveAfterVisitChance)
        {
            StartLeaving(false);
        }
        else if (cashierWaypoint != null &&
            activity < spawner.LeaveAfterVisitChance + spawner.CashierAfterVisitChance &&
            agent.SetDestination(cashierWaypoint.position))
        {
            ReleaseSeat();
            state = CustomerState.VisitingCashier;
            agent.isStopped = false;
        }
        else
        {
            ReleaseSeat();
            state = CustomerState.Wandering;
            agent.isStopped = false;
            SetNextDecisionTime();
        }
    }

    private bool TryChooseSeat()
    {
        if (seats == null || seats.Length == 0)
        {
            return false;
        }

        int offset = UnityEngine.Random.Range(0, seats.Length);
        for (int i = 0; i < seats.Length; i++)
        {
            CasinoSeat candidate = seats[(offset + i) % seats.Length];
            if (candidate == null || !candidate.TryReserve(this))
            {
                continue;
            }

            if (!NavMesh.SamplePosition(candidate.Position, out NavMeshHit seatHit, 1.5f, NavMesh.AllAreas) ||
                !agent.SetDestination(seatHit.position))
            {
                candidate.Release(this);
                continue;
            }

            reservedSeat = candidate;
            seatNavPosition = seatHit.position;
            state = CustomerState.TravellingToSeat;
            agent.isStopped = false;
            return true;
        }

        return false;
    }

    private void SetWanderDestination()
    {
        Vector3 randomPoint = transform.position + UnityEngine.Random.insideUnitSphere * wanderRadius;
        randomPoint.y = transform.position.y;
        if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    private bool HasReachedDestination()
    {
        return agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.08f;
    }

    private void SetNextDecisionTime()
    {
        nextDecisionTime = Time.time + UnityEngine.Random.Range(2f, 4f);
    }

    private void BeginWandering()
    {
        state = CustomerState.Wandering;
        agent.isStopped = false;
        SetNextDecisionTime();
        SetWanderDestination();
    }

    private void OnPatienceExpired()
    {
        if (state == CustomerState.Seated)
        {
            StartLeaving(true);
        }
    }

    private void StartLeaving(bool frustrated)
    {
        if (state == CustomerState.Leaving)
        {
            return;
        }

        if (frustrated && !penaltyRecorded)
        {
            penaltyRecorded = true;
            CustomerSatisfaction.RecordPenalty(this);
        }

        LeavingSeat?.Invoke(this);
        ReleaseSeat();
        serviceActive = false;
        state = CustomerState.Leaving;
        agent.isStopped = true;
        leavingDelay = 0.45f;
        headingToExit = false;
        if (exit == null || !agent.isOnNavMesh)
        {
            Debug.LogError($"{name} could not find a NavMesh path to the casino exit.", this);
        }
    }

    private void ReleaseSeat()
    {
        if (reservedSeat != null)
        {
            reservedSeat.Release(this);
            reservedSeat = null;
        }
    }

    private void UpdateImpatienceColor()
    {
        if (bodyRenderer == null || patience == null || !patience.IsWaiting)
        {
            return;
        }

        float frustration = 1f - patience.Normalized;
        SetBodyColor(Color.Lerp(baseColor, new Color(0.88f, 0.12f, 0.08f), frustration * 0.8f));
    }

    private void SetBodyColor(Color color)
    {
        if (bodyRenderer == null)
        {
            return;
        }

        bodyRenderer.GetPropertyBlock(colorBlock);
        colorBlock.SetColor("_BaseColor", color);
        colorBlock.SetColor("_Color", color);
        bodyRenderer.SetPropertyBlock(colorBlock);
    }

    private void Despawn()
    {
        spawner?.NotifyDespawned(this);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (patience != null)
        {
            patience.PatienceExpired -= OnPatienceExpired;
        }

        ReleaseSeat();
    }
}
