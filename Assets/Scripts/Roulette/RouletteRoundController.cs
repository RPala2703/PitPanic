using System;
using System.Collections.Generic;
using UnityEngine;

public enum RouletteRoundPhase
{
    WaitingForPlayers,
    AcceptingBets,
    AwaitingAssistance,
    Spinning,
    SettlingBets,
    ClearingTable
}

public sealed class RouletteCustomerBet
{
    public long RoundId { get; }
    public int BetId { get; }
    public CasinoCustomerAI Customer { get; }
    public RouletteBet Bet { get; }
    public long GrossReturnPence { get; internal set; }
    public bool IsSettled { get; internal set; }
    public bool IsWinning { get; internal set; }
    public bool IsPaid { get; internal set; }
    public long PaidOutPence { get; internal set; }
    public bool IsSwept { get; internal set; }

    public RouletteCustomerBet(long roundId, int betId, CasinoCustomerAI customer, RouletteBet bet)
    {
        RoundId = roundId;
        BetId = betId;
        Customer = customer;
        Bet = bet;
    }
}

[DisallowMultipleComponent]
public sealed class RouletteRoundController : MonoBehaviour
{
    [SerializeField] private CasinoDealerStation dealerStation;
    [SerializeField] private CasinoSeat[] customerPositions;
    [SerializeField] private CasinoFloatRefillDestination floatDestination;
    [SerializeField] private CasinoMinigameStation assistanceStation;
    [SerializeField] private RouletteTableMinigame assistanceMinigame;
    [SerializeField] private Transform physicalWheel;
    [SerializeField] private Transform chipContainer;
    [SerializeField, Min(0.1f)] private float bettingSeconds = 3f;
    [SerializeField, Min(0.1f)] private float spinSeconds = 3f;
    [SerializeField, Min(0.1f)] private float settlementSeconds = 1.5f;
    [SerializeField, Min(0.1f)] private float clearingSeconds = 1.5f;

    private readonly List<RouletteCustomerBet> currentBets = new List<RouletteCustomerBet>();
    private readonly List<RouletteCustomerBet> previousBets = new List<RouletteCustomerBet>();
    private readonly List<RouletteCustomerBet> assistanceBets = new List<RouletteCustomerBet>();
    private readonly Queue<int> recentResults = new Queue<int>(5);
    private readonly List<GameObject> tableChips = new List<GameObject>();
    private readonly HashSet<CasinoCustomerAI> activeCustomers = new HashSet<CasinoCustomerAI>();
    private float phaseTimer;
    private float spinElapsed;
    private float wheelStartAngle;
    private float wheelExtraRotation;
    private int pendingWinningNumber = -1;
    private int latestWinningNumber = -1;
    private int assistanceWinningNumber = -1;
    private long nextRoundId;
    private long activeRoundId;
    private long assistanceRoundId;
    private bool roundSettled;
    private bool subscriptionsBound;

    public event Action<RouletteRoundController> AssistanceOpportunity;

