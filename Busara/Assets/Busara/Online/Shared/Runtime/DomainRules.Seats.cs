using System.Collections.Generic;
using System.Linq;

namespace Busara.Online
{
    // Seats for two to four players. The board is the offline game's 8x8 grid of four 4x4 quadrants, seated
    // clockwise: top-left, top-right, bottom-right, bottom-left. A two-seat match uses the top half, which is the
    // original online board, so older two-player rules see exactly the slots and adjacency they always had.
    public static partial class DomainRules
    {
        public const int MinimumSeats = 2;
        public const int MaximumSeats = 4;

        public static int SeatOfSlot(int id)
        {
            int row = id / 8, column = id % 8;
            return row < 4 ? (column < 4 ? 0 : 1) : (column < 4 ? 3 : 2);
        }

        public static IEnumerable<int> SlotsOf(int seats) => Enumerable.Range(0, 64).Where(id => SeatOfSlot(id) < seats);

        public static int NextSeat(MatchState state, int seat) => (seat + 1) % state.seats.Count;

        // The other seats in turn order, starting after the given seat.
        public static IEnumerable<int> OthersAfter(MatchState state, int seat)
        {
            for (int step = 1; step < state.seats.Count; step++)
                yield return (seat + step) % state.seats.Count;
        }

        // The player a two-seat rule meant by "the opponent". With more seats the command must name one.
        private static int Opponent(MatchState state, int seat, int target)
        {
            if (target < 0 && state.seats.Count == 2)
                return 1 - seat;
            Require(target >= 0 && target < state.seats.Count && target != seat, "invalid_target", "Choose another player.");
            return target;
        }

        // Trades saved before seats were generalized have no partner; in those two-seat matches it is the other seat.
        private static int Partner(MatchState state) =>
            state.pending.partner >= 0 ? state.pending.partner : 1 - state.activeSeat;

        // Offers a reaction window to the first other seat after `after`, in turn order, whose power fits it. A pass
        // continues from the passer, so every eligible player is asked once and the actor never reacts to itself.
        private static bool OfferReactionAfter(MatchState state, string window, int actor, int after)
        {
            for (int seat = NextSeat(state, after); seat != actor; seat = NextSeat(state, seat))
                if (OfferReaction(state, seat, window))
                    return true;
            return false;
        }

        private static int WindowActor(MatchState state, string window) =>
            window == PowerWindow.Necklace ? state.power.caster : state.activeSeat;

        private static void AddBot(MatchState state, int seat, int target)
        {
            Require(state.phase == "Lobby" && seat == 0, "forbidden", "Only the host adds bots.");
            Require(target > 0 && target < state.seats.Count && !state.seats[target].joined,
                "seat_unavailable", "Choose an empty seat for the bot.");
            SeatState bot = state.seats[target];
            bot.joined = true;
            bot.ready = true;
            bot.bot = true;
            bot.name = "Bot " + (target + 1);
        }

        private static void RemoveBot(MatchState state, int seat, int target)
        {
            Require(state.phase == "Lobby" && seat == 0, "forbidden", "Only the host removes bots.");
            Require(target > 0 && target < state.seats.Count && state.seats[target].bot,
                "seat_unavailable", "That seat is not a bot.");
            state.seats[target] = new SeatState { seat = target, name = "Guest" };
        }
    }
}
