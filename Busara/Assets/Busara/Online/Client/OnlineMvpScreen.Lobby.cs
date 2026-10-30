using UnityEngine.UI;

namespace Busara.Online.Client
{
    // Entry and Lobby pages: an authored story panel on the left and an action panel on the right.
    public sealed partial class OnlineMvpScreen
    {
        private const string Dot = "\u00b7";

        private void Scope()
        {
            Label("Scope",
                "Private " + Dot + " 2 human players " + Dot + " " + (session.View == null ? Definitions.CurrentRuleset : session.View.ruleset) + "\n\n" +
                "New matches deal two distinct random kingdoms from all 15, each with its kingdom power: own-turn powers, " +
                "off-turn reactions (Retraction, Time, Manipulation, King's Necklace, Celestial Dome) and hidden-only Abundance. " +
                "Five native setup resources each; a 24-card resource-only deck; draw and place, adjacent moves, forge chains, " +
                "trades, weapons and native kingdom victory. Existing v1/v2 matches keep their original rules.\n\n" +
                "Not available online: disasters, bots, matchmaking or spectators.\n\n" +
                "Disconnects wait indefinitely: no automatic Pass, forfeit or bot. Guests expire after a fixed 30 days; " +
                "there is no account or seat recovery. Invitations claim only the empty seat, expire after 24 hours, and stop working on use or start.");
        }

        private void Entry()
        {
            Label("Entry heading", "A private table. The server keeps the rules.");
            Scope();
            Label("Entry actions", "Play online");
            Label("Offline distinction",
                "Full offline play is unchanged in the desktop game's main menu. This Web entry never starts offline managers.");
            if (session.Guest == null)
            {
                Button("create-guest", "Create a new 30-day guest", session.CreateGuest, session.NeedsGuest && !session.Busy);
                Button("retry-session", "Retry existing guest session", session.RefreshGuest, !session.Busy);
            }
            else if (session.RoomPending)
            {
                Button("retry-room", "Retry saved room request", session.RetryRoom, session.CanRetryRoom);
                if (session.CanDiscardRoom)
                    Button("discard-room", "Discard rejected room request", session.DiscardRejectedRoom, !session.Busy);
            }
            else
            {
                Button("create-room", "Create private room", session.CreateRoom, !session.Busy && session.RoomOperationsReady);
                Button("join-room", session.HasInvite ? "Accept private invitation" : "Open an invitation link to join",
                    session.JoinRoom, session.HasInvite && !session.Busy && session.RoomOperationsReady);
            }
        }

        private void Lobby()
        {
            var view = session.View;
            Label("Lobby heading", "Private room " + Dot + " you are seat " + (view.seat + 1));
            Scope();
            foreach (var player in view.players)
            {
                // Ready seats and your own seat are marked by colour: state, not styling.
                var card = Use<Image>("Seat card " + player.seat);
                if (card != null) card.color = player.ready ? OnlineTheme.Selected : OnlineTheme.Unselected;
                var seat = Label("Seat " + player.seat,
                    "Seat " + (player.seat + 1) + "  " + Dot + "  " + (player.joined ? player.name : "Waiting for invited player") +
                    "\n" + (player.ready ? "Ready" : "Not ready"));
                if (seat != null) seat.color = player.seat == view.seat ? OnlineTheme.Gold : OnlineTheme.Text;
            }
            Input("player-name", "Your name", draftName, value => draftName = value);
            var self = view.players.Find(p => p.seat == view.seat);
            var configure = view.choices.Find(c => c.kind == "configure");
            if (configure != null)
                Button("configure", self != null && self.ready ? "Save name and mark not ready" : "Save name and mark ready",
                    () => session.Submit(configure, null, draftName, self == null || !self.ready), session.CanAct);
            var start = view.choices.Find(c => c.kind == "start");
            Button("start", "Start with random kingdoms", () => session.Submit(start, null), session.CanAct && start != null);
            if (!string.IsNullOrEmpty(session.InviteUrl))
                Button("copy-invite", "Copy single-use invitation", session.CopyInvite);
            Label("Room revision", "Revision " + view.version + "  " + Dot + "  the host starts once both players are ready.");
        }
    }
}
