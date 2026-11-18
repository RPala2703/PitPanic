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

[InitializeOnLoad]
public static class PenguinDirectionalWalkPlayModeValidation
{
    private const string Key = "PitPanic.PenguinDirectionalWalkValidation";
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly string[] WalkDirections = { "south", "east", "north", "west" };
    private const string PlayerArtPath = "Assets/Art/ApprovedCasino/Characters/Player";

    static PenguinDirectionalWalkPlayModeValidation() => EditorApplication.update += StartTest;

    public static void ImportAndConfigure()
    {
        AssetDatabase.Refresh();
        Sprite[] walk = new Sprite[WalkDirections.Length * 4];
        for (int direction = 0; direction < WalkDirections.Length; direction++)
        for (int frame = 0; frame < 4; frame++)
            walk[direction * 4 + frame] = ImportPlayerSprite($"{PlayerArtPath}/{WalkSpriteName(direction, frame)}.png");

        const string prefabPath = "Assets/Prefabs/Characters/Character_PenguinPixel.prefab";
        GameObject prefab = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            PixelCharacterVisual visual = prefab.GetComponent<PixelCharacterVisual>();
            if (visual == null)
                throw new InvalidOperationException("The Penguin player prefab has no PixelCharacterVisual.");
            SerializedObject serialized = new SerializedObject(visual);
            SerializedProperty walkFrames = serialized.FindProperty("playerWalkFrames");
            if (walkFrames == null)
                throw new InvalidOperationException("PixelCharacterVisual is missing playerWalkFrames.");
            walkFrames.arraySize = walk.Length;
            for (int i = 0; i < walk.Length; i++)
                walkFrames.GetArrayElementAtIndex(i).objectReferenceValue = walk[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefab);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("PENGUIN WALK ART IMPORTED: 16 full 256x256 frames, point-filtered at 256 PPU with shared bottom-center pivots. Existing idle poses were not changed.");
    }

    private static string WalkSpriteName(int direction, int frame) =>
        $"penguin-walk-{WalkDirections[direction]}-{frame + 1:00}";

    private static Sprite ImportPlayerSprite(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new FileNotFoundException("Missing Penguin walk frame.", path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePivot = new Vector2(.5f, 0);
        importer.spritePixelsPerUnit = 256;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.isReadable = true;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spritePixelsPerUnit = 256;
        settings.spritePivot = new Vector2(.5f, 0);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null || sprite.rect != new Rect(0, 0, 256, 256) ||
            sprite.pixelsPerUnit != 256 || sprite.pivot != new Vector2(128, 0))
            throw new InvalidOperationException($"Could not import the full 256x256 Penguin walk frame from {path}.");
        return sprite;
    }

    public static void Run()
    {
        if (Application.isBatchMode)
            throw new InvalidOperationException("Run the Penguin directional validation in a visible Editor for live Play Mode captures.");
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
        new GameObject("Penguin directional walk validation").AddComponent<Driver>();
    }

