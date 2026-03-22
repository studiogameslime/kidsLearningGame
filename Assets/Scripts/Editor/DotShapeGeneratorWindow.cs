using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Editor window to generate DotShapeData assets from animal sprites
/// and built-in geometric shapes.
/// Tools > Kids Learning Game > Dot Shape Generator
///
/// Features:
/// - Batch-generate all 19 animal shapes from sprites
/// - Generate built-in geometric shapes (circle, square, triangle, etc.)
/// - Auto-classify complexity (Low/Medium/High)
/// - Visual preview with difficulty toggle
/// </summary>
public class DotShapeGeneratorWindow : EditorWindow
{
    private Sprite singleSprite;
    private string singleName = "";
    private string singleHebrew = "";
    private int previewDifficulty = 0;
    private DotShapeData previewData;
    private Vector2 scrollPos;

    // Point counts per difficulty
    private int easyCount = 10;
    private int mediumCount = 18;
    private int hardCount = 28;

    private static readonly string[] ComplexityLabels = { "LOW", "MEDIUM", "HIGH" };
    private static readonly Color[] ComplexityColors =
    {
        new Color(0.3f, 0.9f, 0.3f),  // green
        new Color(1f, 0.8f, 0.2f),    // yellow
        new Color(1f, 0.4f, 0.3f),    // red
    };

    // ══════════════════════════════════════════
    //  ANIMAL DEFINITIONS
    // ══════════════════════════════════════════

    private static readonly (string id, string hebrew)[] Animals = new[]
    {
        ("Bear", "\u05D3\u05D5\u05D1"),
        ("Bird", "\u05E6\u05D9\u05E4\u05D5\u05E8"),
        ("Cat", "\u05D7\u05EA\u05D5\u05DC"),
        ("Chicken", "\u05EA\u05E8\u05E0\u05D2\u05D5\u05DC"),
        ("Cow", "\u05E4\u05E8\u05D4"),
        ("Dog", "\u05DB\u05DC\u05D1"),
        ("Donkey", "\u05D7\u05DE\u05D5\u05E8"),
        ("Duck", "\u05D1\u05E8\u05D5\u05D6"),
        ("Elephant", "\u05E4\u05D9\u05DC"),
        ("Fish", "\u05D3\u05D2"),
        ("Frog", "\u05E6\u05E4\u05E8\u05D3\u05E2"),
        ("Giraffe", "\u05D2\u05F3\u05D9\u05E8\u05E4\u05D4"),
        ("Horse", "\u05E1\u05D5\u05E1"),
        ("Lion", "\u05D0\u05E8\u05D9\u05D4"),
        ("Monkey", "\u05E7\u05D5\u05E3"),
        ("Sheep", "\u05DB\u05D1\u05E9\u05D4"),
        ("Snake", "\u05E0\u05D7\u05E9"),
        ("Turtle", "\u05E6\u05D1"),
        ("Zebra", "\u05D6\u05D1\u05E8\u05D4"),
    };

    private static readonly Color[] AnimalColors = new[]
    {
        new Color(0.72f, 0.52f, 0.3f),   // Bear
        new Color(0.4f, 0.78f, 0.9f),    // Bird
        new Color(0.95f, 0.65f, 0.3f),   // Cat
        new Color(0.95f, 0.75f, 0.2f),   // Chicken
        new Color(0.85f, 0.85f, 0.9f),   // Cow
        new Color(0.72f, 0.52f, 0.3f),   // Dog
        new Color(0.6f, 0.55f, 0.5f),    // Donkey
        new Color(1f, 0.84f, 0f),        // Duck
        new Color(0.65f, 0.65f, 0.72f),  // Elephant
        new Color(0.3f, 0.75f, 0.95f),   // Fish
        new Color(0.45f, 0.78f, 0.35f),  // Frog
        new Color(0.95f, 0.80f, 0.3f),   // Giraffe
        new Color(0.72f, 0.45f, 0.25f),  // Horse
        new Color(0.92f, 0.65f, 0.2f),   // Lion
        new Color(0.72f, 0.52f, 0.3f),   // Monkey
        new Color(0.85f, 0.85f, 0.85f),  // Sheep
        new Color(0.45f, 0.75f, 0.35f),  // Snake
        new Color(0.45f, 0.78f, 0.45f),  // Turtle
        new Color(0.85f, 0.85f, 0.9f),   // Zebra
    };

