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
        PlayerPrefs.DeleteKey("PitPanic.TutorialComplete");
        PlayerPrefs.DeleteKey("PitPanic.UnlockedShift");
        EditorSceneManager.OpenScene("Assets/Scenes/PitPanicPrototype.unity");
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

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
            float deadline = Time.realtimeSinceStartup + 240f;
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
            menu.transform.Find("Start").GetComponent<Button>().onClick.Invoke();
            GameObject intro = (GameObject)Get(shift, "intro");
            Check(intro.activeSelf && !shift.IsRunning, "Intro did not follow menu");
            intro.transform.Find("Begin").GetComponent<Button>().onClick.Invoke();
            Check(shift.IsRunning, "Start button failed");
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

            MoveNear(blackjack.transform, blackjack.IsPlayerInRange);
            yield return Press(UnityEngine.InputSystem.Key.Q);
            Check(blackjack.IsInspectionOpen, "Q failed to inspect BJ1");
            yield return null;
            yield return Press(UnityEngine.InputSystem.Key.Escape);
            float timeout = Time.realtimeSinceStartup + 60;
            while (!blackjack.IsFlickReady && Time.realtimeSinceStartup < timeout) yield return null;
            Check(blackjack.IsFlickReady, "FLICK did not become ready");
            yield return Press(UnityEngine.InputSystem.Key.E);
            Check(flick.IsRunning, "E failed to start FLICK");
            yield return Press(UnityEngine.InputSystem.Key.Escape);
            Check(!flick.IsRunning && blackjack.IsFlickReady, "FLICK cancellation failed");
            yield return Press(UnityEngine.InputSystem.Key.E);
            Check(flick.IsRunning, "FLICK retry failed");
            Set(flick, "timeRemaining", 0f);
            yield return null;
            Check(!flick.IsRunning && blackjack.IsFlickReady, "Failed FLICK did not allow retry");
            yield return Press(UnityEngine.InputSystem.Key.E);
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
            Check(rgame.IsRunning, "E failed to start roulette");
            yield return Press(UnityEngine.InputSystem.Key.Escape);
            Check(!rgame.IsRunning && roulette.HasPendingAssistance, "Roulette cancellation failed");
            yield return Press(UnityEngine.InputSystem.Key.E);
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
            results.transform.Find("Next shift").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            shift = FindFirstObjectByType<CasinoShiftController>();
            Check(shift != null && shift.IsRunning && shift.CurrentShift == 2, "Next shift did not start");
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
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            yield return null;
            yield return null;
            shift = FindFirstObjectByType<CasinoShiftController>();
            menu = (GameObject)Get(shift, "menu");
            menu.transform.Find("Skip").GetComponent<Button>().onClick.Invoke();
            spawner = FindFirstObjectByType<CasinoCustomerSpawner>();
            Check(shift.IsRunning && !(bool)Get(shift, "tutorial") && spawner.AcceptingArrivals,
                "Skip tutorial did not start normal scheduling");
        }
    }
}
