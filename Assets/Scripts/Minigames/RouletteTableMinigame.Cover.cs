using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class RouletteCoverSelection
{
    private readonly Dictionary<int, RouletteCustomerBet> bets = new Dictionary<int, RouletteCustomerBet>();
    private readonly HashSet<int> marked = new HashSet<int>();
    public int WinningNumber { get; }
    public int WinnerCount { get; }
    public int MarkedCount => marked.Count;
    public bool AllWinnersMarked => WinnerCount > 0 && MarkedCount == WinnerCount;
    public bool CanDeclareNoWinners => WinnerCount == 0;

    public RouletteCoverSelection(long roundId, int number, IReadOnlyList<RouletteCustomerBet> roundBets)
    {
        if (roundId <= 0 || !EuropeanRouletteRules.IsValidNumber(number) || roundBets == null || roundBets.Count == 0)
            throw new ArgumentException("COVER requires a real round, result and customer bets.");
        WinningNumber = number;
        foreach (RouletteCustomerBet bet in roundBets)
        {
            if (bet == null || bet.RoundId != roundId || !EuropeanRouletteRules.IsValidBet(bet.Bet))
                throw new ArgumentException("COVER bets must belong to the same valid round.");
            bets.Add(bet.BetId, bet);
            if (EuropeanRouletteRules.Wins(bet.Bet, number)) WinnerCount++;
        }
    }

    public bool TryMark(int betId)
    {
        return bets.TryGetValue(betId, out RouletteCustomerBet bet) &&
            EuropeanRouletteRules.Wins(bet.Bet, WinningNumber) && marked.Add(betId);
    }
}

public sealed partial class RouletteTableMinigame
{
    private const float CoverAllowance = 60f;
    private readonly Dictionary<int, List<Button>> coverButtons = new Dictionary<int, List<Button>>();
    private readonly Dictionary<int, RectTransform> coverNumberCells = new Dictionary<int, RectTransform>();
    private readonly Dictionary<RouletteBetType, RectTransform> coverOutsideCells = new Dictionary<RouletteBetType, RectTransform>();
    private readonly Dictionary<int, Outline> coverOutlines = new Dictionary<int, Outline>();
    private RouletteCoverSelection coverSelection;
    private Text coverFeedback;
    private Text coverTimer;
    private GameObject betGuide;
    private float coverRemaining;
    private float coverFinishDelay;
    public bool IsCoverMode => IsRunning && stage == RouletteStage.Cover;
    public RouletteCoverSelection CoverSelection => coverSelection;

    public override bool TryGetDeadline(out float remaining, out float duration)
    {
        remaining = coverRemaining;
        duration = CoverAllowance;
        return IsCoverMode && coverFinishDelay <= 0 && remaining > 0;
    }

