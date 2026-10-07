using System.Linq;

namespace Busara.Online
{
    public static partial class Projection
    {
        private static void ExpandedActionChoices(MatchState state, ClientView view, int viewer)
        {
            if (view.choices.Any(choice => choice.kind == "forge"))
                Add(view, "forgeChain", "Forge an ordered resource chain");
            if (OrdinarySelection.HasWeapon(state.board, viewer))
                Add(view, "weapon", "Create a weapon from three connected resources");
            foreach (SlotState source in state.board.Where(slot => slot.seat == viewer && slot.pieceId != null &&
                state.controller < 0))
                for (int type = 0; type < 4; type++)
                    if (type != (int)source.type)
                        Add(view, "trade", "Offer " + source.type + " #" + source.id + " for " + (ResourceType)type,
                            from: source.id, type: type);
        }

        private static void ExpandedDecisionChoices(MatchState state, ClientView view, int viewer)
        {
            PendingDecision pending = state.pending;
            switch (pending.kind)
            {
                case "WeaponDiscard":
                    view.decision.prompt = "The weapon is complete. Choose one of your own resources to discard.";
                    foreach (SlotState slot in state.board.Where(slot => slot.seat == viewer && slot.pieceId != null))
                        Add(view, "weaponDiscard", "Discard " + slot.type + " #" + slot.id, to: slot.id);
                    break;
                case "TradeResponse":
                    SlotState offer = state.board.Single(slot => slot.id == pending.offerFrom);
                    view.decision.prompt = "Trade offer: " + offer.type + " #" + offer.id + " for your " +
                        (ResourceType)pending.offerResourceType + ". Accept or reject?";
                    if (state.board.Any(slot => slot.seat == viewer && slot.pieceId != null &&
                        (int)slot.type == pending.offerResourceType))
                        Add(view, "tradeAccept", "Accept trade");
                    Add(view, "tradeReject", "Reject trade");
                    break;
                case "TradeSelect":
                    view.decision.prompt = "Trade accepted. Confirm the opposing resource to receive, or cancel.";
                    foreach (SlotState slot in state.board.Where(slot => slot.seat != viewer && slot.pieceId != null &&
                        (int)slot.type == pending.offerResourceType))
                        Add(view, "tradeComplete", "Swap with " + slot.type + " #" + slot.id, to: slot.id);
                    Add(view, "tradeCancel", "Cancel trade");
                    break;
                case "TradeDeclined":
                    view.decision.prompt = "Your trade was declined. Return to choose an ordinary action.";
                    Add(view, "tradeCancel", "Return to actions");
                    break;
                default:
                    throw new RuleException("invalid_state", "The saved decision is unsupported.");
            }
        }
    }
}