    // ══════════════════════════════════════════
    //  BUILT-IN GEOMETRIC SHAPES
    //  These have hardcoded points and complexity = 0 (Low)
    // ══════════════════════════════════════════

    private struct GeometricShape
    {
        public string id;
        public string hebrew;
        public Color color;
        public Vector2[] points; // canonical shape, will be scaled per difficulty

        public GeometricShape(string id, string hebrew, Color color, Vector2[] points)
        {
            this.id = id;
            this.hebrew = hebrew;
            this.color = color;
            this.points = points;
        }
    }

    private static readonly GeometricShape[] GeometricShapes = new GeometricShape[]
    {
        // Circle (24 points, will be resampled down)
        new GeometricShape("Circle", "\u05E2\u05D9\u05D2\u05D5\u05DC",
            new Color(0.3f, 0.75f, 0.95f),
            GenerateCircle(24)),

        // Square
        new GeometricShape("Square", "\u05E8\u05D9\u05D1\u05D5\u05E2",
            new Color(0.55f, 0.45f, 0.90f),
            new Vector2[] {
                new Vector2(0.25f, 0.75f), new Vector2(0.75f, 0.75f),
                new Vector2(0.75f, 0.25f), new Vector2(0.25f, 0.25f)
            }),

        // Triangle
        new GeometricShape("Triangle", "\u05DE\u05E9\u05D5\u05DC\u05E9",
            new Color(0.35f, 0.75f, 0.35f),
            new Vector2[] {
                new Vector2(0.50f, 0.85f),
                new Vector2(0.80f, 0.22f),
                new Vector2(0.20f, 0.22f)
            }),

        // Rectangle (wider than tall)
        new GeometricShape("Rectangle", "\u05DE\u05DC\u05D1\u05DF",
            new Color(0.85f, 0.55f, 0.25f),
            new Vector2[] {
                new Vector2(0.18f, 0.70f), new Vector2(0.82f, 0.70f),
                new Vector2(0.82f, 0.30f), new Vector2(0.18f, 0.30f)
            }),

        // Diamond
        new GeometricShape("Diamond", "\u05D9\u05D4\u05DC\u05D5\u05DD",
            new Color(0.45f, 0.85f, 0.85f),
            new Vector2[] {
                new Vector2(0.50f, 0.88f), new Vector2(0.78f, 0.50f),
                new Vector2(0.50f, 0.12f), new Vector2(0.22f, 0.50f)
            }),

        // Star (10 points — alternating outer/inner)
        new GeometricShape("Star", "\u05DB\u05D5\u05DB\u05D1",
            new Color(1f, 0.84f, 0f),
            GenerateStar(5, 0.38f, 0.18f)),

        // Heart
        new GeometricShape("Heart", "\u05DC\u05D1",
            new Color(0.96f, 0.30f, 0.45f),
            new Vector2[] {
                new Vector2(0.50f, 0.25f),
                new Vector2(0.35f, 0.38f),
                new Vector2(0.22f, 0.55f),
                new Vector2(0.22f, 0.70f),
                new Vector2(0.32f, 0.82f),
                new Vector2(0.50f, 0.72f),
                new Vector2(0.68f, 0.82f),
                new Vector2(0.78f, 0.70f),
                new Vector2(0.78f, 0.55f),
                new Vector2(0.65f, 0.38f),
            }),
    };

