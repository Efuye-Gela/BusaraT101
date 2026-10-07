using System.Linq;

namespace Busara.Online
{
    // Full-ruleset turn windows mirroring PowerManager.BeginNormalTurn/CompleteAction/NextPlayer.
    public static partial class DomainRules
    {
        private static readonly string[] ControlledKinds =
            { "draw", "place", "move", "forge", "forgeChain", "weapon", "trade", "power", "abundance", "knowledge" };

        public static int ActingSeat(MatchState state) => state.controller >= 0 ? state.controller : state.activeSeat;

        // A controller chooses the active player's action; power effects and reactions stay with their owners.
        public static int DecisionActor(MatchState state) =>
            state.pending != null && state.pending.kind == "PlaceDraw" && state.controller >= 0
                ? state.controller : state.pending?.owner ?? -1;

        private static int MapControlledSeat(MatchState state, int seat, string kind)
        {
            if (state.controller < 0 || !ControlledKinds.Contains(kind))
                return seat;
            Require(seat == state.controller, "controlled_turn", "Your opponent controls this turn's action.");
            return state.activeSeat;
        }

        private static void BeginFullTurn(MatchState state)
        {
            // This is the existing empty-board game rule, not a disconnect timeout.
            while (!state.board.Any(slot => slot.seat == state.activeSeat && slot.pieceId != null))
            {
                state.consecutiveEmptySkips++;
                if (state.consecutiveEmptySkips > 2)
                {
                    state.phase = "Finished";
                    state.draw = true;
                    return;
                }
                if (Win(state))
                    return;
                state.activeSeat = NextSeat(state);
            }
            state.consecutiveEmptySkips = 0;
            state.phase = "Action";
            state.pending = null;
            state.snapshot = Capture(state);
            if (!OfferReaction(state, 1 - state.activeSeat, PowerWindow.TurnStart))
                StartAction(state);
        }

        private static void StartAction(MatchState state)
        {
            state.pending = null;
            state.phase = "Action";
            state.snapshot = Capture(state);
            state.reactionPayments.Clear();
        }

        private static void AfterActionWindow(MatchState state)
        {
            state.pending = null;
            state.phase = "Action";
            if (!OfferReaction(state, 1 - state.activeSeat, PowerWindow.AfterAction))
                EndAction(state);
        }

        private static void EndAction(MatchState state)
        {
            if (state.extraActions > 0 && !state.retracted)
            {
                state.extraActions--;
                StartAction(state);
                AddNotice(state, state.seats[state.activeSeat].name + " has " + (state.extraActions + 1) +
                    " action(s) remaining.");
            }
            else
                FinishFullTurn(state);
        }

        private static void FinishFullTurn(MatchState state)
        {
            state.pending = null;
            state.power = null;
            state.snapshot = null;
            state.reactionPayments.Clear();
            state.controller = -1;
            state.extraActions = 0;
            state.retracted = false;
            if (Win(state))
                return;
            state.activeSeat = NextSeat(state);
            BeginFullTurn(state);
        }

        private static int NextSeat(MatchState state)
        {
            int sequential = 1 - state.activeSeat;
            if (state.extraTurns.Count > 0)
            {
                if (state.resumeSeat < 0)
                    state.resumeSeat = sequential;
                int next = state.extraTurns[0];
                state.extraTurns.RemoveAt(0);
                return next;
            }
            if (state.resumeSeat < 0)
                return sequential;
            int resume = state.resumeSeat;
            state.resumeSeat = -1;
            return resume;
        }

        private static void CompletePower(MatchState state)
        {
            PowerUseState use = state.power;
            state.power = null;
            state.pending = null;
            state.phase = "Action";
            switch (use.window)
            {
                case PowerWindow.Own: AfterActionWindow(state); break;
                case PowerWindow.TurnStart: StartAction(state); break;
                case PowerWindow.AfterAction: EndAction(state); break;
                case PowerWindow.Dome:
                    if (use.cancelled)
                        Decide(state, use.caster, "WeaponDiscard", "AfterAction");
                    else
                        AfterActionWindow(state);
                    break;
                default: throw new RuleException("invalid_state", "The saved power window is unsupported.");
            }
        }

        private static void AddNotice(MatchState state, string message) =>
            state.notice = string.IsNullOrEmpty(state.notice) ? message : state.notice + " " + message;
    }
}
