using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class BotPowerPlan
{
    public PowerUse Use;
    public float Benefit;
    public float Cost;
    public float Score => Benefit - Cost;
    public string Reason;
}

/// <summary>One-step estimates from the decision owner's information, never from deck order.</summary>
public static class BotPowerPlanner
{
    public static bool Supports(Power power) =>
        power is Abundance || power is BlessingOfPlenty || power is CelestialDome ||
        power is IdentitySurfing || power is Imagination || power is InfiniteKnowledge ||
        power is Invisibility || power is KingsNecklace || power is Magic ||
        power is Manipulation || power is Rain || power is Retraction ||
        power is TimePower || power is TransformPower || power is Witchcraft;

    public static BotCell[] Cells(Player player) => player.Board.Slots.Select(slot =>
        new BotCell(slot.Index, slot.resource == null ? (ResourceType?)null : slot.resource.resourceType,
            BoardManager.GetAdjacentSlots(slot).Where(next => next.board == player.Board).Select(next => next.Index))).ToArray();

    public static BotRecipe[] Recipes(Player player, Player viewer)
    {
        bool visible = player.CanSeeKingdom(viewer) && player.CanSeeVirtues(viewer);
        return ForgeManager.Instance.AllVirtues.Select(virtue => new BotRecipe(virtue.type,
            virtue.componentOne, virtue.componentTwo, visible ? Math.Max(0,
                Goal(player.Kingdom, virtue.type) - player.Virtues.Count(item => item != null && item.type == virtue.type)) : 0,
            PowerRules.VirtueStock(virtue))).ToArray();
    }

    public static int Goal(Kingdom kingdom, VirtueType type) => kingdom.virtuesForWin
        .Where(goal => goal.virtues.type == type).Select(goal => goal.NumberofVirtues).DefaultIfEmpty(0).Max();

    public static float InventoryValue(Kingdom kingdom, IEnumerable<VirtueType> inventory)
    {
        var counts = inventory.GroupBy(type => type).ToDictionary(group => group.Key, group => group.Count());
        float value = counts.Sum(pair => 15 * pair.Value + 85 * Math.Min(pair.Value, Goal(kingdom, pair.Key)));
        if (kingdom.virtuesForWin.Length > 0 && kingdom.virtuesForWin.All(goal =>
            counts.TryGetValue(goal.virtues.type, out int count) && count >= goal.NumberofVirtues))
            value += 10000;
        return value;
    }

    public static float PaymentLoss(Kingdom kingdom, IEnumerable<Virtue> owned, IEnumerable<Virtue> payment)
    {
        var remaining = owned.ToList();
        float before = InventoryValue(kingdom, remaining.Select(virtue => virtue.type));
        foreach (Virtue virtue in payment)
            if (!remaining.Remove(virtue))
                return float.PositiveInfinity;
        return before - InventoryValue(kingdom, remaining.Select(virtue => virtue.type));
    }

    private static List<Virtue> Payment(Player caster, IList<Virtue> selected, int count)
    {
        var payment = new List<Virtue>(selected);
        var available = new List<Virtue>(caster.Virtues);
        foreach (Virtue virtue in payment)
            if (!available.Remove(virtue))
                return null;
        while (payment.Count < count && available.Count > 0)
        {
            Virtue next = available.Distinct().OrderBy(virtue =>
                PaymentLoss(caster.Kingdom, available, new[] { virtue })).ThenBy(virtue => virtue.type).First();
            payment.Add(next);
            available.Remove(next);
        }
        return payment.Count == count ? payment : null;
    }

    public static float BoardValue(Player player, Player viewer, Dictionary<int, ResourceType?> board = null)
    {
        BotCell[] cells = Cells(player);
        board = board ?? cells.ToDictionary(cell => cell.Index, cell => cell.Resource);
        return BoardValue(cells, Recipes(player, viewer), board);
    }

    private static float BoardValue(BotCell[] cells, BotRecipe[] recipes, Dictionary<int, ResourceType?> board) =>
        board.Count(pair => pair.Value.HasValue) * 8 + BotPlanner.Potential(cells, recipes, board);

