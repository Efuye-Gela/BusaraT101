using System;
using System.Collections.Generic;

public enum ResourceType { Water, Earth, Fire, Air }
public enum VirtueType { Art, Security, Wisdom, Energy, Economy, Nature }

namespace Busara.Online
{
    [Serializable]
    public sealed class OnlineCommand
    {
        public string commandId;
        public string expectedVersion;
        public string decisionId;
        public string kind;
        public string name;
        public bool ready;
        public int from = -1;
        public int to = -1;
        public int resourceType = -1;
        public string[] paymentIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class GuestView
    {
        public string guestId;
        public string csrfToken;
        public string expiresAt;
    }

    [Serializable]
    public sealed class RoomResult
    {
        public string matchId;
        public string inviteUrl;
        public string version;
    }

    [Serializable]
    public sealed class CommandReceipt
    {
        public string commandId;
        public string matchId;
        public string version;
        public string status;
        public string code;
        public string message;
    }

    [Serializable]
    public sealed class TokenState
    {
        public string id;
        public VirtueType type;
    }

    [Serializable]
    public sealed class SlotState
    {
        public int id;
        public int seat;
        public string pieceId;
        public ResourceType type;
    }

    [Serializable]
    public sealed class CardState
    {
        public string id;
        public ResourceType type;
    }

    [Serializable]
    public sealed class SeatState
    {
        public int seat;
        public string name;
        public bool joined;
        public bool ready;
        public string kingdom;
        public bool revealed;
        public bool virtuesHidden;
        public List<string> knownKingdoms = new List<string>();
        public List<TokenState> virtues = new List<TokenState>();
        public List<ResourceType> setupRemaining = new List<ResourceType>();
    }

    [Serializable]
    public sealed class ActionSnapshot
    {
        public string actionId;
        public List<SeatState> seats = new List<SeatState>();
        public List<SlotState> board = new List<SlotState>();
        public List<CardState> deck = new List<CardState>();
        public int deckRemaining;
    }

    [Serializable]
    public sealed class ReactionPayment
    {
        public int seat;
        public List<TokenState> tokens = new List<TokenState>();
    }

    [Serializable]
    public sealed class PendingDecision
    {
        public string id;
        public string kind;
        public int owner;
        public string continuation;
        public int remaining;
        public CardState drawnCard;
    }

    [Serializable]
    public sealed class MatchState
    {
        public int schemaVersion = 1;
        public string ruleset = "busara-online-mvp-v1";
        public string id;
        public long version;
        public string phase = "Lobby";
        public int activeSeat;
        public int winner = -1;
        public bool draw;
        public int consecutiveEmptySkips;
        public List<SeatState> seats = new List<SeatState>();
        public List<SlotState> board = new List<SlotState>();
        public List<CardState> deck = new List<CardState>();
        public int deckRemaining;
        public PendingDecision pending;
        public ActionSnapshot snapshot;
        public List<ReactionPayment> reactionPayments = new List<ReactionPayment>();
    }

    [Serializable]
    public sealed class LegalChoice
    {
        public string kind;
        public string label;
        public int from = -1;
        public int to = -1;
        public int resourceType = -1;
        public int paymentCost;
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
    }

    public sealed class RuleException : Exception
    {
        public string Code { get; }
        public RuleException(string code, string message) : base(message) { Code = code; }
    }

    public interface IGameRandom { int Next(int exclusiveMaximum); }

    public sealed class TransitionResult
    {
        public MatchState state;
        public string eventKind;
        public string actionId;
    }
}
