#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public sealed class CasinoPresentationCapture : MonoBehaviour
{
    private string directory;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartCapture()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "--pit-capture-dir");
        if (index < 0 || index + 1 >= args.Length) return;
        Application.runInBackground = true;
        CasinoPresentationCapture capture = new GameObject("Presentation Capture").AddComponent<CasinoPresentationCapture>();
        capture.directory = args[index + 1];
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
            if (button.name == "Begin" && button.transform.parent.name == "Introduction")
            {
                button.onClick.Invoke();
                break;
            }
        yield return new WaitForSecondsRealtime(3);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "casino.png"));
        yield return new WaitForSecondsRealtime(1);
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
}
#endif