    public RouletteRoundPhase Phase { get; private set; } = RouletteRoundPhase.WaitingForPlayers;
    public IReadOnlyList<RouletteCustomerBet> CurrentBets => currentBets;
    public IReadOnlyList<RouletteCustomerBet> PreviousBets => previousBets;
    public IReadOnlyList<RouletteCustomerBet> AssistanceBets => assistanceBets;
    public long AssistanceRoundId => assistanceRoundId;
    public int AssistanceWinningNumber => assistanceWinningNumber;
    public IReadOnlyList<int> RecentResults => recentResults.ToArray();
    public int RoulettePositionCount => customerPositions == null ? 0 : customerPositions.Length;
    public int ValidRoulettePositionCount
    {
        get
        {
            if (customerPositions == null)
            {
                return 0;
            }

            int count = 0;
            foreach (CasinoSeat seat in customerPositions)
            {
                if (seat != null && seat.GameType == CasinoCustomerGame.Roulette &&
                    seat.DealerStation == dealerStation)
                {
                    count++;
                }
            }

            return count;
        }
    }
    public int LatestWinningNumber => latestWinningNumber;
    public IReadOnlyList<CasinoCustomerAI> SeatedCustomers
    {
        get
        {
            List<CasinoCustomerAI> seated = new List<CasinoCustomerAI>(customerPositions != null ? customerPositions.Length : 0);
            if (customerPositions == null)
            {
                return seated;
            }

            foreach (CasinoSeat seat in customerPositions)
            {
                if (seat != null && seat.Occupant != null)
                {
                    seated.Add(seat.Occupant);
                }
            }

            return seated;
        }
    }
    public int ParticipantCount => activeCustomers.Count > 0 ? activeCustomers.Count : SeatedCustomers.Count;
    public bool IsDealerStaffed => dealerStation != null &&
                                  dealerStation.AssignedDealer != null &&
                                  dealerStation.AssignedDealer.isActiveAndEnabled &&
                                  dealerStation.AssignedDealer.AssignedStation == dealerStation &&
                                  dealerStation.AssignedDealer.IsAtStation;
    public bool HasActiveCustomersAndDealer => IsDealerStaffed && ParticipantCount > 0 &&
                                               Phase != RouletteRoundPhase.WaitingForPlayers;
    public bool IsConfigured => dealerStation != null && dealerStation.IsConfigured &&
                                customerPositions != null && customerPositions.Length == 4 &&
                                ValidRoulettePositionCount == 4 &&
                                floatDestination != null && floatDestination.IsConfigured &&
                                assistanceStation != null && assistanceStation.IsConfigured &&
                                assistanceMinigame != null && physicalWheel != null &&
                                chipContainer != null;
    public AIDealer AssignedDealer => dealerStation != null ? dealerStation.AssignedDealer : null;
    public bool HasPendingAssistance => assistanceStation != null && assistanceStation.IsTaskPending;
    public CasinoFloatRefillDestination FloatDestination => floatDestination;
    public string PhaseLabel => Phase switch
    {
        RouletteRoundPhase.WaitingForPlayers => IsDealerStaffed ? "WAITING FOR PLAYERS" : "UNSTAFFED",
        RouletteRoundPhase.AcceptingBets => "ACCEPTING BETS",
        RouletteRoundPhase.AwaitingAssistance => "AWAITING PIT BOSS SPIN",
        RouletteRoundPhase.Spinning => "SPINNING",
        RouletteRoundPhase.SettlingBets => "SETTLING BETS",
        RouletteRoundPhase.ClearingTable => "CLEARING TABLE",
        _ => "UNSTAFFED"
    };

    public void Configure(
        CasinoDealerStation station,
        CasinoSeat[] positions,
        CasinoFloatRefillDestination destination,
        CasinoMinigameStation taskStation,
        RouletteTableMinigame taskMinigame,
        Transform wheel,
        Transform chipRoot)
    {
        dealerStation = station;
        customerPositions = positions;
        floatDestination = destination;
        assistanceStation = taskStation;
        assistanceMinigame = taskMinigame;
        physicalWheel = wheel;
        chipContainer = chipRoot;
        assistanceMinigame.ConfigureRoundController(this);
        BindTaskEvents();
    }

    public bool TryPauseForAssistance()
    {
        if (Phase != RouletteRoundPhase.AcceptingBets || currentBets.Count == 0 ||
            !IsDealerStaffed || !HasActiveCustomersAndDealer ||
            assistanceStation == null || !assistanceStation.IsTaskPending)
        {
            return false;
        }

        Phase = RouletteRoundPhase.AwaitingAssistance;
        assistanceRoundId = activeRoundId;
        assistanceBets.Clear();
        assistanceBets.AddRange(currentBets);
        assistanceWinningNumber = -1;
        return true;
    }

    public bool TrySetAssistanceWinningNumber(long roundId, int number)
    {
        if (!CanSetAssistanceWinningNumber(
                Phase == RouletteRoundPhase.AwaitingAssistance,
                assistanceRoundId,
                roundId,
                assistanceWinningNumber,
                number))
        {
            return false;
        }

        if (assistanceWinningNumber < 0)
        {
            assistanceWinningNumber = number;
        }

        foreach (RouletteCustomerBet customerBet in assistanceBets)
        {
            customerBet.IsWinning = EuropeanRouletteRules.Wins(customerBet.Bet, assistanceWinningNumber);
            if (!EuropeanRouletteRules.TryGetGrossReturnPence(
                    customerBet.Bet, assistanceWinningNumber, out long grossReturn))
            {
                throw new InvalidOperationException("A valid roulette assistance bet could not be evaluated.");
            }

            customerBet.GrossReturnPence = grossReturn;
        }

        return true;
    }

