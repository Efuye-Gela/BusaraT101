using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Busara.Online.Client
{
    // Payment and Transform exchange selection. The server re-validates ownership, cost and stock.
    public sealed partial class OnlineMvpScreen
    {
        private readonly HashSet<string> exchange = new HashSet<string>();

        private bool Exchanging => paying != null && paying.kind == "power" && paying.virtueType >= 0 &&
            session.View.players.Any(p => p.seat == ActingSeat && p.powerName == "Transform");

        // Under Manipulation the controller pays with the controlled (active) player's virtues.
        private int ActingSeat => session.View.controller == session.View.seat && session.View.decision == null
            ? session.View.activeSeat : session.View.seat;

        private void PaymentPanel(RectTransform actions)
        {
            var view = session.View;
            float y = 0;
            ListLabel(actions, (view.decision == null ? paying.label : view.decision.prompt) +
                "\nSelect exactly " + paying.paymentCost + " owned virtues. Each choice below is one distinct token.", ref y, 130);
            var acting = view.players.Find(p => p.seat == ActingSeat);
            var options = view.decision != null && view.decision.owner == view.seat
                ? view.decision.paymentOptions : acting.virtues;
            for (int i = 0; i < options.Count; i++)
            {
                var token = options[i];
                string label = (payment.Contains(token.id) ? "Paying · " : exchange.Contains(token.id) ? "Exchanging · " :
                    "Select · ") + token.type + " #" + (i + 1);
                var option = ListButton(actions, "payment-" + i, label, ref y, () => Toggle(token.id), session.CanAct);
                if (payment.Contains(token.id) || exchange.Contains(token.id)) option.image.color = OnlineTheme.Selected;
            }
            if (Exchanging)
                ListLabel(actions, "After paying, select any further virtues to exchange for " +
                    (VirtueType)paying.virtueType + " (" + exchange.Count + " selected).", ref y, 90);
            Primary(ListButton(actions, "confirm-payment", "Confirm " + paying.kind + " · pay " + payment.Count + "/" + paying.paymentCost,
                ref y, () => session.Submit(paying, payment.ToList(), exchangeIds: exchange.ToList()),
                session.CanAct && payment.Count == paying.paymentCost));
            ListButton(actions, "cancel-payment", "Back — do not submit", ref y, () =>
            { paying = null; payment.Clear(); exchange.Clear(); Rebuild(); }, session.CanAct);
            var pass = view.choices.Find(c => c.kind == "pass");
            if (pass != null)
                ListButton(actions, "pass", "Pass without payment", ref y, () => session.Submit(pass, null), session.CanAct);
            actions.sizeDelta = new Vector2(0, y);
        }

        private void Toggle(string id)
        {
            if (payment.Remove(id)) { if (Exchanging) exchange.Add(id); }
            else if (exchange.Remove(id)) { }
            else if (payment.Count < paying.paymentCost) payment.Add(id);
            else if (Exchanging) exchange.Add(id);
            Rebuild();
        }
    }
}
