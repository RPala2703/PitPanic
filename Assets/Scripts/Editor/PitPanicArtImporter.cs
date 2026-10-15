using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Imports locally downloaded third-party art packs. It never downloads anything, never overwrites
/// existing files, and only reports counts (not asset names) so protected art is not exposed in logs.
/// </summary>
public sealed class PitPanicArtImporter : EditorWindow
{
    private const string ThirdPartyRoot = "Assets/ThirdParty";
    private const string UrpMaterialFolder = "Assets/Art/ImportedMaterialsURP";
    private const string RecordsPath = "UserSettings/PitPanicImportedPacks.json";
    private const string SearchFolderPref = "PitPanic.ArtImporter.SearchFolder";

    private sealed class PackDefinition
    {
        public string Id;
        public string DisplayName;
        public string[][] KeywordGroups;
        public bool Characters;
        public string Licence;
    }

    private sealed class FoundPack
    {
        public PackDefinition Definition;
        public string Path;
        public bool AlreadyImported;
    }

    [Serializable]
    private sealed class ImportRecord
    {
        public string packId;
        public string sha256;
        public string importedAt;
    }

    [Serializable]
    private sealed class ImportRecordFile
    {
        public List<ImportRecord> records = new List<ImportRecord>();
    }

    private static readonly PackDefinition[] Packs =
    {
        new PackDefinition
        {
            Id = "CasinoFree",
            DisplayName = "Casino FREE (ITHappy Studios)",
            KeywordGroups = new[] { new[] { "casino", "free" }, new[] { "ithappy", "casino" } },
            Licence = "Asset Store EULA. Do not commit to a public repository."
        },
        new PackDefinition
        {
            Id = "QuaterniusUniversalBaseCharacters",
            DisplayName = "Quaternius Universal Base Characters",
            KeywordGroups = new[] { new[] { "universal", "base", "character" }, new[] { "quaternius", "character" } },
            Characters = true,
            Licence = "Check the pack's own licence file before redistributing."
        },
        new PackDefinition
        {
            Id = "KayKitFurnitureBits",
            DisplayName = "KayKit Furniture Bits",
            KeywordGroups = new[] { new[] { "furniture", "bits" }, new[] { "kaykit", "furniture" } },
            Licence = "CC0 per KayKit; confirm on the download page."
        },
        new PackDefinition
        {
            Id = "KayKitCharacterAnimations",
            DisplayName = "KayKit Character Animations",
            KeywordGroups = new[] { new[] { "character", "animation" }, new[] { "kaykit", "animation" } },
            Characters = true,
            Licence = "CC0 per KayKit; confirm on the download page."
        }
    };

    private readonly List<FoundPack> found = new List<FoundPack>();
    private readonly List<string> report = new List<string>();
    private string searchFolder;
    private bool configureHumanoid = true;
    private bool createUrpMaterials = true;
    private Vector2 scroll;

    [MenuItem("Pit Panic/Import Free Art Assets")]
    public static void Open()
    {
        PitPanicArtImporter window = GetWindow<PitPanicArtImporter>("Import Free Art");
        window.minSize = new Vector2(560f, 420f);
        window.Scan();
    }

