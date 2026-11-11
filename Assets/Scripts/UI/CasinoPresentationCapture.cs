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
        capture.oldTutorial = PlayerPrefs.GetInt("PitPanic.TutorialComplete", -1);
        foreach (string key in new[] { "PitPanic.TutorialComplete", "PitPanic.UnlockedShift",
            CasinoDemoProgression.CreditsKey, CasinoDemoProgression.UpgradeKeys[0],
            CasinoDemoProgression.UpgradeKeys[1], CasinoDemoProgression.UpgradeKeys[2] })
            capture.savedProgress[key] = PlayerPrefs.GetInt(key, -1);
        Directory.CreateDirectory(capture.directory);
    }

    private IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(2);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "title.png"));
        yield return new WaitForSecondsRealtime(1);
        CasinoTitleScreen title = FindFirstObjectByType<CasinoTitleScreen>();
        title.transform.Find("Start").GetComponent<Button>().onClick.Invoke();
        yield return new WaitForSecondsRealtime(1);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "intro.png"));
        yield return new WaitForSecondsRealtime(1);
        foreach (Button button in FindObjectsByType<Button>(FindObjectsSortMode.None))
            if (button.name == (watch || demoCheck ? "Skip" : "Begin") && button.transform.parent.name == "Introduction")
            {
                button.onClick.Invoke();
                break;
            }
        yield return new WaitForSecondsRealtime(3);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "casino.png"));
        yield return new WaitForSecondsRealtime(1);
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
            resultPanel.transform.Find("Retry").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return new WaitForSecondsRealtime(2);
            shift = FindFirstObjectByType<CasinoShiftController>();
            if (shift == null || !shift.IsRunning || Time.timeScale <= 0 || AudioListener.pause)
            {
                Debug.LogError("Standalone retry did not resume a fresh shift.");
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
        foreach (Button button in FindObjectsByType<Button>(FindObjectsSortMode.None))
            if (button.name == "Close") { button.onClick.Invoke(); break; }
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
        Application.Quit();
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
        foreach (var entry in savedProgress)
            if (entry.Value < 0) PlayerPrefs.DeleteKey(entry.Key);
            else PlayerPrefs.SetInt(entry.Key, entry.Value);
        PlayerPrefs.Save();
    }
}
#endif