    private static Vector2[] GenerateCircle(int segments)
    {
        var pts = new Vector2[segments];
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            // Start from top, go clockwise
            float x = 0.5f + 0.32f * Mathf.Sin(angle);
            float y = 0.5f + 0.32f * Mathf.Cos(angle);
            pts[i] = new Vector2(x, y);
        }
        return pts;
    }

    private static Vector2[] GenerateStar(int points, float outerR, float innerR)
    {
        var pts = new Vector2[points * 2];
        for (int i = 0; i < points * 2; i++)
        {
            float angle = (float)i / (points * 2) * Mathf.PI * 2f - Mathf.PI / 2f;
            float r = (i % 2 == 0) ? outerR : innerR;
            pts[i] = new Vector2(0.5f + r * Mathf.Cos(angle), 0.5f + r * Mathf.Sin(angle));
        }
        return pts;
    }

    // ══════════════════════════════════════════
    //  EDITOR WINDOW
    // ══════════════════════════════════════════

    [MenuItem("Tools/Kids Learning Game/Dot Shape Generator")]
    public static void ShowWindow()
    {
        GetWindow<DotShapeGeneratorWindow>("Dot Shape Generator");
    }

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        EditorGUILayout.LabelField("Dot Shape Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Point count settings
        EditorGUILayout.LabelField("Point Counts", EditorStyles.boldLabel);
        easyCount = EditorGUILayout.IntSlider("Easy", easyCount, 6, 15);
        mediumCount = EditorGUILayout.IntSlider("Medium", mediumCount, 12, 24);
        hardCount = EditorGUILayout.IntSlider("Hard", hardCount, 20, 35);
        EditorGUILayout.Space();

        // Batch generation
        EditorGUILayout.LabelField("Batch Generation", EditorStyles.boldLabel);
        if (GUILayout.Button("Generate All (Animals + Geometric)", GUILayout.Height(35)))
            EditorApplication.delayCall += GenerateAll;

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Animals Only"))
            EditorApplication.delayCall += GenerateAllAnimals;
        if (GUILayout.Button("Geometric Only"))
            EditorApplication.delayCall += GenerateAllGeometric;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();

        // Single sprite generation
        EditorGUILayout.LabelField("Single Sprite", EditorStyles.boldLabel);
        singleSprite = (Sprite)EditorGUILayout.ObjectField("Sprite", singleSprite, typeof(Sprite), false);
        singleName = EditorGUILayout.TextField("Shape ID", singleName);
        singleHebrew = EditorGUILayout.TextField("Hebrew Name", singleHebrew);
        if (GUILayout.Button("Generate Single") && singleSprite != null)
        {
            var sp = singleSprite;
            var n = singleName;
            var h = singleHebrew;
            EditorApplication.delayCall += () =>
            {
                previewData = GenerateAnimalShape(sp, n, h, null, Color.white);
                Repaint();
            };
        }
        EditorGUILayout.Space();

        // Preview
        if (previewData != null)
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            // Show complexity
            int cx = previewData.complexity;
            Color prevColor = GUI.color;
            GUI.color = ComplexityColors[cx];
            EditorGUILayout.LabelField($"Complexity: {ComplexityLabels[cx]} (score: {previewData.complexityScore:F3})");
            GUI.color = prevColor;

            previewDifficulty = GUILayout.Toolbar(previewDifficulty, new[] { "Easy", "Medium", "Hard" });

            bool allowed = previewData.IsAllowedForDifficulty(previewDifficulty);
            if (!allowed)
            {
                EditorGUILayout.HelpBox(
                    $"This shape would NOT appear in {new[]{"Easy","Medium","Hard"}[previewDifficulty]} " +
                    $"difficulty (complexity too high).", MessageType.Warning);
            }

            var pts = previewData.GetPoints(previewDifficulty);
            if (pts != null && pts.Length > 0)
            {
                EditorGUILayout.LabelField($"Points: {pts.Length}");
                DrawPreview(pts);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    // ══════════════════════════════════════════
    //  GENERATION
    // ══════════════════════════════════════════

    private void GenerateAll()
    {
        GenerateAllGeometric();
        GenerateAllAnimals();
    }

    private void GenerateAllGeometric()
    {
        EnsureFolder("Assets/Resources/DotShapes");

        for (int i = 0; i < GeometricShapes.Length; i++)
        {
            var shape = GeometricShapes[i];
            EditorUtility.DisplayProgressBar("Generating Geometric Shapes",
                $"{shape.id} ({i + 1}/{GeometricShapes.Length})", (float)i / GeometricShapes.Length);

            GenerateGeometricShape(shape);
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Generated {GeometricShapes.Length} geometric shape assets.");
    }

    private void GenerateAllAnimals()
    {
        EnsureFolder("Assets/Resources/DotShapes");

        int total = Animals.Length;
        for (int i = 0; i < total; i++)
        {
            var (id, hebrew) = Animals[i];
            EditorUtility.DisplayProgressBar("Generating Animal Shapes",
                $"{id} ({i + 1}/{total})", (float)i / total);

            string spritePath = $"Assets/Art/Animals/{id}/Art/{id}Sprite.png";
            if (id == "Giraffe") spritePath = "Assets/Art/Animals/Giraffe/Art/Giraffe.png";

            var sprite = LoadSprite(spritePath);
            if (sprite == null)
            {
                Debug.LogWarning($"Sprite not found: {spritePath}");
                continue;
            }

            Color color = i < AnimalColors.Length ? AnimalColors[i] : Color.white;
            GenerateAnimalShape(sprite, id, hebrew, id, color);
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Generated {total} animal shape assets.");
    }

    /// <summary>Generate a DotShapeData from a sprite with auto-complexity.</summary>
    private DotShapeData GenerateAnimalShape(Sprite sprite, string shapeId, string hebrew,
        string animalId, Color color)
    {
        EnsureFolder("Assets/Resources/DotShapes");

        // Extract at medium count to get complexity
        var medResult = SpriteContourExtractor.ExtractWithComplexity(sprite, mediumCount);
        if (medResult.points == null)
        {
            Debug.LogError($"Failed to extract contour from {sprite.name}");
            return null;
        }

        Vector2[] easy = SpriteContourExtractor.Extract(sprite, easyCount);
        Vector2[] hard = SpriteContourExtractor.Extract(sprite, hardCount);

        if (easy == null || hard == null)
        {
            Debug.LogError($"Failed to extract contour from {sprite.name}");
            return null;
        }

        string assetPath = $"Assets/Resources/DotShapes/{shapeId}.asset";
        var data = AssetDatabase.LoadAssetAtPath<DotShapeData>(assetPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<DotShapeData>();
            AssetDatabase.CreateAsset(data, assetPath);
        }

        data.shapeId = shapeId;
        data.hebrewName = hebrew;
        data.animalId = animalId;
        data.sourceSprite = sprite;
        data.lineColor = color;
        data.easyPoints = easy;
        data.mediumPoints = medResult.points;
        data.hardPoints = hard;
        data.complexityScore = medResult.complexityScore;
        data.complexity = medResult.complexityLevel;

        EditorUtility.SetDirty(data);
        Debug.Log($"  {shapeId}: complexity={ComplexityLabels[data.complexity]} " +
                  $"(score={data.complexityScore:F3}), pts={easy.Length}/{medResult.points.Length}/{hard.Length}");
        return data;
    }

    /// <summary>Generate a DotShapeData from a built-in geometric shape definition.</summary>
    private void GenerateGeometricShape(GeometricShape shape)
    {
        EnsureFolder("Assets/Resources/DotShapes");

        Vector2[] canonical = shape.points;

        // For geometric shapes, resample to each difficulty's point count
        Vector2[] easy = ResamplePolygon(canonical, easyCount);
        Vector2[] medium = ResamplePolygon(canonical, mediumCount);
        Vector2[] hard = ResamplePolygon(canonical, hardCount);

        // Fit to play area
        easy = FitToPlayArea(easy);
        medium = FitToPlayArea(medium);
        hard = FitToPlayArea(hard);

        string assetPath = $"Assets/Resources/DotShapes/{shape.id}.asset";
        var data = AssetDatabase.LoadAssetAtPath<DotShapeData>(assetPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<DotShapeData>();
            AssetDatabase.CreateAsset(data, assetPath);
        }

        data.shapeId = shape.id;
        data.hebrewName = shape.hebrew;
        data.animalId = null;
        data.sourceSprite = null;
        data.lineColor = shape.color;
        data.easyPoints = easy;
        data.mediumPoints = medium;
        data.hardPoints = hard;
        data.complexity = 0; // geometric shapes are always Low
        data.complexityScore = 0f;

        EditorUtility.SetDirty(data);
        Debug.Log($"  {shape.id}: complexity=LOW (geometric), pts={easy.Length}/{medium.Length}/{hard.Length}");
    }

    /// <summary>
    /// Resample a polygon to exactly targetCount points,
    /// distributing evenly along the perimeter.
    /// </summary>
    private static Vector2[] ResamplePolygon(Vector2[] polygon, int targetCount)
    {
        if (polygon.Length >= targetCount)
            return polygon;

        int n = polygon.Length;

        // For geometric shapes: keep all original vertices, distribute extra points
        // evenly along edges proportional to edge length.
        int extraPoints = targetCount - n;
        float[] edgeLengths = new float[n];
        float totalLen = 0f;
        for (int i = 0; i < n; i++)
        {
            edgeLengths[i] = Vector2.Distance(polygon[i], polygon[(i + 1) % n]);
            totalLen += edgeLengths[i];
        }
        if (totalLen < 0.001f) return polygon;

        // Distribute extra points per edge proportional to length
        int[] pointsPerEdge = new int[n];
        int assigned = 0;
        for (int i = 0; i < n; i++)
        {
            pointsPerEdge[i] = Mathf.RoundToInt(extraPoints * (edgeLengths[i] / totalLen));
            assigned += pointsPerEdge[i];
        }
        // Fix rounding errors
        int diff = extraPoints - assigned;
        for (int i = 0; diff != 0; i = (i + 1) % n)
        {
            if (diff > 0) { pointsPerEdge[i]++; diff--; }
            else { if (pointsPerEdge[i] > 0) { pointsPerEdge[i]--; diff++; } }
        }

        var result = new System.Collections.Generic.List<Vector2>();
        for (int i = 0; i < n; i++)
        {
            result.Add(polygon[i]); // always include the vertex
            int subdivs = pointsPerEdge[i];
            int nextIdx = (i + 1) % n;
            for (int s = 1; s <= subdivs; s++)
            {
                float t = (float)s / (subdivs + 1);
                result.Add(Vector2.Lerp(polygon[i], polygon[nextIdx], t));
            }
        }

        return result.ToArray();
    }

    /// <summary>Scale and center points to fit 0.15–0.85 range.</summary>
    private static Vector2[] FitToPlayArea(Vector2[] points)
    {
        if (points == null || points.Length == 0) return points;

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var p in points)
        {
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }

        float w = maxX - minX;
        float h = maxY - minY;
        if (w < 0.001f || h < 0.001f) return points;

        float targetRange = 0.70f;
        float scale = targetRange / Mathf.Max(w, h);
        float centerX = (minX + maxX) * 0.5f;
        float centerY = (minY + maxY) * 0.5f;

        var result = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            result[i] = new Vector2(
                (points[i].x - centerX) * scale + 0.5f,
                (points[i].y - centerY) * scale + 0.5f);
        }
        return result;
    }

    // ══════════════════════════════════════════
    //  PREVIEW
    // ══════════════════════════════════════════

    private void DrawPreview(Vector2[] points)
    {
        Rect area = GUILayoutUtility.GetRect(300, 300);
        EditorGUI.DrawRect(area, new Color(0.05f, 0.1f, 0.25f));

        float padding = 10f;
        float drawW = area.width - padding * 2;
        float drawH = area.height - padding * 2;

        // Lines
        Handles.color = new Color(1f, 0.95f, 0.75f, 0.5f);
        for (int i = 0; i < points.Length; i++)
        {
            int next = (i + 1) % points.Length;
            Vector2 a = new Vector2(
                area.x + padding + points[i].x * drawW,
                area.y + padding + (1f - points[i].y) * drawH);
            Vector2 b = new Vector2(
                area.x + padding + points[next].x * drawW,
                area.y + padding + (1f - points[next].y) * drawH);
            Handles.DrawLine(new Vector3(a.x, a.y, 0), new Vector3(b.x, b.y, 0));
        }

        // Dots with numbers
        for (int i = 0; i < points.Length; i++)
        {
            float px = area.x + padding + points[i].x * drawW;
            float py = area.y + padding + (1f - points[i].y) * drawH;

            Rect dotRect = new Rect(px - 8, py - 8, 16, 16);
            EditorGUI.DrawRect(dotRect, new Color(1f, 0.95f, 0.7f, 0.9f));

            var style = new GUIStyle(EditorStyles.miniLabel);
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = Color.black;
            style.fontSize = 9;
            EditorGUI.LabelField(new Rect(px - 10, py - 7, 20, 14),
                (i + 1).ToString(), style);
        }
    }

    // ══════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════

    private static Sprite LoadSprite(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null) return sprite;
        var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
        if (allAssets != null)
            foreach (var asset in allAssets)
                if (asset is Sprite s) return s;
        return null;
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
