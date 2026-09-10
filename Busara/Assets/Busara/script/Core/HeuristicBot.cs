using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class BotDecision
{
    public Player Player { get; }
    public string PlayerName { get; }
    public IReadOnlyList<BotCandidate> Candidates { get; }
    public BotCandidate Chosen => Candidates.Count > 0 ? Candidates[0] : null;
    internal string Signature { get; }

    internal BotDecision(Player player, IReadOnlyList<BotCandidate> candidates, string signature)
    {
        Player = player;
        PlayerName = player.Name;
        Candidates = candidates;
        Signature = signature;
    }
}

public static class HeuristicBot
{
    public static string BlockReason(Player player)
    {
        if (!Application.isPlaying)
            return "Enter Play Mode.";
        if (PlayerManager.Instance == null || TurnManager.Instance == null || ActionManager.Instance == null ||
            SelectionManager.Instance == null || ForgeManager.Instance == null)
            return "Wait for GameScene initialization.";
        if (!TurnManager.Instance.enabled || (GameManager.Instance != null &&
            ((GameManager.Instance.WinScreen != null && GameManager.Instance.WinScreen.activeSelf) ||
             (GameManager.Instance.DrawScreen != null && GameManager.Instance.DrawScreen.activeSelf))))
            return "The game has ended.";
        if (PlayerManager.Instance.Players == null || PlayerManager.Instance.Players.Any(item =>
            item == null || item.Board == null || item.Virtues == null || item.Board.Slots == null || item.Board.Slots.Contains(null)))
            return "The participating player list is invalid.";
        if (PlayerManager.Instance.IsAwaitingSetup || PlayerManager.Instance.Players.Any(item => !item.hasFinishedSettingUp))
            return "Complete player and resource setup.";
        if (PowerManager.Instance != null && PowerManager.Instance.IsBusy)
        {
            PowerDecisionContext context = PowerManager.Instance.DecisionContext;
            if (context == null || context.Kind == PowerDecisionKind.Unknown)
                return "Unrecognized power choice: inspect the typed decision context.";
            if (context.Kind == PowerDecisionKind.ManualInspection)
                return "Manual inspection is open.";
            return context.Owner == player ? "" :
                $"Waiting for {(context.Owner != null ? context.Owner.Name : "an assigned owner")}: {context.Kind}.";
        }
        if (player == null || player != TurnManager.Instance.ActivePlayer)
            return "Waiting for the selected player's turn.";
        if (HardWinterDisaster.Active != null && HardWinterDisaster.Active.RequiresDiscard(player))
        {
            if (player.Kingdom == null || player.Kingdom.virtuesForWin == null ||
                player.Kingdom.virtuesForWin.Any(goal => goal == null || goal.virtues == null))
                return "The bot needs valid victory goals to choose a virtue discard.";
            return player.Virtues.Any(virtue => HardWinterDisaster.Active.CanDiscard(player, virtue))
                ? "" : "Waiting for a legal owned virtue discard.";
        }
        if (TurnManager.Instance.isSpecialCardDrawn || HardWinterDisaster.Active != null)
            return "Resolve the disaster or weapon discard manually.";
        if (player.Board == null || player.Virtues == null || player.Kingdom == null ||
            player.Kingdom.virtuesForWin == null || player.Kingdom.virtuesForWin.Length == 0 ||
            player.Kingdom.virtuesForWin.Any(goal => goal == null || goal.virtues == null) ||
            ForgeManager.Instance.AllVirtues == null || ForgeManager.Instance.AllVirtues.Any(virtue => virtue == null))
            return "The player needs a valid board, inventory, kingdom goals and forge recipes.";
        if (player.Board.Slots == null || player.Board.Slots.Count == 0 ||
            player.Board.Slots.Any(slot => slot == null || slot.board != player.Board ||
                slot.isOccupied != (slot.resource != null) || (slot.resource != null && slot.resource.slot != slot)) ||
            player.Kingdom.virtuesForWin.Any(goal => !ForgeManager.Instance.AllVirtues.Any(virtue => virtue.type == goal.virtues.type)))
            return "Repair inconsistent board slots or missing goal recipes before running the bot.";
        if (player.hasDrawnResource)
        {
            DrawResourceActionMove draw = Object.FindFirstObjectByType<DrawResourceActionMove>(FindObjectsInactive.Include);
            if (ActionManager.Instance.CurrentState != ActionManager.ActionState.DrewCard || draw == null || draw.PendingResource == null)
                return "Finish the pending draw manually.";
            return PowerRules.EmptySlots(player).Count == 0 ? "No owned space for the drawn resource; resolve it manually." : "";
        }
        return ActionManager.Instance.CurrentState == ActionManager.ActionState.None ? "" : "Finish the current action manually.";
    }