    public static float PlacementGain(Player player, Player viewer, ResourceType type, Slot slot)
    {
        var board = Cells(player).ToDictionary(cell => cell.Index, cell => cell.Resource);
        float before = BoardValue(player, viewer, board);
        board[slot.Index] = type;
        return BoardValue(player, viewer, board) - before;
    }

    public static float RemovalLoss(Player player, Player viewer, Resource resource)
    {
        var board = Cells(player).ToDictionary(cell => cell.Index, cell => cell.Resource);
        float before = BoardValue(player, viewer, board);
        board[resource.slot.Index] = null;
        return before - BoardValue(player, viewer, board);
    }

    public static float RearrangeGain(Player player, Player viewer, Resource resource, Slot destination)
    {
        var board = Cells(player).ToDictionary(cell => cell.Index, cell => cell.Resource);
        float before = BoardValue(player, viewer, board);
        board[resource.slot.Index] = board[destination.Index];
        board[destination.Index] = resource.resourceType;
        return BoardValue(player, viewer, board) - before;
    }

    private static float AddValue(Player player, Player viewer, int count, IEnumerable<ResourceType> copies = null)
    {
        BotCell[] cells = Cells(player);
        BotRecipe[] recipes = Recipes(player, viewer);
        var board = cells.ToDictionary(cell => cell.Index, cell => cell.Resource);
        float before = BoardValue(cells, recipes, board);
        var types = Enum.GetValues(typeof(ResourceType)).Cast<ResourceType>().ToArray();
        var stock = types.ToDictionary(type => type, PowerRules.ResourceStock);
        var remaining = copies?.ToList();
        for (int i = 0; i < count; i++)
        {
            var options = (from slot in player.Board.Slots where !board[slot.Index].HasValue
                           from type in types where stock[type] > 0 && (remaining == null || remaining.Contains(type))
                           select new { slot, type }).ToList();
            if (options.Count == 0)
                break;
            var best = options.OrderByDescending(option =>
            {
                var next = new Dictionary<int, ResourceType?>(board) { [option.slot.Index] = option.type };
                return BoardValue(cells, recipes, next);
            }).ThenBy(option => option.slot.Index).ThenBy(option => option.type).First();
            board[best.slot.Index] = best.type;
            stock[best.type]--;
            remaining?.Remove(best.type);
        }
        return BoardValue(cells, recipes, board) - before;
    }

    private static float RemoveValue(Player player, Player viewer, int count)
    {
        BotCell[] cells = Cells(player);
        BotRecipe[] recipes = Recipes(player, viewer);
        var board = cells.ToDictionary(cell => cell.Index, cell => cell.Resource);
        float before = BoardValue(cells, recipes, board);
        for (int i = 0; i < count && board.Any(pair => pair.Value.HasValue); i++)
        {
            int index = board.Where(pair => pair.Value.HasValue).OrderByDescending(pair =>
            {
                var next = new Dictionary<int, ResourceType?>(board) { [pair.Key] = null };
                return BoardValue(cells, recipes, next);
            }).ThenBy(pair => pair.Key).First().Key;
            board[index] = null;
        }
        return before - BoardValue(cells, recipes, board);
    }

    private static float RearrangeValue(Player player, Player viewer)
    {
        BotCell[] cells = Cells(player);
        BotRecipe[] recipes = Recipes(player, viewer);
        var board = cells.ToDictionary(cell => cell.Index, cell => cell.Resource);
        float before = BoardValue(cells, recipes, board), value = before;
        while (true)
        {
            Dictionary<int, ResourceType?> best = null;
            float bestValue = value;
            foreach (int source in board.Keys.OrderBy(index => index))
                foreach (int destination in board.Keys.Where(index => index > source).OrderBy(index => index))
                {
                    var next = new Dictionary<int, ResourceType?>(board)
                    {
                        [source] = board[destination], [destination] = board[source]
                    };
                    float score = BoardValue(cells, recipes, next);
                    if (score > bestValue)
                    {
                        best = next;
                        bestValue = score;
                    }
                }
            if (best == null)
                return value - before;
            board = best;
            value = bestValue;
        }
    }