    private static object Get(object target, string name) =>
        target.GetType().GetField(name, Flags).GetValue(target);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public sealed class Driver : MonoBehaviour
    {
        private readonly List<InputDevice> disabledDevices = new List<InputDevice>();
        private readonly Dictionary<Collider, bool> obstacleColliderStates =
            new Dictionary<Collider, bool>();
        private Keyboard keyboard;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
        private Exception failure;
        private string directory;
        private readonly List<string> capturedFiles = new List<string>();
        private Camera sceneCamera;
        private Vector3 cameraStartPosition;
        private float cameraStartOrthographicSize;

        private void Start()
        {
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
            directory = Environment.GetEnvironmentVariable("PIT_PANIC_PENGUIN_EVIDENCE");
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("Set PIT_PANIC_PENGUIN_EVIDENCE.");
            Directory.CreateDirectory(directory);
            foreach (InputDevice device in InputSystem.devices)
                if (device.enabled && (device is Keyboard || device is Mouse))
                {
                    disabledDevices.Add(device);
                    InputSystem.DisableDevice(device);
                }
            keyboard = InputSystem.AddDevice<Keyboard>();
            oldBackground = InputSystem.settings.backgroundBehavior;
            oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            sceneCamera = Camera.main;
            Check(sceneCamera != null && sceneCamera.orthographic,
                "Directional art validation requires the live orthographic casino camera.");
            cameraStartPosition = sceneCamera.transform.position;
            cameraStartOrthographicSize = sceneCamera.orthographicSize;
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
            while (tests.Count > 0)
            {
                object current = null;
                bool moved;
                try
                {
                    if (failure != null) throw failure;
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
            Debug.Log("PENGUIN WALK CYCLE PLAY MODE PASSED: all four directional idles and 01/02/03/04 walk cycles, 0.2s phase holds, projected aspect, zoom/full-floor captures, real interaction and final-zero freeze.");
            Restore();
            EditorApplication.Exit(0);
        }

        private IEnumerator Test()
        {
            yield return null;
            if (failure != null) throw failure;
            CasinoShiftController shift = FindFirstObjectByType<CasinoShiftController>();
            Check(shift != null, "The playable scene did not create its shift controller.");
            typeof(CasinoShiftController).GetMethod("BeginShift", Flags).Invoke(shift, new object[] { 1, false });
            shift.DismissLesson();
            FindFirstObjectByType<CasinoCustomerSpawner>().SetAcceptingArrivals(false);

            CasinoPlayerController player = FindFirstObjectByType<CasinoPlayerController>();
            DisableTestOnlyMovementObstacles(player);
            player.transform.position = new Vector3(-6f, player.transform.position.y, -3f);
            Physics.SyncTransforms();
            yield return null;
            yield return null;
            PixelCharacterVisual visual = player.GetComponentInChildren<PixelCharacterVisual>();
            Check(visual != null && visual.UsesPlayerDirectionalArt && visual.UsesPlayerWalkArt,
                "Actual player is not wired to four directional idles and sixteen directional walk frames.");
            UnityEngine.InputSystem.Key[] keys =
            {
                UnityEngine.InputSystem.Key.S,
                UnityEngine.InputSystem.Key.D,
                UnityEngine.InputSystem.Key.W,
                UnityEngine.InputSystem.Key.A
            };
            string[] names = { "Front", "Right", "Back", "Left" };
            for (int i = 0; i < keys.Length; i++)
            {
                Vector3 directionStart = player.transform.position;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys[i]));
                float deadline = Time.realtimeSinceStartup + 3;
                while ((!visual.IsPlayerWalking || visual.PlayerFacingName != names[i] ||
                        visual.PlayerWalkFrameIndex != i * 4 ||
                        Vector2.Distance(new Vector2(directionStart.x, directionStart.z),
                            new Vector2(player.transform.position.x, player.transform.position.z)) < .12f) &&
                       Time.realtimeSinceStartup < deadline)
                {
                    if (failure != null) throw failure;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys[i]));
                    yield return null;
                }
                Check(Vector2.Distance(new Vector2(directionStart.x, directionStart.z),
                          new Vector2(player.transform.position.x, player.transform.position.z)) >= .12f &&
                      visual.IsPlayerWalking && visual.PlayerFacingName == names[i] &&
                      visual.PlayerWalkFrameIndex == i * 4 &&
                      visual.Renderer.sprite.name == WalkSpriteName(i, 0),
                    $"Movement {keys[i]} did not select the first {names[i]} walk frame: " +
                    $"{visual.PlayerFacingName}/{visual.Renderer.sprite.name}, " +
                    $"start={directionStart}, now={player.transform.position}, forward={player.transform.forward}, " +
                    $"movementEnabled={Get(player, "movementEnabled")}, frozen={CasinoShiftController.GameplayFrozen}, " +
                    $"pressed={keyboard[keys[i]].isPressed}, timeScale={Time.timeScale:0.00}.");
                ApprovedArtPlayModeValidation.ValidateActor(visual);
                float[] phaseDurations = new float[4];
                float currentPhaseStarted = Time.realtimeSinceStartup;
                for (int transition = 1; transition <= 5; transition++)
                {
                    int previousPhase = (transition - 1) % 4;
                    int nextPhase = transition % 4;
                    float phaseDeadline = currentPhaseStarted + .6f;
                    while (visual.PlayerWalkFrameIndex == i * 4 + previousPhase &&
                           Time.realtimeSinceStartup < phaseDeadline)
                    {
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys[i]));
                        yield return null;
                    }