    public static bool CanSetAssistanceWinningNumber(
        bool awaitingAssistance,
        long currentRoundId,
        long requestedRoundId,
        int existingNumber,
        int requestedNumber) =>
        awaitingAssistance && currentRoundId > 0 && currentRoundId == requestedRoundId &&
        EuropeanRouletteRules.IsValidNumber(requestedNumber) &&
        (existingNumber < 0 || existingNumber == requestedNumber);

    public bool TryMarkAssistanceBetSwept(long roundId, int betId)
    {
        RouletteCustomerBet customerBet = FindAssistanceBet(roundId, betId);
        if (customerBet == null || customerBet.IsWinning || customerBet.IsSwept)
        {
            return false;
        }

        customerBet.IsSwept = true;
        return true;
    }

    public bool TryRecordAssistancePayout(
        long roundId,
        int betId,
        int amountPence,
        int[] chipCounts)
    {
        RouletteCustomerBet customerBet = FindAssistanceBet(roundId, betId);
        if (customerBet == null || !CanRecordAssistancePayout(
                customerBet.IsWinning,
                customerBet.IsPaid,
                customerBet.GrossReturnPence,
                amountPence) ||
            chipCounts == null || chipCounts.Length != 4 ||
            !PayoutChipsMatchAmount(chipCounts, amountPence) ||
            floatDestination == null || !floatDestination.TryConsumeChips(chipCounts))
        {
            return false;
        }

        customerBet.PaidOutPence = amountPence;
        customerBet.IsPaid = true;
        return true;
    }

    public static bool PayoutChipsMatchAmount(int[] chipCounts, int amountPence)
    {
        if (chipCounts == null || chipCounts.Length != 4 || amountPence <= 0)
        {
            return false;
        }

        long total = 0;
        int[] denominationValues = { 500, 250, 2500, 10000 };
        for (int i = 0; i < chipCounts.Length; i++)
        {
            if (chipCounts[i] < 0)
            {
                return false;
            }

            total += (long)chipCounts[i] * denominationValues[i];
        }

        return total == amountPence;
    }

    public static bool CanRecordAssistancePayout(
        bool isWinningBet,
        bool alreadyPaid,
        long grossReturnPence,
        int amountPence) =>
        isWinningBet && !alreadyPaid && grossReturnPence > 0 &&
        grossReturnPence == amountPence;

    private RouletteCustomerBet FindAssistanceBet(long roundId, int betId)
    {
        if (Phase != RouletteRoundPhase.AwaitingAssistance || roundId != assistanceRoundId)
        {
            return null;
        }

        foreach (RouletteCustomerBet customerBet in assistanceBets)
        {
            if (customerBet.RoundId == roundId && customerBet.BetId == betId)
            {
                return customerBet;
            }
        }

        return null;
    }

    public bool CanAcceptAssistanceResult(int number) =>
        Phase == RouletteRoundPhase.AwaitingAssistance &&
        CanSettleRound(roundSettled, currentBets.Count, number);

    public static bool CanSettleRound(bool alreadySettled, int betCount, int winningNumber) =>
        !alreadySettled && betCount > 0 && EuropeanRouletteRules.IsValidNumber(winningNumber);

    public void RefreshAssistanceStatus()
    {
        if (Phase == RouletteRoundPhase.AwaitingAssistance &&
            (assistanceStation == null || !assistanceStation.IsTaskPending))
        {
            BeginAutomaticSpin();
        }
    }

    private void Start()
    {
        if (!IsConfigured)
        {
            Debug.LogError($"{nameof(RouletteRoundController)} on {name} needs four customer positions, a staffed station, float destination, and roulette minigame.", this);
            enabled = false;
            return;
        }

        BindTaskEvents();
    }

    private void OnEnable()
    {
        BindTaskEvents();
    }

    private void OnDisable()
    {
        UnbindTaskEvents();
    }

