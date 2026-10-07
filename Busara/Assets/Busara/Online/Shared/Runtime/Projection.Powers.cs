using System.Collections.Generic;
using System.Linq;

namespace Busara.Online
{
    // Full-ruleset choices. Every list is derived from server state for the viewer's own decision only.
    public static partial class Projection
    {
        private static void FullStatus(MatchState state, ClientView view)
        {
            view.controller = state.controller;
            view.extraActions = state.extraActions;
            view.extraTurns = new List<int>(state.extraTurns);
            view.notice = state.notice;
        }

        private static void FullPowerChoices(MatchState state, ClientView view, int seat)
        {
            KingdomDefinition kingdom = KingdomCatalog.Find(state.seats[seat].kingdom);
            if (kingdom.Timing != PowerTiming.OwnTurn || !DomainRules.CanUse(state, seat) ||
                (state.controller >= 0 && !DomainRules.ControllerSees(state)))
                return;
            string label = "Use " + kingdom.PowerName + " (pay " + kingdom.Cost + " virtue" + (kingdom.Cost == 1 ? "" : "s") + ")";
            SeatState target = state.seats[1 - seat];
            switch (kingdom.Power)
            {
                case KingdomCatalog.Rain:
                    foreach (int count in new[] { 1, 2, 3, -1, -2, -3 })
                        Add(view, "power", label + ": each player " + (count > 0 ? "adds " : "removes ") +
                            System.Math.Abs(count), payment: kingdom.Cost, count: count);
                    break;
                case KingdomCatalog.Imagination:
                    IEnumerable<int> types = target.virtuesHidden ? Enumerable.Range(0, 6) :
                        target.virtues.Select(token => (int)token.type).Distinct().OrderBy(type => type);
                    foreach (int type in types)
                        Add(view, "power", label + ": take " + (VirtueType)type, payment: kingdom.Cost, virtue: type);
                    break;
                case KingdomCatalog.Transform:
                    for (int type = 0; type < 6; type++)
                        Add(view, "power", label + ": exchange virtues for " + (VirtueType)type,
                            payment: kingdom.Cost, virtue: type);
                    break;
                case KingdomCatalog.Invisibility:
                    if (state.board.Any(slot => slot.seat == target.seat && slot.pieceId != null) &&
                        state.board.Any(slot => slot.seat == seat && slot.pieceId == null))
                        Add(view, "power", label, payment: kingdom.Cost);
                    break;
                default:
                    Add(view, "power", label, payment: kingdom.Cost);
                    break;
            }
        }

        private static void FullDecisionChoices(MatchState state, ClientView view, int viewer)
        {
            PendingDecision pending = state.pending;
            var decision = new DecisionView
            {
                id = pending.id, owner = pending.owner, kind = pending.kind, power = pending.power,
                window = pending.window, remaining = pending.remaining
            };
            view.decision = decision;
            int owner = pending.owner;
            string power = pending.power == null ? null : KingdomCatalog.ByPower(pending.power).PowerName;
            switch (pending.kind)
            {
                case "PlaceDraw":
                    decision.prompt = "Place the drawn " + pending.drawnCard.type + " on " +
                        (owner == viewer ? "your" : state.seats[owner].name + "'s") + " board.";
                    foreach (SlotState target in EmptySlots(state, owner))
                        Add(view, "place", "Place " + pending.drawnCard.type + " at #" + target.id, to: target.id);
                    break;
                case "Reaction":
                    ReactionChoices(state, view, viewer, power);
                    break;
                case "Retraction":
                    RetractionChoices(state, view, viewer);
                    break;
                case "AddResource":
                    decision.prompt = power + ": add a resource to your board (" + pending.remaining + " remaining).";
                    foreach (SlotState target in EmptySlots(state, owner))
                        foreach (ResourceType type in DomainRules.AddableTypes(state))
                            Add(view, "addResource", "Add " + type + " at #" + target.id, to: target.id, type: (int)type);
                    break;
                case "RemoveResource":
                    decision.prompt = power + ": remove a resource from your board (" + pending.remaining + " remaining).";
                    foreach (SlotState slot in Pieces(state, owner))
                        Add(view, "removeResource", "Remove " + slot.type + " #" + slot.id, to: slot.id);
                    break;
                case "Rearrange":
                    decision.prompt = power + ": rearrange resources within your kingdom, then finish.";
                    foreach (SlotState source in Pieces(state, owner))
                        foreach (SlotState target in state.board.Where(slot => slot.seat == owner && slot.id != source.id))
                            Add(view, "witchMove", (target.pieceId == null ? "Move " : "Swap ") + source.type + " #" +
                                source.id + " -> #" + target.id, source.id, target.id);
                    Add(view, "witchFinish", "Finish rearranging");
                    break;
                case "Steal":
                    decision.prompt = power + ": choose an opposing resource and an empty space on your board.";
                    foreach (SlotState source in Pieces(state, state.power.target))
                        foreach (SlotState target in EmptySlots(state, owner))
                            Add(view, "steal", "Steal " + source.type + " #" + source.id + " -> #" + target.id,
                                source.id, target.id);
                    break;
                case "Knowledge":
                    string kingdom = state.seats[state.power.target].kingdom;
                    decision.prompt = "Private information: " + Definitions.KingdomName(kingdom) +
                        ". Goal: " + Definitions.GoalText(kingdom) + ".";
                    Add(view, "ack", "Continue");
                    break;
                case "RetractionNotice":
                    decision.prompt = "The opposing action was undone. Your payment remains spent. Continue.";
                    Add(view, "ack", "Continue");
                    break;
                default:
                    ExpandedDecisionChoices(state, view, viewer);
                    break;
            }
        }

        private static IEnumerable<SlotState> EmptySlots(MatchState state, int seat) =>
            state.board.Where(slot => slot.seat == seat && slot.pieceId == null);

        private static IEnumerable<SlotState> Pieces(MatchState state, int seat) =>
            state.board.Where(slot => slot.seat == seat && slot.pieceId != null);
    }
}