    public static BotDecision Analyze(Player player)
    {
        string reason = BlockReason(player);
        if (reason.Length > 0)
            throw new InvalidOperationException(reason);
        if (PowerManager.Instance != null && PowerManager.Instance.IsBusy)
            return BotPowerPlanner.Analyze(player);
        Player viewer = PowerManager.Instance != null && PowerManager.Instance.Controller != null
            ? PowerManager.Instance.Controller : player;
        if (HardWinterDisaster.Active != null)
        {
            HardWinterDisaster winter = HardWinterDisaster.Active;
            var goals = player.Kingdom.virtuesForWin.GroupBy(goal => goal.virtues.type)
                .ToDictionary(group => group.Key, group => group.Max(goal => goal.NumberofVirtues));
            var choices = BotPlanner.EvaluateVirtueDiscard(
                player.Virtues.Where(virtue => winter.CanDiscard(player, virtue)).Select(virtue => virtue.type), goals);
            string discardSignature = $"Winter:{winter.GetInstanceID()}:{winter.DiscardRevision}:{player.GetInstanceID()}:" +
                string.Join(";", player.Virtues.Where(virtue => virtue != null).Select(virtue => $"{virtue.GetInstanceID()}:{virtue.type}")) +
                ":" + string.Join(";", goals.OrderBy(goal => goal.Key).Select(goal => $"{goal.Key}:{goal.Value}"));
            return new BotDecision(player, choices, discardSignature);
        }
        BotCell[] cells = BotPowerPlanner.Cells(player);
        BotRecipe[] recipes = BotPowerPlanner.Recipes(player, viewer);
        DrawResourceActionMove draw = Object.FindFirstObjectByType<DrawResourceActionMove>(FindObjectsInactive.Include);
        Resource pending = player.hasDrawnResource ? draw.PendingResource : null;
        bool canDraw = draw != null && DeckManager.Instance != null && DeckManager.Instance.Cards != null &&
            DeckManager.Instance.Cards.Count > 0;
        var candidates = BotPlanner.Evaluate(cells, recipes, canDraw,
            pending == null ? (ResourceType?)null : pending.resourceType).ToList();
        if (viewer != player)
            candidates = candidates.Select(candidate => new BotCandidate(candidate.Kind, "Controlled turn: " + candidate.Path,
                new[] { new BotScoreTerm("Deny the action owner's visible progress",
                    candidate.Kind == BotActionKind.EndTurn ? 0 :
                    candidate.Kind == BotActionKind.Forge ? -Math.Max(60, candidate.Score) : -candidate.Score) },
                    candidate.From, candidate.To)).ToList();
        else if (!player.hasDrawnResource && PowerManager.Instance != null &&
            player.Kingdom.power.timing == PowerTiming.OwnTurn && PowerManager.Instance.CanUse(player))
        {
            BotPowerPlan power = BotPowerPlanner.Plan(player);
            if (power.Score > 0)
                candidates.Add(new BotCandidate(BotActionKind.UsePower, "Activate " + player.Kingdom.power.powerName,
                    new[] { new BotScoreTerm("Estimated power benefit", power.Benefit),
                        new BotScoreTerm("Owned virtue payment opportunity cost", -power.Cost) }));
        }
        candidates = candidates.OrderByDescending(candidate => candidate.Score).ThenBy(candidate => candidate.Kind)
            .ThenBy(candidate => candidate.From).ThenBy(candidate => candidate.To).ToList();
        // Identity as well as values matters: a replaced piece must invalidate an old execution plan.
        string signature = player.GetInstanceID() + ":" + player.Kingdom.GetInstanceID() + ":" + player.kingdomRevealed +
            ":" + viewer.GetInstanceID() + ":" + ActionManager.Instance.CurrentState + ":" + (pending == null ? 0 : pending.GetInstanceID()) +
            ":" + canDraw + ":" + string.Join(";", player.Board.Slots.Select(slot =>
                $"{slot.Index}:{(slot.resource == null ? 0 : slot.resource.GetInstanceID())}:{slot.resource?.resourceType}")) +
            ":" + string.Join(";", recipes.Select(recipe => $"{recipe.Virtue}:{recipe.Needed}:{recipe.Stock}:{recipe.First}:{recipe.Second}")) +
            ":" + string.Join(";", cells.Select(cell => $"{cell.Index}>{string.Join(",", cell.Neighbors)}"));
        return new BotDecision(player, candidates, signature);
    }

