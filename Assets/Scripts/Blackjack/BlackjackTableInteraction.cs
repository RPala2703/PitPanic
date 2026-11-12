using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class BlackjackTableInteraction : MonoBehaviour
{
    [SerializeField] private CasinoPlayerController playerController;
    [SerializeField] private TableInteractionZone interactionZone;
    [SerializeField] private CasinoTableAttention attention;
    [SerializeField] private CasinoSeat[] seats;
    [SerializeField] private int[] availableBetOptionsPence =
        { 500, 1000, 1500, 2000, 2500, 3000, 3500, 4000, 4500, 5000 };
    [SerializeField] private BlackjackChipDenomination[] chipDenominations = BlackjackChipDenomination.CreateDefaults();
    [SerializeField, Min(0.1f)] private float dealerActionInterval = 0.8f;
    [SerializeField, Range(0f, 1f)] private float shuffleAssistanceChance = 0.18f;

    private readonly Queue<CasinoCustomerAI> waitingCustomers = new Queue<CasinoCustomerAI>();
    private readonly HashSet<CasinoCustomerAI> waitingSet = new HashSet<CasinoCustomerAI>();
    private readonly List<int> selectedPayoutChips = new List<int>();
    private BlackjackTableView tableView;
    private BlackjackTableRound round;
    private BlackjackTableState state = BlackjackTableState.WaitingForCustomers;
    private int activeHandIndex = -1;
    private int dealStep;
    private int payoutHandIndex = -1;
    private int accuracyScore;
    private int payoutPenaltyCount;
    private float nextDecisionTime;
    private float clearTableAt;
    private bool panelOpen;
    private bool awaitingCustomerCard;
    private bool awaitingDealerCard;
    private bool shuffleAssistanceForRound;
    private bool shuffleDealerPaused;
    private bool shuffleBoundaryReady;
    private bool forceShuffleNextRound;
    private float nextShuffleTaskTime;
    private int roundsSinceShuffle;
    private AIDealer assignedDealer;
    [SerializeField] private CasinoMinigameStation shuffleStation;
    [SerializeField] private CasinoFloatRefillDestination floatDestination;
    [SerializeField] private string tableIdentifier;
    private float nextDealerActionTime;
    private string message = "Waiting for customers.";

    public bool IsConfigured =>
        playerController != null &&
        interactionZone != null &&
        interactionZone.IsConfigured &&
        attention != null &&
        (floatDestination != null || GetComponent<CasinoFloatRefillDestination>() != null) &&
        seats != null &&
        seats.Length == 3;

    public int WaitingCustomerCount => waitingSet.Count;
    public bool IsInspectionOpen => panelOpen;
    public BlackjackTableRound ActiveRound => round;
    public BlackjackTableState State => state;
    public bool IsShuffleReady => shuffleBoundaryReady && shuffleDealerPaused &&
                                  shuffleStation != null && shuffleStation.IsTaskPending &&
                                  CanOfferShuffle(IsDealerStaffed, IsPlayerOperating);
    public bool HasShuffleAssistanceStation => shuffleStation != null;
    public event System.Action<BlackjackTableRound> RoundSettled;
    public AIDealer AssignedDealer => assignedDealer;
    public string TableDisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(tableIdentifier))
            {
                return $"BLACKJACK {tableIdentifier.Substring(tableIdentifier.Length - 1)}";
            }

            return name == "Blackjack Table 1" ? "BLACKJACK 1" :
                   name == "Blackjack Table 2" ? "BLACKJACK 2" :
                   name.ToUpperInvariant();
        }
    }
    public bool IsDealerStaffed => assignedDealer != null && assignedDealer.CanServe;
    public bool IsPlayerOperating { get; private set; }
    public bool IsServiceAvailable => IsDealerStaffed || IsPlayerOperating;
    public bool IsActivelyServing => waitingSet.Count > 0 ||
                                     state != BlackjackTableState.WaitingForCustomers &&
                                     state != BlackjackTableState.ClearingTable;

    public static bool CanOfferShuffle(bool hasActiveDealer, bool playerOperating) =>
        hasActiveDealer && !playerOperating;

    public void Configure(
        CasinoPlayerController player,
        TableInteractionZone tableInteractionZone,
        CasinoTableAttention tableAttention,
        CasinoSeat[] tableSeats,
        string identifier = null)
    {
        playerController = player;
        interactionZone = tableInteractionZone;
        attention = tableAttention;
        seats = tableSeats;
        tableIdentifier = identifier;
    }

    public void ConfigureShuffleAssistance(CasinoMinigameStation station)
    {
        shuffleStation = station;
    }

    private void Start()
    {
        if (!IsConfigured)
        {
            Debug.LogError($"{nameof(BlackjackTableInteraction)} on {name} is missing its player, table interaction zone, attention system, or seats.", this);
            enabled = false;
            return;
        }

        foreach (CasinoSeat seat in seats)
        {
            if (seat == null)
            {
                Debug.LogError($"{nameof(BlackjackTableInteraction)} on {name} has an unassigned customer seat.", this);
                enabled = false;
                return;
            }

            seat.CustomerSeated += OnCustomerSeated;
            seat.CustomerReleased += OnCustomerReleased;
            if (seat.Occupant != null)
            {
                OnCustomerSeated(seat.Occupant);
            }
        }

        tableView = GetComponent<BlackjackTableView>();
        if (floatDestination == null) floatDestination = GetComponent<CasinoFloatRefillDestination>();
        if (floatDestination == null)
        {
            Debug.LogError($"{nameof(BlackjackTableInteraction)} on {name} has no table chip inventory.", this);
            enabled = false;
            return;
        }
        if (tableView == null)
        {
            tableView = gameObject.AddComponent<BlackjackTableView>();
        }

        if (!ValidateBetConfiguration())
        {
            enabled = false;
            return;
        }

        tableView.Configure(ManualDealStep, ClosePanel, AddPayoutChip, RemovePayoutChip, ConfirmPayout, chipDenominations, TableDisplayName);
        RefreshView();
    }

    private void OnDestroy()
    {
        if (seats != null)
        {
            foreach (CasinoSeat seat in seats)
            {
                if (seat != null)
                {
                    seat.CustomerSeated -= OnCustomerSeated;
                    seat.CustomerReleased -= OnCustomerReleased;
                }
            }
        }

        ResolveAllAttention(TableAttentionReason.PayoutRequired);
        ResolveAllAttention(TableAttentionReason.ShuffleAssistanceRequired);
    }

    private void Update()
    {
        if (Time.timeScale == 0) return;
        if (shuffleStation != null && shuffleStation.IsTaskPending &&
            !shuffleStation.IsMinigameRunning && (!IsDealerStaffed || IsPlayerOperating))
        {
            shuffleStation.ResolveTaskAfterFatalFailure();
            shuffleBoundaryReady = false;
            shuffleDealerPaused = false;
        }
        if (IsPlayerOperating && IsDealerStaffed)
        {
            IsPlayerOperating = false;
            message = "Your dealer is back! Normal service has resumed.";
            RefreshView();
        }
        if (IsShuffleReady && !ArrayHasSeatedCustomer() && !shuffleStation.IsMinigameRunning)
            AbortShuffleAssistance();
        UpdateRoundProgress();
        UpdateAutomatedDealer();

        if (CasinoMinigamePresentation.IsOpen)
        {
            tableView?.SetPromptVisible(false, "");
            return;
        }

        if (playerController != null &&
            playerController.GetComponent<CasinoPlayerFloatCarrier>()?.IsCarrying == true)
        {
            tableView?.SetPromptVisible(false, "");
            return;
        }

        Keyboard keyboard = Keyboard.current;
#if UNITY_EDITOR
        if (keyboard != null && keyboard.f8Key.wasPressedThisFrame)
        {
            QueueShuffleAssistanceTest();
        }
#endif

        if (shuffleStation != null && shuffleStation.IsMinigameRunning)
        {
            tableView?.SetPromptVisible(false, "");
            return;
        }

        if (panelOpen)
        {
            tableView.SetTakeover(!IsDealerStaffed, IsPlayerOperating, BeginPlayerTakeover);
            tableView.SetAssistance(IsShuffleReady, StartAssistance);
            if (IsPlayerOperating && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                ManualDealStep();
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                ClosePanel();
            }
            return;
        }

        if (playerController == null || interactionZone == null || tableView == null)
        {
            return;
        }

        bool inRange = IsPlayerInRange();
        bool shuffleReady = IsShuffleReady;
        tableView.SetPromptVisible(
            inRange && !CasinoPlayerInteractionRouter.IsAnyInspectionOpen() && !CasinoMinigamePresentation.IsOpen &&
                playerController.GetComponent<CasinoPlayerFloatCarrier>()?.IsCarrying != true,
            $"E  •  {TableDisplayName}{(shuffleReady ? "  •  SHUFFLE! READY" : "")}");
        tableView.SetAssistance(IsShuffleReady, StartAssistance);
    }

    private void OnCustomerSeated(CasinoCustomerAI customer)
    {
        if (customer == null || !waitingSet.Add(customer))
        {
            return;
        }

        waitingCustomers.Enqueue(customer);
        message = state == BlackjackTableState.WaitingForCustomers
            ? $"{waitingSet.Count} customer(s) ready. The dealer will start the shared round."
            : $"{customer.name} is seated for the next round.";
        RefreshView();
    }

    private void OnCustomerReleased(CasinoCustomerAI customer)
    {
        if (ReferenceEquals(customer, null) || !waitingSet.Remove(customer))
        {
            RefreshView();
            return;
        }

        RefreshView();
    }

    private void OpenPanel()
    {
        if (CasinoMinigamePresentation.IsOpen || CasinoPlayerInteractionRouter.IsAnyInspectionOpen() ||
            playerController.GetComponent<CasinoPlayerFloatCarrier>()?.IsCarrying == true)
        {
            return;
        }

        panelOpen = true;
        CasinoSoundscape.SetFocus(transform);
        tableView.SetPromptVisible(false, "");
        tableView.SetVisible(true);
        playerController.SetMovementEnabled(false);

        RefreshView();
    }

    private void ClosePanel()
    {
        IsPlayerOperating = false;
        CasinoSoundscape.ReleaseFocus(transform);
        panelOpen = false;
        if (tableView != null)
        {
            tableView.SetVisible(false);
        }

        if (playerController != null)
        {
            playerController.SetMovementEnabled(true);
        }
    }

    private bool IsPlayerInRange()
    {
        return interactionZone.IsPlayerInRange(playerController.transform);
    }

    public bool IsPlayerInRange(Transform candidate)
    {
        return candidate != null && interactionZone != null &&
               interactionZone.IsPlayerInRange(candidate);
    }

    public bool TryOpenInspectionFromPlayerInput()
    {
        if (panelOpen || !IsPlayerInRange(playerController.transform) ||
            shuffleStation != null && shuffleStation.IsMinigameRunning ||
            playerController.GetComponent<CasinoPlayerFloatCarrier>()?.IsCarrying == true ||
            CasinoMinigamePresentation.IsOpen)
        {
            return false;
        }

        OpenPanel();
        return panelOpen;
    }

    private void StartAssistance()
    {
        if (!IsShuffleReady || shuffleStation == null) return;
        ClosePanel();
        if (!shuffleStation.TryBegin())
        {
            message = "Task not ready. Wait for the dealer and try again.";
            OpenPanel();
        }
    }

    private bool ArrayHasSeatedCustomer() => seats != null &&
        System.Array.Exists(seats, seat => seat != null && seat.Occupant != null);

    public bool BeginPlayerTakeover()
    {
        if (!panelOpen || IsDealerStaffed || Time.timeScale == 0 ||
            playerController.GetComponent<CasinoPlayerFloatCarrier>()?.IsCarrying == true) return false;
        IsPlayerOperating = true;
        if (shuffleStation != null && shuffleStation.IsTaskPending)
        {
            shuffleStation.ResolveTaskAfterFatalFailure();
            shuffleDealerPaused = false;
            shuffleBoundaryReady = false;
        }
        message = "YOU'RE DEALING! SPACE or DEAL CARD advances the hand. Pay the exact chips shown; ESC leaves.";
        playerController.GetComponentInChildren<PixelCharacterVisual>()?.PlayAction(CasinoAnimationPose.Deal, transform.position, .8f);
        RefreshView();
        return true;
    }

    public void ManualDealStep()
    {
        if (!IsPlayerOperating || !panelOpen || Time.timeScale == 0 || Time.time < nextDealerActionTime) return;
        nextDealerActionTime = Time.time + .3f;
        if (state == BlackjackTableState.Settlement)
        {
            if (payoutHandIndex >= 0)
            {
                selectedPayoutChips.Clear();
                if (TryGetAvailablePayout(round.CustomerHands[payoutHandIndex].WinningsPence, out int[] counts))
                {
                    int[] values = { 500, 250, 2500, 10000 };
                    for (int i = 0; i < counts.Length; i++)
                        for (int chip = 0; chip < counts[i]; chip++)
                            selectedPayoutChips.Add(values[i]);
                    ConfirmPayout();
                }
                else
                {
                    floatDestination?.RequestAttention();
                    message = "The table float is short. Deliver a refill to finish this payout.";
                    RefreshView();
                }
            }
        }
        else if (state != BlackjackTableState.ClearingTable)
            PerformDealerAction();
        playerController.GetComponentInChildren<PixelCharacterVisual>()?.PlayAction(
            state == BlackjackTableState.Settlement ? CasinoAnimationPose.Pay : CasinoAnimationPose.Deal, transform.position, .5f);
        CasinoSoundscape.Play(CasinoSoundCue.Card, transform.position, transform);
    }

    private void BeginRound()
    {
        (IsPlayerOperating ? playerController.GetComponentInChildren<PixelCharacterVisual>() :
            AssignedDealer?.GetComponentInChildren<PixelCharacterVisual>())?.PlayAction(
            CasinoAnimationPose.Shuffle, transform.position, .9f);
        if (!IsServiceAvailable)
        {
            return;
        }

        List<CasinoCustomerAI> participants = new List<CasinoCustomerAI>(3);
        List<int> participantBetsPence = new List<int>(3);
        int customersToCheck = waitingCustomers.Count;
        while (customersToCheck-- > 0 && participants.Count < seats.Length)
        {
            CasinoCustomerAI customer = waitingCustomers.Dequeue();
            if (!waitingSet.Contains(customer))
            {
                continue;
            }

            if (customer == null || customer.Seat == null || customer.Seat.Occupant != customer ||
                !customer.IsSeated)
            {
                waitingSet.Remove(customer);
                continue;
            }

            if (!customer.TryChooseBetPence(availableBetOptionsPence, out int betPence))
            {
                waitingCustomers.Enqueue(customer);
                continue;
            }

            waitingSet.Remove(customer);
            if (!customer.BeginService())
            {
                continue;
            }

            participants.Add(customer);
            participantBetsPence.Add(betPence);
        }

        if (participants.Count == 0)
        {
            state = BlackjackTableState.WaitingForCustomers;
            message = waitingSet.Count > 0
                ? "No waiting customer has a stake that matches this table's limits."
                : "Waiting for customers.";
            RefreshView();
            return;
        }

        round = new BlackjackTableRound();
        List<string> bets = new List<string>(participants.Count);
        for (int i = 0; i < participants.Count; i++)
        {
            CasinoCustomerAI customer = participants[i];
            int betPence = participantBetsPence[i];
            // Stakes stay on the felt until settlement; only losing stakes enter the float.
            round.AddCustomer(customer, customer.Seat, betPence);
            bets.Add($"{GetCustomerName(customer)} {FormatPounds(betPence)}");
        }

        dealStep = 0;
        activeHandIndex = 0;
        payoutHandIndex = -1;
        selectedPayoutChips.Clear();
        awaitingCustomerCard = false;
        awaitingDealerCard = false;
        state = BlackjackTableState.TakingBets;
        roundsSinceShuffle++;
        shuffleAssistanceForRound = !IsPlayerOperating && shuffleStation != null &&
                                  (shuffleStation.IsTaskPending || forceShuffleNextRound ||
                                   Time.time >= nextShuffleTaskTime && !CasinoMinigamePresentation.IsOpen &&
                                   (roundsSinceShuffle >= 4 || Random.value < shuffleAssistanceChance));
        forceShuffleNextRound = false;
        message = $"{string.Join(", ", bets)}. The dealer is starting the opening deal.";
        RefreshView();
    }

    private void PerformDealerAction()
    {
        switch (state)
        {
            case BlackjackTableState.WaitingForCustomers:
                BeginRound();
                break;
            case BlackjackTableState.TakingBets:
                state = BlackjackTableState.InitialDeal;
                message = GetInitialDealPrompt();
                RefreshView();
                break;
            case BlackjackTableState.InitialDeal:
                DealNextOpeningCard();
                break;
            case BlackjackTableState.CustomerTurns:
                DealRequestedCustomerCard();
                break;
            case BlackjackTableState.DealerTurn:
                DealRequestedDealerCard();
                break;
        }
    }

    private void DealNextOpeningCard()
    {
        if (round == null || round.CustomerHands.Count == 0)
        {
            BeginSettlement();
            return;
        }

        int customerCount = round.CustomerHands.Count;
        if (dealStep < customerCount)
        {
            activeHandIndex = dealStep;
            round.DealTo(round.CustomerHands[dealStep]);
        }
        else if (dealStep == customerCount)
        {
            activeHandIndex = -1;
            round.DealToDealer();
        }
        else if (dealStep <= 2 * customerCount)
        {
            activeHandIndex = dealStep - customerCount - 1;
            round.DealTo(round.CustomerHands[activeHandIndex]);
        }
        else
        {
            activeHandIndex = -1;
            round.DealToDealer();
            dealStep++;
            CompleteInitialDeal();
            return;
        }

        dealStep++;
        if (dealStep == round.CustomerHands.Count + 1 && shuffleAssistanceForRound)
        {
            shuffleAssistanceForRound = false;
            if (RequestShuffleAssistanceAtBoundary())
            {
                return;
            }
        }

        message = GetInitialDealPrompt();
        RefreshView();
    }

    private string GetInitialDealPrompt()
    {
        if (round == null || round.CustomerHands.Count == 0) return "Waiting for customers.";
        int customerCount = round.CustomerHands.Count;
        if (dealStep < customerCount) return $"Dealer deals first card to {GetCustomerName(round.CustomerHands[dealStep])}.";
        if (dealStep == customerCount) return "Dealer deals the up-card.";
        if (dealStep <= 2 * customerCount)
        {
            int handIndex = dealStep - customerCount - 1;
            return $"Dealer deals second card to {GetCustomerName(round.CustomerHands[handIndex])}.";
        }

        return "Dealer deals the hidden card.";
    }

    private void CompleteInitialDeal()
    {
        for (int i = 0; i < round.CustomerHands.Count; i++)
        {
            BlackjackHand hand = round.CustomerHands[i];
            if (hand.IsNatural)
            {
                hand.Status = BlackjackHandStatus.Blackjack;
            }
        }

        if (round.DealerHasNatural)
        {
            message = "Dealer blackjack. Reveal the hole card and settle all hands.";
            BeginSettlement();
            return;
        }

        state = BlackjackTableState.CustomerTurns;
        activeHandIndex = FindNextPlayableHand(0);
        if (activeHandIndex < 0)
        {
            BeginDealerTurn();
            return;
        }

        StartCustomerTurn();
    }

    private void StartCustomerTurn()
    {
        awaitingCustomerCard = false;
        if (activeHandIndex < 0 || activeHandIndex >= round.CustomerHands.Count)
        {
            BeginDealerTurn();
            return;
        }

        BlackjackHand hand = round.CustomerHands[activeHandIndex];
        if (hand.Status == BlackjackHandStatus.Playing)
        {
            nextDecisionTime = Time.time + 0.75f;
            message = $"{GetCustomerName(hand)} is thinking...";
        }

        RefreshView();
    }

    private void UpdateRoundProgress()
    {
        if (!IsServiceAvailable)
        {
            return;
        }

        if (state == BlackjackTableState.CustomerTurns && !awaitingCustomerCard && Time.time >= nextDecisionTime)
        {
            RequestCustomerDecision();
        }
        else if (state == BlackjackTableState.ClearingTable && Time.time >= clearTableAt)
        {
            FinishClearingTable();
        }
    }

    private void RequestCustomerDecision()
    {
        if (round == null || activeHandIndex < 0 || activeHandIndex >= round.CustomerHands.Count)
        {
            BeginDealerTurn();
            return;
        }

        BlackjackHand hand = round.CustomerHands[activeHandIndex];
        if (hand.Status != BlackjackHandStatus.Playing || hand.Total >= 21)
        {
            if (hand.Status == BlackjackHandStatus.Playing)
            {
                hand.Status = hand.Total > 21 ? BlackjackHandStatus.Bust : BlackjackHandStatus.Standing;
            }

            AdvanceCustomerTurn();
            return;
        }

        if (hand.Customer != null && hand.Customer.WantsAnotherCard(hand.Total))
        {
            awaitingCustomerCard = true;
            message = $"{GetCustomerName(hand)} requests a card.";
        }
        else
        {
            hand.Status = BlackjackHandStatus.Standing;
            message = $"{GetCustomerName(hand)} stands on {hand.Total}.";
            AdvanceCustomerTurn();
        }
    }

    private void DealRequestedCustomerCard()
    {
        if (!awaitingCustomerCard || round == null || activeHandIndex < 0)
        {
            return;
        }

        BlackjackHand hand = round.CustomerHands[activeHandIndex];
        awaitingCustomerCard = false;
        round.DealTo(hand);
        if (hand.Total > 21)
        {
            hand.Status = BlackjackHandStatus.Bust;
            message = $"{GetCustomerName(hand)} busts at {hand.Total}.";
            AdvanceCustomerTurn();
        }
        else if (hand.Total == 21)
        {
            hand.Status = BlackjackHandStatus.Standing;
            message = $"{GetCustomerName(hand)} reaches 21 and stands.";
            AdvanceCustomerTurn();
        }
        else
        {
            message = $"{GetCustomerName(hand)} is deciding whether to hit again.";
            nextDecisionTime = Time.time + 0.75f;
            RefreshView();
        }
    }

    private void AdvanceCustomerTurn()
    {
        awaitingCustomerCard = false;
        activeHandIndex = FindNextPlayableHand(activeHandIndex + 1);
        if (activeHandIndex < 0)
        {
            BeginDealerTurn();
            return;
        }

        StartCustomerTurn();
    }

    private int FindNextPlayableHand(int fromIndex)
    {
        for (int i = Mathf.Max(0, fromIndex); i < round.CustomerHands.Count; i++)
        {
            BlackjackHand hand = round.CustomerHands[i];
            if (hand.Status == BlackjackHandStatus.Playing)
            {
                return i;
            }
        }

        return -1;
    }

    private void BeginDealerTurn()
    {
        state = BlackjackTableState.DealerTurn;
        activeHandIndex = -1;
        if (round == null)
        {
            BeginSettlement();
            return;
        }

        if (!round.NeedsDealerComparison)
        {
            message = "No hands need comparing. Return surviving stakes and settle the winnings.";
            BeginSettlement();
            return;
        }

        if (BlackjackRules.DealerMustHit(round.DealerCards))
        {
            awaitingDealerCard = true;
            message = $"Dealer shows {round.DealerTotal} and draws.";
        }
        else
        {
            message = $"Dealer stands on {round.DealerTotal}.";
            BeginSettlement();
            return;
        }

        RefreshView();
    }

    private void DealRequestedDealerCard()
    {
        if (!awaitingDealerCard || round == null)
        {
            return;
        }

        awaitingDealerCard = false;
        round.DealToDealer();
        if (!BlackjackRules.DealerMustHit(round.DealerCards))
        {
            message = round.DealerTotal > 21
                ? $"Dealer busts at {round.DealerTotal}."
                : $"Dealer stands on {round.DealerTotal}.";
            BeginSettlement();
        }
        else
        {
            awaitingDealerCard = true;
            message = $"Dealer draws to {round.DealerTotal}.";
            RefreshView();
        }
    }

    private void BeginSettlement()
    {
        awaitingDealerCard = false;
        awaitingCustomerCard = false;
        if (round == null)
        {
            state = BlackjackTableState.WaitingForCustomers;
            RefreshView();
            return;
        }

        state = BlackjackTableState.Settlement;
        round.Settle();
        foreach (BlackjackHand hand in round.CustomerHands)
        {
            if (hand.Outcome != BlackjackOutcome.Lose) continue;
            int[] wagerCounts = new int[4];
            foreach (BlackjackChipDenomination chip in BlackjackChipDenomination.GetExactComposition(hand.BetPence, chipDenominations))
            {
                int index = chip.ValuePence switch { 500 => 0, 250 => 1, 2500 => 2, 10000 => 3, _ => -1 };
                if (index >= 0) wagerCounts[index]++;
            }
            if (!floatDestination.TryAddChips(wagerCounts))
                throw new System.InvalidOperationException($"Could not collect a losing stake at {TableDisplayName}.");
        }
        foreach (BlackjackHand hand in round.CustomerHands)
            if (hand.Customer != null)
                hand.Customer.GetComponentInChildren<PixelCharacterVisual>()?.React(hand.PayoutPence >= hand.BetPence);
        RoundSettled?.Invoke(round);
        payoutHandIndex = -1;
        selectedPayoutChips.Clear();
        SelectNextPayout();
    }

    private void SelectNextPayout()
    {
        payoutHandIndex = -1;
        if (round != null)
        {
            for (int i = 0; i < round.CustomerHands.Count; i++)
            {
                if (round.CustomerHands[i].PayoutPence > 0 && !round.CustomerHands[i].PayoutPaid)
                {
                    payoutHandIndex = i;
                    break;
                }
            }
        }

        if (payoutHandIndex < 0)
        {
            BeginClearingTable();
            return;
        }

        BlackjackHand hand = round.CustomerHands[payoutHandIndex];
        message = $"{OutcomeName(hand.Outcome)} for {GetCustomerName(hand)}. Stake returned; pay {FormatPounds(hand.WinningsPence)} winnings.";
        RefreshView();
    }

    private void AddPayoutChip(int denomination)
    {
        if (state != BlackjackTableState.Settlement || payoutHandIndex < 0 ||
            !ContainsDenomination(denomination))
        {
            return;
        }

        int selected = GetSelectedPayoutTotal();
        int required = round.CustomerHands[payoutHandIndex].WinningsPence;
        if (!HasChipAvailable(denomination))
        {
            floatDestination?.RequestAttention();
            message = $"{FormatPounds(denomination)} chips are out of stock. Request or deliver a table float.";
            RefreshView();
            return;
        }
        if (selected + denomination > required)
        {
            message = "That chip exceeds the remaining payout. Remove a chip or choose a smaller value.";
        }
        else
        {
            selectedPayoutChips.Add(denomination);
            message = $"Selected {FormatPounds(GetSelectedPayoutTotal())} of {FormatPounds(required)}.";
        }

        RefreshView();
    }

    private void RemovePayoutChip()
    {
        if (selectedPayoutChips.Count == 0)
        {
            return;
        }

        selectedPayoutChips.RemoveAt(selectedPayoutChips.Count - 1);
        BlackjackHand hand = round.CustomerHands[payoutHandIndex];
        message = $"Selected {FormatPounds(GetSelectedPayoutTotal())} of {FormatPounds(hand.WinningsPence)} winnings.";
        RefreshView();
    }

    private void ConfirmPayout()
    {
        if (state != BlackjackTableState.Settlement || payoutHandIndex < 0)
        {
            return;
        }

        BlackjackHand hand = round.CustomerHands[payoutHandIndex];
        int selectedTotal = GetSelectedPayoutTotal();
        if (selectedTotal != hand.WinningsPence)
        {
            payoutPenaltyCount++;
            selectedPayoutChips.Clear();
            message = $"Incorrect payout. Check the chips and try again; accuracy penalty {payoutPenaltyCount}.";
            RefreshView();
            return;
        }

        int[] payoutCounts = GetSelectedPayoutCounts();
        if (floatDestination == null || !floatDestination.TryConsumeChips(payoutCounts))
        {
            floatDestination?.RequestAttention();
            message = "The table float cannot cover these chips. Refill the table before paying.";
            RefreshView();
            return;
        }

        if (!round.TryMarkPayoutPaid(hand))
        {
            floatDestination.TryAddChips(payoutCounts);
            Debug.LogError("Blackjack payout was already completed or is not payable.", this);
            return;
        }

        accuracyScore++;
        attention.ResolveAttention(TableAttentionReason.PayoutRequired);
        selectedPayoutChips.Clear();
        message = $"Correct payout to {GetCustomerName(hand)}.";
        if (floatDestination != null && floatDestination.NeedsRefill())
            floatDestination.RequestAttention();
        SelectNextPayout();
    }

    private bool HasChipAvailable(int denominationPence)
    {
        int index = denominationPence switch { 500 => 0, 250 => 1, 2500 => 2, 10000 => 3, _ => -1 };
        return index >= 0 && floatDestination != null && floatDestination.GetChipCount(index) > 0;
    }

    private int[] GetSelectedPayoutCounts()
    {
        int[] counts = new int[4];
        foreach (int denomination in selectedPayoutChips)
        {
            int index = denomination switch { 500 => 0, 250 => 1, 2500 => 2, 10000 => 3, _ => -1 };
            if (index >= 0) counts[index]++;
        }
        return counts;
    }

    private bool TryGetAvailablePayout(int amountPence, out int[] counts)
    {
        if (floatDestination != null)
            return floatDestination.TryGetAvailableComposition(amountPence, out counts);
        counts = null;
        return false;
    }

    private void BeginClearingTable()
    {
        state = BlackjackTableState.ClearingTable;
        activeHandIndex = -1;
        payoutHandIndex = -1;
        clearTableAt = Time.time + 1.25f;
        message = "Round complete. Clearing cards for the next customers...";
        RefreshView();
    }

    private void FinishClearingTable()
    {
        if (round != null)
        {
            foreach (BlackjackHand hand in round.CustomerHands)
            {
                if (hand.Customer != null && hand.Customer.Seat == hand.Seat && hand.Customer.IsSeated)
                {
                    hand.Customer.CompleteBlackjackRound();
                }
            }
        }

        round = null;
        state = BlackjackTableState.WaitingForCustomers;
        message = "Waiting for the next customers.";
        ResolveAllAttention(TableAttentionReason.PayoutRequired);
        RefreshView();
        if (waitingSet.Count > 0)
        {
            BeginRound();
        }
    }

    private void ResolveAllAttention(TableAttentionReason reason)
    {
        while (attention != null && attention.ResolveAttention(reason))
        {
        }
    }

    private void RefreshView()
    {
        if (tableView == null)
        {
            return;
        }

        bool revealDealerHole = state == BlackjackTableState.DealerTurn ||
                                state == BlackjackTableState.Settlement ||
                                state == BlackjackTableState.ClearingTable;
        tableView.Refresh(
            state,
            seats,
            round,
            GetHighlightedHandIndex(),
            revealDealerHole,
            payoutHandIndex,
            selectedPayoutChips,
            accuracyScore,
            payoutPenaltyCount,
            awaitingCustomerCard,
            !IsPlayerOperating,
            message);
        tableView.SetFloatInventory(floatDestination);
        tableView.SetTakeover(!IsDealerStaffed, IsPlayerOperating, BeginPlayerTakeover);
    }

    public void SetAssignedDealer(AIDealer dealer)
    {
        if (ReferenceEquals(assignedDealer, dealer))
        {
            return;
        }

        assignedDealer = dealer;
        if (IsDealerStaffed)
        {
            ResolveAllAttention(TableAttentionReason.TableUnstaffed);
            ResolveAllAttention(TableAttentionReason.DealerBreakRequired);
            ResolveAllAttention(TableAttentionReason.DealerExhausted);
            nextDealerActionTime = Time.time + dealerActionInterval;
        }
        else if (dealer == null)
        {
            attention.RequestAttentionOnce(TableAttentionReason.TableUnstaffed);
        }

        RefreshView();
    }

    private void UpdateAutomatedDealer()
    {
        if (!IsDealerStaffed || IsPlayerOperating || shuffleDealerPaused || Time.time < nextDealerActionTime)
        {
            return;
        }

        nextDealerActionTime = Time.time + dealerActionInterval * assignedDealer.WorkSpeedMultiplier;
        if (state == BlackjackTableState.WaitingForCustomers)
        {
            if (waitingSet.Count > 0)
            {
                BeginRound();
            }

            return;
        }

        if (state == BlackjackTableState.ClearingTable)
        {
            FinishClearingTable();
            return;
        }

        if (state == BlackjackTableState.Settlement)
        {
            foreach (BlackjackHand hand in round.CustomerHands)
            {
                if (!hand.PayoutPaid && hand.PayoutPence > 0)
                {
                    if (floatDestination == null ||
                        !TryGetAvailablePayout(hand.WinningsPence, out int[] counts) ||
                        !floatDestination.TryConsumeChips(counts))
                    {
                        floatDestination?.RequestAttention();
                        message = "Table float is short; deliver a refill to complete the automatic payout.";
                        RefreshView();
                        return;
                    }

                    bool paid = round.TryMarkPayoutPaid(hand);
                    if (!paid) floatDestination.TryAddChips(counts);
                }
            }

            ResolveAllAttention(TableAttentionReason.PayoutRequired);
            BeginClearingTable();
            return;
        }

        PerformDealerAction();
    }

    private int GetHighlightedHandIndex()
    {
        if (state == BlackjackTableState.Settlement) return payoutHandIndex;
        if (state == BlackjackTableState.InitialDeal && round != null)
        {
            int customerCount = round.CustomerHands.Count;
            if (dealStep < customerCount) return dealStep;
            if (dealStep > customerCount && dealStep <= 2 * customerCount) return dealStep - customerCount - 1;
            return -1;
        }

        return state == BlackjackTableState.CustomerTurns ? activeHandIndex : -1;
    }

    private string GetActionPrompt()
    {
        return $"Q  •  Inspect {TableDisplayName} and dealer";
    }

    public void QueueShuffleAssistanceTest()
    {
        forceShuffleNextRound = true;
        if (state == BlackjackTableState.InitialDeal && round != null &&
            dealStep >= round.CustomerHands.Count + 1)
        {
            forceShuffleNextRound = false;
            shuffleAssistanceForRound = true;
            RequestShuffleAssistanceAtBoundary();
        }
        else
        {
            Debug.Log($"{name}: SHUFFLE! testing task queued for the next safe blackjack-deal boundary.", this);
        }
    }

    public bool CanBeginShuffleAssistance()
    {
        return IsShuffleReady && IsServiceAvailable && state == BlackjackTableState.InitialDeal;
    }

    public bool TryPrepareShuffleAssistance()
    {
        if (!CanBeginShuffleAssistance())
        {
            return false;
        }

        if (panelOpen)
        {
            ClosePanel();
        }
        else
        {
            panelOpen = false;
        }

        if (tableView != null)
        {
            tableView.SetVisible(false);
            tableView.SetPromptVisible(false, "");
        }

        return true;
    }

    public void PlayShuffleAssistanceAction()
    {
        playerController.GetComponentInChildren<PixelCharacterVisual>()?.PlayAction(
            CasinoAnimationPose.Shuffle, transform.position, 1.1f);
        assignedDealer?.GetComponentInChildren<PixelCharacterVisual>()?.PlayAction(
            CasinoAnimationPose.Shuffle, transform.position, 1.1f);
    }

    public void EndShuffleAssistance(bool cancelled = false)
    {
        if (cancelled)
        {
            shuffleBoundaryReady = shuffleStation != null && shuffleStation.IsTaskPending &&
                                   round != null && state == BlackjackTableState.InitialDeal &&
                                   IsDealerStaffed && !IsPlayerOperating;
            shuffleDealerPaused = shuffleBoundaryReady;
            message = shuffleBoundaryReady
                ? "SHUFFLE! is ready to retry."
                : "SHUFFLE! ended; the dealer will continue.";
            RefreshView();
            return;
        }

        shuffleDealerPaused = false;
        shuffleBoundaryReady = false;
        nextDealerActionTime = Time.time + dealerActionInterval;
        RefreshView();
    }

    public void AbortShuffleAssistance()
    {
        shuffleStation?.ResolveTaskAfterFatalFailure();
        EndShuffleAssistance();
    }

    public void NotifyShuffleAttemptFailed()
    {
        if (IsShuffleReady)
        {
            message = "Not quite! Press E to try SHUFFLE! again.";
            RefreshView();
        }
    }

    private bool RequestShuffleAssistanceAtBoundary()
    {
        if (!IsDealerStaffed || IsPlayerOperating || state != BlackjackTableState.InitialDeal ||
            round == null || shuffleStation == null)
        {
            return false;
        }

        if (!shuffleStation.IsTaskPending &&
            !shuffleStation.RequestMinigame(CasinoMinigameType.Shuffle, TableAttentionReason.ShuffleAssistanceRequired))
        {
            return false;
        }

        shuffleBoundaryReady = true;
        shuffleDealerPaused = true;
        nextShuffleTaskTime = Time.time + 60f;
        roundsSinceShuffle = 0;
        message = "Help the dealer shuffle the cards!";
        RefreshView();
        return true;
    }

    private bool ContainsDenomination(int denomination)
    {
        for (int i = 0; i < chipDenominations.Length; i++)
        {
            if (chipDenominations[i].ValuePence == denomination) return true;
        }

        return false;
    }

    private bool ValidateBetConfiguration()
    {
        if (chipDenominations == null || chipDenominations.Length == 0 ||
            availableBetOptionsPence == null || availableBetOptionsPence.Length == 0)
        {
            Debug.LogError($"{nameof(BlackjackTableInteraction)} on {name} needs chip denominations and available bet options.", this);
            return false;
        }

        HashSet<int> denominationValues = new HashSet<int>();
        foreach (BlackjackChipDenomination denomination in chipDenominations)
        {
            if (denomination.ValuePence <= 0 || !denominationValues.Add(denomination.ValuePence))
            {
                Debug.LogError($"{nameof(BlackjackTableInteraction)} on {name} has an invalid or duplicate chip denomination.", this);
                return false;
            }
        }

        HashSet<int> stakeValues = new HashSet<int>();
        foreach (int stake in availableBetOptionsPence)
        {
            if (stake <= 0 || !stakeValues.Add(stake))
            {
                Debug.LogError($"{nameof(BlackjackTableInteraction)} on {name} has an invalid or duplicate bet option.", this);
                return false;
            }

            try
            {
                BlackjackChipDenomination.GetExactComposition(stake, chipDenominations);
            }
            catch (System.InvalidOperationException exception)
            {
                Debug.LogError($"{nameof(BlackjackTableInteraction)} on {name} has a bet that cannot be represented by its chips: {exception.Message}", this);
                return false;
            }
        }

        return true;
    }

    private int GetSelectedPayoutTotal()
    {
        int total = 0;
        for (int i = 0; i < selectedPayoutChips.Count; i++)
        {
            total = checked(total + selectedPayoutChips[i]);
        }

        return total;
    }

    private static string FormatPounds(int pence)
    {
        return $"£{pence / 100}.{pence % 100:00}";
    }

    private static string GetCustomerName(BlackjackHand hand)
    {
        return hand.Customer != null ? hand.Customer.name : "Customer";
    }

    private static string GetCustomerName(CasinoCustomerAI customer)
    {
        return customer != null ? customer.name : "Customer";
    }

    private static string OutcomeName(BlackjackOutcome outcome)
    {
        switch (outcome)
        {
            case BlackjackOutcome.Blackjack: return "BLACKJACK";
            case BlackjackOutcome.Win: return "WIN";
            case BlackjackOutcome.Lose: return "LOSE";
            case BlackjackOutcome.Push: return "PUSH";
            default: return "";
        }
    }
}
