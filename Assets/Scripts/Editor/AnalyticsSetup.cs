using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Creates and populates the analytics ScriptableObject configs:
/// - GameCategoryMapping (which skills each game trains)
/// - AgeDifficultyBaseline (starting difficulty per age bracket)
///
/// Assets are placed in Resources/Analytics/ so StatsManager and DifficultyManager
/// can load them at runtime via Resources.Load.
/// </summary>
public class AnalyticsSetup : EditorWindow
{
    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Analytics", "Creating config assets...", 0.5f);
            CreateCategoryMapping();
            CreateAgeDifficultyBaseline();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    // ── Game → Category Mapping ──────────────────────────────────

    private static void CreateCategoryMapping()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/Analytics");

        string path = "Assets/Resources/Analytics/GameCategoryMapping.asset";
        var existing = AssetDatabase.LoadAssetAtPath<GameCategoryMapping>(path);
        var mapping = existing != null ? existing : ScriptableObject.CreateInstance<GameCategoryMapping>();
        if (existing == null) AssetDatabase.CreateAsset(mapping, path);

        mapping.entries.Clear();

        // ── Memory Game ──
        mapping.entries.Add(Entry("memory",
            W(SkillCategory.Memory, 0.60f),
            W(SkillCategory.Attention, 0.25f),
            W(SkillCategory.VisualMatching, 0.15f)));

        // ── Puzzle Game ──
        mapping.entries.Add(Entry("puzzle",
            W(SkillCategory.FineMotor, 0.35f),
            W(SkillCategory.SpatialReasoning, 0.40f),
            W(SkillCategory.ProblemSolving, 0.25f)));

        // ── Coloring Game ──
        mapping.entries.Add(Entry("coloring",
            W(SkillCategory.FineMotor, 0.55f),
            W(SkillCategory.ColorsAndShapes, 0.30f),
            W(SkillCategory.Attention, 0.15f)));

        // ── Connect the Dots ──
        mapping.entries.Add(Entry("fillthedots",
            W(SkillCategory.FineMotor, 0.35f),
            W(SkillCategory.Numbers, 0.35f),
            W(SkillCategory.Attention, 0.30f)));

        // ── Counting Game ──
        mapping.entries.Add(Entry("findthecount",
            W(SkillCategory.Numbers, 0.60f),
            W(SkillCategory.Attention, 0.25f),
            W(SkillCategory.VisualMatching, 0.15f)));

        // ── Find the Animal ──
        mapping.entries.Add(Entry("findtheobject",
            W(SkillCategory.VisualMatching, 0.45f),
            W(SkillCategory.Attention, 0.35f),
            W(SkillCategory.SpatialReasoning, 0.20f)));

        // ── Shadow Match ──
        mapping.entries.Add(Entry("shadows",
            W(SkillCategory.VisualMatching, 0.50f),
            W(SkillCategory.FineMotor, 0.25f),
            W(SkillCategory.SpatialReasoning, 0.25f)));

        // ── Color Mixing ──
        mapping.entries.Add(Entry("colormixing",
            W(SkillCategory.ColorsAndShapes, 0.50f),
            W(SkillCategory.ProblemSolving, 0.30f),
            W(SkillCategory.FineMotor, 0.20f)));

        // ── Laundry Sorting ──
        mapping.entries.Add(Entry("laundrysorting",
            W(SkillCategory.VisualMatching, 0.40f),
            W(SkillCategory.Attention, 0.30f),
            W(SkillCategory.FineMotor, 0.30f)));


        // ── Ball Maze ──
        mapping.entries.Add(Entry("ballmaze",
            W(SkillCategory.SpatialReasoning, 0.45f),
            W(SkillCategory.FineMotor, 0.35f),
            W(SkillCategory.ProblemSolving, 0.20f)));

        // ── Shared Sticker (Spot It) ──
        mapping.entries.Add(Entry("sharedsticker",
            W(SkillCategory.VisualMatching, 0.45f),
            W(SkillCategory.ReactionSpeed, 0.30f),
            W(SkillCategory.Attention, 0.25f)));

        // ── Flappy Bird ──
        mapping.entries.Add(Entry("flappybird",
            W(SkillCategory.ReactionSpeed, 0.50f),
            W(SkillCategory.Attention, 0.30f),
            W(SkillCategory.FineMotor, 0.20f)));

        // ── Simon Says ──
        mapping.entries.Add(Entry("simonsays",
            W(SkillCategory.Memory, 0.45f),
            W(SkillCategory.InstructionFollowing, 0.30f),
            W(SkillCategory.Attention, 0.25f)));

        // ── Pattern Copy ──
        mapping.entries.Add(Entry("patterncopy",
            W(SkillCategory.VisualMatching, 0.40f),
            W(SkillCategory.SpatialReasoning, 0.35f),
            W(SkillCategory.Attention, 0.25f)));

        // ── Letter Game (First Letter) ──
        mapping.entries.Add(Entry("letters",
            W(SkillCategory.VisualMatching, 0.40f),
            W(SkillCategory.Memory, 0.30f),
            W(SkillCategory.Attention, 0.30f)));

        // ── Number Maze ──
        mapping.entries.Add(Entry("numbermaze",
            W(SkillCategory.Numbers, 0.40f),
            W(SkillCategory.SpatialReasoning, 0.35f),
            W(SkillCategory.Attention, 0.25f)));

        // ── Odd One Out ──
        mapping.entries.Add(Entry("oddoneout",
            W(SkillCategory.VisualMatching, 0.50f),
            W(SkillCategory.Attention, 0.30f),
            W(SkillCategory.ProblemSolving, 0.20f)));

