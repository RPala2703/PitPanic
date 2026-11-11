using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class FirstShiftPlayModeValidation
{
    private const string Key = "PitPanic.FullShiftTest";
    static FirstShiftPlayModeValidation() { EditorApplication.update += StartTest; }
    public static void Run()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Run the shift validation in a batch-mode editor.");
        SessionState.SetInt("PitPanic.OldTutorial", PlayerPrefs.GetInt("PitPanic.TutorialComplete", -1));
        SessionState.SetInt("PitPanic.OldUnlocked", PlayerPrefs.GetInt("PitPanic.UnlockedShift", -1));
        foreach (string key in ProgressKeys())
        {
            SessionState.SetInt("Old." + key, PlayerPrefs.GetInt(key, -1));
            PlayerPrefs.DeleteKey(key);
        }
        foreach (string channel in new[] { "Music", "Ambience", "Effects" })
            SessionState.SetFloat("PitPanic.Old" + channel, PlayerPrefs.GetFloat("PitPanic." + channel + "Volume", -1));
        PlayerPrefs.DeleteKey("PitPanic.TutorialComplete");
        PlayerPrefs.DeleteKey("PitPanic.UnlockedShift");
        EditorSceneManager.OpenScene("Assets/Scenes/PitPanicPrototype.unity");
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    private static string[] ProgressKeys() => new[] { CasinoDemoProgression.CreditsKey,
        CasinoDemoProgression.UpgradeKeys[0], CasinoDemoProgression.UpgradeKeys[1], CasinoDemoProgression.UpgradeKeys[2] };

    private static void StartTest()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
        SessionState.SetBool(Key, false);
        new GameObject("Shift test driver").AddComponent<ShiftTestDriver>();
    }

    public sealed class ShiftTestDriver : MonoBehaviour
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private Keyboard keyboard;
        private Mouse mouse;
        private CasinoPlayerController player;
        private CasinoShiftController shift;
        private static object Get(object o, string name) => o.GetType().GetField(name, Flags).GetValue(o);
        private static object Call(object o, string name, params object[] args) =>
            o.GetType().GetMethod(name, Flags).Invoke(o, args);
        private static void Set(object o, string name, object value) => o.GetType().GetField(name, Flags).SetValue(o, value);
        private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
        private void Start() { DontDestroyOnLoad(gameObject); StartCoroutine(RunTest()); }

        private IEnumerator RunTest()
        {
            IEnumerator test = Test();
            float deadline = Time.realtimeSinceStartup + 420f;
            while (true)
            {
                bool next;
                try
                {
                    Check(Time.realtimeSinceStartup < deadline, "Shift test timed out.");
                    next = test.MoveNext();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    Restore();
                    EditorApplication.Exit(1);
                    yield break;
                }
                if (!next) break;
                yield return test.Current;
            }
            Debug.Log("FULL SHIFT INTERACTION TEST PASSED");
            Restore();
            EditorApplication.Exit(0);
        }

        private static void Restore()
        {
            RestoreKey("PitPanic.TutorialComplete", SessionState.GetInt("PitPanic.OldTutorial", -1));
            RestoreKey("PitPanic.UnlockedShift", SessionState.GetInt("PitPanic.OldUnlocked", -1));
            foreach (string key in ProgressKeys()) RestoreKey(key, SessionState.GetInt("Old." + key, -1));
            foreach (string channel in new[] { "Music", "Ambience", "Effects" })
            {
                float value = SessionState.GetFloat("PitPanic.Old" + channel, -1);
                if (value < 0) PlayerPrefs.DeleteKey("PitPanic." + channel + "Volume");
                else PlayerPrefs.SetFloat("PitPanic." + channel + "Volume", value);
            }
            PlayerPrefs.Save();
        }

        private static void RestoreKey(string key, int value)
        {
            if (value < 0) PlayerPrefs.DeleteKey(key); else PlayerPrefs.SetInt(key, value);
        }

        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        private void MoveNear(Transform center, Func<Transform, bool> inRange)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            Vector3 initial = player.transform.position;
            for (float r = 0.5f; r <= 7f; r += 0.25f)
                for (int angle = 0; angle < 32; angle++)
                {
                    float a = angle * Mathf.PI / 16f;
                    player.transform.position = new Vector3(center.position.x + r * Mathf.Cos(a), initial.y,
                        center.position.z + r * Mathf.Sin(a));
                    if (inRange(player.transform)) { controller.enabled = true; return; }
                }
            controller.enabled = true;
            throw new Exception("No reachable interaction point near " + center.name);
        }

        private IEnumerator Test()
        {
            Check(CasinoDemoProgression.Passed(1, 8, 4, 60) &&
                !CasinoDemoProgression.Passed(1, 7, 4, 100) &&
                !CasinoDemoProgression.Passed(1, 8, 3, 100) &&
                !CasinoDemoProgression.Passed(1, 8, 4, 59) &&
                CasinoDemoProgression.ServiceGoal(3) > CasinoDemoProgression.ServiceGoal(2),
                "Demo goals have incorrect pass/fail boundaries or do not escalate");
            yield return null;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            shift = FindFirstObjectByType<CasinoShiftController>();
            player = FindFirstObjectByType<CasinoPlayerController>();
            var spawner = FindFirstObjectByType<CasinoCustomerSpawner>();
            var blackjack = Array.Find(FindObjectsByType<BlackjackTableInteraction>(FindObjectsSortMode.None),
                t => t.TableDisplayName == "BLACKJACK 1");
            var inspection = FindFirstObjectByType<RouletteInspectionView>();
            var roulette = FindFirstObjectByType<RouletteRoundController>();
            var flick = blackjack.GetComponent<FlickCardMinigame>();
            var rgame = FindFirstObjectByType<RouletteTableMinigame>();
            var pack = FindFirstObjectByType<CashDeskPackMinigame>();
            var desk = FindFirstObjectByType<CasinoCashDeskInteraction>();
            var manager = FindFirstObjectByType<DealerAssignmentManager>();
            Check(Time.timeScale == 0 && !spawner.AcceptingArrivals, "Menu failed to pause");
            GameObject menu = (GameObject)Get(shift, "menu");
            Check(Camera.main.cullingMask == 0, "Title did not isolate the casino camera");
            PixelCharacterVisual[] titleCast = (PixelCharacterVisual[])Get(menu.GetComponent<CasinoTitleScreen>(), "cast");
            Check(titleCast.Length > 0 && titleCast[0] == player.GetComponentInChildren<PixelCharacterVisual>(),
                "Title hero did not match the playable pit boss");
            Check(!menu.transform.Find("Continue").GetComponent<Button>().interactable, "Fresh save enabled Continue");
            menu.transform.Find("Settings").GetComponent<Button>().onClick.Invoke();
            GameObject settings = menu.transform.Find("Settings panel").gameObject;
            Check(settings.activeSelf && Time.timeScale == 0, "Settings did not preserve title pause");
            foreach (string channel in new[] { "Music", "Ambience", "Effects" })
            {
                Slider slider = settings.transform.Find(channel + " volume").GetComponent<Slider>();
                Check(slider != null && slider.minValue == 0 && slider.maxValue == 1,
                    "Missing separate audio control: " + channel);
                float original = slider.value;
                slider.value = .37f;
                Check(Mathf.Approximately(PlayerPrefs.GetFloat("PitPanic." + channel + "Volume"), .37f),
                    "Audio slider did not persist its channel");
                slider.value = original;
            }
            CasinoSoundscape sound = FindFirstObjectByType<CasinoSoundscape>();
            Check(sound != null, "Soundscape was not initialized");
            foreach (AudioClip cue in (AudioClip[])Get(sound, "cues"))
            {
                Check(cue != null && cue.samples > 0, "A sound cue is missing");
                float[] samples = new float[cue.samples];
                Check(cue.GetData(samples, 0), "Sound cue data was unreadable");
                float peak = 0;
                foreach (float sample in samples)
                {
                    Check(!float.IsNaN(sample) && !float.IsInfinity(sample), "Invalid sound sample");
                    peak = Mathf.Max(peak, Mathf.Abs(sample));
                }
                Check(peak > .01f && peak <= 1f, "Sound cue was silent or clipped");
            }
            settings.transform.Find("Back").GetComponent<Button>().onClick.Invoke();
            Check(!settings.activeSelf, "Settings Back failed");
            menu.transform.Find("Start").GetComponent<Button>().onClick.Invoke();
            GameObject intro = (GameObject)Get(shift, "intro");
            Check(intro.activeSelf && !shift.IsRunning, "Intro did not follow menu");
            intro.transform.Find("Back").GetComponent<Button>().onClick.Invoke();
            Check(menu.activeSelf && !intro.activeSelf && Camera.main.cullingMask == 0 &&
                CasinoSoundscape.Focus == menu.transform && !PlayerPrefs.HasKey("PitPanic.TutorialComplete"),
                "Backing out of New Game changed progress or leaked the gameplay camera/audio");
            menu.transform.Find("Start").GetComponent<Button>().onClick.Invoke();
            intro.transform.Find("Begin").GetComponent<Button>().onClick.Invoke();
            Check(shift.IsRunning, "Start button failed");
            Check(Camera.main.cullingMask != 0, "Gameplay camera did not resume");
            Check(player.GetComponentInChildren<CasinoCharacterVisual>().Species == CasinoAnimalSpecies.Penguin,
                "Playable pit boss is not a penguin");
            foreach (PixelCharacterVisual visual in FindObjectsByType<PixelCharacterVisual>(FindObjectsSortMode.None))
                Check(visual.HasFrameAnimation, "Actor has no multi-frame action atlas: " + visual.name);
            foreach (AIDealer dealer in manager.Dealers)
                Check(dealer.DealerId == dealer.GetComponentInChildren<CasinoCharacterVisual>().Species.ToString(),
                    "Dealer name does not match animal");
            yield return Press(UnityEngine.InputSystem.Key.P);
            float pausedAt = shift.RemainingSeconds;
            yield return new WaitForSecondsRealtime(0.5f);
            Check(Mathf.Approximately(pausedAt, shift.RemainingSeconds), "Pause did not stop timer");
            yield return Press(UnityEngine.InputSystem.Key.P);
            Vector3 movementStart = player.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.W));
            yield return new WaitForSecondsRealtime(2.0f);
            Debug.Log($"MOVEMENT TEST {movementStart} -> {player.transform.position}; key={keyboard.wKey.isPressed}; timeScale={Time.timeScale}");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Check((bool)Get(shift, "moved"), "W movement did not advance tutorial");
            foreach (AIDealer dealer in manager.Dealers)
                if (dealer.IsAtStation)
                    Check(Vector3.Distance(dealer.GetComponentInChildren<PixelCharacterVisual>().transform.localPosition,
                        Vector3.forward * .65f) < .02f, "Working dealer visual did not approach table edge");

            MoveNear(blackjack.transform, blackjack.IsPlayerInRange);
            yield return Press(UnityEngine.InputSystem.Key.Q);
            Check(blackjack.IsInspectionOpen, "Q failed to inspect BJ1");
            yield return null;
            yield return Press(UnityEngine.InputSystem.Key.Escape);
            float timeout = Time.realtimeSinceStartup + 60;
            while (!blackjack.IsFlickReady && Time.realtimeSinceStartup < timeout) yield return null;
            Check(blackjack.IsFlickReady, "FLICK did not become ready");
            yield return Press(UnityEngine.InputSystem.Key.E);
            Check(blackjack.IsInspectionOpen, "E did not open unified BJ1 interface");
            Check(!blackjack.GetComponent<CasinoMinigameStation>().TryBegin(), "Minigame overlapped table inspection");
            Check(!inspection.TryOpenInspectionFromPlayerInput(), "Roulette overlapped Blackjack inspection");
            ((Button)Get(Get(blackjack, "tableView"), "assistanceButton")).onClick.Invoke();
            Check(flick.IsRunning, "E failed to start FLICK");
            Check(CasinoSoundscape.Focus == flick.transform && !CasinoSoundscape.AllowsEffect(null) &&
                CasinoSoundscape.AllowsEffect(flick.transform) && !CasinoSoundscape.AllowsEffect(rgame.transform),
                "Minigame audio did not isolate the active task");
            yield return Press(UnityEngine.InputSystem.Key.P);
            float flickPausedAt = (float)Get(flick, "timeRemaining");
            Vector3 pausedPlayer = player.transform.position;
            Check(AudioListener.pause && Time.timeScale == 0, "Pause did not stop audio and simulation");
            yield return Press(UnityEngine.InputSystem.Key.E);
            yield return Press(UnityEngine.InputSystem.Key.W);
            yield return new WaitForSecondsRealtime(.4f);
            Check(flick.IsRunning && (float)Get(flick, "timeRemaining") == flickPausedAt &&
                player.transform.position == pausedPlayer, "Paused minigame or character continued updating");
            ((GameObject)Get(shift, "pausePanel")).transform.Find("Pause card/Resume").GetComponent<Button>().onClick.Invoke();
            Check(!AudioListener.pause && Time.timeScale > 0 && flick.IsRunning,
                "Resume did not restore the existing minigame");
            yield return Press(UnityEngine.InputSystem.Key.Escape);
            Check(!flick.IsRunning && blackjack.IsFlickReady, "FLICK cancellation failed");
            Check(CasinoSoundscape.Focus == null && CasinoSoundscape.AllowsEffect(null),
                "Closing minigame did not restore casino audio");
            yield return Press(UnityEngine.InputSystem.Key.E);
            ((Button)Get(Get(blackjack, "tableView"), "assistanceButton")).onClick.Invoke();
            Check(flick.IsRunning, "FLICK retry failed");
            Set(flick, "timeRemaining", 0f);
            yield return null;
            Check(!flick.IsRunning && blackjack.IsFlickReady, "Failed FLICK did not allow retry");
            yield return Press(UnityEngine.InputSystem.Key.E);
            ((Button)Get(Get(blackjack, "tableView"), "assistanceButton")).onClick.Invoke();
            Check(flick.IsRunning, "FLICK retry after failure failed");
            while (flick.IsRunning)
            {
                int targetId = blackjack.GetNextFlickTarget();
                var targets = (IList)Get(flick, "handTargets");
                RectTransform target = null;
                foreach (object t in targets)
                    if ((int)t.GetType().GetField("Id").GetValue(t) == targetId)
                        target = (RectTransform)t.GetType().GetField("Rect").GetValue(t);
                Check(target != null, "FLICK target missing");
                var shoe = (RectTransform)Get(flick, "shoeRect");
                Vector2 start = RectTransformUtility.WorldToScreenPoint(null, shoe.position);
                Vector2 end = RectTransformUtility.WorldToScreenPoint(null, target.position);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = start, buttons = 1 });
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = end, buttons = 0 });
                yield return null;
                yield return new WaitForSeconds(0.35f);
            }
            Check((bool)Get(shift, "flickDone"), "FLICK did not advance tutorial");
            MoveNear(inspection.transform, inspection.IsPlayerInRange);
            yield return Press(UnityEngine.InputSystem.Key.Q);
            Check(inspection.IsOpen, "Q failed to inspect roulette");
            yield return null;
            yield return Press(UnityEngine.InputSystem.Key.Escape);
            timeout = Time.realtimeSinceStartup + 75;
            while (!roulette.HasPendingAssistance && Time.realtimeSinceStartup < timeout) yield return null;
            Check(roulette.HasPendingAssistance, "Roulette task did not become ready");
            yield return Press(UnityEngine.InputSystem.Key.E);
            Check(inspection.IsOpen, "E did not open unified roulette interface");
            ((GameObject)Get(inspection, "board")).transform.Find("Start assistance")
                .GetComponent<Button>().onClick.Invoke();
            Check(rgame.IsRunning, "E failed to start roulette");
            yield return Press(UnityEngine.InputSystem.Key.Escape);
            Check(!rgame.IsRunning && roulette.HasPendingAssistance, "Roulette cancellation failed");
            yield return Press(UnityEngine.InputSystem.Key.E);
            ((GameObject)Get(inspection, "board")).transform.Find("Start assistance")
                .GetComponent<Button>().onClick.Invoke();
            Check(rgame.IsRunning, "Roulette retry failed");
            Call(rgame, "BeginSpin");
            yield return new WaitForSeconds(3f);
            IList chips = (IList)Get(rgame, "sweepChips");
            var tray = (RectTransform)Get(rgame, "trayHitArea");
            Vector2 trayPoint = RectTransformUtility.WorldToScreenPoint(null, tray.position);
            foreach (object chip in chips)
            {
                Type type = chip.GetType();
                if ((bool)type.GetField("Protected").GetValue(chip)) continue;
                var rect = (RectTransform)type.GetField("Rect").GetValue(chip);
                Call(rgame, "BeginPointerAction", RectTransformUtility.WorldToScreenPoint(null, rect.position));
                Call(rgame, "EndPointerAction", trayPoint);
            }
            while (rgame.IsRunning)
            {
                var bet = (RouletteCustomerBet)Get(rgame, "currentPayoutBet");
                Check(bet != null, "PAY stage did not open");
                int[] counts = RouletteTableMinigame.GetPayoutComposition((int)bet.GrossReturnPence);
                for (int i = 0; i < counts.Length; i++)
                    for (int n = 0; n < counts[i]; n++) Call(rgame, "AddPayoutChips", i);
                Call(rgame, "ConfirmPayout");
                yield return new WaitForSecondsRealtime(0.7f);
            }
            Check((bool)Get(shift, "rouletteDone"), "Roulette did not advance tutorial");
            timeout = Time.realtimeSinceStartup + 10;
            while (desk.GetRequestedOrders().Count == 0 && Time.realtimeSinceStartup < timeout) yield return null;
            Check(desk.GetRequestedOrders().Count > 0, "Float order was not requested");
            var station = desk.GetComponent<CasinoMinigameStation>();
            MoveNear(desk.transform, station.IsPlayerInRange);
            yield return Press(UnityEngine.InputSystem.Key.E);
            Check(pack.IsRunning, "PACK did not open");
            if (pack.IsCashOutMode)
            {
                int[] sequence = (int[])Get(pack, "packingSequence");
                foreach (int denomination in sequence) pack.SelectCashOutChip(denomination);
                Check(pack.IsReadyToDeliver, "Customer cash-out did not complete");
                yield return Press(UnityEngine.InputSystem.Key.E);
                yield return null;
                yield return Press(UnityEngine.InputSystem.Key.E);
                Check(pack.IsRunning && !pack.IsCashOutMode, "Float PACK did not follow customer cash-out");
            }
            Call(pack, "SelectFloatOrder", 0);
            var order = pack.PackedOrder;
            Check(order != null, "Float selection failed");
            yield return Press(UnityEngine.InputSystem.Key.Escape);
            Check(!pack.IsRunning && desk.GetOrderStatus(order.Id) == CasinoFloatRefillStatus.Requested,
                "PACK cancellation failed to restore order");
            yield return null;
            yield return Press(UnityEngine.InputSystem.Key.E);
            Check(pack.IsRunning, "PACK retry failed");
            Call(pack, "SelectFloatOrder", 0);
            order = pack.PackedOrder;
            Set(pack, "timeRemaining", 0f);
            yield return null;
            Check(!pack.IsRunning && desk.GetOrderStatus(order.Id) == CasinoFloatRefillStatus.Requested,
                "Timed-out PACK did not restore order");
            yield return null;
            yield return Press(UnityEngine.InputSystem.Key.E);
            Check(pack.IsRunning, "PACK retry after timeout failed");
            Call(pack, "SelectFloatOrder", 0);
            order = pack.PackedOrder;
            var trayRect = (RectTransform)Get(pack, "floatTrayRect");
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, trayRect.position);
            for (int i = 0; i < 4; i++)
                for (int n = 0; n < order.Counts[i]; n++) pack.TryDropRackChip(i, point);
            var carrier = player.GetComponent<CasinoPlayerFloatCarrier>();
            Check(carrier.IsCarrying, "PACK did not produce carried float");
            MoveNear(order.Destination.transform, order.Destination.InteractionZone.IsPlayerInRange);
            yield return Press(UnityEngine.InputSystem.Key.E);
            Check(!carrier.IsCarrying && (bool)Get(shift, "delivered"), "Float delivery failed");
            yield return null;
            Check(blackjack.AssignedDealer != null && blackjack.AssignedDealer.NeedsBreak,
                "Guided fatigue event did not request cover");
            var pit = FindFirstObjectByType<PitDeskInteraction>();
            MoveNear(pit.transform, pit.IsPlayerInRange);
            yield return Press(UnityEngine.InputSystem.Key.E);
            Check(pit.IsOpen, "Pit desk did not open");
            int readyIndex = -1;
            for (int i = 0; i < manager.Dealers.Count; i++)
                if (manager.Dealers[i].IsReadyAtPitDesk) { readyIndex = i; break; }
            Check(readyIndex >= 0, "No ready dealer");
            Call(pit, "SelectDealer", readyIndex);
            Call(pit, "AssignSelectedDealer", 0);
            timeout = Time.realtimeSinceStartup + 60;
            while (!(bool)Get(shift, "assigned") && Time.realtimeSinceStartup < timeout) yield return null;
            Check((bool)Get(shift, "assigned"), "Assignment did not advance tutorial");
            readyIndex = -1;
            timeout = Time.realtimeSinceStartup + 60;
            while (readyIndex < 0 && Time.realtimeSinceStartup < timeout)
            {
                for (int i = 0; i < manager.Dealers.Count; i++)
                    if (manager.Dealers[i].IsReadyAtPitDesk) { readyIndex = i; break; }
                if (readyIndex < 0) yield return null;
            }
            Check(readyIndex >= 0, "No dealer returned to desk");
            Call(pit, "SendDealerOnBreak", readyIndex);
            yield return null;
            Check(PlayerPrefs.GetInt("PitPanic.TutorialComplete", 0) == 1, "Tutorial did not complete");
            Check(shift.RemainingSeconds >= 179, "Training left no time for normal gameplay");
            Debug.Log($"DEMO GOAL TEST: hands={shift.Accounting.SettledHands}, tasks={Get(shift, "completedTasks")}");
            timeout = Time.realtimeSinceStartup + 60;
            while (shift.Accounting.SettledHands < CasinoDemoProgression.ServiceGoal(1) && Time.realtimeSinceStartup < timeout)
            {
                spawner.TrySpawnGuidedCustomer(CasinoCustomerGame.Blackjack2, 2);
                yield return null;
            }
            Check(shift.Accounting.SettledHands >= 8, "Demo service goal could not be reached with actual settled hands");
            Set(shift, "remaining", 0f);
            yield return null;
            Check(shift.IsRunning && !spawner.AcceptingArrivals,
                "Shift did not wait for the open pit desk to close");
            yield return Press(UnityEngine.InputSystem.Key.Escape);
            yield return null;
            yield return null;
            Check(!shift.IsRunning && PlayerPrefs.GetInt("PitPanic.UnlockedShift", 1) >= 2,
                "Shift results or progression failed");
            GameObject results = (GameObject)Get(shift, "results");
            Check(results.activeSelf, "Results are not visible");
            Check(shift.PassedShift && CasinoDemoProgression.Credits > 0, "Cleared shift did not award upgrade stamps");
            int awardedCredits = CasinoDemoProgression.Credits;
            Call(shift, "FinishShift");
            Check(CasinoDemoProgression.Credits == awardedCredits, "Results awarded stamps twice");
            results.transform.Find("Next shift").GetComponent<Button>().onClick.Invoke();
            GameObject perks = (GameObject)Get(shift, "upgrades");
            Check(perks.activeSelf && !results.activeSelf, "Results did not lead to upgrades");
            if (CasinoDemoProgression.Credits < 3) CasinoDemoProgression.AwardShift(1, 3);
            int beforePurchase = CasinoDemoProgression.Credits;
            perks.transform.Find("Patience").GetComponent<Button>().onClick.Invoke();
            Check(CasinoDemoProgression.Owns(CasinoDemoUpgrade.Patience) &&
                CasinoDemoProgression.Credits == beforePurchase - 3, "Upgrade purchase was not persisted exactly once");
            Check(!CasinoDemoProgression.TryBuy(CasinoDemoUpgrade.Patience), "Duplicate upgrade purchase was accepted");
            perks.transform.Find("Next shift").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            shift = FindFirstObjectByType<CasinoShiftController>();
            Check(shift != null && shift.IsRunning && shift.CurrentShift == 2, "Next shift did not start");
            Check(FindFirstObjectByType<CasinoCustomerSpawner>().PatienceSeconds == 55,
                "Purchased patience perk did not apply to the next shift");
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            yield return null;
            yield return null;
            shift = FindFirstObjectByType<CasinoShiftController>();
            menu = (GameObject)Get(shift, "menu");
            var continueButton = menu.transform.Find("Continue");
            Check(continueButton != null, "Saved continue button missing");
            continueButton.GetComponent<Button>().onClick.Invoke();
            Check(shift.IsRunning && shift.CurrentShift == 2 && !(bool)Get(shift, "tutorial"),
                "Continue did not restore progression without repeating tutorial");
            Check(CasinoDemoProgression.Owns(CasinoDemoUpgrade.Patience), "Continue lost the purchased upgrade");
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            yield return null;
            yield return null;
            shift = FindFirstObjectByType<CasinoShiftController>();
            menu = (GameObject)Get(shift, "menu");
            menu.transform.Find("Start").GetComponent<Button>().onClick.Invoke();
            ((GameObject)Get(shift, "intro")).transform.Find("Skip").GetComponent<Button>().onClick.Invoke();
            spawner = FindFirstObjectByType<CasinoCustomerSpawner>();
            Check(shift.IsRunning && !(bool)Get(shift, "tutorial") && spawner.AcceptingArrivals,
                "Skip tutorial did not start normal scheduling");
            player = FindFirstObjectByType<CasinoPlayerController>();
            BlackjackTableInteraction exhaustedTable = Array.Find(FindObjectsByType<BlackjackTableInteraction>(FindObjectsSortMode.None),
                t => t.TableDisplayName == "BLACKJACK 2");
            Check(spawner.TrySpawnGuidedCustomer(CasinoCustomerGame.Blackjack2), "Could not seat exhaustion-test customer");
            CasinoCustomerAI servedCustomer = null;
            timeout = Time.realtimeSinceStartup + 40;
            while (servedCustomer == null && Time.realtimeSinceStartup < timeout)
            {
                foreach (CasinoCustomerAI customer in FindObjectsByType<CasinoCustomerAI>(FindObjectsSortMode.None))
                    if (customer.Seat != null && customer.Seat.GameType == CasinoCustomerGame.Blackjack2 &&
                        customer.IsInService) { servedCustomer = customer; break; }
                yield return null;
            }
            Check(servedCustomer != null, "Customer did not enter actual Blackjack service");
            Check(servedCustomer.GetComponentInChildren<PixelCharacterVisual>().UsesCustomerAnimation,
                "Customer is using staff-uniform animation");
            AIDealer exhaustedDealer = exhaustedTable.AssignedDealer;
            float oldEnergy = exhaustedDealer.Energy;
            Set(exhaustedDealer, "energy", 0f);
            yield return null;
            Check(!exhaustedDealer.CanServe && !exhaustedTable.IsDealerStaffed,
                "Zero energy dealer still serves the table");
            CustomerPatience patience = servedCustomer.GetComponent<CustomerPatience>();
            Check(patience.IsWaiting, "Exhausted dealer did not restart customer patience");
            int penalties = CustomerSatisfaction.PenaltyCount;
            patience.Begin(1);
            yield return new WaitForSecondsRealtime(1.2f);
            Check(CustomerSatisfaction.PenaltyCount > penalties && !servedCustomer.IsSeated,
                "Neglected exhausted table did not lose its customer and satisfaction");
            Set(exhaustedDealer, "energy", oldEnergy);
            yield return null;
            Check(exhaustedTable.IsDealerStaffed, "Restored dealer cannot serve");
            yield return Press(UnityEngine.InputSystem.Key.P);
            float pausedEnergy = exhaustedDealer.Energy;
            float pausedRemaining = shift.RemainingSeconds;
            yield return new WaitForSecondsRealtime(.4f);
            Check(exhaustedDealer.Energy == pausedEnergy && shift.RemainingSeconds == pausedRemaining,
                "Pause did not freeze dealer energy and shift time");
            ((GameObject)Get(shift, "pausePanel")).transform.Find("Pause card/Main Menu").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            shift = FindFirstObjectByType<CasinoShiftController>();
            menu = (GameObject)Get(shift, "menu");
            Check(menu.activeSelf && !shift.IsRunning && Time.timeScale == 0 && !AudioListener.pause,
                "Pause Main Menu did not return to an idle title");
            Check(menu.transform.Find("Quit").GetComponent<Button>().interactable, "Quit Game button missing");
            Check(CasinoSoundscape.Focus == menu.transform, "Main Menu retained old task audio focus");
            menu.transform.Find("Continue").GetComponent<Button>().onClick.Invoke();
            Check(shift.IsRunning && !AudioListener.pause && Time.timeScale > 0,
                "Continue after Main Menu did not resume playable simulation");
            RouletteRoundController resumedRoulette = FindFirstObjectByType<RouletteRoundController>();
            timeout = Time.realtimeSinceStartup + 30;
            while (!resumedRoulette.IsDealerStaffed && Time.realtimeSinceStartup < timeout) yield return null;
            Check(resumedRoulette.IsDealerStaffed, "Roulette dealer did not reach the table after Continue");
            float rouletteEnergy = resumedRoulette.AssignedDealer.Energy;
            Set(resumedRoulette.AssignedDealer, "energy", 0f);
            yield return null;
            Check(!resumedRoulette.IsDealerStaffed && resumedRoulette.PhaseLabel.Contains("EXHAUSTED"),
                "Roulette dealer exhaustion did not stop service or explain the problem");
            Set(resumedRoulette.AssignedDealer, "energy", rouletteEnergy);
            blackjack = Array.Find(FindObjectsByType<BlackjackTableInteraction>(FindObjectsSortMode.None),
                t => t.TableDisplayName == "BLACKJACK 1");
            player = FindFirstObjectByType<CasinoPlayerController>();
            MoveNear(blackjack.transform, blackjack.IsPlayerInRange);
            blackjack.QueueFlickAssistanceTest();
            FindFirstObjectByType<CasinoCustomerSpawner>().TrySpawnGuidedCustomer(CasinoCustomerGame.Blackjack1);
            timeout = Time.realtimeSinceStartup + 60;
            while (!blackjack.IsFlickReady && Time.realtimeSinceStartup < timeout) yield return null;
            Check(blackjack.IsFlickReady, "Menu-return task did not reach a safe deal boundary");
            yield return Press(UnityEngine.InputSystem.Key.E);
            ((Button)Get(Get(blackjack, "tableView"), "assistanceButton")).onClick.Invoke();
            flick = blackjack.GetComponent<FlickCardMinigame>();
            Check(flick.IsRunning, "Could not start menu-return task");
            yield return Press(UnityEngine.InputSystem.Key.P);
            ((GameObject)Get(shift, "pausePanel")).transform.Find("Pause card/Main Menu").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            shift = FindFirstObjectByType<CasinoShiftController>();
            Check(!shift.IsRunning && !CasinoMinigamePresentation.IsOpen && !AudioListener.pause &&
                CasinoSoundscape.Focus == ((GameObject)Get(shift, "menu")).transform,
                "Returning from a paused task leaked its minigame, pause or audio focus");
            ((GameObject)Get(shift, "menu")).transform.Find("Start").GetComponent<Button>().onClick.Invoke();
            ((GameObject)Get(shift, "intro")).transform.Find("Skip").GetComponent<Button>().onClick.Invoke();
            Check(CasinoDemoProgression.Credits == 0 && !CasinoDemoProgression.Owns(CasinoDemoUpgrade.Patience),
                "New Game did not reset the demo progression");
            Set(shift, "remaining", 0f);
            yield return null;
            yield return null;
            Check(!shift.IsRunning && !shift.PassedShift && CasinoDemoProgression.UnlockedShift == 1 &&
                CasinoDemoProgression.Credits == 0, "Failed shift unlocked content or awarded stamps");
            ((GameObject)Get(shift, "results")).transform.Find("Retry").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            shift = FindFirstObjectByType<CasinoShiftController>();
            Check(shift.IsRunning && shift.CurrentShift == 1 && shift.RemainingSeconds > 530,
                "Results Retry did not start a fresh shift");
            yield return Press(UnityEngine.InputSystem.Key.P);
            ((GameObject)Get(shift, "pausePanel")).transform.Find("Pause card/Restart").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            shift = FindFirstObjectByType<CasinoShiftController>();
            Check(shift.IsRunning && Time.timeScale > 0 && !AudioListener.pause &&
                !CasinoMinigamePresentation.IsOpen, "Restart from pause leaked a paused simulation");
            spawner = FindFirstObjectByType<CasinoCustomerSpawner>();
            desk = FindFirstObjectByType<CasinoCashDeskInteraction>();
            var cashCustomer = (CasinoCustomerAI)Call(spawner, "SpawnCustomer", 0);
            Set(cashCustomer, "state", Enum.Parse(cashCustomer.GetType().GetField("state", Flags).FieldType, "VisitingCashier"));
            cashCustomer.GetComponent<UnityEngine.AI.NavMeshAgent>().SetDestination(spawner.CashierWaypoint.position);
            timeout = Time.realtimeSinceStartup + 30;
            while (!cashCustomer.IsWaitingAtCashDesk && Time.realtimeSinceStartup < timeout) yield return null;
            Check(cashCustomer.IsWaitingAtCashDesk && cashCustomer.GetComponent<CustomerPatience>().IsWaiting,
                "Cashier arrival did not start a patience timer");
            int cashPenalties = CustomerSatisfaction.PenaltyCount;
            cashCustomer.GetComponent<CustomerPatience>().Begin(1);
            yield return new WaitForSecondsRealtime(1.3f);
            Check(!cashCustomer.IsWaitingAtCashDesk && CustomerSatisfaction.PenaltyCount > cashPenalties &&
                desk.ActiveCustomer != cashCustomer, "Expired cashier customer retained the task and queue slot");
            int beforeDestruction = spawner.ActiveCustomerCount;
            Destroy(cashCustomer.gameObject);
            yield return null;
            Check(spawner.ActiveCustomerCount < beforeDestruction, "Destroyed customer leaked a spawner capacity slot");
        }
    }
}
