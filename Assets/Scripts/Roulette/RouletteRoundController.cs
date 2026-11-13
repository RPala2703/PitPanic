using System;
using System.Linq;
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
    public long WinningsPence { get; internal set; }
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
    private readonly Dictionary<RouletteCustomerBet, List<GameObject>> betChipVisuals = new Dictionary<RouletteCustomerBet, List<GameObject>>();
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
    private bool tutorialRoundRequested;
    private bool tutorialRound;
    private Transform visibleBall;

    public bool IsTutorialRound => tutorialRound;
    public void PrepareTutorialRound() => tutorialRoundRequested = true;
    public void CancelTutorialRound() => tutorialRoundRequested = false;

    public event Action<RouletteRoundController> AssistanceOpportunity;
    public event Action<RouletteCustomerBet> BetSettled;
    public event Action<CasinoSecurityEvidence> SecurityEvidenceRecorded;

    public RouletteRoundPhase Phase { get; private set; } = RouletteRoundPhase.WaitingForPlayers;
    public IReadOnlyList<RouletteCustomerBet> CurrentBets => currentBets;
    public IReadOnlyList<RouletteCustomerBet> PreviousBets => previousBets;
    public IReadOnlyList<RouletteCustomerBet> AssistanceBets => assistanceBets;
    public long AssistanceRoundId => assistanceRoundId;
    public int AssistanceWinningNumber => assistanceWinningNumber;
    public bool IsCoverAssistance => HasPendingAssistance &&
        assistanceStation.MinigameType == CasinoMinigameType.Cover && !IsPlayerOperating;
    public bool IsCoverReady => IsCoverAssistance && IsDealerStaffed &&
        Phase == RouletteRoundPhase.AwaitingAssistance && assistanceWinningNumber >= 0;
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
                                  dealerStation.AssignedDealer.CanServe;
    public bool IsPlayerOperating { get; private set; }
    private CasinoCharacterVisual OperatorVisual => IsPlayerOperating
        ? FindFirstObjectByType<CasinoPlayerController>()?.GetComponentInChildren<CasinoCharacterVisual>()
        : AssignedDealer?.GetComponentInChildren<CasinoCharacterVisual>();
    public bool IsServiceAvailable => IsDealerStaffed || IsPlayerOperating;
    public bool BeginPlayerTakeover()
    {
        RouletteInspectionView view = GetComponent<RouletteInspectionView>();
        if (Time.timeScale == 0 || IsDealerStaffed || view == null || !view.IsOpen) return false;
        IsPlayerOperating = true;
        if (Phase == RouletteRoundPhase.Spinning)
        {
            Phase = RouletteRoundPhase.AwaitingAssistance;
            assistanceRoundId = activeRoundId;
            assistanceBets.Clear();
            assistanceBets.AddRange(currentBets);
            assistanceWinningNumber = -1;
            TrySetAssistanceWinningNumber(activeRoundId, pendingWinningNumber);
        }
        return true;
    }
    public void EndPlayerTakeover() => IsPlayerOperating = false;
    public bool RunManualSpin()
    {
        if (!IsPlayerOperating || Time.timeScale == 0) return false;
        if (Phase == RouletteRoundPhase.WaitingForPlayers) TryBeginRound();
        if (Phase != RouletteRoundPhase.AcceptingBets && Phase != RouletteRoundPhase.AwaitingAssistance) return false;
        if (HasPendingAssistance && assistanceStation.MinigameType == CasinoMinigameType.Cover)
            assistanceStation.ResolveTaskAfterFatalFailure();
        if (!HasPendingAssistance)
            assistanceStation.RequestMinigame(CasinoMinigameType.Spin, TableAttentionReason.RouletteSpinRequired);
        if (Phase == RouletteRoundPhase.AcceptingBets && !TryPauseForAssistance()) return false;
        return HasPendingAssistance;
    }
    public bool HasActiveCustomersAndDealer => IsServiceAvailable && ParticipantCount > 0 &&
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
    public string PhaseLabel => !IsPlayerOperating && AssignedDealer != null && AssignedDealer.IsAtStation && !AssignedDealer.CanServe
        ? "EXHAUSTED! ASSIGN COVER" : Phase switch
    {
        RouletteRoundPhase.WaitingForPlayers => IsServiceAvailable ? "WAITING FOR PLAYERS" : "UNSTAFFED",
        RouletteRoundPhase.AcceptingBets => "ACCEPTING BETS",
        RouletteRoundPhase.AwaitingAssistance => IsCoverAssistance ? "COVER! MARK WINNING BETS" : "AWAITING PIT BOSS TAKEOVER",
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
        bool cover = assistanceStation != null && assistanceStation.MinigameType == CasinoMinigameType.Cover;
        if ((cover ? Phase != RouletteRoundPhase.Spinning || spinElapsed < spinSeconds ||
                     pendingWinningNumber < 0 || !IsDealerStaffed || IsPlayerOperating
                   : Phase != RouletteRoundPhase.AcceptingBets || !IsPlayerOperating) || currentBets.Count == 0 ||
            !IsServiceAvailable || !HasActiveCustomersAndDealer ||
            assistanceStation == null || !assistanceStation.IsTaskPending)
        {
            return false;
        }

        Phase = RouletteRoundPhase.AwaitingAssistance;
        assistanceRoundId = activeRoundId;
        assistanceBets.Clear();
        assistanceBets.AddRange(currentBets);
        assistanceWinningNumber = -1;
        if (cover) TrySetAssistanceWinningNumber(assistanceRoundId, pendingWinningNumber);
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
            if (!EuropeanRouletteRules.TryGetWinningsPence(
                    customerBet.Bet, assistanceWinningNumber, out long winnings))
            {
                throw new InvalidOperationException("A valid roulette assistance bet could not be evaluated.");
            }

            customerBet.WinningsPence = winnings;
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
        AnimateSettlement(customerBet, false);
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
                customerBet.WinningsPence,
                amountPence) ||
            chipCounts == null || chipCounts.Length != 4 ||
            !PayoutChipsMatchAmount(chipCounts, amountPence) ||
            floatDestination == null || !floatDestination.TryConsumeChips(chipCounts))
        {
            return false;
        }

        customerBet.PaidOutPence = amountPence;
        customerBet.IsPaid = true;
        AnimateSettlement(customerBet, true);
        CasinoSoundscape.Play(CasinoSoundCue.Chip, transform.position);
        if (customerBet.Customer != null) customerBet.Customer.GetComponentInChildren<PixelCharacterVisual>()?.React(true);
        OperatorVisual?.PlayGesture(
            CasinoGesture.Pay, customerBet.Customer != null ? customerBet.Customer.transform.position : transform.position);
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
        long winningsPence,
        int amountPence) =>
        isWinningBet && !alreadyPaid && winningsPence > 0 &&
        winningsPence == amountPence;

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
        number == assistanceWinningNumber &&
        CanSettleRound(roundSettled, currentBets.Count, number);

    public static bool CanSettleRound(bool alreadySettled, int betCount, int winningNumber) =>
        !alreadySettled && betCount > 0 && EuropeanRouletteRules.IsValidNumber(winningNumber);

    public void RefreshAssistanceStatus()
    {
        if (IsCoverAssistance && !IsDealerStaffed && !assistanceStation.IsMinigameRunning)
            assistanceStation.ResolveTaskAfterFatalFailure();
        if (Phase == RouletteRoundPhase.AwaitingAssistance && assistanceStation != null &&
            assistanceStation.IsTaskPending && !assistanceStation.IsMinigameRunning &&
            !activeCustomers.Any(customer => customer != null && customer.IsSeated))
        {
            assistanceStation.ResolveTaskAfterFatalFailure();
            if (assistanceWinningNumber >= 0) { SettleRound(assistanceWinningNumber); return; }
        }
        if (Phase == RouletteRoundPhase.AwaitingAssistance &&
            (assistanceStation == null || !assistanceStation.IsTaskPending))
        {
            if (!IsDealerStaffed || IsPlayerOperating) return;
            if (assistanceWinningNumber >= 0) SettleRound(assistanceWinningNumber);
            else BeginAutomaticSpin();
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
        if (Time.timeScale == 0) return;
        if (IsPlayerOperating && !GetComponent<RouletteInspectionView>().IsOpen && !assistanceStation.IsMinigameRunning &&
            Phase != RouletteRoundPhase.SettlingBets && Phase != RouletteRoundPhase.ClearingTable)
            IsPlayerOperating = false;
        if (IsPlayerOperating && IsDealerStaffed) IsPlayerOperating = false;
        RefreshAssistanceStatus();
        if (!IsServiceAvailable)
        {
            return;
        }

        switch (Phase)
        {
            case RouletteRoundPhase.WaitingForPlayers:
                TryBeginRound();
                break;
            case RouletteRoundPhase.AcceptingBets:
                if (IsPlayerOperating) break;
                phaseTimer -= Time.deltaTime;
                if (phaseTimer <= 0f)
                {
                    BeginAutomaticSpin();
                }
                break;
            case RouletteRoundPhase.Spinning:
                UpdateWheelSpin();
                break;
            case RouletteRoundPhase.SettlingBets:
            case RouletteRoundPhase.ClearingTable:
                if (Phase == RouletteRoundPhase.SettlingBets && !TrySettleAutomaticPayouts())
                    return;
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
        if (tutorialRoundRequested)
        {
            int ready = 0;
            foreach (CasinoSeat seat in customerPositions)
                if (seat != null && seat.Occupant != null && seat.Occupant.IsSeated &&
                    !seat.Occupant.IsInService) ready++;
            if (ready < 2) return;
        }
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
        tutorialRound = tutorialRoundRequested;
        tutorialRoundRequested = false;
        pendingWinningNumber = -1;
        for (int i = 0; i < participants.Count; i++)
        {
            CasinoCustomerAI customer = participants[i];
            RouletteBet bet = tutorialRound
                ? new RouletteBet(i % 2 == 0 ? RouletteBetType.Red : RouletteBetType.Black, -1, 500)
                : CreateCustomerBet(customer.IsVip);
            RouletteCustomerBet customerBet = new RouletteCustomerBet(activeRoundId, i, customer, bet);
            currentBets.Add(customerBet);
            customer.GetComponentInChildren<CasinoCharacterVisual>()?.PlayGesture(CasinoGesture.Pay, transform.position);
            CreateVisibleBetChips(customerBet);
        }

        Phase = RouletteRoundPhase.AcceptingBets;
        phaseTimer = bettingSeconds;
    }

    private RouletteBet CreateCustomerBet(bool highRoller)
    {
        RouletteBetType[] types = activeRoundId <= 3
            ? new[] { RouletteBetType.StraightUp, RouletteBetType.Red, RouletteBetType.Black, RouletteBetType.Odd, RouletteBetType.Even }
            : activeRoundId <= 6
                ? new[] { RouletteBetType.StraightUp, RouletteBetType.Red, RouletteBetType.Black, RouletteBetType.Split, RouletteBetType.Street }
                : (RouletteBetType[])Enum.GetValues(typeof(RouletteBetType));
        RouletteBetType type = types[UnityEngine.Random.Range(0, types.Length)];
        int row = UnityEngine.Random.Range(0, 12);
        int column = UnityEngine.Random.Range(0, 3);
        int number = row * 3 + column + 1;
        int[] numbers = null;
        switch (type)
        {
            case RouletteBetType.Split:
                numbers = column < 2 ? new[] { number, number + 1 } :
                    row < 11 ? new[] { number, number + 3 } : new[] { number - 3, number };
                break;
            case RouletteBetType.Street:
                number = row * 3 + 1;
                numbers = new[] { number, number + 1, number + 2 };
                break;
            case RouletteBetType.Corner:
                row = UnityEngine.Random.Range(0, 11);
                column = UnityEngine.Random.Range(0, 2);
                number = row * 3 + column + 1;
                numbers = new[] { number, number + 1, number + 3, number + 4 };
                break;
            case RouletteBetType.SixLine:
                row = UnityEngine.Random.Range(0, 11);
                number = row * 3 + 1;
                numbers = new[] { number, number + 1, number + 2, number + 3, number + 4, number + 5 };
                break;
        }
        int[] stakes = highRoller
            ? new[] { 2500, 5000, 10000 }
            : new[] { 500, 1000, 2500 };
        int stake = stakes[UnityEngine.Random.Range(0, stakes.Length)];
        return type switch
        {
            RouletteBetType.StraightUp => new RouletteBet(type, UnityEngine.Random.Range(0, 37), stake),
            RouletteBetType.Split or RouletteBetType.Street or RouletteBetType.Corner or RouletteBetType.SixLine =>
                new RouletteBet(type, numbers, stake),
            _ => new RouletteBet(type, -1, stake)
        };
    }

    public static string GetBetLabel(RouletteBet bet) => bet.Type switch
    {
        RouletteBetType.StraightUp => $"NUMBER {bet.Number}",
        RouletteBetType.Split => $"SPLIT {string.Join("/", bet.Numbers)}",
        RouletteBetType.Street => $"STREET {string.Join("/", bet.Numbers)}",
        RouletteBetType.Corner => $"CORNER {string.Join("/", bet.Numbers)}",
        RouletteBetType.SixLine => $"SIX LINE {string.Join("/", bet.Numbers)}",
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
        List<BlackjackChipDenomination> composition = BlackjackChipDenomination.GetExactComposition(
            customerBet.Bet.StakePence, BlackjackChipDenomination.CreateDefaults());
        int chipCount = composition.Count;
        List<GameObject> visuals = new List<GameObject>();
        betChipVisuals[customerBet] = visuals;
        for (int i = 0; i < chipCount; i++)
        {
            GameObject chip = new GameObject($"Roulette Bet Chip {customerBet.Customer.name} {i + 1}");
            chip.transform.SetParent(chipContainer, false);
            chip.transform.localPosition = localPosition + new Vector3((i % 3 - 1) * 0.12f, 0.055f + (i / 3) * 0.05f, (i / 3) * 0.1f);
            SpriteRenderer renderer = PixelWorldProps.CreateSurface(chip.transform, "Pixel Roulette Chip",
                CasinoPixelArt.ChipForValue(composition[i].ValuePence), new Vector2(0.18f, 0.18f), -7);
            renderer.color = Color.white;
            chip.AddComponent<CasinoPropFlight>().Configure(
                transform.InverseTransformPoint(customerBet.Customer.transform.position) + Vector3.up,
                chip.transform.localPosition, .5f + i * .06f, CasinoSoundCue.Chip);
            tableChips.Add(chip);
            visuals.Add(chip);
        }
    }

    private bool TryAddWagerToFloat(int amountPence)
    {
        if (floatDestination == null) return false;
        List<BlackjackChipDenomination> chips = BlackjackChipDenomination.GetExactComposition(
            amountPence, BlackjackChipDenomination.CreateDefaults());
        int[] counts = new int[4];
        foreach (BlackjackChipDenomination chip in chips)
        {
            int index = chip.ValuePence switch { 500 => 0, 250 => 1, 2500 => 2, 10000 => 3, _ => -1 };
            if (index >= 0) counts[index]++;
        }
        return floatDestination.TryAddChips(counts);
    }

    private void BeginAutomaticSpin()
    {
        CasinoSoundscape.Play(CasinoSoundCue.Roulette, transform.position);
        Phase = RouletteRoundPhase.Spinning;
        pendingWinningNumber = UnityEngine.Random.Range(0, 37);
        spinElapsed = 0f;
        wheelStartAngle = physicalWheel.localEulerAngles.y;
        wheelExtraRotation = UnityEngine.Random.Range(900f, 1500f);
        // Everyone leans in to follow the ball, each at their own moment.
        Vector3 wheel = physicalWheel != null ? physicalWheel.position : transform.position;
        int watcher = 0;
        foreach (CasinoCustomerAI customer in activeCustomers)
            CasinoPerformanceDirector.ActLater(customer, CasinoAnimationPose.Anticipate, wheel,
                Mathf.Max(.8f, spinSeconds * .85f), .15f + watcher++ * .18f);
    }

    private Component OperatorCharacter => IsPlayerOperating
        ? FindFirstObjectByType<CasinoPlayerController>()
        : (Component)AssignedDealer;

    private void AnimateSettlement(RouletteCustomerBet bet, bool paid)
    {
        if (!paid)
        {
            if (!betChipVisuals.TryGetValue(bet, out List<GameObject> chips)) return;
            foreach (GameObject chip in chips)
            {
                if (chip == null) continue;
                CasinoPropFlight prior = chip.GetComponent<CasinoPropFlight>();
                if (prior != null) Destroy(prior);
                chip.AddComponent<CasinoPropFlight>().Configure(chip.transform.localPosition,
                    new Vector3(-1.5f, 1.3f, .7f), .6f, CasinoSoundCue.Chip);
            }
            return;
        }
        if (bet.Customer == null) return;
        Vector3 target = transform.InverseTransformPoint(bet.Customer.transform.position) + Vector3.up;
        int count = Mathf.Clamp((int)(bet.WinningsPence / 500), 1, 6);
        for (int i = 0; i < count; i++)
        {
            GameObject chip = new GameObject("Roulette Winner Chip");
            chip.transform.SetParent(chipContainer, false);
            PixelWorldProps.CreateSurface(chip.transform, "Payout", CasinoPixelArt.ChipForAmount((int)bet.WinningsPence), new Vector2(.22f, .22f), -5);
            chip.AddComponent<CasinoPropFlight>().Configure(new Vector3(-1.5f, 1.3f, .7f),
                target + Vector3.up * i * .03f, .65f + i * .06f, CasinoSoundCue.Chip);
            tableChips.Add(chip);
        }
    }

    private void UpdateWheelSpin()
    {
        spinElapsed = Mathf.Min(spinSeconds, spinElapsed + Time.deltaTime);
        float progress = Mathf.Clamp01(spinElapsed / Mathf.Max(0.1f, spinSeconds));
        float eased = 1f - Mathf.Pow(1f - progress, 3f);
        Vector3 angles = physicalWheel.localEulerAngles;
        angles.y = wheelStartAngle + wheelExtraRotation * eased;
        physicalWheel.localEulerAngles = angles;
        if (visibleBall == null)
        {
            visibleBall = new GameObject("Roulette Moving Ball").transform;
            visibleBall.SetParent(physicalWheel, false);
            PixelWorldProps.CreateSurface(visibleBall, "Pixel Ball", CasinoPixelArt.Ball,
                new Vector2(.12f, .12f), -6);
        }
        float ballAngle = -eased * Mathf.PI * 16;
        visibleBall.localPosition = new Vector3(Mathf.Cos(ballAngle) * .6f, .32f, Mathf.Sin(ballAngle) * .6f);
        if (spinElapsed >= spinSeconds)
        {
            float resultAngle = ((Array.IndexOf(CasinoPixelArt.WheelOrder, pendingWinningNumber) + .5f) / 37f * 2 - 1) * Mathf.PI;
            visibleBall.localPosition = new Vector3(Mathf.Cos(resultAngle) * .6f, .32f, Mathf.Sin(resultAngle) * .6f);
            AssistanceOpportunity?.Invoke(this);
            if (Phase == RouletteRoundPhase.Spinning) SettleRound(pendingWinningNumber);
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
        if (visibleBall != null)
        {
            float angle = ((Array.IndexOf(CasinoPixelArt.WheelOrder, winningNumber) + .5f) / 37f * 2 - 1) * Mathf.PI;
            visibleBall.localPosition = new Vector3(Mathf.Cos(angle) * .6f, .32f, Mathf.Sin(angle) * .6f);
        }
        pendingWinningNumber = winningNumber;
        latestWinningNumber = winningNumber;
        recentResults.Enqueue(winningNumber);
        while (recentResults.Count > 5)
        {
            recentResults.Dequeue();
        }

        // Dramatic beat: the dealer points out the winning number before the table erupts.
        Component croupier = OperatorCharacter;
        Vector3 wheelPosition = physicalWheel != null ? physicalWheel.position : transform.position;
        croupier?.GetComponentInChildren<PixelCharacterVisual>()?.PlayAction(CasinoAnimationPose.Present, wheelPosition, .9f);
        int reactionOrder = 0;
        foreach (RouletteCustomerBet customerBet in currentBets)
        {
            if (!EuropeanRouletteRules.TryGetWinningsPence(customerBet.Bet, winningNumber, out long winnings))
            {
                throw new InvalidOperationException("A valid roulette bet could not be settled.");
            }

            customerBet.WinningsPence = winnings;
            customerBet.IsWinning = EuropeanRouletteRules.Wins(customerBet.Bet, winningNumber);
            customerBet.IsSettled = true;
            // Winning stakes stay with their owners; only losing wagers enter the float.
            if (!customerBet.IsWinning && !TryAddWagerToFloat(customerBet.Bet.StakePence))
                throw new InvalidOperationException("Could not collect a losing Roulette wager.");
            if (customerBet.Customer != null)
            {
                CasinoCustomerAI customer = customerBet.Customer;
                customer.RecordGamingResult(customerBet.IsWinning
                    ? (int)customerBet.WinningsPence : -customerBet.Bet.StakePence);
                CasinoPerformanceDirector.ReactLater(customer, customerBet.IsWinning, .6f + reactionOrder * .28f);
                if (customerBet.IsWinning && customerBet.Bet.Type == RouletteBetType.StraightUp)
                    CasinoPerformanceDirector.CrowdNotices(customer);
                if (croupier != null && CasinoTipping.TryTip(customer,
                        $"rl:{GetInstanceID()}:{customerBet.RoundId}:{customerBet.BetId}", customerBet.IsWinning,
                        IsPlayerOperating, out CasinoTipRecord tip))
                {
                    RouletteCustomerBet tipped = customerBet;
                    float start = Time.time + 1.6f + reactionOrder * .45f;
                    CasinoPerformanceDirector.PlayTip(tip, croupier,
                        () => Time.time >= start && (!tipped.IsWinning || tipped.IsPaid), 7f);
                }
                reactionOrder++;
            }
            OperatorVisual?.PlayGesture(
                customerBet.IsWinning ? CasinoGesture.Pay : CasinoGesture.Collect,
                customerBet.Customer != null ? customerBet.Customer.transform.position : transform.position);
            BetSettled?.Invoke(customerBet);
            if (!customerBet.IsWinning && !customerBet.IsSwept)
            {
                AnimateSettlement(customerBet, false);
                customerBet.IsSwept = true;
            }
        }

        RecordSecurityEvidence(winningNumber);
        RequestUnpaidPayoutRefill();
        Phase = RouletteRoundPhase.SettlingBets;
        phaseTimer = settlementSeconds;
    }

    private void RecordSecurityEvidence(int winningNumber)
    {
        CasinoShiftController shift = CasinoShiftController.Active;
        if (shift == null || shift.CurrentShift != 3 ||
            !CasinoSecurityEvidence.TryCreate(activeRoundId, winningNumber, currentBets,
                out CasinoSecurityEvidence evidence))
            return;

        CasinoCustomerAI customer = evidence.Bet.Customer;
        if (evidence.IsCheating)
        {
            if (betChipVisuals.TryGetValue(evidence.Bet, out List<GameObject> chips) &&
                chips.Count > 0 && chips[0] != null)
            {
                GameObject chip = chips[0];
                CasinoPropFlight oldFlight = chip.GetComponent<CasinoPropFlight>();
                if (oldFlight != null) Destroy(oldFlight);
                int column = winningNumber == 0 ? 1 : (winningNumber - 1) % 3;
                int row = winningNumber == 0 ? 11 : (winningNumber - 1) / 3;
                Vector3 target = winningNumber == 0
                    ? new Vector3(0f, 1.34f, 1.12f)
                    : new Vector3((column - 1) * .28f, 1.34f, (.5f - row) * .14f);
                chip.AddComponent<CasinoPropFlight>().Configure(chip.transform.localPosition,
                    target, .72f, CasinoSoundCue.Chip);
            }
        }
        else
        {
            customer?.GetComponentInChildren<CasinoCharacterVisual>()?.PlayGesture(
                CasinoGesture.Pay, customer.transform.position + transform.forward, .8f);
        }
        SecurityEvidenceRecorded?.Invoke(evidence);
    }

    private bool TrySettleAutomaticPayouts()
    {
        foreach (RouletteCustomerBet bet in currentBets)
        {
            if (!bet.IsWinning || bet.IsPaid) continue;
            if (!floatDestination.TryGetAvailableComposition((int)bet.WinningsPence, out int[] counts) ||
                !floatDestination.TryConsumeChips(counts))
            {
                RequestUnpaidPayoutRefill();
                return false;
            }

            bet.PaidOutPence = bet.WinningsPence;
            bet.IsPaid = true;
            AnimateSettlement(bet, true);
        }

        return true;
    }

    public void RequestUnpaidPayoutRefill()
    {
        int unpaid = 0;
        foreach (RouletteCustomerBet bet in currentBets)
            if (bet.IsWinning && !bet.IsPaid)
                unpaid = checked(unpaid + (int)bet.WinningsPence);
        if (unpaid > 0 && !floatDestination.TryGetAvailableComposition(unpaid, out _))
            floatDestination.RequestAttention(unpaid);
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
        betChipVisuals.Clear();
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