    public static string Execute(BotDecision decision)
    {
        if (decision == null || decision.Chosen == null)
            throw new InvalidOperationException("Analyze a legal decision first.");
        if (decision.Chosen.Kind == BotActionKind.PowerChoice)
            return BotPowerPlanner.Execute(decision);
        BotDecision current = Analyze(decision.Player);
        if (current.Signature != decision.Signature || current.Chosen?.Key != decision.Chosen.Key)
            throw new InvalidOperationException("The game changed after analysis. Analyze again before executing.");
        Player player = decision.Player;
        BotCandidate chosen = decision.Chosen;
        if (chosen.Kind == BotActionKind.DiscardVirtue)
        {
            Virtue owned = player.Virtues.FirstOrDefault(virtue => virtue != null && virtue.type == chosen.DiscardType);
            if (HardWinterDisaster.Active == null || !HardWinterDisaster.Active.TryDiscard(player, owned))
                throw new InvalidOperationException("The virtue discard is no longer legal. Analyze again.");
            return chosen.Path + ". Discarded through the normal special-turn handler.";
        }
        Slot source = player.Board.Slots.FirstOrDefault(slot => slot.Index == chosen.From);
        Slot target = player.Board.Slots.FirstOrDefault(slot => slot.Index == chosen.To);
        SelectionManager.Instance.OnTurnEnd();
        switch (chosen.Kind)
        {
            case BotActionKind.UsePower:
                PowerManager.Instance.ActivatePower(player.Kingdom.power);
                break;
            case BotActionKind.Forge:
                ForgeResourcesActionMove forge = Object.FindFirstObjectByType<ForgeResourcesActionMove>(FindObjectsInactive.Include);
                if (forge == null)
                    throw new InvalidOperationException("The scene has no forge action.");
                player.selectedResources.Add(source.resource);
                player.selectedResources.Add(target.resource);
                forge.OnTapForge();
                break;
            case BotActionKind.Move:
                MoveResourceActionMove move = Object.FindFirstObjectByType<MoveResourceActionMove>(FindObjectsInactive.Include);
                if (move == null)
                    throw new InvalidOperationException("The scene has no move action.");
                move.Onselection(source.resource);
                move.Onselection(target);
                break;
            case BotActionKind.Draw:
                Object.FindFirstObjectByType<DrawResourceActionMove>(FindObjectsInactive.Include).OnTapDraw();
                break;
            case BotActionKind.Place:
                DrawResourceActionMove draw = Object.FindFirstObjectByType<DrawResourceActionMove>(FindObjectsInactive.Include);
                draw.Onselection(draw.PendingResource);
                draw.Onselection(target);
                break;
            case BotActionKind.EndTurn:
                TurnManager.Instance.CompleteTurn(player);
                break;
            default:
                throw new InvalidOperationException("Unsupported bot action.");
        }
        return chosen.Path + ". Normal action handler invoked; bot-owned power choices are evaluated separately.";
    }
}
