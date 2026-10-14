using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum CasinoPlayerInteractionKind
{
    None,
    FloatDelivery,
    PendingMinigame,
    RequestMinigame,
    PitDesk
}

[DisallowMultipleComponent]
public sealed class CasinoPlayerInteractionRouter : MonoBehaviour
{
    private CasinoPlayerController player;

    private void Awake()
    {
        player = GetComponent<CasinoPlayerController>();
    }

    private void Update()
    {
        if (CasinoMinigamePresentation.IsOpen || IsAnyInspectionOpen())
        {
            return;
        }

        PitDeskInteraction pitDesk = FindFirstObjectByType<PitDeskInteraction>();
        if (pitDesk != null && pitDesk.IsOpen)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.eKey.wasPressedThisFrame)
        {
            DispatchPrimaryInteraction();
            return;
        }

        if (keyboard.qKey.wasPressedThisFrame)
        {
            DispatchTableInspection();
        }
    }

    private void DispatchPrimaryInteraction()
    {
        CasinoPlayerFloatCarrier carrier = GetComponent<CasinoPlayerFloatCarrier>();
        List<CasinoMinigameStation> pendingStations = GetNearbyPendingStations();
        List<CasinoMinigameStationPrompt> requestablePrompts = GetNearbyRequestablePrompts();
        PitDeskInteraction pitDesk = FindFirstObjectByType<PitDeskInteraction>();
        bool carrying = carrier != null && carrier.IsCarrying;
        bool pitDeskAvailable = pitDesk != null && pitDesk.IsPlayerInRange(player.transform);
        CasinoPlayerInteractionKind action = GetPriorityAction(
            carrying,
            carrier != null && carrier.IsAtCorrectDestination,
            pendingStations.Count > 0,
            requestablePrompts.Count > 0,
            pitDeskAvailable);
        switch (action)
        {
            case CasinoPlayerInteractionKind.FloatDelivery:
                carrier.TryDeliverFromPlayerInput();
                break;
            case CasinoPlayerInteractionKind.PendingMinigame:
                pendingStations[0].TryBegin();
                break;
            case CasinoPlayerInteractionKind.RequestMinigame:
                requestablePrompts[0].TryRequestAndBegin();
                break;
            case CasinoPlayerInteractionKind.PitDesk:
                pitDesk.TryOpenFromPlayerInput();
                break;
        }
    }

    public static CasinoPlayerInteractionKind GetPriorityAction(
        bool carryingFloat,
        bool atCorrectDestination,
        bool pendingMinigame,
        bool requestableMinigame,
        bool pitDeskAvailable)
    {
        if (carryingFloat)
        {
            return atCorrectDestination
                ? CasinoPlayerInteractionKind.FloatDelivery
                : CasinoPlayerInteractionKind.None;
        }

        if (pendingMinigame)
        {
            return CasinoPlayerInteractionKind.PendingMinigame;
        }

        if (requestableMinigame)
        {
            return CasinoPlayerInteractionKind.RequestMinigame;
        }

        return pitDeskAvailable
            ? CasinoPlayerInteractionKind.PitDesk
            : CasinoPlayerInteractionKind.None;
    }

    private void DispatchTableInspection()
    {
        if (GetComponent<CasinoPlayerFloatCarrier>()?.IsCarrying == true)
        {
            return;
        }

        List<MonoBehaviour> inspections = new List<MonoBehaviour>();
        BlackjackTableInteraction[] blackjackTables =
            FindObjectsByType<BlackjackTableInteraction>(FindObjectsSortMode.None);
        foreach (BlackjackTableInteraction table in blackjackTables)
        {
            if (table != null && table.IsPlayerInRange(player.transform))
            {
                inspections.Add(table);
            }
        }

        RouletteInspectionView[] rouletteTables =
            FindObjectsByType<RouletteInspectionView>(FindObjectsSortMode.None);
        foreach (RouletteInspectionView roulette in rouletteTables)
        {
            if (roulette != null && roulette.IsPlayerInRange(player.transform))
            {
                inspections.Add(roulette);
            }
        }

        inspections.Sort((left, right) => CompareDistance(left.transform, right.transform));
        foreach (MonoBehaviour inspection in inspections)
        {
            bool opened = inspection switch
            {
                BlackjackTableInteraction blackjack => blackjack.TryOpenInspectionFromPlayerInput(),
                RouletteInspectionView roulette => roulette.TryOpenInspectionFromPlayerInput(),
                _ => false
            };
            if (opened)
            {
                return;
            }
        }
    }

    private static bool IsAnyInspectionOpen()
    {
        foreach (BlackjackTableInteraction table in
                 FindObjectsByType<BlackjackTableInteraction>(FindObjectsSortMode.None))
        {
            if (table != null && table.IsInspectionOpen)
            {
                return true;
            }
        }

        foreach (RouletteInspectionView roulette in
                 FindObjectsByType<RouletteInspectionView>(FindObjectsSortMode.None))
        {
            if (roulette != null && roulette.IsOpen)
            {
                return true;
            }
        }

        return false;
    }

    private List<CasinoMinigameStation> GetNearbyPendingStations()
    {
        CasinoMinigameStation[] found =
            FindObjectsByType<CasinoMinigameStation>(FindObjectsSortMode.None);
        List<CasinoMinigameStation> result = new List<CasinoMinigameStation>();
        foreach (CasinoMinigameStation station in found)
        {
            if (station != null && station.IsTaskPending &&
                station.IsPlayerInRange(player.transform))
            {
                result.Add(station);
            }
        }

        result.Sort((left, right) => CompareDistance(left.transform, right.transform));
        return result;
    }

    private List<CasinoMinigameStationPrompt> GetNearbyRequestablePrompts()
    {
        CasinoMinigameStationPrompt[] found =
            FindObjectsByType<CasinoMinigameStationPrompt>(FindObjectsSortMode.None);
        List<CasinoMinigameStationPrompt> result = new List<CasinoMinigameStationPrompt>();
        foreach (CasinoMinigameStationPrompt prompt in found)
        {
            if (prompt != null && prompt.CanRequestAtPlayer(player.transform))
            {
                result.Add(prompt);
            }
        }

        result.Sort((left, right) => CompareDistance(left.transform, right.transform));
        return result;
    }

    private int CompareDistance(Transform left, Transform right)
    {
        float leftDistance = (left.position - player.transform.position).sqrMagnitude;
        float rightDistance = (right.position - player.transform.position).sqrMagnitude;
        int distanceOrder = leftDistance.CompareTo(rightDistance);
        return distanceOrder != 0 ? distanceOrder : left.GetInstanceID().CompareTo(right.GetInstanceID());
    }
}
