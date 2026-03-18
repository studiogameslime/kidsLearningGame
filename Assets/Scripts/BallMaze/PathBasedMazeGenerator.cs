using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple maze generator for young children.
/// Places 2-5 obstacles that gently guide the ball toward the hole.
/// Always solvable, always readable, always easy.
/// </summary>
public static class PathBasedMazeGenerator
{
    private const int MaxAttempts = 10;

    // Layout orientations for variety
    private enum Flow { TopLeftToBottomRight, TopRightToBottomLeft, LeftToRight, TopToBottom }
    private const int FlowCount = 4;
    private static Flow _lastFlow = (Flow)(-1);

    public static BallMazeLevel Generate(int difficulty)
    {
        difficulty = Mathf.Clamp(difficulty, 0, 2);

        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var level = Build(difficulty);
            if (level != null) return level;
        }

        return Fallback();
    }

    private static BallMazeLevel Build(int difficulty)
    {
        int bw = 9, bh = 6;

        // Pick a flow different from last
        Flow flow;
        do { flow = (Flow)Random.Range(0, FlowCount); } while (flow == _lastFlow);
        _lastFlow = flow;

        // Determine start and hole positions based on flow
        float ballX, ballY, holeX, holeY;
        switch (flow)
        {
            case Flow.TopLeftToBottomRight:
                ballX = 1.5f; ballY = bh - 1.5f;
                holeX = bw - 1.5f; holeY = 1.5f;
                break;
            case Flow.TopRightToBottomLeft:
                ballX = bw - 1.5f; ballY = bh - 1.5f;
                holeX = 1.5f; holeY = 1.5f;
                break;
            case Flow.LeftToRight:
                ballX = 1.5f; ballY = bh / 2f;
                holeX = bw - 1.5f; holeY = bh / 2f + Random.Range(-1f, 1f);
                break;
            default: // TopToBottom
                ballX = bw / 2f + Random.Range(-1f, 1f); ballY = bh - 1.5f;
                holeX = bw / 2f + Random.Range(-1f, 1f); holeY = 1.5f;
                break;
        }

        holeX = Mathf.Clamp(holeX, 1.5f, bw - 1.5f);
        holeY = Mathf.Clamp(holeY, 1.2f, bh - 1.2f);

        // Place 2-5 obstacles that create 1-2 gentle turns
        var blocks = new List<MazeBlockDef>();
        int blockCount = difficulty == 0 ? Random.Range(2, 4) : Random.Range(3, 6);

        PlaceGuidingObstacles(blocks, ballX, ballY, holeX, holeY, bw, bh, blockCount, flow);

        // Optional rotating gate for medium/hard — only if plenty of space
        var rotating = new List<MazeRotatingDef>();
        if (difficulty >= 1 && blocks.Count >= 2)
        {
            float gateX = (ballX + holeX) / 2f + Random.Range(-1f, 1f);
            float gateY = (ballY + holeY) / 2f + Random.Range(-1f, 1f);
            gateX = Mathf.Clamp(gateX, 2f, bw - 2f);
            gateY = Mathf.Clamp(gateY, 2f, bh - 2f);

            // Only place if far enough from all blocks and from ball/hole
            bool safe = true;
            foreach (var b in blocks)
                if (Vector2.Distance(new Vector2(b.x, b.y), new Vector2(gateX, gateY)) < 2f)
                    safe = false;
            if (Vector2.Distance(new Vector2(ballX, ballY), new Vector2(gateX, gateY)) < 2f) safe = false;
            if (Vector2.Distance(new Vector2(holeX, holeY), new Vector2(gateX, gateY)) < 2f) safe = false;

            if (safe)
            {
                float speed = Random.Range(25f, 35f) * (Random.value > 0.5f ? 1f : -1f);
                rotating.Add(new MazeRotatingDef("rotate_large", gateX, gateY, speed));
            }
        }

        // Validate: nothing too close to hole
        foreach (var b in blocks)
        {
            if (Vector2.Distance(new Vector2(b.x, b.y), new Vector2(holeX, holeY)) < 1.2f)
                return null; // too close to hole, retry
        }

        bool small = difficulty >= 2;
        return new BallMazeLevel
        {
            boardW = bw, boardH = bh,
            ballX = ballX, ballY = ballY,
            holeX = holeX, holeY = holeY,
            ballSprite = Random.value > 0.5f
                ? (small ? "ball_red_small" : "ball_red_large")
                : (small ? "ball_blue_small" : "ball_blue_large"),
            ballRadius = small ? 0.3f : 0.4f,
            holeRadius = small ? 0.5f : 0.55f,
            blocks = blocks.ToArray(),
            rotating = rotating.ToArray(),
        };
    }

    /// <summary>
    /// Places a few obstacles that gently block the direct path,
    /// forcing 1-2 turns without creating tight corridors.
    /// </summary>
    private static void PlaceGuidingObstacles(List<MazeBlockDef> blocks,
        float ballX, float ballY, float holeX, float holeY,
        int bw, int bh, int count, Flow flow)
    {
        float midX = (ballX + holeX) / 2f;
        float midY = (ballY + holeY) / 2f;

        // First obstacle: always block the most direct path
        switch (flow)
        {
            case Flow.TopLeftToBottomRight:
            case Flow.TopRightToBottomLeft:
                // Diagonal flow — place a horizontal bar across the middle
                blocks.Add(new MazeBlockDef("large", midX, midY, 90));
                if (count > 2)
                    blocks.Add(new MazeBlockDef("square", midX + 2f, midY));
                break;

            case Flow.LeftToRight:
                // Horizontal flow — place a vertical bar in the middle
                blocks.Add(new MazeBlockDef("large", midX, midY, 0));
                break;

            case Flow.TopToBottom:
                // Vertical flow — place a horizontal bar in the middle
                blocks.Add(new MazeBlockDef("large", midX, midY, 90));
                break;
        }

        // Additional obstacles placed randomly but away from ball, hole, and existing blocks
        int placed = blocks.Count;
        int safety = 0;
        while (placed < count && safety < 30)
        {
            safety++;
            float ox = Random.Range(1.5f, bw - 1.5f);
            float oy = Random.Range(1.5f, bh - 1.5f);

            // Must be far from ball and hole
            if (Vector2.Distance(new Vector2(ox, oy), new Vector2(ballX, ballY)) < 2f) continue;
            if (Vector2.Distance(new Vector2(ox, oy), new Vector2(holeX, holeY)) < 2f) continue;

            // Must be far from existing blocks
            bool tooClose = false;
            foreach (var b in blocks)
                if (Vector2.Distance(new Vector2(b.x, b.y), new Vector2(ox, oy)) < 1.8f)
                    tooClose = true;
            if (tooClose) continue;

            // Pick a random block type
            string type = Random.value > 0.5f ? "large" : "square";
            float rot = (type == "large" && Random.value > 0.5f) ? 90 : 0;

            blocks.Add(new MazeBlockDef(type, ox, oy, rot));
            placed++;
        }
    }

    private static BallMazeLevel Fallback()
    {
        // Absolute simplest: 2 blocks, diagonal flow
        var blocks = new MazeBlockDef[]
        {
            new MazeBlockDef("large", 4.5f, 3f, 90),
            new MazeBlockDef("square", 6f, 2f),
        };
        return new BallMazeLevel
        {
            boardW = 9, boardH = 6,
            ballX = 1.5f, ballY = 4.5f,
            holeX = 7.5f, holeY = 1.5f,
            ballSprite = "ball_red_large",
            ballRadius = 0.4f, holeRadius = 0.55f,
            blocks = blocks,
            rotating = new MazeRotatingDef[0],
        };
    }
}