    public static float ActionValue(Player player, Player viewer)
    {
        var candidates = BotPlanner.Evaluate(Cells(player), Recipes(player, viewer), true);
        float score = candidates.Max(candidate => candidate.Score);
        // A hidden kingdom's forge is a public opportunity, not a known goal.
        if (!player.CanSeeKingdom(viewer) || !player.CanSeeVirtues(viewer))
            score = Math.Max(score, candidates.Any(candidate => candidate.Kind == BotActionKind.Forge) ? 60 : 0);
        return Math.Max(0, score);
    }

    public static BotPowerPlan Plan(Player caster, PowerDecisionContext context = null)
    {
        PowerUse prepared = context?.Use;
        Power power = prepared?.Power ?? caster.Kingdom.power;
        Player viewer = context?.Owner ?? caster;
        var use = new PowerUse
        {
            Caster = caster, Power = power, Target = prepared?.Target ?? context?.Target,
            Trigger = prepared?.Trigger ?? context?.Trigger, IsAttack = prepared?.IsAttack ?? context?.IsAttack ?? false,
            ChosenVirtue = prepared?.ChosenVirtue,
            ResourceCount = prepared?.ResourceCount ?? 1, RemoveResources = prepared?.RemoveResources ?? false
        };
        var plan = new BotPowerPlan { Use = use, Reason = power.powerName, Benefit = float.NegativeInfinity };
        if (!caster.CanSeeKingdom(viewer) || !caster.CanSeeVirtues(viewer))
        {
            plan.Reason = "Cannot evaluate this controlled player's private payment/goals.";
            return plan;
        }
        if (!Supports(power))
            throw new InvalidOperationException("No bot power policy for " + power.GetType().Name + ".");
        var payment = Payment(caster, prepared?.Payment ?? new List<Virtue>(), power.virtueCost);
        if (payment == null || (power.onlyWhileHidden && caster.kingdomRevealed))
            return plan;
        use.Payment.AddRange(payment);
        plan.Cost = PaymentLoss(caster.Kingdom, caster.Virtues, payment);
        var owned = new List<Virtue>(caster.Virtues);
        foreach (Virtue virtue in payment)
            owned.Remove(virtue);
        float baseInventory = InventoryValue(caster.Kingdom, owned.Select(virtue => virtue.type));
        IEnumerable<Player> targets = PlayerManager.Instance.Players.Where(player => player != caster);
        if (use.Target != null)
            targets = targets.Where(player => player == use.Target);
        if (power is Abundance)
            plan.Benefit = AddValue(caster, viewer, PlayerManager.Instance.Players.Count);
        else if (power is BlessingOfPlenty)
            plan.Benefit = AddValue(caster, viewer, PowerRules.Resources(caster).Count,
                PowerRules.Resources(caster).Select(resource => resource.resourceType));
        else if (power is Witchcraft)
            plan.Benefit = RearrangeValue(caster, viewer);
        else if (power is Magic)
            plan.Benefit = PowerManager.Instance.MagicActionsActive ? 0 : 2 * (8 + ActionValue(caster, viewer));
        else if (power is TimePower)
            plan.Benefit = 8 + ActionValue(caster, viewer);
        else if (power is Manipulation)
            plan.Benefit = use.Target == null ? 0 : ActionValue(use.Target, viewer);
        else if (power is CelestialDome)
            plan.Benefit = ThreatLoss(caster, viewer, use.IsAttack);
        else if (power is KingsNecklace)
            plan.Benefit = use.Trigger == null ? 0 : -Stake(use.Trigger, viewer);
        else if (power is Retraction)
            plan.Benefit = PowerManager.Instance.Validate(use, out _) ? RetractionValue(viewer) : float.NegativeInfinity;
        else if (power is Rain)
        {
            for (int count = 1; count <= 3; count++)
                foreach (bool remove in new[] { false, true })
                {
                    if (context?.Kind == PowerDecisionKind.Confirm &&
                        (count != prepared.ResourceCount || remove != prepared.RemoveResources))
                        continue;
                    float value = PlayerManager.Instance.Players.Sum(player => (player == caster ? 1 : -.35f) *
                        (remove ? -RemoveValue(player, viewer, count) : AddValue(player, viewer, count)));
                    if (value > plan.Benefit)
                    {
                        plan.Benefit = value;
                        use.ResourceCount = count;
                        use.RemoveResources = remove;
                    }
                }
        }
        else if (power is TransformPower)
        {
            foreach (Virtue receive in ForgeManager.Instance.AllVirtues)
            {
                if (prepared?.ChosenVirtue != null && prepared.ChosenVirtue != receive)
                    continue;
                var exchange = prepared?.Exchange.ToList() ?? new List<Virtue>();
                var remaining = owned.ToList();
                bool valid = exchange.All(virtue => remaining.Remove(virtue));
                if (!valid)
                    continue;
                int need = Math.Max(0, Goal(caster.Kingdom, receive.type) - remaining.Count(v => v.type == receive.type));
                int supply = PowerRules.VirtueStock(receive) + payment.Concat(exchange).Count(v => v.type == receive.type);
                while (exchange.Count < Math.Min(need, supply) && context?.Kind != PowerDecisionKind.Virtue &&
                    context?.Kind != PowerDecisionKind.Confirm)
                {
                    Virtue next = remaining.Where(virtue => virtue.type != receive.type).Distinct()
                        .OrderBy(virtue => PaymentLoss(caster.Kingdom, remaining, new[] { virtue }))
                        .ThenBy(virtue => virtue.type).FirstOrDefault();
                    if (next == null || PaymentLoss(caster.Kingdom, remaining, new[] { next }) >= 100)
                        break;
                    remaining.Remove(next);
                    exchange.Add(next);
                }
                if (PowerRules.VirtueStock(receive) + payment.Concat(exchange).Count(v => v.type == receive.type) < exchange.Count)
                    continue;
                float value = InventoryValue(caster.Kingdom,
                    remaining.Select(v => v.type).Concat(Enumerable.Repeat(receive.type, exchange.Count))) - baseInventory;
                if (value > plan.Benefit)
                {
                    plan.Benefit = value;
                    use.ChosenVirtue = receive;
                    use.Exchange.Clear();
                    use.Exchange.AddRange(exchange);
                }
            }
        }
        else
        {
            foreach (Player target in targets)
            {
                float value = float.NegativeInfinity;
                Virtue chosen = null;
                if (power is InfiniteKnowledge)
                    value = target.CanSeeKingdom(viewer) ? 0 : 35;
                else if (power is IdentitySurfing)
                {
                    value = target.CanSeeKingdom(viewer)
                        ? InventoryValue(target.Kingdom, owned.Select(v => v.type)) - baseInventory
                        : 60; // Uncertainty-adjusted prior, not the hidden card's actual goals.
                    if (target.CanSeeVirtues(viewer))
                    {
                        float previous = target.CanSeeKingdom(viewer)
                            ? InventoryValue(target.Kingdom, target.Virtues.Select(v => v.type))
                            : target.Virtues.Count * 15;
                        value -= .35f * (InventoryValue(caster.Kingdom, target.Virtues.Select(v => v.type)) - previous);
                    }
                }
                else if (power is Invisibility && PowerRules.EmptySlots(caster).Count > 0)
                    value = PowerRules.Resources(target).Select(resource =>
                        PowerRules.EmptySlots(caster).Max(slot => PlacementGain(caster, viewer, resource.resourceType, slot)) +
                        .35f * RemovalLoss(target, viewer, resource) + (caster.virtuesHidden ? 0 : 12))
                        .DefaultIfEmpty(float.NegativeInfinity).Max();
                else if (power is Imagination && target.CanSeeVirtues(viewer))
                {
                    foreach (Virtue virtue in target.Virtues.Distinct().OrderBy(v => v.type))
                    {
                        if (prepared?.ChosenVirtue != null && prepared.ChosenVirtue != virtue)
                            continue;
                        float gain = InventoryValue(caster.Kingdom, owned.Select(v => v.type).Concat(new[] { virtue.type })) -
                            baseInventory + .35f * VisibleVirtueLoss(target, viewer, virtue);
                        if (gain > value)
                        {
                            value = gain;
                            chosen = virtue;
                        }
                    }
                }
                if (value > plan.Benefit)
                {
                    plan.Benefit = value;
                    use.Target = target;
                    use.ChosenVirtue = chosen;
                }
            }
        }
        return plan;
    }

