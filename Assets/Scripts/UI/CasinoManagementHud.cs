using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CasinoManagementHud : MonoBehaviour
{
    private static readonly TableAttentionReason[] DisplayPriority =
    {
        TableAttentionReason.DealerExhausted,
        TableAttentionReason.DealerBreakRequired,
        TableAttentionReason.TableUnstaffed,
        TableAttentionReason.PitBossApproval,
        TableAttentionReason.FlickAssistanceRequired,
        TableAttentionReason.RouletteSpinRequired,
        TableAttentionReason.RouletteSweepRequired,
        TableAttentionReason.PayoutRequired,
        TableAttentionReason.CashDeskPackRequired,
        TableAttentionReason.FloatRefillRequired,
        TableAttentionReason.DealerReadyForAssignment,
        TableAttentionReason.StaffRotationRequired,
        TableAttentionReason.CustomerComplaint,
        TableAttentionReason.DealCards,
        TableAttentionReason.ChipRefillRequired,
        TableAttentionReason.CustomersWaiting
    };

    [SerializeField] private CasinoTableAttention[] attentionSources;
    [SerializeField] private DealerAssignmentManager dealerManager;
    [SerializeField] private CasinoPlayerFloatCarrier floatCarrier;

    private readonly List<string> activeTaskLines = new List<string>(4);
    [SerializeField] private Text taskText;
    [SerializeField] private Text dealerText;
    [SerializeField] private Text carryText;
    private float refreshTimer;

    public bool IsConfigured => taskText != null && dealerText != null && carryText != null;

    public void Configure(
        CasinoTableAttention[] sources,
        DealerAssignmentManager assignments,
        CasinoPlayerFloatCarrier carrier)
    {
        attentionSources = sources;
        dealerManager = assignments;
        floatCarrier = carrier;
        BuildHud();
        RefreshHud();
    }

    private void Update()
    {
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer > 0f)
        {
            return;
        }

        refreshTimer = 0.25f;
        RefreshHud();
    }

    private void BuildHud()
    {
        Canvas canvas = CasinoMinigameHud.Create("Casino Management HUD", 20);
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        scaler.referenceResolution = new Vector2(1600f, 900f);
        GameObject panel = CasinoMinigamePresentation.CreateImage(
            "Management HUD Panel",
            canvas.transform,
            Vector2.zero,
            new Vector2(430f, 238f),
            new Color(0.025f, 0.075f, 0.075f, 0.94f));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-24f, -24f);
        Outline panelOutline = panel.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.72f, 0.52f, 0.22f, 0.9f);
        panelOutline.effectDistance = new Vector2(2f, -2f);

        GameObject accent = CasinoMinigamePresentation.CreateImage(
            "Management HUD Accent",
            panel.transform,
            Vector2.zero,
            new Vector2(430f, 8f),
            new Color(0.82f, 0.59f, 0.24f));
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 1f);
        accentRect.anchorMax = Vector2.one;
        accentRect.pivot = new Vector2(0f, 1f);
        accentRect.sizeDelta = new Vector2(0f, 8f);

        Text heading = CreateHudText(panel.transform, "Casino Name",
            new Vector2(24f, -18f), new Vector2(380f, 34f), 21);
        heading.text = "PIT PANIC  /  FLOOR STATUS";
        heading.color = new Color(1f, 0.85f, 0.52f);
        taskText = CreateHudText(panel.transform, "Active Tasks",
            new Vector2(24f, -60f), new Vector2(382f, 94f), 16);
        taskText.alignment = TextAnchor.UpperLeft;
        taskText.color = new Color(0.94f, 0.97f, 0.92f);
        dealerText = CreateHudText(panel.transform, "Dealer Staffing",
            new Vector2(24f, -164f), new Vector2(382f, 28f), 15);
        dealerText.alignment = TextAnchor.MiddleLeft;
        dealerText.color = new Color(0.76f, 0.88f, 0.86f);
        carryText = CreateHudText(panel.transform, "Float Delivery Status",
            new Vector2(24f, -196f), new Vector2(382f, 28f), 15);
        carryText.alignment = TextAnchor.MiddleLeft;
        carryText.color = new Color(0.85f, 0.90f, 1f);
    }

    private void RefreshHud()
    {
        if (taskText == null)
        {
            return;
        }

        activeTaskLines.Clear();
        int totalTaskCount = 0;
        int displayedTaskCount = 0;
        foreach (TableAttentionReason reason in DisplayPriority)
        {
            if (attentionSources == null)
            {
                break;
            }

            for (int sourceIndex = 0; sourceIndex < attentionSources.Length; sourceIndex++)
            {
                CasinoTableAttention attention = attentionSources[sourceIndex];
                int count = attention != null ? attention.GetPendingCount(reason) : 0;
                if (count <= 0)
                {
                    continue;
                }

                totalTaskCount += count;
                if (activeTaskLines.Count < 4)
                {
                    activeTaskLines.Add($"{GetTaskLabel(reason)}  ·  {GetStationLabel(attention.name)}" +
                                        (count > 1 ? $"  ×{count}" : string.Empty));
                    displayedTaskCount += count;
                }
            }
        }

        if (totalTaskCount == 0)
        {
            taskText.text = "ALL STATIONS CLEAR  •  GOOD WORK";
            taskText.color = new Color(0.66f, 0.96f, 0.76f);
        }
        else
        {
            StringBuilder lines = new StringBuilder();
            for (int i = 0; i < activeTaskLines.Count; i++)
            {
                if (i > 0)
                {
                    lines.Append('\n');
                }

                lines.Append(activeTaskLines[i]);
            }

            if (totalTaskCount > displayedTaskCount)
            {
                lines.Append("\n+ ").Append(totalTaskCount - displayedTaskCount).Append(" more active");
            }

            taskText.text = lines.ToString();
            taskText.color = new Color(1f, 0.82f, 0.58f);
        }

        UpdateDealerSummary();
        UpdateFloatSummary();
    }

    private void UpdateDealerSummary()
    {
        if (dealerText == null || dealerManager == null)
        {
            return;
        }

        int ready = 0;
        int needsBreak = 0;
        int inTransit = 0;
        foreach (AIDealer dealer in dealerManager.Dealers)
        {
            if (dealer == null)
            {
                continue;
            }

            if (dealer.IsReadyAtPitDesk)
            {
                ready++;
            }
            else if (dealer.NeedsBreak)
            {
                needsBreak++;
            }

            if (dealer.State == DealerDutyState.TravelingToBreak ||
                dealer.State == DealerDutyState.TravelingToPitDesk ||
                dealer.State == DealerDutyState.ReturningFromBreak ||
                dealer.State == DealerDutyState.TravelingToTable)
            {
                inTransit++;
            }
        }

        dealerText.text = $"DEALERS  •  READY {ready}   NEEDS BREAK {needsBreak}   MOVING {inTransit}";
    }

    private void UpdateFloatSummary()
    {
        if (carryText == null)
        {
            return;
        }

        CasinoFloatRefillOrder order = floatCarrier != null ? floatCarrier.CurrentOrder : null;
        carryText.text = order == null
            ? "FLOAT  •  NOT CARRYING"
            : $"FLOAT  •  DELIVER TO {order.Destination.DisplayName}  (£{order.TotalPence / 100}.{order.TotalPence % 100:00})";
        carryText.color = order == null
            ? new Color(0.74f, 0.80f, 0.78f)
            : new Color(0.77f, 0.90f, 1f);
    }

    private static Text CreateHudText(Transform parent, string name, Vector2 offset, Vector2 size, int fontSize)
    {
        Text text = CasinoMinigameHud.CreateText(parent, name, offset, size, fontSize);
        text.rectTransform.anchorMin = new Vector2(0f, 1f);
        text.rectTransform.anchorMax = new Vector2(0f, 1f);
        text.rectTransform.pivot = new Vector2(0f, 1f);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static string GetStationLabel(string stationName)
    {
        if (stationName.Contains("Blackjack Table 1")) return "BJ1";
        if (stationName.Contains("Blackjack Table 2")) return "BJ2";
        if (stationName.Contains("Roulette")) return "ROULETTE";
        if (stationName.Contains("Cash")) return "CASH DESK";
        if (stationName.Contains("Pit")) return "PIT DESK";
        return stationName.ToUpperInvariant();
    }

    private static string GetTaskLabel(TableAttentionReason reason)
    {
        switch (reason)
        {
            case TableAttentionReason.DealerExhausted: return "DEALER EXHAUSTED";
            case TableAttentionReason.DealerBreakRequired: return "BREAK REQUEST";
            case TableAttentionReason.TableUnstaffed: return "TABLE UNSTAFFED";
            case TableAttentionReason.PitBossApproval: return "APPROVAL NEEDED";
            case TableAttentionReason.FlickAssistanceRequired: return "FLICK! TASK";
            case TableAttentionReason.RouletteSpinRequired: return "SPIN! TASK";
            case TableAttentionReason.RouletteSweepRequired: return "SWEEP! TASK";
            case TableAttentionReason.PayoutRequired: return "PAY! TASK";
            case TableAttentionReason.CashDeskPackRequired: return "PACK! TASK";
            case TableAttentionReason.FloatRefillRequired: return "FLOAT LOW";
            case TableAttentionReason.DealerReadyForAssignment: return "DEALER READY";
            case TableAttentionReason.StaffRotationRequired: return "STAFFING NEEDED";
            case TableAttentionReason.CustomerComplaint: return "CUSTOMER ISSUE";
            case TableAttentionReason.DealCards: return "DEAL CARDS";
            case TableAttentionReason.ChipRefillRequired: return "REFILL CHIPS";
            default: return "CUSTOMERS WAITING";
        }
    }
}
