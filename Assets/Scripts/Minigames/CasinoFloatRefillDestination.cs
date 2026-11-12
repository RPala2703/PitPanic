using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CasinoFloatRefillDestination : MonoBehaviour
{
    [SerializeField] private string destinationId;
    [SerializeField] private string displayName;
    [SerializeField] private TableInteractionZone interactionZone;
    [SerializeField] private CasinoTableAttention attention;
    [SerializeField, Min(0)] private int[] startingChipCounts = { 12, 32, 4, 2 };
    [SerializeField, Min(0)] private int[] refillThresholdCounts = { 3, 8, 1, 0 };
    [SerializeField, Min(0)] private int[] refillOrderCounts = { 10, 20, 4, 0 };

    private readonly int[] chipCounts = new int[4];
    private readonly HashSet<string> receivedOrderIds = new HashSet<string>();
    private bool refillRequested;

    public string DestinationId => destinationId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public TableInteractionZone InteractionZone => interactionZone;
    public CasinoTableAttention Attention => attention;
    public bool HasHealthyStartingInventory
    {
        get
        {
            if (startingChipCounts == null || refillThresholdCounts == null ||
                refillOrderCounts == null || startingChipCounts.Length != 4 ||
                refillThresholdCounts.Length != 4 || refillOrderCounts.Length != 4)
            {
                return false;
            }

            for (int i = 0; i < startingChipCounts.Length; i++)
            {
                if (refillOrderCounts[i] > 0 && startingChipCounts[i] <= refillThresholdCounts[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
    public bool IsConfigured => !string.IsNullOrWhiteSpace(destinationId) &&
                                interactionZone != null && interactionZone.IsConfigured &&
                                attention != null && startingChipCounts != null &&
                                startingChipCounts.Length == 4 && refillThresholdCounts != null &&
                                refillThresholdCounts.Length == 4 && refillOrderCounts != null &&
                                refillOrderCounts.Length == 4;
    public int[] RefillOrderCounts => (int[])refillOrderCounts.Clone();
    public long InventoryValuePence =>
        (long)chipCounts[0] * 500 + (long)chipCounts[1] * 250 +
        (long)chipCounts[2] * 2500 + (long)chipCounts[3] * 10000;

    public void Configure(
        string id,
        string label,
        TableInteractionZone zone,
        CasinoTableAttention tableAttention,
        int[] initialCounts = null,
        int[] orderCounts = null)
    {
        destinationId = id;
        displayName = label;
        interactionZone = zone;
        attention = tableAttention;
        if (initialCounts != null)
        {
            if (initialCounts.Length != 4)
            {
                throw new ArgumentException("Float inventory requires counts for red, blue, green and black chips.", nameof(initialCounts));
            }

            startingChipCounts = (int[])initialCounts.Clone();
        }

        if (orderCounts != null)
        {
            if (orderCounts.Length != 4)
            {
                throw new ArgumentException("Float orders require counts for red, blue, green and black chips.", nameof(orderCounts));
            }

            refillOrderCounts = (int[])orderCounts.Clone();
        }

        Array.Copy(startingChipCounts, chipCounts, chipCounts.Length);
    }

    public int GetChipCount(int denominationIndex)
    {
        if (denominationIndex < 0 || denominationIndex >= chipCounts.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(denominationIndex));
        }

        return chipCounts[denominationIndex];
    }

    public bool TryConsumeChips(int[] requestedCounts)
    {
        if (requestedCounts == null || requestedCounts.Length != chipCounts.Length)
        {
            return false;
        }

        for (int i = 0; i < chipCounts.Length; i++)
        {
            if (requestedCounts[i] < 0 || requestedCounts[i] > chipCounts[i])
            {
                return false;
            }
        }

        for (int i = 0; i < chipCounts.Length; i++)
        {
            chipCounts[i] -= requestedCounts[i];
        }

        if (NeedsRefill()) RequestAttention();
        return true;
    }

    public bool TryAddChips(int[] addedCounts)
    {
        if (addedCounts == null || addedCounts.Length != chipCounts.Length)
            return false;
        for (int i = 0; i < chipCounts.Length; i++)
            if (addedCounts[i] < 0 || (long)chipCounts[i] + addedCounts[i] > int.MaxValue)
                return false;
        for (int i = 0; i < chipCounts.Length; i++)
            chipCounts[i] += addedCounts[i];
        return true;
    }

    public bool TryGetAvailableComposition(int amountPence, out int[] composition)
    {
        composition = new int[chipCounts.Length];
        if (amountPence <= 0 || amountPence % 250 != 0 ||
            amountPence / 250 > InventoryValuePence / 250)
            return false;

        int target = amountPence / 250;
        int[] values = { 2, 1, 10, 40 };
        var failedStates = new HashSet<long>();
        return FindComposition(0, target, values, composition, failedStates);
    }

    private bool FindComposition(int denomination, int remaining, int[] values,
        int[] composition, HashSet<long> failedStates)
    {
        if (denomination == values.Length) return remaining == 0;
        long state = ((long)denomination << 32) | (uint)remaining;
        if (failedStates.Contains(state)) return false;

        int value = values[denomination];
        int maxCount = Mathf.Min(chipCounts[denomination], remaining / value);
        for (int count = maxCount; count >= 0; count--)
        {
            composition[denomination] = count;
            if (FindComposition(denomination + 1, remaining - count * value,
                    values, composition, failedStates))
                return true;
        }

        composition[denomination] = 0;
        failedStates.Add(state);
        return false;
    }

    public bool NeedsRefill()
    {
        if (refillRequested)
        {
            return true;
        }

        for (int i = 0; i < chipCounts.Length; i++)
        {
            if (refillOrderCounts[i] > 0 && chipCounts[i] <= refillThresholdCounts[i])
            {
                return true;
            }
        }

        return false;
    }

    public bool Receive(CasinoFloatRefillOrder order, IReadOnlyIntList counts)
    {
        if (!IsConfigured || order == null || counts == null ||
            order.Destination != this || !order.HasExactContents(counts) ||
            receivedOrderIds.Contains(order.Id))
        {
            return false;
        }

        for (int i = 0; i < chipCounts.Length; i++)
        {
            chipCounts[i] += counts[i];
        }

        receivedOrderIds.Add(order.Id);
        refillRequested = false;
        attention.ResolveAttention(TableAttentionReason.FloatRefillRequired);
        return true;
    }

    public bool Receive(CasinoFloatRefillOrder order, int[] counts)
    {
        if (!IsConfigured || order == null || counts == null ||
            order.Destination != this || !order.HasExactContents(counts) ||
            receivedOrderIds.Contains(order.Id))
        {
            return false;
        }

        for (int i = 0; i < chipCounts.Length; i++)
        {
            chipCounts[i] += counts[i];
        }

        receivedOrderIds.Add(order.Id);
        refillRequested = false;
        attention.ResolveAttention(TableAttentionReason.FloatRefillRequired);
        return true;
    }

    public void RequestAttention()
    {
        refillRequested = true;
        if (attention != null)
        {
            attention.RequestAttentionOnce(TableAttentionReason.FloatRefillRequired);
        }
    }

    public void OnValidate()
    {
        if (startingChipCounts == null || startingChipCounts.Length != 4)
        {
            startingChipCounts = new int[4];
        }

        if (refillThresholdCounts == null || refillThresholdCounts.Length != 4)
        {
            refillThresholdCounts = new int[4];
        }

        if (refillOrderCounts == null || refillOrderCounts.Length != 4)
        {
            refillOrderCounts = new int[4];
        }
    }

    private void Awake()
    {
        if (startingChipCounts != null && startingChipCounts.Length == chipCounts.Length)
        {
            Array.Copy(startingChipCounts, chipCounts, chipCounts.Length);
        }
    }
}
