using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Busara.Online.Client
{
    // Match page: authored turn bar, seat panels and board art; slots sit on the art's wells and show the theme's pieces.
    public sealed partial class OnlineMvpScreen
    {
        private void Board()
        {
            var view = session.View;
            string turn = view.draw ? "Draw — match finished" : view.winner >= 0 ? "Seat " + (view.winner + 1) + " wins" :
                view.awaitingOther ? "Waiting for the other player" :
                view.decision != null ? "Your decision" : view.controller == view.seat ? "You control this turn" :
                view.activeSeat == view.seat ? "Your turn" : "Other player's turn";
            var turnLabel = Label("Turn", turn);
            if (turnLabel != null)
                turnLabel.color = view.phase == "Finished" || view.decision != null || view.activeSeat == view.seat
                    ? OnlineTheme.Gold : OnlineTheme.Text;
            Label("Revision", view.phase + "  " + Dot + "  revision " + view.version);
            Label("Power status", PowerStatus(view));
            for (int seat = 0; seat < 2; seat++) SeatDetails(view, seat);
            foreach (var slot in view.board) Slot(view, slot);
            Dock(view);
            var actions = ActionList();
            if (actions == null) return;
            if (paying != null) PaymentPanel(actions);
            else Choices(actions);
        }

        private void SeatDetails(ClientView view, int seat)
        {
            var player = view.players.Find(p => p.seat == seat);
            if (player == null) return;
            var owner = Label("Board owner " + seat, "Seat " + (seat + 1) + "  " + Dot + "  " + player.name +
                (seat == view.seat ? "  (you)" : ""));
            if (owner != null) owner.color = seat == view.seat ? OnlineTheme.Gold : OnlineTheme.Text;
            string kingdom = string.IsNullOrEmpty(player.kingdom) ? "Kingdom hidden" : player.kingdom;
            string virtues = !player.virtuesVisible ? "Virtues hidden" :
                player.virtues.Count == 0 ? "No virtues" : string.Join(", ", player.virtues.GroupBy(t => t.type)
                    .OrderBy(group => group.Key).Select(group => group.Count() + " " + group.Key));
            Label("Player details " + seat, kingdom + (player.revealed ? " " + Dot + " revealed" : "") +
                (string.IsNullOrEmpty(player.powerName) ? "" : " " + Dot + " " + player.powerName + " (" + player.powerCost + ")") +
                "\n" + virtues + (string.IsNullOrEmpty(player.goal) ? "" : "\nGoal: " + player.goal));
        }

        private void Slot(ClientView view, SlotState slot)
        {
            bool occupied = !string.IsNullOrEmpty(slot.pieceId);
            int selectedIndex = resourceSelection.IndexOf(slot.id);
            string marker = selectedIndex < 0 ? "" : " [" + (selectedIndex + 1) + "]";
            var button = Button("slot-" + slot.id, slot.id + marker + "\n" + (occupied ? slot.type.ToString() : "Empty"),
                () => ClickSlot(slot.id), session.CanAct && paying == null && view.phase != "Finished" &&
                    (!resourceSelection.Active || occupied));
            if (button == null) return;
            // The authored slot shows selection, its resource and its place in a chosen chain.
            bool selected = source == slot.id || selectedIndex >= 0;
            button.image.color = selected ? OnlineTheme.SlotChosen : OnlineTheme.SlotIdle;
            var label = button.transform.Find("Label").GetComponent<TMP_Text>();
            label.text = slot.id.ToString();
            label.color = selected ? OnlineTheme.Text : OnlineTheme.TextMuted;
            label.alignment = occupied ? TextAlignmentOptions.Bottom : TextAlignmentOptions.Center;
            var icon = button.transform.Find("Authored resource").GetComponent<Image>();
            bool hasIcon = occupied && (int)slot.type >= 0 && (int)slot.type < resourceIcons.Length && resourceIcons[(int)slot.type] != null;
            icon.gameObject.SetActive(hasIcon);
            if (hasIcon) icon.sprite = resourceIcons[(int)slot.type];
            button.transform.Find("Legal").gameObject.SetActive(LegalTarget(slot.id));
            var badge = button.transform.Find("Order badge");
            badge.gameObject.SetActive(selectedIndex >= 0);
            if (selectedIndex >= 0) badge.Find("Order").GetComponent<TMP_Text>().text = (selectedIndex + 1).ToString();
        }

        private static string PowerStatus(ClientView view)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (view.controller >= 0) parts.Add("Seat " + (view.controller + 1) + " controls seat " + (view.activeSeat + 1) + "'s turn");
            if (view.extraActions > 0) parts.Add(view.extraActions + " extra action" + (view.extraActions == 1 ? "" : "s"));
            if (view.extraTurns.Count > 0) parts.Add("Extra turns queued: " + string.Join(", ", view.extraTurns.Select(s => "seat " + (s + 1))));
            if (!string.IsNullOrEmpty(view.notice)) parts.Add(view.notice);
            return parts.Count == 0 ? "No active power effects." : string.Join("  " + Dot + "  ", parts);
        }
    }
}
