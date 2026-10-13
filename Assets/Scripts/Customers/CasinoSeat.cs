using UnityEngine;
using System;

[DisallowMultipleComponent]
public sealed class CasinoSeat : MonoBehaviour
{
    [SerializeField] private Transform customerPosition;

    public event Action<CasinoCustomerAI> CustomerSeated;
    public event Action<CasinoCustomerAI> CustomerReleased;
    public CasinoCustomerAI ReservedBy { get; private set; }
    public CasinoCustomerAI Occupant { get; private set; }
    public bool IsAvailable => ReservedBy == null && Occupant == null;
    public Vector3 Position => customerPosition != null ? customerPosition.position : transform.position;

    public void Configure(Transform waitingPosition)
    {
        customerPosition = waitingPosition;
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
}
