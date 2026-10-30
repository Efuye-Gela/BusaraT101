using System;
using System.Collections.Generic;
using System.Linq;

namespace Busara.Online
{
    public static class SharedRules
    {
        public static bool Adjacent(int first, int second, int width = 8)
        {
            if (width < 1 || first < 0 || second < 0 || first >= width * width || second >= width * width)
                return false;
            return Math.Abs(first / width - second / width) + Math.Abs(first % width - second % width) == 1;
        }

        public static bool RecipeMatches(ResourceType a, ResourceType b, ResourceType first, ResourceType second)
        {
            return (a == first && b == second) || (a == second && b == first);
        }

        public static bool CanPay<T>(IList<T> owned, IList<T> selected, int cost) where T : class
        {
            if (owned == null || selected == null || cost < 0 || selected.Count != cost)
                return false;
            var remaining = new List<T>(owned);
            foreach (T item in selected)
                if (item == null || !remaining.Remove(item))
                    return false;
            return true;
        }

        public static bool MeetsGoals(IEnumerable<VirtueType> inventory, IEnumerable<KeyValuePair<VirtueType, int>> goals)
        {
            if (inventory == null || goals == null)
                return false;
            var requirements = goals.ToList();
            if (requirements.Count == 0)
                return false;
            var counts = inventory.GroupBy(item => item).ToDictionary(group => group.Key, group => group.Count());
            return requirements.All(goal => (counts.TryGetValue(goal.Key, out int count) ? count : 0) >= goal.Value);
        }
    }

    public static class Definitions
    {
        public const string LegacyRuleset = "busara-online-mvp-v1";
        public const string OrdinaryRuleset = "busara-online-mvp-v2";
        public const string FullRuleset = "busara-online-v3";
        public const string CurrentRuleset = FullRuleset;
        public const string Egolica = "egolica";
        public const string Mask = "mask-of-light";
        public const string Knowledge = "nevulandis";
        public static readonly ResourceType[] RecipeFirst =
            { ResourceType.Fire, ResourceType.Fire, ResourceType.Fire, ResourceType.Air, ResourceType.Air, ResourceType.Water };
        public static readonly ResourceType[] RecipeSecond =
            { ResourceType.Water, ResourceType.Earth, ResourceType.Air, ResourceType.Earth, ResourceType.Water, ResourceType.Earth };

        public static bool IsExpanded(string ruleset) => ruleset == OrdinaryRuleset || ruleset == FullRuleset;

        public static bool IsFull(string ruleset) => ruleset == FullRuleset;

        public static bool IsKnown(string ruleset) => ruleset == LegacyRuleset || IsExpanded(ruleset);

        public static string KingdomName(string id)
        {
            KingdomDefinition kingdom = KingdomCatalog.Find(id);
            return kingdom.Name + " - " + kingdom.PowerName;
        }

        public static KeyValuePair<VirtueType, int>[] Goals(string kingdom) => KingdomCatalog.Find(kingdom).Goals.ToArray();

        public static string GoalText(string kingdom) =>
            string.Join(", ", Goals(kingdom).Select(goal => goal.Value + " " + goal.Key));

        public static ResourceType[] Setup(int seat) => seat == 0
            ? new[] { ResourceType.Earth, ResourceType.Water, ResourceType.Water, ResourceType.Air, ResourceType.Air }
            : new[] { ResourceType.Fire, ResourceType.Fire, ResourceType.Fire, ResourceType.Water, ResourceType.Earth };

        public static VirtueType Forge(ResourceType first, ResourceType second)
        {
            for (int i = 0; i < RecipeFirst.Length; i++)
                if (SharedRules.RecipeMatches(first, second, RecipeFirst[i], RecipeSecond[i]))
                    return (VirtueType)i;
            throw new RuleException("invalid_forge", "Choose two adjacent resources of different types.");
        }
    }
}
