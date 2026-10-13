using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DealerAssignmentManager : MonoBehaviour
{
    [SerializeField, Min(0f)] private float minimumEnergyForAssignment = 20f;
    [SerializeField, Range(0.25f, 10f)] private float simulationTimeScale = 1f;
    [SerializeField] private AIDealer[] dealers;
    [SerializeField] private CasinoDealerStation[] stations;
    [SerializeField] private Transform[] breakPositions;
    private float previousTimeScale = 1f;

    public IReadOnlyList<AIDealer> Dealers => dealers;
    public IReadOnlyList<CasinoDealerStation> Stations => stations;
    public IReadOnlyList<Transform> BreakPositions => breakPositions;
    public bool IsConfigured => dealers != null && dealers.Length > 0 && stations != null &&
                                stations.Length > 0 && breakPositions != null && breakPositions.Length > 0;
    public float SimulationTimeScale
    {
        get => simulationTimeScale;
        set => simulationTimeScale = Mathf.Clamp(value, 0.25f, 10f);
    }

    public void Configure(AIDealer[] casinoDealers, CasinoDealerStation[] casinoStations, Transform[] restPositions)
    {
        dealers = casinoDealers;
        stations = casinoStations;
        breakPositions = restPositions;
    }

    private void Awake()
    {
        previousTimeScale = Time.timeScale;
        simulationTimeScale = Mathf.Clamp(simulationTimeScale, 0.25f, 10f);
    }

    private void Start()
    {
        if (!IsConfigured)
        {
            Debug.LogError($"{nameof(DealerAssignmentManager)} on {name} needs dealers, table stations, and break-room positions.", this);
            enabled = false;
            return;
        }

        for (int i = 0; i < stations.Length; i++)
        {
            if (stations[i] == null || !stations[i].IsConfigured)
            {
                Debug.LogError($"{nameof(DealerAssignmentManager)} on {name} has an unconfigured dealer station at index {i}.", this);
                enabled = false;
                return;
            }

            if (stations[i].AssignedDealer == null)
            {
                stations[i].Attention.RequestAttention(TableAttentionReason.TableUnstaffed);
            }
        }
    }

    private void Update()
    {
        Time.timeScale = simulationTimeScale;
        RotateTiredDealers();
    }

    private void OnDisable()
    {
        Time.timeScale = previousTimeScale;
    }

    public bool TryAssignDealer(AIDealer dealer, CasinoDealerStation station)
    {
        if (!Contains(dealer) || !Contains(station) || !dealer.IsAvailable ||
            dealer.Energy < minimumEnergyForAssignment || station.PendingReplacement != null)
        {
            return false;
        }

        if (!station.BeginReplacement(dealer))
        {
            return false;
        }

        if (dealer.BeginAssignment(station))
        {
            return true;
        }

        station.CancelReplacement(dealer);
        return false;
    }

    public bool SendDealerToBreak(AIDealer dealer)
    {
        if (!Contains(dealer) || dealer.AssignedStation == null)
        {
            return false;
        }

        CasinoDealerStation station = dealer.AssignedStation;
        AIDealer replacement = FindRestedReplacement(dealer);
        if (replacement == null)
        {
            return false;
        }

        return TryAssignDealer(replacement, station);
    }

    public void NotifyDealerArrived(AIDealer dealer, CasinoDealerStation station)
    {
        if (!Contains(dealer) || !Contains(station) || station.PendingReplacement != dealer)
        {
            return;
        }

        AIDealer outgoingDealer = station.CompleteReplacement(dealer);
        if (outgoingDealer != null)
        {
            Transform restPosition = GetBreakPosition(outgoingDealer);
            if (restPosition != null)
            {
                outgoingDealer.SetBreakPosition(restPosition);
                outgoingDealer.GoToBreakRoom();
            }
        }
    }

    public void NotifyDealerCouldNotReach(AIDealer dealer, CasinoDealerStation station)
    {
        if (dealer == null || station == null || !station.CancelReplacement(dealer))
        {
            return;
        }

        dealer.GoToBreakRoom();
    }

    public Transform GetBreakPosition(AIDealer dealer)
    {
        if (breakPositions == null || breakPositions.Length == 0)
        {
            return null;
        }

        int index = Mathf.Abs(dealer.GetInstanceID()) % breakPositions.Length;
        return breakPositions[index];
    }

    private bool Contains(AIDealer dealer)
    {
        if (dealer == null || dealers == null)
        {
            return false;
        }

        for (int i = 0; i < dealers.Length; i++)
        {
            if (dealers[i] == dealer) return true;
        }

        return false;
    }

    private void RotateTiredDealers()
    {
        if (dealers == null || stations == null)
        {
            return;
        }

        foreach (CasinoDealerStation station in stations)
        {
            AIDealer assigned = station != null ? station.AssignedDealer : null;
            if (assigned == null || assigned.Energy > assigned.TiredThreshold ||
                station.PendingReplacement != null)
            {
                continue;
            }

            AIDealer replacement = FindRestedReplacement(assigned);
            if (replacement != null)
            {
                TryAssignDealer(replacement, station);
            }
        }
    }

    private AIDealer FindRestedReplacement(AIDealer outgoingDealer)
    {
        AIDealer replacement = null;
        if (dealers == null)
        {
            return replacement;
        }

        foreach (AIDealer candidate in dealers)
        {
            if (candidate != outgoingDealer && candidate != null && candidate.IsAvailable &&
                candidate.Energy >= minimumEnergyForAssignment &&
                (replacement == null || candidate.Energy > replacement.Energy))
            {
                replacement = candidate;
            }
        }

        return replacement;
    }

    private bool Contains(CasinoDealerStation station)
    {
        if (station == null || stations == null)
        {
            return false;
        }

        for (int i = 0; i < stations.Length; i++)
        {
            if (stations[i] == station) return true;
        }

        return false;
    }
}
