using UnityEngine;
using System;

public enum CasinoCustomerGame
{
    Blackjack1,
    Blackjack2,
    Roulette
}

[DisallowMultipleComponent]
public sealed class CasinoSeat : MonoBehaviour
{
    [SerializeField] private Transform customerPosition;
    [SerializeField] private Transform bettingPosition;
    [SerializeField] private Transform approachPosition;
    [SerializeField] private CasinoCustomerGame gameType;
    [SerializeField] private CasinoDealerStation dealerStation;

    public event Action<CasinoCustomerAI> CustomerSeated;
    public event Action<CasinoCustomerAI> CustomerReleased;
    public CasinoCustomerAI ReservedBy { get; private set; }
    public CasinoCustomerAI Occupant { get; private set; }
    public bool IsAvailable => ReservedBy == null && Occupant == null;
    public Vector3 Position => approachPosition != null ? approachPosition.position : transform.position;
    public Quaternion Facing => customerPosition != null ? customerPosition.rotation : transform.rotation;
    public Transform BettingPosition => bettingPosition;
    public CasinoCustomerGame GameType => gameType;
    public CasinoDealerStation DealerStation => dealerStation;
    public bool IsGameAvailable => dealerStation == null ||
                                   dealerStation.AssignedDealer != null &&
                                   dealerStation.AssignedDealer.isActiveAndEnabled &&
                                   dealerStation.AssignedDealer.IsAtStation;
    public Vector3 SeatedVisualOffset => customerPosition != null && approachPosition != null
        ? transform.InverseTransformVector(customerPosition.position - approachPosition.position)
        : Vector3.zero;

    public void Configure(Transform waitingPosition, Transform betPosition, Transform reachableApproach = null)
    {
        customerPosition = waitingPosition;
        bettingPosition = betPosition;
        approachPosition = reachableApproach;
    }

    public void ConfigureGame(CasinoCustomerGame type, CasinoDealerStation station)
    {
        gameType = type;
        dealerStation = station;
    }

    public bool TryReserve(CasinoCustomerAI customer)
    {
        if (customer == null || !IsAvailable)
        {
            return false;
        }

        ReservedBy = customer;
        return true;
    }

    public bool SeatCustomer(CasinoCustomerAI customer)
    {
        if (customer == null || ReservedBy != customer || Occupant != null)
        {
            return false;
        }

        Occupant = customer;
        CustomerSeated?.Invoke(customer);
        return true;
    }

    public void Release(CasinoCustomerAI customer)
    {
        if (customer == null || (ReservedBy != customer && Occupant != customer))
        {
            return;
        }

        bool wasSeated = Occupant == customer;
        ReservedBy = null;
        Occupant = null;
        if (wasSeated)
        {
            CustomerReleased?.Invoke(customer);
        }
    }

    public void NotifyReadyForService(CasinoCustomerAI customer)
    {
        if (customer != null && Occupant == customer && ReservedBy == customer)
        {
            CustomerSeated?.Invoke(customer);
        }
    }
}
