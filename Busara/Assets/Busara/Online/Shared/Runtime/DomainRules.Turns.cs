using System;
using System.Collections.Generic;
using System.Linq;

namespace Busara.Online
{
    public static partial class DomainRules
    {
        public static int ResourceStock(MatchState state, ResourceType type) =>
            Math.Max(0, 20 - state.board.Count(slot => slot.pieceId != null && slot.type == type));

        public static bool SetupSpace(MatchState state, int seat, int destination, int ignoredSource) =>
            !state.board.Any(slot => slot.seat == seat && slot.pieceId != null && slot.id != ignoredSource &&
                SharedRules.Adjacent(slot.id, destination));

        private static void Start(MatchState state, IGameRandom random)
        {
            var kingdoms = KingdomCatalog.Pool(state.ruleset).ToList();
            Shuffle(kingdoms, random);
            for (int seat = 0; seat < 2; seat++)
            {
                state.seats[seat].kingdom = kingdoms[seat];
                state.seats[seat].setupRemaining = Definitions.Setup(seat).ToList();
            }
            for (int type = 0; type < 4; type++)
                for (int copy = 0; copy < 6; copy++)
                    state.deck.Add(new CardState { id = "resource-" + type + "-" + copy, type = (ResourceType)type });
            Shuffle(state.deck, random);
            state.deckRemaining = state.deck.Count;
            state.phase = "Setup";
            state.activeSeat = 0;
        }

        private static void FinishSetupPlacement(MatchState state)
        {
            if (state.seats[state.activeSeat].setupRemaining.Count != 0)
                return;
            state.activeSeat = 1 - state.activeSeat;
            if (state.seats.All(seat => seat.setupRemaining.Count == 0))
                BeginTurn(state);
        }

        private static void ContinueAbundance(MatchState state)
        {
            if (state.pending.remaining == 0)
                AfterAction(state);
            else if (!state.board.Any(slot => slot.seat == state.activeSeat && slot.pieceId == null) ||
                !Enumerable.Range(0, 4).Any(type => ResourceStock(state, (ResourceType)type) > 0))
                Decide(state, state.activeSeat, "AbundanceNotice", "AfterAction");
        }

        private static void AfterAction(MatchState state)
        {
            if (Definitions.IsFull(state.ruleset))
            {
                AfterActionWindow(state);
                return;
            }
            state.pending = null;
            int reactor = 1 - state.activeSeat;
            if (state.snapshot != null && state.seats[reactor].kingdom == Definitions.Mask &&
                state.seats[reactor].virtues.Count >= 2)
                Decide(state, reactor, "Retraction", "TurnEnd");
            else
                FinishTurn(state);
        }

        private static void FinishTurn(MatchState state)
        {
            state.pending = null;
            state.snapshot = null;
            state.reactionPayments.Clear();
            if (Win(state))
                return;
            state.activeSeat = 1 - state.activeSeat;
            BeginTurn(state);
        }

        private static bool Win(MatchState state)
        {
            foreach (int seat in new[] { state.activeSeat, 1 - state.activeSeat })
                if (SharedRules.MeetsGoals(state.seats[seat].virtues.Select(token => token.type),
                    Definitions.Goals(state.seats[seat].kingdom)))
                {
                    state.phase = "Finished";
                    state.winner = seat;
                    return true;
                }
            return false;
        }

        private static void BeginTurn(MatchState state)
        {
            if (Definitions.IsFull(state.ruleset))
            {
                BeginFullTurn(state);
                return;
            }
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
                state.activeSeat = 1 - state.activeSeat;
            }
            state.consecutiveEmptySkips = 0;
            state.phase = "Action";
            state.snapshot = Capture(state);
        }

        private static void Decide(MatchState state, int owner, string kind, string continuation)
        {
            state.phase = "Decision";
            state.pending = new PendingDecision { id = NewId(), owner = owner, kind = kind, continuation = continuation };
        }
    }
}
