using System;
using System.Collections.Generic;
using System.Linq;

public enum BotActionKind { Forge, Move, Draw, Place, EndTurn, DiscardVirtue, UsePower, PowerChoice }

public sealed class BotCell
{
    public int Index { get; }
    public ResourceType? Resource { get; }
    public IReadOnlyList<int> Neighbors { get; }

    public BotCell(int index, ResourceType? resource, IEnumerable<int> neighbors)
    {
        Index = index;
        Resource = resource;
        Neighbors = Array.AsReadOnly(neighbors.ToArray());
    }
}

public sealed class BotRecipe
{
    public VirtueType Virtue { get; }
    public ResourceType First { get; }
    public ResourceType Second { get; }
    public int Needed { get; }
    public int Stock { get; }

    public BotRecipe(VirtueType virtue, ResourceType first, ResourceType second, int needed, int stock)
    {
        Virtue = virtue;
        First = first;
        Second = second;
        Needed = Math.Max(0, needed);
        Stock = stock;
    }

    public bool Matches(ResourceType first, ResourceType second) =>
        first != second && ((First == first && Second == second) || (First == second && Second == first));
}

public sealed class BotScoreTerm
{
    public string Reason { get; }
    public float Value { get; }
    public BotScoreTerm(string reason, float value) { Reason = reason; Value = value; }
}

public sealed class BotCandidate
{
    public BotActionKind Kind { get; }
    public int From { get; }
    public int To { get; }
    public string Path { get; }
    public VirtueType? DiscardType { get; }
    public PowerChoice Choice { get; }
    public IReadOnlyList<BotScoreTerm> Terms { get; }
    public float Score => Terms.Sum(term => term.Value);
    public string Key => $"{Kind}:{From}:{To}:{DiscardType}:{Choice?.Option?.Kind}";

    public BotCandidate(BotActionKind kind, string path, IEnumerable<BotScoreTerm> terms, int from = -1, int to = -1,
        VirtueType? discardType = null, PowerChoice choice = null)
    {
        Kind = kind;
        Path = path;
        From = from;
        To = to;
        DiscardType = discardType;
        Choice = choice;
        Terms = Array.AsReadOnly(terms.ToArray());
    }
}

/// <summary>Deterministic one-step evaluator. Inputs contain no deck order or opponent secrets.</summary>
public static class BotPlanner
{
    public static IReadOnlyList<BotCandidate> EvaluateVirtueDiscard(IEnumerable<VirtueType> owned,
        IReadOnlyDictionary<VirtueType, int> goals)
    {
        if (owned == null || goals == null)
            throw new ArgumentException("Provide the owner's virtues and victory goals.");
        return Array.AsReadOnly(owned.GroupBy(type => type).Select(group =>
        {
            int required = goals.TryGetValue(group.Key, out int count) ? count : 0;
            bool losesGoal = group.Count() <= required;
            return new BotCandidate(BotActionKind.DiscardVirtue,
                $"Hard Winter: discard one {group.Key} -> " +
                (losesGoal ? "one more virtue needed for victory" : "preserve current victory progress"),
                new[] { Term(losesGoal ? "Lose a goal virtue" : "Lose a surplus virtue", losesGoal ? -100 : -15) },
                discardType: group.Key);
        }).OrderByDescending(candidate => candidate.Score).ThenBy(candidate => candidate.DiscardType).ToArray());
    }

