using System.Collections.Generic;
using System.Linq;

namespace Busara.Online
{
    public static partial class DomainRules
    {
        public static bool CanRestorePayment(MatchState state, int seat, string[] ids, int cost = 2)
        {
            if (state.snapshot == null || ids == null || ids.Length != cost || ids.Distinct().Count() != ids.Length ||
                ids.Any(id => !state.seats[seat].virtues.Any(token => token.id == id)))
                return false;
            var ledger = state.reactionPayments.Select(payment => new ReactionPayment
            {
                seat = payment.seat, tokens = payment.tokens.Select(Copy).ToList()
            }).ToList();
            ledger.Add(new ReactionPayment
            {
                seat = seat, tokens = ids.Select(id => Copy(state.seats[seat].virtues.Single(token => token.id == id))).ToList()
            });
            foreach (var group in ledger.GroupBy(payment => payment.seat))
            {
                var selected = group.SelectMany(payment => payment.tokens).ToList();
                if (selected.Select(token => token.id).Distinct().Count() != selected.Count ||
                    selected.Any(token => !state.snapshot.seats[group.Key].virtues.Any(original =>
                        original.id == token.id && original.type == token.type)))
                    return false;
            }
            return true;
        }

        private static List<TokenState> Pay(MatchState state, int seat, string[] ids, int cost)
        {
            Require(ids.Length == cost && ids.Distinct().Count() == cost &&
                ids.All(id => state.seats[seat].virtues.Any(token => token.id == id)),
                "invalid_payment", "Select the exact number of distinct owned virtue tokens.");
            var paid = ids.Select(id => Copy(state.seats[seat].virtues.Single(token => token.id == id))).ToList();
            state.seats[seat].virtues.RemoveAll(token => ids.Contains(token.id));
            return paid;
        }

        private static void RestoreAction(MatchState state)
        {
            var revealed = new HashSet<string>(state.seats.Where(seat => seat.revealed).Select(seat => seat.kingdom));
            var knowledge = state.seats.Select(seat => new List<string>(seat.knownKingdoms)).ToList();
            state.board = state.snapshot.board.Select(Copy).ToList();
            state.deck = state.snapshot.deck.Select(Copy).ToList();
            state.deckRemaining = state.snapshot.deckRemaining;
            state.seats = state.snapshot.seats.Select(Copy).ToList();
            foreach (SeatState seat in state.seats)
            {
                seat.revealed |= revealed.Contains(seat.kingdom);
                seat.knownKingdoms = seat.knownKingdoms.Union(knowledge[seat.seat]).ToList();
            }
            foreach (ReactionPayment payment in state.reactionPayments)
                foreach (TokenState token in payment.tokens)
                    Require(state.seats[payment.seat].virtues.RemoveAll(item => item.id == token.id) == 1,
                        "invalid_state", "The saved reaction payment could not be reapplied.");
        }
    }
}