    private static float VisibleVirtueLoss(Player player, Player viewer, Virtue virtue)
    {
        if (!player.CanSeeVirtues(viewer))
            return 0;
        return player.CanSeeKingdom(viewer) ? PaymentLoss(player.Kingdom, player.Virtues, new[] { virtue }) : 15;
    }

    private static float ThreatLoss(Player player, Player viewer, bool attack) =>
        RemoveValue(player, viewer, attack ? 2 : Math.Max(1, PowerRules.Resources(player).Count / 2)) +
        (attack || !player.CanSeeVirtues(viewer) ? 0 :
            player.Virtues.Select(virtue => VisibleVirtueLoss(player, viewer, virtue)).DefaultIfEmpty(0).Min());

    public static float Stake(PowerUse use, Player viewer)
    {
        float sign = use.Caster == viewer ? 1 : -.35f;
        if (use.Power is KingsNecklace)
            return use.Trigger == null ? 0 : -Stake(use.Trigger, viewer);
        if (use.Power is Retraction)
            return RetractionValue(viewer);
        if (use.Power is CelestialDome)
            return sign * ThreatLoss(use.Caster, viewer, use.IsAttack);
        if (use.Power is Magic)
            return sign * 2 * (8 + ActionValue(use.Caster, viewer));
        if (use.Power is TimePower)
            return sign * (8 + ActionValue(use.Caster, viewer));
        if (use.Power is Manipulation)
            return use.Target == viewer ? -ActionValue(viewer, viewer) : sign * 60;
        if (use.Power is Imagination && use.ChosenVirtue != null)
        {
            float gain = use.Caster == viewer
                ? InventoryValue(viewer.Kingdom, viewer.Virtues.Select(v => v.type).Concat(new[] { use.ChosenVirtue.type })) -
                    InventoryValue(viewer.Kingdom, viewer.Virtues.Select(v => v.type))
                : 15;
            return sign * gain + (use.Target == viewer ? -VisibleVirtueLoss(viewer, viewer, use.ChosenVirtue) : 0);
        }
        if (use.Power is Invisibility)
            return sign * 20 - (use.Target == viewer ? PowerRules.Resources(viewer)
                .Select(resource => RemovalLoss(viewer, viewer, resource)).DefaultIfEmpty(0).Max() : 0);
        if (use.Power is IdentitySurfing)
        {
            if (use.Target == viewer && use.Caster.CanSeeKingdom(viewer))
                return InventoryValue(use.Caster.Kingdom, viewer.Virtues.Select(v => v.type)) -
                    InventoryValue(viewer.Kingdom, viewer.Virtues.Select(v => v.type));
            return sign * 60;
        }
        if (use.Power is Rain)
            return PlayerManager.Instance.Players.Sum(player => (player == viewer ? 1 : -.35f) *
                (use.RemoveResources ? -RemoveValue(player, viewer, use.ResourceCount) : AddValue(player, viewer, use.ResourceCount)));
        if (use.Power is Abundance)
            return sign * AddValue(use.Caster, viewer, PlayerManager.Instance.Players.Count);
        if (use.Power is BlessingOfPlenty)
            return sign * AddValue(use.Caster, viewer, PowerRules.Resources(use.Caster).Count,
                PowerRules.Resources(use.Caster).Select(resource => resource.resourceType));
        if (use.Power is Witchcraft)
            return sign * RearrangeValue(use.Caster, viewer);
        if (use.Power is TransformPower)
            return sign * use.Exchange.Count * 85;
        if (use.Power is InfiniteKnowledge)
            return sign * 35;
        throw new InvalidOperationException("No stakeholder policy for " + use.Power.GetType().Name + ".");
    }

