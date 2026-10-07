using System.Collections.Generic;
using System.Linq;

namespace Busara.Online
{
    public static partial class DomainRules
    {
        private static void RequireExpanded(MatchState state) =>
            Require(Definitions.IsExpanded(state.ruleset), "unsupported_command",
                "This action is available only in new matches using the expanded rules.");

        private static void ForgePair(MatchState state, int seat, int from, int to)
        {
            RequireAction(state, seat);
            SlotState first = OwnedPiece(state, seat, from);
            SlotState second = Piece(state, to);
            Require(first.id != second.id && SharedRules.Adjacent(first.id, second.id),
                "invalid_forge", "Choose an ordered pair starting with your resource and an adjacent resource.");
            Definitions.Forge(first.type, second.type);
            Forge(state, seat, new[] { from, to });
        }

        private static void Forge(MatchState state, int seat, IList<int> slots)
        {
            RequireAction(state, seat);
            string error = OrdinarySelection.Error(state.board, seat, slots, false);
            Require(error == null, "invalid_forge", error);
            var selected = slots.Select(id => Slot(state, id)).ToList();
            var recipients = new HashSet<int> { selected[0].seat };
            for (int index = 1; index < selected.Count; index++)
            {
                recipients.Add(selected[index].seat);
                VirtueType virtue = Definitions.Forge(selected[index - 1].type, selected[index].type);
                // Offline forge recipients accumulate across the entire ordered chain.
                foreach (int recipient in recipients)
                    state.seats[recipient].virtues.Add(new TokenState { id = NewId(), type = virtue });
            }
            foreach (SlotState slot in selected)
                slot.pieceId = null;
            AfterAction(state);
        }

        private static void Weapon(MatchState state, int seat, IList<int> slots)
        {
            RequireExpanded(state);
            RequireAction(state, seat);
            string error = OrdinarySelection.Error(state.board, seat, slots, true);
            Require(error == null, "invalid_weapon", error);
            foreach (int id in slots)
                Slot(state, id).pieceId = null;
            int opponent = 1 - seat;
            if (!state.board.Any(slot => slot.seat == opponent && slot.pieceId != null))
                AfterAction(state);
            else if (Definitions.IsFull(state.ruleset) && OfferReaction(state, opponent, PowerWindow.Dome))
                return;
            else
                Decide(state, opponent, "WeaponDiscard", "AfterAction");
        }

        private static void WeaponDiscard(MatchState state, int seat, OnlineCommand command)
        {
            RequireExpanded(state);
            RequireDecision(state, seat, command, "WeaponDiscard");
            OwnedPiece(state, seat, command.to).pieceId = null;
            AfterAction(state);
        }

        private static void Trade(MatchState state, int seat, OnlineCommand command)
        {
            RequireExpanded(state);
            RequireAction(state, seat);
            Require(state.controller < 0, "controlled_turn", "Trading is not allowed during a controlled turn.");
            SlotState offered = OwnedPiece(state, seat, command.from);
            ResourceType requested = Resource(command.resourceType);
            Require(offered.type != requested, "invalid_trade", "Request a different resource type.");
            Decide(state, 1 - seat, "TradeResponse", "TradeSelect");
            state.pending.offerFrom = offered.id;
            state.pending.offerResourceType = (int)requested;
        }

        private static void TradeResponse(MatchState state, int seat, OnlineCommand command, bool accept)
        {
            RequireExpanded(state);
            RequireDecision(state, seat, command, "TradeResponse");
            if (accept)
                Require(state.board.Any(slot => slot.seat == seat && slot.pieceId != null &&
                    (int)slot.type == state.pending.offerResourceType),
                    "invalid_trade", "You do not own the requested resource.");
            int from = state.pending.offerFrom;
            int resource = state.pending.offerResourceType;
            Decide(state, state.activeSeat, accept ? "TradeSelect" : "TradeDeclined", accept ? "AfterAction" : "Action");
            state.pending.offerFrom = from;
            state.pending.offerResourceType = resource;
        }

        private static void TradeComplete(MatchState state, int seat, OnlineCommand command)
        {
            RequireExpanded(state);
            RequireDecision(state, seat, command, "TradeSelect");
            SlotState source = OwnedPiece(state, seat, state.pending.offerFrom);
            SlotState target = OwnedPiece(state, 1 - seat, command.to);
            Require((int)target.type == state.pending.offerResourceType,
                "invalid_trade", "Choose an opposing resource of the requested type.");
            string piece = source.pieceId;
            ResourceType type = source.type;
            source.pieceId = target.pieceId;
            source.type = target.type;
            target.pieceId = piece;
            target.type = type;
            AfterAction(state);
        }

        private static void TradeCancel(MatchState state, int seat, OnlineCommand command)
        {
            RequireExpanded(state);
            Require(state.pending != null && (state.pending.kind == "TradeSelect" || state.pending.kind == "TradeDeclined"),
                "stale_decision", "There is no accepted or declined trade to cancel.");
            RequireDecision(state, seat, command, state.pending.kind);
            state.pending = null;
            state.phase = "Action";
        }
    }
}
