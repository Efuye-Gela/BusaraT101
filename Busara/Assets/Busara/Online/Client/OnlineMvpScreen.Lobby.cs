using TMPro;
using UnityEngine;

namespace Busara.Online.Client
{
    // Entry and Lobby pages: an authored story panel on the left and an action panel on the right.
    public sealed partial class OnlineMvpScreen
    {
        private const string Dot = "\u00b7";
        private const float Left = 36f;
        private const float Right = OnlineTheme.SideWidth + OnlineTheme.PanelGap + 36f;
        private const float RightWidth = 526f;

        private void Heading(string name, string value, float x, float y, float width)
        {
            var heading = Label(name, content, value, OnlineTheme.HeadingSize, x, y, width, 76);
            heading.fontStyle = FontStyles.Bold;
            heading.textWrappingMode = TextWrappingModes.Normal;
        }

        private void Scope(float y)
        {
            var scope = Label("Scope", content,
                "Private " + Dot + " 2 human players " + Dot + " " + (session.View == null ? Definitions.CurrentRuleset : session.View.ruleset) + "\n\n" +
                "New matches deal two distinct random kingdoms from all 15, each with its kingdom power: own-turn powers, " +
                "off-turn reactions (Retraction, Time, Manipulation, King's Necklace, Celestial Dome) and hidden-only Abundance. " +
                "Five native setup resources each; a 24-card resource-only deck; draw and place, adjacent moves, forge chains, " +
                "trades, weapons and native kingdom victory. Existing v1/v2 matches keep their original rules.\n\n" +
                "Not available online: disasters, bots, matchmaking or spectators.\n\n" +
                "Disconnects wait indefinitely: no automatic Pass, forfeit or bot. Guests expire after a fixed 30 days; " +
                "there is no account or seat recovery. Invitations claim only the empty seat, expire after 24 hours, and stop working on use or start.",
                OnlineTheme.BodySize, Left, y, OnlineTheme.SideWidth - 72, OnlineTheme.PanelHeight - y - 24);
            scope.color = OnlineTheme.TextMuted;
            scope.textWrappingMode = TextWrappingModes.Normal;
        }

        private void Entry()
        {
            Heading("Entry heading", "A private table. The server keeps the rules.", Left, 32, OnlineTheme.SideWidth - 72);
            Scope(124);
            Heading("Entry actions", "Play online", Right, 32, RightWidth);
            var note = Label("Offline distinction", content,
                "Full offline play is unchanged in the desktop game's main menu. This Web entry never starts offline managers.",
                OnlineTheme.BodySize, Right, 112, RightWidth, 110);
            note.color = OnlineTheme.TextMuted;
            note.textWrappingMode = TextWrappingModes.Normal;
            if (session.Guest == null)
            {
                Primary(Button("create-guest", "Create a new 30-day guest", content, Right, 300, RightWidth, 60,
                    session.CreateGuest, session.NeedsGuest && !session.Busy));
                Button("retry-session", "Retry existing guest session", content, Right, 376, RightWidth, 60,
                    session.RefreshGuest, !session.Busy);
            }
            else if (session.RoomPending)
            {
                Primary(Button("retry-room", "Retry saved room request", content, Right, 300, RightWidth, 60,
                    session.RetryRoom, session.CanRetryRoom));
                if (session.CanDiscardRoom)
                    Button("discard-room", "Discard rejected room request", content, Right, 376, RightWidth, 60,
                        session.DiscardRejectedRoom, !session.Busy);
            }
            else
            {
                Primary(Button("create-room", "Create private room", content, Right, 300, RightWidth, 60, session.CreateRoom,
                    !session.Busy && session.RoomOperationsReady));
                Button("join-room", session.HasInvite ? "Accept private invitation" : "Open an invitation link to join",
                    content, Right, 376, RightWidth, 60, session.JoinRoom,
                    session.HasInvite && !session.Busy && session.RoomOperationsReady);
            }
        }

        private void Lobby()
        {
            var view = session.View;
            Heading("Lobby heading", "Private room " + Dot + " you are seat " + (view.seat + 1), Left, 32, OnlineTheme.SideWidth - 72);
            Scope(124);
            float y = 32;
            foreach (var player in view.players)
            {
                Surface("Seat card " + player.seat, content, Right, y, RightWidth, 84,
                    player.ready ? OnlineTheme.Selected : OnlineTheme.SurfaceRaised);
                var seat = Label("Seat " + player.seat, content,
                    "Seat " + (player.seat + 1) + "  " + Dot + "  " + (player.joined ? player.name : "Waiting for invited player") +
                    "\n" + (player.ready ? "Ready" : "Not ready"), 22, Right + 20, y + 10, RightWidth - 40, 66);
                seat.color = player.seat == view.seat ? OnlineTheme.Amber : OnlineTheme.Text;
                y += 100;
            }
            Input("player-name", "Your name", content, draftName, Right, 252, RightWidth, 58, value => draftName = value);
            var self = view.players.Find(p => p.seat == view.seat);
            var configure = view.choices.Find(c => c.kind == "configure");
            if (configure != null)
                Button("configure", self != null && self.ready ? "Save name and mark not ready" : "Save name and mark ready",
                    content, Right, 326, RightWidth, 60,
                    () => session.Submit(configure, null, draftName, self == null || !self.ready), session.CanAct);
            var start = view.choices.Find(c => c.kind == "start");
            Primary(Button("start", "Start with random kingdoms", content, Right, 402, RightWidth, 60,
                () => session.Submit(start, null), session.CanAct && start != null));
            if (!string.IsNullOrEmpty(session.InviteUrl))
                Button("copy-invite", "Copy single-use invitation", content, Right, 478, RightWidth, 60, session.CopyInvite);
            var revision = Label("Room revision", content, "Revision " + view.version + "  " + Dot + "  the host starts once both players are ready.",
                OnlineTheme.SmallSize, Right, 560, RightWidth, 60);
            revision.color = OnlineTheme.TextMuted;
            revision.textWrappingMode = TextWrappingModes.Normal;
        }
    }
}