    public static IReadOnlyList<BotCandidate> Evaluate(IReadOnlyList<BotCell> cells,
        IReadOnlyList<BotRecipe> recipes, bool canDraw, ResourceType? pendingResource = null)
    {
        if (cells == null || recipes == null || cells.Select(cell => cell.Index).Distinct().Count() != cells.Count)
            throw new ArgumentException("Provide a board with unique slot indices and its virtue recipes.");
        var board = cells.ToDictionary(cell => cell.Index, cell => cell.Resource);
        var candidates = new List<BotCandidate>();
        float before = Potential(cells, recipes, board);
        foreach (BotCell empty in cells.Where(cell => !cell.Resource.HasValue))
        {
            if (pendingResource.HasValue)
            {
                var next = new Dictionary<int, ResourceType?>(board) { [empty.Index] = pendingResource };
                candidates.Add(new BotCandidate(BotActionKind.Place,
                    $"Place revealed {pendingResource} at #{empty.Index + 1} -> improve future forging",
                    new[] { Term("Resource retained", 8), Term("Useful pair potential change", Potential(cells, recipes, next) - before) },
                    to: empty.Index));
            }
        }
        if (!pendingResource.HasValue)
        {
            foreach (BotCell first in cells.Where(cell => cell.Resource.HasValue).OrderBy(cell => cell.Index))
            {
                foreach (int neighbor in first.Neighbors.OrderBy(index => index))
                {
                    if (!board.TryGetValue(neighbor, out ResourceType? other))
                        continue;
                    if (!other.HasValue)
                    {
                        var moved = new Dictionary<int, ResourceType?>(board)
                        {
                            [first.Index] = null, [neighbor] = first.Resource
                        };
                        candidates.Add(new BotCandidate(BotActionKind.Move,
                            $"Move {first.Resource} #{first.Index + 1} -> #{neighbor + 1} -> improve future forging",
                            new[] { Term("Move action cost", -2), Term("Useful pair potential change", Potential(cells, recipes, moved) - before) },
                            first.Index, neighbor));
                    }
                    else if (first.Index < neighbor)
                    {
                        BotRecipe recipe = recipes.FirstOrDefault(item => item.Stock > 0 &&
                            item.Matches(first.Resource.Value, other.Value));
                        if (recipe == null)
                            continue;
                        bool wins = recipe.Needed == 1 && recipes.All(item => item.Virtue == recipe.Virtue || item.Needed == 0);
                        var removed = new Dictionary<int, ResourceType?>(board)
                        {
                            [first.Index] = null, [neighbor] = null
                        };
                        candidates.Add(new BotCandidate(BotActionKind.Forge,
                            $"Forge #{first.Index + 1} + #{neighbor + 1} -> gain {recipe.Virtue}" + (wins ? " -> victory goals complete" : ""),
                            new[]
                            {
                                Term(recipe.Needed > 0 ? "Missing goal virtue" : "Surplus virtue", recipe.Needed > 0 ? 100 : 15),
                                Term("Two resources spent", -16),
                                Term("Useful pair potential change", Potential(cells, recipes, removed) - before),
                                Term("Completes all victory goals", wins ? 10000 : 0)
                            }, first.Index, neighbor));
                    }
                }
            }
            if (canDraw && cells.Any(cell => !cell.Resource.HasValue))
            {
                // This prior is deliberately fixed; evaluating never peeks at the next card.
                var outcomes = new List<BotScoreTerm>();
                ResourceType[] types = Enum.GetValues(typeof(ResourceType)).Cast<ResourceType>().ToArray();
                foreach (ResourceType type in types)
                {
                    var best = cells.Where(cell => !cell.Resource.HasValue).Select(empty =>
                    {
                        var placed = new Dictionary<int, ResourceType?>(board) { [empty.Index] = type };
                        return new { empty.Index, Value = 8 + Potential(cells, recipes, placed) - before };
                    }).OrderByDescending(outcome => outcome.Value).ThenBy(outcome => outcome.Index).First();
                    outcomes.Add(Term($"{type} -> best #{best.Index + 1}, raw {best.Value:0.##}; assumed {75f / types.Length:0.##}%",
                        .75f / types.Length * best.Value));
                }
                outcomes.Add(Term("Assumed 25% disaster risk; raw -12", .25f * -12));
                candidates.Add(new BotCandidate(BotActionKind.Draw,
                    "Draw unknown card -> resource: score revealed placements; disaster: pause for human resolution",
                    outcomes));
            }
            candidates.Add(new BotCandidate(BotActionKind.EndTurn, "End turn -> no board progress",
                new[] { Term("Idle action penalty", -50) }));
        }
        return Array.AsReadOnly(candidates.OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Kind).ThenBy(candidate => candidate.From).ThenBy(candidate => candidate.To).ToArray());
    }

    private static BotScoreTerm Term(string reason, float value) => new BotScoreTerm(reason, value);

    public static float Potential(IReadOnlyList<BotCell> cells, IReadOnlyList<BotRecipe> recipes,
        Dictionary<int, ResourceType?> board)
    {
        float value = 0;
        foreach (BotCell cell in cells)
        {
            if (!board[cell.Index].HasValue)
                continue;
            foreach (int neighbor in cell.Neighbors.Where(index => index > cell.Index))
            {
                if (!board.TryGetValue(neighbor, out ResourceType? other) || !other.HasValue)
                    continue;
                BotRecipe recipe = recipes.FirstOrDefault(item => item.Stock > 0 &&
                    item.Matches(board[cell.Index].Value, other.Value));
                if (recipe != null)
                    value += recipe.Needed > 0 ? 12 : 2;
            }
        }
        return value;
    }
}
