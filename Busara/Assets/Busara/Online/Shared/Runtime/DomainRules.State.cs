using System.Collections.Generic;
using System.Linq;

namespace Busara.Online
{
    public static partial class DomainRules
    {
        public static MatchState Create(string matchId)
        {
            Require(!string.IsNullOrWhiteSpace(matchId), "invalid_match", "A match ID is required.");
            var state = new MatchState { id = matchId, version = 1 };
            for (int seat = 0; seat < 2; seat++)
                state.seats.Add(new SeatState { seat = seat, joined = seat == 0, name = seat == 0 ? "Host" : "Guest" });
            for (int id = 0; id < 32; id++)
                state.board.Add(new SlotState { id = id, seat = id % 8 < 4 ? 0 : 1 });
            return state;
        }

        public static MatchState Join(MatchState current, int seat, string name)
        {
            ValidateState(current);
            Require(current.phase == "Lobby" && seat == 1 && !current.seats[1].joined,
                "room_unavailable", "This invitation cannot claim a seat.");
            MatchState state = Clone(current);
            state.seats[seat].joined = true;
            state.seats[seat].name = ValidName(name);
            state.version++;
            return state;
        }

        public static ActionSnapshot Capture(MatchState state) => new ActionSnapshot
        {
            actionId = NewId(), seats = state.seats.Select(Copy).ToList(), board = state.board.Select(Copy).ToList(),
            deck = state.deck.Select(Copy).ToList(), deckRemaining = state.deckRemaining
        };

        public static MatchState Clone(MatchState state) => new MatchState
        {
            schemaVersion = state.schemaVersion, ruleset = state.ruleset, id = state.id, version = state.version,
            phase = state.phase, activeSeat = state.activeSeat, winner = state.winner, draw = state.draw,
            consecutiveEmptySkips = state.consecutiveEmptySkips, seats = state.seats.Select(Copy).ToList(),
            board = state.board.Select(Copy).ToList(), deck = state.deck.Select(Copy).ToList(),
            deckRemaining = state.deckRemaining,
            pending = state.pending == null ? null : new PendingDecision
            {
                id = state.pending.id, kind = state.pending.kind, owner = state.pending.owner,
                continuation = state.pending.continuation, remaining = state.pending.remaining,
                offerFrom = state.pending.offerFrom, offerResourceType = state.pending.offerResourceType,
                power = state.pending.power, window = state.pending.window,
                drawnCard = state.pending.drawnCard == null ? null : Copy(state.pending.drawnCard)
            },
            snapshot = state.snapshot == null ? null : new ActionSnapshot
            {
                actionId = state.snapshot.actionId, seats = state.snapshot.seats.Select(Copy).ToList(),
                board = state.snapshot.board.Select(Copy).ToList(), deck = state.snapshot.deck.Select(Copy).ToList(),
                deckRemaining = state.snapshot.deckRemaining
            },
            reactionPayments = state.reactionPayments.Select(payment => new ReactionPayment
            {
                seat = payment.seat, tokens = payment.tokens.Select(Copy).ToList()
            }).ToList(),
            power = state.power?.Copy(), controller = state.controller, extraActions = state.extraActions,
            extraTurns = new List<int>(state.extraTurns), resumeSeat = state.resumeSeat, retracted = state.retracted,
            notice = state.notice
        };

        public static SlotState Copy(SlotState slot) => new SlotState
        {
            id = slot.id, seat = slot.seat, pieceId = slot.pieceId, type = slot.type
        };
        public static TokenState Copy(TokenState token) => new TokenState { id = token.id, type = token.type };
        public static CardState Copy(CardState card) => new CardState { id = card.id, type = card.type };
        private static SeatState Copy(SeatState seat) => new SeatState
        {
            seat = seat.seat, name = seat.name, joined = seat.joined, ready = seat.ready, kingdom = seat.kingdom,
            revealed = seat.revealed, virtuesHidden = seat.virtuesHidden, virtues = seat.virtues.Select(Copy).ToList(),
            knownKingdoms = new List<string>(seat.knownKingdoms), setupRemaining = new List<ResourceType>(seat.setupRemaining)
        };
    }
}