    private void BuildCover()
    {
        coverSelection = new RouletteCoverSelection(assistanceRoundId, winningNumber, roundController.AssistanceBets);
        coverRemaining = CoverAllowance;
        coverFinishDelay = 0;
        coverButtons.Clear();
        coverNumberCells.Clear();
        coverOutsideCells.Clear();
        coverOutlines.Clear();
        CasinoMinigamePresentation.CreateText(gameArea, "COVER Winning Number",
            $"WINNING NUMBER  {winningNumber}  {GetPocketColorName(winningNumber)}",
            new Vector2(-200, 257), new Vector2(940, 54), 30, CasinoHudStyle.Gold);
        coverTimer = CasinoMinigamePresentation.CreateText(gameArea, "COVER Timer", "",
            new Vector2(505, 257), new Vector2(220, 48), 22, Color.white);
        BuildBettingLayout(gameArea, new Vector2(-325, -6), 620, 480,
            coverNumberCells, coverOutsideCells, coverOutlines, false);
        HighlightWinningNumber(coverOutlines);
        CasinoMinigamePresentation.CreateText(gameArea, "COVER Customer Bets", "CLICK THE WINNING BETS",
            new Vector2(340, 210), new Vector2(560, 36), 23, CasinoHudStyle.Gold);

        for (int i = 0; i < roundController.AssistanceBets.Count; i++)
        {
            RouletteCustomerBet bet = roundController.AssistanceBets[i];
            int betId = bet.BetId;
            Color customerColor = Color.HSVToRGB((i * .23f + .07f) % 1f, .6f, .95f);
            string customer = bet.Customer != null ? bet.Customer.name : $"CUSTOMER {i + 1}";
            Button entry = CreatePayButton(gameArea, $"COVER Bet {betId}",
                $"{i + 1}  {customer}\n{RouletteRoundController.GetBetLabel(bet.Bet)}  {RouletteRoundController.FormatPounds(bet.Bet.StakePence)}",
                new Vector2(340, 145 - i * 86), new Vector2(560, 76), () => SelectCoverBet(betId));
            entry.GetComponent<Image>().color = Color.Lerp(customerColor, new Color(.035f, .17f, .13f), .68f);
            Text label = entry.GetComponentInChildren<Text>();
            label.fontSize = 18;
            label.raycastTarget = false;
            Vector2 chipPosition = GetBetBoardPosition(bet.Bet, coverNumberCells, coverOutsideCells);
            // Separate identical bets along the cell's width, keeping every chip clickable.
            int duplicateIndex = 0;
            int duplicateCount = 0;
            foreach (RouletteCustomerBet other in roundController.AssistanceBets)
                if (GetBetRegionKey(other.Bet) == GetBetRegionKey(bet.Bet))
                {
                    if (other.BetId < betId) duplicateIndex++;
                    duplicateCount++;
                }
            chipPosition.x += (duplicateIndex - (duplicateCount - 1) * .5f) * 22;
            Button chip = CreatePayButton(gameArea, $"COVER Chip {betId}", (i + 1).ToString(),
                chipPosition, new Vector2(32, 32), () => SelectCoverBet(betId));
            CasinoPixelArt.SetSprite(chip.GetComponent<Image>(), CasinoPixelArt.ChipForAmount(bet.Bet.StakePence), Color.white);
            Outline customerRim = chip.gameObject.AddComponent<Outline>();
            customerRim.effectColor = customerColor;
            customerRim.effectDistance = new Vector2(2, -2);
            chip.GetComponentInChildren<Text>().fontSize = 16;
            chip.GetComponentInChildren<Text>().raycastTarget = false;
            coverButtons[betId] = new List<Button> { entry, chip };
        }

        coverFeedback = CasinoMinigamePresentation.CreateText(gameArea, "COVER Feedback",
            "Match the number to the bets. Leave losing bets alone!",
            new Vector2(0, -278), new Vector2(1360, 40), 19, CasinoHudStyle.Gold);
        CreatePayButton(gameArea, "COVER No Winners", "NO WINNERS",
            new Vector2(195, -215), new Vector2(265, 50), DeclareNoCoverWinners);
        CreatePayButton(gameArea, "COVER Bet Guide", "BET GUIDE",
            new Vector2(485, -215), new Vector2(265, 50), () => betGuide.SetActive(true));
        BuildCoverGuide();
    }

    private void BuildCoverGuide()
    {
        betGuide = CasinoMinigamePresentation.CreateImage("COVER Bet Guide Panel", gameArea,
            Vector2.zero, new Vector2(1100, 540), CasinoHudStyle.Ink, true);
        CasinoHudStyle.Panel(betGuide.GetComponent<Image>(), CasinoHudStyle.Ink);
        CasinoMinigamePresentation.CreateText(betGuide.transform, "Guide title", "WHICH BETS COVER THE NUMBER?",
            new Vector2(0, 216), new Vector2(1040, 50), 27, CasinoHudStyle.Gold);
        CasinoMinigamePresentation.CreateText(betGuide.transform, "Guide examples",
            "STRAIGHT  [7]  -> 7  |  35:1\nSPLIT  [1|2]  -> either neighbour  |  17:1\n" +
            "STREET  [1|2|3]  -> one full row  |  11:1\nCORNER  [1|2 / 4|5]  -> four touching numbers  |  8:1\n" +
            "SIX-LINE  [1|2|3 / 4|5|6]  -> two full rows  |  5:1\n" +
            "RED / BLACK  -> the number's colour  |  1:1\nODD / EVEN  -> the number's parity  |  1:1\n" +
            "ZERO is green: outside bets lose. A 0 straight or 0 split can win.",
            new Vector2(0, 0), new Vector2(1030, 350), 23, Color.white);
        CreatePayButton(betGuide.transform, "Close bet guide", "GOT IT!",
            new Vector2(0, -218), new Vector2(280, 50), () => betGuide.SetActive(false));
        betGuide.SetActive(false);
    }

