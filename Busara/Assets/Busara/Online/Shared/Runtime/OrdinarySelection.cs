using System.Collections.Generic;
using System.Linq;

namespace Busara.Online
{
    public static class OrdinarySelection
    {
        public static string Error(IList<SlotState> board, int seat, IList<int> slots, bool weapon)
        {
            if (board == null || seat < 0 || seat > 1 || slots == null ||
                (weapon ? slots.Count != 3 : slots.Count < 2 || slots.Count > 32))
                return weapon ? "Select exactly three connected resources." : "Select an ordered chain of 2 to 32 resources.";
            if (slots.Distinct().Count() != slots.Count)
                return "Select each resource only once.";
            var selected = new List<SlotState>();
            foreach (int id in slots)
            {
                SlotState slot = board.FirstOrDefault(item => item != null && item.id == id);
                if (id < 0 || id >= 32 || slot == null || slot.pieceId == null ||
                    (int)slot.type < 0 || (int)slot.type > 3)
                    return "Choose occupied participating board spaces.";
                selected.Add(slot);
            }
            if (selected[0].seat != seat)
                return "Start with a resource on your board.";
            if (weapon)
            {
                if (selected.Any(slot => slot.type != selected[0].type))
                    return "A weapon requires three resources of the same type.";
                return Connected(selected[0].id, selected[1].id, selected[2].id)
                    ? null : "All three weapon resources must be connected.";
            }
            for (int index = 1; index < selected.Count; index++)
                if (!SharedRules.Adjacent(selected[index - 1].id, selected[index].id) ||
                    selected[index - 1].type == selected[index].type)
                    return "Each next resource must be adjacent and of a different type.";
            return null;
        }

        public static bool HasWeapon(IList<SlotState> board, int seat)
        {
            var occupied = board.Where(slot => slot.pieceId != null).ToList();
            foreach (SlotState first in occupied.Where(slot => slot.seat == seat))
            {
                var matching = occupied.Where(slot => slot.id != first.id && slot.type == first.type).ToList();
                for (int second = 0; second < matching.Count; second++)
                    for (int third = second + 1; third < matching.Count; third++)
                        if (Connected(first.id, matching[second].id, matching[third].id))
                            return true;
            }
            return false;
        }

        private static bool Connected(int first, int second, int third) =>
            (SharedRules.Adjacent(first, second) ? 1 : 0) + (SharedRules.Adjacent(first, third) ? 1 : 0) +
            (SharedRules.Adjacent(second, third) ? 1 : 0) >= 2;
    }
}