    private static float RetractionValue(Player viewer)
    {
        float value = 0;
        foreach (Player subject in PlayerManager.Instance.Players)
        {
            SnapshotObservation before = PowerManager.Instance.ObserveBeforeAction(subject, viewer);
            if (before == null)
                continue;
            BotCell[] cells = Cells(subject);
            var oldBoard = cells.ToDictionary(cell => cell.Index, cell =>
                before.Board.TryGetValue(cell.Index, out ResourceType type) ? (ResourceType?)type : null);
            var currentBoard = cells.ToDictionary(cell => cell.Index, cell => cell.Resource);
            // Project both states through the same captured-and-current visibility boundary.
            var nowVirtues = before.Virtues == null ? null : subject.Virtues.Select(virtue => virtue.type).ToArray();
            float restored = ObservedValue(cells, oldBoard, before.Kingdom, before.Virtues);
            float current = ObservedValue(cells, currentBoard, before.Kingdom, nowVirtues);
            value += (subject == viewer ? 1 : -.35f) * (restored - current);
        }
        return value;
    }

    private static float ObservedValue(BotCell[] cells, Dictionary<int, ResourceType?> board,
        Kingdom kingdom, IEnumerable<VirtueType> virtues)
    {
        VirtueType[] visible = virtues?.ToArray();
        float inventory = visible == null ? 0 : kingdom == null ? visible.Length * 15 : InventoryValue(kingdom, visible);
        BotRecipe[] recipes = ForgeManager.Instance.AllVirtues.Select(virtue => new BotRecipe(virtue.type,
            virtue.componentOne, virtue.componentTwo, kingdom == null || visible == null ? 0 :
                Goal(kingdom, virtue.type) - visible.Count(type => type == virtue.type), PowerRules.VirtueStock(virtue))).ToArray();
        return inventory + BoardValue(cells, recipes, board);
    }