    private void OnEnable()
    {
        searchFolder = EditorPrefs.GetString(SearchFolderPref, string.Empty);
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Looks only for packs you have already downloaded. Missing packs must be downloaded manually " +
            "(account login may be required). Respect each licence, including NoAI terms: this tool imports " +
            "files through Unity and does not read or log asset names.", MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Extra search folder", GUILayout.Width(120f));
        EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(searchFolder) ? "(none)" : searchFolder,
            GUILayout.Height(18f));
        if (GUILayout.Button("Choose…", GUILayout.Width(80f)))
        {
            string chosen = EditorUtility.OpenFolderPanel("Folder containing downloaded packs", searchFolder, "");
            if (!string.IsNullOrEmpty(chosen))
            {
                searchFolder = chosen;
                EditorPrefs.SetString(SearchFolderPref, chosen);
                Scan();
            }
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("Also searched: ~/Downloads and <project>/ArtSource");
        configureHumanoid = EditorGUILayout.ToggleLeft(
            "Set imported character models to Humanoid rigs", configureHumanoid);
        createUrpMaterials = EditorGUILayout.ToggleLeft(
            "Create URP copies of non-URP materials (originals untouched)", createUrpMaterials);

        if (GUILayout.Button("Rescan"))
        {
            Scan();
        }

        EditorGUILayout.Space();
        foreach (PackDefinition pack in Packs)
        {
            FoundPack match = found.Find(f => f.Definition == pack);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(pack.DisplayName, GUILayout.Width(280f));
            if (match == null)
            {
                EditorGUILayout.LabelField("MISSING — download manually");
            }
            else if (match.AlreadyImported)
            {
                EditorGUILayout.LabelField("Already imported");
            }
            else
            {
                EditorGUILayout.LabelField("Found: " + Path.GetFileName(match.Path));
                if (GUILayout.Button("Import", GUILayout.Width(80f)))
                {
                    ImportPack(match);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Import all found packs"))
        {
            foreach (FoundPack pack in found.ToArray())
            {
                if (!pack.AlreadyImported)
                {
                    ImportPack(pack);
                }
            }
        }

        if (GUILayout.Button("Check URP compatibility of imported materials"))
        {
            CheckUrpCompatibility(createUrpMaterials);
        }

        EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (string line in report)
        {
            EditorGUILayout.LabelField(line, EditorStyles.wordWrappedLabel);
        }

        EditorGUILayout.EndScrollView();
    }

    private void Scan()
    {
        found.Clear();
        ImportRecordFile records = LoadRecords();
        foreach (string root in SearchRoots())
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                continue;
            }

            foreach (string file in files)
            {
                string extension = Path.GetExtension(file).ToLowerInvariant();
                if (extension != ".unitypackage" && extension != ".zip")
                {
                    continue;
                }

                PackDefinition definition = Match(Path.GetFileNameWithoutExtension(file));
                if (definition == null || found.Exists(f => f.Definition == definition))
                {
                    continue;
                }

                string hash = Hash(file);
                found.Add(new FoundPack
                {
                    Definition = definition,
                    Path = file,
                    AlreadyImported = records.records.Exists(r => r.packId == definition.Id && r.sha256 == hash)
                });
            }
        }

        report.Clear();
        report.Add($"Scan complete: {found.Count} of {Packs.Length} supported packs found.");
        foreach (PackDefinition pack in Packs)
        {
            if (!found.Exists(f => f.Definition == pack))
            {
                report.Add($"Missing: {pack.DisplayName} (.unitypackage or .zip). Download it and place it in ~/Downloads or ArtSource.");
            }
        }

        Repaint();
    }

    private IEnumerable<string> SearchRoots()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return Path.Combine(home, "Downloads");
        yield return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ArtSource"));
        if (!string.IsNullOrEmpty(searchFolder))
        {
            yield return searchFolder;
        }
    }

    private static PackDefinition Match(string fileName)
    {
        string lower = fileName.ToLowerInvariant().Replace('_', ' ').Replace('-', ' ');
        foreach (PackDefinition pack in Packs)
        {
            foreach (string[] group in pack.KeywordGroups)
            {
                bool all = true;
                foreach (string keyword in group)
                {
                    all &= lower.Contains(keyword);
                }

                if (all)
                {
                    return pack;
                }
            }
        }

        return null;
    }

    private void ImportPack(FoundPack pack)
    {
        string name = pack.Definition.DisplayName;
        report.Add($"Importing {name}…");
        try
        {
            string extension = Path.GetExtension(pack.Path).ToLowerInvariant();
            if (extension == ".unitypackage")
            {
                ImportUnityPackage(pack);
            }
            else
            {
                ImportZip(pack);
                FinishImport(pack);
            }
        }
        catch (Exception exception)
        {
            report.Add($"ERROR importing {name}: {exception.Message}");
            Debug.LogError($"[PitPanic] Import failed for {name}: {exception}");
        }
    }

    private void ImportUnityPackage(FoundPack pack)
    {
        AssetDatabase.importPackageCompleted += OnCompleted;
        AssetDatabase.importPackageFailed += OnFailed;
        AssetDatabase.importPackageCancelled += OnCancelled;

        void Unsubscribe()
        {
            AssetDatabase.importPackageCompleted -= OnCompleted;
            AssetDatabase.importPackageFailed -= OnFailed;
            AssetDatabase.importPackageCancelled -= OnCancelled;
        }

        void OnCompleted(string packageName)
        {
            Unsubscribe();
            FinishImport(pack);
            Repaint();
        }

        void OnFailed(string packageName, string errorMessage)
        {
            Unsubscribe();
            report.Add($"ERROR: {pack.Definition.DisplayName} failed to import: {errorMessage}");
            Repaint();
        }

        void OnCancelled(string packageName)
        {
            Unsubscribe();
            report.Add($"Cancelled: {pack.Definition.DisplayName}.");
            Repaint();
        }

        AssetDatabase.ImportPackage(pack.Path, false);
    }

