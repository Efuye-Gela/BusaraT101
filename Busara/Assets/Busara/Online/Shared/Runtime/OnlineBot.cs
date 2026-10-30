using System;
using System.Collections.Generic;
using System.Linq;

namespace Busara.Online
{
    // Server-side bots for v4 matches. A bot sees only its own seat's projection and plays ordinary actions:
    // setup, draw and place, moves and forges that work toward its kingdom's goal. It rejects trades, passes
    // reactions and leaves its kingdom power unused.
    public static class OnlineBot
    {
        public const int MaximumSteps = 200;

        // The seat that must act next, or -1 when the match waits for nobody.
        public static int NextActor(MatchState state)
        {
            if (state.phase == "Lobby" || state.phase == "Finished")
                return -1;
            if (state.pending != null)
                return DomainRules.DecisionActor(state);
            return state.phase == "Action" ? DomainRules.ActingSeat(state) : state.activeSeat;
        }

        // Plays every bot turn and decision until a human must act, the match ends, or the step limit is reached.
        public static List<TransitionResult> Run(MatchState state, IGameRandom random)
        {
            var transitions = new List<TransitionResult>();
            for (int step = 0; step < MaximumSteps; step++)
            {
                int actor = NextActor(state);
                if (actor < 0 || !state.seats[actor].bot)
                    break;
                TransitionResult transition;
                try { transition = DomainRules.Apply(state, actor, Choose(state, actor), random); }
                catch (RuleException) { break; }   // a stuck bot leaves the match as it is; nothing is half-applied
                transitions.Add(transition);
                state = transition.state;
            }
            return transitions;
        }

        public static OnlineCommand Choose(MatchState state, int seat)
        {
            ClientView view = Projection.ForBot(state, seat);
            LegalChoice choice = Pick(state, view, seat);
            if (choice == null)
                throw new RuleException("bot_stuck", "The bot has no legal choice.");
            return new OnlineCommand
            {
                commandId = Guid.NewGuid().ToString(), expectedVersion = view.version, decisionId = view.decision?.id,
                kind = choice.kind, from = choice.from, to = choice.to, resourceType = choice.resourceType,
                target = choice.target, count = choice.count, virtueType = choice.virtueType
            };
        }

        private static LegalChoice Pick(MatchState state, ClientView view, int seat)
        {
            List<LegalChoice> choices = view.choices;
            LegalChoice First(params string[] kinds) => choices.FirstOrDefault(choice => kinds.Contains(choice.kind));
            if (view.phase == "Setup")
                return choices.Where(choice => choice.kind == "setupPlace")
                    .OrderByDescending(choice => Checkerboard(choice.to)).ThenBy(choice => choice.to).FirstOrDefault();
            if (view.decision != null)
            {
                switch (view.decision.kind)
                {
                    case "PlaceDraw":
                        return BestPlacement(state, view, seat, choices.Where(choice => choice.kind == "place"));
                    case "Reaction":
                    case "Retraction":
                        return First("pass");
                    case "TradeResponse":
                        return First("tradeReject");
                    case "TradeSelect":
                    case "TradeDeclined":
                        return First("tradeCancel", "tradeComplete");
                    case "Rearrange":
                        return First("witchFinish");
                }
                return First("ack", "weaponDiscard", "removeResource", "addResource", "abundancePlace", "steal",
                    "tradeComplete", "pass") ?? choices.FirstOrDefault(choice => choice.paymentCost == 0);
            }
            return BestAction(state, view, seat);
        }

        private static int Checkerboard(int id) => (id / 8 + id % 8) % 2 == 0 ? 1 : 0;

        private static Dictionary<VirtueType, int> Needs(ClientView view, int seat)
        {
            PlayerView self = view.players.Single(player => player.seat == seat);
            var needs = new Dictionary<VirtueType, int>();
            if (string.IsNullOrEmpty(self.kingdomId))
                return needs;
            foreach (var goal in Definitions.Goals(self.kingdomId))
                needs[goal.Key] = Math.Max(0, goal.Value - self.virtues.Count(token => token.type == goal.Key));
            return needs;
        }

        private static int Need(Dictionary<VirtueType, int> needs, VirtueType type) =>
            needs.TryGetValue(type, out int need) ? need : 0;

        // Forge toward the goal when it helps; otherwise move a piece next to a useful partner; otherwise draw.
        private static LegalChoice BestAction(MatchState state, ClientView view, int seat)
        {
            var needs = Needs(view, seat);
            int owned = view.board.Count(slot => slot.seat == seat && slot.pieceId != null);
            SlotState At(int id) => view.board.Single(slot => slot.id == id);
            int Value(LegalChoice choice)
            {
                switch (choice.kind)
                {
                    case "forge":
                        int need = Need(needs, Definitions.Forge(At(choice.from).type, At(choice.to).type));
                        return need > 0 ? 100 + need * 10 : owned >= 14 ? 20 : 1;
                    case "move":
                        ResourceType moving = At(choice.from).type;
                        int partners = view.board.Count(slot => slot.pieceId != null && slot.id != choice.from &&
                            slot.seat == seat && slot.type != moving && SharedRules.Adjacent(slot.id, choice.to) &&
                            Need(needs, Definitions.Forge(moving, slot.type)) > 0);
                        bool home = At(choice.to).seat == seat;
                        return partners > 0 && home ? 40 + partners * 5 : home ? 5 : 2;
                    case "draw":
                        return 30;
                    default:
                        return int.MinValue;
                }
            }
            return view.choices.Where(choice => choice.paymentCost == 0)
                .Select(choice => (choice, value: Value(choice)))
                .Where(item => item.value > int.MinValue)
                .OrderByDescending(item => item.value).ThenBy(item => item.choice.from).ThenBy(item => item.choice.to)
                .Select(item => item.choice).FirstOrDefault()
                ?? view.choices.FirstOrDefault(choice => choice.kind == "pass" || choice.kind == "ack");
        }

        private static LegalChoice BestPlacement(MatchState state, ClientView view, int seat, IEnumerable<LegalChoice> places)
        {
            var needs = Needs(view, seat);
            ResourceType drawn = state.pending.drawnCard.type;
            return places.OrderByDescending(choice => view.board.Count(slot => slot.pieceId != null && slot.seat == seat &&
                    slot.type != drawn && SharedRules.Adjacent(slot.id, choice.to) &&
                    Need(needs, Definitions.Forge(drawn, slot.type)) > 0))
                .ThenBy(choice => choice.to).FirstOrDefault();
        }
    }
}