    public static BotDecision Analyze(Player owner)
    {
        PowerManager manager = PowerManager.Instance;
        PowerDecisionContext context = manager.DecisionContext;
        if (!manager.IsBusy || context == null || context.Owner != owner ||
            context.Kind == PowerDecisionKind.Unknown || context.Kind == PowerDecisionKind.ManualInspection)
            throw new InvalidOperationException("No typed, automatic power decision belongs to this player.");
        bool preparing = context.Kind == PowerDecisionKind.Reaction || context.Kind == PowerDecisionKind.Payment ||
            context.Kind == PowerDecisionKind.Target || context.Kind == PowerDecisionKind.Exchange ||
            context.Kind == PowerDecisionKind.Virtue || context.Kind == PowerDecisionKind.RainOperation ||
            context.Kind == PowerDecisionKind.Confirm;
        BotPowerPlan plan = preparing
            ? Plan(context.Use?.Caster ?? owner, context) : null;
        var candidates = new List<BotCandidate>();
        foreach (PowerChoice choice in manager.Choices.Where(choice => choice.Enabled))
        {
            if (choice.Option == null || choice.Option.Kind == PowerOptionKind.Unknown)
                throw new InvalidOperationException("Power menu has an untyped option; automation stopped without selecting it.");
            float score = ChoiceScore(owner, context, choice.Option, plan);
            if (float.IsNegativeInfinity(score))
                continue;
            candidates.Add(new BotCandidate(BotActionKind.PowerChoice,
                context.Kind + ": " + choice.Label, new[] { new BotScoreTerm(Reason(context, choice.Option, plan), score) },
                choice: choice));
        }
        if (candidates.Count == 0)
            throw new InvalidOperationException("No legal forward power choice; inspect " + context.Kind + ".");
        return new BotDecision(owner, candidates.OrderByDescending(candidate => candidate.Score).ToArray(),
            Signature(owner, context, manager.ChoiceRevision));
    }

    private static string Reason(PowerDecisionContext context, PowerOption option, BotPowerPlan plan) =>
        option.Kind == PowerOptionKind.Pass || option.Kind == PowerOptionKind.Cancel ? "Decline without a new payment" :
        option.Kind == PowerOptionKind.PaymentAdd ? "Forward selection priority (1000) minus marginal virtue loss" :
        option.Kind == PowerOptionKind.ExchangeAdd ? "Forward selection toward the beneficial exchange multiset" :
        (context.Kind == PowerDecisionKind.Reaction || context.Kind == PowerDecisionKind.Confirm) && plan != null
            ? $"Estimated benefit {plan.Benefit:0.##} minus owned payment loss {plan.Cost:0.##}"
            : option.Kind == PowerOptionKind.Finish ? "No strictly improving rearrangement remains" :
                "Legal forward choice; goal value, board potential and exact remaining selections";

