using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class ApprovedArtPlayModeValidation
{
    private const string Key = "PitPanic.ApprovedArtValidation";
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly HashSet<Sprite> ValidatedAuras = new HashSet<Sprite>();
    private static readonly CasinoAnimalSpecies[] CustomerSpecies =
    {
        CasinoAnimalSpecies.Rabbit, CasinoAnimalSpecies.Dog, CasinoAnimalSpecies.Cat,
        CasinoAnimalSpecies.Raccoon, CasinoAnimalSpecies.Deer, CasinoAnimalSpecies.Elephant
    };

    static ApprovedArtPlayModeValidation() { EditorApplication.update += StartTest; }

    public static void Run()
    {
        if (Application.isBatchMode)
            throw new InvalidOperationException("Run live ScreenCapture validation with a visible Editor; batch mode does not present Game View frames.");
        foreach (string key in SaveKeys())
            SessionState.SetInt("ApprovedArt.Old." + key, PlayerPrefs.GetInt(key, -1));
        PrepareGameView();
        EditorSceneManager.OpenScene("Assets/Scenes/PitPanicPrototype.unity");
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    internal static void PrepareGameView()
    {
        Assembly assembly = typeof(Editor).Assembly;
        Type sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        object sizes = sizesType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static |
            BindingFlags.FlattenHierarchy).GetValue(null);
        Type groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
        object group = sizesType.GetMethod("GetGroup").Invoke(sizes,
            new[] { Enum.Parse(groupType, "Standalone") });
        Type sizeType = assembly.GetType("UnityEditor.GameViewSize");
        Type resolutionType = assembly.GetType("UnityEditor.GameViewSizeType");
        object size = Activator.CreateInstance(sizeType, Enum.Parse(resolutionType, "FixedResolution"),
            1920, 1080, "Approved art live evidence");
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
        int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
        Type gameViewType = assembly.GetType("UnityEditor.GameView");
        EditorWindow window = EditorWindow.GetWindow(gameViewType);
        gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance).SetValue(window, index);
        window.Show();
        window.Focus();
    }

    internal static IEnumerable<string> SaveKeys()
    {
        yield return CasinoDemoProgression.UnlockedLevelKey;
        yield return CasinoDemoProgression.CreditsKey;
        yield return "PitPanic.TutorialComplete";
        foreach (string key in CasinoDemoProgression.UpgradeKeys) yield return key;
        for (int level = 1; level <= 3; level++) yield return CasinoDemoProgression.BestStarsKey(level);
        foreach (string key in CasinoHandbookProgress.AllKeys()) yield return key;
    }

    private static void StartTest()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
        SessionState.SetBool(Key, false);
        new GameObject("Approved real-game art validation").AddComponent<Driver>();
    }

    private static object Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, Flags).Invoke(target, args);

    private static void Check(bool value, string reason)
    {
        if (!value) throw new InvalidOperationException(reason);
    }

    public static void ValidateActor(PixelCharacterVisual pixel)
    {
        Check(Application.isPlaying, "Art validation must inspect the running game.");
        CasinoAnimalSpecies species = pixel.GetComponent<CasinoCharacterVisual>().Species;
        SpriteRenderer renderer = pixel.Renderer;
        Sprite sprite = renderer.sprite;
        string path = AssetDatabase.GetAssetPath(sprite);
        bool directionalPlayer = pixel.UsesPlayerDirectionalArt;
        bool directionalTiger = species == CasinoAnimalSpecies.Tiger;
        Check(pixel.UsesDealerWalkArt == directionalTiger, $"Unexpected dealer walk bindings for {species}.");
        string expectedName = $"{(pixel.IsVip ? "VIP_" : "")}{species}_Standing_48x64";
        string expectedPath = directionalPlayer
            ? "Assets/Art/ApprovedCasino/Characters/Player/"
            : directionalTiger ? "Assets/Art/ApprovedCasino/Characters/Staff/"
            : ApprovedCasinoArtIntegration.ArtRoot + "/";
        bool expectedSprite = directionalPlayer
            ? pixel.IsPlayerWalking
                ? pixel.UsesPlayerWalkArt && pixel.IsPlayerWalkSprite(sprite)
                : sprite == pixel.SelectedStandingArt && sprite.name.EndsWith("-03", StringComparison.Ordinal)
            : directionalTiger ? sprite.name.StartsWith("tiger-walk-", StringComparison.Ordinal)
            : sprite == pixel.SelectedStandingArt && sprite.name == expectedName;
        Check(pixel.UsesApprovedStandingArt && expectedSprite && path.StartsWith(expectedPath),
            $"Wrong live sprite for {species}/VIP={pixel.IsVip}: {path}/{sprite?.name}");
        if (directionalTiger)
        {
            string[] headings = { "south", "east", "north", "west" };
            SerializedProperty cycle = new SerializedObject(pixel).FindProperty("dealerWalkFrames");
            Check(cycle.arraySize == 16, "Tiger must have four walk frames per heading.");
            for (int i = 0; i < cycle.arraySize; i++)
                Check(cycle.GetArrayElementAtIndex(i).objectReferenceValue is Sprite frame &&
                    frame.name == $"tiger-walk-{headings[i / 4]}-{1 + i % 4:00}",
                    $"Tiger walk frame {i} is missing or out of order.");
            AIDealer dealer = pixel.GetComponentInParent<AIDealer>();
            if (dealer.IsResting || dealer.IsAtStation)
            {
                string heading = headings.Single(candidate =>
                    sprite.name.StartsWith($"tiger-walk-{candidate}-", StringComparison.Ordinal));
                string idleFrame = heading == "south" || heading == "north" ? "03" : "01";
                Check(sprite == pixel.SelectedStandingArt && sprite.name.EndsWith("-" + idleFrame, StringComparison.Ordinal),
                    $"Stationary Tiger is not holding the planted {heading} frame.");
            }
        }
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        Check(block.GetTexture("_MainTex") == sprite.texture,
            $"URP _MainTex does not match the selected {species} sprite.");
        Vector3 scale = renderer.transform.lossyScale;
        Check(Mathf.Abs(scale.x - scale.y) < .001f && Mathf.Abs(scale.y - scale.z) < .001f &&
            Mathf.Abs(sprite.bounds.size.y * scale.y - PixelCharacterVisual.StandingHeight(species)) < .01f,
            $"Character {species} is squashed or outside its world-height target: {scale}");
        Check(renderer.enabled && renderer.color.a > .9f, $"Character {species} is hidden.");
        Check(pixel.GetComponentsInChildren<MeshRenderer>(true).All(mesh => !mesh.enabled),
            $"Legacy procedural character is visible beside approved {species}.");
        bool directional = directionalPlayer || directionalTiger;
        int pixelsPerUnit = directional ? 256 : 32;
        Vector2 expectedSize = directional ? new Vector2(256, 256) : new Vector2(48, 64);
        Check(sprite.pivot == new Vector2(sprite.rect.width * .5f, 0),
            "Standing/directional sprite lost its bottom-center foot pivot.");
        Check(sprite.pixelsPerUnit == pixelsPerUnit && sprite.texture.filterMode == FilterMode.Point &&
            sprite.texture.mipmapCount == 1, "Standing sprite lost its pixel-art import settings.");
        Check(sprite.rect == new Rect(Vector2.zero, expectedSize) && sprite.textureRect == sprite.rect &&
            sprite.vertices.Length == 4 && sprite.triangles.Length == 6 &&
            (Vector2)sprite.bounds.min == new Vector2(-expectedSize.x / (2 * pixelsPerUnit), 0) &&
            (Vector2)sprite.bounds.max ==
                new Vector2(expectedSize.x / (2 * pixelsPerUnit), expectedSize.y / pixelsPerUnit),
            $"The live {species}/VIP={pixel.IsVip} mesh crops the full-body source image.");
        Check(renderer.drawMode == SpriteDrawMode.Simple && renderer.maskInteraction == SpriteMaskInteraction.None,
            $"The live {species} renderer slices, tiles or masks the full-body image.");
        foreach (Vector2 corner in new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one })
            Check(Array.IndexOf(sprite.uv, corner) >= 0, $"Live {species} UVs crop the source image.");
        ValidateAura(pixel);
    }

    private static void ValidateAura(PixelCharacterVisual pixel)
    {
        SpriteRenderer aura = pixel.GetComponentsInChildren<SpriteRenderer>(true)
            .SingleOrDefault(renderer => renderer.name == "VIP Golden Aura");
        if (!pixel.IsVip)
        {
            Check(aura == null || !aura.enabled, "A regular character has a VIP glow.");
            return;
        }
        SpriteRenderer actor = pixel.Renderer;
        Check(aura != null && aura.enabled && aura.sprite != null && aura.color.a >= .83f &&
            aura.transform.parent == actor.transform && aura.transform.localScale == Vector3.one &&
            aura.sortingLayerID == actor.sortingLayerID && aura.sortingOrder == actor.sortingOrder - 1 &&
            aura.sharedMaterial == actor.sharedMaterial, "VIP glow is hidden, misaligned or drawn above its character.");
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        aura.GetPropertyBlock(block);
        Check(block.GetTexture("_MainTex") == aura.sprite.texture, "VIP glow has the wrong URP texture binding.");
        Sprite sprite = actor.sprite;
        Check(aura.sprite.rect.size == sprite.rect.size + Vector2.one * 8 &&
            aura.sprite.pivot == sprite.pivot + Vector2.one * 4 &&
            aura.sprite.pixelsPerUnit == sprite.pixelsPerUnit, "VIP outline padding or foot alignment is incorrect.");
        if (!ValidatedAuras.Add(aura.sprite)) return;
        Color[] source = sprite.texture.GetPixels();
        Color[] glow = aura.sprite.texture.GetPixels();
        int width = aura.sprite.texture.width;
        int visible = 0;
        for (int y = 0; y < (int)sprite.rect.height; y++)
        for (int x = 0; x < (int)sprite.rect.width; x++)
        {
            Color original = source[y * (int)sprite.rect.width + x];
            Color outline = glow[(y + 4) * width + x + 4];
            if (original.a == 1) Check(outline.a == 0, "VIP glow paints over opaque body/foot artwork.");
            if (outline.a > .1f && original.a == 0)
            {
                Check(outline.r > .98f && outline.g > .65f && outline.g < .8f && outline.b < .2f,
                    "VIP outline is not gold.");
                visible++;
            }
        }
        Check(visible > 50, "VIP glow is missing around the outside of its silhouette.");
        Debug.Log($"VIP GOLD OUTLINE {sprite.name}: exteriorPixels={visible}; body/feet preserved; URP binding correct.");
    }

    public sealed class Driver : MonoBehaviour
    {
        private Exception failure;
        private string directory;
        private Keyboard keyboard;
        private Mouse mouse;
        private InputSettings.BackgroundBehavior oldBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditorBehavior;
        private readonly List<InputDevice> disabledDevices = new List<InputDevice>();

        private void Start()
        {
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
            foreach (InputDevice device in InputSystem.devices)
                if (device.enabled && (device is Keyboard || device is Mouse))
                {
                    disabledDevices.Add(device);
                    InputSystem.DisableDevice(device);
                }
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            oldBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            oldEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            directory = Environment.GetEnvironmentVariable("PIT_PANIC_ART_EVIDENCE");
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("Set PIT_PANIC_ART_EVIDENCE to a capture directory.");
            Directory.CreateDirectory(directory);
            Application.logMessageReceived += OnLog;
            StartCoroutine(RunTest());
        }

        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception)
                failure = new Exception(message + "\n" + stack);
        }

        private IEnumerator RunTest()
        {
            // Flatten nested enumerators so any assertion also restores the real profile on failure.
            Stack<IEnumerator> tests = new Stack<IEnumerator>();
            tests.Push(Test());
            float deadline = Time.realtimeSinceStartup + 240;
            while (tests.Count > 0)
            {
                object current = null;
                bool moved = false;
                try
                {
                    if (failure != null) throw failure;
                    Check(Time.realtimeSinceStartup < deadline, "Approved-art Play Mode test timed out.");
                    IEnumerator test = tests.Peek();
                    moved = test.MoveNext();
                    if (moved) current = test.Current;
                    else tests.Pop();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    Restore();
                    EditorApplication.Exit(1);
                    yield break;
                }
                if (!moved) continue;
                if (current is IEnumerator nested) tests.Push(nested);
                else yield return current;
            }
            Debug.Log("APPROVED REAL-GAME ART PLAY MODE PASSED: player, 4 dealers, 6 regular + 6 VIP real spawns, movement, seating, URP bindings, 3 campaign floors, live captures.");
            Restore();
            EditorApplication.Exit(0);
        }

        private void Restore()
        {
            Application.logMessageReceived -= OnLog;
            InputSystem.settings.backgroundBehavior = oldBackgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorBehavior;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            foreach (InputDevice device in disabledDevices)
                if (device.added) InputSystem.EnableDevice(device);
            foreach (string key in SaveKeys())
            {
                int value = SessionState.GetInt("ApprovedArt.Old." + key, -1);
                if (value < 0) PlayerPrefs.DeleteKey(key);
                else PlayerPrefs.SetInt(key, value);
            }
            PlayerPrefs.Save();
        }

        private IEnumerator Capture(string name)
        {
            Check(Application.isPlaying && Time.timeScale > 0, "Capture attempted outside active Play Mode.");
            string path = Path.Combine(directory, name + ".png");
            if (File.Exists(path)) File.Delete(path);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            float deadline = Time.realtimeSinceStartup + 5;
            while (!File.Exists(path) && Time.realtimeSinceStartup < deadline) yield return null;
            Check(File.Exists(path) && new FileInfo(path).Length > 10000, "Live Game View capture failed: " + path);
            Debug.Log($"PLAY MODE CAPTURE {path}; frame={Time.frameCount}; size={Screen.width}x{Screen.height}");
        }

        private IEnumerator Test()
        {
            yield return null;
            Check(Mathf.Abs(PixelCharacterVisual.StandingHeight(CasinoAnimalSpecies.Raccoon) -
                PixelCharacterVisual.StandingHeight(CasinoAnimalSpecies.Dog)) < .001f,
                "Raccoon must use the Dog's world-height target.");
            Check(Mathf.Abs(PixelCharacterVisual.StandingHeight(CasinoAnimalSpecies.Elephant) - 3.15f) < .001f &&
                PixelCharacterVisual.StandingHeight(CasinoAnimalSpecies.Elephant) >
                PixelCharacterVisual.StandingHeight(CasinoAnimalSpecies.Dog),
                "Elephant should be slightly larger than its previous 3.0-unit canvas target.");
            for (int level = 1; level <= 3; level++)
            {
                if (level > 1)
                {
                    SceneManager.LoadScene("PitPanicPrototype");
                    yield return null;
                    yield return null;
                }
                CasinoShiftController shift = FindFirstObjectByType<CasinoShiftController>();
                Call(shift, "BeginShift", level, false);
                if (shift.IsLessonOpen) shift.DismissLesson();
                Time.timeScale = 1;
                AudioListener.pause = false;
                CasinoPlayerController player = FindFirstObjectByType<CasinoPlayerController>();
                // Dialogue releases gameplay input on a later frame after its closing click.
                yield return null;
                yield return null;
                Vector3 playerStart = player.transform.position;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.W));
                yield return new WaitForSeconds(.4f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Check(Vector3.Distance(playerStart, player.transform.position) > .3f,
                    $"Real Penguin player did not respond to movement input: level={level}, " +
                    $"start={playerStart}, end={player.transform.position}, focused={Application.isFocused}.");
                Debug.Log($"LIVE PLAYER level={level} species=Penguin moved={Vector3.Distance(playerStart, player.transform.position):F2}");
                CasinoCustomerSpawner spawner = FindFirstObjectByType<CasinoCustomerSpawner>();
                GameObject[] pool = (GameObject[])typeof(CasinoCustomerSpawner)
                    .GetField("customerCharacterPrefabs", Flags).GetValue(spawner);
                Check(pool.Length == CustomerSpecies.Length &&
                    CustomerSpecies.All(species => pool.Any(prefab =>
                        prefab.GetComponent<CasinoCharacterVisual>().Species == species)),
                    "The live customer pool does not match the six supplied regular/VIP customer species.");
                spawner.SetGuidedPractice(true);
                spawner.SetAcceptingArrivals(false);
                foreach (AIDealer dealer in FindObjectsByType<AIDealer>(FindObjectsSortMode.None))
                    dealer.TrainingProtected = true;
                yield return Capture($"level-{level}-roulette-chairs");
                List<CasinoCustomerAI> spawned = new List<CasinoCustomerAI>();
                Dictionary<CasinoCustomerAI, Vector3> origins = new Dictionary<CasinoCustomerAI, Vector3>();
                int before = spawner.TotalSpawned;
                if (level != 2)
                {
                    foreach (CasinoAnimalSpecies species in CustomerSpecies)
                    {
                        CasinoCustomerAI customer = (CasinoCustomerAI)Call(spawner, "SpawnCustomer", spawned.Count * 2, (CasinoAnimalSpecies?)species);
                        Check(customer != null, $"Real spawner could not create {species}.");
                        if (level == 3)
                        {
                            customer.SetVip();
                            PixelCharacterVisual visual = customer.GetComponentInChildren<PixelCharacterVisual>();
                            visual.SetVip(false);
                            ValidateAura(visual);
                            visual.SetVip(true);
                        }
                        spawned.Add(customer);
                        origins.Add(customer, customer.transform.position);
                        CasinoSeat seat = FindObjectsByType<CasinoSeat>(FindObjectsSortMode.None)
                            .OrderBy(candidate => spawned.Count <= 4 && candidate.GameType == CasinoCustomerGame.Roulette ? 0 : 1)
                            .FirstOrDefault(candidate => candidate.IsAvailable && candidate.IsGameAvailable);
                        Check(seat != null && customer.TryVisitSeat(seat), $"Real spawn {species} could not reserve a real seat.");
                        yield return new WaitForSeconds(.4f);
                    }
                    Check(spawner.TotalSpawned == before + 6, "Customers did not originate from the real spawner.");
                }
                else
                {
                    // This enables the campaign's own delayed Rabbit/Deer/Cat/Raccoon VIP opening arrivals.
                    spawner.SetAcceptingArrivals(true);
                }

                int firstFrame = Time.frameCount;
                float until = Time.realtimeSinceStartup + 35;
                while (Time.realtimeSinceStartup < until)
                {
                    if (shift.IsLessonOpen) shift.DismissLesson();
                    if (Time.frameCount - firstFrame > 90)
                        foreach (PixelCharacterVisual pixel in FindObjectsByType<PixelCharacterVisual>(FindObjectsSortMode.None))
                            ValidateActor(pixel);
                    CasinoCustomerAI[] customers = FindObjectsByType<CasinoCustomerAI>(FindObjectsSortMode.None);
                    if (Time.frameCount - firstFrame > 120 && customers.Count(customer => customer.IsSeated) >= 4 &&
                        (level == 2 || RouletteCustomersSeated()) &&
                        (level != 2 || CustomerOpeningVipsSeated(customers))) break;
                    yield return null;
                }
                foreach (PixelCharacterVisual pixel in FindObjectsByType<PixelCharacterVisual>(FindObjectsSortMode.None))
                {
                    ValidateActor(pixel);
                    SpriteRenderer renderer = pixel.Renderer;
                    Rect canvas = ScreenRect(renderer);
                    Check(canvas.xMin >= 0 && canvas.yMin >= 0 &&
                        canvas.xMax <= Screen.width && canvas.yMax <= Screen.height,
                        "A live character canvas extends offscreen: " + renderer.sprite.name);
                    Vector3 feet = renderer.transform.TransformPoint(new Vector3(0, renderer.sprite.bounds.min.y, 0));
                    Check(feet.y >= 0, "Character foot pivot is below the playable floor: " + renderer.sprite.name);
                    Debug.Log($"FULL BODY LIVE level={level} sprite={renderer.sprite.name} screen={canvas} " +
                        $"feetHeight={feet.y:F3}; full 48x64 image, no mask or sliced draw mode.");
                }
                foreach (CasinoCustomerAI customer in spawned)
                {
                    Check(customer != null && Vector3.Distance(customer.transform.position, origins[customer]) > .5f,
                        "Spawned customer did not navigate away from the entrance.");
                    ValidateActor(customer.GetComponentInChildren<PixelCharacterVisual>());
                    Debug.Log($"LIVE SPECIES level={level} species={customer.GetComponentInChildren<CasinoCharacterVisual>().Species} " +
                        $"VIP={customer.IsVip} moved={Vector3.Distance(customer.transform.position, origins[customer]):F2} " +
                        $"seated={customer.IsSeated} service={customer.IsInService}");
                }
                Check(FindObjectsByType<CasinoCustomerAI>(FindObjectsSortMode.None).Any(customer => customer.IsSeated),
                    $"No real seated customer on level {level}.");
                if (level != 2)
                    Check(RouletteCustomersSeated(), $"Not all four rearranged Roulette seats were reached on level {level}.");
                if (level == 2)
                    Check(CustomerOpeningVipsSeated(FindObjectsByType<CasinoCustomerAI>(FindObjectsSortMode.None)),
                        "Campaign's actual four opening VIPs did not seat.");
                Check(FindObjectsByType<AIDealer>(FindObjectsSortMode.None).Length == 4,
                    "The live floor must retain all four real dealers.");
                Check(FindFirstObjectByType<CasinoPlayerController>().GetComponentInChildren<CasinoCharacterVisual>().Species ==
                    CasinoAnimalSpecies.Penguin, "The real player is not Penguin.");
                CasinoAnimalSpecies[] staff = { CasinoAnimalSpecies.Tiger, CasinoAnimalSpecies.Bear,
                    CasinoAnimalSpecies.Fox, CasinoAnimalSpecies.Pig };
                Check(staff.All(species => FindObjectsByType<AIDealer>(FindObjectsSortMode.None)
                    .Any(dealer => dealer.GetComponentInChildren<CasinoCharacterVisual>().Species == species)),
                    "The live crew must retain Tiger, Bear, Fox and Pig.");
                foreach (AIDealer dealer in FindObjectsByType<AIDealer>(FindObjectsSortMode.None))
                    if (dealer.IsAtStation)
                        Check(Vector3.Distance(dealer.transform.position, dealer.AssignedStation.DealerPosition.position) < .15f,
                            "Dealer no longer aligns with the campaign station.");
                ValidateFloorArt();
                yield return Capture($"level-{level}-live-floor");
                yield return new WaitForSeconds(1);
                foreach (PixelCharacterVisual pixel in FindObjectsByType<PixelCharacterVisual>(FindObjectsSortMode.None))
                    ValidateActor(pixel);
                yield return Capture($"level-{level}-live-service");
            }
        }

        private static bool RouletteCustomersSeated()
        {
            CasinoSeat[] seats = FindObjectsByType<CasinoSeat>(FindObjectsSortMode.None)
                .Where(seat => seat.GameType == CasinoCustomerGame.Roulette).ToArray();
            return seats.Length == 4 && seats.All(seat => seat.Occupant != null && seat.Occupant.IsSeated);
        }

        private static bool CustomerOpeningVipsSeated(CasinoCustomerAI[] customers) =>
            new[] { CasinoAnimalSpecies.Rabbit, CasinoAnimalSpecies.Deer, CasinoAnimalSpecies.Cat, CasinoAnimalSpecies.Raccoon }
                .All(species => customers.Any(customer => customer.IsVip && customer.IsSeated &&
                    customer.GetComponentInChildren<CasinoCharacterVisual>().Species == species));

        private static void ValidateFloorArt()
        {
            SpriteRenderer carpet = GameObject.Find("Pixel Carpet").GetComponent<SpriteRenderer>();
            Check(carpet.sprite.rect == new Rect(1004, 416, 160, 160) &&
                AssetDatabase.GetAssetPath(carpet.sprite.texture) ==
                    "Assets/Resources/Art/BurgundyFloor/Burgundy_Casino_Floor_Tile_Atlas.png" &&
                carpet.drawMode == SpriteDrawMode.Tiled && carpet.transform.localScale == Vector3.one &&
                Mathf.Abs(carpet.bounds.size.x - 38) < .01f &&
                Mathf.Abs(carpet.bounds.size.z - 22) < .01f,
                "Burgundy carpet must tile the supplied PNG crop without stretching or changing the floor footprint.");
            foreach (string name in CasinoCampaignPresentation.FloorVisualNames)
            {
                SpriteRenderer floor = carpet.transform.parent.Find(name)?.GetComponent<SpriteRenderer>();
                Check(floor != null && floor.enabled && floor.sprite.texture == carpet.sprite.texture &&
                    floor.sprite.pixelsPerUnit == 32 && floor.transform.localScale == Vector3.one &&
                    floor.drawMode == SpriteDrawMode.Tiled && floor.tileMode == SpriteTileMode.Continuous &&
                    floor.GetComponent<Collider>() == null && floor.sortingOrder > carpet.sortingOrder &&
                    floor.sortingOrder < -95,
                    $"Floor-only sprite {name} lost its source artwork, pixel density, layering or collision-free setup.");
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                floor.GetPropertyBlock(block);
                Check(block.GetTexture("_MainTex") == floor.sprite.texture,
                    $"Floor-only sprite {name} lost its URP atlas binding.");
            }
            SpriteRenderer runner = carpet.transform.parent.Find("Burgundy Gold Runner").GetComponent<SpriteRenderer>();
            float cashZ = FindFirstObjectByType<CasinoCashDeskInteraction>().transform.position.z;
            float rouletteZ = FindFirstObjectByType<RouletteRoundController>().transform.position.z;
            Check(Mathf.Abs(runner.bounds.min.z - Mathf.Min(cashZ, rouletteZ)) < .001f &&
                Mathf.Abs(runner.bounds.max.z - Mathf.Max(cashZ, rouletteZ)) < .001f &&
                Mathf.Abs(runner.bounds.size.x - 103f / 32) < .001f &&
                runner.sprite.rect == new Rect(280, 71, 103, 64),
                "Gold-trimmed aisle must connect the existing cashier and Roulette in each campaign layout.");
            Check(carpet.sprite.texture.filterMode == FilterMode.Point &&
                carpet.sprite.texture.mipmapCount == 1 && carpet.sprite.texture.width == 1448 &&
                carpet.sprite.texture.height == 1086,
                "The flooring atlas must stay crisp, unscaled and free of mipmap blur.");
            Check(TileJoinDifference(carpet.sprite, true) < 4f / 255 &&
                TileJoinDifference(carpet.sprite, false) < 4f / 255 &&
                TileJoinDifference(runner.sprite, true) < 4f / 255,
                "The selected source crops introduce a stronger carpet/runner seam than the audited atlas.");
            SpriteRenderer wall = GameObject.Find("Casino Back Wall").GetComponent<SpriteRenderer>();
            Check(!wall.enabled && !GameObject.Find("Break Room Label").GetComponent<Canvas>().enabled &&
                FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
                    .Where(renderer => renderer.name == "Pixel Window" || renderer.name == "Pixel Column" ||
                        renderer.name == "Pixel Curtain").All(renderer => !renderer.enabled),
                "Redundant wall/window/sconce artwork or the old floating break-room sign remains visible.");
            ValidateWallArt();
            foreach (AIDealer dealer in FindObjectsByType<AIDealer>(FindObjectsSortMode.None))
            {
                SpriteRenderer actor = dealer.GetComponentInChildren<PixelCharacterVisual>().Renderer;
                Color32[] pixels = actor.sprite.texture.GetPixels32();
                int min = actor.sprite.texture.height, max = -1;
                for (int y = 0; y < actor.sprite.texture.height; y++)
                for (int x = 0; x < actor.sprite.texture.width; x++)
                    if (pixels[y * actor.sprite.texture.width + x].a > 0)
                    {
                        min = Mathf.Min(min, y);
                        max = Mathf.Max(max, y);
                    }

                float height = (max - min + 1) / actor.sprite.pixelsPerUnit * actor.transform.lossyScale.y;
                float penguinHeight = 2.2f * 60f / 64f;
                Check(Mathf.Abs(height / penguinHeight - 1) < .03f,
                    "Dealer apparent height differs from Penguin by more than 3%: " + dealer.name);
            }
            foreach (SpriteRenderer chair in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
                .Where(renderer => renderer.name == "Pixel Chair"))
            {
                CasinoSeat seat = chair.GetComponentInParent<CasinoSeat>();
                Check(Vector3.Distance(chair.transform.position, seat.SittingPosition) <= .16f,
                    "Independent chair no longer follows its real seat/pull-out animation.");
                if (seat.GameType != CasinoCustomerGame.Roulette)
                    Check(chair.sprite.name == "VelvetChair_View05",
                        "Blackjack chairs must face the table, not the camera.");
            }
            Check(FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
                .Where(renderer => renderer.name == "Pixel BreakRoom" || renderer.name == "Pixel Entrance" ||
                    renderer.name == "Table Rug").All(renderer => !renderer.enabled),
                "Obsolete procedural room/entrance/rug artwork is still visible.");
            foreach (string labelName in new[] { "Blackjack Table 1 Label", "Blackjack Table 2 Label",
                "Roulette Table Label" })
            {
                RectTransform label = (RectTransform)GameObject.Find(labelName).transform;
                SpriteRenderer table = label.parent.GetComponentsInChildren<SpriteRenderer>()
                    .Single(renderer => renderer.name == (labelName.StartsWith("Roulette") ?
                        "Pixel Roulette" : "Pixel BlackjackV2"));
                Check(!ScreenRect(label).Overlaps(ScreenRect(table)),
                    "Station label covers the table artwork: " + labelName);
            }
            string[] names = { "Pixel BlackjackV2", "Pixel Roulette", "Pixel CashDesk", "Pixel PitDesk",
                "Pixel Chair", "Pixel Plant", "Pixel Sofa" };
            foreach (string name in names)
            {
                SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
                    .Where(renderer => renderer.name == name).ToArray();
                Check(renderers.Length > 0, "Missing real floor object: " + name);
                foreach (SpriteRenderer renderer in renderers)
                {
                    Check(AssetDatabase.GetAssetPath(renderer.sprite).StartsWith(ApprovedCasinoArtIntegration.ArtRoot + "/"),
                        "Old floor art survived: " + name);
                    Check(renderer.sprite.pixelsPerUnit == 32 &&
                        renderer.sprite.texture.filterMode == FilterMode.Point &&
                        renderer.sprite.texture.mipmapCount == 1,
                        "Approved floor sprite lost its pixel-art import settings: " + name);
                    MaterialPropertyBlock block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    Check(block.GetTexture("_MainTex") == renderer.sprite.texture, "Floor texture binding failed: " + name);
                }
            }
            Check(FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
                .Where(renderer => renderer.name == "Pixel Roulette Wheel").All(renderer => !renderer.enabled),
                "Duplicate wheel is drawn over the approved table.");
            ValidateRouletteSeating();
        }

        private static void ValidateRouletteSeating()
        {
            RouletteRoundController roulette = FindFirstObjectByType<RouletteRoundController>();
            CasinoSeat[] seats = roulette.GetComponentsInChildren<CasinoSeat>()
                .OrderBy(seat => seat.name).ToArray();
            Check(seats.Length == 4, "Roulette must retain all four real customer seats.");
            Vector3 entrance = FindFirstObjectByType<CasinoCustomerSpawner>().Entrance.position;
            foreach (CasinoSeat seat in seats)
            {
                bool rightEnd = seat.name.EndsWith("4");
                Vector3 position = roulette.transform.InverseTransformPoint(seat.SittingPosition);
                Vector3 facing = seat.Facing * Vector3.forward;
                SpriteRenderer chair = seat.GetComponentInChildren<SpriteRenderer>();
                Check(chair.sprite.name == (rightEnd ? "CasinoStool_View03" : "CasinoStool_View05"),
                    "Roulette chair faces the wrong direction: " + seat.name);
                Check(rightEnd ? position.x > 3 && Vector3.Dot(facing, Vector3.left) > .99f :
                    position.x >= 0 && position.x <= 2.51f && Mathf.Abs(position.z + 2.05f) < .01f &&
                    Vector3.Dot(facing, Vector3.forward) > .99f,
                    "Roulette chairs must sit by the layout, not around the wheel: " + seat.name);
                UnityEngine.AI.NavMeshPath path = new UnityEngine.AI.NavMeshPath();
                Check(UnityEngine.AI.NavMesh.SamplePosition(seat.Position, out UnityEngine.AI.NavMeshHit hit,
                    .2f, UnityEngine.AI.NavMesh.AllAreas) &&
                    UnityEngine.AI.NavMesh.CalculatePath(entrance, hit.position, UnityEngine.AI.NavMesh.AllAreas, path) &&
                    path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete,
                    "A rearranged Roulette seat is unreachable: " + seat.name);
                Check(Vector3.Distance(chair.transform.position, seat.SittingPosition) <= .16f,
                    "A Roulette chair no longer follows its seated customer's anchor.");
            }
            Check(Mathf.Abs(seats[1].SittingPosition.x - seats[0].SittingPosition.x - 1.25f) < .01f &&
                Mathf.Abs(seats[2].SittingPosition.x - seats[1].SittingPosition.x - 1.25f) < .01f,
                "The three near-side Roulette chairs are not evenly spaced.");
            Debug.Log("LIVE ROULETTE CHAIRS PASSED: 3 facing away by the layout, 1 facing left at the right end, " +
                "4 reachable real seats, matching chair/sitting anchors and preserved pull-out animation.");
        }

        private static void ValidateWallArt()
        {
            Transform root = GameObject.Find(BurgundyWallIntegration.WallName)?.transform;
            Check(root != null && root.GetComponentsInChildren<Collider>().Length == 0 &&
                root.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>().Length == 0,
                "Supplied walls must be visual-only, with no navigation or collision changes.");
            SpriteRenderer[] modules = root.GetComponentsInChildren<SpriteRenderer>()
                .Where(renderer => renderer.enabled).ToArray();
            Check(modules.Length == 5 && modules.All(renderer =>
                renderer.name.StartsWith("Wood Base") && renderer.sprite.name == "Wainscot_WoodGold_A"),
                "The shallow wall must contain only five matching supplied panels.");
            foreach (SpriteRenderer renderer in modules)
            {
                Sprite sprite = renderer.sprite;
                string path = AssetDatabase.GetAssetPath(sprite);
                Vector3 scale = renderer.transform.lossyScale;
                Check(path.StartsWith(BurgundyWallIntegration.Root) && renderer.enabled &&
                    renderer.drawMode == SpriteDrawMode.Simple &&
                    sprite.rect == new Rect(0, 0, sprite.texture.width, sprite.texture.height) &&
                    sprite.vertices.Length == 4 && sprite.texture.filterMode == FilterMode.Point &&
                    sprite.texture.mipmapCount == 1 && sprite.pixelsPerUnit == 80 &&
                    Mathf.Abs(scale.x - scale.y) < .001f && Mathf.Abs(scale.y - scale.z) < .001f,
                    "Wall artwork is cropped, tiled, stretched or blurred: " + renderer.name);
                Check(renderer.sortingOrder >= -95 && renderer.sortingOrder <= -91,
                    "Wall artwork must remain behind all furniture and characters.");
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                Check(block.GetTexture("_MainTex") == sprite.texture,
                    "Wall sprite has an incorrect URP texture: " + renderer.name);
            }
            Check(root.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(renderer => renderer.name.StartsWith("Wall Window") ||
                    renderer.name == "Break Room Doorway").All(renderer => !renderer.enabled),
                "A removed window or break-room doorway remains visible.");
            SpriteRenderer rouletteArt = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
                .Single(renderer => renderer.name == "Pixel Roulette");
            float wallBottom = modules.Where(renderer => renderer.name.StartsWith("Wood Base"))
                .Min(renderer => ScreenRect(renderer).yMin);
            Check(wallBottom >= Screen.height * .80f &&
                wallBottom > ScreenRect(rouletteArt).yMin + Screen.height * .08f,
                "The wall extends down behind Roulette's base instead of leaving it on the carpet.");
            SpriteRenderer[] wallLamps = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
                .Where(renderer => renderer.name == "Pixel Lamp" &&
                    renderer.transform.parent.name == "Casino Atmosphere" &&
                    renderer.transform.localPosition.z >= 8f).ToArray();
            Check(wallLamps.Length > 0 && wallLamps.All(renderer => !renderer.enabled),
                "Floating wall-side lamps remain visible.");
            Debug.Log("LIVE WALL ART PASSED: shallow matching panels only, no windows, doorway or floating lamps, " +
                "Roulette base on carpet, " +
                "original texture binding and no new colliders.");
        }

        private static float TileJoinDifference(Sprite sprite, bool verticalRepeat)
        {
            Rect rect = sprite.rect;
            Color[] pixels = sprite.texture.GetPixels((int)rect.x, (int)rect.y,
                (int)rect.width, (int)rect.height);
            int width = (int)rect.width, height = (int)rect.height;
            int count = verticalRepeat ? width : height;
            float difference = 0;
            for (int i = 0; i < count; i++)
            {
                Color first = pixels[verticalRepeat ? i : i * width];
                Color last = pixels[verticalRepeat ? (height - 1) * width + i : i * width + width - 1];
                difference += Mathf.Abs(first.r - last.r) + Mathf.Abs(first.g - last.g) +
                    Mathf.Abs(first.b - last.b);
            }
            return difference / (count * 3);
        }

        private static Rect ScreenRect(RectTransform transform)
        {
            Vector3[] corners = new Vector3[4];
            transform.GetWorldCorners(corners);
            return Project(corners);
        }

        private static Rect ScreenRect(SpriteRenderer renderer)
        {
            Bounds bounds = renderer.sprite.bounds;
            return Project(new[]
            {
                renderer.transform.TransformPoint(new Vector3(bounds.min.x, bounds.min.y, 0)),
                renderer.transform.TransformPoint(new Vector3(bounds.max.x, bounds.min.y, 0)),
                renderer.transform.TransformPoint(new Vector3(bounds.min.x, bounds.max.y, 0)),
                renderer.transform.TransformPoint(new Vector3(bounds.max.x, bounds.max.y, 0))
            });
        }

        private static Rect Project(Vector3[] corners)
        {
            Vector3[] screen = corners.Select(point => Camera.main.WorldToScreenPoint(point)).ToArray();
            return Rect.MinMaxRect(screen.Min(point => point.x), screen.Min(point => point.y),
                screen.Max(point => point.x), screen.Max(point => point.y));
        }
    }
}