    private static void ImportZip(FoundPack pack)
    {
        string destination = $"{ThirdPartyRoot}/{pack.Definition.Id}";
        string destinationFull = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(destination);
        using (ZipArchive archive = ZipFile.OpenRead(pack.Path))
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name) || entry.FullName.StartsWith("__MACOSX", StringComparison.Ordinal))
                {
                    continue;
                }

                string target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
                if (!target.StartsWith(destinationFull, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Archive contains an unsafe path and was skipped.");
                }

                if (File.Exists(target))
                {
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target));
                entry.ExtractToFile(target, false);
            }
        }

        AssetDatabase.Refresh();
    }

    private void FinishImport(FoundPack pack)
    {
        AssetDatabase.Refresh();
        string folder = AssetDatabase.IsValidFolder($"{ThirdPartyRoot}/{pack.Definition.Id}")
            ? $"{ThirdPartyRoot}/{pack.Definition.Id}"
            : "Assets";
        int models = 0;
        int textures = 0;
        int materials = 0;
        int rigged = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
        {
            models++;
            if (configureHumanoid && pack.Definition.Characters &&
                AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) is ModelImporter importer &&
                importer.animationType == ModelImporterAnimationType.Generic && importer.importAnimation)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.SaveAndReimport();
                rigged++;
            }
        }

        textures = AssetDatabase.FindAssets("t:Texture2D", new[] { folder }).Length;
        materials = AssetDatabase.FindAssets("t:Material", new[] { folder }).Length;
        SaveRecord(pack);
        pack.AlreadyImported = true;
        report.Add($"Imported {pack.Definition.DisplayName}: {models} models, {textures} textures, " +
                   $"{materials} materials, {rigged} set to Humanoid.");
        report.Add($"Licence note: {pack.Definition.Licence}");
        CheckUrpCompatibility(createUrpMaterials);
    }

    private void CheckUrpCompatibility(bool createCopies)
    {
        bool urpActive = GraphicsSettings.currentRenderPipeline != null;
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (!urpActive || urpLit == null)
        {
            report.Add("WARNING: URP is not active or its Lit shader was not found; materials cannot be checked.");
            return;
        }

        int incompatible = 0;
        int created = 0;
        if (!Directory.Exists(ThirdPartyRoot))
        {
            report.Add("URP check: no imported third-party content yet.");
            return;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { ThirdPartyRoot, "Assets/Asset Store" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || material.shader == null ||
                material.shader.name.StartsWith("Universal Render Pipeline", StringComparison.Ordinal) ||
                material.shader.name.StartsWith("Shader Graphs", StringComparison.Ordinal))
            {
                continue;
            }

            incompatible++;
            if (!createCopies)
            {
                continue;
            }

            string copyPath = $"{UrpMaterialFolder}/{Path.GetFileNameWithoutExtension(path)}_{guid.Substring(0, 6)}_URP.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(copyPath) != null)
            {
                continue;
            }

            EnsureFolder(UrpMaterialFolder);
            Material copy = new Material(urpLit) { name = Path.GetFileNameWithoutExtension(copyPath) };
            if (material.HasProperty("_Color"))
            {
                copy.SetColor("_BaseColor", material.GetColor("_Color"));
            }

            if (material.HasProperty("_MainTex"))
            {
                copy.SetTexture("_BaseMap", material.GetTexture("_MainTex"));
            }

            AssetDatabase.CreateAsset(copy, copyPath);
            created++;
        }

        AssetDatabase.SaveAssets();
        report.Add(incompatible == 0
            ? "URP check: all imported materials use URP shaders."
            : $"URP check: {incompatible} non-URP materials; {created} URP copies created in {UrpMaterialFolder}. " +
              "Assign the copies (or use Edit > Rendering > Materials > Convert) before using them in scenes.");
    }

    private static void EnsureFolder(string folder)
    {
        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static string Hash(string path)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
        {
            StringBuilder builder = new StringBuilder();
            foreach (byte b in sha.ComputeHash(stream))
            {
                builder.Append(b.ToString("x2"));
            }

            return builder.ToString();
        }
    }

    private static ImportRecordFile LoadRecords()
    {
        if (!File.Exists(RecordsPath))
        {
            return new ImportRecordFile();
        }

        try
        {
            return JsonUtility.FromJson<ImportRecordFile>(File.ReadAllText(RecordsPath)) ?? new ImportRecordFile();
        }
        catch (ArgumentException)
        {
            return new ImportRecordFile();
        }
    }

    private static void SaveRecord(FoundPack pack)
    {
        ImportRecordFile file = LoadRecords();
        file.records.RemoveAll(r => r.packId == pack.Definition.Id);
        file.records.Add(new ImportRecord
        {
            packId = pack.Definition.Id,
            sha256 = Hash(pack.Path),
            importedAt = DateTime.UtcNow.ToString("o")
        });
        Directory.CreateDirectory(Path.GetDirectoryName(RecordsPath));
        File.WriteAllText(RecordsPath, JsonUtility.ToJson(file, true));
    }
}
