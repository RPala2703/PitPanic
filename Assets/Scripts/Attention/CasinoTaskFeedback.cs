using UnityEngine;

public static class CasinoTaskFeedback
{
    public static Color UrgencyColor(float fraction) => fraction > .5f
        ? new Color(.3f, .95f, .45f) : fraction > .2f ? CasinoHudStyle.Gold : CasinoHudStyle.Coral;

    public static bool TryGetDeadline(CasinoTableAttention attention, out float remaining, out float duration)
    {
        remaining = float.PositiveInfinity;
        duration = 0;
        if (attention == null) return false;
        CasinoMinigame game = attention.GetComponent<CasinoMinigame>();
        if (game != null && game.TryGetDeadline(out float taskTime, out float taskDuration))
        { remaining = taskTime; duration = taskDuration; }
        foreach (CasinoCustomerAI customer in Object.FindObjectsByType<CasinoCustomerAI>(FindObjectsSortMode.None))
        {
            CustomerPatience patience = customer.GetComponent<CustomerPatience>();
            bool here = customer.Seat?.DealerStation?.Attention == attention ||
                customer.IsWaitingAtCashDesk && attention.GetComponent<CasinoCashDeskInteraction>() != null;
            if (here && patience != null && patience.IsWaiting && patience.Remaining < remaining)
            { remaining = patience.Remaining; duration = patience.Duration; }
        }
        AIDealer dealer = attention.GetComponent<CasinoDealerStation>()?.AssignedDealer;
        if (dealer != null && !dealer.TrainingProtected && dealer.NeedsBreak && dealer.Energy > 0)
        {
            float time = dealer.SecondsUntilExhausted;
            if (time < remaining) { remaining = time; duration = dealer.ExhaustionWarningDuration; }
        }
        return !float.IsInfinity(remaining) && duration > 0;
    }

    public static bool TryGetDeadline(CasinoTableAttention attention, TableAttentionReason reason, out float remaining, out float duration)
    {
        remaining = duration = 0;
        if (reason == TableAttentionReason.FloatRefillRequired || reason == TableAttentionReason.ChipRefillRequired ||
            reason == TableAttentionReason.TableUnstaffed || reason == TableAttentionReason.DealerExhausted ||
            reason == TableAttentionReason.DealerReadyForAssignment || reason == TableAttentionReason.StaffRotationRequired)
            return false;
        return TryGetDeadline(attention, out remaining, out duration);
    }

    public static string Message(CasinoTableAttention attention)
    {
        if (attention == null || !attention.HasPendingTasks) return "";
        if (attention.GetPendingCount(TableAttentionReason.DealerExhausted) > 0 ||
            attention.GetPendingCount(TableAttentionReason.TableUnstaffed) > 0)
            return "E: Dealer unavailable - TAKE OVER, or assign cover at Pit Desk!";
        if (attention.GetPendingCount(TableAttentionReason.FloatRefillRequired) > 0 ||
            attention.GetPendingCount(TableAttentionReason.ChipRefillRequired) > 0)
        {
            CasinoFloatRefillDestination destination = attention.GetComponent<CasinoFloatRefillDestination>();
            if (destination != null)
            {
                CasinoFloatRefillOrder order = new CasinoFloatRefillOrder("preview", destination.DestinationId,
                    destination, destination.RefillOrderCounts);
                return $"Cash Desk E: PACK {RouletteRoundController.FormatPounds(order.TotalPence)}, then E: deliver here!";
            }
            return "Cash Desk E: prepare and deliver a float tray!";
        }
        if (attention.GetPendingCount(TableAttentionReason.DealerBreakRequired) > 0)
            return "Pit Desk E: assign cover, then send the tired dealer on break!";
        if (attention.GetPendingCount(TableAttentionReason.FlickAssistanceRequired) > 0 ||
            attention.GetPendingCount(TableAttentionReason.DealCards) > 0)
            return "E: Blackjack needs SHUFFLE! Click the shoe to deal each opening card.";
        if (attention.GetPendingCount(TableAttentionReason.RouletteCoverRequired) > 0)
            return "E: Roulette needs COVER! Spin, sweep losing bets, then pay the winners.";
        if (attention.GetPendingCount(TableAttentionReason.RouletteSpinRequired) > 0 ||
            attention.GetPendingCount(TableAttentionReason.RouletteSweepRequired) > 0)
            return "E: Roulette needs SPIN! Clear losers, then pay winners exactly.";
        if (attention.GetPendingCount(TableAttentionReason.CashDeskPackRequired) > 0)
            return "E: PACK! Click chips to match the order. Undo removes the last chip.";
        if (attention.GetPendingCount(TableAttentionReason.DealerReadyForAssignment) > 0 ||
            attention.GetPendingCount(TableAttentionReason.StaffRotationRequired) > 0)
            return "Pit Desk E: dealer waiting - assign a table or send on break!";
        if (attention.GetPendingCount(TableAttentionReason.PayoutRequired) > 0)
            return "E: pay the exact winnings shown to finish the round!";
        return "E: customers are waiting for table service!";
    }
}