    private void Update()
    {
        RefreshAssistanceStatus();
        if (!IsDealerStaffed)
        {
            return;
        }

        switch (Phase)
        {
            case RouletteRoundPhase.WaitingForPlayers:
                TryBeginRound();
                break;
            case RouletteRoundPhase.AcceptingBets:
                phaseTimer -= Time.deltaTime;
                if (phaseTimer <= 0f)
                {
                    AssistanceOpportunity?.Invoke(this);
                    if (Phase == RouletteRoundPhase.AcceptingBets)
                    {
                        BeginAutomaticSpin();
                    }
                }
                break;
            case RouletteRoundPhase.Spinning:
                UpdateWheelSpin();
                break;
            case RouletteRoundPhase.SettlingBets:
            case RouletteRoundPhase.ClearingTable:
                phaseTimer -= Time.deltaTime;
                if (phaseTimer <= 0f)
                {
                    if (Phase == RouletteRoundPhase.SettlingBets)
                    {
                        FinishRound();
                    }
                    else
                    {
                        Phase = RouletteRoundPhase.WaitingForPlayers;
                    }
                }
                break;
        }
    }

    private void TryBeginRound()
    {
        List<CasinoCustomerAI> participants = new List<CasinoCustomerAI>(customerPositions.Length);
        for (int i = 0; i < customerPositions.Length; i++)
        {
            CasinoSeat seat = customerPositions[i];
            CasinoCustomerAI customer = seat != null ? seat.Occupant : null;
            if (customer != null && customer.IsSeated && !customer.IsInService &&
                customer.BeginService())
            {
                participants.Add(customer);
            }
        }

        if (participants.Count == 0)
        {
            return;
        }

        ClearChips();
        currentBets.Clear();
        activeCustomers.Clear();
        activeCustomers.UnionWith(participants);
        activeRoundId = ++nextRoundId;
        assistanceRoundId = 0;
        assistanceWinningNumber = -1;
        roundSettled = false;
        pendingWinningNumber = -1;
        for (int i = 0; i < participants.Count; i++)
        {
            CasinoCustomerAI customer = participants[i];
            RouletteBet bet = CreateCustomerBet();
            RouletteCustomerBet customerBet = new RouletteCustomerBet(activeRoundId, i, customer, bet);
            currentBets.Add(customerBet);
            CreateVisibleBetChips(customerBet);
        }

        Phase = RouletteRoundPhase.AcceptingBets;
        phaseTimer = bettingSeconds;
    }

    private static RouletteBet CreateCustomerBet()
    {
        RouletteBetType type = (RouletteBetType)UnityEngine.Random.Range(
            0, Enum.GetValues(typeof(RouletteBetType)).Length);
        int number = type == RouletteBetType.StraightUp
            ? UnityEngine.Random.Range(0, 37)
            : -1;
        int[] stakes = { 500, 1000, 2500 };
        int stake = stakes[UnityEngine.Random.Range(0, stakes.Length)];
        return new RouletteBet(type, number, stake);
    }

    public static string GetBetLabel(RouletteBet bet) => bet.Type switch
    {
        RouletteBetType.StraightUp => $"NUMBER {bet.Number}",
        RouletteBetType.Red => "RED",
        RouletteBetType.Black => "BLACK",
        RouletteBetType.Odd => "ODD",
        RouletteBetType.Even => "EVEN",
        _ => "INVALID"
    };

    public static string FormatPounds(long pence) =>
        $"£{pence / 100}.{Math.Abs(pence % 100):00}";

    private void CreateVisibleBetChips(RouletteCustomerBet customerBet)
    {
        CasinoSeat seat = customerBet.Customer != null ? customerBet.Customer.Seat : null;
        if (seat == null || seat.BettingPosition == null)
        {
            return;
        }

        Vector3 localPosition = transform.InverseTransformPoint(seat.BettingPosition.position);
        Color color = customerBet.Bet.Type switch
        {
            RouletteBetType.Red => new Color(0.84f, 0.13f, 0.10f),
            RouletteBetType.Black => new Color(0.10f, 0.11f, 0.13f),
            RouletteBetType.Odd => new Color(0.12f, 0.44f, 0.85f),
            RouletteBetType.Even => new Color(0.14f, 0.61f, 0.32f),
            _ => new Color(0.87f, 0.70f, 0.20f)
        };

        int chipCount = Mathf.Clamp(customerBet.Bet.StakePence / 500, 1, 5);
        for (int i = 0; i < chipCount; i++)
        {
            GameObject chip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            chip.name = $"Roulette Bet Chip {customerBet.Customer.name} {i + 1}";
            chip.transform.SetParent(chipContainer, false);
            chip.transform.localPosition = localPosition + new Vector3((i % 3 - 1) * 0.12f, 0.055f + (i / 3) * 0.05f, (i / 3) * 0.1f);
            chip.transform.localScale = new Vector3(0.18f, 0.045f, 0.18f);
            Collider collider = chip.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            Renderer renderer = chip.GetComponent<Renderer>();
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
            tableChips.Add(chip);
        }
    }

