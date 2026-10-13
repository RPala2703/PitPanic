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

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.48f, 0.55f, 0.62f);
        RenderSettings.ambientEquatorColor = new Color(0.30f, 0.34f, 0.35f);
        RenderSettings.ambientGroundColor = new Color(0.12f, 0.13f, 0.13f);
        RenderSettings.fog = false;

        CreateLighting();
        CreateFloor(floorMaterial, trimMaterial);
        CreateBlackjackTable(woodMaterial, feltMaterial, trimMaterial, accentMaterial);
        CreatePlayer(playerMaterial);
        CreateCamera();

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            throw new IOException($"Could not save the prototype scene to {ScenePath}.");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Pit Panic prototype scene saved to {ScenePath}.");
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

    private static void CreateBlackjackTable(Material woodMaterial, Material feltMaterial, Material trimMaterial, Material accentMaterial)
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
    }

    private static void CreatePlayer(Material playerMaterial)
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
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 12f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 100f;
        camera.backgroundColor = new Color(0.055f, 0.075f, 0.09f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        cameraObject.AddComponent<AudioListener>();
        cameraObject.transform.position = new Vector3(13f, 17f, -16f);
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
            AssetDatabase.CreateAsset(material, assetPath);
        }

        material.color = color;
        EditorUtility.SetDirty(material);
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
