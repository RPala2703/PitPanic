using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class CasinoGameplayValidation
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, Flags).SetValue(target, value);
    private static object Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, Flags).Invoke(target, args);

    public static void Run()
    {
        CheckCoverRules();
        CheckFiniteSettlement();
        CheckAllBust();
        CheckBlackjackWinnings();
        CheckChipPaletteAndRefills();
        Debug.Log("Casino gameplay validation passed: COVER rules, zero, duplicates, finite payout recovery and Blackjack all-bust.");
    }

    private static void CheckChipPaletteAndRefills()
    {
        Color black = BlackjackChipView.GetChipColor(2500);
        Color pink = BlackjackChipView.GetChipColor(10000);
        Check(black.r < .15f && black.g < .15f && black.b < .15f &&
            pink.r > .8f && pink.b > .5f && pink.g < .5f, "£25 must be black and £100 must be pink.");
        foreach (int value in new[] { 250, 500, 2500, 10000 })
            Check(CasinoPixelArt.ChipForValue(value).texture.filterMode == FilterMode.Point,
                "Denomination chip artwork is not point-filtered.");
        GameObject root = new GameObject("Demand-based float test");
        try
        {
            CasinoFloatRefillDestination stock = root.AddComponent<CasinoFloatRefillDestination>();
            CasinoTableAttention attention = root.AddComponent<CasinoTableAttention>();
            stock.Configure("TEST", "TEST", null, attention, new[] { 15, 20, 4, 2 });
            Check(stock.TryConsumeChips(new[] { 15, 0, 0, 0 }), "Could not deplete red stock.");
            int[] redOnly = stock.GetRequiredRefillCounts();
            Check(redOnly[0] == 15 && redOnly[1] == 0 && redOnly[2] == 0 && redOnly[3] == 0,
                "Red-only shortage should request only fifteen £5 chips.");
            stock.TryAddChips(redOnly);
            stock.TryConsumeChips(new[] { 0, 18, 0, 0 });
            int[] blueOnly = stock.GetRequiredRefillCounts();
            Check(blueOnly[0] == 0 && blueOnly[1] == 18 && blueOnly[2] == 0 && blueOnly[3] == 0,
                "Refill quantities should change with the actual shortage.");
            stock.RequestAttention(175000);
            int[] largeRefill = stock.GetRequiredRefillCounts();
            stock.TryAddChips(largeRefill);
            Check(stock.TryGetAvailableComposition(175000, out _),
                "Refill request did not cover a blocked high-roller payout.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void CheckCoverRules()
    {
        RouletteBet[] bets =
        {
            new RouletteBet(RouletteBetType.StraightUp, 7, 500),
            new RouletteBet(RouletteBetType.StraightUp, 0, 500),
            new RouletteBet(RouletteBetType.Split, new[] { 1, 2 }, 500),
            new RouletteBet(RouletteBetType.Street, new[] { 1, 2, 3 }, 500),
            new RouletteBet(RouletteBetType.Corner, new[] { 1, 2, 4, 5 }, 500),
            new RouletteBet(RouletteBetType.SixLine, new[] { 1, 2, 3, 4, 5, 6 }, 500),
            new RouletteBet(RouletteBetType.Red, -1, 500),
            new RouletteBet(RouletteBetType.Black, -1, 500),
            new RouletteBet(RouletteBetType.Odd, -1, 500),
            new RouletteBet(RouletteBetType.Even, -1, 500),
            new RouletteBet(RouletteBetType.Split, new[] { 0, 2 }, 500)
        };
        int[][] coverage =
        {
            new[] { 7 }, new[] { 0 }, new[] { 1, 2 }, new[] { 1, 2, 3 }, new[] { 1, 2, 4, 5 },
            new[] { 1, 2, 3, 4, 5, 6 },
            new[] { 1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36 },
            new[] { 2, 4, 6, 8, 10, 11, 13, 15, 17, 20, 22, 24, 26, 28, 29, 31, 33, 35 },
            new[] { 1, 3, 5, 7, 9, 11, 13, 15, 17, 19, 21, 23, 25, 27, 29, 31, 33, 35 },
            new[] { 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 34, 36 },
            new[] { 0, 2 }
        };
        int[] grossMultipliers = { 35, 35, 17, 11, 8, 5, 1, 1, 1, 1, 17 };
        for (int result = 0; result <= 36; result++)
        {
            List<RouletteCustomerBet> actualBets = new List<RouletteCustomerBet>();
            for (int i = 0; i < bets.Length; i++)
                actualBets.Add(new RouletteCustomerBet(17, i, null, bets[i]));
            // Identical bets from separate customers must both be marked.
            actualBets.Add(new RouletteCustomerBet(17, bets.Length, null, bets[0]));
            RouletteCoverSelection selection = new RouletteCoverSelection(17, result, actualBets);
            int winners = 0;
            for (int i = 0; i < actualBets.Count; i++)
            {
                int index = i == bets.Length ? 0 : i;
                bool expected = Array.IndexOf(coverage[index], result) >= 0;
                Check(EuropeanRouletteRules.TryGetWinningsPence(bets[index], result, out long gross) &&
                    gross == (expected ? 500L * grossMultipliers[index] : 0), "Incorrect Roulette odds or zero coverage.");
                Check(selection.TryMark(i) == expected, "COVER accepted a loser or rejected a winner.");
                Check(!selection.TryMark(i), "COVER accepted a repeated or losing selection.");
                if (expected) winners++;
                if (i < actualBets.Count - 1 && result == 7)
                    Check(!selection.AllWinnersMarked, "COVER finished before an identical winning bet was marked.");
            }
            Check(selection.WinnerCount == winners && selection.CanDeclareNoWinners == (winners == 0) &&
                selection.AllWinnersMarked == (winners > 0) && !selection.TryMark(-1),
                "COVER completion or no-winner rules are incorrect.");
        }
        RouletteCoverSelection noWinners = new RouletteCoverSelection(1, 0,
            new[] { new RouletteCustomerBet(1, 0, null, bets[6]), new RouletteCustomerBet(1, 1, null, bets[7]) });
        Check(noWinners.CanDeclareNoWinners && !noWinners.TryMark(0), "Zero incorrectly pays an outside bet.");
        Check(!CasinoMinigameStation.ShouldResolveTaskOnAbandon(CasinoMinigameType.Cover),
            "Cancelling COVER must retain its bets and result.");
    }

    private static void CheckFiniteSettlement()
    {
        GameObject root = new GameObject("Finite COVER settlement test");
        try
        {
            CasinoTableAttention attention = root.AddComponent<CasinoTableAttention>();
            CasinoFloatRefillDestination stock = root.AddComponent<CasinoFloatRefillDestination>();
            TableInteractionZone zone = root.AddComponent<TableInteractionZone>();
            zone.Configure(root.AddComponent<BoxCollider>());
            stock.Configure("TEST", "TEST", zone, attention, new int[4]);
            RouletteRoundController rounds = root.AddComponent<RouletteRoundController>();
            Set(rounds, "floatDestination", stock);
            var bets = (List<RouletteCustomerBet>)typeof(RouletteRoundController).GetField("currentBets", Flags).GetValue(rounds);
            bets.Add(new RouletteCustomerBet(1, 0, null, new RouletteBet(RouletteBetType.Red, -1, 500)));
            bets.Add(new RouletteCustomerBet(1, 1, null, new RouletteBet(RouletteBetType.Red, -1, 500)));
            int settlements = 0;
            rounds.BetSettled += _ => settlements++;
            Call(rounds, "SettleRound", 1);
            Check(!(bool)Call(rounds, "TrySettleAutomaticPayouts") &&
                attention.GetPendingCount(TableAttentionReason.FloatRefillRequired) == 1 &&
                !bets[0].IsPaid, "Unfunded COVER payout did not create a real refill request.");
            stock.TryAddChips(new[] { 1, 0, 0, 0 });
            Check(!(bool)Call(rounds, "TrySettleAutomaticPayouts") && bets[0].IsPaid && !bets[1].IsPaid &&
                stock.InventoryValuePence == 0, "Partial COVER settlement lost its paid-bet guard.");
            var order = new CasinoFloatRefillOrder("cover-refill", "TEST", stock, new[] { 1, 0, 0, 0 });
            Check(stock.Receive(order, new[] { 1, 0, 0, 0 }) && !stock.Receive(order, new[] { 1, 0, 0, 0 }),
                "Real refill delivery failed or duplicated.");
            Check((bool)Call(rounds, "TrySettleAutomaticPayouts") && bets[1].IsPaid &&
                stock.InventoryValuePence == 0, "COVER did not resume the unpaid payout after refill.");
            Call(rounds, "SettleRound", 1);
            Check((bool)Call(rounds, "TrySettleAutomaticPayouts") && stock.InventoryValuePence == 0 &&
                settlements == 2, "COVER paid or recorded the same bet twice.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void CheckAllBust()
    {
        GameObject root = new GameObject("Blackjack all-bust test");
        try
        {
            CasinoTableAttention attention = root.AddComponent<CasinoTableAttention>();
            CasinoFloatRefillDestination stock = root.AddComponent<CasinoFloatRefillDestination>();
            stock.Configure("BJTEST", "BJTEST", null, attention, new int[4]);
            BlackjackTableInteraction table = root.AddComponent<BlackjackTableInteraction>();
            Set(table, "attention", attention);
            Set(table, "floatDestination", stock);
            var round = new BlackjackTableRound(new BlackjackShoe(new[]
            {
                new BlackjackCard(10, 0), new BlackjackCard(8, 0), new BlackjackCard(5, 0),
                new BlackjackCard(2, 0), new BlackjackCard(3, 0), new BlackjackCard(10, 1)
            }));
            BlackjackHand hand = round.AddCustomer(null, null, 500);
            round.DealTo(hand); round.DealTo(hand); round.DealTo(hand);
            round.DealToDealer(); round.DealToDealer();
            int dealerCards = round.DealerCards.Count;
            Set(table, "round", round);
            Call(table, "BeginDealerTurn");
            Check(round.IsSettled && round.DealerCards.Count == dealerCards &&
                stock.InventoryValuePence == 500 && hand.WinningsPence == 0,
                "All-bust Blackjack drew dealer cards or failed to collect exactly the losing stake.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void CheckBlackjackWinnings()
    {
        GameObject root = new GameObject("Blackjack stake and winnings test");
        try
        {
            CasinoTableAttention attention = root.AddComponent<CasinoTableAttention>();
            CasinoFloatRefillDestination stock = root.AddComponent<CasinoFloatRefillDestination>();
            stock.Configure("BJTEST", "BJTEST", null, attention, new[] { 4, 0, 0, 0 });
            BlackjackTableInteraction table = root.AddComponent<BlackjackTableInteraction>();
            Set(table, "attention", attention);
            Set(table, "floatDestination", stock);
            var round = new BlackjackTableRound(new BlackjackShoe(new[]
            {
                new BlackjackCard(10, 0), new BlackjackCard(10, 1), new BlackjackCard(9, 0),
                new BlackjackCard(9, 1), new BlackjackCard(10, 2), new BlackjackCard(8, 0)
            }));
            BlackjackHand win = round.AddCustomer(null, null, 1000);
            BlackjackHand push = round.AddCustomer(null, null, 1000);
            round.DealTo(win); round.DealTo(win);
            round.DealTo(push); round.DealTo(push);
            round.DealToDealer(); round.DealToDealer();
            Set(table, "round", round);
            Call(table, "BeginSettlement");
            Check(push.PayoutPaid && push.WinningsPence == 0 && stock.InventoryValuePence == 2000 &&
                win.WinningsPence == 1000 && !win.PayoutPaid, "Blackjack push demanded chips or a win required double the bet.");
            var selected = (List<int>)typeof(BlackjackTableInteraction).GetField("selectedPayoutChips", Flags).GetValue(table);
            selected.Add(500); selected.Add(500);
            Call(table, "ConfirmPayout");
            Check(win.PayoutPaid && stock.InventoryValuePence == 1000,
                "Blackjack paid the returned stake from table inventory instead of only the winnings.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
