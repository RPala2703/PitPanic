using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
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
            out CasinoDealerStation firstBlackjackStation);
        CasinoSeat[] secondTableSeats = CreateBlackjackTable(
            "Blackjack Table 2",
            new Vector3(6.2f, 0f, 0.6f),
            woodMaterial,
            feltMaterial,
            trimMaterial,
            accentMaterial,
            playerController,
            out CasinoDealerStation secondBlackjackStation);
        CasinoSeat[] seats = new CasinoSeat[firstTableSeats.Length + secondTableSeats.Length];
        firstTableSeats.CopyTo(seats, 0);
        secondTableSeats.CopyTo(seats, firstTableSeats.Length);
        (Transform entrance, Transform exit) = CreateCasinoEntrances(trimMaterial, accentMaterial);
        CreateCustomerSpawner(entrance, exit, seats, customerMaterials);
        CasinoDealerStation rouletteStation = CreateRouletteTable(new Vector3(0f, 0f, 6.4f), woodMaterial, feltMaterial, trimMaterial, accentMaterial);
        Transform[] breakPositions = CreateBreakRoom(woodMaterial, feltMaterial, trimMaterial);
        CreateCashierStation(new Vector3(0f, 0f, -8.1f), woodMaterial, accentMaterial);
        PitDeskInteraction pitDesk = CreatePitDesk(new Vector3(12f, 0f, -4f), woodMaterial, trimMaterial, playerController);
        DealerAssignmentManager dealerManager = CreateDealerManager(
            new[] { firstBlackjackStation, secondBlackjackStation, rouletteStation },
            breakPositions,
            dealerMaterials);
        pitDesk.Configure(playerController, pitDesk.GetComponent<TableInteractionZone>(), dealerManager);
        NavMeshSurface navigationSurface = CreateNavigationSurface();
        CreateCamera();
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
        AssertOutcome("natural push", BlackjackOutcome.Push, 21, 21, true, true);
        AssertOutcome("player bust", BlackjackOutcome.Lose, 22, 18, false, false);
        AssertOutcome("dealer bust", BlackjackOutcome.Win, 20, 22, false, false);
        AssertOutcome("equal totals", BlackjackOutcome.Push, 19, 19, false, false);
        AssertPayout("natural blackjack return at a £10 bet", 2500, BlackjackOutcome.Blackjack, 1000);
        AssertPayout("ordinary win at a £30 bet", 6000, BlackjackOutcome.Win, 3000);
        AssertPayout("ordinary win return", 4000, BlackjackOutcome.Win, 2000);
        AssertPayout("push returns stake", 2000, BlackjackOutcome.Push, 2000);
        AssertPayout("loss returns nothing", 0, BlackjackOutcome.Lose, 2000);
        BlackjackChipDenomination[] chipDenominations = BlackjackChipDenomination.CreateDefaults();
        AssertChipComposition("£10 uses two £5 chips", new[] { 500, 500 }, 1000, chipDenominations);
        AssertChipComposition("£20 uses four £5 chips", new[] { 500, 500, 500, 500 }, 2000, chipDenominations);
        AssertChipComposition("£30 uses £25 and £5 chips", new[] { 2500, 500 }, 3000, chipDenominations);
        AssertChipComposition("£40 uses £25, £10, and £5 chips", new[] { 2500, 1000, 500 }, 4000, chipDenominations);
        AssertChipComposition("£50 uses two £25 chips", new[] { 2500, 2500 }, 5000, chipDenominations);
        if (AIDealer.CalculateEnergyAfterWork(100f, 5f, 10f) != 50f ||
            AIDealer.CalculateEnergyAfterRest(50f, 5f, 10f) != 100f ||
            AIDealer.CalculateEnergyAfterRest(95f, 5f, 10f) != 100f)
        {
            throw new System.InvalidOperationException("Dealer work energy or break-room energy recovery is incorrect.");
        }

        GameObject stationObject = new GameObject("Dealer Station Reservation Test");
        CasinoTableAttention stationAttention = stationObject.AddComponent<CasinoTableAttention>();
        GameObject dealerPoint = new GameObject("Dealer Station Reservation Test Point");
        CasinoDealerStation reservationStation = stationObject.AddComponent<CasinoDealerStation>();
        reservationStation.Configure("Reservation Test", dealerPoint.transform, stationAttention, null);
        GameObject currentDealerObject = new GameObject("Current Dealer Handover Test");
        AIDealer currentDealer = currentDealerObject.AddComponent<AIDealer>();
        reservationStation.SetInitialDealer(currentDealer);
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
        AssertCardViewLayout(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
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
        BlackjackHand naturalHand = sharedRound.AddCustomer(null, null, 1000);
        BlackjackHand pushHand = sharedRound.AddCustomer(null, null, 3000);
        BlackjackHand bustHand = sharedRound.AddCustomer(null, null, 5000);

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
            naturalHand.BetPence != 1000 || pushHand.BetPence != 3000 || bustHand.BetPence != 5000)
        {
            throw new System.InvalidOperationException("Multiple customer hands did not use the expected shared shoe and deal order.");
        }

        sharedRound.Settle();
        if (naturalHand.Outcome != BlackjackOutcome.Blackjack || pushHand.Outcome != BlackjackOutcome.Push ||
            bustHand.Outcome != BlackjackOutcome.Lose || naturalHand.PayoutPence != 2500 ||
            pushHand.PayoutPence != 3000 || bustHand.PayoutPence != 0 ||
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
        CasinoPlayerController player = Object.FindFirstObjectByType<CasinoPlayerController>();
        BlackjackTableInteraction[] blackjackTables = Object.FindObjectsByType<BlackjackTableInteraction>(FindObjectsSortMode.None);
        CasinoDealerStation[] stations = Object.FindObjectsByType<CasinoDealerStation>(FindObjectsSortMode.None);
        DealerAssignmentManager dealerManager = Object.FindFirstObjectByType<DealerAssignmentManager>();
        PitDeskInteraction pitDesk = Object.FindFirstObjectByType<PitDeskInteraction>();
        CasinoCustomerSpawner spawner = Object.FindFirstObjectByType<CasinoCustomerSpawner>();
        NavMeshSurface navigation = Object.FindFirstObjectByType<NavMeshSurface>();
        CasinoSeat[] seats = Object.FindObjectsByType<CasinoSeat>(FindObjectsSortMode.None);
        AIDealer[] dealers = Object.FindObjectsByType<AIDealer>(FindObjectsSortMode.None);
        if (camera == null || !camera.orthographic)
        {
            throw new System.InvalidOperationException("Generated scene does not have an orthographic camera.");
        }

        if (player == null || blackjackTables.Length != 2 || seats.Length != 6 ||
            stations.Length != 3 || dealers.Length != 4 || dealerManager == null ||
            !dealerManager.IsConfigured || pitDesk == null || !pitDesk.IsConfigured ||
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

        foreach (AIDealer dealer in dealerManager.Dealers)
        {
            if (dealer == null || !System.Array.Exists(dealers, listedDealer => listedDealer == dealer))
            {
                throw new System.InvalidOperationException("The dealer manager contains a missing or duplicate dealer reference.");
            }
        }

        foreach (BlackjackTableInteraction blackjackTable in blackjackTables)
        {
            if (!blackjackTable.IsConfigured || blackjackTable.GetComponent<TableInteractionZone>() == null ||
                !blackjackTable.GetComponent<TableInteractionZone>().IsConfigured ||
                blackjackTable.GetComponent<BlackjackTableView>() == null ||
                blackjackTable.GetComponent<CasinoTableAttention>() == null ||
                !HasBettingSpots(blackjackTable.transform))
            {
                throw new System.InvalidOperationException($"{blackjackTable.name} is missing its interaction, view, or attention system.");
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
        if (indicators.Length != 4 || stations.Length != 3 ||
            Object.FindObjectsByType<CasinoTableAttention>(FindObjectsSortMode.None).Length != 4)
        {
            throw new System.InvalidOperationException("Generated blackjack, roulette, and pit desk attention indicators are missing or duplicated.");
        }

        if (GameObject.Find("Roulette Table") == null || GameObject.Find("Cashier Station") == null ||
            GameObject.Find("Dealer Break Room") == null || GameObject.Find("Pit Desk") == null)
        {
            throw new System.InvalidOperationException("Generated scene is missing the roulette, cashier, break room, or pit desk.");
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

        Debug.Log("Prototype scene validation passed: camera, two blackjack tables, six seats, roulette, four dealers, pit desk, attention indicators, references, and NavMesh paths are present.");
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
        out CasinoDealerStation dealerStation)
    {
        GameObject table = new GameObject(tableName);
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
        CreateAttentionIndicator(table.transform, attention);
        CreateWorldLabel(table.transform, $"{tableName} Label", tableName.ToUpperInvariant(), center + new Vector3(0f, 2.2f, -1.5f));

        CasinoSeat[] seats = new CasinoSeat[3];
        float[] seatOffsets = { -1.7f, 0f, 1.7f };
        for (int i = 0; i < seats.Length; i++)
        {
            Vector3 betSpotWorld = center + new Vector3(seatOffsets[i], 1.43f, -0.65f);
            CreatePrimitive(PrimitiveType.Cylinder, $"Blackjack Bet Spot {i + 1} Rim", betSpotWorld, new Vector3(0.63f, 0.018f, 0.63f), trimMaterial, table.transform);
            CreatePrimitive(PrimitiveType.Cylinder, $"Blackjack Bet Spot {i + 1}", betSpotWorld + new Vector3(0f, 0.035f, 0f), new Vector3(0.53f, 0.018f, 0.53f), feltMaterial, table.transform);

            GameObject bettingPosition = new GameObject($"Betting Position {i + 1}");
            bettingPosition.transform.SetParent(table.transform, false);
            bettingPosition.transform.localPosition = new Vector3(seatOffsets[i], 1.47f, 0.65f);

            GameObject seatObject = new GameObject($"Customer Seat {i + 1}");
            seatObject.transform.SetParent(table.transform, false);
            seatObject.transform.localPosition = new Vector3(seatOffsets[i], 0f, -1.35f);
            GameObject customerPosition = new GameObject("Customer Sitting Position");
            customerPosition.transform.SetParent(seatObject.transform, false);
            CasinoSeat seat = seatObject.AddComponent<CasinoSeat>();
            seat.Configure(customerPosition.transform, bettingPosition.transform);
            seats[i] = seat;
        }

        TableInteractionZone interactionZone = table.AddComponent<TableInteractionZone>();
        interactionZone.Configure(tabletop.GetComponent<BoxCollider>());
        table.AddComponent<BlackjackTableView>();
        BlackjackTableInteraction interaction = table.AddComponent<BlackjackTableInteraction>();
        interaction.Configure(playerController, interactionZone, attention, seats);
        GameObject dealerPosition = new GameObject("Dealer Station Position");
        dealerPosition.transform.SetParent(table.transform, false);
        dealerPosition.transform.localPosition = center + new Vector3(0f, 0f, 2.35f);
        dealerStation = table.AddComponent<CasinoDealerStation>();
        dealerStation.Configure(tableName, dealerPosition.transform, attention, interaction);
        return seats;
    }

    private static void CreateAttentionIndicator(Transform tableRoot, CasinoTableAttention attention)
    {
        GameObject indicator = new GameObject("Table Attention Indicator", typeof(RectTransform), typeof(Canvas));
        indicator.transform.SetParent(tableRoot, false);
        indicator.transform.localPosition = new Vector3(0f, 5.1f, 0f);
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
        labelObject.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        Canvas canvas = labelObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
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

    private static CasinoDealerStation CreateRouletteTable(
        Vector3 center,
        Material woodMaterial,
        Material feltMaterial,
        Material trimMaterial,
        Material accentMaterial)
    {
        GameObject table = new GameObject("Roulette Table");
        NavMeshModifier modifier = table.AddComponent<NavMeshModifier>();
        modifier.overrideArea = true;
        modifier.area = 1;
        CreatePrimitive(PrimitiveType.Cube, "Roulette Counter", center + new Vector3(0f, 1.05f, 0f), new Vector3(4.4f, 0.32f, 2.6f), woodMaterial, table.transform);
        CreatePrimitive(PrimitiveType.Cylinder, "Roulette Wheel Rim", center + new Vector3(0f, 1.32f, 0f), new Vector3(1.65f, 0.14f, 1.65f), trimMaterial, table.transform);
        CreatePrimitive(PrimitiveType.Cylinder, "Roulette Wheel", center + new Vector3(0f, 1.42f, 0f), new Vector3(1.38f, 0.12f, 1.38f), feltMaterial, table.transform);
        CreatePrimitive(PrimitiveType.Cylinder, "Roulette Hub", center + new Vector3(0f, 1.52f, 0f), new Vector3(0.36f, 0.11f, 0.36f), accentMaterial, table.transform);

        CasinoTableAttention attention = table.AddComponent<CasinoTableAttention>();
        CreateAttentionIndicator(table.transform, attention);
        CreateWorldLabel(table.transform, "Roulette Table Label", "ROULETTE", center + new Vector3(0f, 2.2f, -1.5f));
        GameObject dealerPosition = new GameObject("Dealer Station Position");
        dealerPosition.transform.SetParent(table.transform, false);
        dealerPosition.transform.localPosition = center + new Vector3(0f, 0f, 2.25f);
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

    private static void CreateCashierStation(Vector3 center, Material woodMaterial, Material accentMaterial)
    {
        GameObject cashier = new GameObject("Cashier Station");
        CreatePrimitive(PrimitiveType.Cube, "Cashier Counter", center + new Vector3(0f, 0.65f, 0f), new Vector3(4.2f, 1.3f, 1.2f), woodMaterial, cashier.transform);
        CreatePrimitive(PrimitiveType.Cube, "Cashier Sign", center + new Vector3(0f, 1.55f, 0.7f), new Vector3(2.4f, 0.45f, 0.15f), accentMaterial, cashier.transform);
        CreateWorldLabel(cashier.transform, "Cashier Station Label", "CASHIER", center + new Vector3(0f, 1.9f, 0.65f));
    }

    private static PitDeskInteraction CreatePitDesk(
        Vector3 center,
        Material woodMaterial,
        Material trimMaterial,
        CasinoPlayerController player)
    {
        GameObject desk = new GameObject("Pit Desk");
        GameObject counter = CreatePrimitive(PrimitiveType.Cube, "Pit Boss Counter", center + new Vector3(0f, 0.65f, 0f), new Vector3(3.4f, 1.3f, 1.4f), woodMaterial, desk.transform);
        CreatePrimitive(PrimitiveType.Cube, "Pit Desk Sign", center + new Vector3(0f, 1.65f, 0.75f), new Vector3(2.5f, 0.4f, 0.14f), trimMaterial, desk.transform);
        CreateWorldLabel(desk.transform, "Pit Desk Label", "PIT BOSS", center + new Vector3(0f, 2f, 0.7f));
        CasinoTableAttention attention = desk.AddComponent<CasinoTableAttention>();
        CreateAttentionIndicator(desk.transform, attention);
        TableInteractionZone zone = desk.AddComponent<TableInteractionZone>();
        zone.Configure(counter.GetComponent<BoxCollider>(), 1.5f);
        PitDeskInteraction interaction = desk.AddComponent<PitDeskInteraction>();
        interaction.Configure(player, zone, null);
        return interaction;
    }

    private static DealerAssignmentManager CreateDealerManager(
        CasinoDealerStation[] stations,
        Transform[] breakPositions,
        Material[] dealerMaterials)
    {
        GameObject managerObject = new GameObject("Dealer Assignment Manager");
        DealerAssignmentManager manager = managerObject.AddComponent<DealerAssignmentManager>();
        AIDealer[] dealers = new AIDealer[4];
        manager.Configure(dealers, stations, breakPositions);

        for (int i = 0; i < dealers.Length; i++)
        {
            CasinoDealerStation station = i < stations.Length ? stations[i] : null;
            Transform spawnPosition = station != null ? station.DealerPosition : breakPositions[3];
            GameObject dealerObject = new GameObject($"Dealer {i + 1:00}");
            dealerObject.transform.SetPositionAndRotation(spawnPosition.position, spawnPosition.rotation);
            NavMeshAgent agent = dealerObject.AddComponent<NavMeshAgent>();
            agent.radius = 0.28f;
            agent.height = 1.7f;
            AIDealer dealer = dealerObject.AddComponent<AIDealer>();
            CreateDealerVisual(dealerObject.transform, dealerMaterials[i], i);
            dealer.Configure(manager, breakPositions[i], station, i < stations.Length ? 100f : 72f);
            dealers[i] = dealer;
        }

        manager.Configure(dealers, stations, breakPositions);
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

    private static void CreateCustomerSpawner(Transform entrance, Transform exit, CasinoSeat[] seats, Material[] customerMaterials)
    {
        GameObject spawnerObject = new GameObject("Customer Spawner");
        CasinoCustomerSpawner spawner = spawnerObject.AddComponent<CasinoCustomerSpawner>();
        spawner.Configure(entrance, exit, seats, customerMaterials);
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
