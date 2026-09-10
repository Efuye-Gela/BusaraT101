public enum PowerDecisionKind
{
    Unknown, Reaction, Payment, Target, Exchange, Virtue, RainOperation, Confirm,
    AddResource, EmptySlot, StealResource, RearrangeResource, RearrangeDestination,
    RemoveResource, Notice, ManualInspection
}

public enum PowerOptionKind
{
    Unknown, Use, Pass, Continue, Cancel, Back, PaymentAdd, PaymentRemove,
    ExchangeAdd, ExchangeRemove, Target, Virtue, RainOperation, ResourceType,
    Resource, Slot, Finish
}

public sealed class PowerOption
{
    public PowerOptionKind Kind;
    public Player Player;
    public Virtue Virtue;
    public Resource Resource;
    public Slot Slot;
    public ResourceType ResourceType;
    public int Count;
    public bool Remove;

    public PowerOption(PowerOptionKind kind) { Kind = kind; }
}

public sealed class PowerDecisionContext
{
    public PowerDecisionKind Kind;
    public Player Owner;
    public PowerUse Use;
    public PowerUse Trigger;
    public Player Target;
    public Resource Resource;
    public ResourceType? ResourceType;
    public int Count;
    public bool IsAttack;

    public PowerDecisionContext(PowerDecisionKind kind, Player owner, PowerUse use = null)
    {
        Kind = kind;
        Owner = owner;
        Use = use;
    }
}
