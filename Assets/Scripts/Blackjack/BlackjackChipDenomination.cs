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
            new BlackjackChipDenomination("£2.50", 250, new Color(0.16f, 0.43f, 0.78f)),
            new BlackjackChipDenomination("£25", 2500, new Color(0.20f, 0.62f, 0.34f)),
            new BlackjackChipDenomination("£100", 10000, new Color(0.08f, 0.09f, 0.11f))
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
        if (amountPence == 0)
        {
            return new List<BlackjackChipDenomination>();
        }

        int[] minimumChipCounts = new int[checked(amountPence + 1)];
        int[] lastDenomination = new int[minimumChipCounts.Length];
        for (int value = 1; value < minimumChipCounts.Length; value++)
        {
            minimumChipCounts[value] = int.MaxValue;
        }

        for (int value = 1; value < minimumChipCounts.Length; value++)
        {
            for (int denominationIndex = 0; denominationIndex < ordered.Count; denominationIndex++)
            {
                int denominationValue = ordered[denominationIndex].valuePence;
                if (denominationValue <= value &&
                    minimumChipCounts[value - denominationValue] != int.MaxValue &&
                    minimumChipCounts[value - denominationValue] + 1 < minimumChipCounts[value])
                {
                    minimumChipCounts[value] = minimumChipCounts[value - denominationValue] + 1;
                    lastDenomination[value] = denominationIndex;
                }
            }
        }

        if (minimumChipCounts[amountPence] == int.MaxValue)
        {
            throw new InvalidOperationException($"No exact chip composition exists for {amountPence} pence.");
        }

        var result = new List<BlackjackChipDenomination>(minimumChipCounts[amountPence]);
        int remainingPence = amountPence;
        while (remainingPence > 0)
        {
            BlackjackChipDenomination denomination = ordered[lastDenomination[remainingPence]];
            result.Add(denomination);
            remainingPence -= denomination.valuePence;
        }

        result.Sort((left, right) => right.valuePence.CompareTo(left.valuePence));
        return result;
    }
}
