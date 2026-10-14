using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PitPanicPrototypeGenerator
{
    private const string ScenePath = "Assets/Scenes/PitPanicPrototype.unity";
    private const string MaterialsPath = "Assets/Materials/PitPanic";
    private const string NavigationPath = "Assets/Navigation";
    private const string NavigationDataPath = NavigationPath + "/CasinoNavMesh.asset";

    [MenuItem("Pit Panic/Run Prototype Validation")]
    public static void RunPrototypeValidation()
    {
        RunBlackjackRuleChecks();
        GeneratePrototypeScene();
        ValidatePrototypeScene();
    }

    [MenuItem("Pit Panic/Generate Prototype Scene")]
    public static void GeneratePrototypeScene()
    {
        Material floorMaterial = GetOrCreateMaterial("CasinoFloor", new Color(0.08f, 0.16f, 0.17f));
        Material trimMaterial = GetOrCreateMaterial("Brass", new Color(0.76f, 0.48f, 0.16f));
        Material feltMaterial = GetOrCreateMaterial("BlackjackFelt", new Color(0.04f, 0.31f, 0.22f));
        Material woodMaterial = GetOrCreateMaterial("TableWood", new Color(0.24f, 0.10f, 0.06f));
        Material playerMaterial = GetOrCreateMaterial("Player", new Color(0.91f, 0.59f, 0.23f));
        Material accentMaterial = GetOrCreateMaterial("Accent", new Color(0.25f, 0.49f, 0.55f));
        Material[] customerMaterials =
        {
            GetOrCreateMaterial("CustomerCoral", new Color(0.94f, 0.35f, 0.28f)),
            GetOrCreateMaterial("CustomerBlue", new Color(0.27f, 0.66f, 0.86f)),
            GetOrCreateMaterial("CustomerGold", new Color(0.94f, 0.72f, 0.24f)),
            GetOrCreateMaterial("CustomerViolet", new Color(0.56f, 0.4f, 0.8f)),
            GetOrCreateMaterial("CustomerGreen", new Color(0.3f, 0.76f, 0.49f)),
            GetOrCreateMaterial("CustomerRose", new Color(0.92f, 0.46f, 0.7f))
        };
        Material[] dealerMaterials =
        {
            GetOrCreateMaterial("DealerNavy", new Color(0.12f, 0.22f, 0.36f)),
            GetOrCreateMaterial("DealerBurgundy", new Color(0.48f, 0.14f, 0.20f)),
            GetOrCreateMaterial("DealerTeal", new Color(0.10f, 0.36f, 0.33f)),
            GetOrCreateMaterial("DealerPlum", new Color(0.34f, 0.18f, 0.40f))
        };
        AssetDatabase.SaveAssets();

        SceneSetup[] previousSceneSetup = EditorSceneManager.GetSceneManagerSetup();
        bool hasSavedScenes = false;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene loadedScene = SceneManager.GetSceneAt(i);
            if (loadedScene.isLoaded && !string.IsNullOrEmpty(loadedScene.path))
            {
                hasSavedScenes = true;
            }

            if (loadedScene.isLoaded && loadedScene.path == ScenePath && loadedScene.isDirty &&
                !Application.isBatchMode &&
                !EditorUtility.DisplayDialog("Regenerate Pit Panic Prototype", "This will replace the contents of PitPanicPrototype.unity. Continue?", "Regenerate", "Cancel"))
            {
                return;
            }
        }

        if (hasSavedScenes)
        {
            if (Application.isBatchMode && EditorSceneManager.GetSceneManagerSetup().Length > 0)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    if (SceneManager.GetSceneAt(i).isDirty)
                    {
                        throw new System.InvalidOperationException("Cannot regenerate the prototype while a modified scene is open in batch mode.");
                    }
                }
            }
            else if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.48f, 0.55f, 0.62f);
        RenderSettings.ambientEquatorColor = new Color(0.30f, 0.34f, 0.35f);
        RenderSettings.ambientGroundColor = new Color(0.12f, 0.13f, 0.13f);
        RenderSettings.fog = false;

        CreateLighting();
        CreateFloor(floorMaterial, trimMaterial);
        CasinoPlayerController playerController = CreatePlayer(playerMaterial);
        CasinoSeat[] firstTableSeats = CreateBlackjackTable(
            "Blackjack Table 1",
            new Vector3(-6.2f, 0f, 0.6f),
            woodMaterial,
            feltMaterial,
            trimMaterial,
            accentMaterial,
            playerController,
            out CasinoDealerStation firstBlackjackStation,
            out CasinoFloatRefillDestination firstFloatDestination);
        CasinoSeat[] secondTableSeats = CreateBlackjackTable(
            "Blackjack Table 2",
            new Vector3(6.2f, 0f, 0.6f),
            woodMaterial,
            feltMaterial,
            trimMaterial,
            accentMaterial,
            playerController,
            out CasinoDealerStation secondBlackjackStation,
            out CasinoFloatRefillDestination secondFloatDestination);
        CasinoSeat[] seats = new CasinoSeat[firstTableSeats.Length + secondTableSeats.Length];
        firstTableSeats.CopyTo(seats, 0);
        secondTableSeats.CopyTo(seats, firstTableSeats.Length);
        (Transform entrance, Transform exit) = CreateCasinoEntrances(trimMaterial, accentMaterial);
        CasinoDealerStation rouletteStation = CreateRouletteTable(
            new Vector3(0f, 0f, 6.4f),
            woodMaterial,
            feltMaterial,
            trimMaterial,
            accentMaterial,
            playerController,
            out CasinoFloatRefillDestination rouletteFloatDestination);
        Transform[] breakPositions = CreateBreakRoom(woodMaterial, feltMaterial, trimMaterial);
        Transform cashierWaypoint = CreateCashierStation(
            new Vector3(0f, 0f, -8.1f),
            woodMaterial,
            accentMaterial,
            playerController,
            out CasinoCashDeskInteraction cashDesk);
        cashDesk.ConfigureFloatRefills(
            playerController,
            firstFloatDestination,
            secondFloatDestination,
            rouletteFloatDestination);
        PitDeskInteraction pitDesk = CreatePitDesk(new Vector3(12f, 0f, -4f), woodMaterial, trimMaterial, playerController);
        Transform[] pitDeskPositions = CreatePitDeskWaitingPositions(new Vector3(12f, 0f, -4f), trimMaterial);
        DealerAssignmentManager dealerManager = CreateDealerManager(
            new[] { firstBlackjackStation, secondBlackjackStation, rouletteStation },
            breakPositions,
            pitDeskPositions,
            pitDesk.GetComponent<CasinoTableAttention>(),
            dealerMaterials);
        pitDesk.Configure(
            playerController,
            pitDesk.GetComponent<TableInteractionZone>(),
            dealerManager,
            pitDesk.GetComponent<CasinoTableAttention>());
        CreateCustomerSpawner(entrance, exit, cashierWaypoint, seats, customerMaterials, cashDesk);
        NavMeshSurface navigationSurface = CreateNavigationSurface();
        CreateCamera();
        CreateInputEventSystem();
        SaveNavigationData(navigationSurface);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            throw new IOException($"Could not save the prototype scene to {ScenePath}.");
        }

        if (hasSavedScenes)
        {
            EditorSceneManager.RestoreSceneManagerSetup(previousSceneSetup);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Pit Panic prototype scene saved to {ScenePath}.");
    }

    [MenuItem("Pit Panic/Run Blackjack Rule Checks")]
    public static void RunBlackjackRuleChecks()
    {
        AssertTotal("ace and nine", 20, new BlackjackCard(1, 0), new BlackjackCard(9, 1));
        AssertTotal("ace softens after bust", 15, new BlackjackCard(1, 0), new BlackjackCard(9, 1), new BlackjackCard(5, 2));
        AssertTotal("multiple aces", 21, new BlackjackCard(1, 0), new BlackjackCard(1, 1), new BlackjackCard(9, 2));
        AssertOutcome("player natural", BlackjackOutcome.Blackjack, 21, 18, true, false);
        AssertOutcome("non-natural twenty one beats dealer twenty", BlackjackOutcome.Win, 21, 20, false, false);
        AssertOutcome("non-natural twenty one loses to dealer natural", BlackjackOutcome.Lose, 21, 21, false, true);
        AssertOutcome("natural push", BlackjackOutcome.Push, 21, 21, true, true);
        AssertOutcome("player bust", BlackjackOutcome.Lose, 22, 18, false, false);
        AssertOutcome("dealer bust", BlackjackOutcome.Win, 20, 22, false, false);
        AssertOutcome("equal totals", BlackjackOutcome.Push, 19, 19, false, false);
        for (int betPence = 500; betPence <= 5000; betPence += 500)
        {
            AssertPayout($"natural 3:2 gross return at {betPence}p", betPence + betPence * 3 / 2,
                BlackjackOutcome.Blackjack, betPence);
            AssertPayout($"ordinary win gross return at {betPence}p", betPence * 2,
                BlackjackOutcome.Win, betPence);
            AssertPayout($"push returns stake at {betPence}p", betPence,
                BlackjackOutcome.Push, betPence);
            AssertPayout($"loss returns nothing at {betPence}p", 0,
                BlackjackOutcome.Lose, betPence);
        }

        BlackjackChipDenomination[] chipDenominations = BlackjackChipDenomination.CreateDefaults();
        int[] expectedDenominationValues = { 500, 250, 2500, 10000 };
        string[] expectedDenominationLabels = { "£5", "£2.50", "£25", "£100" };
        if (chipDenominations.Length != expectedDenominationValues.Length)
        {
            throw new System.InvalidOperationException("The casino must use exactly four chip denominations.");
        }

        for (int i = 0; i < chipDenominations.Length; i++)
        {
            if (chipDenominations[i].ValuePence != expectedDenominationValues[i] ||
                chipDenominations[i].Label != expectedDenominationLabels[i])
            {
                throw new System.InvalidOperationException("Casino chip denominations must be red £5, blue £2.50, green £25, and black £100.");
            }
        }

        AssertChipComposition("£10 uses two £5 chips", new[] { 500, 500 }, 1000, chipDenominations);
        AssertChipComposition("£12.50 uses minimum exact chips", new[] { 500, 500, 250 }, 1250, chipDenominations);
        AssertChipComposition("£20 uses four £5 chips", new[] { 500, 500, 500, 500 }, 2000, chipDenominations);
        AssertChipComposition("£30 uses £25 and £5 chips", new[] { 2500, 500 }, 3000, chipDenominations);
        AssertChipComposition("£37.50 uses minimum exact chips", new[] { 2500, 500, 500, 250 }, 3750, chipDenominations);
        AssertChipComposition("£40 uses minimum exact chips", new[] { 2500, 500, 500, 500 }, 4000, chipDenominations);
        AssertChipComposition("£50 uses two £25 chips", new[] { 2500, 2500 }, 5000, chipDenominations);
        AssertChipComposition(
            "custom denominations use the minimum chip count",
            new[] { 3, 3 },
            6,
            new[]
            {
                new BlackjackChipDenomination("four", 4, Color.white),
                new BlackjackChipDenomination("three", 3, Color.white),
                new BlackjackChipDenomination("one", 1, Color.white)
            });
        AssertCustomerBetChoices();
        if (AIDealer.CalculateEnergyAfterWork(100f, 5f, 10f) != 50f ||
            AIDealer.CalculateEnergyAfterRest(50f, 5f, 10f) != 100f ||
            AIDealer.CalculateEnergyAfterRest(95f, 5f, 10f) != 100f ||
            !AIDealer.CanBeAssignedFromState(DealerDutyState.ReadyAtPitDesk) ||
            AIDealer.CanBeAssignedFromState(DealerDutyState.Resting) ||
            AIDealer.CanBeAssignedFromState(DealerDutyState.TravelingToPitDesk) ||
            AIDealer.CanBeAssignedFromState(DealerDutyState.Working) ||
            AIDealer.CanBeAssignedFromState(DealerDutyState.NeedsBreak) ||
            !AIDealer.CanStartBreakFromState(DealerDutyState.ReadyAtPitDesk) ||
            AIDealer.CanStartBreakFromState(DealerDutyState.Working) ||
            AIDealer.CanStartBreakFromState(DealerDutyState.NeedsBreak) ||
            AIDealer.CanStartBreakFromState(DealerDutyState.ReplacementRequested) ||
            AIDealer.CanStartBreakFromState(DealerDutyState.TravelingToBreak) ||
            !AIDealer.CanRouteToBreakFromState(DealerDutyState.TravelingToBreak) ||
            AIDealer.CanRouteToBreakFromState(DealerDutyState.TravelingToTable) ||
            CasinoCustomerAI.RoundsAfterCompletedHand(3) != 2 ||
            CasinoCustomerAI.RoundsAfterCompletedHand(1) != 0 ||
            CasinoCustomerAI.RoundsAfterCompletedHand(0) != 0 ||
            !CasinoDealerStation.CanReplaceDealer(false) ||
            CasinoDealerStation.CanReplaceDealer(true))
        {
            throw new System.InvalidOperationException("Dealer availability, visit-round decrement, or energy recovery is incorrect.");
        }

        GameObject stationObject = new GameObject("Dealer Station Reservation Test");
        CasinoTableAttention stationAttention = stationObject.AddComponent<CasinoTableAttention>();
        GameObject dealerPoint = new GameObject("Dealer Station Reservation Test Point");
        CasinoDealerStation reservationStation = stationObject.AddComponent<CasinoDealerStation>();
        reservationStation.Configure("Reservation Test", dealerPoint.transform, stationAttention, null);
        GameObject currentDealerObject = new GameObject("Current Dealer Handover Test");
        AIDealer currentDealer = currentDealerObject.AddComponent<AIDealer>();
        reservationStation.SetInitialDealer(currentDealer);
        if (currentDealer.IsAvailable || !reservationStation.CanAcceptReplacement ||
            reservationStation.BeginReplacement(null))
        {
            throw new System.InvalidOperationException("A healthy working dealer could not be replaced.");
        }

        GameObject firstReplacementObject = new GameObject("First Dealer Replacement Test");
        AIDealer firstReplacement = firstReplacementObject.AddComponent<AIDealer>();
        GameObject secondReplacementObject = new GameObject("Second Dealer Replacement Test");
        AIDealer secondReplacement = secondReplacementObject.AddComponent<AIDealer>();
        if (!reservationStation.BeginReplacement(firstReplacement) ||
            reservationStation.BeginReplacement(secondReplacement) ||
            reservationStation.PendingReplacement != firstReplacement)
        {
            throw new System.InvalidOperationException("A dealer station accepted concurrent replacement assignments.");
        }

        if (reservationStation.AssignedDealer != currentDealer)
        {
            throw new System.InvalidOperationException("A dealer station released its current dealer before replacement arrived.");
        }
        if (currentDealer.State != DealerDutyState.Working || !currentDealer.IsAtStation)
        {
            throw new System.InvalidOperationException("The outgoing dealer stopped working before replacement arrival.");
        }

        AIDealer replacedDealer = reservationStation.CompleteReplacement(firstReplacement);
        if (replacedDealer != currentDealer || reservationStation.AssignedDealer != firstReplacement ||
            reservationStation.PendingReplacement != null)
        {
            throw new System.InvalidOperationException("Dealer station replacement did not atomically hand off staffing.");
        }

        UnityEngine.Object.DestroyImmediate(currentDealerObject);
        UnityEngine.Object.DestroyImmediate(firstReplacementObject);
        UnityEngine.Object.DestroyImmediate(secondReplacementObject);
        UnityEngine.Object.DestroyImmediate(dealerPoint);
        UnityEngine.Object.DestroyImmediate(stationObject);
        int[] expectedFlickTargets = { 0, 1, 2, -1, 0, 1, 2, -1, -2 };
        if (FlickCardMinigame.RemainingOpeningCards(3) != 4 ||
            FlickCardMinigame.RemainingOpeningCards(3, 4) != 4 ||
            FlickCardMinigame.RemainingOpeningCards(3, 5) != 3 ||
            FlickCardMinigame.RemainingOpeningCards(3, 8) != 0 ||
            FlickCardMinigame.CanCompleteChallenge(4, 4, 1) ||
            FlickCardMinigame.CanCompleteChallenge(5, 4, 0) ||
            !FlickCardMinigame.CanCompleteChallenge(4, 4, 0) ||
            CasinoMinigameStation.ShouldResolveTaskOnAbandon(CasinoMinigameType.Flick) ||
            !CasinoMinigameStation.ShouldResolveTaskOnAbandon(CasinoMinigameType.Pack))
        {
            throw new System.InvalidOperationException("FLICK! retry state or remaining-card counts are incorrect.");
        }

        AssertFlickChallengeLifecycle();
        for (int dealStep = 0; dealStep < expectedFlickTargets.Length; dealStep++)
        {
            if (FlickCardMinigame.OpeningTargetForStep(3, dealStep) != expectedFlickTargets[dealStep])
            {
                throw new System.InvalidOperationException($"FLICK! recipient at opening-deal step {dealStep} is incorrect.");
            }
        }

        if (FlickCardMinigame.IsValidFlickTarget(1, 1, false) == false ||
            FlickCardMinigame.IsValidFlickTarget(2, 1, false) ||
            FlickCardMinigame.IsValidFlickTarget(1, 1, true) ||
            FlickCardMinigame.IsValidFlickTarget(-2, -2, false))
        {
            throw new System.InvalidOperationException("FLICK! accepted an invalid, duplicate, or in-flight card target.");
        }

        if (!RouletteTableMinigame.TryGetWinningNumber(0f, out int firstPocket) || firstPocket != 0 ||
            !RouletteTableMinigame.TryGetWinningNumber(0.99f, out int lastPocket) || lastPocket != 36 ||
            RouletteTableMinigame.TryGetWinningNumber(1f, out _) ||
            RouletteTableMinigame.TryGetWinningNumber(float.NaN, out _) ||
            RouletteTableMinigame.GetPocketColorName(0) != "GREEN" ||
            RouletteTableMinigame.GetPocketColorName(1) != "RED" ||
            RouletteTableMinigame.GetPocketColorName(2) != "BLACK" ||
            RouletteTableMinigame.GetPocketColorName(26) != "BLACK" ||
            RouletteTableMinigame.GetPocketColorName(32) != "RED" ||
            RouletteTableMinigame.CanCompleteSweep(2, 3) ||
            !RouletteTableMinigame.CanCompleteSweep(3, 3) ||
            RouletteTableMinigame.CanCompleteSweep(1, 0))
        {
            throw new System.InvalidOperationException("Roulette spin result mapping is invalid.");
        }

        int[] packedChips = CashDeskPackMinigame.GetExactChipComposition(4000, new[] { 500, 250, 2500, 10000 });
        if (packedChips.Length != 4 || packedChips[0] != 2500 ||
            packedChips[1] != 500 || packedChips[2] != 500 || packedChips[3] != 500 ||
            CashDeskPackMinigame.GetExactChipComposition(5000, new[] { 500, 250, 2500, 10000 }).Length != 2)
        {
            throw new System.InvalidOperationException("Cash desk chip packing does not match the pence total exactly.");
        }

        int[] denominationDisplaySequence = { 1, 0, 2, 1, 0 };
        string sequenceDisplay = CashDeskPackMinigame.BuildPackingSequenceDisplay(denominationDisplaySequence, 3);
        if (sequenceDisplay != "BLUE £2.50 • RED £5.00 • GREEN £25.00\nBLUE £2.50 • RED £5.00")
        {
            throw new System.InvalidOperationException("Cash desk sequence labels must show chip names, exact pence values, and fit deterministic rows.");
        }

        for (int resolutionIndex = 0; resolutionIndex < 3; resolutionIndex++)
        {
            Vector2 resolution = resolutionIndex == 0
                ? new Vector2(1280f, 720f)
                : resolutionIndex == 1
                    ? new Vector2(1920f, 1080f)
                    : new Vector2(2560f, 1440f);
            float scale = Mathf.Min(resolution.x / 1600f, resolution.y / 900f);
            float horizontalMargin = (resolution.x - 1520f * scale) * 0.5f;
            if (scale <= 0f || horizontalMargin < 0f)
            {
                throw new System.InvalidOperationException($"Cash desk layout exceeds the screen at {resolution.x:0}x{resolution.y:0}.");
            }
        }

        int[] expectedPackingCounts = { 10, 5, 4, 0 };
        int[] packingSequence = CashDeskPackMinigame.BuildPackingSequence(expectedPackingCounts, 127);
        int[] sequencedCounts = new int[4];
        foreach (int denominationIndex in packingSequence)
        {
            if (denominationIndex < 0 || denominationIndex >= sequencedCounts.Length)
            {
                throw new System.InvalidOperationException("Cash desk packing sequence contains an invalid denomination.");
            }

            sequencedCounts[denominationIndex]++;
        }

        bool sequenceCountsMatch = true;
        for (int i = 0; i < expectedPackingCounts.Length; i++)
        {
            sequenceCountsMatch &= sequencedCounts[i] == expectedPackingCounts[i];
        }

        int nextPackingIndex;
        bool wrongChipAdvanced = CashDeskPackMinigame.TryAdvancePackingSequence(packingSequence, 0, 3, out nextPackingIndex);
        bool rightChipAdvanced = CashDeskPackMinigame.TryAdvancePackingSequence(packingSequence, 0, packingSequence[0], out int advancedPackingIndex);
        bool completedSequenceAdvanced = CashDeskPackMinigame.TryAdvancePackingSequence(
            packingSequence,
            packingSequence.Length,
            0,
            out int completedPackingIndex);
        if (packingSequence.Length != 19 ||
            !sequenceCountsMatch ||
            CashDeskPackMinigame.BuildPackingSequence(expectedPackingCounts, 127).Length != packingSequence.Length ||
            CashDeskPackMinigame.GetSequenceTimeLimit(19) <= CashDeskPackMinigame.GetSequenceTimeLimit(10) ||
            wrongChipAdvanced || nextPackingIndex != 0 ||
            !rightChipAdvanced || advancedPackingIndex != 1 ||
            completedSequenceAdvanced || completedPackingIndex != packingSequence.Length)
        {
            throw new System.InvalidOperationException("Cash desk packing sequence does not preserve exact order quantities or scale its timer.");
        }

        GameObject floatDestinationObject = new GameObject("Float Delivery Rule Test");
        BoxCollider floatFootprint = floatDestinationObject.AddComponent<BoxCollider>();
        floatFootprint.size = new Vector3(4f, 1f, 3f);
        TableInteractionZone floatZone = floatDestinationObject.AddComponent<TableInteractionZone>();
        floatZone.Configure(floatFootprint);
        CasinoTableAttention floatAttention = floatDestinationObject.AddComponent<CasinoTableAttention>();
        CasinoFloatRefillDestination floatDestination = floatDestinationObject.AddComponent<CasinoFloatRefillDestination>();
        floatDestination.Configure("TEST", "Test Table", floatZone, floatAttention);
        CasinoFloatRefillOrder floatOrder = CasinoFloatRefillOrder.CreateStandard("float-test", "TEST", floatDestination);
        int[] correctFloatCounts = { 10, 5, 4, 0 };
        int[] incorrectFloatCounts = { 10, 5, 3, 0 };
        floatDestination.RequestAttention();
        if (!floatDestination.NeedsRefill() ||
            floatOrder.TotalPence != 16250 || floatOrder.TotalChips != 19 ||
            !floatOrder.HasExactContents(correctFloatCounts) ||
            floatOrder.HasExactContents(incorrectFloatCounts) ||
            !floatDestination.Receive(floatOrder, correctFloatCounts) ||
            floatDestination.GetChipCount(0) != 10 ||
            floatDestination.GetChipCount(1) != 5 ||
            floatDestination.GetChipCount(2) != 4 ||
            floatDestination.NeedsRefill() ||
            floatAttention.HasPendingTasks ||
            floatDestination.Receive(floatOrder, correctFloatCounts) ||
            floatDestination.Receive(floatOrder, incorrectFloatCounts))
        {
            throw new System.InvalidOperationException("Float refill packing, exact denomination totals, or duplicate delivery validation failed.");
        }

        if (!floatDestination.TryConsumeChips(new[] { 10, 0, 0, 0 }) ||
            !floatDestination.NeedsRefill() ||
            floatDestination.TryConsumeChips(new[] { 1, 0, 0, 0 }))
        {
            throw new System.InvalidOperationException("Table float consumption did not atomically trigger a low-stock refill.");
        }

        UnityEngine.Object.DestroyImmediate(floatDestinationObject);

        AssertCardViewLayout(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        AssertFlickCardFaceHierarchy();
        AssertHiddenHandRendering(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        AssertPopupScreenSpaceHitTesting();
        AssertInteractionPriority();
        AssertInteractionZoneReachability();
        if (!BlackjackRules.IsSoft17(new[] { new BlackjackCard(1, 0), new BlackjackCard(6, 1) }) ||
            !BlackjackRules.IsSoft17(new[] { new BlackjackCard(1, 0), new BlackjackCard(1, 1), new BlackjackCard(5, 2) }) ||
            BlackjackRules.DealerMustHit(new[] { new BlackjackCard(1, 0), new BlackjackCard(6, 1) }) ||
            !BlackjackRules.DealerMustHit(new[] { new BlackjackCard(10, 0), new BlackjackCard(6, 1) }))
        {
            throw new System.InvalidOperationException("Dealer rules do not stand on soft 17 and hit totals below 17.");
        }

        GameObject attentionObject = new GameObject("Attention Rule Test");
        CasinoTableAttention attention = attentionObject.AddComponent<CasinoTableAttention>();
        int attentionEvents = 0;
        attention.AttentionChanged += _ => attentionEvents++;
        attention.RequestAttention(TableAttentionReason.CustomersWaiting);
        attention.RequestAttention(TableAttentionReason.DealCards);
        if (!attention.HasPendingTasks || attention.GetPendingCount(TableAttentionReason.CustomersWaiting) != 1 ||
            attention.GetPendingCount(TableAttentionReason.DealCards) != 1 || attentionEvents != 1)
        {
            throw new System.InvalidOperationException("Table attention did not combine task reasons into one visible indicator.");
        }

        attention.ResolveAttention(TableAttentionReason.CustomersWaiting);
        if (!attention.HasPendingTasks || attentionEvents != 1)
        {
            throw new System.InvalidOperationException("Resolving one reason incorrectly hid the table attention indicator.");
        }

        attention.ResolveAttention(TableAttentionReason.DealCards);
        if (attention.HasPendingTasks || attentionEvents != 2)
        {
            throw new System.InvalidOperationException("Resolving the final reason did not hide the table attention indicator.");
        }

        attention.RequestAttention(TableAttentionReason.CustomerComplaint);
        attention.RequestAttention(TableAttentionReason.DealerReadyForAssignment);
        attention.RequestAttention(TableAttentionReason.DealerReadyForAssignment);
        if (attention.GetPendingCount(TableAttentionReason.DealerReadyForAssignment) != 2)
        {
            throw new System.InvalidOperationException("Separate pit-desk dealer decisions were not tracked independently.");
        }

        attention.ResolveAttention(TableAttentionReason.DealerReadyForAssignment);
        if (attention.GetPendingCount(TableAttentionReason.DealerReadyForAssignment) != 1 ||
            !attention.HasPendingTasks)
        {
            throw new System.InvalidOperationException("Acknowledging one dealer incorrectly cleared another pending desk decision.");
        }

        attention.ResolveAttention(TableAttentionReason.DealerReadyForAssignment);
        if (!attention.HasPendingTasks ||
            attention.GetPendingCount(TableAttentionReason.CustomerComplaint) != 1)
        {
            throw new System.InvalidOperationException("Resolving dealer decisions cleared an unrelated attention task.");
        }

        attention.ResolveAttention(TableAttentionReason.CustomerComplaint);
        attention.RequestAttentionOnce(TableAttentionReason.FlickAssistanceRequired);
        bool duplicateFlickTaskAdded = attention.RequestAttentionOnce(TableAttentionReason.FlickAssistanceRequired);
        if (duplicateFlickTaskAdded ||
            attention.GetPendingCount(TableAttentionReason.FlickAssistanceRequired) != 1)
        {
            throw new System.InvalidOperationException("Repeated FLICK assistance requests created duplicate attention tasks.");
        }

        UnityEngine.Object.DestroyImmediate(attentionObject);

        GameObject seatObject = new GameObject("Seat Rule Test");
        CasinoSeat testSeat = seatObject.AddComponent<CasinoSeat>();
        GameObject firstCustomerObject = new GameObject("First Seat Customer");
        CasinoCustomerAI firstCustomer = firstCustomerObject.AddComponent<CasinoCustomerAI>();
        GameObject secondCustomerObject = new GameObject("Second Seat Customer");
        CasinoCustomerAI secondCustomer = secondCustomerObject.AddComponent<CasinoCustomerAI>();
        if (!testSeat.TryReserve(firstCustomer) || testSeat.TryReserve(secondCustomer) ||
            !testSeat.SeatCustomer(firstCustomer) || testSeat.SeatCustomer(secondCustomer))
        {
            throw new System.InvalidOperationException("A casino seat allowed more than one customer to reserve or occupy it.");
        }

        testSeat.Release(firstCustomer);
        if (!testSeat.IsAvailable || !testSeat.TryReserve(secondCustomer))
        {
            throw new System.InvalidOperationException("A released casino seat could not be reserved by the next customer.");
        }

        UnityEngine.Object.DestroyImmediate(firstCustomerObject);
        UnityEngine.Object.DestroyImmediate(secondCustomerObject);
        UnityEngine.Object.DestroyImmediate(seatObject);

        BlackjackCard[] scriptedShoeCards =
        {
            new BlackjackCard(1, 3),
            new BlackjackCard(10, 3),
            new BlackjackCard(10, 2),
            new BlackjackCard(10, 0),
            new BlackjackCard(13, 0),
            new BlackjackCard(8, 0),
            new BlackjackCard(7, 3),
            new BlackjackCard(8, 1),
            new BlackjackCard(6, 1)
        };
        BlackjackShoe shuffledShoe = new BlackjackShoe(seed: 2703);
        HashSet<string> drawnCards = new HashSet<string>();
        for (int i = 0; i < 52; i++)
        {
            if (!drawnCards.Add(shuffledShoe.Draw().ToString()))
            {
                throw new System.InvalidOperationException("The shuffled shoe dealt a physical card more than once.");
            }
        }

        if (shuffledShoe.RemainingCards != 0)
        {
            throw new System.InvalidOperationException("The shared shoe did not remove dealt cards.");
        }

        BlackjackTableRound sharedRound = new BlackjackTableRound(new BlackjackShoe(scriptedShoeCards));
        BlackjackHand naturalHand = sharedRound.AddCustomer(null, null, 500);
        BlackjackHand pushHand = sharedRound.AddCustomer(null, null, 1500);
        BlackjackHand bustHand = sharedRound.AddCustomer(null, null, 2500);

        sharedRound.DealTo(naturalHand);
        sharedRound.DealTo(pushHand);
        sharedRound.DealTo(bustHand);
        sharedRound.DealToDealer();
        sharedRound.DealTo(naturalHand);
        sharedRound.DealTo(pushHand);
        sharedRound.DealTo(bustHand);
        sharedRound.DealToDealer();
        sharedRound.DealTo(bustHand);
        AssertThrows("customer joining during a round", () => sharedRound.AddCustomer(null, null, 1000));

        if (naturalHand.Total != 21 || !naturalHand.IsNatural || pushHand.Total != 18 ||
            bustHand.Total != 23 || sharedRound.DealerTotal != 18 || sharedRound.Shoe.RemainingCards != 0 ||
            naturalHand.BetPence != 500 || pushHand.BetPence != 1500 || bustHand.BetPence != 2500)
        {
            throw new System.InvalidOperationException("Multiple customer hands did not use the expected shared shoe and deal order.");
        }

        sharedRound.Settle();
        if (naturalHand.Outcome != BlackjackOutcome.Blackjack || pushHand.Outcome != BlackjackOutcome.Push ||
            bustHand.Outcome != BlackjackOutcome.Lose || naturalHand.PayoutPence != 1250 ||
            pushHand.PayoutPence != 1500 || bustHand.PayoutPence != 0 ||
            bustHand.Status != BlackjackHandStatus.Bust)
        {
            throw new System.InvalidOperationException("Three different customer outcomes were not settled correctly in one round.");
        }

        if (!sharedRound.TryMarkPayoutPaid(naturalHand) || sharedRound.TryMarkPayoutPaid(naturalHand) ||
            !sharedRound.TryMarkPayoutPaid(pushHand) || sharedRound.TryMarkPayoutPaid(bustHand))
        {
            throw new System.InvalidOperationException("A payout was duplicated or a losing bet incorrectly required a payout.");
        }

        AssertThrows("settling a round twice", sharedRound.Settle);

        Debug.Log("Blackjack rule checks passed.");
    }

    [MenuItem("Pit Panic/Validate Prototype Scene")]
    public static void ValidatePrototypeScene()
    {
        if (!File.Exists(ScenePath))
        {
            throw new FileNotFoundException($"Prototype scene not found at {ScenePath}.");
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null)
                {
                    throw new System.InvalidOperationException($"Missing script in generated scene object {root.name}.");
                }
            }
        }

        Camera camera = Object.FindFirstObjectByType<Camera>();
        EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
        CasinoPlayerController player = Object.FindFirstObjectByType<CasinoPlayerController>();
        BlackjackTableInteraction[] blackjackTables = Object.FindObjectsByType<BlackjackTableInteraction>(FindObjectsSortMode.None);
        CasinoDealerStation[] stations = Object.FindObjectsByType<CasinoDealerStation>(FindObjectsSortMode.None);
        DealerAssignmentManager dealerManager = Object.FindFirstObjectByType<DealerAssignmentManager>();
        PitDeskInteraction pitDesk = Object.FindFirstObjectByType<PitDeskInteraction>();
        CasinoCashDeskInteraction cashDesk = Object.FindFirstObjectByType<CasinoCashDeskInteraction>();
        RouletteTableMinigame rouletteMinigame = Object.FindFirstObjectByType<RouletteTableMinigame>();
        CasinoMinigameStationPrompt[] stationPrompts =
            Object.FindObjectsByType<CasinoMinigameStationPrompt>(FindObjectsSortMode.None);
        CashDeskPackMinigame packMinigame = Object.FindFirstObjectByType<CashDeskPackMinigame>();
        CasinoCustomerSpawner spawner = Object.FindFirstObjectByType<CasinoCustomerSpawner>();
        NavMeshSurface navigation = Object.FindFirstObjectByType<NavMeshSurface>();
        CasinoSeat[] seats = Object.FindObjectsByType<CasinoSeat>(FindObjectsSortMode.None);
        AIDealer[] dealers = Object.FindObjectsByType<AIDealer>(FindObjectsSortMode.None);
        cashDesk?.InitializeRuntime();
        if (camera == null || !camera.orthographic || eventSystem == null ||
            eventSystem.GetComponent<InputSystemUIInputModule>() == null ||
            eventSystem.GetComponent<StandaloneInputModule>() != null)
        {
            throw new System.InvalidOperationException("Generated scene needs its orthographic camera and a new Input System UI EventSystem.");
        }

        if (player == null || player.GetComponent<CasinoPlayerInteractionRouter>() == null ||
            blackjackTables.Length != 2 || seats.Length != 6 ||
            stations.Length != 3 || dealers.Length != 4 || dealerManager == null ||
            !dealerManager.IsConfigured || pitDesk == null || !pitDesk.IsConfigured ||
            dealerManager.PitDeskPositions.Count != dealers.Length ||
            dealerManager.BreakPositions.Count < dealers.Length ||
            cashDesk == null || !cashDesk.IsConfigured || rouletteMinigame == null ||
            !rouletteMinigame.IsConfigured || packMinigame == null || !packMinigame.IsConfigured ||
            stationPrompts.Length != 2 ||
            GameObject.Find("Dealer Interaction Position") != null)
        {
            throw new System.InvalidOperationException("Generated scene is missing the player, two blackjack tables, six seats, three staffed stations, four dealers, or configured pit desk.");
        }

        HashSet<AIDealer> assignedDealers = new HashSet<AIDealer>();
        foreach (CasinoDealerStation station in stations)
        {
            if (!station.IsConfigured || station.InitialDealer == null ||
                !assignedDealers.Add(station.InitialDealer) ||
                station.BlackjackTable != null && !station.BlackjackTable.IsConfigured)
            {
                throw new System.InvalidOperationException($"{station.name} has an incomplete or duplicate dealer assignment.");
            }

            ValidateNavMeshRoute(station.DealerPosition.position, null, station.name);
        }

        foreach (BlackjackTableInteraction table in blackjackTables)
        {
            ValidateBlackjackEconomy(table);
        }

        int reserveDealers = 0;
        foreach (AIDealer dealer in dealers)
        {
            if (dealer.InitialAssignedStation == null)
            {
                reserveDealers++;
            }
            else if (!assignedDealers.Contains(dealer))
            {
                throw new System.InvalidOperationException($"{dealer.name} is assigned to multiple stations or is absent from the dealer manager.");
            }
        }

        if (assignedDealers.Count != 3 || dealerManager.Dealers.Count != 4 || reserveDealers != 1)
        {
            throw new System.InvalidOperationException("The three initial casino tables must each have one unique dealer and one reserve dealer.");
        }

        CasinoFloatRefillDestination[] floatDestinations =
            Object.FindObjectsByType<CasinoFloatRefillDestination>(FindObjectsSortMode.None);
        if (floatDestinations.Length != 3 || cashDesk.Destinations.Count != 3)
        {
            throw new System.InvalidOperationException("The generated casino needs float refill destinations for both blackjack tables and roulette.");
        }

        foreach (CasinoFloatRefillDestination destination in floatDestinations)
        {
            if (!destination.IsConfigured || destination.Attention == null ||
                destination.InteractionZone == null || !destination.InteractionZone.IsConfigured ||
                destination.RefillOrderCounts.Length != 4)
            {
                throw new System.InvalidOperationException($"{destination.name} has incomplete float inventory or delivery wiring.");
            }
        }

        cashDesk.RefreshFloatRequests();
        IReadOnlyList<CasinoFloatRefillOrder> requestedFloats = cashDesk.GetRequestedOrders();
        if (requestedFloats.Count != 3 || requestedFloats[0].TotalPence != 16250 ||
            !requestedFloats[0].HasExactContents(new[] { 10, 5, 4, 0 }))
        {
            throw new System.InvalidOperationException("Low table floats did not create distinct, exact-value delivery requests.");
        }

        string testOrderId = requestedFloats[0].Id;
        int redRegisterStock = cashDesk.GetRegisterStock(0);
        if (!cashDesk.TryBeginPacking(testOrderId) ||
            cashDesk.GetOrderStatus(testOrderId) != CasinoFloatRefillStatus.Packing ||
            cashDesk.GetRegisterStock(0) != redRegisterStock - 10 ||
            !cashDesk.CancelPacking(testOrderId) ||
            cashDesk.GetOrderStatus(testOrderId) != CasinoFloatRefillStatus.Requested ||
            cashDesk.GetRegisterStock(0) != redRegisterStock)
        {
            throw new System.InvalidOperationException("Float packing cancellation did not restore the reserved chips and request.");
        }

        foreach (AIDealer dealer in dealerManager.Dealers)
        {
            if (dealer == null || !System.Array.Exists(dealers, listedDealer => listedDealer == dealer))
            {
                throw new System.InvalidOperationException("The dealer manager contains a missing or duplicate dealer reference.");
            }
        }

        foreach (BlackjackTableInteraction blackjackTable in blackjackTables)
        {
            CasinoMinigameStation flickStation = blackjackTable.GetComponent<CasinoMinigameStation>();
            FlickCardMinigame flickMinigame = blackjackTable.GetComponent<FlickCardMinigame>();
            if (!blackjackTable.IsConfigured || blackjackTable.GetComponent<TableInteractionZone>() == null ||
                !blackjackTable.GetComponent<TableInteractionZone>().IsConfigured ||
                blackjackTable.GetComponent<BlackjackTableView>() == null ||
                blackjackTable.GetComponent<CasinoTableAttention>() == null ||
                !blackjackTable.HasFlickAssistanceStation ||
                flickStation == null || !flickStation.IsConfigured ||
                flickMinigame == null || !flickMinigame.IsConfigured ||
                !HasBettingSpots(blackjackTable.transform))
            {
                throw new System.InvalidOperationException($"{blackjackTable.name} is missing its interaction, view, attention, or FLICK! system.");
            }

            string expectedTableLabel = blackjackTable.name == "Blackjack Table 1" ? "BLACKJACK 1" : "BLACKJACK 2";
            if (blackjackTable.TableDisplayName != expectedTableLabel)
            {
                throw new System.InvalidOperationException($"{blackjackTable.name} does not have its distinct blackjack table identifier.");
            }

            string[] requiredLabelNames =
            {
                "Blackjack Table 1 Label",
                "Blackjack Table 2 Label",
                "Roulette Table Label",
                "Pit Desk Label",
                "Cashier Station Label",
                "Break Room Label"
            };
            string[] requiredLabelValues =
            {
                "BLACKJACK 1",
                "BLACKJACK 2",
                "ROULETTE",
                "PIT BOSS",
                "CASHIER",
                "DEALER BREAK ROOM"
            };
            for (int labelIndex = 0; labelIndex < requiredLabelNames.Length; labelIndex++)
            {
                string labelName = requiredLabelNames[labelIndex];
                GameObject labelObject = GameObject.Find(labelName);
                CameraFacingCanvas facing = labelObject != null
                    ? labelObject.GetComponent<CameraFacingCanvas>()
                    : null;
                Canvas labelCanvas = labelObject != null ? labelObject.GetComponent<Canvas>() : null;
                Text labelText = labelObject != null ? labelObject.GetComponentInChildren<Text>(true) : null;
                if (facing == null || labelCanvas == null || labelText == null ||
                    labelText.text != requiredLabelValues[labelIndex])
                {
                    throw new System.InvalidOperationException($"{labelName} is missing its readable camera-facing canvas or expected station text.");
                }

                facing.AlignTo(camera);
                if (labelCanvas.renderMode != RenderMode.WorldSpace || !facing.IsReadableFrom(camera))
                {
                    throw new System.InvalidOperationException($"{labelName} is not screen-aligned and readable from the gameplay camera.");
                }
            }
        }

        foreach (CasinoSeat seat in seats)
        {
            if (seat.BettingPosition == null || Mathf.Abs(seat.Position.x - seat.BettingPosition.position.x) > 0.05f)
            {
                throw new System.InvalidOperationException($"{seat.name} is not aligned with its visible blackjack betting spot.");
            }
        }

        if (spawner == null || !spawner.IsConfigured || spawner.InitialCustomerCount != 4)
        {
            throw new System.InvalidOperationException("Generated scene is missing the configured four-customer spawner.");
        }

        if (navigation == null || navigation.navMeshData == null)
        {
            throw new System.InvalidOperationException("Generated scene is missing its baked NavMesh surface.");
        }

        if (Mathf.Abs(camera.transform.eulerAngles.y) > 0.1f || Vector3.Angle(camera.transform.forward, new Vector3(0f, -0.866f, 0.5f)) > 1f)
        {
            throw new System.InvalidOperationException("Generated camera is not aligned to the overhead, yaw-zero viewpoint.");
        }

        if (Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") == null)
        {
            throw new System.InvalidOperationException("Unity's LegacyRuntime.ttf font required by the blackjack panel is unavailable.");
        }

        if (NavMesh.CalculateTriangulation().vertices.Length == 0)
        {
            throw new System.InvalidOperationException("The generated NavMesh contains no walkable polygons.");
        }

        Transform entrance = GameObject.Find("Casino Entrance Point")?.transform;
        Transform exit = GameObject.Find("Casino Exit Point")?.transform;
        if (entrance == null || exit == null ||
            !NavMesh.SamplePosition(entrance.position, out _, 2f, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(exit.position, out _, 2f, NavMesh.AllAreas))
        {
            throw new System.InvalidOperationException("The generated casino entrance or exit is not reachable from the baked NavMesh.");
        }

        foreach (CasinoSeat seat in seats)
        {
            if (!NavMesh.SamplePosition(seat.Position, out NavMeshHit seatHit, 2f, NavMesh.AllAreas))
            {
                throw new System.InvalidOperationException($"{seat.name} is not reachable from the baked NavMesh.");
            }

            NavMeshPath path = new NavMeshPath();
            if (!NavMesh.CalculatePath(entrance.position, seatHit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
            {
                throw new System.InvalidOperationException($"No complete NavMesh path exists from the entrance to {seat.name}.");
            }

            if (!NavMesh.CalculatePath(seatHit.position, exit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
            {
                throw new System.InvalidOperationException($"No complete NavMesh path exists from {seat.name} to the exit.");
            }
        }

        CasinoAttentionIndicator[] indicators = Object.FindObjectsByType<CasinoAttentionIndicator>(FindObjectsSortMode.None);
        if (indicators.Length != 5 || stations.Length != 3 ||
            Object.FindObjectsByType<CasinoTableAttention>(FindObjectsSortMode.None).Length != 5)
        {
            throw new System.InvalidOperationException("Generated blackjack, roulette, cashier, and pit desk attention indicators are missing or duplicated.");
        }

        CasinoTableAttention pitDeskAttention = pitDesk.GetComponent<CasinoTableAttention>();
        CasinoAttentionIndicator pitDeskIndicator =
            pitDesk.GetComponentInChildren<CasinoAttentionIndicator>(true);
        if (pitDeskAttention == null || pitDeskIndicator == null ||
            pitDeskIndicator.Attention != pitDeskAttention)
        {
            throw new System.InvalidOperationException("Pit desk staffing decisions are not connected to the pit desk attention indicator.");
        }

        if (GameObject.Find("Roulette Table") == null || GameObject.Find("Cashier Station") == null ||
            GameObject.Find("Dealer Break Room") == null || GameObject.Find("Pit Desk") == null)
        {
            throw new System.InvalidOperationException("Generated scene is missing the roulette, cashier, break room, or pit desk.");
        }

        TableInteractionZone rouletteZone = rouletteMinigame.GetComponent<TableInteractionZone>();
        TableInteractionZone cashDeskZone = cashDesk.GetComponent<TableInteractionZone>();
        if (rouletteZone == null || !rouletteZone.IsConfigured ||
            cashDeskZone == null || !cashDeskZone.IsConfigured ||
            rouletteMinigame.GetComponent<CasinoMinigameStation>() == null ||
            cashDesk.GetComponent<CasinoMinigameStation>() == null ||
            rouletteMinigame.GetComponent<CasinoTableAttention>() == null ||
            cashDesk.GetComponent<CasinoTableAttention>() == null)
        {
            throw new System.InvalidOperationException("Roulette or cash desk is missing interaction, attention, or minigame station wiring.");
        }

        if (player.GetComponent<CasinoPlayerFloatCarrier>() == null ||
            cashDesk.GetComponent<CashDeskPackMinigame>().GetComponent<CasinoMinigameStation>() == null)
        {
            throw new System.InvalidOperationException("The float packing system is missing its player carrier or PACK! station.");
        }

        foreach (Canvas hudCanvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (hudCanvas.name.Contains("HUD") && hudCanvas.renderMode == RenderMode.ScreenSpaceOverlay &&
                hudCanvas.GetComponent<GraphicRaycaster>() == null)
            {
                throw new System.InvalidOperationException($"{hudCanvas.name} is missing its GraphicRaycaster.");
            }
        }

        foreach (Transform breakPosition in dealerManager.BreakPositions)
        {
            if (breakPosition == null ||
                !NavMesh.SamplePosition(breakPosition.position, out NavMeshHit breakHit, 2f, NavMesh.AllAreas) ||
                Vector3.Distance(breakHit.position, breakPosition.position) > 0.5f)
            {
                throw new System.InvalidOperationException("A dealer break position is missing or is not on the baked NavMesh.");
            }

            foreach (CasinoDealerStation station in stations)
            {
                NavMeshPath rotationPath = new NavMeshPath();
                if (!NavMesh.CalculatePath(breakPosition.position, station.DealerPosition.position, NavMesh.AllAreas, rotationPath) ||
                    rotationPath.status != NavMeshPathStatus.PathComplete)
                {
                    throw new System.InvalidOperationException($"No complete dealer rotation path exists from {breakPosition.name} to {station.name}.");
                }
            }
        }

        ValidateCasinoWorldPlacements(camera, blackjackTables, stations, seats, indicators, dealerManager);

        Debug.Log("Prototype scene validation passed: camera, two blackjack tables, six seats, roulette, four dealers, pit desk, attention indicators, references, and NavMesh paths are present.");
    }

    private static void ValidateCasinoWorldPlacements(
        Camera camera,
        BlackjackTableInteraction[] tables,
        CasinoDealerStation[] stations,
        CasinoSeat[] seats,
        CasinoAttentionIndicator[] indicators,
        DealerAssignmentManager manager)
    {
        HashSet<Transform> uniquePitDeskPositions = new HashSet<Transform>();
        foreach (Transform position in manager.PitDeskPositions)
        {
            if (position == null || !uniquePitDeskPositions.Add(position))
            {
                throw new System.InvalidOperationException("Pit desk dealer waiting positions are missing or shared.");
            }
        }

        HashSet<Transform> uniqueBreakPositions = new HashSet<Transform>();
        foreach (Transform position in manager.BreakPositions)
        {
            if (position == null || !uniqueBreakPositions.Add(position))
            {
                throw new System.InvalidOperationException("Dealer break positions are missing or shared.");
            }
        }

        foreach (BlackjackTableInteraction table in tables)
        {
            BoxCollider tabletop = table.GetComponentInChildren<BoxCollider>();
            if (tabletop == null)
            {
                throw new System.InvalidOperationException($"{table.name} has no physical tabletop collider.");
            }

            Vector3 tableCenter = tabletop.bounds.center;
            if (Mathf.Abs(table.transform.position.x - tableCenter.x) > 0.5f ||
                Vector3.Distance(table.transform.position, tableCenter) > 3f)
            {
                throw new System.InvalidOperationException($"{table.name} root and world-space geometry do not share a consistent origin.");
            }

            CasinoAttentionIndicator indicator = table.GetComponentInChildren<CasinoAttentionIndicator>(true);
            if (indicator == null ||
                Vector3.Distance(indicator.transform.position, table.transform.position + Vector3.up * 5.1f) > 0.1f)
            {
                throw new System.InvalidOperationException($"{table.name} attention icon is not positioned above its own table.");
            }

            CameraFacingCanvas facing = table.GetComponentInChildren<CameraFacingCanvas>(true);
            if (table.GetComponentInChildren<Text>(true) == null || facing == null)
            {
                throw new System.InvalidOperationException($"{table.name} label does not have a camera-facing text component.");
            }
        }

        foreach (CasinoSeat seat in seats)
        {
            BlackjackTableInteraction owner = seat.GetComponentInParent<BlackjackTableInteraction>();
            if (owner == null)
            {
                throw new System.InvalidOperationException($"{seat.name} is not parented to its blackjack table.");
            }

            if (!NavMesh.SamplePosition(seat.Position, out NavMeshHit approach, 0.5f, NavMesh.AllAreas) ||
                Vector3.Distance(approach.position, seat.Position) > 0.25f)
            {
                throw new System.InvalidOperationException($"{seat.name} approach point is not on the walkable NavMesh.");
            }

            if (Mathf.Abs(seat.Position.x - owner.transform.position.x) > 2.2f ||
                Mathf.Abs(seat.Position.z - owner.transform.position.z) > 3.5f ||
                Vector3.Distance(seat.Position, owner.transform.position) > 3.6f)
            {
                throw new System.InvalidOperationException($"{seat.name} is not located beside its owning table.");
            }

            Vector3 approachToBet = seat.Position - seat.BettingPosition.position;
            Vector3 seatedToBet = seat.Position + seat.SeatedVisualOffset - seat.BettingPosition.position;
            approachToBet.y = 0f;
            seatedToBet.y = 0f;
            if (approachToBet.magnitude > 2.6f || seatedToBet.magnitude > 2f)
            {
                throw new System.InvalidOperationException($"{seat.name} seated pose is not aligned with its matching betting spot.");
            }
        }

        foreach (CasinoDealerStation station in stations)
        {
            if (!NavMesh.SamplePosition(station.DealerPosition.position, out NavMeshHit dealerStand, 0.5f, NavMesh.AllAreas) ||
                Vector3.Distance(dealerStand.position, station.DealerPosition.position) > 0.25f)
            {
                throw new System.InvalidOperationException($"{station.StationName} dealer position is not directly on the NavMesh.");
            }

            bool hasReturnPosition = false;
            foreach (Transform waitingPosition in manager.PitDeskPositions)
            {
                NavMeshPath returnPath = new NavMeshPath();
                if (waitingPosition != null &&
                    NavMesh.CalculatePath(dealerStand.position, waitingPosition.position, NavMesh.AllAreas, returnPath) &&
                    returnPath.status == NavMeshPathStatus.PathComplete)
                {
                    hasReturnPosition = true;
                    break;
                }
            }

            if (!hasReturnPosition)
            {
                throw new System.InvalidOperationException($"{station.StationName} has no safe pit-desk position for an outgoing dealer.");
            }
        }

        foreach (Transform position in manager.PitDeskPositions)
        {
            if (position == null ||
                !NavMesh.SamplePosition(position.position, out NavMeshHit waitingPoint, 0.5f, NavMesh.AllAreas) ||
                Vector3.Distance(waitingPoint.position, position.position) > 0.25f)
            {
                throw new System.InvalidOperationException("A dealer waiting position beside the pit desk is unreachable.");
            }

            foreach (CasinoDealerStation station in stations)
            {
                NavMeshPath assignmentPath = new NavMeshPath();
                if (!NavMesh.CalculatePath(waitingPoint.position, station.DealerPosition.position, NavMesh.AllAreas, assignmentPath) ||
                    assignmentPath.status != NavMeshPathStatus.PathComplete)
                {
                    throw new System.InvalidOperationException($"No dealer assignment route exists from {position.name} to {station.StationName}.");
                }
            }
        }

        foreach (Transform breakPosition in manager.BreakPositions)
        {
            bool hasPitDeskRoute = false;
            foreach (Transform waitingPosition in manager.PitDeskPositions)
            {
                NavMeshPath returnPath = new NavMeshPath();
                if (NavMesh.CalculatePath(breakPosition.position, waitingPosition.position, NavMesh.AllAreas, returnPath) &&
                    returnPath.status == NavMeshPathStatus.PathComplete)
                {
                    hasPitDeskRoute = true;
                    break;
                }
            }

            if (!hasPitDeskRoute)
            {
                throw new System.InvalidOperationException($"No dealer return route exists from {breakPosition.name} to the pit desk.");
            }
        }

        GameObject pitDesk = GameObject.Find("Pit Desk");
        CasinoAttentionIndicator deskIndicator = pitDesk != null
            ? pitDesk.GetComponentInChildren<CasinoAttentionIndicator>(true)
            : null;
        if (deskIndicator == null ||
            Vector3.Distance(deskIndicator.transform.position, pitDesk.transform.position + new Vector3(0f, 3.5f, 0f)) > 0.1f)
        {
            throw new System.InvalidOperationException("The pit desk attention indicator is misplaced.");
        }

        for (int i = 0; i < indicators.Length; i++)
        {
            for (int j = i + 1; j < indicators.Length; j++)
            {
                if (indicators[i].Attention == indicators[j].Attention)
                {
                    throw new System.InvalidOperationException("Two casino indicators are attached to the same attention state.");
                }
            }
        }
    }

    private static void ValidateNavMeshRoute(Vector3 position, Transform entrance, string targetName)
    {
        if (!NavMesh.SamplePosition(position, out NavMeshHit targetHit, 2f, NavMesh.AllAreas) ||
            Vector3.Distance(targetHit.position, position) > 0.5f)
        {
            throw new System.InvalidOperationException($"{targetName} is not positioned on the baked NavMesh.");
        }

        if (entrance != null)
        {
            NavMeshPath path = new NavMeshPath();
            if (!NavMesh.CalculatePath(entrance.position, targetHit.position, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete)
            {
                throw new System.InvalidOperationException($"No complete NavMesh path exists from the entrance to {targetName}.");
            }
        }
    }

    private static bool HasBettingSpots(Transform table)
    {
        bool first = false;
        bool second = false;
        bool third = false;
        foreach (Transform child in table.GetComponentsInChildren<Transform>(true))
        {
            first |= child.name == "Blackjack Bet Spot 1";
            second |= child.name == "Blackjack Bet Spot 2";
            third |= child.name == "Blackjack Bet Spot 3";
        }

        return first && second && third;
    }

    private static void AssertTotal(string testName, int expected, params BlackjackCard[] cards)
    {
        int actual = BlackjackRules.CalculateBestTotal(cards);
        if (actual != expected)
        {
            throw new System.InvalidOperationException($"Blackjack rule check '{testName}' failed: expected {expected}, got {actual}.");
        }
    }

    private static void AssertOutcome(string testName, BlackjackOutcome expected, int playerTotal, int dealerTotal, bool playerNatural, bool dealerNatural)
    {
        BlackjackOutcome actual = BlackjackRules.ResolveOutcome(playerTotal, dealerTotal, playerNatural, dealerNatural);
        if (actual != expected)
        {
            throw new System.InvalidOperationException($"Blackjack rule check '{testName}' failed: expected {expected}, got {actual}.");
        }
    }

    private static void AssertPayout(string testName, int expected, BlackjackOutcome outcome, int betPence)
    {
        int actual = BlackjackRules.GetPayoutPence(outcome, betPence);
        if (actual != expected)
        {
            throw new System.InvalidOperationException($"Blackjack payout check '{testName}' failed: expected {expected}, got {actual}.");
        }
    }

    private static void AssertCustomerBetChoices()
    {
        GameObject firstCustomerObject = new GameObject("First Bet Selection Test Customer");
        GameObject secondCustomerObject = new GameObject("Second Bet Selection Test Customer");
        UnityEngine.Random.State savedRandomState = UnityEngine.Random.state;
        try
        {
            CasinoCustomerAI firstCustomer = firstCustomerObject.AddComponent<CasinoCustomerAI>();
            CasinoCustomerAI secondCustomer = secondCustomerObject.AddComponent<CasinoCustomerAI>();
            int[] availableStakes = { 500, 1000, 1500, 2000, 2500, 3000, 3500, 4000, 4500, 5000 };
            HashSet<int> observedStakes = new HashSet<int>();
            UnityEngine.Random.InitState(4271);
            for (int i = 0; i < 32; i++)
            {
                if (!firstCustomer.TryChooseBetPence(availableStakes, out int firstBet) ||
                    !secondCustomer.TryChooseBetPence(availableStakes, out int secondBet) ||
                    firstBet < 500 || firstBet > 5000 || firstBet % 500 != 0 ||
                    secondBet < 500 || secondBet > 5000 || secondBet % 500 != 0)
                {
                    throw new System.InvalidOperationException("Customers must independently select an available £5-increment stake between £5 and £50.");
                }

                observedStakes.Add(firstBet);
                observedStakes.Add(secondBet);
            }

            if (observedStakes.Count < 2)
            {
                throw new System.InvalidOperationException("Customer stake selection did not vary across new betting rounds.");
            }
        }
        finally
        {
            UnityEngine.Random.state = savedRandomState;
            Object.DestroyImmediate(firstCustomerObject);
            Object.DestroyImmediate(secondCustomerObject);
        }
    }

    private static void ValidateBlackjackEconomy(BlackjackTableInteraction table)
    {
        SerializedObject serializedTable = new SerializedObject(table);
        SerializedProperty betOptions = serializedTable.FindProperty("availableBetOptionsPence");
        SerializedProperty denominations = serializedTable.FindProperty("chipDenominations");
        if (betOptions == null || betOptions.arraySize != 10 ||
            denominations == null || denominations.arraySize != 4)
        {
            throw new System.InvalidOperationException($"{table.name} does not have the complete serialized stake and chip configuration.");
        }

        for (int i = 0; i < betOptions.arraySize; i++)
        {
            if (betOptions.GetArrayElementAtIndex(i).intValue != (i + 1) * 500)
            {
                throw new System.InvalidOperationException($"{table.name} must offer every £5 stake from £5 to £50.");
            }
        }

        int[] expectedValues = { 500, 250, 2500, 10000 };
        string[] expectedLabels = { "£5", "£2.50", "£25", "£100" };
        for (int i = 0; i < denominations.arraySize; i++)
        {
            SerializedProperty denomination = denominations.GetArrayElementAtIndex(i);
            SerializedProperty value = denomination.FindPropertyRelative("valuePence");
            SerializedProperty label = denomination.FindPropertyRelative("label");
            if (value == null || label == null ||
                value.intValue != expectedValues[i] || label.stringValue != expectedLabels[i])
            {
                throw new System.InvalidOperationException($"{table.name} has stale serialized chip denominations.");
            }
        }
    }

    private static void AssertThrows(string testName, System.Action action)
    {
        try
        {
            action();
        }
        catch (System.InvalidOperationException)
        {
            return;
        }

        throw new System.InvalidOperationException($"Blackjack rule check '{testName}' did not reject the invalid operation.");
    }

    private static void AssertChipComposition(string testName, int[] expectedValues, int amountPence, BlackjackChipDenomination[] denominations)
    {
        List<BlackjackChipDenomination> actual = BlackjackChipDenomination.GetExactComposition(amountPence, denominations);
        int total = 0;
        for (int i = 0; i < actual.Count; i++)
        {
            total = checked(total + actual[i].ValuePence);
        }

        if (total != amountPence || actual.Count != expectedValues.Length)
        {
            throw new System.InvalidOperationException($"Bet chip composition '{testName}' does not represent the exact bet amount.");
        }

        for (int i = 0; i < actual.Count; i++)
        {
            if (actual[i].ValuePence != expectedValues[i])
            {
                throw new System.InvalidOperationException($"Bet chip composition '{testName}' used an unexpected denomination.");
            }
        }
    }

    private static void AssertCardViewLayout(Font font)
    {
        if (font == null)
        {
            throw new System.InvalidOperationException("Cannot test card layout without the runtime UI font.");
        }

        GameObject canvasObject = new GameObject("Card View Layout Test Canvas", typeof(RectTransform), typeof(Canvas));
        GameObject cardObject = new GameObject("Card View Layout Test Card", typeof(RectTransform));
        BlackjackCardView cardView = cardObject.AddComponent<BlackjackCardView>();
        cardView.Configure(font, canvasObject.GetComponent<Canvas>());
        RectTransform cardRect = cardObject.GetComponent<RectTransform>();
        RectTransform rankRect = cardObject.transform.Find("Rank").GetComponent<RectTransform>();
        RectTransform suitRect = cardObject.transform.Find("Suit").GetComponent<RectTransform>();
        if (rankRect.parent != cardRect || suitRect.parent != cardRect ||
            rankRect.anchorMin != new Vector2(0f, 1f) || rankRect.anchorMax != rankRect.anchorMin ||
            suitRect.anchorMin != new Vector2(0.5f, 0.5f) || suitRect.anchorMax != suitRect.anchorMin ||
            rankRect.anchoredPosition.x + rankRect.sizeDelta.x > cardRect.sizeDelta.x ||
            -rankRect.anchoredPosition.y + rankRect.sizeDelta.y > cardRect.sizeDelta.y)
        {
            throw new System.InvalidOperationException("Playing card labels are not anchored within their card background.");
        }

        string[] expectedSuits = { "♠", "♥", "♦", "♣" };
        for (int suit = 0; suit < expectedSuits.Length; suit++)
        {
            if (!font.HasCharacter(expectedSuits[suit][0]))
            {
                throw new System.InvalidOperationException($"The runtime UI font does not contain the {expectedSuits[suit]} playing-card suit glyph.");
            }

            cardView.SetCard(new BlackjackCard(1, suit), true);
            Text suitText = suitRect.GetComponent<Text>();
            bool shouldBeRed = suit == 1 || suit == 2;
            Color expectedInk = shouldBeRed ? new Color(0.72f, 0.08f, 0.08f) : new Color(0.08f, 0.10f, 0.12f);
            if (suitText.text != expectedSuits[suit] || suitText.color != expectedInk)
            {
                throw new System.InvalidOperationException($"Playing card suit {suit} has the wrong glyph or ink color.");
            }
        }

        UnityEngine.Object.DestroyImmediate(cardObject);
        UnityEngine.Object.DestroyImmediate(canvasObject);
    }

    private static void AssertFlickCardFaceHierarchy()
    {
        GameObject card = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Transform cardTransform = card.transform;
        cardTransform.localPosition = new Vector3(1f, 2f, 3f);
        cardTransform.localRotation = Quaternion.Euler(5f, 15f, 25f);
        cardTransform.localScale = new Vector3(0.48f, 0.035f, 0.68f);
        Vector3 positionBefore = cardTransform.localPosition;
        Quaternion rotationBefore = cardTransform.localRotation;
        Vector3 scaleBefore = cardTransform.localScale;

        if (!FlickCardMinigame.TryCreateCardFaceText(cardTransform, new BlackjackCard(12, 1), out TextMesh face) ||
            face == null || face.transform.parent != cardTransform ||
            cardTransform.GetComponent<TextMesh>() != null ||
            cardTransform.localPosition != positionBefore ||
            cardTransform.localRotation != rotationBefore ||
            cardTransform.localScale != scaleBefore ||
            face.text != "Q♥")
        {
            throw new System.InvalidOperationException("FLICK card face text is missing, attached to the physical card, or changed its transform.");
        }

        if (!FlickCardMinigame.TryCreateCardFaceText(cardTransform, new BlackjackCard(1, 3), out TextMesh updatedFace) ||
            updatedFace != face || updatedFace.text != "A♣" ||
            cardTransform.Find("Card Face") == null ||
            FlickCardMinigame.TryCreateCardFaceText(cardTransform, new BlackjackCard(0, 0), out _))
        {
            throw new System.InvalidOperationException("FLICK card face update duplicated its child or accepted an invalid card.");
        }

        UnityEngine.Object.DestroyImmediate(card);
    }

    private static void AssertFlickChallengeLifecycle()
    {
        int cancelledDealStep = 4;
        int firstAttemptCards = FlickCardMinigame.RemainingOpeningCards(3, cancelledDealStep);
        if (firstAttemptCards != 4)
        {
            throw new System.InvalidOperationException("A new FLICK challenge did not begin with all four remaining opening cards.");
        }

        int firstAttemptTarget = FlickCardMinigame.OpeningTargetForStep(3, cancelledDealStep);
        cancelledDealStep++;
        if (firstAttemptTarget != 0 || FlickCardMinigame.RemainingOpeningCards(3, cancelledDealStep) != 3)
        {
            throw new System.InvalidOperationException("Cancelling after one successful flick did not preserve exactly three remaining cards.");
        }

        int retryCards = FlickCardMinigame.RemainingOpeningCards(3, cancelledDealStep);
        int retrySuccesses = 0;
        while (FlickCardMinigame.RemainingOpeningCards(3, cancelledDealStep) > 0)
        {
            if (FlickCardMinigame.OpeningTargetForStep(3, cancelledDealStep) == -2)
            {
                throw new System.InvalidOperationException("A FLICK retry produced an invalid remaining-card target.");
            }

            cancelledDealStep++;
            retrySuccesses++;
        }

        if (!FlickCardMinigame.CanCompleteChallenge(retrySuccesses, retryCards, 0))
        {
            throw new System.InvalidOperationException("A cancelled FLICK challenge could not complete on retry.");
        }

        int nextRoundDealStep = 4;
        int nextRoundCards = FlickCardMinigame.RemainingOpeningCards(3, nextRoundDealStep);
        for (int i = 0; i < nextRoundCards; i++)
        {
            if (FlickCardMinigame.OpeningTargetForStep(3, nextRoundDealStep) == -2)
            {
                throw new System.InvalidOperationException("A consecutive FLICK challenge produced an invalid target.");
            }

            nextRoundDealStep++;
        }

        if (!FlickCardMinigame.CanCompleteChallenge(
                nextRoundCards,
                nextRoundCards,
                FlickCardMinigame.RemainingOpeningCards(3, nextRoundDealStep)))
        {
            throw new System.InvalidOperationException("A second consecutive FLICK challenge did not complete cleanly.");
        }
    }

    private static void AssertHiddenHandRendering(Font font)
    {
        GameObject canvasObject = new GameObject("Hidden Hand Rule Test Canvas", typeof(RectTransform), typeof(Canvas));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        GameObject board = new GameObject("Hidden Hand Rule Test Board", typeof(RectTransform));
        board.transform.SetParent(canvasObject.transform, false);
        GameObject handObject = new GameObject("Hidden Hand Rule Test", typeof(RectTransform));
        handObject.transform.SetParent(board.transform, false);
        BlackjackHandView hand = handObject.AddComponent<BlackjackHandView>();
        GameObject shoeObject = new GameObject("Hidden Hand Rule Test Shoe", typeof(RectTransform));
        shoeObject.transform.SetParent(canvasObject.transform, false);
        hand.Configure(font, canvas, shoeObject.GetComponent<RectTransform>());
        board.SetActive(false);

        BlackjackCard[] cards = { new BlackjackCard(10, 0) };
        hand.Render(cards, -1, true, BlackjackHandStatus.Playing, true);
        BlackjackCardView[] cardViews = board.GetComponentsInChildren<BlackjackCardView>(true);
        if (cardViews.Length != 1 || cardViews[0].IsAnimating ||
            cardViews[0].GetComponent<CanvasGroup>() != null)
        {
            throw new System.InvalidOperationException("Rendering a hidden blackjack hand started a card animation.");
        }

        board.SetActive(true);
        hand.Render(cards, -1, true, BlackjackHandStatus.Playing, true);
        if (board.GetComponentsInChildren<BlackjackCardView>(true).Length != 1 ||
            cardViews[0].IsAnimating || cardViews[0].GetComponent<RectTransform>().localScale != Vector3.one)
        {
            throw new System.InvalidOperationException("A blackjack card did not remain correctly visible when its hand UI opened.");
        }

        UnityEngine.Object.DestroyImmediate(canvasObject);
    }

    private static void AssertPopupScreenSpaceHitTesting()
    {
        GameObject canvasObject = new GameObject(
            "Popup Hit Test Canvas", typeof(RectTransform), typeof(Canvas));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        GameObject targetObject = new GameObject("Popup Hit Test Target", typeof(RectTransform));
        targetObject.transform.SetParent(canvasObject.transform, false);
        RectTransform target = targetObject.GetComponent<RectTransform>();
        target.anchorMin = target.anchorMax = new Vector2(0.5f, 0.5f);
        target.sizeDelta = new Vector2(240f, 150f);
        Vector2 center = RectTransformUtility.WorldToScreenPoint(null, target.position);
        if (!CasinoMinigamePresentation.ContainsScreenPoint(target, center) ||
            CasinoMinigamePresentation.ContainsScreenPoint(target, center + new Vector2(500f, 500f)))
        {
            throw new System.InvalidOperationException("Popup hit testing does not use the visible screen-space RectTransform bounds.");
        }

        UnityEngine.Object.DestroyImmediate(canvasObject);
    }

    private static void AssertInteractionPriority()
    {
        if (CasinoPlayerInteractionRouter.GetPriorityAction(true, true, true, true, true) !=
                CasinoPlayerInteractionKind.FloatDelivery ||
            CasinoPlayerInteractionRouter.GetPriorityAction(true, false, true, true, true) !=
                CasinoPlayerInteractionKind.None ||
            CasinoPlayerInteractionRouter.GetPriorityAction(false, false, true, true, true) !=
                CasinoPlayerInteractionKind.PendingMinigame ||
            CasinoPlayerInteractionRouter.GetPriorityAction(false, false, false, true, true) !=
                CasinoPlayerInteractionKind.RequestMinigame ||
            CasinoPlayerInteractionRouter.GetPriorityAction(false, false, false, false, true) !=
                CasinoPlayerInteractionKind.PitDesk ||
            CasinoPlayerInteractionRouter.GetPriorityAction(false, false, false, false, false) !=
                CasinoPlayerInteractionKind.None)
        {
            throw new System.InvalidOperationException("Player interaction routing violates float/task/station priority or allows a second action.");
        }
    }

    private static void AssertInteractionZoneReachability()
    {
        GameObject table = new GameObject("Interaction Zone Rule Test Table");
        BoxCollider footprint = table.AddComponent<BoxCollider>();
        footprint.center = new Vector3(0f, 1f, 0f);
        footprint.size = new Vector3(4f, 2f, 2f);
        TableInteractionZone zone = table.AddComponent<TableInteractionZone>();
        zone.Configure(footprint, 0.6f);
        GameObject playerObject = new GameObject("Interaction Zone Rule Test Player");
        Vector3[] reachablePositions =
        {
            new Vector3(0f, 0f, 1.4f),
            new Vector3(0f, 0f, -1.4f),
            new Vector3(2.4f, 0f, 0f),
            new Vector3(-2.4f, 0f, 0f)
        };

        foreach (Vector3 position in reachablePositions)
        {
            playerObject.transform.position = position;
            Physics.SyncTransforms();
            if (!zone.IsPlayerInRange(playerObject.transform))
            {
                throw new System.InvalidOperationException($"Table interaction was unavailable from reachable side at {position}.");
            }
        }

        playerObject.transform.position = new Vector3(0f, 0f, 1.7f);
        Physics.SyncTransforms();
        if (zone.IsPlayerInRange(playerObject.transform))
        {
            throw new System.InvalidOperationException("Table interaction remained available outside the configured distance.");
        }

        GameObject wall = new GameObject("Interaction Zone Rule Test Wall");
        wall.transform.position = new Vector3(0f, 0.5f, -1.2f);
        wall.AddComponent<BoxCollider>().size = new Vector3(0.5f, 1f, 0.3f);
        playerObject.transform.position = new Vector3(0f, 0f, -1.4f);
        Physics.SyncTransforms();
        if (zone.IsPlayerInRange(playerObject.transform))
        {
            throw new System.InvalidOperationException("Table interaction incorrectly passed through a blocking wall.");
        }

        UnityEngine.Object.DestroyImmediate(wall);
        UnityEngine.Object.DestroyImmediate(playerObject);
        UnityEngine.Object.DestroyImmediate(table);
        Physics.SyncTransforms();
    }

    private static void CreateFloor(Material floorMaterial, Material trimMaterial)
    {
        GameObject floor = new GameObject("Casino Floor");
        CreatePrimitive(PrimitiveType.Cube, "Floor", new Vector3(0f, -0.25f, 0f), new Vector3(32f, 0.5f, 22f), floorMaterial, floor.transform);

        CreatePrimitive(PrimitiveType.Cube, "North Brass Border", new Vector3(0f, 0.03f, 10.65f), new Vector3(31.5f, 0.08f, 0.12f), trimMaterial, floor.transform);
        CreatePrimitive(PrimitiveType.Cube, "South Brass Border", new Vector3(0f, 0.03f, -10.65f), new Vector3(31.5f, 0.08f, 0.12f), trimMaterial, floor.transform);
        CreatePrimitive(PrimitiveType.Cube, "East Brass Border", new Vector3(15.65f, 0.03f, 0f), new Vector3(0.12f, 0.08f, 21.3f), trimMaterial, floor.transform);
        CreatePrimitive(PrimitiveType.Cube, "West Brass Border", new Vector3(-15.65f, 0.03f, 0f), new Vector3(0.12f, 0.08f, 21.3f), trimMaterial, floor.transform);

        for (int x = -1; x <= 1; x += 2)
        {
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 position = new Vector3(x * 14.4f, 2f, z * 9.3f);
                CreatePrimitive(PrimitiveType.Cube, "Decorative Column", position, new Vector3(0.8f, 4f, 0.8f), trimMaterial, floor.transform);
                CreatePrimitive(PrimitiveType.Cube, "Column Base", new Vector3(position.x, 0.25f, position.z), new Vector3(1.1f, 0.5f, 1.1f), floorMaterial, floor.transform);
            }
        }
    }

    private static CasinoSeat[] CreateBlackjackTable(
        string tableName,
        Vector3 center,
        Material woodMaterial,
        Material feltMaterial,
        Material trimMaterial,
        Material accentMaterial,
        CasinoPlayerController playerController,
        out CasinoDealerStation dealerStation,
        out CasinoFloatRefillDestination floatDestination)
    {
        GameObject table = new GameObject(tableName);
        table.transform.position = center;
        NavMeshModifier navMeshModifier = table.AddComponent<NavMeshModifier>();
        navMeshModifier.overrideArea = true;
        navMeshModifier.area = 1;

        GameObject tabletop = CreatePrimitive(PrimitiveType.Cube, "Tabletop", center + new Vector3(0f, 1.15f, 0f), new Vector3(5.2f, 0.32f, 2.6f), woodMaterial, table.transform);
        CreatePrimitive(PrimitiveType.Cube, "Felt Playing Surface", center + new Vector3(0f, 1.34f, 0f), new Vector3(4.85f, 0.08f, 2.25f), feltMaterial, table.transform);

        for (int x = -1; x <= 1; x += 2)
        {
            for (int z = -1; z <= 1; z += 2)
            {
                CreatePrimitive(
                    PrimitiveType.Cylinder,
                    "Table Leg",
                    center + new Vector3(x * 1.8f, 0.56f, z * 0.78f),
                    new Vector3(0.24f, 0.56f, 0.24f),
                    trimMaterial,
                    table.transform);
            }
        }

        CreatePrimitive(PrimitiveType.Cube, "Dealer Rail", center + new Vector3(0f, 1.62f, 1.18f), new Vector3(5.2f, 0.42f, 0.22f), woodMaterial, table.transform);
        CreatePrimitive(PrimitiveType.Cube, "Dealer Rail Accent", center + new Vector3(0f, 1.84f, 1.18f), new Vector3(4.5f, 0.05f, 0.12f), accentMaterial, table.transform);

        for (int x = -1; x <= 1; x++)
        {
            CreatePrimitive(
                PrimitiveType.Cylinder,
                "Player Stool",
                center + new Vector3(x * 1.7f, 0.42f, -1.85f),
                new Vector3(0.52f, 0.42f, 0.52f),
                woodMaterial,
                table.transform);
            CreatePrimitive(
                PrimitiveType.Cylinder,
                "Stool Cushion",
                center + new Vector3(x * 1.7f, 0.86f, -1.85f),
                new Vector3(0.55f, 0.08f, 0.55f),
                feltMaterial,
                table.transform);
        }

        CasinoTableAttention attention = table.AddComponent<CasinoTableAttention>();
        CreateAttentionIndicator(table.transform, attention, center + Vector3.up * 5.1f);
        string destinationId = tableName == "Blackjack Table 1" ? "BJ1" : "BJ2";
        string tableDisplayName = destinationId == "BJ1" ? "BLACKJACK 1" : "BLACKJACK 2";
        CreateWorldLabel(table.transform, $"{tableName} Label", tableDisplayName, center + new Vector3(0f, 2.2f, -1.5f));

        CasinoSeat[] seats = new CasinoSeat[3];
        float[] seatOffsets = { -1.7f, 0f, 1.7f };
        for (int i = 0; i < seats.Length; i++)
        {
            Vector3 betSpotWorld = center + new Vector3(seatOffsets[i], 1.43f, -0.65f);
            CreatePrimitive(PrimitiveType.Cylinder, $"Blackjack Bet Spot {i + 1} Rim", betSpotWorld, new Vector3(0.63f, 0.018f, 0.63f), trimMaterial, table.transform);
            CreatePrimitive(PrimitiveType.Cylinder, $"Blackjack Bet Spot {i + 1}", betSpotWorld + new Vector3(0f, 0.035f, 0f), new Vector3(0.53f, 0.018f, 0.53f), feltMaterial, table.transform);

            GameObject bettingPosition = new GameObject($"Betting Position {i + 1}");
            bettingPosition.transform.SetParent(table.transform, false);
            bettingPosition.transform.localPosition = new Vector3(seatOffsets[i], 1.47f, -0.65f);

            GameObject seatObject = new GameObject($"Customer Seat {i + 1}");
            seatObject.transform.SetParent(table.transform, false);
            seatObject.transform.localPosition = new Vector3(seatOffsets[i], 0f, -3f);
            GameObject customerPosition = new GameObject("Customer Sitting Position");
            customerPosition.transform.SetParent(seatObject.transform, false);
            customerPosition.transform.localPosition = new Vector3(0f, 0f, 0.62f);
            customerPosition.transform.localRotation = Quaternion.identity;
            CasinoSeat seat = seatObject.AddComponent<CasinoSeat>();
            seat.Configure(customerPosition.transform, bettingPosition.transform, seatObject.transform);
            seats[i] = seat;
        }

        TableInteractionZone interactionZone = table.AddComponent<TableInteractionZone>();
        interactionZone.Configure(tabletop.GetComponent<BoxCollider>());
        floatDestination = table.AddComponent<CasinoFloatRefillDestination>();
        floatDestination.Configure(destinationId, tableName, interactionZone, attention);
        table.AddComponent<BlackjackTableView>();
        BlackjackTableInteraction interaction = table.AddComponent<BlackjackTableInteraction>();
        interaction.Configure(playerController, interactionZone, attention, seats, destinationId);
        CasinoMinigameStation flickStation = table.AddComponent<CasinoMinigameStation>();
        FlickCardMinigame flickMinigame = table.AddComponent<FlickCardMinigame>();
        interaction.ConfigureFlickAssistance(flickStation);
        flickMinigame.Configure(interaction, playerController, table.transform);
        flickStation.Configure(
            playerController,
            interactionZone,
            attention,
            flickMinigame,
            CasinoMinigameType.Flick,
            TableAttentionReason.FlickAssistanceRequired);
        GameObject dealerPosition = new GameObject("Dealer Station Position");
        dealerPosition.transform.SetParent(table.transform, false);
        dealerPosition.transform.localPosition = new Vector3(0f, 0f, 2.35f);
        dealerStation = table.AddComponent<CasinoDealerStation>();
        dealerStation.Configure(tableName, dealerPosition.transform, attention, interaction);
        return seats;
    }

    private static void CreateAttentionIndicator(Transform tableRoot, CasinoTableAttention attention, Vector3 worldPosition)
    {
        GameObject indicator = new GameObject("Table Attention Indicator", typeof(RectTransform), typeof(Canvas));
        indicator.transform.SetParent(tableRoot, false);
        indicator.transform.position = worldPosition;
        Canvas canvas = indicator.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 20;
        RectTransform canvasRect = indicator.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(180f, 180f);
        indicator.transform.localScale = Vector3.one * 0.012f;

        GameObject textObject = new GameObject("Exclamation Mark", typeof(RectTransform));
        textObject.transform.SetParent(indicator.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = "!";
        text.fontSize = 150;
        text.fontStyle = FontStyle.Bold;
        text.color = new Color(1f, 0.84f, 0.08f);
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        Outline outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.32f, 0.07f, 0.015f);
        outline.effectDistance = new Vector2(4f, -4f);
        indicator.AddComponent<CasinoAttentionIndicator>().Configure(attention, canvas, text);
        canvas.enabled = false;
    }

    private static void CreateWorldLabel(Transform parent, string objectName, string value, Vector3 position)
    {
        GameObject labelObject = new GameObject(objectName, typeof(RectTransform), typeof(Canvas));
        labelObject.transform.SetParent(parent, false);
        labelObject.transform.position = position;
        Canvas canvas = labelObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        labelObject.AddComponent<CameraFacingCanvas>().Configure(canvas);
        RectTransform canvasRect = labelObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(420f, 84f);
        labelObject.transform.localScale = Vector3.one * 0.01f;

        GameObject textObject = new GameObject("Label Text", typeof(RectTransform));
        textObject.transform.SetParent(labelObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = 44;
        text.fontStyle = FontStyle.Bold;
        text.color = new Color(1f, 0.88f, 0.60f);
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
    }

    private static void CreateInputEventSystem()
    {
        GameObject eventSystemObject = new GameObject("EventSystem");
        EventSystem eventSystem = eventSystemObject.AddComponent<EventSystem>();
        InputSystemUIInputModule inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
        inputModule.AssignDefaultActions();
    }

    private static CasinoDealerStation CreateRouletteTable(
        Vector3 center,
        Material woodMaterial,
        Material feltMaterial,
        Material trimMaterial,
        Material accentMaterial,
        CasinoPlayerController player,
        out CasinoFloatRefillDestination floatDestination)
    {
        GameObject table = new GameObject("Roulette Table");
        table.transform.position = center;
        NavMeshModifier modifier = table.AddComponent<NavMeshModifier>();
        modifier.overrideArea = true;
        modifier.area = 1;
        GameObject counter = CreatePrimitive(PrimitiveType.Cube, "Roulette Counter", center + new Vector3(0f, 1.05f, 0f), new Vector3(4.4f, 0.32f, 2.6f), woodMaterial, table.transform);
        CreatePrimitive(PrimitiveType.Cylinder, "Roulette Wheel Rim", center + new Vector3(0f, 1.32f, 0f), new Vector3(1.65f, 0.14f, 1.65f), trimMaterial, table.transform);
        GameObject wheel = CreatePrimitive(PrimitiveType.Cylinder, "Roulette Wheel", center + new Vector3(0f, 1.42f, 0f), new Vector3(1.38f, 0.12f, 1.38f), feltMaterial, table.transform);
        CreatePrimitive(PrimitiveType.Cylinder, "Roulette Hub", center + new Vector3(0f, 1.52f, 0f), new Vector3(0.36f, 0.11f, 0.36f), accentMaterial, table.transform);

        CasinoTableAttention attention = table.AddComponent<CasinoTableAttention>();
        CreateAttentionIndicator(table.transform, attention, center + Vector3.up * 4f);
        CreateWorldLabel(table.transform, "Roulette Table Label", "ROULETTE", center + new Vector3(0f, 2.2f, -1.5f));
        TableInteractionZone interactionZone = table.AddComponent<TableInteractionZone>();
        interactionZone.Configure(counter.GetComponent<BoxCollider>());
        floatDestination = table.AddComponent<CasinoFloatRefillDestination>();
        floatDestination.Configure("ROULETTE", "Roulette Table", interactionZone, attention);
        RouletteTableMinigame minigame = table.AddComponent<RouletteTableMinigame>();
        minigame.Configure(player, table.transform, wheel.transform, attention);
        CasinoMinigameStation minigameStation = table.AddComponent<CasinoMinigameStation>();
        minigameStation.Configure(
            player,
            interactionZone,
            attention,
            minigame,
            CasinoMinigameType.Spin,
            TableAttentionReason.RouletteSpinRequired);
        table.AddComponent<CasinoMinigameStationPrompt>().Configure(
            player,
            interactionZone,
            attention,
            minigameStation,
            CasinoMinigameType.Spin,
            TableAttentionReason.RouletteSpinRequired,
            "SPIN! AND SWEEP THE ROULETTE TABLE",
            allowTaskRequestWhenIdle: true);
        GameObject dealerPosition = new GameObject("Dealer Station Position");
        dealerPosition.transform.SetParent(table.transform, false);
        dealerPosition.transform.localPosition = new Vector3(0f, 0f, 2.25f);
        CasinoDealerStation station = table.AddComponent<CasinoDealerStation>();
        station.Configure("Roulette Table", dealerPosition.transform, attention, null);
        return station;
    }

    private static Transform[] CreateBreakRoom(Material woodMaterial, Material feltMaterial, Material trimMaterial)
    {
        GameObject room = new GameObject("Dealer Break Room");
        CreatePrimitive(PrimitiveType.Cube, "Break Room Rug", new Vector3(-11.4f, 0.015f, 5.1f), new Vector3(7.2f, 0.04f, 4.6f), feltMaterial, room.transform);
        CreatePrimitive(PrimitiveType.Cube, "Break Room Sign", new Vector3(-11.4f, 2.9f, 7.45f), new Vector3(5f, 0.48f, 0.25f), trimMaterial, room.transform);
        CreatePrimitive(PrimitiveType.Cube, "Break Room Bench", new Vector3(-11.4f, 0.45f, 6.6f), new Vector3(4.5f, 0.25f, 0.8f), woodMaterial, room.transform);
        CreateWorldLabel(room.transform, "Break Room Label", "DEALER BREAK ROOM", new Vector3(-11.4f, 3.25f, 7.35f));

        Transform[] positions = new Transform[4];
        Vector3[] offsets =
        {
            new Vector3(-1.45f, 0f, -0.75f),
            new Vector3(0.15f, 0f, -0.75f),
            new Vector3(1.75f, 0f, -0.75f),
            new Vector3(0f, 0f, -1.75f)
        };
        for (int i = 0; i < positions.Length; i++)
        {
            GameObject position = new GameObject($"Dealer Rest Position {i + 1}");
            position.transform.SetParent(room.transform, false);
            position.transform.position = new Vector3(-11.4f, 0f, 5.1f) + offsets[i];
            position.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            positions[i] = position.transform;
            CreatePrimitive(PrimitiveType.Cylinder, $"Resting Stool {i + 1}", position.transform.position + Vector3.right * 0.65f + Vector3.up * 0.2f, new Vector3(0.55f, 0.2f, 0.55f), woodMaterial, room.transform);
        }

        return positions;
    }

    private static Transform CreateCashierStation(
        Vector3 center,
        Material woodMaterial,
        Material accentMaterial,
        CasinoPlayerController player,
        out CasinoCashDeskInteraction cashDeskInteraction)
    {
        GameObject cashier = new GameObject("Cashier Station");
        cashier.transform.position = center;
        GameObject counter = CreatePrimitive(PrimitiveType.Cube, "Cashier Counter", center + new Vector3(0f, 0.65f, 0f), new Vector3(4.2f, 1.3f, 1.2f), woodMaterial, cashier.transform);
        CreatePrimitive(PrimitiveType.Cube, "Cashier Sign", center + new Vector3(0f, 1.55f, 0.7f), new Vector3(2.4f, 0.45f, 0.15f), accentMaterial, cashier.transform);
        CreateWorldLabel(cashier.transform, "Cashier Station Label", "CASHIER", center + new Vector3(0f, 1.9f, 0.65f));
        GameObject waypoint = new GameObject("Cashier Customer Waypoint");
        waypoint.transform.position = center + new Vector3(0f, 0f, -1.5f);
        CasinoTableAttention attention = cashier.AddComponent<CasinoTableAttention>();
        CreateAttentionIndicator(cashier.transform, attention, center + Vector3.up * 3.2f);
        TableInteractionZone zone = cashier.AddComponent<TableInteractionZone>();
        zone.Configure(counter.GetComponent<BoxCollider>(), 1.5f);
        CashDeskPackMinigame packMinigame = cashier.AddComponent<CashDeskPackMinigame>();
        packMinigame.Configure(player, cashier.transform);
        CasinoMinigameStation station = cashier.AddComponent<CasinoMinigameStation>();
        station.Configure(
            player,
            zone,
            attention,
            packMinigame,
            CasinoMinigameType.Pack,
            TableAttentionReason.CashDeskPackRequired);
        cashier.AddComponent<CasinoMinigameStationPrompt>().Configure(
            player,
            zone,
            attention,
            station,
            CasinoMinigameType.Pack,
            TableAttentionReason.CashDeskPackRequired,
            "PACK TABLE FLOAT",
            allowTaskRequestWhenIdle: false);
        cashDeskInteraction = cashier.AddComponent<CasinoCashDeskInteraction>();
        cashDeskInteraction.Configure(attention, station, packMinigame);
        return waypoint.transform;
    }

    private static PitDeskInteraction CreatePitDesk(
        Vector3 center,
        Material woodMaterial,
        Material trimMaterial,
        CasinoPlayerController player)
    {
        GameObject desk = new GameObject("Pit Desk");
        desk.transform.position = center;
        GameObject counter = CreatePrimitive(PrimitiveType.Cube, "Pit Boss Counter", center + new Vector3(0f, 0.65f, 0f), new Vector3(3.4f, 1.3f, 1.4f), woodMaterial, desk.transform);
        CreatePrimitive(PrimitiveType.Cube, "Pit Desk Sign", center + new Vector3(0f, 1.65f, 0.75f), new Vector3(2.5f, 0.4f, 0.14f), trimMaterial, desk.transform);
        CreateWorldLabel(desk.transform, "Pit Desk Label", "PIT BOSS", center + new Vector3(0f, 2f, 0.7f));
        CasinoTableAttention attention = desk.AddComponent<CasinoTableAttention>();
        CreateAttentionIndicator(desk.transform, attention, center + Vector3.up * 3.5f);
        TableInteractionZone zone = desk.AddComponent<TableInteractionZone>();
        zone.Configure(counter.GetComponent<BoxCollider>(), 1.5f);
        PitDeskInteraction interaction = desk.AddComponent<PitDeskInteraction>();
        interaction.Configure(player, zone, null, attention);
        return interaction;
    }

    private static Transform[] CreatePitDeskWaitingPositions(Vector3 deskCenter, Material trimMaterial)
    {
        Transform[] positions = new Transform[4];
        float[] offsets = { -1.35f, -0.45f, 0.45f, 1.35f };
        for (int i = 0; i < positions.Length; i++)
        {
            GameObject position = new GameObject($"Pit Desk Dealer Position {i + 1}");
            position.transform.position = deskCenter + new Vector3(offsets[i], 0f, 1.55f);
            position.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            positions[i] = position.transform;
            GameObject marker = CreatePrimitive(
                PrimitiveType.Cylinder,
                $"Pit Desk Waiting Marker {i + 1}",
                position.transform.position + Vector3.up * 0.025f,
                new Vector3(0.62f, 0.025f, 0.62f),
                trimMaterial,
                position.transform);
            Object.DestroyImmediate(marker.GetComponent<Collider>());
        }

        return positions;
    }

    private static DealerAssignmentManager CreateDealerManager(
        CasinoDealerStation[] stations,
        Transform[] breakPositions,
        Transform[] pitDeskPositions,
        CasinoTableAttention pitDeskAttention,
        Material[] dealerMaterials)
    {
        GameObject managerObject = new GameObject("Dealer Assignment Manager");
        DealerAssignmentManager manager = managerObject.AddComponent<DealerAssignmentManager>();
        AIDealer[] dealers = new AIDealer[4];
        manager.Configure(dealers, stations, breakPositions, pitDeskPositions, pitDeskAttention);

        for (int i = 0; i < dealers.Length; i++)
        {
            CasinoDealerStation station = i < stations.Length ? stations[i] : null;
            Transform initialPitPosition = station == null ? pitDeskPositions[0] : null;
            Transform spawnPosition = station != null ? station.DealerPosition : initialPitPosition;
            GameObject dealerObject = new GameObject($"Dealer {i + 1:00}");
            dealerObject.transform.SetPositionAndRotation(spawnPosition.position, spawnPosition.rotation);
            NavMeshAgent agent = dealerObject.AddComponent<NavMeshAgent>();
            agent.radius = 0.28f;
            agent.height = 1.7f;
            AIDealer dealer = dealerObject.AddComponent<AIDealer>();
            CreateDealerVisual(dealerObject.transform, dealerMaterials[i], i);
            dealer.Configure(
                manager,
                breakPositions[i],
                initialPitPosition,
                station,
                station != null ? 100f : 97f,
                manager.FullRestThreshold);
            dealers[i] = dealer;
        }

        manager.Configure(dealers, stations, breakPositions, pitDeskPositions, pitDeskAttention);
        return manager;
    }

    private static void CreateDealerVisual(Transform parent, Material material, int index)
    {
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Dealer Uniform";
        body.transform.SetParent(parent, false);
        body.transform.localPosition = new Vector3(0f, 0.76f, 0f);
        body.transform.localScale = new Vector3(0.55f, 0.76f, 0.55f);
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.GetComponent<Renderer>().sharedMaterial = material;

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Dealer Head";
        head.transform.SetParent(parent, false);
        head.transform.localPosition = new Vector3(0f, 1.58f, 0f);
        head.transform.localScale = Vector3.one * 0.38f;
        Object.DestroyImmediate(head.GetComponent<Collider>());
        head.GetComponent<Renderer>().sharedMaterial = material;

        GameObject nameBadge = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nameBadge.name = $"Dealer Badge {index + 1}";
        nameBadge.transform.SetParent(parent, false);
        nameBadge.transform.localPosition = new Vector3(0f, 0.92f, -0.29f);
        nameBadge.transform.localScale = new Vector3(0.22f, 0.1f, 0.035f);
        Object.DestroyImmediate(nameBadge.GetComponent<Collider>());
        nameBadge.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static (Transform entrance, Transform exit) CreateCasinoEntrances(Material trimMaterial, Material accentMaterial)
    {
        Transform entrance = CreateEntrance("Casino Entrance", new Vector3(-10f, 0f, -7.2f), trimMaterial);
        Transform exit = CreateEntrance("Casino Exit", new Vector3(10f, 0f, -7.2f), accentMaterial);
        return (entrance, exit);
    }

    private static Transform CreateEntrance(string name, Vector3 position, Material material)
    {
        GameObject entrance = new GameObject(name);
        entrance.transform.position = position;
        CreatePrimitive(PrimitiveType.Cube, "Entrance Sign", position + new Vector3(0f, 3.1f, 0f), new Vector3(3.2f, 0.38f, 0.35f), material, entrance.transform);
        CreatePrimitive(PrimitiveType.Cube, "Left Entrance Post", position + new Vector3(-1.45f, 1.5f, 0f), new Vector3(0.3f, 3f, 0.35f), material, entrance.transform);
        CreatePrimitive(PrimitiveType.Cube, "Right Entrance Post", position + new Vector3(1.45f, 1.5f, 0f), new Vector3(0.3f, 3f, 0.35f), material, entrance.transform);
        GameObject point = new GameObject(name + " Point");
        point.transform.SetParent(entrance.transform, false);
        return point.transform;
    }

    private static void CreateCustomerSpawner(
        Transform entrance,
        Transform exit,
        Transform cashierWaypoint,
        CasinoSeat[] seats,
        Material[] customerMaterials,
        CasinoCashDeskInteraction cashDesk)
    {
        GameObject spawnerObject = new GameObject("Customer Spawner");
        CasinoCustomerSpawner spawner = spawnerObject.AddComponent<CasinoCustomerSpawner>();
        spawner.Configure(entrance, exit, cashierWaypoint, seats, customerMaterials, cashDesk);
    }

    private static NavMeshSurface CreateNavigationSurface()
    {
        GameObject navigationObject = new GameObject("Casino Navigation");
        NavMeshSurface surface = navigationObject.AddComponent<NavMeshSurface>();
        surface.agentTypeID = 0;
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ~0;
        surface.BuildNavMesh();
        if (surface.navMeshData == null)
        {
            throw new System.InvalidOperationException("Unity AI Navigation could not bake a NavMesh for the casino floor.");
        }

        return surface;
    }

    private static void SaveNavigationData(NavMeshSurface surface)
    {
        EnsureFolder(NavigationPath);
        if (surface == null || surface.navMeshData == null)
        {
            throw new System.InvalidOperationException("The generated scene has no baked casino NavMesh.");
        }

        AssetDatabase.DeleteAsset(NavigationDataPath);
        AssetDatabase.CreateAsset(surface.navMeshData, NavigationDataPath);
        EditorUtility.SetDirty(surface);
        AssetDatabase.SaveAssets();
    }

    private static CasinoPlayerController CreatePlayer(Material playerMaterial)
    {
        GameObject player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 0f, -6f);
        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.38f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        controller.stepOffset = 0.3f;
        controller.slopeLimit = 45f;
        player.AddComponent<CasinoPlayerController>();
        if (player.GetComponent<CasinoPlayerInteractionRouter>() == null)
        {
            player.AddComponent<CasinoPlayerInteractionRouter>();
        }

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Player Visual";
        visual.transform.SetParent(player.transform, false);
        visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        visual.transform.localScale = new Vector3(0.76f, 0.9f, 0.76f);
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.GetComponent<Renderer>().sharedMaterial = playerMaterial;
        return player.GetComponent<CasinoPlayerController>();
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 11f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 100f;
        camera.backgroundColor = new Color(0.055f, 0.075f, 0.09f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        cameraObject.AddComponent<AudioListener>();
        cameraObject.transform.position = new Vector3(0f, 16f, -9f);
        cameraObject.transform.LookAt(new Vector3(0f, 0.4f, 0f));
    }

    private static void CreateLighting()
    {
        GameObject sunObject = new GameObject("Casino Key Light");
        Light sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.82f, 0.62f);
        sun.intensity = 1.5f;
        sun.shadows = LightShadows.Soft;
        sunObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        for (int x = -1; x <= 1; x++)
        {
            GameObject lightObject = new GameObject("Warm Ceiling Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.68f, 0.36f);
            light.intensity = 7f;
            light.range = 10f;
            light.shadows = LightShadows.None;
            lightObject.transform.position = new Vector3(x * 7f, 6f, 0f);
        }
    }

    private static GameObject CreatePrimitive(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material, Transform parent)
    {
        GameObject primitive = GameObject.CreatePrimitive(type);
        primitive.name = name;
        primitive.transform.SetParent(parent, true);
        primitive.transform.position = position;
        primitive.transform.localScale = scale;
        primitive.GetComponent<Renderer>().sharedMaterial = material;
        return primitive;
    }

    private static Material GetOrCreateMaterial(string materialName, Color color)
    {
        EnsureFolder("Assets/Materials");
        EnsureFolder(MaterialsPath);

        string assetPath = $"{MaterialsPath}/{materialName}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                throw new System.InvalidOperationException("Could not find a supported shader for the prototype materials.");
            }

            material = new Material(shader);
            material.color = color;
            AssetDatabase.CreateAsset(material, assetPath);
        }

        return material;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            string parentPath = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            string folderName = Path.GetFileName(folderPath);
            if (string.IsNullOrEmpty(parentPath) || !AssetDatabase.IsValidFolder(parentPath))
            {
                throw new DirectoryNotFoundException($"Could not create folder {folderPath} because its parent does not exist.");
            }

            AssetDatabase.CreateFolder(parentPath, folderName);
        }
    }
}
