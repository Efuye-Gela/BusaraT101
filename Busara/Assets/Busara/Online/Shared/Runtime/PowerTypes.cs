using System;
using System.Collections.Generic;

namespace Busara.Online
{
    public static class PowerWindow
    {
        public const string Own = "Own", TurnStart = "TurnStart", AfterAction = "AfterAction", Dome = "Dome",
            Necklace = "Necklace";
    }

    // Durable state of one committed power while its effect resolves across player decisions.
    [Serializable]
    public sealed class PowerUseState
    {
        public string id;
        public string power;
        public int caster = -1;
        public int target = -1;
        public string window;
        public int virtueType = -1;
        public List<string> exchangeIds = new List<string>();
        public int count;
        public int remaining;
        public List<ResourceType> copies = new List<ResourceType>();
        public int rainSeat;
        public bool cancelled;

        public PowerUseState Copy() => new PowerUseState
        {
            id = id, power = power, caster = caster, target = target, window = window, virtueType = virtueType,
            exchangeIds = new List<string>(exchangeIds), count = count, remaining = remaining,
            copies = new List<ResourceType>(copies), rainSeat = rainSeat, cancelled = cancelled
        };
    }
}
