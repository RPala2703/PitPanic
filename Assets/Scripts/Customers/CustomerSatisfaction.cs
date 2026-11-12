using UnityEngine;

public static class CustomerSatisfaction
{
    public static int PenaltyCount { get; private set; }

    public static void RecordPenalty(CasinoCustomerAI customer) => RecordPenalty(customer, 1);

    public static void RecordPenalty(CasinoCustomerAI customer, int weight)
    {
        if (customer == null) throw new System.ArgumentNullException(nameof(customer));
        if (weight < 1) throw new System.ArgumentOutOfRangeException(nameof(weight));
        PenaltyCount = checked(PenaltyCount + weight);
        Debug.Log($"Customer satisfaction penalty: {customer.name} left after running out of patience. " +
            $"Impact: {weight}. Total weighted penalties: {PenaltyCount}.");
    }
}
