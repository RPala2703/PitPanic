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
    [SerializeField] private Text additionalTaskText;
    [SerializeField] private Image taskBadge;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private GameObject details;
    [SerializeField] private Button toggleButton;
    [SerializeField] private Text toggleLabel;
    private bool hasTasks;
    private bool expanded;
    private float refreshTimer;

    public bool IsConfigured => taskText != null && dealerText != null && carryText != null &&
                                additionalTaskText != null && panelRect != null && details != null &&
                                toggleButton != null && toggleLabel != null && taskBadge != null;

    private void Awake()
    {
        if (panelRect != null && panelRect.TryGetComponent(out Image panel))
            CasinoHudStyle.Panel(panel, CasinoHudStyle.Ink);
        if (AudioListener.pause) return;
        if (taskBadge != null)
            CasinoHudStyle.Panel(taskBadge, taskBadge.color);
        if (toggleButton != null)
        {
            CasinoHudStyle.Button(toggleButton);
            toggleButton.onClick.AddListener(ToggleDetails);
        }
        SetExpanded(false);
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
        if (panelRect != null)
        {
            PitDeskInteraction pitDesk = FindFirstObjectByType<PitDeskInteraction>();
            bool visible = Time.timeScale > 0 && !CasinoMinigamePresentation.IsOpen &&
                !CasinoPlayerInteractionRouter.IsAnyInspectionOpen() && (pitDesk == null || !pitDesk.IsOpen);
            if (panelRect.gameObject.activeSelf != visible) panelRect.gameObject.SetActive(visible);
        }
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
            new Vector2(350f, 76f),
            CasinoHudStyle.Ink);
        CasinoHudStyle.Panel(panel.GetComponent<Image>(), CasinoHudStyle.Ink);
        panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-24f, -24f);
        GameObject badge = CasinoMinigamePresentation.CreateImage(
            "Task Badge", panel.transform, Vector2.zero, new Vector2(294f, 34f), CasinoHudStyle.Coral);
        RectTransform badgeRect = badge.GetComponent<RectTransform>();
        badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(0f, 1f);
        badgeRect.pivot = new Vector2(0f, 1f);
        badgeRect.anchoredPosition = new Vector2(8f, -7f);
        taskBadge = badge.GetComponent<Image>();
        CasinoHudStyle.Panel(taskBadge, taskBadge.color);
        taskText = CreateHudText(panel.transform, "Active Tasks",
            new Vector2(16f, -9f), new Vector2(278f, 30f), 17);
        taskText.alignment = TextAnchor.MiddleLeft;
        taskText.color = new Color(0.94f, 0.97f, 0.92f);
        carryText = CreateHudText(panel.transform, "Float Delivery Status",
            new Vector2(16f, -46f), new Vector2(320f, 23f), 15);
        carryText.alignment = TextAnchor.MiddleLeft;
        carryText.color = new Color(0.85f, 0.90f, 1f);

        GameObject toggle = CasinoMinigamePresentation.CreateImage(
            "HUD Details Toggle", panel.transform, Vector2.zero, new Vector2(32f, 34f),
            new Color(0.24f, 0.37f, 0.31f));
        RectTransform toggleRect = toggle.GetComponent<RectTransform>();
        toggleRect.anchorMin = toggleRect.anchorMax = new Vector2(1f, 1f);
        toggleRect.pivot = Vector2.one;
        toggleRect.anchoredPosition = new Vector2(-8f, -7f);
        toggleButton = toggle.AddComponent<Button>();
        toggleButton.targetGraphic = toggle.GetComponent<Image>();
        toggleButton.targetGraphic.raycastTarget = true;
        CasinoHudStyle.Button(toggleButton);
        toggleLabel = CasinoMinigamePresentation.CreateText(toggle.transform, "Toggle Label", "+",
            Vector2.zero, new Vector2(30f, 30f), 22, Color.white);

        details = new GameObject("HUD Details", typeof(RectTransform));
        details.transform.SetParent(panel.transform, false);
        RectTransform detailsRect = details.GetComponent<RectTransform>();
        detailsRect.anchorMin = Vector2.zero;
        detailsRect.anchorMax = Vector2.one;
        detailsRect.offsetMin = detailsRect.offsetMax = Vector2.zero;
        additionalTaskText = CreateHudText(details.transform, "More Tasks",
            new Vector2(16f, -78f), new Vector2(318f, 50f), 15);
        additionalTaskText.alignment = TextAnchor.UpperLeft;
        additionalTaskText.color = new Color(1f, 0.82f, 0.58f);
        dealerText = CreateHudText(details.transform, "Dealer Staffing",
            new Vector2(16f, -133f), new Vector2(318f, 24f), 15);
        dealerText.alignment = TextAnchor.MiddleLeft;
        dealerText.color = new Color(0.76f, 0.88f, 0.86f);
        SetExpanded(false);
    }

    private void ToggleDetails() => SetExpanded(!expanded);

    private void SetExpanded(bool value)
    {
        expanded = value;
        if (panelRect != null)
            panelRect.sizeDelta = new Vector2(350f, expanded ? 168f : 76f);
        if (details != null)
            details.SetActive(expanded);
        if (toggleLabel != null)
            toggleLabel.text = expanded ? "-" : "+";
    }

    private void RefreshHud()
    {
        if (taskText == null)
        {
            return;
        }

        activeTaskLines.Clear();
        int totalTaskCount = 0;
        int firstTaskCount = 0;
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
                    if (activeTaskLines.Count == 1)
                        firstTaskCount = count;
                    displayedTaskCount += count;
                }
            }
        }

        hasTasks = totalTaskCount > 0;
        if (!hasTasks)
        {
            taskText.text = "ALL CLEAR!";
            taskText.color = new Color(0.66f, 0.96f, 0.76f);
            additionalTaskText.text = "NO OTHER TASKS";
        }
        else
        {
            StringBuilder lines = new StringBuilder();
            taskText.text = activeTaskLines[0] +
                            (totalTaskCount > firstTaskCount ? $"  +{totalTaskCount - firstTaskCount}" : "");
            for (int i = 1; i < activeTaskLines.Count; i++)
            {
                lines.Append(activeTaskLines[i]);
            }

            if (totalTaskCount > displayedTaskCount)
            {
                if (lines.Length > 0) lines.Append('\n');
                lines.Append("+ ").Append(totalTaskCount - displayedTaskCount).Append(" more");
            }

            additionalTaskText.text = lines.Length > 0 ? lines.ToString() : "NO OTHER TASKS";
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
