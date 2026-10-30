using Busara.Online;
using NUnit.Framework;

namespace Busara.Domain.Tests;

// busara-online-v4: two to four seats on the offline 8x8 grid, with bots filling empty seats in the lobby.
public sealed class SeatsRulesetTests
{
    private sealed class FixedRandom : IGameRandom { public int Next(int maximum) => 0; }
    private static readonly FixedRandom Random = new();
    private MatchState state = null!;

    private static OnlineCommand Command(MatchState state, string kind, int from = -1, int to = -1, int type = -1,
        int target = -1, string[]? payment = null, int[]? slots = null) => new()
    {
        kind = kind, commandId = Guid.NewGuid().ToString(), expectedVersion = state.version.ToString(),
        decisionId = state.pending?.id, from = from, to = to, resourceType = type, target = target,
        name = state.seats.Count > 0 ? "Seat" + Guid.NewGuid().ToString("N")[..6] : null, ready = true,
        paymentIds = payment ?? Array.Empty<string>(), slots = slots ?? Array.Empty<int>()
    };

    private void Apply(int seat, string kind, int from = -1, int to = -1, int type = -1, int target = -1,
        string[]? payment = null, int[]? slots = null)
    {
        state = DomainRules.Apply(state, seat, Command(state, kind, from, to, type, target, payment, slots), Random).state;
        DomainRules.ValidateState(state);
    }

    // A started four-seat match in the Action phase with chosen kingdoms and pieces.
    private void Playing(int seats, string[] kingdoms, params (int id, ResourceType type)[] pieces)
    {
        state = DomainRules.Join(DomainRules.Create("seats-" + Guid.NewGuid().ToString("N"), seats), 1, "Bo");
        for (int seat = 2; seat < seats; seat++)
            state = DomainRules.Join(state, seat, "P" + seat);
        for (int seat = 0; seat < seats; seat++)
            state.seats[seat].kingdom = kingdoms[seat];
        state.deck = Enumerable.Range(0, 24).Select(index =>
            new CardState { id = "card-" + index, type = (ResourceType)(index % 4) }).ToList();
        state.deckRemaining = 24;
        state.phase = "Action";
        foreach (var piece in pieces)
        {
            SlotState slot = state.board.Single(item => item.id == piece.id);
            slot.pieceId = "piece-" + piece.id;
            slot.type = piece.type;
        }
        state.snapshot = DomainRules.Capture(state);
        DomainRules.ValidateState(state);
    }

    private static readonly string[] Quiet = { "egolica", "konga", "logone", "ubunifu" };

    [Test]
    public void BoardsFollowTheOfflineQuadrantsClockwise()
    {
        Assert.That(DomainRules.Create("two").board.Select(slot => slot.id), Is.EqualTo(Enumerable.Range(0, 32)));
        MatchState four = DomainRules.Create("four", 4);
        Assert.That(four.seats, Has.Count.EqualTo(4));
        Assert.That(four.board, Has.Count.EqualTo(64));
        Assert.That(new[] { 0, 7, 63, 56 }.Select(DomainRules.SeatOfSlot), Is.EqualTo(new[] { 0, 1, 2, 3 }));
        MatchState three = DomainRules.Create("three", 3);
        Assert.That(three.board.Select(slot => slot.seat).Distinct().OrderBy(seat => seat), Is.EqualTo(new[] { 0, 1, 2 }));
        Assert.That(three.board.Any(slot => slot.seat == 3), Is.False);
        Assert.Throws<RuleException>(() => DomainRules.Create("five", 5));
    }

