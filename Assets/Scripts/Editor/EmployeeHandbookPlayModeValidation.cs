using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Live Play Mode check for the Employee Handbook: opens/closes from the main menu and level select,
/// navigates categories and entries, clears NEW markers, and keeps unlock/read state across scene reloads
/// and New Game. The real profile is saved first and restored afterwards.
/// </summary>
[InitializeOnLoad]
public static class EmployeeHandbookPlayModeValidation
{
    private const string Key = "PitPanic.EmployeeHandbookValidation";

    static EmployeeHandbookPlayModeValidation() => EditorApplication.update += StartTest;

    public static void Run()
    {
        if (Application.isBatchMode)
            throw new InvalidOperationException("Run the handbook validation in a visible Editor for live Play Mode captures.");
        foreach (string key in ApprovedArtPlayModeValidation.SaveKeys())
        {
            SessionState.SetInt(Key + key, PlayerPrefs.GetInt(key, -1));
            PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save();
        ApprovedArtPlayModeValidation.PrepareGameView();
        EditorSceneManager.OpenScene("Assets/Scenes/PitPanicPrototype.unity");
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    private static void StartTest()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(Key, false)) return;
        SessionState.SetBool(Key, false);
        new GameObject("Employee handbook validation").AddComponent<Driver>();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public sealed class Driver : MonoBehaviour
    {
        private readonly List<string> capturedFiles = new List<string>();
        private Keyboard keyboard;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
        private Exception failure;
        private string directory;

        private void Start()
        {
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
            directory = Environment.GetEnvironmentVariable("PIT_PANIC_HANDBOOK_EVIDENCE");
            if (string.IsNullOrEmpty(directory)) throw new InvalidOperationException("Set PIT_PANIC_HANDBOOK_EVIDENCE.");
            Directory.CreateDirectory(directory);
            keyboard = InputSystem.AddDevice<Keyboard>();
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
            if (type == LogType.Error || type == LogType.Exception) failure = new Exception(message + "\n" + stack);
        }

        private IEnumerator RunTests()
        {
            Stack<IEnumerator> tests = new Stack<IEnumerator>();
            tests.Push(Test());
            while (tests.Count > 0)
            {
                object current = null;
                bool moved;
                try
                {
                    if (failure != null) throw failure;
                    moved = tests.Peek().MoveNext();
                    if (moved) current = tests.Peek().Current;
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
            Debug.Log("EMPLOYEE HANDBOOK PLAY MODE PASSED: opens/closes from main menu and level select, " +
                      "navigates all four categories, NEW markers clear on reading, read and unlock state persist " +
                      "across scene reloads and New Game, progression unlocks Guests/Equipment/House Rules.");
            Restore();
            EditorApplication.Exit(0);
        }

        private IEnumerator Test()
        {
            CasinoHandbookContent content = CasinoHandbookContent.Load();
            HandbookEntry[] entries = content.entries;
            Check(entries.Length >= 4 && entries.Select(e => e.id).Distinct().Count() == entries.Length,
                "Handbook content must have unique entry ids.");
            foreach (HandbookCategory category in Enum.GetValues(typeof(HandbookCategory)))
                Check(entries.Any(e => e.category == category), $"Handbook has no {category} entries.");
            foreach (HandbookEntry entry in entries)
                Check(entry.image != null && !string.IsNullOrWhiteSpace(entry.title) &&
                      !string.IsNullOrWhiteSpace(entry.description), $"Handbook entry {entry.id} is incomplete.");
            HandbookEntry[] staff = entries.Where(e => e.category == HandbookCategory.Staff).ToArray();
            Check(staff.All(e => e.unlock == HandbookUnlock.Always), "Staff entries must be available immediately.");
            Check(entries.Where(e => e.category != HandbookCategory.Staff).All(e => e.unlock != HandbookUnlock.Always),
                "Non-staff entries must unlock through demo progression.");

            // Fresh isolated profile: only Staff is readable.
            CasinoTitleScreen title = null;
            yield return WaitForTitle(t => title = t);
            Button launcher = title.transform.Find("Handbook")?.GetComponent<Button>();
            Check(launcher != null && launcher.interactable, "Main menu has no interactable HANDBOOK button.");
            yield return null;
            CheckNoOverlap(launcher, title);
            Check(Badge(launcher), "HANDBOOK button should show NEW while Staff entries are unread.");
            yield return Capture("01-main-menu-handbook-button");

            Click(launcher);
            yield return null;
            yield return new WaitForEndOfFrame();
            yield return null;
            CasinoEmployeeHandbook handbook = FindHandbook();
            Check(handbook.IsOpen, "HANDBOOK button did not open the handbook.");
            Check(TopmostAt(title.transform.Find("Start")) == handbook.transform ||
                  TopmostAt(title.transform.Find("Start")).IsChildOf(handbook.transform),
                "Open handbook must block clicks on the menu underneath; top hit was " +
                TopmostAt(title.transform.Find("Start")).name + " under " + TopmostAt(title.transform.Find("Start")).parent?.name);
            Check(title.transform.Find("Start").GetComponent<Button>().interactable == false,
                "Menu buttons should be disabled while the handbook is open.");
            Check(handbook.CurrentCategory == HandbookCategory.Staff && handbook.CurrentEntry.id == staff[0].id,
                "Handbook should open on the first unread Staff entry.");
            Check(handbook.CurrentEntryShowsNew && !handbook.CurrentEntryLocked &&
                  handbook.CurrentImage == staff[0].image && handbook.CurrentTitleText == staff[0].title &&
                  handbook.CurrentDescriptionText == staff[0].description,
                "First Staff entry should display its image, title, description and NEW marker.");
            Check(handbook.CategoryShowsNew(HandbookCategory.Staff), "Staff tab should show NEW with unread entries.");
            foreach (HandbookCategory category in new[] { HandbookCategory.Guests, HandbookCategory.Equipment, HandbookCategory.HouseRules })
                Check(!handbook.CategoryShowsNew(category), $"Locked {category} tab must not show NEW.");
            yield return Capture("02-handbook-staff-new");

            Button nextButton = Find(handbook, "Next entry");
            Button previousButton = Find(handbook, "Previous entry");
            for (int i = 1; i < staff.Length; i++)
            {
                Click(nextButton);
                yield return null;
                Check(handbook.CurrentEntry.id == staff[i].id && handbook.CurrentEntryShowsNew,
                    $"NEXT should show unread Staff entry {staff[i].id}.");
                if (i == 1) yield return Capture("03-handbook-staff-next");
            }
            Check(!handbook.CategoryShowsNew(HandbookCategory.Staff), "Staff NEW marker should clear after reading every entry.");
            Click(nextButton);
            yield return null;
            Check(handbook.CurrentEntry.id == staff[0].id && !handbook.CurrentEntryShowsNew,
                "NEXT should wrap to the first, now read, Staff entry.");
            Click(previousButton);
            yield return null;
            Check(handbook.CurrentEntry.id == staff[staff.Length - 1].id, "PREV should wrap to the last Staff entry.");

            foreach (HandbookCategory category in new[] { HandbookCategory.Guests, HandbookCategory.Equipment, HandbookCategory.HouseRules })
            {
                Click(Find(handbook, "Tab " + category));
                yield return null;
                Check(handbook.CurrentCategory == category && handbook.CurrentEntryLocked &&
                      handbook.CurrentTitleText == "LOCKED" && !handbook.CurrentEntryShowsNew,
                    $"{category} entries should be locked on a fresh profile.");
                if (category == HandbookCategory.Guests) yield return Capture("04-handbook-guests-locked");
            }

            yield return PressEscape();
            Check(!handbook.IsOpen, "Escape did not close the handbook.");
            Check(launcher.interactable && title.transform.Find("Start").GetComponent<Button>().interactable,
                "Closing the handbook should restore the main menu.");
            Check(!Badge(launcher), "HANDBOOK NEW marker should clear once every unlocked entry is read.");
            foreach (HandbookEntry entry in staff)
                Check(PlayerPrefs.GetInt(CasinoHandbookProgress.ReadKey(entry.id), 0) == 1, $"{entry.id} read state not saved.");

            // Read state survives a full scene reload.
            yield return ReloadScene();
            yield return WaitForTitle(t => title = t);
            launcher = title.transform.Find("Handbook").GetComponent<Button>();
            yield return null;
            Check(!Badge(launcher), "Read state was lost after reloading the scene.");
            Click(launcher);
            yield return null;
            handbook = FindHandbook();
            Check(handbook.IsOpen && handbook.CurrentCategory == HandbookCategory.Staff && !handbook.CurrentEntryShowsNew,
                "Reloaded handbook should open with Staff already read.");
            Click(Find(handbook, "Close handbook"));
            yield return null;
            Check(!handbook.IsOpen, "CLOSE did not close the handbook.");

            // Normal demo progression: training complete and Level 1 passed, unlocking Level 2.
            PlayerPrefs.SetInt("PitPanic.TutorialComplete", 1);
            CasinoDemoProgression.AwardShift(1, 2);
            yield return ReloadScene();
            yield return WaitForTitle(t => title = t);
            launcher = title.transform.Find("Handbook").GetComponent<Button>();
            yield return null;
            Check(Badge(launcher), "Newly unlocked entries should mark the HANDBOOK button NEW.");
            Button continueButton = title.transform.Find("Continue").GetComponent<Button>();
            Check(continueButton.interactable, "Continue should be available after passing Level 1.");
            Click(continueButton);
            GameObject levelSelect = null;
            float deadline = Time.realtimeSinceStartup + 10;
            while (Time.realtimeSinceStartup < deadline)
            {
                levelSelect = GameObject.Find("Level Select");
                if (levelSelect != null && levelSelect.activeInHierarchy) break;
                yield return null;
            }
            Check(levelSelect != null && levelSelect.activeInHierarchy, "Continue did not reach level select.");
            Button levelLauncher = levelSelect.transform.Find("Level Select Handbook")?.GetComponent<Button>();
            Check(levelLauncher != null && Badge(levelLauncher), "Level select HANDBOOK button missing or not marked NEW.");
            yield return Capture("05-level-select-handbook-button");

            Click(levelLauncher);
            yield return null;
            handbook = FindHandbook();
            HandbookEntry[] guests = entries.Where(e => e.category == HandbookCategory.Guests).ToArray();
            Check(handbook.IsOpen && handbook.CurrentCategory == HandbookCategory.Guests &&
                  handbook.CurrentEntry.id == guests[0].id && handbook.CurrentEntryShowsNew && !handbook.CurrentEntryLocked,
                "Level-select handbook should open on the newly unlocked Guests.");
            yield return Capture("06-handbook-guests-unlocked");
            foreach (HandbookEntry entry in entries)
                Check(CasinoHandbookProgress.IsUnlocked(entry) == CasinoHandbookProgress.ConditionMet(entry),
                    $"Unlock state for {entry.id} does not follow demo progression.");
            HandbookEntry lockedLater = entries.FirstOrDefault(e => !CasinoHandbookProgress.IsUnlocked(e));
            Check(lockedLater != null && lockedLater.unlock == HandbookUnlock.LevelUnlocked && lockedLater.level == 3,
                "Level 3 content should stay locked until Level 3 is unlocked.");

            Click(Find(handbook, "Tab " + HandbookCategory.Equipment));
            yield return null;
            Check(handbook.CurrentCategory == HandbookCategory.Equipment && !handbook.CurrentEntryLocked,
                "Equipment should unlock after training.");
            yield return Capture("07-handbook-equipment");
            Click(Find(handbook, "Tab " + HandbookCategory.HouseRules));
            yield return null;
            Check(handbook.CurrentCategory == HandbookCategory.HouseRules && !handbook.CurrentEntryLocked,
                "House Rules should unlock after training.");
            yield return Capture("08-handbook-house-rules");
            Click(Find(handbook, "Close handbook"));
            yield return null;
            Check(!handbook.IsOpen && levelSelect.activeInHierarchy, "Closing should return to level select.");

            // New Game clears campaign progress but must not re-lock earned handbook entries.
            int read = entries.Count(e => PlayerPrefs.GetInt(CasinoHandbookProgress.ReadKey(e.id), 0) == 1);
            CasinoDemoProgression.StartNewGame();
            Check(CasinoDemoProgression.UnlockedShift == 1, "StartNewGame should still reset the campaign.");
            foreach (HandbookEntry entry in guests)
                Check(CasinoHandbookProgress.IsUnlocked(entry), $"{entry.id} was re-locked by New Game.");
            Check(entries.Count(e => PlayerPrefs.GetInt(CasinoHandbookProgress.ReadKey(e.id), 0) == 1) == read,
                "New Game changed handbook read state.");
            yield return ReloadScene();
            yield return WaitForTitle(t => title = t);
            Click(title.transform.Find("Handbook").GetComponent<Button>());
            yield return null;
            handbook = FindHandbook();
            Click(Find(handbook, "Tab " + HandbookCategory.Guests));
            yield return null;
            Check(!handbook.CurrentEntryLocked && !handbook.CurrentEntryShowsNew,
                "Guest unlock/read state should persist after New Game and reload.");
            yield return PressEscape();
            Check(!handbook.IsOpen, "Escape did not close the reloaded handbook.");
            yield return VerifyCapturedFiles();
        }

        private static CasinoEmployeeHandbook FindHandbook()
        {
            CasinoEmployeeHandbook handbook = FindFirstObjectByType<CasinoEmployeeHandbook>(FindObjectsInactive.Include);
            Check(handbook != null, "The playable scene did not create the Employee Handbook.");
            return handbook;
        }

        private static IEnumerator WaitForTitle(Action<CasinoTitleScreen> found)
        {
            float deadline = Time.realtimeSinceStartup + 15;
            CasinoTitleScreen title = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                title = FindFirstObjectByType<CasinoTitleScreen>();
                if (title != null && title.isActiveAndEnabled && title.transform.Find("Handbook") != null) break;
                yield return null;
            }
            Check(title != null && title.isActiveAndEnabled, "Main menu did not appear.");
            for (int i = 0; i < 5; i++) yield return null;
            found(title);
        }

        private static IEnumerator ReloadScene()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            yield return null;
            yield return null;
        }

        private IEnumerator PressEscape()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.Escape));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        private static Button Find(CasinoEmployeeHandbook handbook, string name)
        {
            Button button = handbook.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == name);
            Check(button != null, "Missing handbook control " + name);
            return button;
        }

        private static bool Badge(Button launcher)
        {
            Transform badge = launcher.transform.Find("NEW badge");
            return badge != null && badge.gameObject.activeInHierarchy;
        }

        private static void Click(Button button)
        {
            Check(button.interactable && button.gameObject.activeInHierarchy, $"{button.name} is not clickable.");
            // Route through the real pointer path to the topmost raycast hit at the button centre.
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
            Check(TopmostAt(button.transform) == button.transform || TopmostAt(button.transform).IsChildOf(button.transform),
                $"{button.name} is covered by {TopmostAt(button.transform).name} at {point}.");
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { position = point },
                ExecuteEvents.pointerClickHandler);
        }

        private static Transform TopmostAt(Transform target)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, target.position);
            List<RaycastResult> hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
            Check(hits.Count > 0, "Nothing under " + target.name);
            return hits[0].gameObject.transform;
        }

        private static void CheckNoOverlap(Button launcher, CasinoTitleScreen title)
        {
            Rect handbookRect = ScreenRect((RectTransform)launcher.transform);
            Check(handbookRect.xMin >= 0 && handbookRect.xMax <= Screen.width && handbookRect.yMin >= 0 &&
                  handbookRect.yMax <= Screen.height, $"HANDBOOK button is off-screen: {handbookRect}.");
            foreach (string name in new[] { "Start", "Continue", "Settings", "Quit" })
                Check(!handbookRect.Overlaps(ScreenRect((RectTransform)title.transform.Find(name))),
                    $"HANDBOOK button overlaps {name}.");
        }

        private static Rect ScreenRect(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private IEnumerator Capture(string name)
        {
            string path = Path.Combine(directory, name + ".png");
            if (File.Exists(path)) File.Delete(path);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            capturedFiles.Add(path);
            Debug.Log("HANDBOOK PLAY MODE CAPTURE " + path);
        }

        private IEnumerator VerifyCapturedFiles()
        {
            float deadline = Time.realtimeSinceStartup + 8;
            foreach (string path in capturedFiles)
            {
                while ((!File.Exists(path) || new FileInfo(path).Length <= 10000) && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Check(File.Exists(path) && new FileInfo(path).Length > 10000, "Missing live Play Mode screenshot: " + path);
            }
        }

        private void Restore()
        {
            Application.logMessageReceived -= OnLog;
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            foreach (string key in ApprovedArtPlayModeValidation.SaveKeys())
            {
                int value = SessionState.GetInt(Key + key, -1);
                if (value < 0) PlayerPrefs.DeleteKey(key);
                else PlayerPrefs.SetInt(key, value);
            }
            PlayerPrefs.Save();
            Time.timeScale = 1;
        }
    }
}
