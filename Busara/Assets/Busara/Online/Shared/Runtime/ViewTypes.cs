using System;
using System.Collections.Generic;

namespace Busara.Online
{
    [Serializable]
    public sealed class LegalChoice
    {
        public string kind;
        public string label;
        public int from = -1;
        public int to = -1;
        public int resourceType = -1;
        public int paymentCost;
        public int count;
        public int virtueType = -1;
    }

    [Serializable]
    public sealed class PlayerView
    {
        public int seat;
        public string name;
        public bool joined;
        public bool ready;
        public string kingdom;
        public bool revealed;
        public bool virtuesVisible;
        public List<TokenState> virtues = new List<TokenState>();
        public List<ResourceType> setupRemaining = new List<ResourceType>();
        public string goal;
        public string kingdomId;
        public string powerName;
        public int powerCost;
    }

    [Serializable]
    public sealed class DecisionView
    {
        public string id;
        public string kind;
        public int owner;
        public string prompt;
        public int paymentCost;
        public List<TokenState> paymentOptions = new List<TokenState>();
        public string power;
        public string window;
        public int remaining;
    }

    [Serializable]
    public sealed class ClientView
    {
        public string matchId;
        public string ruleset;
        public string version;
        public string phase;
        public int seat;
        public int activeSeat;
        public int winner = -1;
        public bool draw;
        public bool awaitingOther;
        public List<PlayerView> players = new List<PlayerView>();
        public List<SlotState> board = new List<SlotState>();
        public DecisionView decision;
        public List<LegalChoice> choices = new List<LegalChoice>();
        public int controller = -1;
        public int extraActions;
        public List<int> extraTurns = new List<int>();
        public string notice;
    }
}
