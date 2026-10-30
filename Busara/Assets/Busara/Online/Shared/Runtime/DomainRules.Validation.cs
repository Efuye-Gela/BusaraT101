using System.Collections.Generic;
using System.Linq;

namespace Busara.Online
{
    public static partial class DomainRules
    {
        public static void ValidateState(MatchState state)
        {
            Require(state != null && state.schemaVersion == 1 && Definitions.IsKnown(state.ruleset),
                "unsupported_state", "This saved match uses an unsupported state or ruleset version.");
            ValidateWorld(state.seats, state.board);
            Require(!string.IsNullOrWhiteSpace(state.id) && state.version >= 1 && state.activeSeat >= 0 &&
                state.activeSeat < 2 && state.winner >= -1 && state.winner <= 1,
                "invalid_state", "The saved match identity or turn is inconsistent.");
            Require(new[] { "Lobby", "Setup", "Action", "Decision", "Finished" }.Contains(state.phase),
                "invalid_state", "The saved phase is unsupported.");
            Require((state.phase == "Decision") == (state.pending != null),
                "invalid_state", "The saved decision state is inconsistent.");
            Require(state.reactionPayments != null && state.reactionPayments.All(payment =>
                payment != null && payment.seat >= 0 && payment.seat < 2 && payment.tokens != null &&
                payment.tokens.All(ValidToken)), "invalid_state", "The saved payment ledger is inconsistent.");
            if (state.phase != "Lobby")
            {
                Require(state.deck != null && state.deck.Count == 24 && state.seats.All(seat =>
                    KingdomCatalog.Pool(state.ruleset).Contains(seat.kingdom)) &&
                    state.seats.Select(seat => seat.kingdom).Distinct().Count() == 2,
                    "invalid_state", "The saved definitions or deck are inconsistent.");
                Require(state.deck.All(card => card != null && !string.IsNullOrWhiteSpace(card.id) &&
                    (int)card.type >= 0 && (int)card.type < 4) &&
                    state.deck.Select(card => card.id).Distinct().Count() == 24 &&
                    state.deckRemaining >= 0 && state.deckRemaining <= 24,
                    "invalid_state", "The saved deck order or cursor is inconsistent.");
            }
            if (state.phase == "Action" || state.phase == "Decision")
            {
                Require(state.snapshot != null && !string.IsNullOrWhiteSpace(state.snapshot.actionId),
                    "invalid_state", "The saved action snapshot is missing.");
                ValidateWorld(state.snapshot.seats, state.snapshot.board);
                Require(state.snapshot.deck != null && state.snapshot.deck.Count == 24 &&
                    state.snapshot.deck.All(card => card != null) && state.snapshot.deckRemaining >= 0 &&
                    state.snapshot.deckRemaining <= 24, "invalid_state", "The saved snapshot deck is inconsistent.");
            }
            if (state.pending != null)
                ValidateDecision(state);
            ValidatePowerState(state);
        }