    [Test]
    public void HostFillsSeatsWithBotsAndStartsWhenEverySeatIsReady()
    {
        state = DomainRules.Create("lobby", 4);
        Apply(0, "configure");
        Assert.Throws<RuleException>(() => Apply(0, "start"));
        Assert.Throws<RuleException>(() => Apply(1, "addBot", target: 2), "only the host adds bots");
        Apply(0, "addBot", target: 2);
        Apply(0, "addBot", target: 3);
        Assert.That(state.seats[2].bot && state.seats[2].ready && state.seats[2].name == "Bot 3", Is.True);
        Assert.That(Projection.ForSeat(state, 0).choices.Select(c => c.kind + c.target),
            Is.EquivalentTo(new[] { "configure-1", "addBot1", "removeBot2", "removeBot3" }));
        Assert.Throws<RuleException>(() => DomainRules.Join(state, 2, "Late"), "an invitation never claims a bot seat");
        state = DomainRules.Join(state, 1, "Bo");
        Apply(1, "configure");
        state.seats[0].ready = true;
        Apply(0, "start");
        Assert.That(state.phase, Is.EqualTo("Setup"));
        Assert.That(state.seats.Select(seat => seat.kingdom).Distinct().Count(), Is.EqualTo(4));
        Assert.That(state.seats[2].setupRemaining, Is.EqualTo(Definitions.Setup(2)));
        Assert.That(state.seats[3].setupRemaining, Is.EqualTo(Definitions.Setup(3)));
        Assert.Throws<RuleException>(() => Projection.ForSeat(state, 2), "a bot seat has no player view");
    }

    [Test]
    public void SetupAndTurnsGoRoundEverySeat()
    {
        state = DomainRules.Create("turns", 3);
        state = DomainRules.Join(state, 1, "Bo");
        Apply(0, "addBot", target: 2);
        state.seats[0].ready = state.seats[1].ready = true;
        Apply(0, "start");
        foreach (int seat in new[] { 0, 1, 2 })
        {
            Assert.That(state.activeSeat, Is.EqualTo(seat));
            var spaces = state.board.Where(slot => slot.seat == seat).Select(slot => slot.id)
                .Where(id => id % 2 == (id / 8) % 2).Take(5).ToList();
            foreach (var (type, space) in state.seats[seat].setupRemaining.ToList().Zip(spaces))
                Apply(seat, "setupPlace", to: space, type: (int)type);
        }
        Assert.That(state.phase, Is.EqualTo("Action"));
        Assert.That(state.activeSeat, Is.EqualTo(0));
        int[] order = new int[4];
        for (int turn = 0; turn < 4; turn++)
        {
            order[turn] = state.activeSeat;
            int seat = state.activeSeat;
            SlotState source = state.board.First(slot => slot.seat == seat && slot.pieceId != null &&
                state.board.Any(next => next.seat == seat && next.pieceId == null && SharedRules.Adjacent(slot.id, next.id)));
            SlotState target = state.board.First(next => next.seat == seat && next.pieceId == null &&
                SharedRules.Adjacent(source.id, next.id));
            Apply(seat, "move", from: source.id, to: target.id);
        }
        Assert.That(order, Is.EqualTo(new[] { 0, 1, 2, 0 }));
    }

    [Test]
    public void TradesNameTheirPartner()
    {
        Playing(4, Quiet, (0, ResourceType.Water), (63, ResourceType.Fire), (7, ResourceType.Fire));
        var trades = Projection.ForSeat(state, 0).choices.Where(c => c.kind == "trade" && c.resourceType == (int)ResourceType.Fire).ToList();
        Assert.That(trades.Select(c => c.target), Is.EquivalentTo(new[] { 1, 2, 3 }));
        Assert.Throws<RuleException>(() => Apply(0, "trade", from: 0, type: (int)ResourceType.Fire), "more than two seats need a partner");
        Apply(0, "trade", from: 0, type: (int)ResourceType.Fire, target: 2);
        Assert.That(state.pending.owner, Is.EqualTo(2));
        Apply(2, "tradeAccept");
        Assert.That(state.pending.kind, Is.EqualTo("TradeSelect"));
        Assert.Throws<RuleException>(() => Apply(0, "tradeComplete", to: 7), "only the partner's resource can be swapped");
        Apply(0, "tradeComplete", to: 63);
        Assert.That(state.board.Single(slot => slot.id == 0).type, Is.EqualTo(ResourceType.Fire));
        Assert.That(state.board.Single(slot => slot.id == 63).type, Is.EqualTo(ResourceType.Water));
        Assert.That(state.activeSeat, Is.EqualTo(1));
    }

