using UnityEngine;

public static class CasinoTaskFeedback
{
    public static Color UrgencyColor(float fraction) => fraction > .5f
        ? new Color(.3f, .95f, .45f) : fraction > .2f ? CasinoHudStyle.Gold : CasinoHudStyle.Coral;

    public static bool TryGetDeadline(CasinoTableAttention attention, out float remaining, out float duration)
    {
        remaining = float.PositiveInfinity;
        duration = 0;
        if (attention == null || !attention.HasVisibleTasks) return false;
        foreach (TableAttentionReason reason in System.Enum.GetValues(typeof(TableAttentionReason)))
            if (TryGetDeadline(attention, reason, out float time, out float total) && time < remaining)
            { remaining = time; duration = total; }
        return !float.IsInfinity(remaining) && duration > 0;
    }

    public static bool TryGetDeadline(CasinoTableAttention attention, TableAttentionReason reason, out float remaining, out float duration)
    {
        remaining = float.PositiveInfinity;
        duration = 0;
        if (attention == null || attention.GetVisiblePendingCount(reason) <= 0) return false;
        if (reason == TableAttentionReason.DealerBreakRequired)
        {
            AIDealer dealer = attention.GetComponent<CasinoDealerStation>()?.AssignedDealer;
            if (dealer == null || dealer.TrainingProtected || !dealer.NeedsBreak || dealer.Energy <= 0) return false;
            remaining = dealer.SecondsUntilExhausted;
            duration = dealer.ExhaustionWarningDuration;
            return duration > 0;
        }
        bool service = reason == TableAttentionReason.ShuffleAssistanceRequired ||
            reason == TableAttentionReason.DealCards || reason == TableAttentionReason.RouletteCoverRequired ||
            reason == TableAttentionReason.RouletteSpinRequired || reason == TableAttentionReason.RouletteSweepRequired ||
            reason == TableAttentionReason.PayoutRequired || reason == TableAttentionReason.CashDeskPackRequired ||
            reason == TableAttentionReason.CustomersWaiting || reason == TableAttentionReason.DealerExhausted;
        if (!service) return false;
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
        return !float.IsInfinity(remaining) && duration > 0;
    }

    public static string Message(CasinoTableAttention attention)
    {
        if (attention == null || !attention.HasVisibleTasks) return "";
        if (attention.GetVisiblePendingCount(TableAttentionReason.DealerExhausted) > 0 ||
            attention.GetVisiblePendingCount(TableAttentionReason.TableUnstaffed) > 0)
            return "Dealer needs break";
        if (attention.GetVisiblePendingCount(TableAttentionReason.FloatRefillRequired) > 0 ||
            attention.GetVisiblePendingCount(TableAttentionReason.ChipRefillRequired) > 0)
        {
            return "Float refill";
        }
        if (attention.GetVisiblePendingCount(TableAttentionReason.DealerBreakRequired) > 0)
            return "Dealer needs break";
        if (attention.GetVisiblePendingCount(TableAttentionReason.ShuffleAssistanceRequired) > 0 ||
            attention.GetVisiblePendingCount(TableAttentionReason.DealCards) > 0)
            return "Shuffle cards";
        if (attention.GetVisiblePendingCount(TableAttentionReason.RouletteCoverRequired) > 0)
            return "Cover winners";
        if (attention.GetVisiblePendingCount(TableAttentionReason.RouletteSpinRequired) > 0 ||
            attention.GetVisiblePendingCount(TableAttentionReason.RouletteSweepRequired) > 0)
            return "Roulette takeover";
        if (attention.GetVisiblePendingCount(TableAttentionReason.SecurityReviewRequired) > 0)
            return "Review CCTV";
        if (attention.GetVisiblePendingCount(TableAttentionReason.CashDeskPackRequired) > 0)
            return "Pack chips";
        if (attention.GetVisiblePendingCount(TableAttentionReason.DealerReadyForAssignment) > 0 ||
            attention.GetVisiblePendingCount(TableAttentionReason.StaffRotationRequired) > 0)
            return "Assign dealer";
        if (attention.GetVisiblePendingCount(TableAttentionReason.PayoutRequired) > 0)
            return "Pay winners";
        return "Customers waiting";
    }
}
