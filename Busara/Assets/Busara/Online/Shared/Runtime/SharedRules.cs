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
        public const string Egolica = "egolica";
        public const string Mask = "mask-of-light";
        public const string Knowledge = "nevulandis";
        public static readonly ResourceType[] RecipeFirst =
            { ResourceType.Fire, ResourceType.Fire, ResourceType.Fire, ResourceType.Air, ResourceType.Air, ResourceType.Water };
        public static readonly ResourceType[] RecipeSecond =
            { ResourceType.Water, ResourceType.Earth, ResourceType.Air, ResourceType.Earth, ResourceType.Water, ResourceType.Earth };

        public static string KingdomName(string id)
        {
            switch (id)
            {
                case Egolica: return "Egolica - Abundance";
                case Mask: return "Mask of Light - Retraction";
                case Knowledge: return "N'evulandis - Infinite Knowledge";
                default: throw new RuleException("invalid_definition", "The saved kingdom definition is unsupported.");
            }
        }

        public static KeyValuePair<VirtueType, int>[] Goals(string kingdom)
        {
            switch (kingdom)
            {
                case Egolica: return new[] { Goal(VirtueType.Security, 2), Goal(VirtueType.Nature, 3), Goal(VirtueType.Economy, 4) };
                case Mask: return new[] { Goal(VirtueType.Art, 3), Goal(VirtueType.Security, 4), Goal(VirtueType.Economy, 2) };
                case Knowledge: return new[] { Goal(VirtueType.Energy, 4), Goal(VirtueType.Wisdom, 2), Goal(VirtueType.Economy, 3) };
                default: throw new RuleException("invalid_definition", "The saved kingdom definition is unsupported.");
            }
        }

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

        private static KeyValuePair<VirtueType, int> Goal(VirtueType type, int count) =>
            new KeyValuePair<VirtueType, int>(type, count);
    }
}
