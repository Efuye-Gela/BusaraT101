using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Busara.Online.Client
{
    // The round buttons beside the board, as in the offline side panel: the four setup resources with their counts
    // during setup, then Draw, Forge, Trade and Weapon around the power knob. Each button stands for server choices;
    // the list below keeps prompts, decisions and every other choice.
    public sealed partial class OnlineMvpScreen
    {
        private const string DrawId = "choice-draw--1--1--1";
        private const string WeaponId = "choice-weapon--1--1--1";
        private const string ForgeChainId = "choice-forgeChain--1--1--1";
        private const string TradeMode = "trade:-1";
        private static readonly string[] PowerKinds = { "power", "abundance", "knowledge" };

        // Choices and board modes a round button already offers, so the list doesn't repeat them.
        private static bool DockedMode(string group) =>
            group == TradeMode || group.StartsWith("setupPlace:", System.StringComparison.Ordinal);

        private static bool DockedChoice(LegalChoice choice) =>
            choice.kind == "draw" || choice.kind == "weapon" || choice.kind == "forgeChain" || PowerKinds.Contains(choice.kind);

        private void Dock(ClientView view)
        {
            if (view.phase == "Setup") SetupDock(view);
            else if (view.phase != "Finished") ActionDock(view);
        }

        private void SetupDock(ClientView view)
        {
            Label("Setup heading", "Setup resources");
            Label("Setup hint", "Choose a resource, then an empty space on your board. Tap one of your pieces, " +
                "then a space, to rearrange.");
            var self = view.players.Find(p => p.seat == view.seat);
            for (int type = 0; type < 4; type++)
            {
                int resource = type;
                int left = self == null ? 0 : self.setupRemaining.Count(t => (int)t == resource);
                string group = "setupPlace:" + resource;
                bool legal = view.choices.Any(c => c.kind == "setupPlace" && c.resourceType == resource);
                var button = Button("mode-" + group, (ResourceType)resource + " · " + left,
                    () => ToggleMode("setupPlace", resource), session.CanAct && legal);
                Mark(button, mode == "setupPlace" && resourceFilter == resource);
            }
        }

        private void ActionDock(ClientView view)
        {
            var draw = view.choices.Find(c => c.kind == "draw");
            Mark(Button(DrawId, "Draw", () => Choose(draw), session.CanAct && draw != null), false);
            // Forge works as offline: tap resources in order (two or more, starting on your board), then confirm.
            // A quick pair also forges by tapping your piece and then the piece next to it.
            var chain = view.choices.Find(c => c.kind == "forgeChain");
            Mark(Button(ForgeChainId, "Forge", () =>
            {
                if (resourceSelection.Active && resourceSelection.Kind == "forgeChain") { resourceSelection.Clear(); Rebuild(); }
                else Choose(chain);
            }, session.CanAct && chain != null), resourceSelection.Active && resourceSelection.Kind == "forgeChain");
            Mark(Button("mode-" + TradeMode, "Trade", () => ToggleMode("trade", -1),
                session.CanAct && view.choices.Any(c => c.kind == "trade")), mode == "trade");
            var weapon = view.choices.Find(c => c.kind == "weapon");
            Mark(Button(WeaponId, "Weapon", () =>
            {
                if (resourceSelection.Active && resourceSelection.Kind == "weapon") { resourceSelection.Clear(); Rebuild(); }
                else Choose(weapon);
            }, session.CanAct && weapon != null), resourceSelection.Active && resourceSelection.Kind == "weapon");

            // One power choice is used straight away; several open in the list below.
            var powers = view.choices.Where(c => PowerKinds.Contains(c.kind)).ToList();
            var knob = Button("use-power", "Use power", () =>
            {
                if (powers.Count == 1) Choose(powers[0]);
                else ToggleMode("power", -1);
            }, session.CanAct && powers.Count > 0);
            Mark(knob, mode == "power");
        }

        private void ToggleMode(string kind, int type)
        {
            bool same = mode == kind && resourceFilter == type;
            mode = same ? null : kind;
            resourceFilter = same ? -1 : type;
            source = -1;
            Rebuild();
        }

        private static void Mark(Button button, bool active)
        {
            if (button != null) button.image.color = active ? OnlineTheme.Selected : OnlineTheme.Unselected;
        }

        // The theme's legal-space ring on every slot the current selection can reach.
        private bool LegalTarget(int slot)
        {
            if (resourceSelection.Active || paying != null || (mode == null && source < 0)) return false;
            return FilterChoices().Any(c => c.to == slot || (source < 0 && c.from == slot));
        }
    }
}
