using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Busara.Online.Client
{
    // Match page: authored turn bar, seat panels and board art; slots sit on the art's wells.
    public sealed partial class OnlineMvpScreen
    {
        private void Board()
        {
            var view = session.View;
            string turn = view.draw ? "Draw — match finished" : view.winner >= 0 ? "Seat " + (view.winner + 1) + " wins" :
                view.awaitingOther ? "Waiting for the other player" :
                view.decision != null ? "Your decision" : view.controller == view.seat ? "You control this turn" :
                view.activeSeat == view.seat ? "Your turn" : "Other player's turn";
            var turnLabel = Label("Turn", content, turn, 26, 24, 6, 900, 38);
            turnLabel.fontStyle = FontStyles.Bold;
            turnLabel.color = view.phase == "Finished" || view.decision != null || view.activeSeat == view.seat
                ? OnlineTheme.Amber : OnlineTheme.Text;
            var meta = Label("Revision", content, view.phase + "  " + Dot + "  revision " + view.version, OnlineTheme.SmallSize,
                1104, 14, 400, 26);
            meta.alignment = TextAlignmentOptions.TopRight;
            meta.color = OnlineTheme.TextMuted;
            Label("Power status", content, PowerStatus(view), 18, 24, 44, 1480, 26).color = OnlineTheme.TextMuted;
            for (int seat = 0; seat < 2; seat++) SeatDetails(view, seat);
            foreach (var slot in view.board) Slot(view, slot);
            var actions = Scroll("Legal actions", content, OnlineTheme.ActionsX, OnlineTheme.SeatY, OnlineTheme.ActionsWidth,
                OnlineTheme.SeatHeight);
            if (paying != null) PaymentPanel(actions);
            else Choices(actions);
        }

        private void SeatDetails(ClientView view, int seat)
        {
            var player = view.players.Find(p => p.seat == seat);
            if (player == null) return;
            float x = OnlineTheme.SeatX(seat) + 16;
            var owner = Label("Board owner " + seat, content, "Seat " + (seat + 1) + "  " + Dot + "  " + player.name +
                (seat == view.seat ? "  (you)" : ""), 22, x, OnlineTheme.SeatY + 8, 456, 34);
            owner.fontStyle = FontStyles.Bold;
            if (seat == view.seat) owner.color = OnlineTheme.Amber;
            string kingdom = string.IsNullOrEmpty(player.kingdom) ? "Kingdom hidden" : player.kingdom;
            string virtues = !player.virtuesVisible ? "Virtues hidden" :
                player.virtues.Count == 0 ? "No virtues" : string.Join(", ", player.virtues.GroupBy(t => t.type)
                    .OrderBy(group => group.Key).Select(group => group.Count() + " " + group.Key));
            Label("Player details " + seat, content, kingdom + (player.revealed ? " " + Dot + " revealed" : "") +
                (string.IsNullOrEmpty(player.powerName) ? "" : " " + Dot + " " + player.powerName + " (" + player.powerCost + ")") +
                "\n" + virtues + (string.IsNullOrEmpty(player.goal) ? "" : "\nGoal: " + player.goal),
                18, x, OnlineTheme.DetailsY, 456, 112);
        }

        private void Slot(ClientView view, SlotState slot)
        {
            Vector2 centre = OnlineTheme.SlotCentre(slot.id);
            float half = OnlineTheme.SlotSize / 2;
            bool occupied = !string.IsNullOrEmpty(slot.pieceId);
            int selectedIndex = resourceSelection.IndexOf(slot.id);
            string marker = selectedIndex < 0 ? "" : " [" + (selectedIndex + 1) + "]";
            var button = Button("slot-" + slot.id, slot.id + marker + "\n" + (occupied ? slot.type.ToString() : "Empty"),
                content, centre.x - half, centre.y - half, OnlineTheme.SlotSize, OnlineTheme.SlotSize,
                () => ClickSlot(slot.id), session.CanAct && paying == null && view.phase != "Finished" &&
                    (!resourceSelection.Active || occupied));
            button.image.sprite = circleSprite;
            button.image.type = Image.Type.Simple;
            bool selected = source == slot.id || selectedIndex >= 0;
            button.image.color = selected ? OnlineTheme.Hex(0xFFB21A, .28f) : OnlineTheme.Hex(0xFFB21A, 0f);
            var label = button.GetComponentInChildren<TMP_Text>(true);
            label.text = slot.id.ToString();
            label.fontSize = OnlineTheme.SmallSize - 2;
            label.color = selected ? OnlineTheme.Text : OnlineTheme.TextMuted;
            label.alignment = occupied ? TextAlignmentOptions.Bottom : TextAlignmentOptions.Center;
            bool hasIcon = occupied && (int)slot.type >= 0 && (int)slot.type < resourceIcons.Length && resourceIcons[(int)slot.type] != null;
            if (hasIcon)
            {
                var icon = Rect("Authored resource", button.transform, 17, 10, 50, 50).gameObject.AddComponent<Image>();
                icon.sprite = resourceIcons[(int)slot.type];
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }
            if (selectedIndex >= 0) OrderBadge(button.transform, selectedIndex + 1);
        }

        // Selection order sits in an amber badge so the resource icon stays readable.
        private void OrderBadge(Transform slot, int order)
        {
            var badge = Surface("Order badge", slot, 58, -4, 30, 30, OnlineTheme.Amber);
            badge.transform.SetAsLastSibling();
            var text = Label("Order", badge.transform, order.ToString(), OnlineTheme.SmallSize - 2, 0, 0, 30, 30);
            text.color = OnlineTheme.Ink;
            text.alignment = TextAlignmentOptions.Center;
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
