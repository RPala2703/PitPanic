using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Tuning for customer tips. Every amount is integer pence and tips are always real chip values.</summary>
[Serializable]
public sealed class CasinoTipPolicy
{
    [Range(0, 1)] public float MinimumSatisfaction = .45f;
    [Range(0, 1)] public float ChanceAtFullSatisfaction = .55f;
    [Range(0, 1)] public float WinChanceBonus = .2f;
    [Range(0, 1)] public float VipChanceBonus = .2f;
    public int[] RegularTipsPence = { 250, 500 };
    public int[] VipTipsPence = { 500, 2500 };
    public int StarBonusTargetPence = 2000;

    public static CasinoTipPolicy Default { get; } = new CasinoTipPolicy();

    /// <summary>
    /// Pure tip decision. Returns 0 when the customer is unhappy, can't afford it, or decides not to tip.
    /// Happier customers tip more often and pick bigger chips; VIPs tip more often and in bigger chips.
    /// </summary>
    public int CalculateTipPence(float satisfaction, bool vip, bool won, int bankrollPence, float chanceRoll, float sizeRoll)
    {
        satisfaction = Mathf.Clamp01(satisfaction);
        if (satisfaction < MinimumSatisfaction || bankrollPence <= 0) return 0;
        float mood = Mathf.InverseLerp(MinimumSatisfaction, 1f, satisfaction);
        float chance = ChanceAtFullSatisfaction * mood + (won ? WinChanceBonus : 0) * mood + (vip ? VipChanceBonus : 0);
        if (chanceRoll >= Mathf.Clamp01(chance)) return 0;
        int[] options = vip ? VipTipsPence : RegularTipsPence;
        if (options == null || options.Length == 0) return 0;
        // The bigger chip needs a really good experience (or a win to celebrate).
        float generosity = mood * .75f + (won ? .25f : 0);
        int index = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(sizeRoll * generosity + generosity * .35f) * options.Length),
            0, options.Length - 1);
        for (int i = index; i >= 0; i--)
            if (options[i] > 0 && options[i] <= bankrollPence) return options[i];
        return 0;
    }

    public int StarBonus(int tipsPence) => tipsPence >= StarBonusTargetPence ? 10 : tipsPence * 2 >= StarBonusTargetPence ? 5 : 0;
}

public readonly struct CasinoTipRecord
{
    public readonly string ServiceKey;
    public readonly int AmountPence;
    public readonly bool Vip;
    public readonly bool ToPitBoss;
    public readonly CasinoCustomerAI Customer;

    public CasinoTipRecord(string serviceKey, int amountPence, bool vip, bool toPitBoss, CasinoCustomerAI customer)
    {
        ServiceKey = serviceKey;
        AmountPence = amountPence;
        Vip = vip;
        ToPitBoss = toPitBoss;
        Customer = customer;
    }
}

/// <summary>
/// The shared tip jar at the Pit Desk. It is deliberately separate from casino profit and table floats:
/// tips are the staff's money, so recording one never touches accounting or chip inventories.
/// </summary>
public static class CasinoTipJar
{
    private static readonly HashSet<string> ServiceKeys = new HashSet<string>();
    public static int TotalPence { get; private set; }
    public static int TipCount { get; private set; }
    public static int VipTipCount { get; private set; }
    public static int PitBossPence { get; private set; }
    public static event Action<CasinoTipRecord> TipRecorded;

    public static void ResetShift()
    {
        ServiceKeys.Clear();
        TotalPence = 0;
        TipCount = 0;
        VipTipCount = 0;
        PitBossPence = 0;
    }

    public static bool HasTipped(string serviceKey) => serviceKey != null && ServiceKeys.Contains(serviceKey);

    /// <summary>Records a tip once per service event. Replays of the same key are ignored.</summary>
    public static bool TryRecord(CasinoTipRecord tip)
    {
        if (string.IsNullOrEmpty(tip.ServiceKey) || tip.AmountPence <= 0 || !ServiceKeys.Add(tip.ServiceKey)) return false;
        TotalPence = checked(TotalPence + tip.AmountPence);
        TipCount++;
        if (tip.Vip) VipTipCount++;
        if (tip.ToPitBoss) PitBossPence = checked(PitBossPence + tip.AmountPence);
        TipRecorded?.Invoke(tip);
        return true;
    }

    public static string Format(int pence) => pence % 100 == 0 ? $"£{pence / 100}" : $"£{pence / 100}.{pence % 100:00}";
}

public static class CasinoTipping
{
    /// <summary>
    /// Gameplay-authoritative tip decision for one service event (a settled hand or bet). The tip is
    /// committed here exactly once; animations only mirror the returned record.
    /// </summary>
    public static bool TryTip(CasinoCustomerAI customer, string serviceKey, bool won, bool toPitBoss, out CasinoTipRecord tip)
    {
        tip = default;
        if (customer == null || CasinoTipJar.HasTipped(serviceKey)) return false;
        int amount = CasinoTipPolicy.Default.CalculateTipPence(customer.Satisfaction, customer.IsVip, won,
            customer.BankrollPence, UnityEngine.Random.value, UnityEngine.Random.value);
        if (amount <= 0 || !customer.TrySpendTip(amount)) return false;
        tip = new CasinoTipRecord(serviceKey, amount, customer.IsVip, toPitBoss, customer);
        if (CasinoTipJar.TryRecord(tip)) return true;
        customer.RefundTip(amount);
        return false;
    }
}
