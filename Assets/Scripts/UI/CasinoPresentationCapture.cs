#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public sealed class CasinoPresentationCapture : MonoBehaviour
{
    private string directory;
    private int oldTutorial;
    private bool watch;
    private bool demoCheck;
    private bool coverCheck;
    private bool seatingCheck;
    private bool securityCheck;
    private bool titleCheck;
    private bool vipCheck;
    private bool resultsPreview;
    private readonly Dictionary<string, int> savedProgress = new Dictionary<string, int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartCapture()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "--pit-capture-dir");
        if (index < 0 || index + 1 >= args.Length) return;
        Application.runInBackground = true;
        CasinoPresentationCapture capture = new GameObject("Presentation Capture").AddComponent<CasinoPresentationCapture>();
        DontDestroyOnLoad(capture.gameObject);
        capture.directory = args[index + 1];
        capture.watch = Array.IndexOf(args, "--pit-watch") >= 0;
        capture.demoCheck = Array.IndexOf(args, "--pit-demo-check") >= 0;
        capture.coverCheck = Array.IndexOf(args, "--pit-cover-check") >= 0;
        capture.seatingCheck = Array.IndexOf(args, "--pit-seating-check") >= 0;
        capture.securityCheck = Array.IndexOf(args, "--pit-security-check") >= 0;
        capture.titleCheck = Array.IndexOf(args, "--pit-title-check") >= 0;
        capture.vipCheck = Array.IndexOf(args, "--pit-vip-check") >= 0;
        capture.resultsPreview = Array.IndexOf(args, "--pit-results-preview") >= 0;
        if (capture.titleCheck) Application.logMessageReceived += capture.OnTitleLog;
        capture.oldTutorial = PlayerPrefs.GetInt("PitPanic.TutorialComplete", -1);
        foreach (string key in new[] { "PitPanic.TutorialComplete", "PitPanic.UnlockedShift",
            CasinoDemoProgression.CreditsKey, CasinoDemoProgression.UpgradeKeys[0],
            CasinoDemoProgression.UpgradeKeys[1], CasinoDemoProgression.UpgradeKeys[2] })
            capture.savedProgress[key] = PlayerPrefs.GetInt(key, -1);
        if (capture.demoCheck)
            PlayerPrefs.SetInt("PitPanic.TutorialComplete", 1);
        Directory.CreateDirectory(capture.directory);
    }

    private IEnumerator Start()
    {
        if (resultsPreview)
        {
            yield return new WaitForSecondsRealtime(2.2f);
            string[] args = Environment.GetCommandLineArgs();
            int outcomeIndex = Array.IndexOf(args, "--pit-results-preview-outcome");
            bool passed = outcomeIndex >= 0 && outcomeIndex + 1 < args.Length &&
                args[outcomeIndex + 1].Equals("pass", StringComparison.OrdinalIgnoreCase);
            CasinoMusicTrack expectedTrack = passed ? CasinoMusicTrack.ResultsPassed : CasinoMusicTrack.ResultsFailed;
            CasinoSoundscape soundscape = FindFirstObjectByType<CasinoSoundscape>();
            AudioSource resultsMusic = soundscape != null
                ? (AudioSource)typeof(CasinoSoundscape).GetField("music", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(soundscape)
                : null;
            if (soundscape == null || resultsMusic == null || CasinoSoundscape.CurrentMusicTrack != expectedTrack ||
                !resultsMusic.isPlaying || resultsMusic.loop)
                throw new InvalidOperationException($"Results preview did not start its one-shot {expectedTrack} track.");
            Debug.Log($"Verified results preview music: {resultsMusic.clip.name}.");
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, "results-preview.png"));
            Debug.Log("Captured the full-screen results preview.");
            yield return new WaitForSecondsRealtime(.5f);
            Application.Quit();
            yield break;
        }
        if (titleCheck)
        {
            yield return CaptureTitle();
            yield break;
        }
        if (vipCheck)
        {
            yield return CaptureHighRollerVips();
            yield break;
        }
        yield return new WaitForSecondsRealtime(2);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "title.png"));
        yield return new WaitForSecondsRealtime(1);
        CasinoTitleScreen title = FindFirstObjectByType<CasinoTitleScreen>();
        title.transform.Find("Start").GetComponent<Button>().onClick.Invoke();
        yield return new WaitForSecondsRealtime(CasinoTitleScreen.TransitionSeconds + .2f);
        CasinoShiftController menuShift = FindFirstObjectByType<CasinoShiftController>();
        GameObject levelSelect = (GameObject)typeof(CasinoShiftController)
            .GetField("levelSelect", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(menuShift);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "level-select.png"));
        yield return new WaitForEndOfFrame();
        levelSelect.transform.Find("Level 1").GetComponent<Button>().onClick.Invoke();
        yield return new WaitForSecondsRealtime(3);
        if (watch || demoCheck || coverCheck || seatingCheck || securityCheck)
        {
            CasinoShiftController shift = FindFirstObjectByType<CasinoShiftController>();
            if (shift.IsLessonOpen)
                typeof(CasinoShiftController).GetMethod("CompleteTutorial", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(shift, null);
            yield return null;
        }

        if (!watch && !demoCheck && !coverCheck && !seatingCheck && !securityCheck)
        {
            CasinoShiftController guidedShift = FindFirstObjectByType<CasinoShiftController>();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, "tutorial.png"));
            yield return new WaitForSecondsRealtime(1);
            if (guidedShift.IsLessonOpen) guidedShift.DismissLesson();
            typeof(CasinoShiftController).GetField("tutorial", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(guidedShift, false);
        }
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "casino.png"));
        yield return new WaitForSecondsRealtime(1);
        if (securityCheck)
        {
            yield return CaptureSecurity();
            yield break;
        }
        if (seatingCheck)
        {
            yield return CaptureSeating();
            yield break;
        }
        if (coverCheck)
        {
            yield return CaptureCover();
            yield break;
        }
        if (demoCheck)
        {
            DealerAssignmentManager manager = FindFirstObjectByType<DealerAssignmentManager>();
            typeof(DealerAssignmentManager).GetField("simulationTimeScale", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(manager, 10f);
            CasinoShiftController shift = FindFirstObjectByType<CasinoShiftController>();
            float deadline = Time.realtimeSinceStartup + 90;
            while (shift.IsRunning && Time.realtimeSinceStartup < deadline) yield return null;
            if (shift.IsRunning || shift.PassedShift || CasinoDemoProgression.Credits != 0 ||
                CasinoDemoProgression.UnlockedShift != 1)
            {
                Debug.LogError("Standalone unattended shift did not finish with a clean failed result and no reward.");
                Application.Quit(1);
                yield break;
            }

            ScreenCapture.CaptureScreenshot(Path.Combine(directory, "failed-results.png"));
            yield return new WaitForSecondsRealtime(1);
            GameObject resultPanel = (GameObject)typeof(CasinoShiftController).GetField("results",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shift);
            resultPanel.transform.Find("Full-screen shift results/Results information panel/Retry Shift")
                .GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return new WaitForSecondsRealtime(2);
            shift = FindFirstObjectByType<CasinoShiftController>();
            if (shift != null && shift.IsLessonOpen)
                shift.DismissLesson();
            if (shift == null || !shift.IsRunning || Time.timeScale <= 0 || AudioListener.pause)
            {
                Debug.LogError($"Standalone retry did not resume a fresh shift: controller={shift != null}, " +
                    $"running={shift != null && shift.IsRunning}, timeScale={Time.timeScale:0.##}, paused={AudioListener.pause}.");
                Application.Quit(1);
                yield break;
            }
            Debug.Log("STANDALONE DEMO CHECK PASSED: full unattended shift, failure, no rewards, retry.");
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, "retry.png"));
            yield return new WaitForSecondsRealtime(1);
            Application.Quit();
            yield break;
        }
        if (watch)
        {
            for (int i = 0; i < 12; i++)
            {
                yield return new WaitForSecondsRealtime(5);
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, $"watch-{i:00}.png"));
            }
            if (oldTutorial < 0) PlayerPrefs.DeleteKey("PitPanic.TutorialComplete");
            else PlayerPrefs.SetInt("PitPanic.TutorialComplete", oldTutorial);
            PlayerPrefs.Save();
            yield return new WaitForSecondsRealtime(1);
            Application.Quit();
            yield break;
        }
        CasinoPlayerController player = FindFirstObjectByType<CasinoPlayerController>();
        RouletteInspectionView roulette = FindFirstObjectByType<RouletteInspectionView>();
        MoveToTable(player, roulette.transform);
        if (!roulette.TryOpenInspectionFromPlayerInput())
        {
            Debug.LogError("Presentation capture could not open roulette.");
            Application.Quit(1);
            yield break;
        }
        yield return new WaitForSecondsRealtime(1);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "roulette.png"));
        yield return new WaitForSecondsRealtime(1);
        typeof(RouletteInspectionView).GetMethod("Close", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(roulette, null);
        BlackjackTableInteraction blackjack = FindFirstObjectByType<BlackjackTableInteraction>();
        MoveToTable(player, blackjack.transform);
        if (!blackjack.TryOpenInspectionFromPlayerInput())
        {
            Debug.LogError("Presentation capture could not open blackjack.");
            Application.Quit(1);
            yield break;
        }
        yield return new WaitForSecondsRealtime(1);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "blackjack.png"));
        yield return new WaitForSecondsRealtime(1);
        typeof(BlackjackTableInteraction).GetMethod("ClosePanel", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(blackjack, null);
        CharacterController controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.position = new Vector3(0, .08f, -6);
        controller.enabled = true;
        CasinoPlayerFloatCarrier carrier = player.GetComponent<CasinoPlayerFloatCarrier>();
        CasinoFloatRefillDestination destination = blackjack.GetComponent<CasinoFloatRefillDestination>();
        CasinoFloatRefillOrder visualOrder = CasinoFloatRefillOrder.CreateRequired("capture-tray", destination.DestinationId, destination);
        if (!carrier.BeginCarrying(visualOrder))
        {
            Debug.LogError("Presentation capture could not show the carried tray.");
            Application.Quit(1);
            yield break;
        }
        Vector3[] directions = { Vector3.back, Vector3.right, Vector3.forward, Vector3.left };
        string[] facing = { "front", "right", "away", "left" };
        for (int i = 0; i < directions.Length; i++)
        {
            player.transform.rotation = Quaternion.LookRotation(directions[i]);
            yield return new WaitForSecondsRealtime(.5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, $"carrying-{facing[i]}.png"));
            yield return new WaitForSecondsRealtime(.5f);
        }
        Application.Quit();
    }

    private IEnumerator CaptureHighRollerVips()
    {
        yield return null;
        CasinoShiftController shift = FindFirstObjectByType<CasinoShiftController>();
        if (shift == null)
        {
            Debug.LogError("High Rollers preview has no shift controller.");
            Application.Quit(1);
            yield break;
        }
        typeof(CasinoShiftController).GetMethod("BeginShift", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(shift, new object[] { 2, false });
        if (shift.IsLessonOpen) shift.DismissLesson();
        Time.timeScale = 1f;
        AudioListener.pause = false;
        yield return null;

        CasinoCustomerAI[] featuredVips = new CasinoCustomerAI[4];
        CasinoAnimalSpecies[] speciesOrder =
        {
            CasinoAnimalSpecies.Rabbit, CasinoAnimalSpecies.Deer,
            CasinoAnimalSpecies.Cat, CasinoAnimalSpecies.Raccoon
        };
        CasinoCustomerGame[] tableOrder =
        {
            CasinoCustomerGame.Blackjack1, CasinoCustomerGame.Blackjack1,
            CasinoCustomerGame.Blackjack2, CasinoCustomerGame.Blackjack2
        };
        float deadline = Time.realtimeSinceStartup + 40f;
        while (Array.Exists(featuredVips, vip => vip == null) && Time.realtimeSinceStartup < deadline)
        {
            foreach (CasinoCustomerAI customer in FindObjectsByType<CasinoCustomerAI>(FindObjectsSortMode.None))
            {
                if (!customer.IsVip || customer.Seat == null || !customer.IsSeated) continue;
                CasinoCharacterVisual character = customer.GetComponentInChildren<CasinoCharacterVisual>();
                if (character == null) continue;
                for (int i = 0; i < featuredVips.Length; i++)
                    if (character.Species == speciesOrder[i] && customer.Seat.GameType == tableOrder[i])
                        featuredVips[i] = customer;
            }
            yield return null;
        }
        if (Array.Exists(featuredVips, vip => vip == null))
        {
            Debug.LogError("The Rabbit, Deer, Cat and Raccoon VIPs did not reach their side tables.");
            Application.Quit(1);
            yield break;
        }
        foreach (CasinoCustomerAI vip in featuredVips)
        {
            SpriteRenderer aura = Array.Find(vip.GetComponentsInChildren<SpriteRenderer>(true),
                renderer => renderer.name == "VIP Golden Aura");
            PixelCharacterVisual character = vip.GetComponentInChildren<PixelCharacterVisual>();
            if (aura == null || !aura.enabled || character == null || !character.IsVip || !character.UsesVipAnimation)
            {
                Debug.LogError($"{vip.name} is missing VIP formalwear or the golden glow.");
                Application.Quit(1);
                yield break;
            }
        }
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "high-rollers-vip-guests.png"));
        Debug.Log("HIGH ROLLERS VIP CHECK PASSED: Rabbit, Deer, Cat and Raccoon seated in their pictured outfits with golden auras.");
        Application.Quit();
    }

    private IEnumerator CaptureSeating()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        for (int level = 1; level <= CasinoDemoProgression.LevelCount; level++)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
            yield return null;
            yield return null;
            CasinoShiftController shift = FindFirstObjectByType<CasinoShiftController>();
            typeof(CasinoShiftController).GetMethod("BeginShift", flags).Invoke(shift, new object[] { level, false });
            if (shift.IsLessonOpen) shift.DismissLesson();
            string[] floorNames = { "OpeningNightFloor", "HighRollersFloor", "ShadyBusinessFloor" };
            SpriteRenderer floor = GameObject.Find("Pixel Carpet")?.GetComponent<SpriteRenderer>();
            if (floor == null || floor.sprite == null || floor.sprite.name != floorNames[level - 1])
            {
                Debug.LogError($"Level {level} did not select its original campaign floor art.");
                Application.Quit(1);
                yield break;
            }
            Color[] sceneryTints =
            {
                Color.white,
                new Color(1f, .91f, .74f),
                new Color(.78f, .84f, 1f)
            };
            Color[] rugTints =
            {
                Color.white,
                new Color(.80f, .96f, .83f),
                new Color(.84f, .81f, 1f)
            };
            SpriteRenderer[] campaignArt = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
            SpriteRenderer themedWindow = Array.Find(campaignArt,
                renderer => renderer.gameObject.name == "Pixel Window");
            SpriteRenderer themedRug = Array.Find(campaignArt,
                renderer => renderer.gameObject.name == "Table Rug");
            if (themedWindow == null || themedWindow.color != sceneryTints[level - 1] ||
                themedRug == null || themedRug.color != rugTints[level - 1])
                throw new InvalidOperationException($"Level {level} did not apply its original scenery palette.");
            CasinoCustomerSpawner spawner = FindFirstObjectByType<CasinoCustomerSpawner>();
            spawner.SetAcceptingArrivals(false);
            spawner.SetGuidedPractice(true);
            typeof(CasinoCustomerSpawner).GetField("maximumCustomers", flags).SetValue(spawner, 10);
            // Hold settlement, not navigation: all ten real customers must reach their chairs.
            foreach (BlackjackTableInteraction table in FindObjectsByType<BlackjackTableInteraction>(FindObjectsSortMode.None))
                table.enabled = false;
            FindFirstObjectByType<RouletteRoundController>().enabled = false;
            CasinoSeat[] seats = FindObjectsByType<CasinoSeat>(FindObjectsSortMode.None);
            float deadline = Time.realtimeSinceStartup + 45;
            while (Time.realtimeSinceStartup < deadline)
            {
                for (int count = 1; count <= 3; count++)
                {
                    spawner.TrySpawnGuidedCustomer(CasinoCustomerGame.Blackjack1, count);
                    spawner.TrySpawnGuidedCustomer(CasinoCustomerGame.Blackjack2, count);
                    spawner.TrySpawnGuidedCustomer(CasinoCustomerGame.Roulette, count);
                }
                spawner.TrySpawnGuidedCustomer(CasinoCustomerGame.Roulette, 4);
                if (Array.TrueForAll(seats, seat => seat.Occupant != null && seat.Occupant.IsSeated)) break;
                yield return null;
            }
            yield return new WaitForSeconds(1.5f);
            foreach (AIDealer dealer in FindObjectsByType<AIDealer>(FindObjectsSortMode.None))
                if (dealer.IsAtStation && dealer.AssignedStation != null &&
                    Vector3.ProjectOnPlane(dealer.transform.position - dealer.AssignedStation.DealerPosition.position,
                        Vector3.up).magnitude > .15f)
                    throw new InvalidOperationException($"Level {level}: {dealer.name} is not beside its moved table: " +
                        $"{dealer.transform.position} versus {dealer.AssignedStation.DealerPosition.position}.");
            foreach (CasinoSeat seat in seats)
            {
                PixelCharacterVisual visual = seat.Occupant != null
                    ? seat.Occupant.GetComponentInChildren<PixelCharacterVisual>() : null;
                if (visual == null || !seat.Occupant.IsSeated ||
                    Vector3.Distance(visual.transform.position, seat.SittingPosition) > .04f ||
                    seat.GameType == CasinoCustomerGame.Roulette && !PixelCharacterVisual.IsSeatedRoulettePose(visual.CurrentPose))
                {
                    foreach (CasinoSeat diagnostic in seats)
                    {
                        CasinoCustomerAI visitor = diagnostic.ReservedBy;
                        UnityEngine.AI.NavMeshAgent agent = visitor != null
                            ? visitor.GetComponent<UnityEngine.AI.NavMeshAgent>() : null;
                        Debug.Log($"SEAT {diagnostic.name}: reserved={visitor?.name}, seated={diagnostic.Occupant?.IsSeated}, " +
                            $"available={diagnostic.IsGameAvailable}, position={visitor?.transform.position}, " +
                            $"moving={agent != null && agent.enabled}, active={spawner.ActiveCustomerCount}");
                    }
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, $"seating-failure-{level}.png"));
                    yield return new WaitForSecondsRealtime(.5f);
                    Debug.LogError($"Seating capture failed at level {level}/{seat.name}: visual={visual != null}, " +
                        $"offset={(visual != null ? Vector3.Distance(visual.transform.position, seat.SittingPosition) : -1)}.");
                    Application.Quit(1);
                    yield break;
                }
            }
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, $"seating-level-{level}.png"));
            yield return new WaitForSecondsRealtime(.5f);
            Camera camera = Camera.main;
            float size = camera.orthographicSize;
            Vector3 position = camera.transform.position;
            BlackjackTableInteraction first = Array.Find(
                FindObjectsByType<BlackjackTableInteraction>(FindObjectsSortMode.None),
                table => table.TableDisplayName == "BLACKJACK 1");
            camera.transform.position += first.transform.position + new Vector3(0, 0, -1.5f);
            camera.orthographicSize = 4;
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, $"seating-detail-{level}.png"));
            yield return new WaitForSecondsRealtime(.5f);
            camera.transform.position = position;
            camera.orthographicSize = size;
            if (level == 2)
            {
                first.enabled = true;
                RouletteRoundController rounds = FindFirstObjectByType<RouletteRoundController>();
                rounds.enabled = true;
                yield return new WaitForSeconds(4);
                CasinoPlayerController player = FindFirstObjectByType<CasinoPlayerController>();
                MoveToTable(player, first.transform);
                if (!first.TryOpenInspectionFromPlayerInput())
                    throw new InvalidOperationException("Could not capture populated Blackjack presentation.");
                yield return new WaitForSecondsRealtime(1);
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "blackjack-populated.png"));
                yield return new WaitForSecondsRealtime(.5f);
                typeof(BlackjackTableInteraction).GetMethod("ClosePanel", flags).Invoke(first, null);
                RouletteInspectionView inspection = rounds.GetComponent<RouletteInspectionView>();
                MoveToTable(player, rounds.transform);
                if (!inspection.TryOpenInspectionFromPlayerInput())
                    throw new InvalidOperationException("Could not capture populated Roulette presentation.");
                yield return new WaitForSecondsRealtime(1);
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "roulette-populated.png"));
                yield return new WaitForSecondsRealtime(.5f);
                typeof(RouletteInspectionView).GetMethod("ToggleDetails", flags).Invoke(inspection, null);
                yield return null;
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "roulette-details.png"));
                yield return new WaitForSecondsRealtime(.5f);
                typeof(RouletteInspectionView).GetMethod("Close", flags).Invoke(inspection, null);
                first.AssignedDealer.RequestTutorialTakeover();
                MoveToTable(player, first.transform);
                yield return new WaitForSecondsRealtime(.5f);
                foreach (CasinoSpeechBubble speech in FindObjectsByType<CasinoSpeechBubble>(FindObjectsSortMode.None))
                {
                    if (speech.Message.Length == 0) continue;
                    RectTransform bubble = speech.transform.Find("Speech Bubble").GetComponent<RectTransform>();
                    Vector3[] corners = new Vector3[4];
                    bubble.GetWorldCorners(corners);
                    Vector3 head = camera.WorldToScreenPoint(
                        speech.GetComponentInChildren<PixelCharacterVisual>().HeadPosition);
                    foreach (Vector3 corner in corners)
                        if (camera.WorldToScreenPoint(corner).y <= head.y)
                            throw new InvalidOperationException("Speech bubble overlaps the character's head.");
                }
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "task-speech-bubbles.png"));
                yield return new WaitForSecondsRealtime(.5f);
                CasinoFloatRefillDestination stock = first.GetComponent<CasinoFloatRefillDestination>();
                if (!stock.TryConsumeChips(new[] { stock.GetChipCount(0), stock.GetChipCount(1), 0, 0 }))
                    throw new InvalidOperationException("Could not prepare large refill capture.");
                CasinoCashDeskInteraction desk = FindFirstObjectByType<CasinoCashDeskInteraction>();
                desk.RefreshFloatRequests();
                CashDeskPackMinigame pack = desk.GetComponent<CashDeskPackMinigame>();
                IReadOnlyList<CasinoFloatRefillOrder> orders = desk.GetRequestedOrders();
                int orderIndex = -1;
                for (int i = 0; i < orders.Count; i++)
                    if (orders[i].Destination == stock) orderIndex = i;
                if (orderIndex < 0) throw new InvalidOperationException("Missing real large-refill order.");
                pack.SetAvailableFloatOrders(orders);
                pack.Begin();
                typeof(CashDeskPackMinigame).GetMethod("SelectFloatOrder", flags).Invoke(pack, new object[] { orderIndex });
                for (int chip = 0; chip < pack.PackedOrder.CountFor(1); chip++) pack.SelectRackChip(1);
                yield return new WaitForSecondsRealtime(.5f);
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "large-refill.png"));
                yield return new WaitForSecondsRealtime(.5f);
                pack.Abandon();
            }
            Debug.Log($"SEATING CHECK level {level}: all ten customers reached and aligned with their chairs.");
        }
        Debug.Log("STANDALONE SEATING CHECK PASSED");
        Application.Quit();
    }

    private IEnumerator CaptureSecurity()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        PitDeskInteraction desk = FindFirstObjectByType<PitDeskInteraction>();
        CasinoCustomerAI customer = FindFirstObjectByType<CasinoCustomerAI>();
        var bet = new RouletteCustomerBet(6, 0, customer, new RouletteBet(RouletteBetType.Black, -1, 500));
        if (!CasinoSecurityEvidence.TryCreate(6, 1, new[] { bet }, out CasinoSecurityEvidence evidence))
            throw new InvalidOperationException("Cannot prepare CCTV capture.");
        var queue = (Queue<CasinoSecurityEvidence>)typeof(PitDeskInteraction).GetField("securityQueue", flags).GetValue(desk);
        queue.Enqueue(evidence);
        typeof(PitDeskInteraction).GetMethod("Open", flags).Invoke(desk, null);
        typeof(PitDeskInteraction).GetMethod("OpenReview", flags).Invoke(desk, null);
        yield return new WaitForSecondsRealtime(1.5f);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "cctv-bet.png"));
        yield return new WaitForSecondsRealtime(2.5f);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "cctv-result.png"));
        yield return new WaitForSecondsRealtime(1.7f);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "cctv-slide.png"));
        yield return new WaitForSecondsRealtime(1.1f);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "cctv-moved.png"));
        yield return new WaitForSecondsRealtime(.5f);
        typeof(PitDeskInteraction).GetMethod("ResolveReview", flags).Invoke(desk, new object[] { false });
        yield return new WaitForSecondsRealtime(.5f);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "cctv-wrong.png"));
        yield return new WaitForSecondsRealtime(.5f);
        typeof(PitDeskInteraction).GetMethod("CloseReview", flags).Invoke(desk, null);
        queue.Enqueue(evidence);
        typeof(PitDeskInteraction).GetMethod("OpenReview", flags).Invoke(desk, null);
        typeof(PitDeskInteraction).GetMethod("ResolveReview", flags).Invoke(desk, new object[] { true });
        yield return new WaitForSecondsRealtime(.5f);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "cctv-correct.png"));
        yield return new WaitForSecondsRealtime(.5f);
        Debug.Log("STANDALONE CCTV CAPTURE PASSED");
        Application.Quit();
    }

    private IEnumerator CaptureCover()
    {
        RouletteRoundController rounds = FindFirstObjectByType<RouletteRoundController>();
        FindFirstObjectByType<RouletteAssistanceScheduler>().RequestTutorialAssistance();
        FindFirstObjectByType<CasinoCustomerSpawner>().TrySpawnGuidedCustomer(CasinoCustomerGame.Roulette, 4);
        float deadline = Time.realtimeSinceStartup + 75;
        while (!rounds.IsCoverReady && Time.realtimeSinceStartup < deadline) yield return null;
        if (!rounds.IsCoverReady)
        {
            Debug.LogError("Standalone COVER check did not reach a real completed dealer spin.");
            Application.Quit(1);
            yield break;
        }
        CasinoPlayerController player = FindFirstObjectByType<CasinoPlayerController>();
        RouletteInspectionView inspection = rounds.GetComponent<RouletteInspectionView>();
        MoveToTable(player, rounds.transform);
        if (!inspection.TryOpenInspectionFromPlayerInput())
        {
            Debug.LogError("Standalone COVER check could not inspect Roulette.");
            Application.Quit(1);
            yield break;
        }
        yield return null;
        GameObject board = (GameObject)typeof(RouletteInspectionView).GetField("board",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(inspection);
        board.transform.Find("Start assistance").GetComponent<Button>().onClick.Invoke();
        RouletteTableMinigame game = rounds.GetComponent<RouletteTableMinigame>();
        if (!game.IsCoverMode || game.WinningNumber != rounds.AssistanceWinningNumber)
        {
            Debug.LogError("Standalone COVER check opened the wrong mode or result.");
            Application.Quit(1);
            yield break;
        }
        yield return new WaitForSecondsRealtime(1);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "cover.png"));
        yield return new WaitForSecondsRealtime(.5f);
        Transform area = FindFirstObjectByType<CasinoMinigamePresentation>().GameArea;
        area.Find("COVER Bet Guide").GetComponent<Button>().onClick.Invoke();
        yield return new WaitForSecondsRealtime(.5f);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "cover-guide.png"));
        yield return new WaitForSecondsRealtime(.5f);
        area.Find("COVER Bet Guide Panel/Close bet guide").GetComponent<Button>().onClick.Invoke();
        List<RouletteCustomerBet> actualBets = new List<RouletteCustomerBet>(rounds.AssistanceBets);
        foreach (RouletteCustomerBet bet in actualBets)
            if (bet.IsWinning) game.SelectCoverBet(bet.BetId);
        if (game.CoverSelection.CanDeclareNoWinners) game.DeclareNoCoverWinners();
        deadline = Time.realtimeSinceStartup + 5;
        while (game.IsRunning && Time.realtimeSinceStartup < deadline) yield return null;
        if (game.IsRunning || actualBets.Exists(bet => !bet.IsSettled))
        {
            Debug.LogError("Standalone COVER check did not complete and settle its real round.");
            Application.Quit(1);
            yield break;
        }
        Debug.Log("STANDALONE COVER CHECK PASSED: actual round/result, interactive layout, guide and winner selection.");
        Application.Quit();
    }

    private IEnumerator CaptureTitle()
    {
        yield return new WaitForSecondsRealtime(.25f);
        CasinoTitleScreen title = FindFirstObjectByType<CasinoTitleScreen>();
        if (title == null) throw new InvalidOperationException("Title screen was not created.");
        RectTransform penguin = title.transform.Find("Casino exterior/Title Penguin").GetComponent<RectTransform>();
        Vector2 centredPosition = new Vector2(-2, -329.5f);
        if (Vector2.Distance(penguin.anchoredPosition, centredPosition) > .01f)
            throw new InvalidOperationException("Penguin did not start centred at the entrance.");
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "title-arrival.png"));
        yield return new WaitForSecondsRealtime(3);
        if (Vector2.Distance(penguin.anchoredPosition, centredPosition) > .01f)
            throw new InvalidOperationException("Penguin still walks sideways on the title screen.");
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "title-walking.png"));
        yield return new WaitForSecondsRealtime(3.3f);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "title-looking-up.png"));
        yield return new WaitForSecondsRealtime(2);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "title-idle.png"));
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(CasinoTitleScreen).GetMethod("StopRemark", flags).Invoke(title, null);
        typeof(CasinoTitleScreen).GetField("elapsed", flags).SetValue(title, 30f);
        typeof(CasinoTitleScreen).GetField("idleSeconds", flags).SetValue(title, 20f);
        typeof(CasinoTitleScreen).GetField("nextRemark", flags).SetValue(title, 0f);
        typeof(CasinoTitleScreen).GetMethod("UpdateRemarks", flags).Invoke(title, new object[] { false });
        yield return new WaitForSecondsRealtime(2);
        CanvasGroup remarkGroup = (CanvasGroup)typeof(CasinoTitleScreen).GetField("remarkGroup", flags).GetValue(title);
        if (remarkGroup.alpha <= 0 || remarkGroup.blocksRaycasts)
            throw new InvalidOperationException("The idle dialogue is missing or blocks menu controls.");
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "title-remark.png"));
        yield return new WaitForEndOfFrame();
        yield return null;
        title.transform.Find("Settings").GetComponent<Button>().onClick.Invoke();
        yield return new WaitForSecondsRealtime(.2f);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "title-settings.png"));
        yield return new WaitForEndOfFrame();
        yield return null;
        title.transform.Find("Settings panel/Back").GetComponent<Button>().onClick.Invoke();
        CasinoSoundscape soundscape = FindFirstObjectByType<CasinoSoundscape>();
        float[] cueTimes = (float[])typeof(CasinoSoundscape).GetField("lastPlayed", flags).GetValue(soundscape);
        float previousDoorCue = cueTimes[(int)CasinoSoundCue.Door];
        title.transform.Find("Start").GetComponent<Button>().onClick.Invoke();
        AudioSource menuMusic = (AudioSource)typeof(CasinoSoundscape).GetField("music", flags)
            .GetValue(FindFirstObjectByType<CasinoSoundscape>());
        if (menuMusic.isPlaying)
            throw new InvalidOperationException("NEW GAME did not immediately stop title music.");
        yield return new WaitForSecondsRealtime(.72f);
        if (AudioListener.pause || cueTimes[(int)CasinoSoundCue.Door] <= previousDoorCue || menuMusic.isPlaying)
            throw new InvalidOperationException("The entrance did not play its door cue with title music stopped.");
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "title-opening.png"));
        yield return new WaitForSecondsRealtime(.6f);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "title-entering.png"));
        yield return new WaitForSecondsRealtime(1.3f);
        CasinoShiftController shift = FindFirstObjectByType<CasinoShiftController>();
        GameObject levels = (GameObject)typeof(CasinoShiftController).GetField("levelSelect", flags).GetValue(shift);
        if (!levels.activeSelf || title.gameObject.activeSelf || shift.IsRunning)
            throw new InvalidOperationException("NEW GAME did not finish at level selection.");
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "title-level-select.png"));
        yield return new WaitForEndOfFrame();
        yield return null;
        levels.transform.Find("Level Select Back").GetComponent<Button>().onClick.Invoke();
        yield return new WaitForSecondsRealtime(.3f);
        if (!title.gameObject.activeSelf || title.IsTransitioning ||
            title.GetComponentsInChildren<Image>().Length == 0 || CasinoSoundscape.CurrentMusicTrack != CasinoMusicTrack.Title)
            throw new InvalidOperationException("Returning to the title did not reset presentation.");
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "title-return.png"));
        yield return new WaitForSecondsRealtime(.3f);
        Debug.Log("STANDALONE TITLE CHECK PASSED");
        title.transform.Find("Quit").GetComponent<Button>().onClick.Invoke();
    }

    private void OnTitleLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception) Application.Quit(1);
    }

    private static void MoveToTable(CasinoPlayerController player, Transform table)
    {
        CharacterController controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.position = new Vector3(table.position.x, player.transform.position.y, table.position.z - 2.5f);
        controller.enabled = true;
    }

    private void OnDestroy()
    {
        if (titleCheck) Application.logMessageReceived -= OnTitleLog;
        foreach (var entry in savedProgress)
            if (entry.Value < 0) PlayerPrefs.DeleteKey(entry.Key);
            else PlayerPrefs.SetInt(entry.Key, entry.Value);
        PlayerPrefs.Save();
    }
}
#endif