        // ── Quantity Match ──
        mapping.entries.Add(Entry("quantitymatch",
            W(SkillCategory.Numbers, 0.55f),
            W(SkillCategory.VisualMatching, 0.25f),
            W(SkillCategory.Attention, 0.20f)));

        // ── Connect Match ──
        mapping.entries.Add(Entry("connectmatch",
            W(SkillCategory.SpatialReasoning, 0.40f),
            W(SkillCategory.VisualMatching, 0.35f),
            W(SkillCategory.FineMotor, 0.25f)));

        // ── Number Train ──
        mapping.entries.Add(Entry("numbertrain",
            W(SkillCategory.Numbers, 0.50f),
            W(SkillCategory.Attention, 0.25f),
            W(SkillCategory.ProblemSolving, 0.25f)));

        // ── Letter Train ──
        mapping.entries.Add(Entry("lettertrain",
            W(SkillCategory.Memory, 0.40f),
            W(SkillCategory.Attention, 0.30f),
            W(SkillCategory.ProblemSolving, 0.30f)));

        // ── Bakery ──
        mapping.entries.Add(Entry("bakery",
            W(SkillCategory.FineMotor, 0.40f),
            W(SkillCategory.VisualMatching, 0.30f),
            W(SkillCategory.Attention, 0.30f)));

        // ── Sock Match ──
        mapping.entries.Add(Entry("sockmatch",
            W(SkillCategory.VisualMatching, 0.50f),
            W(SkillCategory.Attention, 0.30f),
            W(SkillCategory.Memory, 0.20f)));

        // ── Fruit Puzzle ──
        mapping.entries.Add(Entry("vehiclepuzzle",
            W(SkillCategory.VisualMatching, 0.40f),
            W(SkillCategory.Attention, 0.30f),
            W(SkillCategory.ProblemSolving, 0.30f)));

        // ── Color Sort ──
        mapping.entries.Add(Entry("colorsort",
            W(SkillCategory.VisualMatching, 0.40f),
            W(SkillCategory.Attention, 0.30f),
            W(SkillCategory.ProblemSolving, 0.30f)));

        // ── Size Sort ──
        mapping.entries.Add(Entry("sizesort",
            W(SkillCategory.VisualMatching, 0.40f),
            W(SkillCategory.Attention, 0.30f),
            W(SkillCategory.ProblemSolving, 0.30f)));

        EditorUtility.SetDirty(mapping);
    }

    // ── Age → Starting Difficulty ────────────────────────────────

    private static void CreateAgeDifficultyBaseline()
    {
        string path = "Assets/Resources/Analytics/AgeDifficultyBaseline.asset";
        var existing = AssetDatabase.LoadAssetAtPath<AgeDifficultyBaseline>(path);
        var baseline = existing != null ? existing : ScriptableObject.CreateInstance<AgeDifficultyBaseline>();
        if (existing == null) AssetDatabase.CreateAsset(baseline, path);

        baseline.ageRanges.Clear();

        // All game IDs
        string[] allGames = {
            "memory", "puzzle", "coloring", "fillthedots", "findthecount",
            "findtheobject", "shadows", "colormixing",
            "ballmaze", "sharedsticker",
            "flappybird", "simonsays", "patterncopy", "letters", "numbermaze",
            "oddoneout", "quantitymatch", "connectmatch", "numbertrain",
            "lettertrain", "laundrysorting", "bakery", "sockmatch", "sizesort", "colorsort", "vehiclepuzzle"
        };

        // ── Age 2–2.5 years (24–30 months) ──
        var range1 = new AgeRange { minMonths = 24, label = "2–2.5 years" };
        foreach (var id in allGames)
            range1.games.Add(new GameDifficultyEntry { gameId = id, startingDifficulty = 1 });
        baseline.ageRanges.Add(range1);

        // ── Age 2.5–3 years (30–36 months) ──
        var range2 = new AgeRange { minMonths = 30, label = "2.5–3 years" };
        foreach (var id in allGames)
            range2.games.Add(new GameDifficultyEntry { gameId = id, startingDifficulty = 1 });
        baseline.ageRanges.Add(range2);

        // ── Age 3–4 years (36–48 months) ──
        var range3 = new AgeRange { minMonths = 36, label = "3–4 years" };
        foreach (var id in allGames)
        {
            int diff = 2; // default bump for 3+
            // Some games stay at 1 for 3-year-olds
            if (id == "simonsays" || id == "ballmaze" || id == "flappybird")
                diff = 1;
            range3.games.Add(new GameDifficultyEntry { gameId = id, startingDifficulty = diff });
        }
        baseline.ageRanges.Add(range3);

        // ── Age 4–5 years (48–60 months) ──
        var range4 = new AgeRange { minMonths = 48, label = "4–5 years" };
        foreach (var id in allGames)
        {
            int diff = 3;
            if (id == "simonsays" || id == "ballmaze" || id == "flappybird")
                diff = 2;
            range4.games.Add(new GameDifficultyEntry { gameId = id, startingDifficulty = diff });
        }
        baseline.ageRanges.Add(range4);

        // ── Age 5+ (60+ months) ──
        var range5 = new AgeRange { minMonths = 60, label = "5+ years" };
        foreach (var id in allGames)
        {
            int diff = 4;
            if (id == "simonsays" || id == "flappybird")
                diff = 3;
            range5.games.Add(new GameDifficultyEntry { gameId = id, startingDifficulty = diff });
        }
        baseline.ageRanges.Add(range5);

        EditorUtility.SetDirty(baseline);
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static GameCategoryEntry Entry(string gameId, params CategoryWeight[] weights)
    {
        var e = new GameCategoryEntry { gameId = gameId };
        e.weights.AddRange(weights);
        return e;
    }

    private static CategoryWeight W(SkillCategory cat, float weight)
    {
        return new CategoryWeight { category = cat, weight = weight };
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