    public bool SelectCoverBet(int betId)
    {
        if (!IsCoverMode || Time.timeScale == 0 || coverFinishDelay > 0) return false;
        if (!coverSelection.TryMark(betId))
        {
            coverFeedback.text = "Not that one! Match the winning number, and leave marked bets alone.";
            coverFeedback.color = CasinoHudStyle.Coral;
            CasinoSoundscape.Play(CasinoSoundCue.Mistake, transform.position, transform);
            return false;
        }
        foreach (Button button in coverButtons[betId])
        {
            button.interactable = false;
            button.GetComponent<Image>().color = new Color(.25f, .8f, .42f);
            Outline outline = button.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
            outline.effectColor = CasinoHudStyle.Gold;
            outline.effectDistance = new Vector2(3, -3);
            button.transform.localScale = Vector3.one * 1.08f;
        }
        coverFeedback.text = $"Nice spot! {coverSelection.MarkedCount}/{coverSelection.WinnerCount} winning bets covered.";
        coverFeedback.color = CasinoHudStyle.Gold;
        CasinoSoundscape.Play(CasinoSoundCue.Chip, transform.position, transform);
        if (coverSelection.AllWinnersMarked) FinishCoverSelection();
        return true;
    }

    public void DeclareNoCoverWinners()
    {
        if (!IsCoverMode || Time.timeScale == 0 || coverFinishDelay > 0) return;
        if (coverSelection.CanDeclareNoWinners) FinishCoverSelection();
        else
        {
            coverFeedback.text = "Someone has won! Check the bets against the number.";
            coverFeedback.color = CasinoHudStyle.Coral;
            CasinoSoundscape.Play(CasinoSoundCue.Mistake, transform.position, transform);
        }
    }

    private void FinishCoverSelection()
    {
        coverFeedback.text = "COVERED! Your dealer will clear and pay this round.";
        coverFeedback.color = CasinoHudStyle.Gold;
        coverFinishDelay = .65f;
        roundController.AssignedDealer?.GetComponentInChildren<PixelCharacterVisual>()?.PlayAction(
            CasinoAnimationPose.Pay, tableRoot.position, 1f);
    }

    private void TickCover(float deltaTime)
    {
        if (!roundController.IsCoverReady || roundController.AssistanceRoundId != assistanceRoundId ||
            roundController.AssistanceWinningNumber != winningNumber)
        {
            Abandon();
            return;
        }
        if (coverFinishDelay > 0)
        {
            coverFinishDelay -= deltaTime;
            if (coverFinishDelay <= 0) Complete(true, 1000);
            return;
        }
        coverRemaining = Mathf.Max(0, coverRemaining - deltaTime);
        coverTimer.text = $"{Mathf.CeilToInt(coverRemaining)}s  |  ESC: EXIT";
        coverTimer.color = CasinoTaskFeedback.UrgencyColor(coverRemaining / CoverAllowance);
        if (coverRemaining <= 0) Complete(false, coverSelection.MarkedCount * 100);
    }

    private Vector2 GetBetBoardPosition(RouletteBet bet,
        Dictionary<int, RectTransform> numbers, Dictionary<RouletteBetType, RectTransform> outside)
    {
        if (outside.TryGetValue(bet.Type, out RectTransform cell))
            return GetGameAreaPosition(cell);
        if (bet.Type == RouletteBetType.StraightUp) return GetGameAreaPosition(numbers[bet.Number]);
        if (bet.Type == RouletteBetType.Split && (bet.Numbers[0] == 0 || bet.Numbers[1] == 0))
        {
            int other = bet.Numbers[0] == 0 ? bet.Numbers[1] : bet.Numbers[0];
            Vector2 numberPosition = GetGameAreaPosition(numbers[other]);
            return new Vector2(numberPosition.x, (numberPosition.y + GetGameAreaPosition(numbers[0]).y) * .5f);
        }
        Vector2 center = Vector2.zero;
        foreach (int number in bet.Numbers) center += GetGameAreaPosition(numbers[number]);
        center /= bet.Numbers.Length;
        if (bet.Type == RouletteBetType.Street || bet.Type == RouletteBetType.SixLine)
            center.x = GetGameAreaPosition(numbers[bet.Numbers[0]]).x -
                numbers[bet.Numbers[0]].sizeDelta.x * .5f - 1.5f;
        return center;
    }
}
