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
    private bool cashierArrivalReported;
    private int roundsRemaining;
    private Vector3 lastTravelPosition;
    private float stationarySeconds;
    private bool vip;
    [SerializeField, Min(1)] private int minimumBetPence = 500;
    [SerializeField, Min(1)] private int maximumBetPence = 5000;

    public event Action<CasinoCustomerAI> Seated;
    public event Action<CasinoCustomerAI> LeavingSeat;
    public bool IsSeated => state == CustomerState.Seated;
    public bool IsWaitingAtCashDesk => state == CustomerState.VisitingCashier && cashierArrivalReported;
    public bool IsVisitingCashier => state == CustomerState.VisitingCashier;
    public bool CanBeGuidedToCashier => state == CustomerState.Wandering && !serviceActive;
    public bool IsInService => serviceActive;
    public bool IsVip => vip;
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
        SetBodyColor(bodyRenderer is SpriteRenderer ? Color.white : color);
        state = CustomerState.Wandering;
        SetNextDecisionTime();
    }

    private void Update()
    {
        if (Time.timeScale == 0) return;
        if (state == CustomerState.Seated && serviceActive && reservedSeat != null)
        {
            AIDealer dealer = reservedSeat.DealerStation != null ? reservedSeat.DealerStation.AssignedDealer : null;
            BlackjackTableInteraction blackjack = reservedSeat.DealerStation?.BlackjackTable;
            RouletteRoundController roulette = reservedSeat.DealerStation?.GetComponent<RouletteRoundController>();
            bool available = blackjack != null ? blackjack.IsServiceAvailable && !blackjack.IsShuffleReady :
                roulette != null ? roulette.IsServiceAvailable && !roulette.HasPendingAssistance :
                dealer != null && dealer.CanServe;
            if (available) patience.Pause();
            else patience.Resume();
        }
        UpdateImpatienceColor();
        if ((state == CustomerState.TravellingToSeat || state == CustomerState.VisitingCashier && !cashierArrivalReported) &&
            agent.isOnNavMesh && !agent.pathPending)
        {
            stationarySeconds = (transform.position - lastTravelPosition).sqrMagnitude < .01f
                ? stationarySeconds + Time.deltaTime : 0;
            if (stationarySeconds == 0) lastTravelPosition = transform.position;
            if (stationarySeconds > 20)
            {
                Debug.LogWarning($"{name} was blocked while travelling; releasing the reservation and choosing a new route.", this);
                ReleaseSeat();
                patience.Pause();
                BeginWandering();
                stationarySeconds = 0;
            }
        }
        else stationarySeconds = 0;

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
                agent.enabled = false;
                patience.Begin(WaitingPatienceSeconds);
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
            if (!cashierArrivalReported)
            {
                cashierArrivalReported = true;
                agent.isStopped = true;
                patience.Begin(WaitingPatienceSeconds + 35);
                spawner.NotifyCashierArrival(this);
            }
        }

        if (visual != null)
        {
            Vector3 seatedOffset = state == CustomerState.Seated && reservedSeat != null
                ? transform.InverseTransformPoint(reservedSeat.SittingPosition)
                : Vector3.zero;
            visual.localPosition = Vector3.MoveTowards(visual.localPosition, seatedOffset, Time.deltaTime * 2f);
        }
    }

    public bool BeginService()
    {
        if (state != CustomerState.Seated || serviceActive)
        {
            return false;
        }

        serviceActive = true;
        if (roundsRemaining == 0)
            roundsRemaining = UnityEngine.Random.Range(spawner.MinimumRoundsPerVisit, spawner.MaximumRoundsPerVisit + 1);
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
        if (vip && maximumBetPence >= 10000 && !eligibleStakes.Contains(10000))
            eligibleStakes.Add(10000);

        if (eligibleStakes.Count == 0)
        {
            betPence = 0;
            return false;
        }

        betPence = eligibleStakes[UnityEngine.Random.Range(0, eligibleStakes.Count)];
        return true;
    }

    public void SetVip()
    {
        if (vip) return;
        vip = true;
        minimumBetPence = 2500;
        maximumBetPence = 10000;
        PixelCharacterVisual sprite = GetComponentInChildren<PixelCharacterVisual>();
        sprite?.SetVip(true);
        sprite?.PlayAction(CasinoAnimationPose.Celebrate, transform.position + transform.forward, 1.1f);
        CasinoSoundscape.Play(CasinoSoundCue.Celebrate, transform.position);
    }

    public void EscortBySecurity()
    {
        if (state == CustomerState.Leaving) return;
        GetComponentInChildren<PixelCharacterVisual>()?.PlayAction(
            CasinoAnimationPose.Disappointed, transform.position + transform.forward, .9f);
        StartLeaving(false);
    }

    public void ReactToFalseAccusation() => StartLeaving(true);

    private float WaitingPatienceSeconds => spawner.PatienceSeconds * (vip ? 0.62f : 1f);

    public void CompleteBlackjackRound()
    {
        if (state != CustomerState.Seated || !serviceActive)
        {
            return;
        }

        CompleteGamingRound();
    }

    public void CompleteRouletteRound()
    {
        if (state != CustomerState.Seated || !serviceActive)
        {
            return;
        }

        CompleteGamingRound();
    }

    private void CompleteGamingRound()
    {
        serviceActive = false;
        roundsRemaining = RoundsAfterCompletedHand(roundsRemaining);
        if (roundsRemaining > 0)
        {
            patience.Restart(WaitingPatienceSeconds);
            reservedSeat.NotifyReadyForService(this);
            return;
        }

        ReleaseSeat();
        float activity = UnityEngine.Random.value;
        if (activity < spawner.LeaveAfterVisitChance)
        {
            StartLeaving(false);
        }
        else if (cashierWaypoint != null &&
            activity < spawner.LeaveAfterVisitChance + spawner.CashierAfterVisitChance &&
            agent.SetDestination(cashierWaypoint.position))
        {
            state = CustomerState.VisitingCashier;
            cashierArrivalReported = false;
            agent.isStopped = false;
        }
        else
        {
            state = CustomerState.Wandering;
            agent.isStopped = false;
            SetNextDecisionTime();
        }
    }

    public void CompleteCashierVisit()
    {
        if (state != CustomerState.VisitingCashier || !cashierArrivalReported)
        {
            return;
        }

        cashierArrivalReported = false;
        patience.Pause();
        state = CustomerState.Wandering;
        agent.isStopped = false;
        SetNextDecisionTime();
    }

    public bool TryVisitCashier()
    {
        if (cashierWaypoint == null || agent == null || !agent.isOnNavMesh ||
            !agent.SetDestination(cashierWaypoint.position)) return false;
        ReleaseSeat();
        serviceActive = false;
        state = CustomerState.VisitingCashier;
        cashierArrivalReported = false;
        agent.isStopped = false;
        return true;
    }

    private bool TryChooseSeat()
    {
        if (seats == null || seats.Length == 0)
        {
            return false;
        }

        List<CasinoSeat> availableSeats = new List<CasinoSeat>();
        List<CasinoCustomerGame> availableGames = new List<CasinoCustomerGame>();
        List<float> gameWeights = new List<float>();
        foreach (CasinoCustomerGame game in Enum.GetValues(typeof(CasinoCustomerGame)))
        {
            List<CasinoSeat> gameSeats = new List<CasinoSeat>();
            for (int i = 0; i < seats.Length; i++)
            {
                CasinoSeat candidate = seats[i];
                if (candidate != null && candidate.GameType == game &&
                    candidate.IsAvailable && candidate.IsGameAvailable)
                {
                    gameSeats.Add(candidate);
                }
            }

            float weight = spawner.GetGameSelectionWeight(game);
            if (gameSeats.Count == 0 || weight <= 0f)
            {
                continue;
            }

            availableSeats.AddRange(gameSeats);
            availableGames.Add(game);
            gameWeights.Add(weight);
        }

        if (!CasinoCustomerSpawner.TryChooseWeightedGame(
                availableGames, gameWeights, UnityEngine.Random.value, out CasinoCustomerGame selectedGame))
        {
            return false;
        }
        List<CasinoSeat> selectedSeats = new List<CasinoSeat>();
        foreach (CasinoSeat seat in availableSeats)
        {
            if (seat.GameType == selectedGame)
            {
                selectedSeats.Add(seat);
            }
        }

        int offset = UnityEngine.Random.Range(0, selectedSeats.Count);
        for (int i = 0; i < selectedSeats.Count; i++)
        {
            CasinoSeat candidate = selectedSeats[(offset + i) % selectedSeats.Count];
            if (!candidate.TryReserve(this))
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

    public bool TryVisitSeat(CasinoSeat candidate)
    {
        if (state != CustomerState.Wandering || !agent.isOnNavMesh || candidate == null ||
            !candidate.IsGameAvailable || !candidate.TryReserve(this))
            return false;
        if (!NavMesh.SamplePosition(candidate.Position, out NavMeshHit hit, 1.5f, NavMesh.AllAreas) ||
            !agent.SetDestination(hit.position))
        {
            candidate.Release(this);
            return false;
        }
        reservedSeat = candidate;
        seatNavPosition = hit.position;
        state = CustomerState.TravellingToSeat;
        agent.isStopped = false;
        return true;
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
        if (state == CustomerState.Seated || state == CustomerState.VisitingCashier && cashierArrivalReported)
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
            CustomerSatisfaction.RecordPenalty(this, vip ? 2 : 1);
        }

        LeavingSeat?.Invoke(this);
        patience.Pause();
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

    private void ReleaseSeat(bool resumeNavigation = true)
    {
        if (reservedSeat != null)
        {
            reservedSeat.Release(this);
            reservedSeat = null;
            if (resumeNavigation && !agent.enabled)
            {
                if (!NavMesh.SamplePosition(seatNavPosition, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
                    throw new InvalidOperationException($"{name} cannot stand up: its seat approach is no longer walkable.");
                Vector3 visualPosition = visual != null ? visual.position : transform.position;
                transform.position = hit.position;
                agent.enabled = true;
                if (!agent.Warp(hit.position))
                    throw new InvalidOperationException($"{name} could not resume navigation after standing up.");
                if (visual != null) visual.position = visualPosition;
            }
        }
    }

    private void UpdateImpatienceColor()
    {
        if (bodyRenderer == null || patience == null || !patience.IsWaiting)
        {
            return;
        }

        float frustration = 1f - patience.Normalized;
        SetBodyColor(bodyRenderer is SpriteRenderer
            ? Color.Lerp(Color.white, new Color(1f, .8f, .75f), frustration * .35f)
            : Color.Lerp(baseColor, new Color(0.88f, 0.12f, 0.08f), frustration * 0.8f));
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

        ReleaseSeat(false);
        spawner?.NotifyDespawned(this);
    }
}
