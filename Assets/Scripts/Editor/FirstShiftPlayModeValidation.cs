using System;
using System.Collections;
using System.Collections.Generic;
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
        float[] defaults = { .45f, .3f, .65f };
        string[] channels = { "Music", "Ambience", "Effects" };
        for (int i = 0; i < channels.Length; i++)
        {
            string channel = channels[i];
            SessionState.SetFloat("PitPanic.Old" + channel, PlayerPrefs.GetFloat("PitPanic." + channel + "Volume", -1));
            PlayerPrefs.SetFloat("PitPanic." + channel + "Volume", defaults[i]);
        }
        PlayerPrefs.Save();
        PlayerPrefs.DeleteKey("PitPanic.TutorialComplete");
        PlayerPrefs.DeleteKey("PitPanic.UnlockedShift");
        EditorSceneManager.OpenScene("Assets/Scenes/PitPanicPrototype.unity");
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    private static string[] ProgressKeys() => new[] { CasinoDemoProgression.CreditsKey,
        CasinoDemoProgression.UpgradeKeys[0], CasinoDemoProgression.UpgradeKeys[1], CasinoDemoProgression.UpgradeKeys[2],
        CasinoDemoProgression.BestStarsKey(1), CasinoDemoProgression.BestStarsKey(2),
        CasinoDemoProgression.BestStarsKey(3) };

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
        private float lessonObservedAt = -1;
        private float lessonShiftTime;
        private Exception lessonFailure;
        private static object Get(object o, string name) => o.GetType().GetField(name, Flags).GetValue(o);
        private static object Call(object o, string name, params object[] args) =>
            o.GetType().GetMethod(name, Flags).Invoke(o, args);
        private static void Set(object o, string name, object value) => o.GetType().GetField(name, Flags).SetValue(o, value);
        private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
        private void Start()
        {
            DontDestroyOnLoad(gameObject);
            Application.logMessageReceived += OnLog;
            StartCoroutine(RunTest());
        }
        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception)
                lessonFailure = new Exception("Unity Console error during validation: " + message + "\n" + stack);
        }
        private void Update()
        {
            if (shift == null || !shift.IsLessonOpen) { lessonObservedAt = -1; return; }
            try
            {
                Check(Time.timeScale == 0 && AudioListener.pause && !CasinoMinigamePresentation.IsOpen,
                    "Tutorial explanation did not pause or overlapped a minigame");
                GameObject lessonPanel = (GameObject)Get(shift, "lessonPanel");
                Transform lessonCard = lessonPanel.transform.Find("Tiger's lesson");
                Check(lessonCard != null && lessonCard.Find("Practice animation prop") == null &&
                      lessonCard.Find("Practice animation arrow") == null &&
                      lessonCard.Find("Animated demonstration") == null &&
                      lessonCard.Find("Previous lesson") == null &&
                      lessonCard.Find("Next lesson") == null,
                    "Tutorial popup still contains decorative arrows, duplicate instructions, or page navigation");
                Button[] lessonButtons = lessonCard.GetComponentsInChildren<Button>(true);
                Check(lessonButtons.Length == 2 &&
                      lessonCard.Find("Practise").GetComponentInChildren<Text>().text == "Okay" &&
                      lessonCard.Find("Skip training").GetComponentInChildren<Text>().text == "SKIP TRAINING",
                    "Tutorial must keep only the Okay and SKIP TRAINING controls");
                ValidatePopup(lessonCard);
                string instruction = ((Text)Get(shift, "lessonText")).text;
                int sentenceCount = 0;
                foreach (char character in instruction)
                    if (character == '.' || character == '!' || character == '?') sentenceCount++;
                Check(sentenceCount <= 2, $"Tutorial popup is longer than two short sentences: {instruction}");
                if (lessonObservedAt < 0) { lessonObservedAt = Time.realtimeSinceStartup; lessonShiftTime = shift.RemainingSeconds; }
                Check(shift.RemainingSeconds == lessonShiftTime, "Tutorial explanation advanced the shift timer");
                if (Time.realtimeSinceStartup - lessonObservedAt > .12f)
                {
                    if (shift.TutorialStage == 0)
                        lessonCard.Find("Practise").GetComponent<Button>().onClick.Invoke();
                    else
                        shift.DismissLesson();
                }
            }
            catch (Exception e) { lessonFailure = e; }
        }

        private IEnumerator RunTest()
        {
            IEnumerator test = Test();
            float deadline = Time.realtimeSinceStartup + 420f;
            while (true)
            {
                bool next;
                try
                {
                    if (Time.realtimeSinceStartup >= deadline)
                    {
                        BlackjackTableInteraction stalled = FindFirstObjectByType<BlackjackTableInteraction>();
                        string seats = "";
                        foreach (CasinoCustomerAI c in FindObjectsByType<CasinoCustomerAI>(FindObjectsSortMode.None))
                            seats += $" [{c.name} seated={c.IsSeated} service={c.IsInService} patience={c.PatienceNormalized:0.00}]";
                        throw new Exception($"Shift test timed out. lesson={shift?.IsLessonOpen} stage={shift?.TutorialStage} " +
                            $"bj={stalled?.State} shuffleReady={stalled?.IsShuffleReady} staffed={stalled?.IsDealerStaffed}{seats}");
                    }
                    if (lessonFailure != null) throw lessonFailure;
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

        private IEnumerator ValidateTipping(BlackjackTableInteraction blackjack)
        {
            Check(FindFirstObjectByType<CasinoTipJarProp>() != null, "The Pit Desk tip jar is missing");
            CasinoFloatRefillDestination tableFloat = blackjack.GetComponent<CasinoFloatRefillDestination>();
            yield return null;
            if (blackjack.IsServiceAvailable && !blackjack.IsShuffleReady && !tableFloat.HasBlockedPayout)
                foreach (CasinoCustomerAI seated in FindObjectsByType<CasinoCustomerAI>(FindObjectsSortMode.None))
                    if (seated.IsSeated && seated.Seat != null && seated.Seat.DealerStation != null &&
                        seated.Seat.DealerStation.BlackjackTable == blackjack)
                        Check(!seated.GetComponent<CustomerPatience>().IsWaiting &&
                              !(seated.TryGetComponent(out CustomerPatienceMeter meter) && meter.ShowsVipWarning),
                            "A seated customer kept a HURRY/patience timer at a working table");
            CasinoCustomerSpawner tipSpawner = FindFirstObjectByType<CasinoCustomerSpawner>();
            CasinoCustomerAI tipper = (CasinoCustomerAI)Call(tipSpawner, "SpawnCustomer", 0);
            Check(tipper != null, "Could not create a tipping customer");
            tipper.SetSatisfactionForTesting(1f, 100000);
            long floatBefore = tableFloat.InventoryValuePence;
            int jarBefore = CasinoTipJar.TotalPence;
            CasinoTipRecord tip = default;
            string key = null;
            for (int attempt = 0; attempt < 60 && key == null; attempt++)
                if (CasinoTipping.TryTip(tipper, "playmode:tip:" + attempt, true, false, out tip)) key = tip.ServiceKey;
            Check(key != null && tip.AmountPence > 0, "A delighted customer never tipped");
            Check(tipper.BankrollPence == 100000 - tip.AmountPence, "The tip was not deducted from the customer's bankroll");
            Check(!CasinoTipping.TryTip(tipper, key, true, false, out _) && CasinoTipJar.TotalPence == jarBefore + tip.AmountPence,
                "A service event tipped twice");
            Check(tableFloat.InventoryValuePence == floatBefore, "Tips leaked into the table float");
            bool delivered = false;
            Action<CasinoTipRecord> onDelivered = record => delivered |= record.ServiceKey == key;
            CasinoPerformanceDirector.TipDelivered += onDelivered;
            CasinoPerformanceDirector.PlayTip(tip, blackjack.AssignedDealer != null ? blackjack.AssignedDealer : (Component)blackjack, () => true);
            yield return new WaitForSeconds(.5f);
            Destroy(tipper.gameObject);
            float deadline = Time.realtimeSinceStartup + 6f;
            while (!delivered && Time.realtimeSinceStartup < deadline) yield return null;
            yield return new WaitForSeconds(2.5f);
            CasinoPerformanceDirector.TipDelivered -= onDelivered;
            Check(delivered, "Interrupted tip performance was not delivered");
            Check(CasinoPerformanceDirector.ActivePropCount < 40 && CasinoPerformanceDirector.ActiveBeatCount < 200,
                "Tip performances leaked flying chips, effects or beats");
            Check(tableFloat.InventoryValuePence == floatBefore, "Tip performance changed the float");
        }

        private IEnumerator Press(Key key)
        {
            while (shift != null && shift.IsLessonOpen) yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        private static void ClickRack(CashDeskPackMinigame game, int denomination)
        {
            string[] names = { "RED", "BLUE", "BLACK", "PINK" };
            Canvas canvas = (Canvas)Get(game, "hud");
            canvas.transform.Find($"Register {names[denomination]} Chip Choice").GetComponent<Button>().onClick.Invoke();
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

        private static void ValidatePopup(Transform popup)
        {
            Canvas.ForceUpdateCanvases();
            Button[] buttons = popup.GetComponentsInChildren<Button>();
            foreach (Button button in buttons)
            {
                RectTransform parent = button.transform.parent as RectTransform;
                RectTransform rect = (RectTransform)button.transform;
                Vector3[] corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 local = parent.InverseTransformPoint(corner);
                    Check(local.x >= parent.rect.xMin + 8 && local.x <= parent.rect.xMax - 8 &&
                          local.y >= parent.rect.yMin + 8 && local.y <= parent.rect.yMax - 8,
                        $"{popup.name}/{button.name} touches or crosses its panel border");
                }
                foreach (Button other in buttons)
                {
                    if (other == button || other.transform.parent != parent) continue;
                    RectTransform otherRect = (RectTransform)other.transform;
                    Rect a = new Rect(rect.anchoredPosition - rect.sizeDelta * .5f, rect.sizeDelta);
                    Rect b = new Rect(otherRect.anchoredPosition - otherRect.sizeDelta * .5f, otherRect.sizeDelta);
                    Check(!a.Overlaps(b), $"{popup.name}: {button.name} overlaps {other.name}");
                }
            }
        }

        private static void ValidateCampaignSeating()
        {
            foreach (int level in new[] { 1, 2, 3, 1 })
            {
                CasinoCampaignPresentation.Apply(level);
                foreach (AIDealer dealer in FindObjectsByType<AIDealer>(FindObjectsSortMode.None))
                    if (dealer.IsAtStation && dealer.AssignedStation != null)
                        Check(Vector3.ProjectOnPlane(dealer.transform.position - dealer.AssignedStation.DealerPosition.position,
                            Vector3.up).magnitude < .15f,
                            $"Level {level}: {dealer.name} was left behind when its table moved");
                foreach (CasinoSeat seat in FindObjectsByType<CasinoSeat>(FindObjectsSortMode.None))
                {
                    Transform chair = seat.GetComponent<CasinoSeatVisual>().Chair;
                    Check(Vector3.Distance(chair.position, seat.SittingPosition) < .02f,
                        $"Level {level}: {seat.name} chair is not at its sitting anchor");
                    Check(UnityEngine.AI.NavMesh.SamplePosition(seat.Position, out var hit, .25f,
                        UnityEngine.AI.NavMesh.AllAreas) && Vector3.Distance(hit.position, seat.Position) < .25f,
                        $"Level {level}: {seat.name} approach uses a stale table footprint");
                    UnityEngine.AI.NavMeshPath path = new UnityEngine.AI.NavMeshPath();
                    Check(UnityEngine.AI.NavMesh.CalculatePath(new Vector3(0, 0, -6), hit.position,
                        UnityEngine.AI.NavMesh.AllAreas, path) &&
                        path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete,
                        $"Level {level}: {seat.name} cannot be reached");
                }

            }
        }

        private static void ValidatePackingCapacity(CashDeskPackMinigame pack)
        {
            int[] packed = (int[])Get(pack, "packedCounts");
            int[] required = (int[])Get(pack, "requiredCounts");
            int[] oldPacked = (int[])packed.Clone();
            int[] oldRequired = (int[])required.Clone();
            RectTransform tray = (RectTransform)Get(pack, "floatTrayRect");
            var views = (List<RectTransform>[])Get(pack, "trayChipViews");
            foreach (int count in new[] { 32, 128, 1000 })
            {
                for (int denomination = 0; denomination < packed.Length; denomination++)
                    packed[denomination] = required[denomination] = count;
                Call(pack, "RenderTrayStacks");
                Canvas.ForceUpdateCanvases();
                for (int denomination = 0; denomination < packed.Length; denomination++)
                {
                    Check(views[denomination].Count <= 15, "Large refill rendered an unbounded page of chips");
                    foreach (RectTransform chip in views[denomination])
                    {
                        Vector3[] corners = new Vector3[4];
                        chip.GetWorldCorners(corners);
                        foreach (Vector3 corner in corners)
                        {
                            Vector3 local = tray.InverseTransformPoint(corner);
                            Check(tray.rect.Contains(local), "Packed chips spill outside the float tray");
                        }
                    }
                    Text[] counts = (Text[])Get(pack, "stackCountTexts");
                    Check(counts[denomination].text == $"{count}/{count}",
                        "Compacted chip stacks lost their exact counts");
                }
            }
            Array.Copy(oldPacked, packed, packed.Length);
            Array.Copy(oldRequired, required, required.Length);
            Call(pack, "RenderTrayStacks");
        }

        private IEnumerator Test()
        {
            Check(CasinoDemoProgression.Passed(4, 60, 5) &&
                !CasinoDemoProgression.Passed(3, 100, 0) &&
                !CasinoDemoProgression.Passed(4, 59, 0) &&
                !CasinoDemoProgression.Passed(4, 100, 6),
                "Shift goals have incorrect satisfaction/task boundaries");
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
            var shuffle = blackjack.GetComponent<BlackjackShuffleMinigame>();
            var rgame = FindFirstObjectByType<RouletteTableMinigame>();
            var pack = FindFirstObjectByType<CashDeskPackMinigame>();
            var desk = FindFirstObjectByType<CasinoCashDeskInteraction>();
            var manager = FindFirstObjectByType<DealerAssignmentManager>();
            Check(Time.timeScale == 0 && !spawner.AcceptingArrivals, "Menu failed to pause");
            ValidateCampaignSeating();
            ValidatePopup(((GameObject)Get(shift, "pausePanel")).transform.Find("Pause card"));
            Check(CasinoSoundscape.CurrentMusicTrack == CasinoMusicTrack.Title &&
                ((AudioSource)typeof(CasinoSoundscape).GetField("music", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(FindFirstObjectByType<CasinoSoundscape>())).loop &&
                ((AudioSource)typeof(CasinoSoundscape).GetField("music", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(FindFirstObjectByType<CasinoSoundscape>())).clip.name.Contains("PitPanicWelcome"),
                "Title did not select looping welcome music");
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
            GameObject levelSelect = (GameObject)Get(shift, "levelSelect");
            Check(levelSelect.activeSelf && !shift.IsRunning, "New Game did not open the campaign level selector");
            levelSelect.transform.Find("Level 1").GetComponent<Button>().onClick.Invoke();
            GameObject intro = (GameObject)Get(shift, "intro");
            Check(intro.activeSelf && !shift.IsRunning, "Intro did not follow menu");
            ValidatePopup(intro.transform);
            yield return null;
            Check(!((Text)Get(FindFirstObjectByType<PitDeskInteraction>(), "promptText")).gameObject.activeSelf,
                "Pit Desk interaction prompt leaked behind onboarding");
            intro.transform.Find("Back").GetComponent<Button>().onClick.Invoke();
            Check(levelSelect.activeSelf && !intro.activeSelf &&
                !PlayerPrefs.HasKey("PitPanic.TutorialComplete"),
                "Backing out of a level briefing changed progress or failed to return to level selection");
            levelSelect.transform.Find("Level Select Back").GetComponent<Button>().onClick.Invoke();
            Check(menu.activeSelf && CasinoSoundscape.Focus == menu.transform,
                "Level selection Back did not return to the title");
            menu.transform.Find("Start").GetComponent<Button>().onClick.Invoke();
            levelSelect = (GameObject)Get(shift, "levelSelect");
            levelSelect.transform.Find("Level 1").GetComponent<Button>().onClick.Invoke();
            intro = (GameObject)Get(shift, "intro");
            intro.transform.Find("Begin").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            shift = FindFirstObjectByType<CasinoShiftController>();
            player = FindFirstObjectByType<CasinoPlayerController>();
            spawner = FindFirstObjectByType<CasinoCustomerSpawner>();
            blackjack = Array.Find(FindObjectsByType<BlackjackTableInteraction>(FindObjectsSortMode.None),
                t => t.TableDisplayName == "BLACKJACK 1");
            inspection = FindFirstObjectByType<RouletteInspectionView>();
            roulette = FindFirstObjectByType<RouletteRoundController>();
            shuffle = blackjack.GetComponent<BlackjackShuffleMinigame>();
            rgame = FindFirstObjectByType<RouletteTableMinigame>();
            pack = FindFirstObjectByType<CashDeskPackMinigame>();
            desk = FindFirstObjectByType<CasinoCashDeskInteraction>();
            manager = FindFirstObjectByType<DealerAssignmentManager>();
            Check(shift.IsRunning, "Start button failed");
            Check(CasinoSoundscape.CurrentMusicTrack == CasinoMusicTrack.Casino,
                "Starting a shift did not select casino background music");
            CasinoSoundscape soundscape = FindFirstObjectByType<CasinoSoundscape>();
            AudioSource backgroundMusic = (AudioSource)typeof(CasinoSoundscape)
                .GetField("music", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(soundscape);
            Check(backgroundMusic.loop && backgroundMusic.clip.name.Contains("PixelatedRoulette") &&
                backgroundMusic.volume <= CasinoSoundscape.MusicVolume * .121f &&
                backgroundMusic.volume < CasinoSoundscape.EffectsVolume,
                "Casino music did not loop quietly beneath louder effects");
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
            CasinoTableAttention unrelatedAttention = Array.Find(
                FindObjectsByType<BlackjackTableInteraction>(FindObjectsSortMode.None),
                table => table != blackjack).GetComponent<CasinoTableAttention>();
            unrelatedAttention.RequestAttention(TableAttentionReason.CustomerComplaint);
            foreach (CasinoAttentionIndicator indicator in FindObjectsByType<CasinoAttentionIndicator>(FindObjectsSortMode.None))
            {
                Call(indicator, "LateUpdate");
                Check(!((Canvas)Get(indicator, "indicatorCanvas")).enabled,
                    "Unrelated task marker interrupted the movement lesson");
            }
            Check(unrelatedAttention.HasPendingTasks && !unrelatedAttention.HasVisibleTasks,
                "Training hid a task by deleting its actual gameplay state");
            unrelatedAttention.ResolveAttention(TableAttentionReason.CustomerComplaint);
            Vector3 movementStart = player.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.W));
            yield return new WaitForSeconds(1f);
            Debug.Log($"MOVEMENT TEST {movementStart} -> {player.transform.position}; key={keyboard.wKey.isPressed}; timeScale={Time.timeScale}");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Check((bool)Get(shift, "moved"), "W movement did not advance tutorial");
            while (shift.IsLessonOpen) yield return null;
            yield return null;
            Vector3 arrowStart = player.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.UpArrow));
            yield return new WaitForSeconds(0.25f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Check(player.transform.position.z > arrowStart.z + 0.1f &&
                  Mathf.Abs(player.transform.position.x - arrowStart.x) < 0.1f,
                $"Arrow-key movement did not follow the screen-aligned axes: {arrowStart} -> {player.transform.position}");
            foreach (AIDealer dealer in manager.Dealers)
                if (dealer.IsAtStation)
                    Check(Vector3.Distance(dealer.GetComponentInChildren<PixelCharacterVisual>().transform.localPosition,
                        Vector3.zero) < .02f, "Working dealer visual drifted away from its station");
            foreach (AIDealer dealer in manager.Dealers)
            {
                DealerEnergyMeter energyMeter = dealer.GetComponent<DealerEnergyMeter>();
                Check(energyMeter != null && energyMeter.IsShowing == (dealer.IsAtStation && dealer.Energy > 0) &&
                      (!energyMeter.IsShowing || Mathf.Abs(energyMeter.DisplayedFraction - Mathf.Clamp01(dealer.Energy / 100f)) < .02f),
                    "Working dealers must show their own energy bar above their head");
            }

            MoveNear(blackjack.transform, blackjack.IsPlayerInRange);
            yield return Press(UnityEngine.InputSystem.Key.Q);
            Check(blackjack.IsInspectionOpen, "Q failed to inspect BJ1");
            ValidatePopup(((GameObject)Get(Get(blackjack, "tableView"), "board")).transform);
            yield return null;
            yield return Press(UnityEngine.InputSystem.Key.Escape);
            float timeout = Time.realtimeSinceStartup + 60;
            while (!blackjack.IsShuffleReady && Time.realtimeSinceStartup < timeout) yield return null;
            Check(blackjack.IsShuffleReady, "SHUFFLE did not become ready");
            Check(blackjack.GetComponent<CasinoTableAttention>().HasVisibleTasks &&
                !unrelatedAttention.GetComponent<CasinoMinigameStation>().RequestMinigame(
                    CasinoMinigameType.Shuffle, TableAttentionReason.ShuffleAssistanceRequired),
                "Training did not focus task requests on the active Blackjack lesson");
            yield return Press(UnityEngine.InputSystem.Key.E);
            Check(blackjack.IsInspectionOpen, "E did not open unified BJ1 interface");
            Check(!blackjack.GetComponent<CasinoMinigameStation>().TryBegin(), "Minigame overlapped table inspection");
            Check(!inspection.TryOpenInspectionFromPlayerInput(), "Roulette overlapped Blackjack inspection");
            ((Button)Get(Get(blackjack, "tableView"), "assistanceButton")).onClick.Invoke();
            Check(shuffle.IsRunning, "E failed to start SHUFFLE");
            Check(blackjack.IsDealerStaffed && blackjack.IsShuffleReady,
                "SHUFFLE was offered without an active dealer needing assistance");
            Button shuffleButton = (Button)Get(Get(blackjack, "tableView"), "assistanceButton");
            Check(shuffleButton.GetComponentInChildren<Text>().text == "SHUFFLE!",
                "The Blackjack action button must say only SHUFFLE!");
            Check(shuffle.ExpectedStack is 0 or 1 && shuffle.RequiredCards == 8,
                "New SHUFFLE did not present a two-stack sequence");
            Check(!BlackjackTableInteraction.CanOfferShuffle(false, false) &&
                  !BlackjackTableInteraction.CanOfferShuffle(true, true) &&
                  BlackjackTableInteraction.CanOfferShuffle(true, false),
                "SHUFFLE availability did not require a staffed, dealer-operated table");
            CasinoMinigamePresentation shufflePresentation = FindFirstObjectByType<CasinoMinigamePresentation>();
            Check(shufflePresentation != null &&
                  shufflePresentation.GameArea.Find("SHUFFLE Clickable Card Shoe") == null &&
                  shufflePresentation.GameArea.Find("LEFT STACK") != null &&
                  shufflePresentation.GameArea.Find("RIGHT STACK") != null,
                "SHUFFLE does not show two card stacks or still contains the old shoe");
            Check(CasinoSoundscape.Focus == shuffle.transform && !CasinoSoundscape.AllowsEffect(null) &&
                CasinoSoundscape.AllowsEffect(shuffle.transform) && !CasinoSoundscape.AllowsEffect(rgame.transform),
                "Minigame audio did not isolate the active task");
            Check(CasinoSoundscape.CurrentMusicTrack == CasinoMusicTrack.Casino,
                "Opening a minigame changed the casino soundtrack");
            yield return Press(UnityEngine.InputSystem.Key.P);
            float shufflePausedAt = shuffle.TimeRemaining;
            Vector3 pausedPlayer = player.transform.position;
            Check(AudioListener.pause && Time.timeScale == 0, "Pause did not stop audio and simulation");
            yield return Press(UnityEngine.InputSystem.Key.E);
            yield return Press(UnityEngine.InputSystem.Key.W);
            yield return new WaitForSecondsRealtime(.4f);
            Check(shuffle.IsRunning && shuffle.TimeRemaining == shufflePausedAt &&
                player.transform.position == pausedPlayer, "Paused minigame or character continued updating");
            ((GameObject)Get(shift, "pausePanel")).transform.Find("Pause card/Resume").GetComponent<Button>().onClick.Invoke();
            Check(!AudioListener.pause && Time.timeScale > 0 && shuffle.IsRunning,
                "Resume did not restore the existing minigame");
            yield return Press(UnityEngine.InputSystem.Key.Escape);
            Check(!shuffle.IsRunning && blackjack.IsShuffleReady, "SHUFFLE cancellation failed");
            Check(CasinoSoundscape.Focus == null && CasinoSoundscape.AllowsEffect(null),
                "Closing minigame did not restore casino audio");
            yield return Press(UnityEngine.InputSystem.Key.E);
            ((Button)Get(Get(blackjack, "tableView"), "assistanceButton")).onClick.Invoke();
            Check(shuffle.IsRunning, "SHUFFLE retry failed");
            Button wrongStack = (Button)Get(shuffle, shuffle.ExpectedStack == 0 ? "rightStackButton" : "leftStackButton");
            float beforeMistake = shuffle.TimeRemaining;
            wrongStack.onClick.Invoke();
            Check(!shuffle.IsRunning && blackjack.IsShuffleReady && shuffle.HasCarriedDeadline &&
                shuffle.CompletedCards == 0 && (int)Get(shuffle, "misses") == 1,
                "A wrong SHUFFLE stack did not end the attempt while keeping the task pending");
            Check(shuffle.TryGetCarriedDeadline(out float carriedLeft, out _) && carriedLeft > 0,
                "A failed SHUFFLE hid the task countdown");
            yield return new WaitForSeconds(.5f);
            yield return Press(UnityEngine.InputSystem.Key.E);
            ((Button)Get(Get(blackjack, "tableView"), "assistanceButton")).onClick.Invoke();
            Check(shuffle.IsRunning && shuffle.CompletedCards == 0 && shuffle.TimeRemaining < beforeMistake - .3f,
                $"SHUFFLE retry reset the task countdown: before={beforeMistake}, now={shuffle.TimeRemaining}");
            Set(shuffle, "timeRemaining", 0f);
            yield return null;
            Check(!shuffle.IsRunning && blackjack.IsShuffleReady && !shuffle.HasCarriedDeadline,
                "Failed SHUFFLE did not allow retry");
            yield return Press(UnityEngine.InputSystem.Key.E);
            ((Button)Get(Get(blackjack, "tableView"), "assistanceButton")).onClick.Invoke();
            Check(shuffle.IsRunning, "SHUFFLE retry after failure failed");
            while (shuffle.IsRunning)
            {
                Button stackButton = (Button)Get(shuffle, shuffle.ExpectedStack == 0 ? "leftStackButton" : "rightStackButton");
                stackButton.onClick.Invoke();
                while (shuffle.IsAnimatingCard && shuffle.IsRunning) yield return null;
            }
            Check((bool)Get(shift, "shuffleDone"), "SHUFFLE did not advance tutorial");
            BlackjackTableInteraction[] blackjackTables =
                FindObjectsByType<BlackjackTableInteraction>(FindObjectsSortMode.None);
            MoveNear(inspection.transform, candidate => inspection.IsPlayerInRange(candidate) &&
                Array.TrueForAll(blackjackTables, table => !table.IsPlayerInRange(candidate)));
            Check(inspection.TryOpenInspectionFromPlayerInput(), "Could not open Roulette inspection after SHUFFLE");
            ValidatePopup(((GameObject)Get(inspection, "board")).transform);
            yield return null;
            yield return Press(UnityEngine.InputSystem.Key.Escape);
            Check(roulette.FloatDestination.TryAddChips(new[] { 100, 100, 100, 100 }),
                "Could not provision synthetic roulette payout inventory");
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
            Check(rgame.IsCoverMode && roulette.IsCoverReady && rgame.WinningNumber == roulette.AssistanceWinningNumber,
                "Staffed Roulette did not open real-result COVER");
            int coverNumber = rgame.WinningNumber;
            long coverRound = rgame.AssistanceRoundId;
            Check((Transform)Get(rgame, "spinStageRoot") == null && (Transform)Get(rgame, "sweepStageRoot") == null,
                "COVER still builds SPIN or SWEEP gameplay");
            Set(rgame, "coverRemaining", 0f);
            yield return null;
            Check(!rgame.IsRunning && roulette.IsCoverReady && roulette.AssistanceWinningNumber == coverNumber &&
                roulette.AssistanceRoundId == coverRound, "COVER timeout lost the real result or bets");
            yield return Press(UnityEngine.InputSystem.Key.E);
            Button coverButton = ((GameObject)Get(inspection, "board")).transform.Find("Start assistance").GetComponent<Button>();
            Check(coverButton.GetComponentInChildren<Text>().text == "COVER!", "Roulette button is not simply COVER!");
            coverButton.onClick.Invoke();
            Check(rgame.IsCoverMode && rgame.WinningNumber == coverNumber, "COVER retry spun a new number");
            long inventoryBeforeCover = roulette.FloatDestination.InventoryValuePence;
            long expectedCoverPayment = 0;
            long expectedLosingStakes = 0;
            foreach (RouletteCustomerBet bet in roulette.AssistanceBets)
            {
                if (!bet.IsWinning)
                {
                    expectedLosingStakes += bet.Bet.StakePence;
                    Check(!rgame.SelectCoverBet(bet.BetId), "COVER accepted a losing bet");
                    continue;
                }
                expectedCoverPayment += bet.WinningsPence;
                rgame.DeclareNoCoverWinners();
                Check(rgame.IsRunning && rgame.CoverSelection.MarkedCount == 0, "NO WINNERS bypassed a winner");
                Transform area = FindFirstObjectByType<CasinoMinigamePresentation>().GameArea;
                area.Find($"COVER Bet {bet.BetId}").GetComponent<Button>().onClick.Invoke();
                Check(!rgame.SelectCoverBet(bet.BetId), "COVER accepted a duplicate mark");
            }
            if (rgame.CoverSelection.CanDeclareNoWinners) rgame.DeclareNoCoverWinners();
            while (rgame.IsRunning) yield return null;
            Check(roulette.Phase == RouletteRoundPhase.SettlingBets, "COVER did not settle its real round");
            yield return null;
            while (shift.IsLessonOpen) yield return null;
            yield return null;
            Check(roulette.FloatDestination.InventoryValuePence == inventoryBeforeCover + expectedLosingStakes - expectedCoverPayment,
                $"COVER automatic payouts did not deduct actual inventory exactly once: " +
                $"before={inventoryBeforeCover}, expectedPayment={expectedCoverPayment}, actual={roulette.FloatDestination.InventoryValuePence}");
            foreach (RouletteCustomerBet bet in roulette.CurrentBets)
                Check(!bet.IsWinning || bet.IsPaid, "COVER left a funded winner unpaid");
            Check((bool)Get(shift, "rouletteDone"), "Roulette did not advance tutorial");
            timeout = Time.realtimeSinceStartup + 40;
            while (desk.ActiveCustomer == null && Time.realtimeSinceStartup < timeout) yield return null;
            string customerSnapshot = "";
            foreach (object customerObject in (IEnumerable)Get(spawner, "activeCustomers"))
            {
                CasinoCustomerAI customer = customerObject as CasinoCustomerAI;
                if (customer == null) continue;
                var agent = customer.GetComponent<UnityEngine.AI.NavMeshAgent>();
                string path = agent.enabled ? $"{agent.pathStatus}/{agent.remainingDistance:0.0}" : "seated";
                customerSnapshot += $"{customer.name}:cashier={customer.IsVisitingCashier}," +
                    $"waiting={customer.IsWaitingAtCashDesk},seat={customer.Seat?.name ?? "none"}," +
                    $"position={customer.transform.position},path={path} ";
            }
            Check(desk.ActiveCustomer != null,
                $"Guided cash-out customer did not reach the Cash Desk; stage={shift.TutorialStage}, " +
                $"active={spawner.ActiveCustomerCount}, requestedOrders={desk.GetRequestedOrders().Count}, " +
                $"packTaskPending={desk.GetComponent<CasinoMinigameStation>().IsTaskPending}, customers={customerSnapshot}");
            var station = desk.GetComponent<CasinoMinigameStation>();
            MoveNear(desk.transform, station.IsPlayerInRange);
            yield return Press(UnityEngine.InputSystem.Key.E);
            Check(pack.IsRunning && pack.IsCashOutMode, "Guided cash-out did not open");
            Check(FindObjectsByType<CasinoChipDragHandle>(FindObjectsSortMode.None).Length == 0,
                "Cash Desk rack still has drag handlers");
            int[] guidedSequence = (int[])Get(pack, "packingSequence");
            ClickRack(pack, guidedSequence[0]);
            Check(pack.PackedChipCount == 1, "Cash-out rack click did not add a chip");
            pack.UndoLastChip();
            Check(pack.PackedChipCount == 0, "Cash-out Undo did not remove the last chip");
            foreach (int denomination in guidedSequence) ClickRack(pack, denomination);
            Check(pack.IsReadyToDeliver, "Cash-out clicks did not complete exact contents");
            pack.UndoLastChip();
            Check(!pack.IsReadyToDeliver && pack.PackedChipCount == guidedSequence.Length - 1,
                "Cash-out Undo failed after the tray became ready");
            ClickRack(pack, guidedSequence[guidedSequence.Length - 1]);
            yield return Press(UnityEngine.InputSystem.Key.E);
            Check(!pack.IsRunning && (bool)Get(shift, "cashOutDone"), "Cash-out success did not advance training");
            timeout = Time.realtimeSinceStartup + 10;
            while (desk.GetRequestedOrders().Count == 0 && Time.realtimeSinceStartup < timeout) yield return null;
            Check(desk.GetRequestedOrders().Count > 0, "Float order was not requested");
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
            Check(order.TotalPence > 0 && order.GetDenominationPence(1) == 250 && order.TotalChips > 0,
                "Requested float must preserve actual required chip counts");
            Check(pack.TryGetDeadline(out float packTime, out _) &&
                CasinoTaskFeedback.TryGetDeadline(desk.GetComponent<CasinoTableAttention>(), out float stationTime, out _) &&
                stationTime <= packTime, "Cash Desk urgency did not use a real deadline");
            ValidatePackingCapacity(pack);
            ClickRack(pack, 0);
            pack.UndoLastChip();
            Check(pack.PackedChipCount == 0, "Float Undo failed");
            var trayRect = (RectTransform)Get(pack, "floatTrayRect");
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, trayRect.position);
            for (int i = 0; i < 4; i++)
                for (int n = 0; n < order.Counts[i]; n++) ClickRack(pack, i);
            var carrier = player.GetComponent<CasinoPlayerFloatCarrier>();
            Check(carrier.IsCarrying, "PACK did not produce carried float");
            yield return null;
            Check(player.GetComponentInChildren<PixelCharacterVisual>().CurrentPose == CasinoAnimationPose.Carry,
                "Idle carrying did not hold the tray");
            MoveNear(order.Destination.transform, order.Destination.InteractionZone.IsPlayerInRange);
            yield return Press(UnityEngine.InputSystem.Key.E);
            Check(!carrier.IsCarrying && (bool)Get(shift, "delivered"), "Float delivery failed");
            while (shift.IsLessonOpen) yield return null;
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
            yield return Press(UnityEngine.InputSystem.Key.Escape);
            while (shift.IsLessonOpen) yield return null;
            yield return null;
            Set(blackjack.AssignedDealer, "energy", 100f);
            yield return null;
            Check(!blackjack.IsDealerStaffed, "Early cover made protected manual Blackjack practice impossible");
            MoveNear(blackjack.transform, blackjack.IsPlayerInRange);
            yield return Press(UnityEngine.InputSystem.Key.E);
            Check(blackjack.BeginPlayerTakeover() && blackjack.IsPlayerOperating && !blackjack.IsDealerStaffed,
                "Exhausted Blackjack could not be operated by the pit boss");
            timeout = Time.realtimeSinceStartup + 50;
            while (!(bool)Get(shift, "manualBlackjackDone") && Time.realtimeSinceStartup < timeout)
            {
                blackjack.ManualDealStep();
                yield return new WaitForSeconds(.31f);
            }
            Check((bool)Get(shift, "manualBlackjackDone"), "Pit boss did not resolve a real Blackjack hand");
            BlackjackTableRound manualRound = blackjack.ActiveRound;
            timeout = Time.realtimeSinceStartup + 20;
            while (blackjack.State == BlackjackTableState.Settlement && Time.realtimeSinceStartup < timeout)
            {
                int payoutIndex = (int)Get(blackjack, "payoutHandIndex");
                if (payoutIndex < 0) { yield return null; continue; }
                BlackjackHand owed = manualRound.CustomerHands[payoutIndex];
                blackjack.ManualDealStep();
                yield return new WaitForSeconds(.31f);
                Check(blackjack.State == BlackjackTableState.Settlement && !owed.PayoutPaid,
                    "Space skipped a manual Blackjack payout");
                Call(blackjack, "AddPayoutChip", owed.WinningsPence == 500 ? 2500 : 500);
                Call(blackjack, "ConfirmPayout");
                Check(!owed.PayoutPaid && blackjack.State == BlackjackTableState.Settlement,
                    "An incorrect Blackjack payout skipped the winner");
                if (!owed.PayoutPaid)
                {
                    blackjack.GetComponent<CasinoFloatRefillDestination>().TryAddChips(new[] { 20, 0, 10, 5 });
                    int penaltiesBeforeOverpay = (int)Get(blackjack, "payoutPenaltyCount");
                    int[] overpayValues = { 500, 250, 2500, 10000 };
                    var overpayArgs = new object[] { owed.WinningsPence, null };
                    Check((bool)Call(blackjack, "TryGetAvailablePayout", overpayArgs), "No chips for overpay test");
                    int[] exactCounts = (int[])overpayArgs[1];
                    for (int i = 0; i < exactCounts.Length; i++)
                        for (int n = 0; n < exactCounts[i]; n++) Call(blackjack, "AddPayoutChip", overpayValues[i]);
                    Call(blackjack, "AddPayoutChip", 500);
                    Check((int)Call(blackjack, "GetSelectedPayoutTotal") > owed.WinningsPence,
                        "Blackjack silently rejected an overpayment chip");
                    long floatBeforeOverpay = blackjack.GetComponent<CasinoFloatRefillDestination>().InventoryValuePence;
                    Call(blackjack, "ConfirmPayout");
                    Check(!owed.PayoutPaid && blackjack.State == BlackjackTableState.Settlement &&
                          (int)Get(blackjack, "payoutPenaltyCount") == penaltiesBeforeOverpay + 1 &&
                          blackjack.GetComponent<CasinoFloatRefillDestination>().InventoryValuePence == floatBeforeOverpay,
                        "An overpayment was accepted or consumed float chips");
                    var payoutArgs = new object[] { owed.WinningsPence, null };
                    if (!(bool)Call(blackjack, "TryGetAvailablePayout", payoutArgs))
                    {
                        blackjack.GetComponent<CasinoFloatRefillDestination>().TryAddChips(new[] { 20, 0, 10, 5 });
                        continue;
                    }
                    int[] values = { 500, 250, 2500, 10000 };
                    int[] counts = (int[])payoutArgs[1];
                    for (int i = 0; i < counts.Length; i++)
                        for (int n = 0; n < counts[i]; n++) Call(blackjack, "AddPayoutChip", values[i]);
                    Call(blackjack, "ConfirmPayout");
                }
                yield return null;
            }
            foreach (BlackjackHand hand in manualRound.CustomerHands)
                Check(hand.PayoutPence == 0 || hand.PayoutPaid, "Manual Blackjack left an unpaid winner");
            yield return ValidateTipping(blackjack);
            yield return Press(UnityEngine.InputSystem.Key.Escape);
            Check(!blackjack.IsPlayerOperating, "ESC retained manual Blackjack ownership");
            while (shift.IsLessonOpen) yield return null;
            Set(roulette.AssignedDealer, "energy", 100f);
            yield return null;
            Check(!roulette.IsDealerStaffed, "Early cover made protected manual Roulette practice impossible");
            MoveNear(inspection.transform, candidate => inspection.IsPlayerInRange(candidate) &&
                Array.TrueForAll(blackjackTables, table => !table.IsPlayerInRange(candidate)));
            yield return Press(UnityEngine.InputSystem.Key.E);
            Call(inspection, "StartManualRound");
            Check(roulette.IsPlayerOperating && !roulette.IsDealerStaffed, "Pit boss could not take over Roulette");
            timeout = Time.realtimeSinceStartup + 50;
            while (roulette.Phase != RouletteRoundPhase.AcceptingBets &&
                roulette.Phase != RouletteRoundPhase.AwaitingAssistance && Time.realtimeSinceStartup < timeout) yield return null;
            Call(inspection, "StartManualRound");
            Check(rgame.IsRunning, $"Manual Roulette did not use the existing round minigame: phase={roulette.Phase}, " +
                $"owner={roulette.IsPlayerOperating}, open={inspection.IsOpen}, pending={roulette.HasPendingAssistance}, " +
                $"canBegin={rgame.CanBegin}, bets={roulette.AssistanceBets.Count}, other={CasinoPlayerInteractionRouter.IsAnyInspectionOpen()}, " +
                $"range={roulette.GetComponent<CasinoMinigameStation>().IsPlayerInRange(player.transform)}");
            Call(rgame, "BeginSpin");
            yield return new WaitForSeconds(3);
            IList chips = (IList)Get(rgame, "sweepChips");
            var tray = (RectTransform)Get(rgame, "trayHitArea");
            Vector2 trayPoint = RectTransformUtility.WorldToScreenPoint(null, tray.position);
            foreach (object chip in new ArrayList(chips))
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
                Check(bet != null, "Manual Roulette PAY stage did not open");
                int[] counts = RouletteTableMinigame.GetPayoutComposition((int)bet.WinningsPence);
                for (int i = 0; i < counts.Length; i++)
                    for (int n = 0; n < counts[i]; n++) Call(rgame, "AddPayoutChips", i);
                Call(rgame, "ConfirmPayout");
                yield return new WaitForSecondsRealtime(.7f);
            }
            Check((bool)Get(shift, "manualRouletteDone"), "Manual Roulette did not settle actual bets");
            MoveNear(pit.transform, pit.IsPlayerInRange);
            yield return Press(UnityEngine.InputSystem.Key.E);
            foreach (CasinoDealerStation targetStation in manager.Stations)
            {
                if (targetStation.BlackjackTable != blackjack && targetStation.GetComponent<RouletteRoundController>() != roulette) continue;
                timeout = Time.realtimeSinceStartup + manager.FullRestThreshold / 1.5f + 40;
                readyIndex = -1;
                while (readyIndex < 0 && Time.realtimeSinceStartup < timeout)
                {
                    for (int i = 0; i < manager.Dealers.Count; i++)
                        if (manager.Dealers[i].IsReadyAtPitDesk)
                        {
                            if (manager.Dealers[i].Energy > 0 &&
                                manager.CanAssignDealer(manager.Dealers[i], targetStation, out _))
                            {
                                readyIndex = i;
                                break;
                            }

                            if (manager.Dealers[i].Energy <= 0)
                            {
                                Check(!manager.CanAssignDealer(manager.Dealers[i], targetStation, out string exhaustedReason) &&
                                    exhaustedReason.Contains("break"), "Exhausted off-duty staff could be assigned back to a table");
                                Call(pit, "SendDealerOnBreak", i);
                            }
                        }
                    if (readyIndex < 0) yield return null;
                }
                string staffSnapshot = "";
                foreach (AIDealer dealer in manager.Dealers)
                    staffSnapshot += $"{dealer.DealerId}:{dealer.State}:{dealer.Energy:0.0} ";
                Check(readyIndex >= 0, $"No rested dealer available for manual handoff (scale {Time.timeScale}, {staffSnapshot})");
                Call(pit, "SelectDealer", readyIndex);
                Call(pit, "AssignSelectedDealer", Array.IndexOf(new System.Collections.Generic.List<CasinoDealerStation>(manager.Stations).ToArray(), targetStation));
                timeout = Time.realtimeSinceStartup + 60;
                AIDealer assignedCover = manager.Dealers[readyIndex];
                while (targetStation.AssignedDealer == null || !targetStation.AssignedDealer.CanServe)
                {
                    Check(Time.realtimeSinceStartup < timeout,
                        $"Rested cover did not resume {targetStation.StationName}; " +
                        $"assigned={targetStation.AssignedDealer?.DealerId ?? "none"}, " +
                        $"pending={targetStation.PendingReplacement?.DealerId ?? "none"}, " +
                        $"cover={assignedCover.DealerId}:{assignedCover.State}, " +
                        $"pathFailed={assignedCover.HasReportedPathFailure}, " +
                        $"destination={assignedCover.Destination?.name ?? "none"}");
                    yield return null;
                }
            }
            yield return Press(UnityEngine.InputSystem.Key.Escape);
            timeout = Time.realtimeSinceStartup + 10;
            while (PlayerPrefs.GetInt("PitPanic.TutorialComplete", 0) != 1 && Time.realtimeSinceStartup < timeout) yield return null;
            Check(PlayerPrefs.GetInt("PitPanic.TutorialComplete", 0) == 1, "Expanded tutorial did not complete");
            Check(shift.RemainingSeconds >= 179, "Training left no time for normal gameplay");
            Debug.Log($"SHIFT GOAL TEST: tasks={Get(shift, "completedTasks")}");
            Check((int)Get(shift, "completedTasks") >= 4, "Training did not complete real service tasks");
            Set(shift, "completedTasks", 26);
            yield return null;
            string taskCounts = ((Text)Get(shift, "objectiveText")).text;
            Check(taskCounts.Contains("26") && taskCounts.Contains("MISSED") && !taskCounts.Contains("/4"),
                "Shift HUD still shows a meaningless help target fraction");
            // Earlier fixtures deliberately cancel/time out tasks; isolate the successful-result case.
            Set(shift, "failedAttempts", 0);
            MoveNear(pit.transform, pit.IsPlayerInRange);
            yield return Press(UnityEngine.InputSystem.Key.E);
            Check(pit.IsOpen, "Pit Desk did not reopen before the shift-end handoff");
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
            Check(shift.PassedShift && CasinoDemoProgression.BestStars(1) > 0,
                "Cleared level did not persist its star rating");
            int bestStars = CasinoDemoProgression.BestStars(1);
            Call(shift, "FinishShift");
            Check(CasinoDemoProgression.BestStars(1) == bestStars, "Repeated results awarded stars more than once");
            results.transform.Find("Next shift").GetComponent<Button>().onClick.Invoke();
            levelSelect = (GameObject)Get(shift, "levelSelect");
            Check(levelSelect.activeSelf && !results.activeSelf, "Results did not lead to campaign level selection");
            levelSelect.transform.Find("Level 2").GetComponent<Button>().onClick.Invoke();
            GameObject intro2 = (GameObject)Get(shift, "intro");
            Check(intro2.activeSelf && ((Text)Get(shift, "introTitle")).text == "HIGH ROLLERS",
                "Unlocked High Rollers level did not show its briefing");
            intro2.transform.Find("Begin").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            shift = FindFirstObjectByType<CasinoShiftController>();
            spawner = FindFirstObjectByType<CasinoCustomerSpawner>();
            Check(shift != null && shift.IsRunning && shift.CurrentShift == 2, "High Rollers did not start");
            Check(Mathf.Approximately(spawner.PatienceSeconds, 50f) &&
                Mathf.Approximately(spawner.VipSpawnChance, .35f) &&
                CasinoCampaignPresentation.LayoutsAreSeparated(),
                "High Rollers difficulty or campaign layout was not applied");
            CasinoCustomerAI vip = Array.Find(FindObjectsByType<CasinoCustomerAI>(FindObjectsSortMode.None),
                customer => customer != null && customer.IsVip);
            if (vip == null) vip = (CasinoCustomerAI)Call(spawner, "SpawnCustomer", 0);
            Check(vip != null && vip.IsVip && vip.GetComponentInChildren<PixelCharacterVisual>().IsVip,
                "High Rollers did not create an animated VIP customer");
            Check(vip.TryChooseBetPence(new[] { 1000 }, out int vipStake) && vipStake == 10000,
                "VIP betting limits did not support the larger high-roller stake");
            CasinoDemoProgression.AwardShift(2, 1);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            yield return null;
            yield return null;
            shift = FindFirstObjectByType<CasinoShiftController>();
            menu = (GameObject)Get(shift, "menu");
            var continueButton = menu.transform.Find("Continue");
            Check(continueButton != null, "Saved continue button missing");
            continueButton.GetComponent<Button>().onClick.Invoke();
            levelSelect = (GameObject)Get(shift, "levelSelect");
            Check(levelSelect.activeSelf && !shift.IsRunning, "Continue did not open the saved campaign level selection");
            levelSelect.transform.Find("Level 3").GetComponent<Button>().onClick.Invoke();
            GameObject intro3 = (GameObject)Get(shift, "intro");
            Check(((Text)Get(shift, "introTitle")).text == "SHADY BUSINESS",
                "Shady Business did not unlock after High Rollers");
            intro3.transform.Find("Begin").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            shift = FindFirstObjectByType<CasinoShiftController>();
            player = FindFirstObjectByType<CasinoPlayerController>();
            Check(shift.IsRunning && shift.CurrentShift == 3 && !(bool)Get(shift, "tutorial") &&
                shift.IsLessonOpen && ((Text)Get(shift, "lessonTitle")).text.Contains("SHADY BUSINESS"),
                "Continue did not restore Shady Business or show its Tiger briefing");
            ((GameObject)Get(shift, "lessonPanel")).transform.Find("Tiger's lesson/Practise")
                .GetComponent<Button>().onClick.Invoke();
            roulette = FindFirstObjectByType<RouletteRoundController>();
            var actualBets = (List<RouletteCustomerBet>)Get(roulette, "currentBets");
            actualBets.Add(new RouletteCustomerBet(3, 0, null,
                new RouletteBet(RouletteBetType.Red, -1, 500)));
            Set(roulette, "activeRoundId", 3L);
            Call(roulette, "RecordSecurityEvidence", 1);
            pit = FindFirstObjectByType<PitDeskInteraction>();
            Check(pit.PendingSecurityCases == 1 &&
                CasinoTaskFeedback.Message(pit.GetComponent<CasinoTableAttention>()) == "Review CCTV",
                "A real Roulette round did not surface a security review at the Pit Desk");
            MoveNear(pit.transform, pit.IsPlayerInRange);
            yield return Press(UnityEngine.InputSystem.Key.E);
            Check(pit.IsOpen && ((Button)Get(pit, "reviewButton")).gameObject.activeSelf,
                "CCTV alert was not actionable from the Pit Desk");
            ((Button)Get(pit, "reviewButton")).onClick.Invoke();
            GameObject reviewPanel = (GameObject)Get(pit, "reviewPanel");
            Check(reviewPanel.activeSelf && Time.timeScale == 0 &&
                reviewPanel.GetComponentInChildren<CasinoSecurityReplay>() != null &&
                ((Text)Get(pit, "footageStatus")).text == "DID THE BET MOVE AFTER THE RESULT?",
                "CCTV review did not pause play and show the actual round result");
            ValidatePopup(reviewPanel.transform);
            yield return Press(UnityEngine.InputSystem.Key.Escape);
            Check(!reviewPanel.activeSelf && pit.PendingSecurityCases == 1 && Time.timeScale > 0,
                "Cancelling CCTV review lost the alert or failed to restore play");
            int tasksBeforeReview = (int)Get(shift, "completedTasks");
            ((Button)Get(pit, "reviewButton")).onClick.Invoke();
            reviewPanel.transform.Find("CCTV Review Card/Clear Customer").GetComponent<Button>().onClick.Invoke();
            Check(reviewPanel.activeSelf && pit.PendingSecurityCases == 0 &&
                ((Text)Get(pit, "footageStatus")).text.StartsWith("CORRECT!") &&
                (int)Get(shift, "completedTasks") == tasksBeforeReview + 1,
                "A correct CCTV decision did not resolve and count the security case");
            reviewPanel.transform.Find("CCTV Review Card/Continue CCTV").GetComponent<Button>().onClick.Invoke();
            Check(!reviewPanel.activeSelf && Time.timeScale > 0, "CCTV verdict did not restore play after Okay");
            var cheatingBet = new RouletteCustomerBet(6, 0, null,
                new RouletteBet(RouletteBetType.Black, -1, 500));
            Check(CasinoSecurityEvidence.TryCreate(6, 1, new[] { cheatingBet }, out CasinoSecurityEvidence cheatingCase),
                "Cannot create cheating CCTV regression case");
            Call(pit, "OnSecurityEvidenceRecorded", cheatingCase);
            Call(pit, "OpenReview");
            Set(pit, "reviewElapsed", 5.7f);
            Call(pit, "ToggleReviewPlayback");
            Call(pit, "RenderReviewFrame");
            Transform monitor = reviewPanel.transform.Find("CCTV Review Card/Recorded Roulette Footage");
            Vector2 pausedChip = monitor.Find("Recorded wager").GetComponent<RectTransform>().anchoredPosition;
            yield return new WaitForSecondsRealtime(.2f);
            Check(monitor.Find("Recorded wager").GetComponent<RectTransform>().anchoredPosition == pausedChip &&
                  monitor.Find("Wheel result").GetComponent<Text>().text == "1",
                "Pausing CCTV did not freeze the recorded action and actual result");
            Call(pit, "ReplayReview");
            Call(pit, "RenderReviewFrame");
            Check(monitor.Find("Recorded wager").GetComponent<RectTransform>().anchoredPosition ==
                  CasinoSecurityReplay.BetPosition(cheatingBet.Bet),
                "Replaying CCTV did not reset the chip to its original wager");
            int missesBeforeDecision = (int)Get(shift, "failedAttempts");
            Call(pit, "ResolveReview", false);
            Call(pit, "ResolveReview", false);
            Check(((Text)Get(pit, "footageStatus")).text.StartsWith("WRONG!") && reviewPanel.activeSelf &&
                  (int)Get(shift, "failedAttempts") == missesBeforeDecision + 1,
                "Wrong CCTV verdict was hidden or counted more than once");
            reviewPanel.transform.Find("CCTV Review Card/Continue CCTV").GetComponent<Button>().onClick.Invoke();
            yield return Press(UnityEngine.InputSystem.Key.Escape);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            yield return null;
            yield return null;
            shift = FindFirstObjectByType<CasinoShiftController>();
            menu = (GameObject)Get(shift, "menu");
            menu.transform.Find("Start").GetComponent<Button>().onClick.Invoke();
            levelSelect = (GameObject)Get(shift, "levelSelect");
            levelSelect.transform.Find("Level 1").GetComponent<Button>().onClick.Invoke();
            Check(CasinoDemoProgression.UnlockedShift == 1 &&
                CasinoDemoProgression.BestStars(1) == 0 &&
                CasinoDemoProgression.BestStars(2) == 0 &&
                CasinoDemoProgression.BestStars(3) == 0,
                "New Game did not reset campaign unlocks and star records");
            ((GameObject)Get(shift, "intro")).transform.Find("Skip").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            shift = FindFirstObjectByType<CasinoShiftController>();
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
            yield return new WaitForSeconds(.9f);
            PixelCharacterVisual seatedVisual = servedCustomer.GetComponentInChildren<PixelCharacterVisual>();
            Check(Vector3.Distance(seatedVisual.transform.position, servedCustomer.Seat.SittingPosition) < .03f &&
                seatedVisual.transform.localScale == Vector3.one &&
                seatedVisual.GetComponentInChildren<SpriteRenderer>().transform.localScale == Vector3.one &&
                !servedCustomer.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled,
                "Seated customer does not align with the chair or is being squashed instead of frame animated");
            PixelCharacterVisual playerVisual = player.GetComponentInChildren<PixelCharacterVisual>();
            playerVisual.PlayAction(CasinoAnimationPose.Deal, player.transform.position + Vector3.back, 1f);
            yield return new WaitForSeconds(.3f);
            int earlyFrame = playerVisual.CurrentFrame;
            yield return new WaitForSeconds(.3f);
            Check(playerVisual.CurrentPose == CasinoAnimationPose.Deal && playerVisual.CurrentFrame > earlyFrame,
                "Action did not advance through its drawn frame sequence");
            AIDealer exhaustedDealer = exhaustedTable.AssignedDealer;
            float oldEnergy = exhaustedDealer.Energy;
            Set(exhaustedDealer, "energy", 0f);
            yield return null;
            Check(!exhaustedDealer.CanServe && !exhaustedTable.IsDealerStaffed,
                "Zero energy dealer still serves the table");
            yield return null;
            Check(exhaustedDealer.GetComponent<CasinoSpeechBubble>().Message == "Zzz",
                "An exhausted dealer is missing the sleeping speech bubble");
            MoveNear(exhaustedTable.transform, exhaustedTable.IsPlayerInRange);
            yield return null;
            yield return null;
            Check(player.GetComponent<CasinoSpeechBubble>().Message == "Dealer needs break",
                "The player is missing a short task speech bubble near the exhausted table");
            CustomerPatience patience = servedCustomer.GetComponent<CustomerPatience>();
            Check(patience.IsWaiting, "Exhausted dealer did not restart customer patience");
            int penalties = CustomerSatisfaction.PenaltyCount;
            int missesBeforeTimeout = (int)Get(shift, "failedAttempts");
            patience.Begin(1);
            Check(CasinoTaskFeedback.TryGetDeadline(exhaustedTable.GetComponent<CasinoTableAttention>(),
                out float realDeadline, out _) && realDeadline <= 1 &&
                !CasinoTaskFeedback.TryGetDeadline(exhaustedTable.GetComponent<CasinoTableAttention>(),
                    TableAttentionReason.TableUnstaffed, out _, out _),
                "Urgency did not expose customer patience or assigned a fake staffing deadline");
            yield return new WaitForSecondsRealtime(1.2f);
            Check(CustomerSatisfaction.PenaltyCount > penalties && !servedCustomer.IsSeated,
                "Neglected exhausted table did not lose its customer and satisfaction");
            Check((int)Get(shift, "failedAttempts") > missesBeforeTimeout,
                "Customer patience expiry did not immediately increment MISSED");
            Check(servedCustomer.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled,
                "Customer did not resume navigation after leaving their chair");
            Check(CasinoTaskFeedback.Message(exhaustedTable.GetComponent<CasinoTableAttention>()) == "Dealer needs break",
                "Exhausted station did not provide the short speech-bubble cue");
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
            Check(CasinoSoundscape.CurrentMusicTrack == CasinoMusicTrack.Title,
                "Returning to the main menu did not restore welcome music");
            Check(CasinoSoundscape.Focus == menu.transform, "Main Menu retained old task audio focus");
            menu.transform.Find("Continue").GetComponent<Button>().onClick.Invoke();
            levelSelect = (GameObject)Get(shift, "levelSelect");
            Check(levelSelect.activeSelf && !shift.IsRunning,
                "Continue after Main Menu did not open the saved level selection");
            levelSelect.transform.Find("Level 1").GetComponent<Button>().onClick.Invoke();
            ((GameObject)Get(shift, "intro")).transform.Find("Begin").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            shift = FindFirstObjectByType<CasinoShiftController>();
            player = FindFirstObjectByType<CasinoPlayerController>();
            Check(shift.IsRunning && !(bool)Get(shift, "tutorial") &&
                !AudioListener.pause && Time.timeScale > 0,
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
            blackjack.QueueShuffleAssistanceTest();
            FindFirstObjectByType<CasinoCustomerSpawner>().TrySpawnGuidedCustomer(CasinoCustomerGame.Blackjack1);
            timeout = Time.realtimeSinceStartup + 60;
            while (!blackjack.IsShuffleReady && Time.realtimeSinceStartup < timeout) yield return null;
            Check(blackjack.IsShuffleReady, "Menu-return task did not reach a safe deal boundary");
            yield return Press(UnityEngine.InputSystem.Key.E);
            ((Button)Get(Get(blackjack, "tableView"), "assistanceButton")).onClick.Invoke();
            shuffle = blackjack.GetComponent<BlackjackShuffleMinigame>();
            Check(shuffle.IsRunning, "Could not start menu-return task");
            yield return Press(UnityEngine.InputSystem.Key.P);
            ((GameObject)Get(shift, "pausePanel")).transform.Find("Pause card/Main Menu").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            shift = FindFirstObjectByType<CasinoShiftController>();
            Check(!shift.IsRunning && !CasinoMinigamePresentation.IsOpen && !AudioListener.pause &&
                CasinoSoundscape.Focus == ((GameObject)Get(shift, "menu")).transform,
                "Returning from a paused task leaked its minigame, pause or audio focus");
            ((GameObject)Get(shift, "menu")).transform.Find("Start").GetComponent<Button>().onClick.Invoke();
            levelSelect = (GameObject)Get(shift, "levelSelect");
            levelSelect.transform.Find("Level 1").GetComponent<Button>().onClick.Invoke();
            ((GameObject)Get(shift, "intro")).transform.Find("Begin").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            shift = FindFirstObjectByType<CasinoShiftController>();
            GameObject tutorialCard = ((GameObject)Get(shift, "lessonPanel")).transform.Find("Tiger's lesson").gameObject;
            tutorialCard.transform.Find("Skip training").GetComponent<Button>().onClick.Invoke();
            Check(!shift.IsLessonOpen && PlayerPrefs.GetInt("PitPanic.TutorialComplete", 0) == 1,
                "SKIP TRAINING did not close the lesson and complete the tutorial");
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