    private void BeginAutomaticSpin()
    {
        Phase = RouletteRoundPhase.Spinning;
        pendingWinningNumber = UnityEngine.Random.Range(0, 37);
        spinElapsed = 0f;
        wheelStartAngle = physicalWheel.localEulerAngles.y;
        wheelExtraRotation = UnityEngine.Random.Range(900f, 1500f);
    }

    private void UpdateWheelSpin()
    {
        spinElapsed = Mathf.Min(spinSeconds, spinElapsed + Time.deltaTime);
        float progress = Mathf.Clamp01(spinElapsed / Mathf.Max(0.1f, spinSeconds));
        float eased = 1f - Mathf.Pow(1f - progress, 3f);
        Vector3 angles = physicalWheel.localEulerAngles;
        angles.y = wheelStartAngle + wheelExtraRotation * eased;
        physicalWheel.localEulerAngles = angles;
        if (spinElapsed >= spinSeconds)
        {
            SettleRound(pendingWinningNumber);
        }
    }

    private void OnAssistanceSucceeded(CasinoMinigameResult result)
    {
        if (result.Success && assistanceMinigame != null &&
            CanAcceptAssistanceResult(assistanceMinigame.WinningNumber))
        {
            SettleRound(assistanceMinigame.WinningNumber);
        }
    }

    private void SettleRound(int winningNumber)
    {
        if (!CanSettleRound(roundSettled, currentBets.Count, winningNumber))
        {
            return;
        }

        roundSettled = true;
        pendingWinningNumber = winningNumber;
        latestWinningNumber = winningNumber;
        recentResults.Enqueue(winningNumber);
        while (recentResults.Count > 5)
        {
            recentResults.Dequeue();
        }

        foreach (RouletteCustomerBet customerBet in currentBets)
        {
            if (!EuropeanRouletteRules.TryGetGrossReturnPence(customerBet.Bet, winningNumber, out long grossReturn))
            {
                throw new InvalidOperationException("A valid roulette bet could not be settled.");
            }

            customerBet.GrossReturnPence = grossReturn;
            customerBet.IsWinning = EuropeanRouletteRules.Wins(customerBet.Bet, winningNumber);
            customerBet.IsSettled = true;
        }

        Phase = RouletteRoundPhase.SettlingBets;
        phaseTimer = settlementSeconds;
    }

    private void FinishRound()
    {
        previousBets.Clear();
        previousBets.AddRange(currentBets);
        foreach (CasinoCustomerAI customer in activeCustomers)
        {
            if (customer != null && customer.IsInService)
            {
                customer.CompleteRouletteRound();
            }
        }

        activeCustomers.Clear();
        currentBets.Clear();
        assistanceBets.Clear();
        assistanceRoundId = 0;
        assistanceWinningNumber = -1;
        Phase = RouletteRoundPhase.ClearingTable;
        phaseTimer = clearingSeconds;
    }

    private void ClearChips()
    {
        foreach (GameObject chip in tableChips)
        {
            if (chip != null)
            {
                Destroy(chip);
            }
        }

        tableChips.Clear();
    }

    private void BindTaskEvents()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || assistanceStation == null || subscriptionsBound)
        {
            return;
        }

        assistanceStation.TaskSucceeded += OnAssistanceSucceeded;
        subscriptionsBound = true;
    }

    private void UnbindTaskEvents()
    {
        if (!subscriptionsBound || assistanceStation == null)
        {
            return;
        }

        assistanceStation.TaskSucceeded -= OnAssistanceSucceeded;
        subscriptionsBound = false;
    }

    private void OnDestroy()
    {
        ClearChips();
        foreach (CasinoCustomerAI customer in activeCustomers)
        {
            if (customer != null)
            {
                customer.ResumeWaiting();
            }
        }
    }
}
