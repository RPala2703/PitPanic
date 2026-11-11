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
    [SerializeField] private Image taskBadge;
    private bool hasTasks;
    private float refreshTimer;

    public bool IsConfigured => taskText != null && dealerText != null && carryText != null;

    private void Awake()
    {
        if (taskText != null && taskText.transform.parent.TryGetComponent(out Image panel))
            CasinoHudStyle.Panel(panel, CasinoHudStyle.Ink);
        if (taskBadge != null)
            CasinoHudStyle.Panel(taskBadge, taskBadge.color);
    }

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
        if (taskBadge != null)
        {
            float pulse = hasTasks ? 0.88f + 0.12f * Mathf.Sin(Time.unscaledTime * 3f) : 1f;
            taskBadge.color = hasTasks
                ? new Color(0.53f * pulse, 0.20f * pulse, 0.22f * pulse, 0.98f)
                : new Color(0.12f, 0.32f, 0.26f, 0.98f);
        }
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
            new Vector2(390f, 200f),
            CasinoHudStyle.Ink);
        CasinoHudStyle.Panel(panel.GetComponent<Image>(), CasinoHudStyle.Ink);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-24f, -24f);
        Text heading = CreateHudText(panel.transform, "Casino Name",
            new Vector2(20f, -12f), new Vector2(350f, 29f), 19);
        heading.text = "FLOOR TASKS";
        heading.color = CasinoHudStyle.Gold;
        GameObject badge = CasinoMinigamePresentation.CreateImage(
            "Task Badge", panel.transform, new Vector2(0f, 6f), new Vector2(358f, 82f), CasinoHudStyle.Coral);
        taskBadge = badge.GetComponent<Image>();
        CasinoHudStyle.Panel(taskBadge, taskBadge.color);
        taskText = CreateHudText(panel.transform, "Active Tasks",
            new Vector2(28f, -47f), new Vector2(334f, 70f), 17);
        taskText.alignment = TextAnchor.UpperLeft;
        taskText.color = new Color(0.94f, 0.97f, 0.92f);
        dealerText = CreateHudText(panel.transform, "Dealer Staffing",
            new Vector2(24f, -138f), new Vector2(342f, 24f), 15);
        dealerText.alignment = TextAnchor.MiddleLeft;
        dealerText.color = new Color(0.76f, 0.88f, 0.86f);
        carryText = CreateHudText(panel.transform, "Float Delivery Status",
            new Vector2(24f, -165f), new Vector2(342f, 24f), 15);
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
                if (activeTaskLines.Count < 2)
                {
                    activeTaskLines.Add($"{GetTaskLabel(reason)}: {GetStationLabel(attention.name)}" +
                                        (count > 1 ? $"  ×{count}" : string.Empty));
                    displayedTaskCount += count;
                }
            }
        }

        hasTasks = totalTaskCount > 0;
        if (!hasTasks)
        {
            taskText.text = "ALL CLEAR!\nKeep the casino smiling.";
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
                lines.Append("\n+ ").Append(totalTaskCount - displayedTaskCount).Append(" more");
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

        dealerText.text = $"DEALERS  {ready} READY  •  {needsBreak} BREAK  •  {inTransit} MOVING";
    }

    private void UpdateFloatSummary()
    {
        if (carryText == null)
        {
            return;
        }

        CasinoFloatRefillOrder order = floatCarrier != null ? floatCarrier.CurrentOrder : null;
        carryText.text = order == null
            ? "FLOAT  •  HANDS FREE"
            : $"FLOAT TO {order.Destination.DisplayName}  •  £{order.TotalPence / 100}.{order.TotalPence % 100:00}";
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
            case TableAttentionReason.DealerBreakRequired: return "BREAK NEEDED";
            case TableAttentionReason.TableUnstaffed: return "TABLE UNSTAFFED";
            case TableAttentionReason.PitBossApproval: return "APPROVAL NEEDED";
            case TableAttentionReason.FlickAssistanceRequired: return "FLICK!";
            case TableAttentionReason.RouletteSpinRequired: return "SPIN!";
            case TableAttentionReason.RouletteSweepRequired: return "SWEEP!";
            case TableAttentionReason.PayoutRequired: return "PAY!";
            case TableAttentionReason.CashDeskPackRequired: return "PACK!";
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
