using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Busara.Online
{
    public static class Projection
    {
        public static ClientView ForSeat(MatchState state, int viewer)
        {
            DomainRules.ValidateState(state);
            if (viewer < 0 || viewer > 1 || !state.seats[viewer].joined)
                throw new RuleException("forbidden", "You do not own a seat in this match.");
            var view = new ClientView
            {
                matchId = state.id, ruleset = state.ruleset, version = state.version.ToString(CultureInfo.InvariantCulture),
                phase = state.pending != null && state.pending.owner != viewer ? "Waiting" : state.phase,
                seat = viewer, activeSeat = state.activeSeat, winner = state.winner, draw = state.draw,
                awaitingOther = state.pending != null ? state.pending.owner != viewer :
                    state.phase != "Lobby" && state.phase != "Finished" && state.activeSeat != viewer,
                board = state.board.Select(DomainRules.Copy).ToList()
            };
            foreach (SeatState player in state.seats)
            {
                bool kingdomVisible = state.phase != "Lobby" && (player.seat == viewer || player.revealed ||
                    state.seats[viewer].knownKingdoms.Contains(player.kingdom));
                bool virtuesVisible = player.seat == viewer || !player.virtuesHidden;
                view.players.Add(new PlayerView
                {
                    seat = player.seat, name = player.name, joined = player.joined, ready = player.ready,
                    kingdom = kingdomVisible ? Definitions.KingdomName(player.kingdom) : null,
                    goal = kingdomVisible ? Definitions.GoalText(player.kingdom) : null,
                    revealed = player.revealed, virtuesVisible = virtuesVisible,
                    virtues = virtuesVisible ? player.virtues.Select(token => new TokenState
                    {
                        id = player.seat == viewer ? token.id : null, type = token.type
                    }).ToList() : new List<TokenState>(),
                    setupRemaining = player.seat == viewer ? new List<ResourceType>(player.setupRemaining) : new List<ResourceType>()
                });
            }
            if (state.phase == "Lobby")
            {
                Add(view, "configure", "Update name / ready");
                if (viewer == 0 && state.seats.All(player => player.joined && player.ready))
                    Add(view, "start", "Start match");
            }
            else if (state.phase == "Setup" && state.activeSeat == viewer)
                SetupChoices(state, view, viewer);
            else if (state.phase == "Action" && state.activeSeat == viewer)
                ActionChoices(state, view, viewer);
            else if (state.pending != null && state.pending.owner == viewer)
                DecisionChoices(state, view, viewer);
            return view;
        }

        private static void SetupChoices(MatchState state, ClientView view, int viewer)
        {
            foreach (SlotState target in state.board.Where(slot => slot.seat == viewer && slot.pieceId == null))
            {
                if (DomainRules.SetupSpace(state, viewer, target.id, -1))
                    foreach (ResourceType type in state.seats[viewer].setupRemaining.Distinct())
                        Add(view, "setupPlace", "Place " + type + " at #" + target.id, to: target.id, type: (int)type);
                foreach (SlotState source in state.board.Where(slot => slot.seat == viewer && slot.pieceId != null))
                    if (DomainRules.SetupSpace(state, viewer, target.id, source.id))
                        Add(view, "setupMove", "Rearrange " + source.type + " #" + source.id + " -> #" + target.id,
                            source.id, target.id);
            }
        }

        private static void ActionChoices(MatchState state, ClientView view, int viewer)
        {
            if (state.board.Any(slot => slot.seat == viewer && slot.pieceId == null))
                Add(view, "draw", "Draw a resource");
            foreach (SlotState source in state.board.Where(slot => slot.seat == viewer && slot.pieceId != null))
                foreach (SlotState target in state.board.Where(slot => SharedRules.Adjacent(source.id, slot.id)))
                    if (target.pieceId == null)
                        Add(view, "move", "Move " + source.type + " #" + source.id + " -> #" + target.id,
                            source.id, target.id);
                    else if (source.type != target.type)
                        Add(view, "forge", "Forge #" + source.id + " + #" + target.id + " -> " +
                            Definitions.Forge(source.type, target.type), source.id, target.id);
            if (state.seats[viewer].kingdom == Definitions.Egolica && !state.seats[viewer].revealed)
                Add(view, "abundance", "Use Abundance (reveal kingdom, add up to 2 resources)");
            if (state.seats[viewer].kingdom == Definitions.Knowledge && state.seats[viewer].virtues.Count >= 1)
                Add(view, "knowledge", "Use Infinite Knowledge (pay 1 virtue)", payment: 1);
        }

        private static void DecisionChoices(MatchState state, ClientView view, int viewer)
        {
            PendingDecision pending = state.pending;
            var decision = new DecisionView { id = pending.id, owner = viewer, kind = pending.kind };
            view.decision = decision;
            switch (pending.kind)
            {
                case "PlaceDraw":
                    decision.prompt = "Place the drawn " + pending.drawnCard.type + " on your board.";
                    foreach (SlotState target in state.board.Where(slot => slot.seat == viewer && slot.pieceId == null))
                        Add(view, "place", "Place " + pending.drawnCard.type + " at #" + target.id, to: target.id);
                    break;
                case "Abundance":
                    decision.prompt = "Abundance: choose a resource and space (" + pending.remaining + " remaining).";
                    foreach (SlotState target in state.board.Where(slot => slot.seat == viewer && slot.pieceId == null))
                        for (int type = 0; type < 4; type++)
                            if (DomainRules.ResourceStock(state, (ResourceType)type) > 0)
                                Add(view, "abundancePlace", "Add " + (ResourceType)type + " at #" + target.id,
                                    to: target.id, type: type);
                    break;
                case "Retraction":
                    decision.prompt = "Your decision: undo the completed opposing action for 2 virtues, or pass.";
                    decision.paymentCost = 2;
                    List<TokenState> owned = state.seats[viewer].virtues;
                    var payable = new HashSet<string>();
                    for (int first = 0; first < owned.Count; first++)
                        for (int second = first + 1; second < owned.Count; second++)
                            if (DomainRules.CanRestorePayment(state, viewer, new[] { owned[first].id, owned[second].id }))
                            {
                                payable.Add(owned[first].id);
                                payable.Add(owned[second].id);
                            }
                    decision.paymentOptions = owned.Where(token => payable.Contains(token.id)).Select(DomainRules.Copy).ToList();
                    if (decision.paymentOptions.Count >= 2)
                        Add(view, "use", "Use Retraction (pay 2 virtues)", payment: 2);
                    else
                        decision.prompt += " No payment survives undoing this action.";
                    Add(view, "pass", "Pass");
                    break;
                case "Knowledge":
                    decision.prompt = "Private information: " + Definitions.KingdomName(state.seats[1 - viewer].kingdom) +
                        ". Goal: " + Definitions.GoalText(state.seats[1 - viewer].kingdom) + ".";
                    Add(view, "ack", "Continue");
                    break;
                case "RetractionNotice":
                    decision.prompt = "The opposing action was undone. Your selected 2 virtues remain spent. Continue to end that turn.";
                    Add(view, "ack", "Continue");
                    break;
                case "AbundanceNotice":
                    decision.prompt = "No more resources can be added. The rest of Abundance is skipped.";
                    Add(view, "ack", "Continue");
                    break;
                default:
                    throw new RuleException("invalid_state", "The saved decision is unsupported.");
            }
        }

        private static void Add(ClientView view, string kind, string label, int from = -1, int to = -1,
            int type = -1, int payment = 0)
        {
            view.choices.Add(new LegalChoice
            {
                kind = kind, label = label, from = from, to = to, resourceType = type, paymentCost = payment
            });
        }
    }
}
