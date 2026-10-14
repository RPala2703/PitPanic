using System;
using System.Collections.Generic;

public sealed class CasinoFloatRefillOrder : IReadOnlyIntList
{
    public const int RedChipPence = 500;
    public const int BlueChipPence = 250;
    public const int GreenChipPence = 2500;
    public const int BlackChipPence = 10000;
    public const int RedChipCount = 10;
    public const int BlueChipCount = 5;
    public const int GreenChipCount = 4;
    public const int BlackChipCount = 0;

    private readonly int[] counts;

    public string Id { get; }
    public string DestinationId { get; }
    public CasinoFloatRefillDestination Destination { get; }
    public int[] Counts => (int[])counts.Clone();
    public int TotalPence => counts[0] * RedChipPence +
                             counts[1] * BlueChipPence +
                             counts[2] * GreenChipPence +
                             counts[3] * BlackChipPence;
    public int TotalChips => counts[0] + counts[1] + counts[2] + counts[3];
    int IReadOnlyIntList.Count => counts.Length;
    int IReadOnlyIntList.this[int index] => counts[index];

    public CasinoFloatRefillOrder(string id, string destinationId, CasinoFloatRefillDestination destination, int[] counts)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(destinationId) ||
            destination == null || counts == null || counts.Length != 4)
        {
            throw new ArgumentException("A float refill order needs an id, destination, and four denomination counts.");
        }

        for (int i = 0; i < counts.Length; i++)
        {
            if (counts[i] < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(counts), "Chip counts cannot be negative.");
            }
        }

        Id = id;
        DestinationId = destinationId;
        Destination = destination;
        this.counts = (int[])counts.Clone();
    }

    public static CasinoFloatRefillOrder CreateStandard(
        string id,
        string destinationId,
        CasinoFloatRefillDestination destination)
    {
        return new CasinoFloatRefillOrder(
            id,
            destinationId,
            destination,
            new[] { RedChipCount, BlueChipCount, GreenChipCount, BlackChipCount });
    }

    public int CountFor(int denominationIndex)
    {
        if (denominationIndex < 0 || denominationIndex >= counts.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(denominationIndex));
        }

        return counts[denominationIndex];
    }

    public bool HasExactContents(IReadOnlyIntList counts)
    {
        if (counts == null || counts.Count != this.counts.Length)
        {
            return false;
        }

        for (int i = 0; i < this.counts.Length; i++)
        {
            if (counts[i] != this.counts[i])
            {
                return false;
            }
        }

        return true;
    }

    public bool HasExactContents(int[] counts)
    {
        if (counts == null || counts.Length != this.counts.Length)
        {
            return false;
        }

        for (int i = 0; i < this.counts.Length; i++)
        {
            if (counts[i] != this.counts[i])
            {
                return false;
            }
        }

        return true;
    }

    public int GetDenominationPence(int index)
    {
        switch (index)
        {
            case 0: return RedChipPence;
            case 1: return BlueChipPence;
            case 2: return GreenChipPence;
            case 3: return BlackChipPence;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}

public interface IReadOnlyIntList
{
    int Count { get; }
    int this[int index] { get; }
}