    private static float ChoiceScore(Player owner, PowerDecisionContext context, PowerOption option, BotPowerPlan plan)
    {
        PowerUse use = context.Use;
        if (option.Kind == PowerOptionKind.Back && context.Kind == PowerDecisionKind.RearrangeDestination)
            return 0;
        if (option.Kind == PowerOptionKind.Back || option.Kind == PowerOptionKind.PaymentRemove ||
            option.Kind == PowerOptionKind.ExchangeRemove)
            return float.NegativeInfinity;
        if (option.Kind == PowerOptionKind.Cancel || option.Kind == PowerOptionKind.Pass)
            return 0;
        if (context.Kind == PowerDecisionKind.Notice)
            return option.Kind == PowerOptionKind.Continue ? 1 : float.NegativeInfinity;
        if (context.Kind == PowerDecisionKind.Reaction || context.Kind == PowerDecisionKind.Confirm)
        {
            if (option.Kind != PowerOptionKind.Use || plan == null || plan.Score <= 0)
                return float.NegativeInfinity;
            if (context.Kind == PowerDecisionKind.Confirm && !PowerManager.Instance.Validate(use, out _))
                return float.NegativeInfinity;
            return plan.Score;
        }
        if (context.Kind == PowerDecisionKind.Payment)
        {
            if (plan == null || plan.Score <= 0)
                return float.NegativeInfinity;
            if (option.Kind == PowerOptionKind.Continue)
                return 1;
            var remaining = use.Caster.Virtues.ToList();
            var planned = plan.Use.Payment.ToList();
            foreach (Virtue selected in use.Payment)
            {
                remaining.Remove(selected);
                planned.Remove(selected);
            }
            return option.Kind == PowerOptionKind.PaymentAdd && planned.Contains(option.Virtue)
                ? Math.Max(1, 1000 - PaymentLoss(use.Caster.Kingdom, remaining, new[] { option.Virtue }))
                : float.NegativeInfinity;
        }
        if (context.Kind == PowerDecisionKind.Target)
        {
            if (option.Kind != PowerOptionKind.Target)
                return float.NegativeInfinity;
            PowerUse candidate = Copy(use);
            candidate.Target = option.Player;
            return Plan(use.Caster, new PowerDecisionContext(context.Kind, owner, candidate)).Score;
        }
        if (context.Kind == PowerDecisionKind.Exchange)
        {
            var remaining = plan.Use.Exchange.ToList();
            foreach (Virtue selected in use.Exchange)
                remaining.Remove(selected);
            if (option.Kind == PowerOptionKind.ExchangeAdd)
                return remaining.Contains(option.Virtue) ? 1000 : float.NegativeInfinity;
            return option.Kind == PowerOptionKind.Continue && remaining.Count == 0 ? 1 : float.NegativeInfinity;
        }
        if (context.Kind == PowerDecisionKind.Virtue)
        {
            if (option.Kind != PowerOptionKind.Virtue)
                return float.NegativeInfinity;
            PowerUse candidate = Copy(use);
            candidate.ChosenVirtue = option.Virtue;
            return Plan(use.Caster, new PowerDecisionContext(context.Kind, owner, candidate)).Score;
        }
        if (context.Kind == PowerDecisionKind.RainOperation)
        {
            if (option.Kind != PowerOptionKind.RainOperation)
                return float.NegativeInfinity;
            PowerUse candidate = Copy(use);
            candidate.ResourceCount = option.Count;
            candidate.RemoveResources = option.Remove;
            return Plan(use.Caster, new PowerDecisionContext(PowerDecisionKind.Confirm, owner, candidate)).Score;
        }
        if (context.Kind == PowerDecisionKind.AddResource)
            return option.Kind == PowerOptionKind.ResourceType ? PowerRules.EmptySlots(owner)
                .Select(slot => PlacementGain(owner, owner, option.ResourceType, slot)).DefaultIfEmpty(0).Max() : float.NegativeInfinity;
        if (context.Kind == PowerDecisionKind.EmptySlot)
        {
            if (option.Kind != PowerOptionKind.Slot || !context.ResourceType.HasValue)
                throw new InvalidOperationException("Resource placement needs typed resource and slot metadata.");
            return PlacementGain(owner, owner, context.ResourceType.Value, option.Slot);
        }
        if (context.Kind == PowerDecisionKind.StealResource)
            return option.Kind == PowerOptionKind.Resource ? PowerRules.EmptySlots(owner)
                .Max(slot => PlacementGain(owner, owner, option.Resource.resourceType, slot)) +
                .35f * RemovalLoss(option.Resource.slot.board.player, owner, option.Resource) : float.NegativeInfinity;
        if (context.Kind == PowerDecisionKind.RemoveResource)
            return option.Kind == PowerOptionKind.Resource ? -RemovalLoss(owner, owner, option.Resource) : float.NegativeInfinity;
        if (context.Kind == PowerDecisionKind.RearrangeResource)
        {
            if (option.Kind == PowerOptionKind.Finish)
                return 0;
            if (option.Kind != PowerOptionKind.Resource)
                return float.NegativeInfinity;
            float gain = owner.Board.Slots.Where(slot => slot != option.Resource.slot)
                .Select(slot => RearrangeGain(owner, owner, option.Resource, slot)).DefaultIfEmpty(0).Max();
            return gain > 0 ? gain : float.NegativeInfinity;
        }
        if (context.Kind == PowerDecisionKind.RearrangeDestination)
        {
            if (option.Kind != PowerOptionKind.Slot || context.Resource == null)
                throw new InvalidOperationException("Rearrangement needs a typed source and destination.");
            float gain = RearrangeGain(owner, owner, context.Resource, option.Slot);
            return gain > 0 ? gain : float.NegativeInfinity;
        }
        throw new InvalidOperationException("No bot policy for power phase " + context.Kind + ".");
    }