    [Test]
    public void AWeaponMakesItsTargetDiscard()
    {
        Playing(4, Quiet, (0, ResourceType.Fire), (1, ResourceType.Fire), (2, ResourceType.Fire),
            (56, ResourceType.Earth), (7, ResourceType.Air));
        var weapons = Projection.ForSeat(state, 0).choices.Where(c => c.kind == "weapon").ToList();
        Assert.That(weapons.Select(c => c.target), Is.EquivalentTo(new[] { 1, 2, 3 }));
        Apply(0, "weapon", slots: new[] { 0, 1, 2 }, target: 3);
        Assert.That(state.pending.kind, Is.EqualTo("WeaponDiscard"));
        Assert.That(state.pending.owner, Is.EqualTo(3));
        Apply(3, "weaponDiscard", to: 56);
        Assert.That(state.board.Single(slot => slot.id == 56).pieceId, Is.Null);
        Assert.That(state.board.Single(slot => slot.id == 7).pieceId, Is.Not.Null);
    }

    [Test]
    public void ReactionsAreOfferedToEachEligibleSeatInTurnOrder()
    {
        // Seat 2 holds Retraction and seat 3 Time; both may react after seat 0's action.
        Playing(4, new[] { "egolica", "konga", "mask-of-light", "telalila" }, (0, ResourceType.Water),
            (7, ResourceType.Air), (63, ResourceType.Fire), (56, ResourceType.Earth));
        foreach (int seat in new[] { 2, 3 })
            for (int i = 0; i < 2; i++)
                state.seats[seat].virtues.Add(new TokenState { id = "v" + seat + i, type = VirtueType.Art });
        state.snapshot = DomainRules.Capture(state);
        Apply(0, "move", from: 0, to: 1);
        Assert.That(state.pending.owner, Is.EqualTo(2));
        Assert.That(state.pending.kind, Is.EqualTo("Retraction"));
        Assert.That(Projection.ForSeat(state, 2).decision.prompt, Does.StartWith("Undo"));
        Apply(2, "pass");
        Assert.That(state.pending.owner, Is.EqualTo(3), "after a pass the next eligible seat is asked");
        Assert.That(state.pending.power, Is.EqualTo(KingdomCatalog.Time));
        Apply(3, "pass");
        Assert.That(state.activeSeat, Is.EqualTo(1));
        Assert.That(state.pending.owner, Is.EqualTo(3), "Time is offered again as the next turn starts");
        Assert.That(state.pending.window, Is.EqualTo(PowerWindow.TurnStart));
    }

    [Test]
    public void TargetedPowersNeedATargetWithMoreThanTwoSeats()
    {
        Playing(3, new[] { "nevulandis", "konga", "logone" }, (0, ResourceType.Water));
        state.seats[0].virtues.Add(new TokenState { id = "k", type = VirtueType.Art });
        state.snapshot = DomainRules.Capture(state);
        var uses = Projection.ForSeat(state, 0).choices.Where(c => c.kind == "power").ToList();
        Assert.That(uses.Select(c => c.target), Is.EquivalentTo(new[] { 1, 2 }));
        Assert.Throws<RuleException>(() => Apply(0, "power", payment: new[] { "k" }));
        Apply(0, "power", payment: new[] { "k" }, target: 2);
        Assert.That(state.seats[0].knownKingdoms, Does.Contain("logone"));
        Assert.That(state.seats[0].knownKingdoms, Does.Not.Contain("konga"));
    }

    [Test]
    public void OlderRulesetsStayTwoSeat()
    {
        state = DomainRules.Create("old", 3);
        state.ruleset = Definitions.FullRuleset;
        Assert.Throws<RuleException>(() => DomainRules.ValidateState(state));
        Assert.That(Definitions.MaximumSeats(Definitions.FullRuleset), Is.EqualTo(2));
        Assert.That(Definitions.MaximumSeats(Definitions.SeatsRuleset), Is.EqualTo(4));
    }
}
