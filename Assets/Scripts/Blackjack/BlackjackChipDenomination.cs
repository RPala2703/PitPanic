using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct BlackjackChipDenomination
{
    [SerializeField] private string label;
    [SerializeField, Min(1)] private int valuePence;
    [SerializeField] private Color color;

    public string Label => label;
    public int ValuePence => valuePence;
    public Color Color => color;

    public BlackjackChipDenomination(string denominationLabel, int denominationValuePence, Color denominationColor)
    {
        label = denominationLabel;
        valuePence = denominationValuePence;
        color = denominationColor;
    }

    public static BlackjackChipDenomination[] CreateDefaults()
    {
        return new[]
        {
            new BlackjackChipDenomination("£5", 500, new Color(0.81f, 0.16f, 0.13f)),
            new BlackjackChipDenomination("£10", 1000, new Color(0.16f, 0.43f, 0.78f)),
            new BlackjackChipDenomination("£25", 2500, new Color(0.20f, 0.62f, 0.34f))
        };
    }

    public static List<BlackjackChipDenomination> GetExactComposition(
        int amountPence,
        IReadOnlyList<BlackjackChipDenomination> denominations)
    {
        if (amountPence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amountPence));
        }

        if (denominations == null || denominations.Count == 0)
        {
            throw new ArgumentException("At least one chip denomination is required.", nameof(denominations));
        }

        var ordered = new List<BlackjackChipDenomination>(denominations.Count);
        for (int i = 0; i < denominations.Count; i++)
        {
            if (denominations[i].valuePence <= 0)
            {
                throw new ArgumentException("Chip denomination values must be positive.", nameof(denominations));
            }

            ordered.Add(denominations[i]);
        }

        ordered.Sort((left, right) => right.valuePence.CompareTo(left.valuePence));
        var result = new List<BlackjackChipDenomination>();
        int firstDenomination = amountPence < ordered[0].valuePence && ordered.Count > 1
            ? ordered.Count - 1
            : 0;
        if (!TryCompose(amountPence, firstDenomination, ordered, result))
        {
            throw new InvalidOperationException($"No exact chip composition exists for {amountPence} pence.");
        }

        return result;
    }

    private static bool TryCompose(
        int remainingPence,
        int denominationIndex,
        IReadOnlyList<BlackjackChipDenomination> denominations,
        List<BlackjackChipDenomination> result)
    {
        if (remainingPence == 0)
        {
            return true;
        }

        if (denominationIndex >= denominations.Count)
        {
            return false;
        }

        BlackjackChipDenomination denomination = denominations[denominationIndex];
        int maximumCount = remainingPence / denomination.valuePence;
        for (int count = maximumCount; count >= 0; count--)
        {
            int resultCount = result.Count;
            for (int i = 0; i < count; i++)
            {
                result.Add(denomination);
            }

            if (TryCompose(remainingPence - count * denomination.valuePence, denominationIndex + 1, denominations, result))
            {
                return true;
            }

            result.RemoveRange(resultCount, result.Count - resultCount);
        }

        return false;
    }
}