        private static void ValidateDecision(MatchState state)
        {
            if (Definitions.IsFull(state.ruleset))
            {
                ValidateFullDecision(state);
                return;
            }
            PendingDecision pending = state.pending;
            bool expanded = new[] { "WeaponDiscard", "TradeResponse", "TradeSelect", "TradeDeclined" }.Contains(pending.kind);
            Require(!string.IsNullOrWhiteSpace(pending.id) && pending.owner >= 0 && pending.owner < 2 &&
                (expanded ? Definitions.IsExpanded(state.ruleset) : new[] { "PlaceDraw", "Abundance", "Retraction",
                    "Knowledge", "RetractionNotice", "AbundanceNotice" }.Contains(pending.kind)),
                "invalid_state", "The saved decision is unsupported.");
            bool reaction = pending.kind == "Retraction" || pending.kind == "RetractionNotice";
            string continuation = reaction ? "TurnEnd" : pending.kind == "TradeResponse" ? "TradeSelect" :
                pending.kind == "TradeDeclined" ? "Action" : "AfterAction";
            Require(pending.continuation == continuation,
                "invalid_state", "The saved continuation is unsupported.");
            bool opposing = reaction || pending.kind == "WeaponDiscard" || pending.kind == "TradeResponse";
            Require(pending.owner == (opposing ? 1 - state.activeSeat : state.activeSeat),
                "invalid_state", "The saved decision owner is inconsistent.");
            if (pending.kind == "PlaceDraw")
                Require(pending.drawnCard != null && state.deck.Any(card =>
                    card.id == pending.drawnCard.id && card.type == pending.drawnCard.type),
                    "invalid_state", "The saved drawn card is inconsistent.");
            if (pending.kind == "Abundance")
                Require(pending.remaining >= 1 && pending.remaining <= 2,
                    "invalid_state", "The saved resource continuation is inconsistent.");
            if (!expanded)
                return;
            Require(pending.drawnCard == null && pending.remaining == 0,
                "invalid_state", "The saved ordinary action continuation is inconsistent.");
            if (pending.kind == "WeaponDiscard")
                Require(pending.offerFrom == -1 && pending.offerResourceType == -1 &&
                    state.board.Any(slot => slot.seat == pending.owner && slot.pieceId != null),
                    "invalid_state", "The saved discard is inconsistent.");
            else
                ValidateOffer(state);
        }

        private static void ValidateOffer(MatchState state)
        {
            PendingDecision pending = state.pending;
            SlotState source = state.board.SingleOrDefault(slot => slot.id == pending.offerFrom);
            Require(source != null && source.seat == state.activeSeat && source.pieceId != null &&
                pending.offerResourceType >= 0 && pending.offerResourceType < 4 &&
                pending.offerResourceType != (int)source.type,
                "invalid_state", "The saved trade offer is inconsistent.");
            SlotState original = state.snapshot.board.Single(slot => slot.id == source.id);
            Require(original.pieceId == source.pieceId && original.type == source.type,
                "invalid_state", "The saved offered resource changed.");
            if (pending.kind == "TradeSelect")
                Require(state.board.Any(slot => slot.seat == 1 - state.activeSeat && slot.pieceId != null &&
                    (int)slot.type == pending.offerResourceType),
                    "invalid_state", "The saved accepted trade has no matching resource.");
        }

        private static void ValidateWorld(List<SeatState> seats, List<SlotState> board)
        {
            Require(seats != null && seats.Count == 2 && seats[0]?.seat == 0 && seats[1]?.seat == 1 &&
                seats.All(seat => seat.virtues != null && seat.knownKingdoms != null && seat.setupRemaining != null &&
                    seat.virtues.All(ValidToken)), "invalid_state", "The saved players or inventories are inconsistent.");
            var tokens = seats.SelectMany(seat => seat.virtues).ToList();
            Require(tokens.Select(token => token.id).Distinct().Count() == tokens.Count,
                "invalid_state", "A saved virtue token has multiple owners.");
            Require(board != null && board.Count == 32 && board.All(slot => slot != null &&
                slot.id >= 0 && slot.id < 32 && slot.seat == (slot.id % 8 < 4 ? 0 : 1) &&
                (int)slot.type >= 0 && (int)slot.type < 4 &&
                (slot.pieceId == null || !string.IsNullOrWhiteSpace(slot.pieceId))) &&
                board.Select(slot => slot.id).Distinct().Count() == 32,
                "invalid_state", "The saved board geometry is inconsistent.");
            var pieces = board.Where(slot => slot.pieceId != null).Select(slot => slot.pieceId).ToList();
            Require(pieces.Distinct().Count() == pieces.Count, "invalid_state", "A saved resource occupies multiple spaces.");
        }

        private static bool ValidToken(TokenState token) =>
            token != null && !string.IsNullOrWhiteSpace(token.id) && (int)token.type >= 0 && (int)token.type < 6;
    }
}
