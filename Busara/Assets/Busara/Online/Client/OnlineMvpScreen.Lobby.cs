using UnityEngine.UI;

namespace Busara.Online.Client
{
    // Entry and Lobby pages: an authored story panel on the left and an action panel on the right.
    public sealed partial class OnlineMvpScreen
    {
        private const string Dot = "\u00b7";

        private int roomSeats = DomainRules.MinimumSeats;
        private string draftCode = "";

        private void Scope()
        {
            Label("Scope",
                "Private " + Dot + " 2 to 4 players or bots " + Dot + " " + (session.View == null ? Definitions.CurrentRuleset : session.View.ruleset) + "\n\n" +
                "Each player gets a random kingdom from all 15, with its kingdom power: own-turn powers, off-turn " +
                "reactions (Retraction, Time, Manipulation, King's Necklace, Celestial Dome) and hidden-only Abundance. " +
                "Draw and place, adjacent moves, forge chains, trades, weapons and native kingdom victory.\n\n" +
                "The host can fill empty seats with bots, which play straight away on the server. Not available online: " +
                "disasters, matchmaking or spectators.\n\n" +
                "Disconnects wait indefinitely: no automatic Pass or forfeit. Guests expire after a fixed 30 days. " +
                "The room code and invitation fill empty seats until the room is full or starts, and expire after 24 hours.");
        }

        // Play with friends: your name, a new room for two to four players, or a room code to join.
        private void Entry()
        {
            Label("Entry heading", "A private table. The server keeps the rules.");
            Scope();
            Label("Entry actions", "Play with friends");
            Label("Offline distinction",
                "Full offline play, with bots, is in the desktop game's main menu. This Web entry never starts offline managers.");
            if (session.Guest == null && !session.NeedsGuest)
            {
                Button("retry-session", "Retry existing guest session", session.RefreshGuest, !session.Busy);
                return;
            }
            if (session.RoomPending)
            {
                Button("retry-room", "Retry saved room request", session.RetryRoom, session.CanRetryRoom);
                if (session.CanDiscardRoom)
                    Button("discard-room", "Discard rejected room request", session.DiscardRejectedRoom, !session.Busy);
                return;
            }
            Label("Name caption", "YOUR NAME");
            Input("entry-name", "Your name", draftName, value => draftName = value, editable: true);
            Label("New room caption", "NEW ROOM");
            Button("seat-count", "PLAYERS: " + roomSeats, () =>
            {
                roomSeats = roomSeats >= DomainRules.MaximumSeats ? DomainRules.MinimumSeats : roomSeats + 1;
                Rebuild();
            }, !session.Busy);
            Button("create-room", "CREATE ROOM", () => session.CreateRoom(roomSeats), session.CanStartRoom);
            Label("Code caption", "HAVE A CODE?");
            Input("room-code", "Room code", draftCode, value => draftCode = value, editable: true);
            Button("join-code", "JOIN", () => session.JoinWithCode(draftCode), session.CanStartRoom);
            if (session.HasInvite)
                Button("join-room", "Accept private invitation", session.JoinRoom, session.CanStartRoom);
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
                string who = player.bot ? player.name + " (bot)" : player.joined ? player.name : "Open seat";
                var seat = Label("Seat " + player.seat, "Seat " + (player.seat + 1) + "  " + Dot + "  " + who + "\n" +
                    (player.joined ? player.ready ? "Ready" : "Not ready" : "Share the code or add a bot"));
                if (seat != null) seat.color = player.seat == view.seat ? OnlineTheme.Gold : OnlineTheme.Text;
                // Only the host is offered bots; the server checks it too.
                var add = view.choices.Find(c => c.kind == "addBot" && c.target == player.seat);
                var remove = view.choices.Find(c => c.kind == "removeBot" && c.target == player.seat);
                if (add != null)
                    Button("seat-bot-" + player.seat, "Add bot", () => session.Submit(add, null), session.CanAct);
                else if (remove != null)
                    Button("seat-bot-" + player.seat, "Remove bot", () => session.Submit(remove, null), session.CanAct);
            }
            if (!string.IsNullOrEmpty(session.RoomCode))
            {
                Label("Room code caption", "ROOM CODE");
                Label("Room code", session.RoomCode.Substring(0, 4) + " " + session.RoomCode.Substring(4));
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
                Button("copy-invite", "Copy invitation link", session.CopyInvite);
            Label("Room revision", "Revision " + view.version + "  " + Dot + "  the host starts once every seat is filled and ready.");
        }
    }
}
