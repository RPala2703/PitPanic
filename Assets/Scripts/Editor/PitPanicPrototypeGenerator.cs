using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PitPanicPrototypeGenerator
{
    private const string ScenePath = "Assets/Scenes/PitPanicPrototype.unity";
    private const string MaterialsPath = "Assets/Materials/PitPanic";

    [MenuItem("Pit Panic/Generate Prototype Scene")]
    public static void GeneratePrototypeScene()
    {
        Material floorMaterial = GetOrCreateMaterial("CasinoFloor", new Color(0.08f, 0.16f, 0.17f));
        Material trimMaterial = GetOrCreateMaterial("Brass", new Color(0.76f, 0.48f, 0.16f));
        Material feltMaterial = GetOrCreateMaterial("BlackjackFelt", new Color(0.04f, 0.31f, 0.22f));
        Material woodMaterial = GetOrCreateMaterial("TableWood", new Color(0.24f, 0.10f, 0.06f));
        Material playerMaterial = GetOrCreateMaterial("Player", new Color(0.91f, 0.59f, 0.23f));
        Material accentMaterial = GetOrCreateMaterial("Accent", new Color(0.25f, 0.49f, 0.55f));
        AssetDatabase.SaveAssets();

        SceneSetup[] previousSceneSetup = EditorSceneManager.GetSceneManagerSetup();
        bool hasSavedScenes = false;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene loadedScene = SceneManager.GetSceneAt(i);
            if (loadedScene.isLoaded && !string.IsNullOrEmpty(loadedScene.path))
            {
                hasSavedScenes = true;
            }

            if (loadedScene.isLoaded && loadedScene.path == ScenePath && loadedScene.isDirty &&
                !Application.isBatchMode &&
                !EditorUtility.DisplayDialog("Regenerate Pit Panic Prototype", "This will replace the contents of PitPanicPrototype.unity. Continue?", "Regenerate", "Cancel"))
            {
                return;
            }
        }

        if (hasSavedScenes)
        {
            if (Application.isBatchMode && EditorSceneManager.GetSceneManagerSetup().Length > 0)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    if (SceneManager.GetSceneAt(i).isDirty)
                    {
                        throw new System.InvalidOperationException("Cannot regenerate the prototype while a modified scene is open in batch mode.");
                    }
                }
            }
            else if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.48f, 0.55f, 0.62f);
        RenderSettings.ambientEquatorColor = new Color(0.30f, 0.34f, 0.35f);
        RenderSettings.ambientGroundColor = new Color(0.12f, 0.13f, 0.13f);
        RenderSettings.fog = false;

        CreateLighting();
        CreateFloor(floorMaterial, trimMaterial);
        CasinoPlayerController playerController = CreatePlayer(playerMaterial);
        CreateBlackjackTable(woodMaterial, feltMaterial, trimMaterial, accentMaterial, playerController);
        CreateCamera();

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            throw new IOException($"Could not save the prototype scene to {ScenePath}.");
        }

        if (hasSavedScenes)
        {
            EditorSceneManager.RestoreSceneManagerSetup(previousSceneSetup);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Pit Panic prototype scene saved to {ScenePath}.");
    }

    [MenuItem("Pit Panic/Run Blackjack Rule Checks")]
    public static void RunBlackjackRuleChecks()
    {
        AssertTotal("ace and nine", 20, new BlackjackCard(1, 0), new BlackjackCard(9, 1));
        AssertTotal("ace softens after bust", 15, new BlackjackCard(1, 0), new BlackjackCard(9, 1), new BlackjackCard(5, 2));
        AssertTotal("multiple aces", 21, new BlackjackCard(1, 0), new BlackjackCard(1, 1), new BlackjackCard(9, 2));
        AssertOutcome("player natural", BlackjackOutcome.Blackjack, 21, 18, true, false);
        AssertOutcome("natural push", BlackjackOutcome.Push, 21, 21, true, true);
        AssertOutcome("player bust", BlackjackOutcome.Lose, 22, 18, false, false);
        AssertOutcome("dealer bust", BlackjackOutcome.Win, 20, 22, false, false);
        AssertOutcome("equal totals", BlackjackOutcome.Push, 19, 19, false, false);

        BlackjackRound standRound = new BlackjackRound();
        if (!standRound.IsComplete)
        {
            standRound.Stand();
            if (standRound.DealerTotal < 17 && standRound.DealerTotal <= 21)
            {
                throw new System.InvalidOperationException("Dealer failed to draw to at least 17.");
            }
        }

        BlackjackRound hitRound = new BlackjackRound();
        for (int i = 0; i < 52 && !hitRound.IsComplete; i++)
        {
            hitRound.Hit();
        }

        if (!hitRound.IsComplete)
        {
            throw new System.InvalidOperationException("Repeated hits did not finish the blackjack round.");
        }

        Debug.Log("Blackjack rule checks passed.");
    }

    [MenuItem("Pit Panic/Validate Prototype Scene")]
    public static void ValidatePrototypeScene()
    {
        if (!File.Exists(ScenePath))
        {
            throw new FileNotFoundException($"Prototype scene not found at {ScenePath}.");
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null)
                {
                    throw new System.InvalidOperationException($"Missing script in generated scene object {root.name}.");
                }
            }
        }

        Camera camera = Object.FindFirstObjectByType<Camera>();
        CasinoPlayerController player = Object.FindFirstObjectByType<CasinoPlayerController>();
        BlackjackTableInteraction table = Object.FindFirstObjectByType<BlackjackTableInteraction>();
        if (camera == null || !camera.orthographic || player == null || table == null || !table.IsConfigured)
        {
            throw new System.InvalidOperationException("Generated scene is missing its orthographic camera, player controller, or configured blackjack interaction.");
        }

        if (Mathf.Abs(camera.transform.eulerAngles.y) > 0.1f || Vector3.Angle(camera.transform.forward, new Vector3(0f, -0.866f, 0.5f)) > 1f)
        {
            throw new System.InvalidOperationException("Generated camera is not aligned to the overhead, yaw-zero viewpoint.");
        }

        if (Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") == null)
        {
            throw new System.InvalidOperationException("Unity's LegacyRuntime.ttf font required by the blackjack panel is unavailable.");
        }

        Debug.Log("Prototype scene validation passed: camera, player, blackjack references, and scripts are present.");
    }

    private static void AssertTotal(string testName, int expected, params BlackjackCard[] cards)
    {
        int actual = BlackjackRules.CalculateBestTotal(cards);
        if (actual != expected)
        {
            throw new System.InvalidOperationException($"Blackjack rule check '{testName}' failed: expected {expected}, got {actual}.");
        }
    }

    private static void AssertOutcome(string testName, BlackjackOutcome expected, int playerTotal, int dealerTotal, bool playerNatural, bool dealerNatural)
    {
        BlackjackOutcome actual = BlackjackRules.ResolveOutcome(playerTotal, dealerTotal, playerNatural, dealerNatural);
        if (actual != expected)
        {
            throw new System.InvalidOperationException($"Blackjack rule check '{testName}' failed: expected {expected}, got {actual}.");
        }
    }

    private static void CreateFloor(Material floorMaterial, Material trimMaterial)
    {
        GameObject floor = new GameObject("Casino Floor");
        CreatePrimitive(PrimitiveType.Cube, "Floor", new Vector3(0f, -0.25f, 0f), new Vector3(24f, 0.5f, 18f), floorMaterial, floor.transform);

        CreatePrimitive(PrimitiveType.Cube, "North Brass Border", new Vector3(0f, 0.03f, 8.65f), new Vector3(23.5f, 0.08f, 0.12f), trimMaterial, floor.transform);
        CreatePrimitive(PrimitiveType.Cube, "South Brass Border", new Vector3(0f, 0.03f, -8.65f), new Vector3(23.5f, 0.08f, 0.12f), trimMaterial, floor.transform);
        CreatePrimitive(PrimitiveType.Cube, "East Brass Border", new Vector3(11.65f, 0.03f, 0f), new Vector3(0.12f, 0.08f, 17.3f), trimMaterial, floor.transform);
        CreatePrimitive(PrimitiveType.Cube, "West Brass Border", new Vector3(-11.65f, 0.03f, 0f), new Vector3(0.12f, 0.08f, 17.3f), trimMaterial, floor.transform);

        for (int x = -1; x <= 1; x += 2)
        {
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 position = new Vector3(x * 10.6f, 2f, z * 7.6f);
                CreatePrimitive(PrimitiveType.Cube, "Decorative Column", position, new Vector3(0.8f, 4f, 0.8f), trimMaterial, floor.transform);
                CreatePrimitive(PrimitiveType.Cube, "Column Base", new Vector3(position.x, 0.25f, position.z), new Vector3(1.1f, 0.5f, 1.1f), floorMaterial, floor.transform);
            }
        }
    }

    private static void CreateBlackjackTable(Material woodMaterial, Material feltMaterial, Material trimMaterial, Material accentMaterial, CasinoPlayerController playerController)
    {
        GameObject table = new GameObject("Blackjack Table");
        Vector3 center = new Vector3(0f, 0f, 1.3f);

        CreatePrimitive(PrimitiveType.Cube, "Tabletop", center + new Vector3(0f, 1.15f, 0f), new Vector3(5.2f, 0.32f, 2.6f), woodMaterial, table.transform);
        CreatePrimitive(PrimitiveType.Cube, "Felt Playing Surface", center + new Vector3(0f, 1.34f, 0f), new Vector3(4.85f, 0.08f, 2.25f), feltMaterial, table.transform);

        for (int x = -1; x <= 1; x += 2)
        {
            for (int z = -1; z <= 1; z += 2)
            {
                CreatePrimitive(
                    PrimitiveType.Cylinder,
                    "Table Leg",
                    center + new Vector3(x * 1.8f, 0.56f, z * 0.78f),
                    new Vector3(0.24f, 0.56f, 0.24f),
                    trimMaterial,
                    table.transform);
            }
        }

        CreatePrimitive(PrimitiveType.Cube, "Dealer Rail", center + new Vector3(0f, 1.62f, 1.18f), new Vector3(5.2f, 0.42f, 0.22f), woodMaterial, table.transform);
        CreatePrimitive(PrimitiveType.Cube, "Dealer Rail Accent", center + new Vector3(0f, 1.84f, 1.18f), new Vector3(4.5f, 0.05f, 0.12f), accentMaterial, table.transform);

        for (int x = -1; x <= 1; x++)
        {
            CreatePrimitive(
                PrimitiveType.Cylinder,
                "Player Stool",
                center + new Vector3(x * 1.7f, 0.42f, -1.85f),
                new Vector3(0.52f, 0.42f, 0.52f),
                woodMaterial,
                table.transform);
            CreatePrimitive(
                PrimitiveType.Cylinder,
                "Stool Cushion",
                center + new Vector3(x * 1.7f, 0.86f, -1.85f),
                new Vector3(0.55f, 0.08f, 0.55f),
                feltMaterial,
                table.transform);
        }

        GameObject dealerPosition = new GameObject("Dealer Interaction Position");
        dealerPosition.transform.SetParent(table.transform, false);
        dealerPosition.transform.localPosition = new Vector3(0f, 0f, -1.8f);
        BlackjackTableInteraction interaction = table.AddComponent<BlackjackTableInteraction>();
        interaction.Configure(playerController, dealerPosition.transform);
    }

    private static CasinoPlayerController CreatePlayer(Material playerMaterial)
    {
        GameObject player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 0f, -6f);
        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.38f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        controller.stepOffset = 0.3f;
        controller.slopeLimit = 45f;
        player.AddComponent<CasinoPlayerController>();

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Player Visual";
        visual.transform.SetParent(player.transform, false);
        visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        visual.transform.localScale = new Vector3(0.76f, 0.9f, 0.76f);
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.GetComponent<Renderer>().sharedMaterial = playerMaterial;
        return player.GetComponent<CasinoPlayerController>();
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 11f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 100f;
        camera.backgroundColor = new Color(0.055f, 0.075f, 0.09f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        cameraObject.AddComponent<AudioListener>();
        cameraObject.transform.position = new Vector3(0f, 16f, -9f);
        cameraObject.transform.LookAt(new Vector3(0f, 0.4f, 0f));
    }

    private static void CreateLighting()
    {
        GameObject sunObject = new GameObject("Casino Key Light");
        Light sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.82f, 0.62f);
        sun.intensity = 1.5f;
        sun.shadows = LightShadows.Soft;
        sunObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        for (int x = -1; x <= 1; x++)
        {
            GameObject lightObject = new GameObject("Warm Ceiling Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.68f, 0.36f);
            light.intensity = 7f;
            light.range = 10f;
            light.shadows = LightShadows.None;
            lightObject.transform.position = new Vector3(x * 7f, 6f, 0f);
        }
    }

    private static GameObject CreatePrimitive(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material, Transform parent)
    {
        GameObject primitive = GameObject.CreatePrimitive(type);
        primitive.name = name;
        primitive.transform.SetParent(parent, true);
        primitive.transform.position = position;
        primitive.transform.localScale = scale;
        primitive.GetComponent<Renderer>().sharedMaterial = material;
        return primitive;
    }

    private static Material GetOrCreateMaterial(string materialName, Color color)
    {
        EnsureFolder("Assets/Materials");
        EnsureFolder(MaterialsPath);

        string assetPath = $"{MaterialsPath}/{materialName}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                throw new System.InvalidOperationException("Could not find a supported shader for the prototype materials.");
            }

            material = new Material(shader);
            material.color = color;
            AssetDatabase.CreateAsset(material, assetPath);
        }

        return material;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            string parentPath = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            string folderName = Path.GetFileName(folderPath);
            if (string.IsNullOrEmpty(parentPath) || !AssetDatabase.IsValidFolder(parentPath))
            {
                throw new DirectoryNotFoundException($"Could not create folder {folderPath} because its parent does not exist.");
            }

            AssetDatabase.CreateFolder(parentPath, folderName);
        }
    }
}
