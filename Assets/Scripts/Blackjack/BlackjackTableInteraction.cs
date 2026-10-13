using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class BlackjackTableInteraction : MonoBehaviour
{
    private const int FixedBetPence = 2000;
    private static readonly int[] ChipDenominations = { 500, 1000, 2500 };

    [SerializeField] private CasinoPlayerController playerController;
    [SerializeField] private Transform interactionPosition;
    [SerializeField] private CasinoTableAttention attention;
    [SerializeField] private CasinoSeat[] seats;
    [SerializeField, Min(0.1f)] private float interactionDistance = 2f;

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
    private bool dealAttentionRequested;
    private string message = "Waiting for customers.";

    public bool IsConfigured =>
        playerController != null &&
        interactionPosition != null &&
        attention != null &&
        seats != null &&
        seats.Length == 3;

    public int WaitingCustomerCount => waitingSet.Count;

    public void Configure(CasinoPlayerController player, Transform dealerPosition, CasinoTableAttention tableAttention, CasinoSeat[] tableSeats)
    {
        playerController = player;
        interactionPosition = dealerPosition;
        attention = tableAttention;
        seats = tableSeats;
    }

    private void Start()
    {
        if (!IsConfigured)
        {
            Debug.LogError($"{nameof(BlackjackTableInteraction)} on {name} is missing its player, dealer position, attention system, or seats.", this);
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

        tableView.Configure(PerformDealerAction, ClosePanel, AddPayoutChip, RemovePayoutChip, ConfirmPayout);
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

        ResolveAllAttention(TableAttentionReason.DealCards);
        ResolveAllAttention(TableAttentionReason.PayoutRequired);
        ResolveAllAttention(TableAttentionReason.CustomersWaiting);
    }

    private void Update()
    {
        UpdateRoundProgress();

        Keyboard keyboard = Keyboard.current;
        if (panelOpen)
        {
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                ClosePanel();
            }
            else if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                PerformDealerAction();
            }

            return;
        }

        if (playerController == null || interactionPosition == null || tableView == null)
        {
            return;
        }

        bool inRange = IsPlayerInRange();
        bool hasAction = waitingSet.Count > 0 || state != BlackjackTableState.WaitingForCustomers;
        tableView.SetPromptVisible(inRange && hasAction, GetActionPrompt());
        if (inRange && hasAction && keyboard != null && keyboard.eKey.wasPressedThisFrame)
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
        attention.RequestAttention(TableAttentionReason.CustomersWaiting);
        message = state == BlackjackTableState.WaitingForCustomers
            ? $"{waitingSet.Count} customer(s) ready. Press Space to start the shared round."
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

        attention.ResolveAttention(TableAttentionReason.CustomersWaiting);
        RefreshView();
    }

    private void OpenPanel()
    {
        panelOpen = true;
        tableView.SetPromptVisible(false, "");
        tableView.SetVisible(true);
        playerController.SetMovementEnabled(false);

        if (state == BlackjackTableState.WaitingForCustomers && waitingSet.Count > 0)
        {
            BeginRound();
        }
        else
        {
            RefreshView();
        }
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
        Vector3 playerPosition = playerController.transform.position;
        Vector3 dealerPosition = interactionPosition.position;
        return Vector2.Distance(
            new Vector2(playerPosition.x, playerPosition.z),
            new Vector2(dealerPosition.x, dealerPosition.z)) <= interactionDistance;
    }

    private void BeginRound()
    {
        List<CasinoCustomerAI> participants = new List<CasinoCustomerAI>(3);
        while (waitingCustomers.Count > 0 && participants.Count < seats.Length)
        {
            CasinoCustomerAI customer = waitingCustomers.Dequeue();
            if (!waitingSet.Remove(customer))
            {
                continue;
            }

            attention.ResolveAttention(TableAttentionReason.CustomersWaiting);
            if (customer == null || customer.Seat == null || customer.Seat.Occupant != customer ||
                !customer.IsSeated || !customer.BeginService())
            {
                continue;
            }

            participants.Add(customer);
        }

        if (participants.Count == 0)
        {
            state = BlackjackTableState.WaitingForCustomers;
            message = "Waiting for customers.";
            RefreshView();
            return;
        }

        round = new BlackjackTableRound(FixedBetPence);
        foreach (CasinoCustomerAI customer in participants)
        {
            round.AddCustomer(customer, customer.Seat);
        }

        dealStep = 0;
        activeHandIndex = 0;
        payoutHandIndex = -1;
        selectedPayoutChips.Clear();
        awaitingCustomerCard = false;
        awaitingDealerCard = false;
        state = BlackjackTableState.TakingBets;
        message = $"£20 bet placed at each seat. Press Space to begin the opening deal.";
        RequestDealAttention();
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
                ResolveDealAttention();
                state = BlackjackTableState.InitialDeal;
                message = GetInitialDealPrompt();
                RequestDealAttention();
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
            ResolveDealAttention();
            CompleteInitialDeal();
            return;
        }

        dealStep++;
        ResolveDealAttention();
        message = GetInitialDealPrompt();
        RequestDealAttention();
        RefreshView();
    }

    private string GetInitialDealPrompt()
    {
        if (round == null || round.CustomerHands.Count == 0) return "Waiting for customers.";
        int customerCount = round.CustomerHands.Count;
        if (dealStep < customerCount) return $"Deal first card to {GetCustomerName(round.CustomerHands[dealStep])}.  •  SPACE";
        if (dealStep == customerCount) return "Deal the dealer's up-card.  •  SPACE";
        if (dealStep <= 2 * customerCount)
        {
            int handIndex = dealStep - customerCount - 1;
            return $"Deal second card to {GetCustomerName(round.CustomerHands[handIndex])}.  •  SPACE";
        }

        return "Deal the dealer's hidden card.  •  SPACE";
    }

    private void CompleteInitialDeal()
    {
        ResolveDealAttention();
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
            message = $"{GetCustomerName(hand)} requests a card. Deal it with Space.";
            RequestDealAttention();
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
        ResolveDealAttention();
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
        ResolveDealAttention();
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
            message = $"Dealer shows {round.DealerTotal}. Press Space to draw.";
            RequestDealAttention();
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

        ResolveDealAttention();
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
            message = $"Dealer draws to {round.DealerTotal}. Press Space to draw again.";
            RequestDealAttention();
            RefreshView();
        }
    }

    private void BeginSettlement()
    {
        ResolveDealAttention();
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
        for (int i = 0; i < round.CustomerHands.Count; i++)
        {
            BlackjackHand hand = round.CustomerHands[i];
            if (!hand.PayoutPaid && hand.PayoutPence > 0)
            {
                attention.RequestAttention(TableAttentionReason.PayoutRequired);
            }
        }

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
                    hand.Customer.CompleteVisit();
                }
            }
        }

        round = null;
        state = BlackjackTableState.WaitingForCustomers;
        message = "Waiting for the next customers.";
        ResolveAllAttention(TableAttentionReason.DealCards);
        ResolveAllAttention(TableAttentionReason.PayoutRequired);
        RefreshView();
        if (waitingSet.Count > 0)
        {
            BeginRound();
        }
    }

    private void RequestDealAttention()
    {
        if (dealAttentionRequested)
        {
            return;
        }

        dealAttentionRequested = true;
        attention.RequestAttention(TableAttentionReason.DealCards);
    }

    private void ResolveDealAttention()
    {
        if (!dealAttentionRequested)
        {
            return;
        }

        dealAttentionRequested = false;
        attention.ResolveAttention(TableAttentionReason.DealCards);
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
            message);
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
        if (state == BlackjackTableState.WaitingForCustomers) return "E  •  Start the shared blackjack round";
        if (state == BlackjackTableState.Settlement) return "E  •  Return to the table and finish payouts";
        if (state == BlackjackTableState.ClearingTable) return "E  •  The table is clearing";
        if (state == BlackjackTableState.TakingBets) return "E  •  Join the table and deal";
        if (state == BlackjackTableState.CustomerTurns && awaitingCustomerCard) return "E  •  A customer requests a card";
        if (state == BlackjackTableState.DealerTurn && awaitingDealerCard) return "E  •  Deal the dealer's next card";
        return "E  •  Resume blackjack table service";
    }

    private static bool ContainsDenomination(int denomination)
    {
        for (int i = 0; i < ChipDenominations.Length; i++)
        {
            if (ChipDenominations[i] == denomination) return true;
        }

        return false;
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
