using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CasinoCashDeskInteraction : MonoBehaviour
{
    private static readonly int[] AvailablePackAmountsPence = { 1000, 2000, 3000, 4000, 5000 };

    [SerializeField] private CasinoTableAttention attention;
    [SerializeField] private CasinoMinigameStation minigameStation;
    [SerializeField] private CashDeskPackMinigame packMinigame;
    private readonly Queue<CasinoCustomerAI> waitingCustomers = new Queue<CasinoCustomerAI>();
    private readonly HashSet<CasinoCustomerAI> queuedCustomers = new HashSet<CasinoCustomerAI>();
    private CasinoCustomerAI activeCustomer;

    public int WaitingCustomerCount => waitingCustomers.Count + (activeCustomer != null ? 1 : 0);
    public CasinoCustomerAI ActiveCustomer => activeCustomer;
    public bool IsConfigured => attention != null && minigameStation != null &&
                                minigameStation.IsConfigured && packMinigame != null;

    public void Configure(
        CasinoTableAttention tableAttention,
        CasinoMinigameStation station,
        CashDeskPackMinigame game)
    {
        attention = tableAttention;
        minigameStation = station;
        packMinigame = game;
        if (minigameStation != null)
        {
            minigameStation.TaskSucceeded -= OnPackDelivered;
            minigameStation.TaskSucceeded += OnPackDelivered;
            minigameStation.TaskAbandoned -= OnPackAbandoned;
            minigameStation.TaskAbandoned += OnPackAbandoned;
        }
    }

    public void CustomerArrived(CasinoCustomerAI customer)
    {
        if (customer == null || !queuedCustomers.Add(customer))
        {
            return;
        }

        waitingCustomers.Enqueue(customer);
        BeginNextPackIfReady();
    }

    private void Start()
    {
        if (!IsConfigured)
        {
            Debug.LogError($"{nameof(CasinoCashDeskInteraction)} on {name} is missing its minigame, station, or attention references.", this);
            enabled = false;
            return;
        }

        minigameStation.TaskSucceeded -= OnPackDelivered;
        minigameStation.TaskSucceeded += OnPackDelivered;
        minigameStation.TaskAbandoned -= OnPackAbandoned;
        minigameStation.TaskAbandoned += OnPackAbandoned;
    }

    private void OnDestroy()
    {
        if (minigameStation != null)
        {
            minigameStation.TaskSucceeded -= OnPackDelivered;
            minigameStation.TaskAbandoned -= OnPackAbandoned;
        }
    }

    private void OnPackDelivered(CasinoMinigameResult result)
    {
        if (activeCustomer != null)
        {
            activeCustomer.CompleteCashierVisit();
            queuedCustomers.Remove(activeCustomer);
            activeCustomer = null;
        }

        BeginNextPackIfReady();
    }

    private void OnPackAbandoned()
    {
        BeginNextPackIfReady();
    }

    private void BeginNextPackIfReady()
    {
        if (activeCustomer == null)
        {
            while (waitingCustomers.Count > 0 && activeCustomer == null)
            {
                CasinoCustomerAI customer = waitingCustomers.Dequeue();
                if (customer != null && customer.IsWaitingAtCashDesk)
                {
                    activeCustomer = customer;
                }
                else
                {
                    queuedCustomers.Remove(customer);
                }
            }
        }

        if (activeCustomer == null || minigameStation == null || packMinigame == null ||
            minigameStation.IsTaskPending)
        {
            return;
        }

        packMinigame.SetPackAmount(AvailablePackAmountsPence[Random.Range(0, AvailablePackAmountsPence.Length)]);
        minigameStation.RequestMinigame(
            CasinoMinigameType.Pack,
            TableAttentionReason.CashDeskPackRequired);
    }
}