                    float phaseDuration = Time.realtimeSinceStartup - currentPhaseStarted;
                    string expectedSprite = WalkSpriteName(i, nextPhase);
                    Check(visual.PlayerWalkFrameIndex == i * 4 + nextPhase &&
                          visual.Renderer.sprite.name == expectedSprite &&
                          phaseDuration >= (transition == 1 ? .05f : .15f) &&
                          phaseDuration <= .26f,
                        $"{names[i]} walk phase {transition} did not hold/select the expected pose " +
                        $"{expectedSprite} for about 0.2s: {phaseDuration:0.000}s, " +
                        $"{visual.PlayerWalkFrameIndex}/{visual.Renderer.sprite.name}.");
                    phaseDurations[(transition - 1) % 4] = phaseDuration;
                    currentPhaseStarted = Time.realtimeSinceStartup;
                    ApprovedArtPlayModeValidation.ValidateActor(visual);
                    ValidatePlayerScreenAspect(visual.Renderer.sprite, visual.Renderer.transform, sceneCamera);
                }
                Check(phaseDurations.Average() >= .18f && phaseDurations.Average() <= .22f,
                    $"{names[i]} four-beat walk cycle averaged {phaseDurations.Average():0.000}s per pose.");

                yield return Capture($"floor-{WalkDirections[i]}-walk-02");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                deadline = Time.realtimeSinceStartup + 3;
                while (visual.IsPlayerWalking && Time.realtimeSinceStartup < deadline) yield return null;
                Check(!visual.IsPlayerWalking && visual.Renderer.sprite == visual.SelectedStandingArt &&
                      visual.Renderer.sprite.name == $"Penguin_Idle_{names[i]}",
                    $"{names[i]} did not immediately return to its directional idle pose on stop.");
                Vector3 stoppedAt = player.transform.position;
                yield return new WaitForSecondsRealtime(.15f);
                Check(Vector3.Distance(stoppedAt, player.transform.position) < .05f,
                    $"{names[i]} player continued drifting after the idle pose returned.");
                ApprovedArtPlayModeValidation.ValidateActor(visual);
                yield return new WaitForEndOfFrame();
                ValidatePlayerScreenAspect(visual.Renderer.sprite, visual.Renderer.transform, sceneCamera);
                yield return Capture($"floor-{names[i].ToLowerInvariant()}-idle");
                if (i == 0) yield return CaptureZoomedAppearance(player.transform);
                Debug.Log($"PENGUIN {WalkDirections[i].ToUpperInvariant()} 01/02/03/04/01 CYCLE PASSED: " +
                    $"{string.Join("/", phaseDurations.Select(duration => $"{duration:0.000}s"))}; " +
                    $"idle={visual.Renderer.sprite.name}, position={player.transform.position}");
            }

            yield return ValidateDiagonalFacing(player, visual);
            RestoreMovementObstacles();
            PitDeskInteraction pitDesk = FindFirstObjectByType<PitDeskInteraction>();
            Vector3 interactionPosition = FindDeskInteractionPoint(pitDesk, player.transform);
            player.transform.position = interactionPosition;
            Physics.SyncTransforms();
            Check(pitDesk.IsPlayerInRange(player.transform), "Could not place the real player inside the Pit Desk interaction range.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.E));
            float interactionDeadline = Time.realtimeSinceStartup + 2;
            while (!pitDesk.IsOpen && Time.realtimeSinceStartup < interactionDeadline) yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Check(pitDesk.IsOpen, "Pressing E in range did not open the real Pit Desk interaction.");
            Debug.Log("PENGUIN PLAYER INTERACTION PASSED: in-range E opened the Pit Desk.");
            typeof(PitDeskInteraction).GetMethod("Close", Flags).Invoke(pitDesk, null);
            yield return null;

            typeof(CasinoShiftController).GetField("remaining", Flags).SetValue(shift, .15f);
            float freezeDeadline = Time.realtimeSinceStartup + 2;
            while (!CasinoShiftController.GameplayFrozen && Time.realtimeSinceStartup < freezeDeadline)
                yield return null;
            Check(CasinoShiftController.GameplayFrozen && Time.timeScale == 0,
                "The existing final-zero flow did not freeze the live game.");
            Sprite frozenSprite = visual.Renderer.sprite;
            Vector3 frozenPosition = player.transform.position;
            yield return Capture("penguin-time-up");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.D));
            yield return new WaitForSecondsRealtime(.35f);
            Check(player.transform.position == frozenPosition && visual.Renderer.sprite == frozenSprite,
                "Player movement or facing changed after the final-zero freeze.");
            Debug.Log("PENGUIN ZERO FREEZE PASSED: movement/facing remained fixed while TIME'S UP! held.");
            yield return VerifyCapturedFiles();
            Check(failure == null, failure != null ? failure.Message : "");
        }

        private IEnumerator ValidateDiagonalFacing(CasinoPlayerController player, PixelCharacterVisual visual)
        {
            UnityEngine.InputSystem.Key w = UnityEngine.InputSystem.Key.W;
            UnityEngine.InputSystem.Key d = UnityEngine.InputSystem.Key.D;
            yield return HoldFacing(visual, "Back", "north", .1f, w);
            Vector3 start = player.transform.position;
            yield return HoldFacing(visual, "Back", "north", .85f, w, d);
            Vector3 travel = player.transform.position - start;
            Check(travel.x > .5f && travel.z > .5f && Mathf.Abs(travel.x - travel.z) < .05f,
                $"Holding W+D no longer moves diagonally: {travel}.");
            yield return HoldFacing(visual, "Right", "east", .1f, d);
            yield return HoldFacing(visual, "Right", "east", .85f, w, d);
            yield return HoldFacing(visual, "Back", "north", .1f, w);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
            Check(!visual.IsPlayerWalking && visual.Renderer.sprite.name == "Penguin_Idle_Back",
                "Releasing both direction keys did not preserve the last facing for idle.");
            Debug.Log("PENGUIN DIAGONAL FACING PASSED: W+D moved diagonally without switching poses in either key order; releasing one key selected the remaining direction.");
        }

        private IEnumerator HoldFacing(PixelCharacterVisual visual, string expectedFacing, string expectedDirection, float duration,
            params UnityEngine.InputSystem.Key[] held)
        {
            KeyboardState state = new KeyboardState(held);
            float deadline = Time.realtimeSinceStartup + 1f;
            do
            {
                InputSystem.QueueStateEvent(keyboard, state);
                yield return null;
            } while ((!visual.IsPlayerWalking || !held.All(key => keyboard[key].isPressed)) &&
                     Time.realtimeSinceStartup < deadline);
            // The input state may be visible before the controller's next Update.
            InputSystem.QueueStateEvent(keyboard, state);
            yield return null;
            float until = Time.realtimeSinceStartup + duration;
            do
            {
                string spriteName = visual.Renderer.sprite.name;
                Check(visual.IsPlayerWalking && visual.PlayerFacingName == expectedFacing &&
                      spriteName.StartsWith($"penguin-walk-{expectedDirection}-", StringComparison.Ordinal),
                    $"Holding {string.Join("+", held)} switched away from {expectedFacing}: {spriteName}.");
                InputSystem.QueueStateEvent(keyboard, state);
                yield return null;
            } while (Time.realtimeSinceStartup < until);
        }

        private static void ValidatePlayerScreenAspect(Sprite sprite, Transform spriteTransform, Camera camera)
        {
            Color32[] pixels = sprite.texture.GetPixels32();
            int textureWidth = sprite.texture.width;
            int left = Mathf.RoundToInt(sprite.rect.x);
            int bottom = Mathf.RoundToInt(sprite.rect.y);
            int width = Mathf.RoundToInt(sprite.rect.width);
            int height = Mathf.RoundToInt(sprite.rect.height);
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (pixels[(bottom + y) * textureWidth + left + x].a <= 127) continue;
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }
            Check(maxX >= minX && maxY >= minY, $"Sprite {sprite.name} has no opaque artwork.");
            float sourceAspect = (maxX - minX + 1f) / (maxY - minY + 1f);
            float ppu = sprite.pixelsPerUnit;
            Vector3[] sourceCorners =
            {
                new Vector3((minX - sprite.pivot.x) / ppu, (minY - sprite.pivot.y) / ppu, 0),
                new Vector3((maxX + 1 - sprite.pivot.x) / ppu, (minY - sprite.pivot.y) / ppu, 0),
                new Vector3((minX - sprite.pivot.x) / ppu, (maxY + 1 - sprite.pivot.y) / ppu, 0),
                new Vector3((maxX + 1 - sprite.pivot.x) / ppu, (maxY + 1 - sprite.pivot.y) / ppu, 0)
            };
            Vector3 first = camera.WorldToScreenPoint(spriteTransform.TransformPoint(sourceCorners[0]));
            float screenMinX = first.x, screenMaxX = first.x;
            float screenMinY = first.y, screenMaxY = first.y;
            for (int i = 1; i < sourceCorners.Length; i++)
            {
                Vector3 projected = camera.WorldToScreenPoint(spriteTransform.TransformPoint(sourceCorners[i]));
                screenMinX = Mathf.Min(screenMinX, projected.x);
                screenMaxX = Mathf.Max(screenMaxX, projected.x);
                screenMinY = Mathf.Min(screenMinY, projected.y);
                screenMaxY = Mathf.Max(screenMaxY, projected.y);
            }
            float screenWidth = screenMaxX - screenMinX;
            float screenHeight = screenMaxY - screenMinY;
            float screenAspect = screenWidth / screenHeight;
            float relativeError = Mathf.Abs(screenAspect / sourceAspect - 1f);
            Transform parent = spriteTransform.parent;
            string scaleDetails = $"local={spriteTransform.localScale}, world={spriteTransform.lossyScale}, " +
                $"parentLocal={(parent != null ? parent.localScale.ToString() : "none")}, " +
                $"parentWorld={(parent != null ? parent.lossyScale.ToString() : "none")}, " +
                $"parentRotation={(parent != null ? parent.rotation.eulerAngles.ToString() : "none")}, " +
                $"spriteRotation={spriteTransform.rotation.eulerAngles}, cameraRotation={camera.transform.rotation.eulerAngles}";
            Check(screenWidth > 1 && screenHeight > 1 && relativeError < .02f,
                $"{sprite.name} is distorted in the camera view: source={sourceAspect:0.000}, " +
                $"screen={screenAspect:0.000} ({screenWidth:0.0}x{screenHeight:0.0}px); {scaleDetails}.");
            Debug.Log($"PENGUIN SCREEN ASPECT {sprite.name}: source={sourceAspect:0.000}, " +
                $"rendered={screenAspect:0.000} ({screenWidth:0.0}x{screenHeight:0.0}px).");
        }

        private IEnumerator CaptureZoomedAppearance(Transform player)
        {
            Vector3 oldPosition = sceneCamera.transform.position;
            float oldSize = sceneCamera.orthographicSize;
            sceneCamera.orthographicSize = 4.5f;
            Vector3 viewport = sceneCamera.WorldToViewportPoint(player.position + Vector3.up * 1.1f);
            sceneCamera.transform.position += sceneCamera.transform.right *
                ((viewport.x - .5f) * 2f * sceneCamera.orthographicSize * sceneCamera.aspect) +
                sceneCamera.transform.up * ((viewport.y - .5f) * 2f * sceneCamera.orthographicSize);
            yield return Capture("zoomed-front-idle");
            sceneCamera.transform.position = oldPosition;
            sceneCamera.orthographicSize = oldSize;
        }

        private static Vector3 FindDeskInteractionPoint(PitDeskInteraction pitDesk, Transform player)
        {
            Check(pitDesk != null && pitDesk.IsConfigured, "Real Pit Desk interaction is not configured.");
            Transform zone = ((TableInteractionZone)Get(pitDesk, "interactionZone")).transform;
            Vector3 center = zone.position;
            BlackjackTableInteraction[] blackjack = FindObjectsByType<BlackjackTableInteraction>(FindObjectsSortMode.None);
            RouletteInspectionView[] roulette = FindObjectsByType<RouletteInspectionView>(FindObjectsSortMode.None);
            for (float radius = 0; radius <= 3; radius += .25f)
            for (int step = 0; step < 16; step++)
            {
                float angle = step * Mathf.PI / 8;
                Vector3 candidate = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                player.position = candidate;
                Physics.SyncTransforms();
                if (!pitDesk.IsPlayerInRange(player) ||
                    blackjack.Any(table => table.IsPlayerInRange(player)) ||
                    roulette.Any(table => table.IsPlayerInRange(player))) continue;
                return candidate;
            }
            throw new InvalidOperationException("No unobstructed player interaction position was found at the Pit Desk.");
        }

        private void DisableTestOnlyMovementObstacles(CasinoPlayerController player)
        {
            bool foundFloorCollider = false;
            foreach (Collider collider in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (!collider.enabled || collider.isTrigger ||
                    collider is CharacterController ||
                    collider.GetComponentInParent<CasinoPlayerController>() == player) continue;

                Bounds bounds = collider.bounds;
                bool floor = bounds.size.x >= 20f && bounds.size.z >= 15f;
                if (floor)
                {
                    foundFloorCollider = true;
                    continue;
                }

                obstacleColliderStates.Add(collider, collider.enabled);
                collider.enabled = false;
            }

            Check(foundFloorCollider,
                "Could not identify the real casino floor collider to isolate Play Mode animation movement.");
            Debug.Log($"PENGUIN TEST FIXTURE: temporarily disabled {obstacleColliderStates.Count} non-floor solid colliders.");
        }

        private void RestoreMovementObstacles()
        {
            foreach (KeyValuePair<Collider, bool> pair in obstacleColliderStates)
                if (pair.Key != null) pair.Key.enabled = pair.Value;
            obstacleColliderStates.Clear();
        }

        private IEnumerator Capture(string name)
        {
            Check(Application.isPlaying, "Directional-art evidence must be captured in Play Mode.");
            string path = Path.Combine(directory, name + ".png");
            if (File.Exists(path)) File.Delete(path);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            capturedFiles.Add(path);
            Debug.Log("PENGUIN PLAY MODE CAPTURE " + path);
        }

        private IEnumerator VerifyCapturedFiles()
        {
            float deadline = Time.realtimeSinceStartup + 8;
            foreach (string path in capturedFiles)
            {
                while ((!File.Exists(path) || new FileInfo(path).Length <= 10000) &&
                       Time.realtimeSinceStartup < deadline) yield return null;
                Check(File.Exists(path) && new FileInfo(path).Length > 10000,
                    "Missing live Play Mode screenshot: " + path);
            }
        }

        private void Restore()
        {
            Application.logMessageReceived -= OnLog;
            if (sceneCamera != null)
            {
                sceneCamera.transform.position = cameraStartPosition;
                sceneCamera.orthographicSize = cameraStartOrthographicSize;
            }
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
            RestoreMovementObstacles();
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
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
    }
}
