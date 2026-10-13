using System;
using System.Collections.Generic;
using UnityEngine;

public enum CasinoFloatRefillStatus
{
    Requested,
    Packing,
    Prepared,
    Carrying,
    Delivered
}

[DisallowMultipleComponent]
public sealed class CasinoCashDeskInteraction : MonoBehaviour
{
    private static readonly int[] AvailablePackAmountsPence = { 1000, 2000, 3000, 4000, 5000 };

    [SerializeField] private CasinoTableAttention attention;
    [SerializeField] private CasinoMinigameStation minigameStation;
    [SerializeField] private CashDeskPackMinigame packMinigame;
    [SerializeField] private CasinoPlayerController player;
    [SerializeField, Min(0)] private int[] registerChipStock = { 500, 500, 500, 50 };
    [SerializeField] private CasinoFloatRefillDestination[] configuredDestinations;

    private readonly Queue<CasinoCustomerAI> waitingCustomers = new Queue<CasinoCustomerAI>();
    private readonly HashSet<CasinoCustomerAI> queuedCustomers = new HashSet<CasinoCustomerAI>();
    private readonly List<CasinoFloatRefillDestination> destinations = new List<CasinoFloatRefillDestination>();
    private readonly Dictionary<string, CasinoFloatRefillOrder> orders = new Dictionary<string, CasinoFloatRefillOrder>();
    private readonly Dictionary<string, CasinoFloatRefillStatus> orderStatuses = new Dictionary<string, CasinoFloatRefillStatus>();
    private readonly Dictionary<string, CasinoFloatRefillOrder> activeOrdersByDestination = new Dictionary<string, CasinoFloatRefillOrder>();
    private CasinoCustomerAI activeCustomer;
    private CasinoFloatRefillOrder activeOrder;
    private CasinoFloatRefillOrder carryingOrder;
    private CasinoPlayerFloatCarrier carrier;
    private int nextOrderSequence;

