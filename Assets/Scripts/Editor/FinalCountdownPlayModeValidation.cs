using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class FinalCountdownPlayModeValidation
{
    private const string Key = "PitPanic.FinalCountdownValidation";
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    static FinalCountdownPlayModeValidation() => EditorApplication.update += StartTest;

    public static void Run()
    {
        if (Application.isBatchMode)
            throw new InvalidOperationException("Run the countdown validation in a visible Editor for live captures.");
        foreach (string key in ApprovedArtPlayModeValidation.SaveKeys())
            SessionState.SetInt(Key + key, PlayerPrefs.GetInt(key, -1));
        ApprovedArtPlayModeValidation.PrepareGameView();
        EditorSceneManager.OpenScene("Assets/Scenes/PitPanicPrototype.unity");
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    private static void StartTest()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(Key, false)) return;
        SessionState.SetBool(Key, false);
        new GameObject("Final countdown validation").AddComponent<Driver>();
    }

    private static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
    private static object Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, Flags).Invoke(target, args);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public sealed class Driver : MonoBehaviour
    {
        private readonly List<InputDevice> disabledDevices = new List<InputDevice>();
        private Keyboard keyboard;
        private Mouse mouse;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
        private Exception failure;
        private string directory;

        private void Start()
        {
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
            directory = Environment.GetEnvironmentVariable("PIT_PANIC_COUNTDOWN_EVIDENCE");
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("Set PIT_PANIC_COUNTDOWN_EVIDENCE.");
            Directory.CreateDirectory(directory);
            foreach (InputDevice device in InputSystem.devices)
                if (device.enabled && (device is Keyboard || device is Mouse))
                {
                    disabledDevices.Add(device);
                    InputSystem.DisableDevice(device);
                }
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            oldBackground = InputSystem.settings.backgroundBehavior;
            oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Application.logMessageReceived += OnLog;
            StartCoroutine(RunTests());
        }

        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception)
                failure = new Exception(message + "\n" + stack);
        }

        private IEnumerator RunTests()
        {
            Stack<IEnumerator> tests = new Stack<IEnumerator>();
            tests.Push(Test());
            float deadline = Time.realtimeSinceStartup + 150;
            while (tests.Count > 0)
            {
                object current = null;
                bool moved;
                try
                {
                    if (failure != null) throw failure;
                    Check(Time.realtimeSinceStartup < deadline, "Final countdown validation timed out.");
                    IEnumerator test = tests.Peek();
                    moved = test.MoveNext();
                    if (moved) current = test.Current;
                    else tests.Pop();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    Restore();
                    EditorApplication.Exit(1);
                    yield break;
                }
                if (!moved) continue;
                if (current is IEnumerator nested) tests.Push(nested);
                else yield return current;
            }
            Debug.Log("FINAL COUNTDOWN PLAY MODE PASSED: 10..1, movement, pause/resume, final-second real task, " +
                "immediate frozen simulation and inputs, 2s unscaled hold, original pass/fail results, single award and retry.");
            Restore();
            EditorApplication.Exit(0);
        }

        private void Restore()
        {
            Application.logMessageReceived -= OnLog;
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            foreach (InputDevice device in disabledDevices)
                if (device.added) InputSystem.EnableDevice(device);
            foreach (string key in ApprovedArtPlayModeValidation.SaveKeys())
            {
                int value = SessionState.GetInt(Key + key, -1);
                if (value < 0) PlayerPrefs.DeleteKey(key);
                else PlayerPrefs.SetInt(key, value);
            }
            PlayerPrefs.Save();
            Time.timeScale = 1;
            AudioListener.pause = false;
        }

        private IEnumerator Capture(string name)
        {
            Check(Application.isPlaying, "Countdown screenshot must be captured in Play Mode.");
            string path = Path.Combine(directory, name + ".png");
            if (File.Exists(path)) File.Delete(path);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            float deadline = Time.realtimeSinceStartup + 4;
            while (!File.Exists(path) && Time.realtimeSinceStartup < deadline) yield return null;
            Check(File.Exists(path) && new FileInfo(path).Length > 10000, "Missing live screenshot: " + name);
            Debug.Log("FINAL COUNTDOWN CAPTURE " + path);
        }

        private IEnumerator Test()
        {
            yield return null;
            PlayerPrefs.SetInt(CasinoDemoProgression.CreditsKey, 0);
            PlayerPrefs.SetInt(CasinoDemoProgression.UnlockedLevelKey, 1);
            for (int level = 1; level <= 3; level++) PlayerPrefs.DeleteKey(CasinoDemoProgression.BestStarsKey(level));
            foreach (string key in CasinoDemoProgression.UpgradeKeys) PlayerPrefs.DeleteKey(key);
            CasinoShiftController shift = FindFirstObjectByType<CasinoShiftController>();
            Call(shift, "BeginShift", 1, false);
            shift.DismissLesson();
            CasinoCustomerSpawner spawner = FindFirstObjectByType<CasinoCustomerSpawner>();
            spawner.SetAcceptingArrivals(false);
            foreach (AIDealer dealer in FindObjectsByType<AIDealer>(FindObjectsSortMode.None))
                dealer.TrainingProtected = true;
            Set(shift, "completedTasks", 4);
            Set(shift, "failedAttempts", 0);
            Set(shift, "remaining", 10.1f);
            CasinoShiftFinale finale = shift.GetComponent<CasinoShiftFinale>();
            List<int> numbers = new List<int>();
            float deadline = Time.realtimeSinceStartup + 14;
            bool captured = false;
            bool pauseChecked = false;
            CasinoPlayerController player = FindFirstObjectByType<CasinoPlayerController>();
            Vector3 start = player.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.A));
            while (shift.IsRunning && Time.realtimeSinceStartup < deadline)
            {
                int number = finale.DisplayedNumber;
                if (number > 0 && !numbers.Contains(number))
                {
                    numbers.Add(number);
                    AudioSource[] voices = (AudioSource[])Get(FindFirstObjectByType<CasinoSoundscape>(), "voices");
                    Check(voices.Any(voice => voice.isPlaying &&
                        (number > 3 || voice.pitch > 1.1f)), "Countdown tick or urgent audio was not played.");
                }
                if (number == 8 && !pauseChecked)
                {
                    Call(shift, "SetPaused", true);
                    float pausedTime = shift.RemainingSeconds;
                    yield return new WaitForSecondsRealtime(.3f);
                    Check(shift.RemainingSeconds == pausedTime && finale.DisplayedNumber == 8,
                        "Pause advanced the final countdown.");
                    Call(shift, "SetPaused", false);
                    pauseChecked = true;
                }
                if (number == 5 && !captured)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return new WaitForSecondsRealtime(.1f);
                    yield return Capture("countdown-5");
                    captured = true;
                }
                yield return null;
            }
            Check(numbers.SequenceEqual(Enumerable.Range(1, 10).Reverse()), "Countdown did not display exactly 10..1.");
            Check(Vector3.Distance(start, player.transform.position) > .5f, "Movement stopped during the countdown.");
            yield return VerifyFrozenResults(shift, true, "pass", true);
            Debug.Log("COUNTDOWN PASS CASE PASSED: exact 10..1 sequence; movement and pause/resume available.");

            ((GameObject)Get(shift, "results")).transform
                .Find("Full-screen shift results/Results information panel/Retry Shift")
                .GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            shift = FindFirstObjectByType<CasinoShiftController>();
            shift.DismissLesson();
            Check(shift.IsRunning && !CasinoShiftController.GameplayFrozen && Time.timeScale > 0 &&
                shift.GetComponent<CasinoShiftFinale>().DisplayedNumber == 0,
                "Retry did not reset the frozen state/countdown.");
            FindFirstObjectByType<CasinoCustomerSpawner>().SetAcceptingArrivals(false);
            Set(shift, "completedTasks", 0);
            Set(shift, "failedAttempts", 0);
            Set(shift, "remaining", .2f);
            while (shift.IsRunning) yield return null;
            yield return VerifyFrozenResults(shift, false, "fail", false);
            Debug.Log("COUNTDOWN FAIL CASE PASSED: original failed result, no duplicate payout.");

            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            yield return null;
            yield return null;
            shift = FindFirstObjectByType<CasinoShiftController>();
            Call(shift, "BeginShift", 1, false);
            shift.DismissLesson();
            spawner = FindFirstObjectByType<CasinoCustomerSpawner>();
            spawner.SetGuidedPractice(true);
            spawner.SetAcceptingArrivals(false);
            BlackjackTableInteraction table = FindObjectsByType<BlackjackTableInteraction>(FindObjectsSortMode.None)
                .Single(candidate => candidate.TableDisplayName == "BLACKJACK 1");
            CasinoCustomerAI customer = (CasinoCustomerAI)Call(spawner, "SpawnCustomer", 0,
                (CasinoAnimalSpecies?)CasinoAnimalSpecies.Dog);
            CasinoSeat seat = table.GetComponentsInChildren<CasinoSeat>().First();
            Check(customer != null && customer.TryVisitSeat(seat), "Real final-second customer could not reserve BJ1.");
            table.QueueShuffleAssistanceTest();
            deadline = Time.realtimeSinceStartup + 40;
            while (!table.IsShuffleReady && Time.realtimeSinceStartup < deadline) yield return null;
            Check(table.IsShuffleReady, "Real customer/dealer never reached shuffle boundary.");
            BlackjackShuffleMinigame shuffle = table.GetComponent<BlackjackShuffleMinigame>();
            shuffle.Begin();
            Check(shuffle.IsRunning, "Real final-second shuffle could not begin.");
            deadline = Time.realtimeSinceStartup + 15;
            while (shuffle.CompletedCards < shuffle.RequiredCards - 1 && Time.realtimeSinceStartup < deadline)
            {
                if (!shuffle.IsAnimatingCard) shuffle.SelectStack(shuffle.ExpectedStack);
                yield return null;
            }
            while (shuffle.IsAnimatingCard) yield return null;
            Set(shift, "completedTasks", 3);
            Set(shift, "failedAttempts", 0);
            Set(shift, "remaining", .9f);
            Check(shuffle.SelectStack(shuffle.ExpectedStack), "Could not click the final correct shuffle stack.");
            while (shuffle.IsRunning && shift.IsRunning) yield return null;
            Check(!shuffle.IsRunning && shift.IsRunning && shift.RemainingSeconds > 0 &&
                shift.RemainingSeconds < 1 && (int)Get(shift, "completedTasks") == 4,
                "The real task completed in the final second was not counted.");
            Debug.Log($"FINAL-SECOND TASK PASSED: real SHUFFLE completed at {shift.RemainingSeconds:F3}s, tasks=4.");
            while (shift.IsRunning) yield return null;
            yield return VerifyFrozenResults(shift, true, "final-second", false);

            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            yield return null;
            yield return null;
            shift = FindFirstObjectByType<CasinoShiftController>();
            Call(shift, "BeginShift", 1, false);
            shift.DismissLesson();
            spawner = FindFirstObjectByType<CasinoCustomerSpawner>();
            spawner.SetGuidedPractice(true);
            spawner.SetAcceptingArrivals(false);
            table = FindObjectsByType<BlackjackTableInteraction>(FindObjectsSortMode.None)
                .Single(candidate => candidate.TableDisplayName == "BLACKJACK 1");
            customer = (CasinoCustomerAI)Call(spawner, "SpawnCustomer", 0, (CasinoAnimalSpecies?)CasinoAnimalSpecies.Dog);
            Check(customer.TryVisitSeat(table.GetComponentsInChildren<CasinoSeat>().First()),
                "Could not reserve unfinished-task fixture.");
            table.QueueShuffleAssistanceTest();
            deadline = Time.realtimeSinceStartup + 40;
            while (!table.IsShuffleReady && Time.realtimeSinceStartup < deadline) yield return null;
            Check(table.IsShuffleReady, "Unfinished-task fixture never reached a real shuffle boundary.");
            shuffle = table.GetComponent<BlackjackShuffleMinigame>();
            shuffle.Begin();
            Check(shuffle.IsRunning, "Could not open the unfinished shuffle.");
            Set(shift, "completedTasks", 0);
            Set(shift, "remaining", .2f);
            while (shift.IsRunning) yield return null;
            float taskTime = shuffle.TimeRemaining;
            int cards = shuffle.CompletedCards;
            UnityEngine.EventSystems.EventSystem events = UnityEngine.EventSystems.EventSystem.current;
            Check(events != null && !events.sendNavigationEvents && events.currentSelectedGameObject == null,
                "UI keyboard navigation can still submit a hidden task at zero.");
            Check(!shuffle.SelectStack(shuffle.ExpectedStack), "An input changed the unfinished task after zero.");
            yield return VerifyFrozenResults(shift, false, "unfinished-task", false);
            Check(shuffle.IsRunning && shuffle.TimeRemaining == taskTime && shuffle.CompletedCards == cards,
                "The unfinished minigame continued or completed after zero.");
            Debug.Log("UNFINISHED TASK FREEZE PASSED: real open SHUFFLE timer/cards frozen; mouse shield and " +
                "keyboard navigation blocked; existing unfinished-task closing rules retained.");
        }

        private IEnumerator VerifyFrozenResults(CasinoShiftController shift, bool passed, string name, bool capture)
        {
            CasinoShiftFinale finale = shift.GetComponent<CasinoShiftFinale>();
            GameObject results = (GameObject)Get(shift, "results");
            Check(CasinoShiftController.GameplayFrozen && Time.timeScale == 0 && finale.IsTimeUp &&
                finale.Message == "TIME'S UP!" && !results.activeSelf,
                "Zero did not immediately freeze gameplay before showing results.");
            Check(!finale.GetComponentInChildren<CanvasGroup>().interactable &&
                finale.GetComponentInChildren<CanvasGroup>().blocksRaycasts,
                "The TIME'S UP overlay does not block underlying mouse interaction.");
            float zero = Time.realtimeSinceStartup;
            float simulation = Time.time;
            int tasks = (int)Get(shift, "completedTasks");
            int misses = (int)Get(shift, "failedAttempts");
            int tips = CasinoTipJar.TotalPence;
            int credits = CasinoDemoProgression.Credits;
            int bestStars = CasinoDemoProgression.BestStars(1);
            int unlocked = CasinoDemoProgression.UnlockedShift;
            if (passed)
                Check(bestStars > 0 && unlocked >= 2, "The original successful result did not award progression.");
            int spawned = FindFirstObjectByType<CasinoCustomerSpawner>().TotalSpawned;
            Dictionary<Transform, Vector3> positions = FindObjectsByType<PixelCharacterVisual>(FindObjectsSortMode.None)
                .ToDictionary(actor => actor.transform, actor => actor.transform.position);
            Dictionary<AIDealer, float> energies = FindObjectsByType<AIDealer>(FindObjectsSortMode.None)
                .ToDictionary(dealer => dealer, dealer => dealer.Energy);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.W,
                UnityEngine.InputSystem.Key.E, UnityEngine.InputSystem.Key.Escape,
                UnityEngine.InputSystem.Key.Enter, UnityEngine.InputSystem.Key.Space));
            Call(shift, "SetPaused", false);
            Call(shift, "OnTaskSucceeded", new CasinoMinigameResult(true, 100, 0));
            Call(shift, "OnTaskMissed");
            Call(shift, "FinishShift");
            yield return new WaitForSecondsRealtime(.4f);
            Check(!results.activeSelf, "Results appeared before the frozen hold.");
            if (capture) yield return Capture("times-up");
            yield return new WaitForSecondsRealtime(.6f);
            Check(Time.time == simulation && shift.RemainingSeconds == 0 && Time.timeScale == 0 &&
                positions.All(pair => pair.Key.position == pair.Value) &&
                energies.All(pair => pair.Key.Energy == pair.Value),
                "Frozen actors, energy or simulation time advanced.");
            Check((int)Get(shift, "completedTasks") == tasks && (int)Get(shift, "failedAttempts") == misses &&
                CasinoTipJar.TotalPence == tips && CasinoDemoProgression.Credits == credits &&
                CasinoDemoProgression.BestStars(1) == bestStars && CasinoDemoProgression.UnlockedShift == unlocked &&
                FindFirstObjectByType<CasinoCustomerSpawner>().TotalSpawned == spawned,
                "A task, tip, arrival or duplicate payout changed after zero.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            while (!results.activeSelf && Time.realtimeSinceStartup - zero < 3) yield return null;
            float hold = Time.realtimeSinceStartup - zero;
            Check(results.activeSelf && hold >= 1.8f && hold < 2.8f &&
                shift.PassedShift == passed, "Unscaled two-second hold or original pass/fail calculation failed.");
            yield return new WaitForSecondsRealtime(.6f);
            Check(results.transform.Find("Full-screen shift results/Illustrated background")
                .GetComponent<Image>().sprite.name == $"Level1_{(passed ? "Pass" : "Fail")}_Background",
                "Wrong existing results artwork.");
            string totals = results.transform.Find("Full-screen shift results/Results information panel/Task totals")
                .GetComponent<Text>().text;
            Check(totals.Contains($"TASKS COMPLETED  {tasks}"), "Results lost the final task count.");
            Call(shift, "FinishShift");
            Check(CasinoDemoProgression.Credits == credits && CasinoDemoProgression.BestStars(1) == bestStars &&
                CasinoDemoProgression.UnlockedShift == unlocked, "Repeated finalization awarded twice.");
            Debug.Log($"COUNTDOWN {name.ToUpperInvariant()} RESULTS PASSED: hold={hold:F2}s, tasks={tasks}, " +
                $"misses={misses}, passed={passed}, frozen energy/positions/timers, credits={credits} unchanged.");
        }
    }
}
