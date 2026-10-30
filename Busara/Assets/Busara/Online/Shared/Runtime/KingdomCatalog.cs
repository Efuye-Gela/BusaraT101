using System.Collections.Generic;
using System.Linq;

namespace Busara.Online
{
    public enum PowerTiming { OwnTurn, PowerReaction, ThreatReaction, TurnStart, OtherTurn, AfterAction }

    public sealed class KingdomDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Power { get; }
        public string PowerName { get; }
        public int Cost { get; }
        public PowerTiming Timing { get; }
        public bool HiddenOnly { get; }
        public KeyValuePair<VirtueType, int>[] Goals { get; }

        public KingdomDefinition(string id, string name, string power, string powerName, int cost, PowerTiming timing,
            bool hiddenOnly, params KeyValuePair<VirtueType, int>[] goals)
        {
            Id = id; Name = name; Power = power; PowerName = powerName; Cost = cost;
            Timing = timing; HiddenOnly = hiddenOnly; Goals = goals;
        }
    }

    // Mirrors the authored kingdom/power assets; KingdomPowerTests is the offline reference.
    public static class KingdomCatalog
    {
        public const string Abundance = "abundance", Magic = "magic", IdentitySurfing = "identitySurfing",
            Transform = "transform", Witchcraft = "witchcraft", Necklace = "kingsNecklace", Invisibility = "invisibility",
            Dome = "celestialDome", Knowledge = "infiniteKnowledge", Manipulation = "manipulation", Rain = "rain",
            Imagination = "imagination", Blessing = "blessingOfPlenty", Time = "time", Retraction = "retraction";

        public static readonly KingdomDefinition[] All =
        {
            K("egolica", "Egolica", Abundance, "Abundance", 0, PowerTiming.OwnTurn, true,
                (VirtueType.Security, 2), (VirtueType.Nature, 3), (VirtueType.Economy, 4)),
            K("mask-of-light", "Mask of Light", Retraction, "Retraction", 2, PowerTiming.AfterAction, false,
                (VirtueType.Art, 3), (VirtueType.Security, 4), (VirtueType.Economy, 2)),
            K("nevulandis", "N'evulandis", Knowledge, "Infinite Knowledge", 1, PowerTiming.OwnTurn, false,
                (VirtueType.Energy, 4), (VirtueType.Wisdom, 2), (VirtueType.Economy, 3)),
            K("konga", "Konga", Magic, "Magic", 2, PowerTiming.OwnTurn, false,
                (VirtueType.Art, 3), (VirtueType.Security, 4), (VirtueType.Nature, 2)),
            K("aradas", "The Aradas", IdentitySurfing, "Identity Surfing", 3, PowerTiming.OwnTurn, false,
                (VirtueType.Art, 2), (VirtueType.Wisdom, 3), (VirtueType.Nature, 4)),
            K("eko-akete", "Eko Akete", Transform, "Transform", 4, PowerTiming.OwnTurn, false,
                (VirtueType.Art, 4), (VirtueType.Wisdom, 3), (VirtueType.Economy, 2)),
            K("ilagik", "ILAGIK", Witchcraft, "Witchcraft", 3, PowerTiming.OwnTurn, false,
                (VirtueType.Energy, 4), (VirtueType.Security, 3), (VirtueType.Nature, 2)),
            K("bis-bese-avouman", "Bis-Bese Avouman", Necklace, "King's Necklace", 3, PowerTiming.PowerReaction, false,
                (VirtueType.Art, 2), (VirtueType.Nature, 4), (VirtueType.Economy, 3)),
            K("milu", "Milu", Invisibility, "Invisibility", 2, PowerTiming.OwnTurn, false,
                (VirtueType.Energy, 2), (VirtueType.Nature, 3), (VirtueType.Economy, 4)),
            K("empire-volta", "Empire Volta", Dome, "Celestial Dome", 1, PowerTiming.ThreatReaction, false,
                (VirtueType.Energy, 3), (VirtueType.Security, 4), (VirtueType.Wisdom, 2)),
            K("royaume-kongo", "Royaume Kongo", Manipulation, "Manipulation", 3, PowerTiming.TurnStart, false,
                (VirtueType.Art, 4), (VirtueType.Energy, 2), (VirtueType.Security, 3)),
            K("kavango-keendobe", "Kavango Keendobe", Rain, "Rain", 3, PowerTiming.OwnTurn, false,
                (VirtueType.Security, 2), (VirtueType.Wisdom, 3), (VirtueType.Economy, 4)),
            K("ubunifu", "Ubunifu", Imagination, "Imagination", 3, PowerTiming.OwnTurn, false,
                (VirtueType.Art, 2), (VirtueType.Energy, 3), (VirtueType.Wisdom, 4)),
            K("logone", "Logone", Blessing, "Blessing of Plenty", 4, PowerTiming.OwnTurn, false,
                (VirtueType.Wisdom, 3), (VirtueType.Nature, 4), (VirtueType.Economy, 2)),
            K("telalila", "Telalila", Time, "Time", 2, PowerTiming.OtherTurn, false,
                (VirtueType.Art, 4), (VirtueType.Energy, 3), (VirtueType.Nature, 2))
        };

        public static readonly string[] MvpKingdoms = { "egolica", "mask-of-light", "nevulandis" };

        public static KingdomDefinition Find(string id)
        {
            KingdomDefinition kingdom = All.FirstOrDefault(item => item.Id == id);
            if (kingdom == null)
                throw new RuleException("invalid_definition", "The saved kingdom definition is unsupported.");
            return kingdom;
        }

        public static bool Exists(string id) => All.Any(item => item.Id == id);

        public static string PowerOf(string kingdom) => Find(kingdom).Power;

        public static KingdomDefinition ByPower(string power) => All.FirstOrDefault(item => item.Power == power);

        public static string[] Pool(string ruleset) =>
            ruleset == Definitions.FullRuleset ? All.Select(item => item.Id).ToArray() : MvpKingdoms.ToArray();

        private static KingdomDefinition K(string id, string name, string power, string powerName, int cost,
            PowerTiming timing, bool hiddenOnly, params (VirtueType type, int count)[] goals) =>
            new KingdomDefinition(id, name, power, powerName, cost, timing, hiddenOnly,
                goals.Select(goal => new KeyValuePair<VirtueType, int>(goal.type, goal.count)).ToArray());
    }
}
