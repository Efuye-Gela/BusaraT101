using System;
using System.Collections.Generic;
using System.Linq;

public sealed class PowerUse
{
    public Player Caster;
    public Power Power;
    public Player Target;
    public PowerUse Trigger;
    public bool IsAttack;
    public Virtue ChosenVirtue;
    public readonly List<Virtue> Payment = new List<Virtue>();
    public readonly List<Virtue> Exchange = new List<Virtue>();
    public int ResourceCount = 1;
    public bool RemoveResources;
    public Action Complete;
}

public static class PowerRules
{
    public static bool CanPay(IList<Virtue> owned, IList<Virtue> selected, int cost)
    {
        if (selected == null || selected.Any(virtue => virtue == null))
            return false;
        return Busara.Online.SharedRules.CanPay(owned, selected, cost);
    }

    public static List<Slot> EmptySlots(Player player)
    {
        return player.Board.Slots.Where(slot => !slot.isOccupied && slot.resource == null).ToList();
    }

    public static List<Resource> Resources(Player player)
    {
        return player.Board.Slots.Where(slot => slot.isOccupied && slot.resource != null)
            .Select(slot => slot.resource).ToList();
    }

    public static int VirtueStock(Virtue virtue)
    {
        return Math.Max(0, 12 - PlayerManager.Instance.Players.Sum(
            player => player.Virtues.Count(item => item.type == virtue.type)));
    }

    public static int ResourceStock(ResourceType type)
    {
        return Math.Max(0, 20 - PlayerManager.Instance.Players.Sum(
            player => Resources(player).Count(resource => resource.resourceType == type)));
    }
}
