using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class PowerEffects
{
    private static PowerManager Manager => PowerManager.Instance;

    private static PowerDecisionContext Context(PowerDecisionKind kind, Player owner, PowerUse use, int count = 0)
    {
        return new PowerDecisionContext(kind, owner, use)
        {
            Target = use?.Target, Trigger = use?.Trigger, IsAttack = use != null && use.IsAttack, Count = count
        };
    }

    public static void AddResources(Player player, int count, Action complete, List<ResourceType> copies = null,
        PowerUse use = null)
    {
        if (count == 0)
        {
            complete();
            return;
        }
        List<ResourceType> types = copies == null
            ? Enum.GetValues(typeof(ResourceType)).Cast<ResourceType>().ToList()
            : copies.Distinct().ToList();
        types = types.Where(type => PowerRules.ResourceStock(type) > 0).ToList();
        if (PowerRules.EmptySlots(player).Count == 0 || types.Count == 0)
        {
            Manager.Notice($"{player.Name}: no more resources can be added. The rest of the effect is skipped.", complete,
                Context(PowerDecisionKind.Notice, player, use, count));
            return;
        }

        var choices = types.Select(type => new PowerChoice(type.ToString(), () =>
        {
            ChooseEmptySlot(player, slot =>
            {
                GameObject piece = BoardManager.Instance.SpawnByResourceType(type, slot.gameObject);
                Board.PlaceResource(piece.GetComponent<Resource>(), slot);
                if (copies != null)
                    copies.Remove(type);
                AddResources(player, count - 1, complete, copies, use);
            }, use, type, count: count);
        }, true, new PowerOption(PowerOptionKind.ResourceType) { ResourceType = type })).ToList();
        Manager.Choose($"{player.Name}: choose a resource to add ({count} remaining).", choices,
            Context(PowerDecisionKind.AddResource, player, use, count));
    }

    public static void ChooseEmptySlot(Player player, Action<Slot> selected, PowerUse use = null,
        ResourceType? resourceType = null, Resource resource = null, int count = 0)
    {
        var context = Context(PowerDecisionKind.EmptySlot, player, use, count);
        context.ResourceType = resourceType;
        context.Resource = resource;
        var choices = PowerRules.EmptySlots(player).Select(slot => new PowerChoice(
            $"Space {slot.Index + 1}", () => selected(slot), true,
            new PowerOption(PowerOptionKind.Slot) { Slot = slot })).ToList();
        Manager.Choose($"{player.Name}: choose an empty space on your board.", choices, context);
    }

    public static void Steal(PowerUse use)
    {
        var choices = PowerRules.Resources(use.Target).Select(resource => new PowerChoice(
            $"{resource.resourceType} at space {resource.slot.Index + 1}", () =>
            {
                ChooseEmptySlot(use.Caster, slot =>
                {
                    Board.MoveResource(resource, slot);
                    use.Caster.virtuesHidden = true;
                    use.Complete();
                }, use, resource.resourceType, resource);
            }, true, new PowerOption(PowerOptionKind.Resource) { Resource = resource })).ToList();
        Manager.Choose($"{use.Caster.Name}: choose a resource to steal from {use.Target.Name}.", choices,
            Context(PowerDecisionKind.StealResource, use.Caster, use));
    }

    public static void Rearrange(PowerUse use)
    {
        var choices = PowerRules.Resources(use.Caster).Select(resource => new PowerChoice(
            $"{resource.resourceType} at space {resource.slot.Index + 1}", () =>
            {
                var destinations = use.Caster.Board.Slots.Where(slot => slot != resource.slot)
                    .Select(slot => new PowerChoice($"Space {slot.Index + 1}" +
                        (slot.resource != null ? $" (swap with {slot.resource.resourceType})" : ""), () =>
                        {
                            SwapOrMove(resource, slot);
                            Rearrange(use);
                        }, true, new PowerOption(PowerOptionKind.Slot) { Slot = slot })).ToList();
                destinations.Add(new PowerChoice("Back", () => Rearrange(use), true, new PowerOption(PowerOptionKind.Back)));
                var context = Context(PowerDecisionKind.RearrangeDestination, use.Caster, use);
                context.Resource = resource;
                context.ResourceType = resource.resourceType;
                Manager.Choose("Choose a destination within your kingdom.", destinations, context);
            }, true, new PowerOption(PowerOptionKind.Resource) { Resource = resource })).ToList();
        choices.Add(new PowerChoice("Finish rearranging", use.Complete, true, new PowerOption(PowerOptionKind.Finish)));
        Manager.Choose($"{use.Caster.Name}: rearrange any resources, then finish.", choices,
            Context(PowerDecisionKind.RearrangeResource, use.Caster, use));
    }

    public static void SwapOrMove(Resource resource, Slot destination)
    {
        Slot source = resource.slot;
        Resource other = destination.resource;
        if (other == null)
        {
            Board.MoveResource(resource, destination);
            return;
        }
        Slot.EmptySlotByResource(resource);
        Slot.EmptySlotByResource(other);
        Board.PlaceResource(resource, destination);
        Board.PlaceResource(other, source);
    }

    public static void Rain(PowerUse use, int playerIndex = 0)
    {
        if (playerIndex == PlayerManager.Instance.Players.Count)
        {
            use.Complete();
            return;
        }
        Player player = PlayerManager.Instance.Players[playerIndex];
        Action next = () => Rain(use, playerIndex + 1);
        if (use.RemoveResources)
            RemoveResources(player, use.ResourceCount, next, use);
        else
            AddResources(player, use.ResourceCount, next, use: use);
    }

    public static void RemoveResources(Player player, int count, Action complete, PowerUse use = null)
    {
        if (count == 0)
        {
            complete();
            return;
        }
        List<Resource> resources = PowerRules.Resources(player);
        if (resources.Count == 0)
        {
            Manager.Notice($"{player.Name}: no resources remain to remove.", complete,
                Context(PowerDecisionKind.Notice, player, use, count));
            return;
        }
        var choices = resources.Select(resource => new PowerChoice(
            $"{resource.resourceType} at space {resource.slot.Index + 1}", () =>
            {
                resource.slot.EmptySlot();
                resource.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(resource.gameObject);
                RemoveResources(player, count - 1, complete, use);
            }, true, new PowerOption(PowerOptionKind.Resource) { Resource = resource, Count = count, Remove = true })).ToList();
        Manager.Choose($"{player.Name}: choose a resource to remove ({count} remaining).", choices,
            Context(PowerDecisionKind.RemoveResource, player, use, count));
    }
}
