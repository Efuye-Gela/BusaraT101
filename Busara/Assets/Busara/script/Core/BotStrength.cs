using System.Collections.Generic;

/// <summary>How well a bot plays. Hard always takes the heuristic's best move; Normal sometimes takes the
/// second best; Easy picks loosely among the top few.</summary>
public enum BotStrength { Easy, Normal, Hard }

/// <summary>The choices made on the main menu's New Game screen, read once by the setup screen.</summary>
public static class GameLaunch
{
    public const int MaximumBots = 3;
    public static int Bots { get; private set; }
    public static BotStrength Strength { get; private set; } = BotStrength.Normal;

    public static void Configure(int bots, BotStrength strength)
    {
        Bots = bots < 1 ? 1 : bots > MaximumBots ? MaximumBots : bots;
        Strength = strength;
    }

    /// <summary>The number of bots to seat, then forgets it so a later scene load starts fresh.</summary>
    public static int TakeBots()
    {
        int bots = Bots;
        Bots = 0;
        return bots;
    }
}

public static class BotStrengths
{
    /// <summary>
    /// Moves the candidate this strength plays to the front of a best-first list. The pick is seeded by the
    /// decision's signature, so analysing the same position again picks the same move.
    /// </summary>
    public static List<BotCandidate> Apply(List<BotCandidate> ranked, BotStrength strength, string signature)
    {
        if (strength == BotStrength.Hard || ranked.Count < 2)
            return ranked;
        uint roll = Hash(signature) % 100;
        int pick = strength == BotStrength.Normal
            ? (roll < 25 ? 1 : 0)
            : (int)(roll % (uint)System.Math.Min(4, ranked.Count));
        if (pick == 0)
            return ranked;
        var reordered = new List<BotCandidate>(ranked);
        BotCandidate chosen = reordered[pick];
        reordered.RemoveAt(pick);
        reordered.Insert(0, chosen);
        return reordered;
    }

    private static uint Hash(string text)
    {
        uint hash = 2166136261;
        foreach (char c in text ?? "")
            hash = (hash ^ c) * 16777619;
        return hash;
    }
}
