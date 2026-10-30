using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Busara.Online.Client
{
    public sealed partial class OnlineMvpScreen
    {
        private void ClickSlot(int slot)
        {
            if (!session.CanAct) return;
            if (resourceSelection.Active)
            {
                resourceSelection.Toggle(slot);
                Rebuild();
                return;
            }
            var candidates = FilterChoices().ToList();
            var destinations = candidates.Where(c => c.to == slot && (c.from < 0 || c.from == source)).ToList();
            if (destinations.Count == 1)
            {
                Choose(destinations[0]);
                return;
            }
            source = source == slot ? -1 : slot;
            Rebuild();
        }

        private IEnumerable<LegalChoice> FilterChoices()
        {
            return session.View.choices.Where(c => (mode == null || c.kind == mode) &&
                (resourceFilter < 0 || c.resourceType == resourceFilter) &&
                (source < 0 || c.from == source || (c.from < 0 && c.to == source)));
        }

        private void Choices(RectTransform actions)
        {
            var view = session.View;
            float y = 0;
            if (view.phase == "Finished")
            {
                ListLabel(actions, view.draw ? "The match ended in a stalemate." :
                    "The virtue goal has been met. The match is finished and no more actions can be taken.", ref y, 150);
                actions.sizeDelta = new Vector2(0, y);
                return;
            }
            if (resourceSelection.Active)
            {
                ResourceSelectionPanel(actions);
                return;
            }
            string prompt = !string.IsNullOrEmpty(view.decision?.prompt) ? view.decision.prompt :
                view.awaitingOther ? "The other player's required action is pending. Nothing will be passed automatically." :
                "Choose an action below. Board clicks filter a source or choose a unique legal destination.";
            ListLabel(actions, prompt, ref y, 120);
            if (mode != null || source >= 0)
                ListButton(actions, "clear-selection", "Show all legal actions", ref y, () =>
                { mode = null; source = -1; resourceFilter = -1; Rebuild(); });
            if (mode == null)
            {
                foreach (var marker in view.choices.Where(c => c.kind == "forgeChain" || c.kind == "weapon"))
                    ListButton(actions, "choice-" + marker.kind + "--1--1--1", marker.label, ref y,
                        () => Choose(marker), session.CanAct);
                foreach (var group in view.choices.Where(c => c.to >= 0 || c.from >= 0)
                    .GroupBy(c => c.kind + ":" + (TypedKind(c.kind) ? c.resourceType : -1)))
                {
                    var first = group.First();
                    int type = TypedKind(first.kind) ? first.resourceType : -1;
                    string label = ActionName(first.kind) + (type < 0 ? "" : " " + ((ResourceType)type));
                    ListButton(actions, "mode-" + group.Key, label + " — select on board", ref y, () =>
                    { mode = first.kind; resourceFilter = type; source = -1; Rebuild(); });
                }
            }
            foreach (var choice in FilterChoices().Where(c => c.kind != "forgeChain" && c.kind != "weapon"))
            {
                var selected = choice;
                string label = string.IsNullOrEmpty(choice.label) ? choice.kind : choice.label;
                if (choice.from >= 0) label += " · " + choice.from;
                if (choice.to >= 0) label += " → " + choice.to;
                if (choice.paymentCost > 0) label += " · pay " + choice.paymentCost;
                string id = "choice-" + choice.kind + "-" + choice.from + "-" + choice.to + "-" + choice.resourceType;
                ListButton(actions, id, label, ref y, () => Choose(selected), session.CanAct);
            }
            if (view.choices.Count == 0) ListLabel(actions, "No legal actions for your seat right now.", ref y, 80);
            actions.sizeDelta = new Vector2(0, y);
        }

        private void Choose(LegalChoice choice)
        {
            if (!session.CanAct) return;
            if (choice.kind == "forgeChain" || choice.kind == "weapon")
            {
                resourceSelection.Begin(choice.kind);
                source = -1;
                Rebuild();
                return;
            }
            if (choice.paymentCost > 0)
            {
                paying = choice;
                payment.Clear();
                exchange.Clear();
                Rebuild();
            }
            else session.Submit(choice, null);
        }

        private static bool TypedKind(string kind) =>
            kind == "setupPlace" || kind == "abundancePlace" || kind == "addResource";

        private static string ActionName(string kind)
        {
            switch (kind)
            {
                case "setupPlace": return "Set up";
                case "setupMove": return "Rearrange setup";
                case "move": return "Move resource";
                case "forge": return "Forge two resources";
                case "trade": return "Offer a trade";
                case "tradeComplete": return "Choose the traded resource";
                case "weaponDiscard": return "Discard one of your resources";
                case "place": return "Place drawn resource";
                case "abundancePlace": return "Abundance: place";
                case "addResource": return "Add resource";
                case "removeResource": return "Remove resource";
                case "witchMove": return "Rearrange";
                case "steal": return "Steal resource";
                default: return kind;
            }
        }
    }
}
