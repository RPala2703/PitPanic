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
    [SerializeField] private int[] availableBetOptionsPence = { 1000, 2000, 3000, 4000, 5000 };
    [SerializeField] private BlackjackChipDenomination[] chipDenominations = BlackjackChipDenomination.CreateDefaults();
    [SerializeField, Min(0.1f)] private float dealerActionInterval = 0.8f;
    [SerializeField, Range(0f, 1f)] private float flickAssistanceChance = 0.18f;

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
    private bool flickAssistanceForRound;
    private bool flickDealerPaused;
    private bool flickBoundaryReady;
    private bool forceFlickNextRound;
    private AIDealer assignedDealer;
    [SerializeField] private CasinoMinigameStation flickStation;
    private float nextDealerActionTime;
    private string message = "Waiting for customers.";

    public bool IsConfigured =>
        playerController != null &&
        interactionZone != null &&
        interactionZone.IsConfigured &&
        attention != null &&
        seats != null &&
        seats.Length == 3;

    public int WaitingCustomerCount => waitingSet.Count;
    public BlackjackTableRound ActiveRound => round;
    public bool IsFlickReady => flickBoundaryReady && flickDealerPaused && flickStation != null && flickStation.IsTaskPending;
    public bool HasFlickAssistanceStation => flickStation != null;
    public AIDealer AssignedDealer => assignedDealer;
    public bool IsDealerStaffed => assignedDealer != null && assignedDealer.IsAtStation;
    public bool IsActivelyServing => waitingSet.Count > 0 ||
                                     state != BlackjackTableState.WaitingForCustomers &&
                                     state != BlackjackTableState.ClearingTable;

    public void Configure(
        CasinoPlayerController player,
        TableInteractionZone tableInteractionZone,
        CasinoTableAttention tableAttention,
        CasinoSeat[] tableSeats)
    {
        playerController = player;
        interactionZone = tableInteractionZone;
        attention = tableAttention;
        seats = tableSeats;
    }

    public void ConfigureFlickAssistance(CasinoMinigameStation station)
    {
        flickStation = station;
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
        if (tableView == null)
        {
            tableView = gameObject.AddComponent<BlackjackTableView>();
        }

        if (!ValidateBetConfiguration())
        {
            enabled = false;
            return;
        }

        tableView.Configure(PerformDealerAction, ClosePanel, AddPayoutChip, RemovePayoutChip, ConfirmPayout, chipDenominations);
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
        ResolveAllAttention(TableAttentionReason.FlickAssistanceRequired);
    }

    private void Update()
    {
        UpdateRoundProgress();
        UpdateAutomatedDealer();

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f8Key.wasPressedThisFrame)
        {
            QueueFlickAssistanceTest();
        }

        if (panelOpen)
        {
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
        bool hasAction = IsDealerStaffed && (waitingSet.Count > 0 || state != BlackjackTableState.WaitingForCustomers);
        tableView.SetPromptVisible(inRange && hasAction, IsFlickReady ? "E  •  FLICK! HELP THE DEALER" : GetActionPrompt());
        if (inRange && hasAction && (flickStation == null || !flickStation.IsTaskPending) &&
            keyboard != null && keyboard.eKey.wasPressedThisFrame)
        {
            OpenPanel();
        }
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
        if (!IsDealerStaffed)
        {
            return;
        }

        panelOpen = true;
        tableView.SetPromptVisible(false, "");
        tableView.SetVisible(true);
        playerController.SetMovementEnabled(false);

        RefreshView();
    }

    private void ClosePanel()
    {
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

    private void BeginRound()
    {
        if (!IsDealerStaffed)
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
        flickAssistanceForRound = flickStation != null &&
                                  (flickStation.IsTaskPending || forceFlickNextRound ||
                                   Random.value < flickAssistanceChance);
        forceFlickNextRound = false;
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
        if (dealStep == round.CustomerHands.Count + 1 && flickAssistanceForRound)
        {
            flickAssistanceForRound = false;
            if (RequestFlickAssistanceAtBoundary())
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
        if (!IsDealerStaffed)
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
        message = $"{OutcomeName(hand.Outcome)} for {GetCustomerName(hand)}. Build and confirm {FormatPounds(hand.PayoutPence)} in chips.";
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
        int required = round.CustomerHands[payoutHandIndex].PayoutPence;
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
        message = $"Selected {FormatPounds(GetSelectedPayoutTotal())} of {FormatPounds(hand.PayoutPence)}.";
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
        if (selectedTotal != hand.PayoutPence)
        {
            payoutPenaltyCount++;
            selectedPayoutChips.Clear();
            message = $"Incorrect payout. Check the chips and try again; accuracy penalty {payoutPenaltyCount}.";
            RefreshView();
            return;
        }

        if (!round.TryMarkPayoutPaid(hand))
        {
            Debug.LogError("Blackjack payout was already completed or is not payable.", this);
            return;
        }

        accuracyScore++;
        attention.ResolveAttention(TableAttentionReason.PayoutRequired);
        selectedPayoutChips.Clear();
        message = $"Correct payout to {GetCustomerName(hand)}.";
        SelectNextPayout();
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
        GetComponent<FlickCardMinigame>()?.ClearPhysicalCards();
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
            IsDealerStaffed,
            message);
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
        if (!IsDealerStaffed || flickDealerPaused || Time.time < nextDealerActionTime)
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
                    round.TryMarkPayoutPaid(hand);
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
        return "E  •  Inspect blackjack table and dealer";
    }

    public void QueueFlickAssistanceTest()
    {
        forceFlickNextRound = true;
        if (state == BlackjackTableState.InitialDeal && round != null &&
            dealStep >= round.CustomerHands.Count + 1)
        {
            forceFlickNextRound = false;
            flickAssistanceForRound = true;
            RequestFlickAssistanceAtBoundary();
        }
        else
        {
            Debug.Log($"{name}: FLICK! testing task queued for the next safe blackjack-deal boundary.", this);
        }
    }

    public int GetNextFlickTarget()
    {
        if (!IsFlickReady || round == null)
        {
            return -2;
        }

        int customerCount = round.CustomerHands.Count;
        if (dealStep < customerCount)
        {
            return dealStep;
        }

        if (dealStep == customerCount || dealStep > customerCount * 2)
        {
            return -1;
        }

        return dealStep - customerCount - 1;
    }

    public Vector3 GetFlickTargetPosition(int customerHandIndex)
    {
        if (customerHandIndex >= 0 && round != null && customerHandIndex < round.CustomerHands.Count)
        {
            CasinoSeat seat = round.CustomerHands[customerHandIndex].Seat;
            if (seat != null && seat.BettingPosition != null)
            {
                return seat.BettingPosition.position;
            }
        }

        return transform.TransformPoint(new Vector3(0f, 1.46f, 0.48f));
    }

    public bool TryBeginFlickAssistance()
    {
        return IsFlickReady && IsDealerStaffed && state == BlackjackTableState.InitialDeal;
    }

    public bool TryDealFlickCard(int targetIndex, out BlackjackCard card, out int cardIndex, out bool holeCard)
    {
        card = default;
        cardIndex = -1;
        holeCard = false;
        if (!TryBeginFlickAssistance() || targetIndex != GetNextFlickTarget())
        {
            return false;
        }

        bool dealerRecipient = targetIndex < 0;
        cardIndex = dealerRecipient ? round.DealerCards.Count : round.CustomerHands[targetIndex].Cards.Count;
        DealNextOpeningCard();
        card = dealerRecipient
            ? round.DealerCards[cardIndex]
            : round.CustomerHands[targetIndex].Cards[cardIndex];
        holeCard = dealerRecipient && cardIndex == 1;
        return true;
    }

    public void EndFlickAssistance(bool cancelled = false)
    {
        flickDealerPaused = false;
        flickBoundaryReady = false;
        if (cancelled)
        {
            flickAssistanceForRound = false;
        }

        nextDealerActionTime = Time.time + dealerActionInterval;
        RefreshView();
    }

    public void NotifyFlickAttemptFailed()
    {
        if (IsFlickReady)
        {
            message = "FLICK! was not completed. Press E to try again.";
            RefreshView();
        }
    }

    private bool RequestFlickAssistanceAtBoundary()
    {
        if (!IsDealerStaffed || state != BlackjackTableState.InitialDeal ||
            round == null || flickStation == null)
        {
            return false;
        }

        if (!flickStation.IsTaskPending &&
            !flickStation.RequestMinigame(CasinoMinigameType.Flick, TableAttentionReason.FlickAssistanceRequired))
        {
            return false;
        }

        flickBoundaryReady = true;
        flickDealerPaused = true;
        message = "The dealer needs a hand. Flick the next cards to the highlighted targets!";
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
