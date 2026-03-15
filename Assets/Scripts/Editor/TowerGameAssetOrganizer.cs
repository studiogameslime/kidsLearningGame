using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Organizes TowerGame assets into categorized folders, sets import settings,
/// and creates block prefabs.
/// Run via Tools > Kids Learning Game > Organize Tower Assets.
/// </summary>
public class TowerGameAssetOrganizer : EditorWindow
{
    private const string BasePath = "Assets/Art/TowerGame";
    private const string WoodSrc = "Assets/Art/TowerGame/Wood elements";
    private const string StoneSrc = "Assets/Art/TowerGame/Stone elements";
    private const string PrefabPath = "Assets/Prefabs/TowerBlocks";

    // Block indices (rectangular stacking pieces)
    private static readonly int[] BlockIndices =
    {
        11, 12, 13, 14, 15, 16, 18, 20, 21, 22, 23, 24, 25, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54
    };

    // Frame indices (hollow rectangles)
    private static readonly int[] FrameIndices = { 26, 27, 28, 29, 30, 31 };

    // Decoration indices (circles, triangles, small shapes)
    private static readonly int[] DecoIndices =
    {
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 32, 33, 34, 35, 36, 37, 38, 39
    };

    // Rename mapping for key block sprites: index → friendly name
    private static readonly (int idx, string name)[] WoodRenames =
    {
        (11, "Wood_Block_Tall_Thin"),
        (12, "Wood_Block_Medium"),
        (13, "Wood_Block_Short"),
        (14, "Wood_Block_Wide"),
        (15, "Wood_Block_Long"),
        (16, "Wood_Block_Square"),
        (18, "Wood_Block_Flat"),
        (20, "Wood_Block_Plank_Long"),
        (21, "Wood_Block_Plank_Medium"),
        (22, "Wood_Block_Plank_Short"),
        (23, "Wood_Block_Plank_Wide"),
    };

    private static readonly (int idx, string name)[] StoneRenames =
    {
        (11, "Stone_Block_Tall_Thin"),
        (12, "Stone_Block_Medium"),
        (13, "Stone_Block_Short"),
        (14, "Stone_Block_Wide"),
        (15, "Stone_Block_Long"),
        (16, "Stone_Block_Square"),
        (18, "Stone_Block_Flat"),
        (20, "Stone_Block_Plank_Long"),
        (21, "Stone_Block_Plank_Medium"),
        (22, "Stone_Block_Plank_Short"),
        (23, "Stone_Block_Plank_Wide"),
    };

    [MenuItem("Tools/Kids Learning Game/Organize Tower Assets")]
    public static void Organize()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Tower Assets", "Creating folders…", 0.1f);
            CreateFolders();

            EditorUtility.DisplayProgressBar("Tower Assets", "Sorting wood elements…", 0.2f);
            SortElements("elementWood", WoodSrc);

            EditorUtility.DisplayProgressBar("Tower Assets", "Sorting stone elements…", 0.4f);
            SortElements("elementStone", StoneSrc);

            EditorUtility.DisplayProgressBar("Tower Assets", "Setting import settings…", 0.6f);
            SetImportSettings();

            EditorUtility.DisplayProgressBar("Tower Assets", "Renaming key blocks…", 0.7f);
            RenameBlocks();

            EditorUtility.DisplayProgressBar("Tower Assets", "Creating prefabs…", 0.8f);
            CreatePrefabs();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Tower assets organized successfully!");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void CreateFolders()
    {
        EnsureFolder($"{BasePath}/TowerBlocks");
        EnsureFolder($"{BasePath}/TowerFrames");
        EnsureFolder($"{BasePath}/TowerDecorations");
        EnsureFolder($"{BasePath}/TowerUnused");
        EnsureFolder(PrefabPath);
    }

    private static void SortElements(string prefix, string srcFolder)
    {
        if (!AssetDatabase.IsValidFolder(srcFolder)) return;

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { srcFolder });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string filename = Path.GetFileNameWithoutExtension(path);

            // Extract index from filename (e.g., elementWood011 → 11)
            string numStr = filename.Replace(prefix, "");
            if (!int.TryParse(numStr, out int idx)) continue;