    public int WaitingCustomerCount => waitingCustomers.Count + (activeCustomer != null ? 1 : 0);
    public CasinoCustomerAI ActiveCustomer => activeCustomer;
    public IReadOnlyList<CasinoFloatRefillDestination> Destinations => destinations;
    public CasinoFloatRefillOrder ActiveOrder => activeOrder;
    public CasinoFloatRefillStatus GetOrderStatus(string orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId) ||
            !orderStatuses.TryGetValue(orderId, out CasinoFloatRefillStatus status))
        {
            throw new KeyNotFoundException($"Float refill order '{orderId}' does not exist.");
        }

        return status;
    }
    public int GetRegisterStock(int denominationIndex) =>
        denominationIndex >= 0 && denominationIndex < registerChipStock.Length ? registerChipStock[denominationIndex] : 0;
    public bool IsConfigured => attention != null && minigameStation != null &&
                                minigameStation.IsConfigured && packMinigame != null &&
                                player != null && registerChipStock != null && registerChipStock.Length == 4 &&
                                configuredDestinations != null && configuredDestinations.Length > 0;

    public void Configure(
        CasinoTableAttention tableAttention,
        CasinoMinigameStation station,
        CashDeskPackMinigame game)
    {
        attention = tableAttention;
        minigameStation = station;
        packMinigame = game;
        packMinigame?.SetRefillManager(this);
        BindStationEvents();
    }

    public void ConfigureFloatRefills(
        CasinoPlayerController casinoPlayer,
        params CasinoFloatRefillDestination[] refillDestinations)
    {
        player = casinoPlayer;
        configuredDestinations = refillDestinations != null
            ? (CasinoFloatRefillDestination[])refillDestinations.Clone()
            : null;
        BuildOrdersFromConfiguredDestinations();
        if (refillDestinations == null)
        {
            return;
        }

        carrier = player != null ? player.GetComponent<CasinoPlayerFloatCarrier>() : null;
        if (player != null && carrier == null)
        {
            carrier = player.gameObject.AddComponent<CasinoPlayerFloatCarrier>();
        }

        carrier?.Configure(this);
    }

    public void InitializeRuntime()
    {
        if (orders.Count == 0 && configuredDestinations != null)
        {
            BuildOrdersFromConfiguredDestinations();
        }

        packMinigame?.SetRefillManager(this);
        if (player == null)
        {
            return;
        }

        carrier = player.GetComponent<CasinoPlayerFloatCarrier>();
        if (carrier == null)
        {
            carrier = player.gameObject.AddComponent<CasinoPlayerFloatCarrier>();
        }

        carrier.Configure(this);
    }

    public IReadOnlyList<CasinoFloatRefillOrder> GetRequestedOrders()
    {
        List<CasinoFloatRefillOrder> result = new List<CasinoFloatRefillOrder>();
        foreach (KeyValuePair<string, CasinoFloatRefillOrder> pair in orders)
        {
            if (GetOrderStatus(pair.Key) == CasinoFloatRefillStatus.Requested &&
                HasRegisterStock(pair.Value.Counts))
            {
                result.Add(pair.Value);
            }
        }

        result.Sort((left, right) => string.CompareOrdinal(left.DestinationId, right.DestinationId));
        return result;
    }

    public bool TryBeginPacking(string orderId)
    {
        if (!orders.TryGetValue(orderId, out CasinoFloatRefillOrder order) ||
            activeOrder != null ||
            GetOrderStatus(orderId) != CasinoFloatRefillStatus.Requested ||
            !HasRegisterStock(order.Counts))
        {
            return false;
        }

        for (int i = 0; i < registerChipStock.Length; i++)
        {
            registerChipStock[i] -= order.Counts[i];
        }

        activeOrder = order;
        orderStatuses[orderId] = CasinoFloatRefillStatus.Packing;
        packMinigame.SetFloatOrder(order);
        return true;
    }

    public bool CancelPacking(string orderId)
    {
        if (activeOrder == null || activeOrder.Id != orderId ||
            GetOrderStatus(orderId) != CasinoFloatRefillStatus.Packing)
        {
            return false;
        }

        ReturnOrderToRequested(activeOrder, refundStock: true);
        activeOrder = null;
        return true;
    }

    public bool TryDeliver(CasinoPlayerFloatCarrier floatCarrier, CasinoFloatRefillDestination destination)
    {
        if (floatCarrier == null || destination == null || floatCarrier != carrier ||
            !floatCarrier.IsCarrying || carryingOrder == null ||
            GetOrderStatus(carryingOrder.Id) != CasinoFloatRefillStatus.Carrying ||
            destination != carryingOrder.Destination ||
            player == null || !destination.InteractionZone.IsPlayerInRange(player.transform) ||
            !destination.Receive(carryingOrder, carryingOrder))
        {
            return false;
        }

        orderStatuses[carryingOrder.Id] = CasinoFloatRefillStatus.Delivered;
        activeOrdersByDestination.Remove(carryingOrder.DestinationId);
        carryingOrder = null;
        floatCarrier.FinishDelivery();
        BeginNextPackIfReady();
        return true;
    }

    public void CustomerArrived(CasinoCustomerAI customer)
    {
        if (customer == null || !queuedCustomers.Add(customer))
        {
            return;
        }

        waitingCustomers.Enqueue(customer);
        BeginNextPackIfReady();
    }

    private void Start()
    {
        if (!IsConfigured)
        {
            Debug.LogError($"{nameof(CasinoCashDeskInteraction)} on {name} is missing its minigame, station, player, attention, or register stock references.", this);
            enabled = false;
            return;
        }

        BindStationEvents();
        InitializeRuntime();
    }

    private void Update()
    {
        if (!IsConfigured)
        {
            return;
        }

        RefreshFloatRequests();

        if (carryingOrder != null && GetOrderStatus(carryingOrder.Id) == CasinoFloatRefillStatus.Carrying &&
            carrier != null && !carrier.IsCarrying)
        {
            ReturnOrderToRequested(carryingOrder, refundStock: true);
            carryingOrder = null;
        }

        BeginNextPackIfReady();
    }

    public void RefreshFloatRequests()
    {
        for (int i = 0; i < destinations.Count; i++)
        {
            CasinoFloatRefillDestination destination = destinations[i];
            if (destination == null || !destination.NeedsRefill())
            {
                continue;
            }

            if (!activeOrdersByDestination.ContainsKey(destination.DestinationId))
            {
                string orderId = $"float-{destination.DestinationId}-{nextOrderSequence++}";
                CasinoFloatRefillOrder order = new CasinoFloatRefillOrder(
                    orderId,
                    destination.DestinationId,
                    destination,
                    destination.RefillOrderCounts);
                orders.Add(orderId, order);
                orderStatuses.Add(orderId, CasinoFloatRefillStatus.Requested);
                activeOrdersByDestination.Add(destination.DestinationId, order);
                destination.RequestAttention();
            }
        }
    }

    private void OnDestroy()
    {
        if (minigameStation != null)
        {
            minigameStation.TaskSucceeded -= OnPackCompleted;
            minigameStation.TaskAbandoned -= OnPackAbandoned;
        }
    }

    private void OnPackCompleted(CasinoMinigameResult result)
    {
        if (activeOrder != null && GetOrderStatus(activeOrder.Id) == CasinoFloatRefillStatus.Packing)
        {
            bool beganCarrying = false;
            if (packMinigame.PackedOrder == activeOrder &&
                activeOrder.HasExactContents(packMinigame.PackedCounts) &&
                carrier != null)
            {
                orderStatuses[activeOrder.Id] = CasinoFloatRefillStatus.Prepared;
                if (carrier.BeginCarrying(activeOrder))
                {
                    orderStatuses[activeOrder.Id] = CasinoFloatRefillStatus.Carrying;
                    beganCarrying = true;
                }
                else
                {
                    ReturnOrderToRequested(activeOrder, refundStock: true);
                }
            }
            else
            {
                ReturnOrderToRequested(activeOrder, refundStock: true);
            }

            if (beganCarrying)
            {
                carryingOrder = activeOrder;
            }

            activeOrder = null;
        }
        else if (activeCustomer != null)
        {
            activeCustomer.CompleteCashierVisit();
            queuedCustomers.Remove(activeCustomer);
            activeCustomer = null;
        }
    }

    private void OnPackAbandoned()
    {
        if (activeOrder != null && GetOrderStatus(activeOrder.Id) == CasinoFloatRefillStatus.Packing)
        {
            CancelPacking(activeOrder.Id);
        }

        BeginNextPackIfReady();
    }

    private void BeginNextPackIfReady()
    {
        if (!IsConfigured || minigameStation.IsTaskPending || minigameStation.IsMinigameRunning ||
            carryingOrder != null)
        {
            return;
        }

        IReadOnlyList<CasinoFloatRefillOrder> requestedOrders = GetRequestedOrders();
        if (requestedOrders.Count > 0)
        {
            packMinigame.SetAvailableFloatOrders(requestedOrders);
            minigameStation.RequestMinigame(CasinoMinigameType.Pack, TableAttentionReason.CashDeskPackRequired);
            return;
        }

        if (activeCustomer == null)
        {
            while (waitingCustomers.Count > 0 && activeCustomer == null)
            {
                CasinoCustomerAI customer = waitingCustomers.Dequeue();
                if (customer != null && customer.IsWaitingAtCashDesk)
                {
                    activeCustomer = customer;
                }
                else
                {
                    queuedCustomers.Remove(customer);
                }
            }
        }

        if (activeCustomer == null)
        {
            return;
        }

        packMinigame.SetCashOutAmount(AvailablePackAmountsPence[UnityEngine.Random.Range(0, AvailablePackAmountsPence.Length)]);
        minigameStation.RequestMinigame(CasinoMinigameType.Pack, TableAttentionReason.CashDeskPackRequired);
    }

    private void ReturnOrderToRequested(CasinoFloatRefillOrder order, bool refundStock)
    {
        if (order == null)
        {
            return;
        }

        if (refundStock)
        {
            for (int i = 0; i < registerChipStock.Length; i++)
            {
                registerChipStock[i] += order.Counts[i];
            }
        }

        orderStatuses[order.Id] = CasinoFloatRefillStatus.Requested;
    }

    private bool HasRegisterStock(int[] requestedCounts)
    {
        if (requestedCounts == null || requestedCounts.Length != registerChipStock.Length)
        {
            return false;
        }

        for (int i = 0; i < registerChipStock.Length; i++)
        {
            if (requestedCounts[i] > registerChipStock[i])
            {
                return false;
            }
        }

        return true;
    }

    private void BindStationEvents()
    {
        if (minigameStation == null)
        {
            return;
        }

        minigameStation.TaskSucceeded -= OnPackCompleted;
        minigameStation.TaskSucceeded += OnPackCompleted;
        minigameStation.TaskAbandoned -= OnPackAbandoned;
        minigameStation.TaskAbandoned += OnPackAbandoned;
    }

    private void BuildOrdersFromConfiguredDestinations()
    {
        destinations.Clear();
        orders.Clear();
        orderStatuses.Clear();
        activeOrdersByDestination.Clear();
        nextOrderSequence = 0;
        if (configuredDestinations == null)
        {
            return;
        }

        for (int i = 0; i < configuredDestinations.Length; i++)
        {
            CasinoFloatRefillDestination destination = configuredDestinations[i];
            if (destination == null || !destination.IsConfigured)
            {
                throw new InvalidOperationException("All float refill destinations must be configured.");
            }

            if (destinations.Exists(existing => existing.DestinationId == destination.DestinationId))
            {
                throw new InvalidOperationException($"Duplicate float destination id '{destination.DestinationId}'.");
            }

            destinations.Add(destination);
        }
    }
}