    private static PowerUse Copy(PowerUse use)
    {
        var copy = new PowerUse
        {
            Caster = use.Caster, Power = use.Power, Target = use.Target, Trigger = use.Trigger,
            ChosenVirtue = use.ChosenVirtue, ResourceCount = use.ResourceCount,
            RemoveResources = use.RemoveResources, IsAttack = use.IsAttack
        };
        copy.Payment.AddRange(use.Payment);
        copy.Exchange.AddRange(use.Exchange);
        return copy;
    }

    private static string Signature(Player owner, PowerDecisionContext context, int revision) =>
        revision + ":" + owner.GetInstanceID() + ":" + context.Kind + ":" +
        string.Join(";", PlayerManager.Instance.Players.SelectMany(player => player.Board.Slots)
            .Select(slot => $"{slot.GetInstanceID()}:{slot.resource?.GetInstanceID()}:{slot.resource?.resourceType}")) +
        ":" + string.Join(";", PlayerManager.Instance.Players.Select(player =>
            (player.CanSeeVirtues(owner) ? string.Join(",", player.Virtues.Select(virtue => virtue.GetInstanceID())) : "hidden") +
            ":" + (player.CanSeeKingdom(owner) ? player.Kingdom.GetInstanceID() + ":" +
                string.Join(",", player.Kingdom.virtuesForWin.Select(goal => $"{goal.virtues.type}:{goal.NumberofVirtues}")) : "hidden"))) +
        ":" + context.Use?.Power.virtueCost + ":" + context.Use?.Target?.GetInstanceID() +
        ":" + (context.Use == null ? "" : string.Join(",", context.Use.Payment.Select(virtue => virtue.GetInstanceID())) +
            "/" + string.Join(",", context.Use.Exchange.Select(virtue => virtue.GetInstanceID())));

    public static string Execute(BotDecision decision)
    {
        BotDecision fresh = Analyze(decision.Player);
        if (fresh.Signature != decision.Signature || !ReferenceEquals(fresh.Chosen.Choice, decision.Chosen.Choice))
            throw new InvalidOperationException("Power state changed after analysis; analyze again.");
        PowerManager manager = PowerManager.Instance;
        if (!manager.TrySelectChoice(decision.Player, manager.ChoiceRevision, decision.Chosen.Choice))
            throw new InvalidOperationException("Power choice is no longer legal.");
        return decision.Chosen.Path + ". Existing power callback invoked.";
    }
}
