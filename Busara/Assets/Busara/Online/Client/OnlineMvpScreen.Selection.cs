using System.Linq;
using UnityEngine;

namespace Busara.Online.Client
{
    public sealed partial class OnlineMvpScreen
    {
        private void ResourceSelectionPanel(RectTransform actions)
        {
            float y = 0;
            bool weapon = resourceSelection.Kind == "weapon";
            string instructions = weapon
                ? "Select three connected resources of the same type, starting on your board. " +
                    "They will be consumed; the opponent chooses one remaining resource to discard."
                : "Select a chain in order, starting on your board. Each next resource must be adjacent " +
                    "and a different type. All selected resources are consumed.";
            ListLabel(actions, instructions, ref y, 150);
            var slots = resourceSelection.Slots;
            ListLabel(actions, slots.Length == 0 ? "No resources selected." :
                "Order: " + string.Join(" -> ", slots.Select(slot => "#" + slot)), ref y, 90);
            string error = resourceSelection.Error(session.View);
            ListLabel(actions, error ?? (weapon ? "Valid weapon. Confirm to use it." :
                "Valid chain. Each recipe rewards all board owners encountered so far."), ref y, 100);
            // With more than two seats a weapon names its target: one confirm button per player.
            var markers = session.View.choices.Where(choice => choice.kind == resourceSelection.Kind).ToList();
            if (markers.Count == 0) markers.Add(null);
            foreach (var marker in markers)
            {
                var chosen = marker;
                string target = marker != null && marker.target >= 0
                    ? session.View.players.Find(p => p.seat == marker.target)?.name : null;
                ListButton(actions, target == null ? "confirm-resource-selection" : "confirm-resource-selection-" + marker.target,
                    weapon ? "Use weapon" + (target == null ? "" : " on " + target) : "Forge selected chain", ref y,
                    () => session.Submit(chosen, null, slots: resourceSelection.Slots),
                    session.CanAct && error == null && chosen != null, primary: true);
            }
            ListButton(actions, "clear-resource-selection", "Clear selected resources", ref y,
                () => { resourceSelection.Begin(resourceSelection.Kind); Rebuild(); }, session.CanAct && slots.Length > 0);
            ListButton(actions, "cancel-resource-selection", "Back without submitting", ref y,
                () => { resourceSelection.Clear(); Rebuild(); }, session.CanAct);
            ListLabel(actions, "Click a selected resource to undo it and later picks. Confirm to send.", ref y, 100);
            actions.sizeDelta = new Vector2(0, y);
        }
    }
}
