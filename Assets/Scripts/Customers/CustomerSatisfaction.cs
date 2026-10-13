using UnityEngine;

public static class CustomerSatisfaction
{
    public static int PenaltyCount { get; private set; }

    public static void RecordPenalty(CasinoCustomerAI customer)
    {
        PenaltyCount++;
        Debug.Log($"Customer satisfaction penalty: {customer.name} left after running out of patience. Total penalties: {PenaltyCount}.");
    }
}