            string destFolder;
            if (System.Array.Exists(BlockIndices, i => i == idx))
                destFolder = $"{BasePath}/TowerBlocks";
            else if (System.Array.Exists(FrameIndices, i => i == idx))
                destFolder = $"{BasePath}/TowerFrames";
            else if (System.Array.Exists(DecoIndices, i => i == idx))
                destFolder = $"{BasePath}/TowerDecorations";
            else
                destFolder = $"{BasePath}/TowerUnused";

            string destPath = $"{destFolder}/{Path.GetFileName(path)}";
            if (path != destPath && !File.Exists(destPath.Replace("Assets/", "").Replace("/", "\\")))
            {
                string result = AssetDatabase.MoveAsset(path, destPath);
                if (!string.IsNullOrEmpty(result))
                    Debug.LogWarning($"Failed to move {path}: {result}");
            }
        }
    }

    private static void SetImportSettings()
    {
        string[] folders =
        {
            $"{BasePath}/TowerBlocks",
            $"{BasePath}/TowerFrames",
            $"{BasePath}/TowerDecorations",
        };

        foreach (string folder in folders)
        {
            if (!AssetDatabase.IsValidFolder(folder)) continue;
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                bool changed = false;

                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    changed = true;
                }
                if (importer.spritePixelsPerUnit != 100)
                {
                    importer.spritePixelsPerUnit = 100;
                    changed = true;
                }
                if (importer.filterMode != FilterMode.Point)
                {
                    importer.filterMode = FilterMode.Point;
                    changed = true;
                }

                var settings = importer.GetDefaultPlatformTextureSettings();
                if (settings.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    settings.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SetPlatformTextureSettings(settings);
                    changed = true;
                }

                var texSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(texSettings);
                if (texSettings.spriteMeshType != SpriteMeshType.FullRect)
                {
                    texSettings.spriteMeshType = SpriteMeshType.FullRect;
                    importer.SetTextureSettings(texSettings);
                    changed = true;
                }

                if (changed)
                    importer.SaveAndReimport();
            }
        }
    }

    private static void RenameBlocks()
    {
        foreach (var (idx, name) in WoodRenames)
        {
            string src = $"{BasePath}/TowerBlocks/elementWood{idx:D3}.png";
            string dst = $"{BasePath}/TowerBlocks/{name}.png";
            if (File.Exists(src.Replace("/", "\\")) || AssetDatabase.LoadAssetAtPath<Texture2D>(src) != null)
            {
                string result = AssetDatabase.MoveAsset(src, dst);
                if (!string.IsNullOrEmpty(result))
                    Debug.LogWarning($"Rename failed {src} → {dst}: {result}");
            }
        }

        foreach (var (idx, name) in StoneRenames)
        {
            string src = $"{BasePath}/TowerBlocks/elementStone{idx:D3}.png";
            string dst = $"{BasePath}/TowerBlocks/{name}.png";
            if (File.Exists(src.Replace("/", "\\")) || AssetDatabase.LoadAssetAtPath<Texture2D>(src) != null)
            {
                string result = AssetDatabase.MoveAsset(src, dst);
                if (!string.IsNullOrEmpty(result))
                    Debug.LogWarning($"Rename failed {src} → {dst}: {result}");
            }
        }
    }

    private static void CreatePrefabs()
    {
        CreateBlockPrefab("WoodBlock", $"{BasePath}/TowerBlocks/Wood_Block_Plank_Long.png");
        CreateBlockPrefab("StoneBlock", $"{BasePath}/TowerBlocks/Stone_Block_Plank_Long.png");
    }

    private static void CreateBlockPrefab(string prefabName, string spritePath)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        // Fallback if rename hasn't happened yet
        if (sprite == null)
        {
            string altPath = spritePath.Contains("Wood")
                ? $"{BasePath}/TowerBlocks/elementWood020.png"
                : $"{BasePath}/TowerBlocks/elementStone020.png";
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(altPath);
        }

        var go = new GameObject(prefabName);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 10;

        var col = go.AddComponent<BoxCollider2D>();
        if (sprite != null)
        {
            col.size = sprite.bounds.size;
            col.offset = Vector2.zero;
        }

        string path = $"{PrefabPath}/{prefabName}.prefab";
        EnsureFolder(PrefabPath);

        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
            PrefabUtility.SaveAsPrefabAsset(go, path);
        else
            PrefabUtility.SaveAsPrefabAsset(go, path);

        Object.DestroyImmediate(go);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
