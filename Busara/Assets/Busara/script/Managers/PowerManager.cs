using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PowerManager : Manager<PowerManager>
{
    public Action OnPowerActivated;
    public Action OnPowerDeactivated;
    public event Action OnStateChanged;
    public bool IsBusy { get; private set; }
    public Player Controller { get; private set; }
    public Player Viewer => Controller != null ? Controller : TurnManager.Instance.ActivePlayer;
    public string ChoiceTitle { get; private set; }
    public List<PowerChoice> Choices { get; private set; } = new List<PowerChoice>();
    public PowerDecisionContext DecisionContext { get; private set; }
    public int ChoiceRevision => menuVersion;
    public int AdditionalActions => additionalActions;
    public bool MagicActionsActive { get; private set; }
    public TurnSnapshot ActionSnapshot => actionSnapshot;

    private PowerUse preparedUse;
    private Action cancelPreparation;
    private TurnSnapshot actionSnapshot;
    private bool actionCompletionPending;
    private bool retracted;
    private bool cancelledPower;
    private bool cancelledThreat;
    private int additionalActions;
    private readonly Queue<Player> extraTurns = new Queue<Player>();
    private readonly List<PowerUse> reactionPayments = new List<PowerUse>();
    private Player resumePlayer;
    private int menuVersion;

#if UNITY_EDITOR
    public void RefreshPlaytestState()
    {
        if (IsBusy || TurnManager.Instance.isSpecialCardDrawn)
            throw new InvalidOperationException("Finish pending choices before rebasing a playtest fixture.");
        // Fixture edits become the new undo baseline without resetting control or queued Time turns.
        actionSnapshot = TurnManager.Instance.ActivePlayer != null &&
            TurnManager.Instance.ActivePlayer.hasFinishedSettingUp ? TurnSnapshot.Capture() : null;
        reactionPayments.Clear();
        OnStateChanged?.Invoke();
    }
#endif

    public void Choose(string title, List<PowerChoice> choices, PowerDecisionContext context = null)
    {
        IsBusy = true;
        ChoiceTitle = title;
        DecisionContext = context;
        int version = ++menuVersion;
        Choices = choices.Select(choice =>
        {
            PowerOption option = CloneOption(choice.Option);
            PowerChoice wrapped = null;
            bool selected = false;
            wrapped = new PowerChoice(choice.Label, () =>
            {
                // Invalid confirmations retain the human validation notice; bot selections are checked below.
                bool confirmation = context != null && context.Kind == PowerDecisionKind.Confirm &&
                    option != null && option.Kind == PowerOptionKind.Use;
                if (selected || version != menuVersion || !choice.Enabled || !wrapped.Enabled ||
                    (!confirmation && !IsChoiceLegal(context, option)))
                {
                    Debug.LogWarning("That power choice is no longer available.");
                    return;
                }
                selected = true;
                menuVersion++;
                choice.OnSelected();
            }, choice.Enabled, CloneOption(option));
            return wrapped;
        }).ToList();
        if (PowerUIManager.Instance != null)
            PowerUIManager.Instance.ShowChoices(title, Choices);
    }

    private static PowerOption CloneOption(PowerOption option)
    {
        return option == null ? null : new PowerOption(option.Kind)
        {
            Player = option.Player, Virtue = option.Virtue, Resource = option.Resource,
            Slot = option.Slot, ResourceType = option.ResourceType, Count = option.Count,
            Remove = option.Remove
        };
    }

    private PowerDecisionContext Context(PowerDecisionKind kind, PowerUse use, bool preparing = false)
    {
        Player owner = (kind != PowerDecisionKind.Notice || preparing) &&
            use.Power.timing == PowerTiming.OwnTurn && Controller != null ? Controller : use.Caster;
        return new PowerDecisionContext(kind, owner, use)
        {
            Target = use.Target, Trigger = use.Trigger, IsAttack = use.IsAttack
        };
    }

    public bool TrySelectChoice(Player owner, int revision, PowerChoice choice)
    {
        if (!IsBusy || owner == null || DecisionContext == null || DecisionContext.Owner != owner ||
            (DecisionContext.Kind == PowerDecisionKind.ManualInspection || DecisionContext.Kind == PowerDecisionKind.Unknown) ||
            revision != menuVersion || choice == null || !Choices.Contains(choice) ||
            !choice.Enabled || choice.Option == null || choice.Option.Kind == PowerOptionKind.Unknown ||
            !IsChoiceLegal(DecisionContext, choice.Option))
            return false;
        choice.OnSelected();
        return revision != menuVersion;
    }

    private bool IsChoiceLegal(PowerDecisionContext context, PowerOption option)
    {
        if (context == null || option == null)
            return true;
        PowerUse use = context.Use;
        switch (option.Kind)
        {
            case PowerOptionKind.Use:
                return use != null ? Validate(use, out _) : CanUse(context.Owner);
            case PowerOptionKind.PaymentAdd:
                return use != null && option.Virtue != null &&
                    use.Payment.Count < use.Power.virtueCost &&
                    use.Payment.Count(v => v == option.Virtue) < use.Caster.Virtues.Count(v => v == option.Virtue);
            case PowerOptionKind.PaymentRemove:
                return use != null && use.Payment.Contains(option.Virtue);
            case PowerOptionKind.ExchangeAdd:
                return use != null && option.Virtue != null &&
                    use.Payment.Concat(use.Exchange).Count(v => v == option.Virtue) <
                    use.Caster.Virtues.Count(v => v == option.Virtue);
            case PowerOptionKind.ExchangeRemove:
                return use != null && use.Exchange.Contains(option.Virtue);
            case PowerOptionKind.Continue:
                return context.Kind != PowerDecisionKind.Payment || (use != null &&
                    PowerRules.CanPay(use.Caster.Virtues, use.Payment, use.Power.virtueCost));
            case PowerOptionKind.Target:
                return use != null && option.Player != null && option.Player != use.Caster &&
                    PlayerManager.Instance.Players.Contains(option.Player) &&
                    (!(use.Power is Imagination) || option.Player.Virtues.Count > 0) &&
                    (!(use.Power is Invisibility) || (PowerRules.Resources(option.Player).Count > 0 &&
                        PowerRules.EmptySlots(use.Caster).Count > 0));
            case PowerOptionKind.Virtue:
                return option.Virtue != null && ForgeManager.Instance.AllVirtues.Contains(option.Virtue) &&
                    (use == null || !(use.Power is Imagination) ||
                        (use.Target != null && use.Target.Virtues.Contains(option.Virtue)));
            case PowerOptionKind.ResourceType:
                return PowerRules.ResourceStock(option.ResourceType) > 0 &&
                    PowerRules.EmptySlots(context.Owner).Count > 0;
            case PowerOptionKind.Resource:
                Player source = context.Kind == PowerDecisionKind.StealResource ? context.Target : context.Owner;
                return source != null && option.Resource != null && PowerRules.Resources(source).Contains(option.Resource) &&
                    (context.Kind != PowerDecisionKind.StealResource || PowerRules.EmptySlots(context.Owner).Count > 0);
            case PowerOptionKind.Slot:
                if (option.Slot == null || !context.Owner.Board.Slots.Contains(option.Slot))
                    return false;
                if (context.Kind == PowerDecisionKind.RearrangeDestination)
                    return context.Resource != null && context.Resource.slot != option.Slot &&
                        PowerRules.Resources(context.Owner).Contains(context.Resource);
                return PowerRules.EmptySlots(context.Owner).Contains(option.Slot) &&
                    (!ReferenceEquals(context.Resource, null)
                        ? context.Resource != null && context.Target != null &&
                            PowerRules.Resources(context.Target).Contains(context.Resource)
                        : !context.ResourceType.HasValue || PowerRules.ResourceStock(context.ResourceType.Value) > 0);
            default:
                return true;
        }
    }

    public void Notice(string message, Action next, PowerDecisionContext context = null)
    {
        Choose(message, new List<PowerChoice>
        {
            new PowerChoice("Continue", next, true, new PowerOption(PowerOptionKind.Continue))
        }, context);
    }

    private void Close()
    {
        IsBusy = false;
        DecisionContext = null;
        menuVersion++;
        Choices.Clear();
        if (PowerUIManager.Instance != null)
            PowerUIManager.Instance.HideChoices();
    }

    public bool CanUse(Player player)
    {
        return player != null && player.Kingdom != null && player.Kingdom.power != null
            && player.Virtues != null && player.Virtues.Count >= player.Kingdom.power.virtueCost
            && (!player.Kingdom.power.onlyWhileHidden || !player.kingdomRevealed);
    }

    public void ActivatePower(Power power)
    {
        Player player = TurnManager.Instance.ActivePlayer;
        if (IsBusy || TurnManager.Instance.isSpecialCardDrawn || !CanUse(player) ||
            power != player.Kingdom.power || power.timing != PowerTiming.OwnTurn ||
            !ActionManager.Instance.CanPerformAction())
        {
            Debug.LogWarning("This power is unavailable now. Reaction powers are offered at their legal timing.");
            if (!IsBusy)
                Notice("This power cannot be used now. Reaction powers are offered automatically at their legal timing.", Close,
                    new PowerDecisionContext(PowerDecisionKind.Notice, Controller != null ? Controller : player));
            return;
        }

        Prepare(player, null, () =>
        {
            Close();
            TurnManager.Instance.CompleteTurn(player);
        }, Close);
    }

    private void Prepare(Player caster, Player target, Action complete, Action cancel,
        PowerUse trigger = null, bool isAttack = false)
    {
        preparedUse = new PowerUse
        {
            Caster = caster, Power = caster.Kingdom.power, Target = target, Trigger = trigger, IsAttack = isAttack,
            Complete = () => { OnStateChanged?.Invoke(); complete(); }
        };
        cancelPreparation = cancel;
        OnPowerActivated?.Invoke();
        PaymentMenu(preparedUse);
    }

    private void PaymentMenu(PowerUse use)
    {
        var choices = new List<PowerChoice>();
        foreach (Virtue virtue in use.Caster.Virtues.Distinct())
        {
            Virtue item = virtue;
            int selected = use.Payment.Count(v => v == item);
            int available = use.Caster.Virtues.Count(v => v == item);
            choices.Add(new PowerChoice($"+ {item.type} ({selected}/{available})", () =>
            {
                use.Payment.Add(item);
                PaymentMenu(use);
            }, selected < available && use.Payment.Count < use.Power.virtueCost,
                new PowerOption(PowerOptionKind.PaymentAdd) { Virtue = item }));
            if (selected > 0)
                choices.Add(new PowerChoice($"- {item.type}", () => { use.Payment.Remove(item); PaymentMenu(use); },
                    true, new PowerOption(PowerOptionKind.PaymentRemove) { Virtue = item }));
        }
        choices.Add(new PowerChoice("Continue", () => Configure(use),
            PowerRules.CanPay(use.Caster.Virtues, use.Payment, use.Power.virtueCost),
            new PowerOption(PowerOptionKind.Continue)));
        choices.Add(new PowerChoice("Cancel / pass", CancelPower, true, new PowerOption(PowerOptionKind.Cancel)));
        Choose($"{use.Caster.Name}: {use.Power.powerName}\n{use.Power.powerDescription}\n" +
            $"Choose payment: {use.Payment.Count}/{use.Power.virtueCost} virtues.", choices,
            Context(PowerDecisionKind.Payment, use));
    }

    private void Configure(PowerUse use)
    {
        if (use.Power is IdentitySurfing || use.Power is InfiniteKnowledge ||
            use.Power is Imagination || use.Power is Invisibility)
        {
            var choices = new List<PowerChoice>();
            foreach (Player player in PlayerManager.Instance.Players.Where(p => p != use.Caster))
            {
                Player target = player;
                bool eligible = (!(use.Power is Imagination) || target.Virtues.Count > 0) &&
                    (!(use.Power is Invisibility) || (PowerRules.Resources(target).Count > 0 &&
                        PowerRules.EmptySlots(use.Caster).Count > 0));
                choices.Add(new PowerChoice(target.Name, () =>
                {
                    use.Target = target;
                    if (use.Power is Imagination)
                        ChooseVirtue(use, false);
                    else
                        Confirm(use);
                }, eligible, new PowerOption(PowerOptionKind.Target) { Player = target }));
            }
            choices.Add(new PowerChoice("Back", () => PaymentMenu(use), true, new PowerOption(PowerOptionKind.Back)));
            choices.Add(new PowerChoice("Cancel / pass", CancelPower, true, new PowerOption(PowerOptionKind.Cancel)));
            Choose("Choose another player.", choices, Context(PowerDecisionKind.Target, use));
        }
        else if (use.Power is TransformPower)
            ExchangeMenu(use);
        else if (use.Power is Rain)
        {
            var choices = new List<PowerChoice>();
            for (int i = 1; i <= 3; i++)
            {
                int count = i;
                choices.Add(new PowerChoice($"Everyone adds {count}", () =>
                {
                    use.ResourceCount = count;
                    use.RemoveResources = false;
                    Confirm(use);
                }, true, new PowerOption(PowerOptionKind.RainOperation) { Count = count, Remove = false }));
                choices.Add(new PowerChoice($"Everyone removes {count}", () =>
                {
                    use.ResourceCount = count;
                    use.RemoveResources = true;
                    Confirm(use);
                }, true, new PowerOption(PowerOptionKind.RainOperation) { Count = count, Remove = true }));
            }
            choices.Add(new PowerChoice("Back", () => PaymentMenu(use), true, new PowerOption(PowerOptionKind.Back)));
            choices.Add(new PowerChoice("Cancel / pass", CancelPower, true, new PowerOption(PowerOptionKind.Cancel)));
            Choose("Rain: choose the same operation and count for all players.", choices,
                Context(PowerDecisionKind.RainOperation, use));
        }
        else
            Confirm(use);
    }

    private void ExchangeMenu(PowerUse use)
    {
        var choices = new List<PowerChoice>();
        foreach (Virtue virtue in use.Caster.Virtues.Distinct())
        {
            Virtue item = virtue;
            int available = use.Caster.Virtues.Count(v => v == item) - use.Payment.Count(v => v == item);
            int selected = use.Exchange.Count(v => v == item);
            choices.Add(new PowerChoice($"+ {item.type} to exchange ({selected}/{available})", () =>
            {
                use.Exchange.Add(item);
                ExchangeMenu(use);
            }, selected < available, new PowerOption(PowerOptionKind.ExchangeAdd) { Virtue = item }));
            if (selected > 0)
                choices.Add(new PowerChoice($"- {item.type} from exchange", () =>
                {
                    use.Exchange.Remove(item);
                    ExchangeMenu(use);
                }, true, new PowerOption(PowerOptionKind.ExchangeRemove) { Virtue = item }));
        }
        choices.Add(new PowerChoice("Choose the received virtue type", () => ChooseVirtue(use, true),
            true, new PowerOption(PowerOptionKind.Continue)));
        choices.Add(new PowerChoice("Back to payment", () => { use.Exchange.Clear(); PaymentMenu(use); },
            true, new PowerOption(PowerOptionKind.Back)));
        choices.Add(new PowerChoice("Cancel / pass", CancelPower, true, new PowerOption(PowerOptionKind.Cancel)));
        Choose($"Transform: exchange {use.Exchange.Count} virtues in addition to the activation cost.", choices,
            Context(PowerDecisionKind.Exchange, use));
    }

    private void ChooseVirtue(PowerUse use, bool transform)
    {
        var choices = ForgeManager.Instance.AllVirtues.Select(virtue => new PowerChoice(virtue.type.ToString(), () =>
        {
            use.ChosenVirtue = virtue;
            Confirm(use);
        }, true,
            new PowerOption(PowerOptionKind.Virtue) { Virtue = virtue })).ToList();
        choices.Add(new PowerChoice("Back", () => Configure(use), true, new PowerOption(PowerOptionKind.Back)));
        choices.Add(new PowerChoice("Cancel / pass", CancelPower, true, new PowerOption(PowerOptionKind.Cancel)));
        Choose(transform ? "Choose one virtue type to receive from the stockpile." : "Choose a virtue to take.", choices,
            Context(PowerDecisionKind.Virtue, use));
    }

    private void Confirm(PowerUse use)
    {
        preparedUse = use;
        Choose($"{use.Caster.Name}: use {use.Power.powerName}?\nCost: {use.Payment.Count} virtues." +
            (use.Target != null ? $"\nTarget: {use.Target.Name}" : "") +
            (use.ChosenVirtue != null ? $"\nVirtue: {use.ChosenVirtue.type}" : ""),
            new List<PowerChoice>
            {
                new PowerChoice("Use power", UsePower, true, new PowerOption(PowerOptionKind.Use)),
                new PowerChoice("Back", () => Configure(use), true, new PowerOption(PowerOptionKind.Back)),
                new PowerChoice("Cancel / pass", CancelPower, true, new PowerOption(PowerOptionKind.Cancel))
            }, Context(PowerDecisionKind.Confirm, use));
    }

    public bool IsValid()
    {
        return preparedUse != null && Validate(preparedUse, out _);
    }

    public bool Validate(PowerUse use, out string error)
    {
        error = null;
        if (use == null || !CanUse(use.Caster) || use.Power != use.Caster.Kingdom.power ||
            !PowerRules.CanPay(use.Caster.Virtues, use.Payment, use.Power.virtueCost))
            error = "Choose the exact cost using virtues you own.";
        else if ((use.Power is IdentitySurfing || use.Power is InfiniteKnowledge ||
            use.Power is Imagination || use.Power is Invisibility) &&
            (use.Target == null || use.Target == use.Caster || !PlayerManager.Instance.Players.Contains(use.Target)))
            error = "Choose another player in this game.";
        else if (use.Power is Imagination &&
            (use.ChosenVirtue == null || !use.Target.Virtues.Contains(use.ChosenVirtue)))
            error = "The chosen player does not have that virtue.";
        else if (use.Power is Invisibility &&
            (PowerRules.EmptySlots(use.Caster).Count == 0 || PowerRules.Resources(use.Target).Count == 0))
            error = "Stealing requires an empty space on your board and a resource on the target's board.";
        else if (use.Power is TransformPower)
        {
            var spending = use.Payment.Concat(use.Exchange).ToList();
            if (use.ChosenVirtue == null || !PowerRules.CanPay(use.Caster.Virtues, spending, spending.Count))
                error = "Choose exchange virtues separately from the activation cost.";
            else if (PowerRules.VirtueStock(use.ChosenVirtue) +
                spending.Count(v => v.type == use.ChosenVirtue.type) < use.Exchange.Count)
                error = "The stockpile cannot supply that many virtues of the chosen type.";
        }
        else if (use.Power is Rain && (use.ResourceCount < 1 || use.ResourceCount > 3))
            error = "Rain must add or remove between one and three resources.";
        else if (use.Power is Retraction && (actionSnapshot == null || retracted || !CanRestorePayments(use)))
            error = "Retraction needs an undoable action and all reaction payments must remain payable after undoing it.";
        return error == null;
    }

    private bool CanRestorePayments(PowerUse retraction)
    {
        return reactionPayments.Concat(new[] { retraction }).Distinct()
            .GroupBy(use => use.Caster)
            .All(group => actionSnapshot.CanPay(group.Key, group.SelectMany(use => use.Payment).ToList()));
    }

    public bool CanRetractPayment(PowerUse use)
    {
        return use != null && actionSnapshot != null && !retracted && CanRestorePayments(use);
    }

    public SnapshotObservation ObserveBeforeAction(Player subject, Player viewer)
    {
        SnapshotObservation observation = actionSnapshot?.Observe(subject, viewer);
        if (observation == null || observation.Virtues == null)
            return observation;
        var virtues = observation.Virtues.ToList();
        foreach (PowerUse reaction in reactionPayments.Where(use => use.Caster == subject))
            foreach (Virtue payment in reaction.Payment)
                virtues.Remove(payment.type);
        return new SnapshotObservation(observation.Board, virtues, observation.Kingdom);
    }

    public void UsePower()
    {
        PowerUse use = preparedUse;
        if (use == null)
        {
            Debug.LogWarning("No power is awaiting confirmation.");
            return;
        }
        if (!Validate(use, out string error))
        {
            Choose(error, new List<PowerChoice>
            {
                new PowerChoice("Back to payment", () => PaymentMenu(use), true, new PowerOption(PowerOptionKind.Back)),
                new PowerChoice("Cancel / pass", CancelPower, true, new PowerOption(PowerOptionKind.Cancel))
            }, Context(PowerDecisionKind.Notice, use, preparing: true));
            return;
        }

        preparedUse = null;
        cancelPreparation = null;
        foreach (Virtue virtue in use.Payment)
            use.Caster.Virtues.Remove(virtue);
        if (use.Power.timing != PowerTiming.OwnTurn && actionSnapshot != null)
            reactionPayments.Add(use);
        use.Caster.kingdomRevealed = true;
        use.Caster.selectedVirtue.Clear();
        OnStateChanged?.Invoke();
        OnPowerDeactivated?.Invoke();
        RunCommitted(use);
    }

    private void RunCommitted(PowerUse use)
    {
        bool previousCancellation = cancelledPower;
        cancelledPower = false;
        var opponents = PlayerManager.Instance.Players.Where(player => player != use.Caster &&
            CanUse(player) && player.Kingdom.power is KingsNecklace).ToList();
        Offer(opponents, "A power was activated. King's Necklace may cancel its effect (not its cost).",
            player => !cancelledPower && CanUse(player),
            (player, next) => Prepare(player, use.Caster, next, next, use),
            () =>
            {
                bool cancelled = cancelledPower;
                cancelledPower = previousCancellation;
                if (cancelled)
                    Notice($"{use.Power.powerName} was cancelled. Its activation cost is still paid.", use.Complete,
                        Context(PowerDecisionKind.Notice, use));
                else
                    use.Power.Execute(use);
            }, target: use.Caster, trigger: use);
    }

    public void CancelPower()
    {
        if (preparedUse == null || cancelPreparation == null)
        {
            Debug.LogWarning("A committed power cannot be cancelled. Finish the current effect.");
            return;
        }
        Action cancel = cancelPreparation;
        preparedUse = null;
        cancelPreparation = null;
        OnPowerDeactivated?.Invoke();
        cancel();
    }

    private void Offer(List<Player> players, string title, Func<Player, bool> eligible,
        Action<Player, Action> activate, Action done, int index = 0, Player target = null,
        PowerUse trigger = null, bool isAttack = false)
    {
        if (index == players.Count)
        {
            done();
            return;
        }
        Player player = players[index];
        Action next = () => Offer(players, title, eligible, activate, done, index + 1, target, trigger, isAttack);
        if (!eligible(player))
        {
            next();
            return;
        }
        Choose($"{title}\n{player.Name}: {player.Kingdom.power.powerName}", new List<PowerChoice>
        {
            new PowerChoice("Use power", () => activate(player, next), true, new PowerOption(PowerOptionKind.Use)),
            new PowerChoice("Pass", next, true, new PowerOption(PowerOptionKind.Pass))
        }, new PowerDecisionContext(PowerDecisionKind.Reaction, player)
        {
            Target = target, Trigger = trigger, IsAttack = isAttack
        });
    }

    public void BeginNormalTurn(Player activePlayer)
    {
        Controller = null;
        additionalActions = 0;
        MagicActionsActive = false;
        retracted = false;
        actionCompletionPending = false;
        var players = PlayerManager.Instance.Players.Where(player => player != activePlayer &&
            player.Kingdom != null && (player.Kingdom.power is Manipulation || player.Kingdom.power is TimePower)).ToList();
        Offer(players, $"{activePlayer.Name} is about to act.", CanUse,
            (player, next) => Prepare(player, activePlayer, next, next),
            () =>
            {
                actionSnapshot = TurnSnapshot.Capture();
                reactionPayments.Clear();
                Close();
                if (Controller != null)
                    Notice($"{Controller.Name} chooses {activePlayer.Name}'s action. Trading is not allowed.", Close,
                        new PowerDecisionContext(PowerDecisionKind.Notice, Controller) { Target = activePlayer });
            }, target: activePlayer);
    }

    public void CompleteAction(Player player, Action endTurn)
    {
        if (actionCompletionPending)
        {
            Debug.LogWarning("This action is already awaiting reactions.");
            return;
        }
        actionCompletionPending = true;
        var players = PlayerManager.Instance.Players.Where(other => other != player && other.Kingdom != null &&
            (other.Kingdom.power is Retraction || other.Kingdom.power is TimePower)).ToList();
        Offer(players, $"{player.Name}'s action has resolved; their turn has not ended yet.",
            other => CanUse(other) && (!(other.Kingdom.power is Retraction) || (!retracted && actionSnapshot != null)),
            (other, next) => Prepare(other, player, next, next),
            () =>
            {
                actionCompletionPending = false;
                Close();
                if (additionalActions > 0 && !retracted)
                {
                    additionalActions--;
                    SelectionManager.Instance.OnTurnEnd();
                    ActionManager.Instance.ResetActionState();
                    player.hasDrawnResource = false;
                    actionSnapshot = TurnSnapshot.Capture();
                    reactionPayments.Clear();
                    Notice($"{player.Name} has {additionalActions + 1} action(s) remaining.", Close,
                        new PowerDecisionContext(PowerDecisionKind.Notice, Controller != null ? Controller : player)
                        { Target = player });
                }
                else
                {
                    Controller = null;
                    MagicActionsActive = false;
                    actionSnapshot = null;
                    reactionPayments.Clear();
                    endTurn();
                }
            }, target: player);
    }

    public void GrantTwoActions()
    {
        additionalActions += 2;
        MagicActionsActive = true;
    }

    public void ControlTurn(Player player)
    {
        Controller = player;
        OnStateChanged?.Invoke();
    }

    public void ScheduleTurn(Player player)
    {
        extraTurns.Enqueue(player);
    }

    public Player NextPlayer(Player sequentialNext)
    {
        if (extraTurns.Count > 0)
        {
            if (resumePlayer == null)
                resumePlayer = sequentialNext;
            return extraTurns.Dequeue();
        }
        if (resumePlayer == null)
            return sequentialNext;
        Player next = resumePlayer;
        resumePlayer = null;
        return next;
    }

    public void Retract(PowerUse use)
    {
        if (!reactionPayments.Contains(use))
            reactionPayments.Add(use);
        actionSnapshot.Restore();
        // Reactions are independent commitments, not part of the move being undone.
        foreach (PowerUse reaction in reactionPayments)
            foreach (Virtue virtue in reaction.Payment)
                reaction.Caster.Virtues.Remove(virtue);
        use.Caster.kingdomRevealed = true;
        additionalActions = 0;
        MagicActionsActive = false;
        retracted = true;
        Notice($"{use.Target.Name}'s action was undone. Retraction's cost remains paid.", use.Complete,
            Context(PowerDecisionKind.Notice, use));
    }

    public void CancelPendingPower()
    {
        cancelledPower = true;
    }

    public void InspectPlayer(Player player)
    {
        if (IsBusy)
            return;
        string kingdom = player.CanSeeKingdom(Viewer)
            ? $"{player.Kingdom.kingdomName}\n{player.Kingdom.kingdomStory}\n" +
                $"{player.Kingdom.power.powerName}\n{player.Kingdom.power.powerDescription}\nGoal: " +
                string.Join(", ", player.Kingdom.virtuesForWin.Select(goal => $"{goal.NumberofVirtues} {goal.virtues.type}"))
            : "Kingdom card: hidden";
        string virtues = player.CanSeeVirtues(Viewer)
            ? string.Join(", ", player.Virtues.GroupBy(virtue => virtue.type).Select(group => $"{group.Key}: {group.Count()}"))
            : "Virtues: hidden";
        Notice($"{player.Name}\n{kingdom}\n{virtues}", Close,
            new PowerDecisionContext(PowerDecisionKind.ManualInspection, Viewer) { Target = player });
    }

    public void CancelPendingThreat()
    {
        cancelledThreat = true;
    }

    public void OfferProtection(Player defender, string threat, Action prevented, Action proceed,
        bool isAttack = false, Player target = null)
    {
        if (!CanUse(defender) || !(defender.Kingdom.power is CelestialDome))
        {
            proceed();
            return;
        }
        cancelledThreat = false;
        Offer(new List<Player> { defender }, threat, CanUse,
            (player, next) => Prepare(player, target, next, next, isAttack: isAttack),
            () =>
            {
                Close();
                if (cancelledThreat)
                    prevented();
                else
                    proceed();
            }, target: target, isAttack: isAttack);
    }
}
