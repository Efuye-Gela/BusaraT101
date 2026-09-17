using System.Text.Json;
using Busara.Online;
using NUnit.Framework;

namespace Busara.Domain.Tests;

public sealed class DomainTests
{
    private static readonly JsonSerializerOptions Json = new() { IncludeFields = true };
    private sealed class FixedRandom : IGameRandom { public int Next(int exclusiveMaximum) => 0; }
    private MatchState state = null!;

    [SetUp]
    public void Setup() => state = Started();

    [Test]
    public void AuthoredSetupAndOrdinaryPlayEarnExactRetractionPayment()
    {
        EarnPayment();
        Assert.That(state.seats[1].virtues.Select(token => token.type), Is.EquivalentTo(new[] { VirtueType.Art, VirtueType.Security }));
        Assert.That(state.board.Single(slot => slot.id == 20).pieceId, Is.Not.Null);
        OpponentMove();
        Assert.That(state.pending.kind, Is.EqualTo("Retraction"));
        Assert.That(state.pending.owner, Is.EqualTo(1));
        Assert.That(state.activeSeat, Is.Zero);
        var pending = RoundTrip(state);
        var view = Projection.ForSeat(pending, 1);
        Assert.That(view.decision.id, Is.EqualTo(state.pending.id));
        Assert.That(view.decision.paymentOptions, Has.Count.EqualTo(2));
        Assert.That(view.choices.Select(choice => choice.kind), Is.EquivalentTo(new[] { "use", "pass" }));
    }

    [Test]
    public void UseRestoresBoardSpendsOnlySelectedTokensAndAdvancesWithoutReplacementAction()
    {
        EarnPayment();
        string before = JsonSerializer.Serialize(state.board, Json);
        OpponentMove();
        long actionVersion = state.version;
        state = RoundTrip(state);
        var payment = state.seats[1].virtues.Select(token => token.id).ToArray();
        TransitionResult result = Execute(1, "use", payment: payment);
        Assert.That(result.eventKind, Is.EqualTo("ActionRetracted"));
        Assert.That(result.actionId, Is.Not.Null.And.Not.Empty);
        Assert.That(state.version, Is.EqualTo(actionVersion + 1));
        Assert.That(JsonSerializer.Serialize(state.board, Json), Is.EqualTo(before));
        Assert.That(state.seats[1].virtues, Is.Empty);
        Assert.That(state.seats[1].revealed, Is.True);
        Assert.That(state.pending.kind, Is.EqualTo("RetractionNotice"));
        Assert.That(state.reactionPayments.Single().tokens.Select(token => token.id), Is.EquivalentTo(payment));
        Execute(1, "ack");
        Assert.That(state.activeSeat, Is.EqualTo(1));
        Assert.That(state.phase, Is.EqualTo("Action"));
    }

    [Test]
    public void PassDoesNotSpendRevealOrUndo()
    {
        EarnPayment();
        OpponentMove();
        string board = JsonSerializer.Serialize(state.board, Json);
        string[] payment = state.seats[1].virtues.Select(token => token.id).ToArray();
        Execute(1, "pass");
        Assert.That(JsonSerializer.Serialize(state.board, Json), Is.EqualTo(board));
        Assert.That(state.seats[1].virtues.Select(token => token.id), Is.EquivalentTo(payment));
        Assert.That(state.seats[1].revealed, Is.False);
        Assert.That(state.activeSeat, Is.EqualTo(1));
    }

    [Test]
    public void WrongSeatStaleDecisionFakeAndDuplicatePaymentCannotMutateInput()
    {
        EarnPayment();
        OpponentMove();
        string before = JsonSerializer.Serialize(state, Json);
        OnlineCommand command = Command("use");
        command.paymentIds = state.seats[1].virtues.Select(token => token.id).ToArray();
        Assert.That(() => DomainRules.Apply(state, 0, command, new FixedRandom()), Throws.TypeOf<RuleException>());
        command.decisionId = Guid.NewGuid().ToString();
        Assert.That(() => DomainRules.Apply(state, 1, command, new FixedRandom()), Throws.TypeOf<RuleException>());
        command.decisionId = state.pending.id;
        command.paymentIds[1] = command.paymentIds[0];
        Assert.That(() => DomainRules.Apply(state, 1, command, new FixedRandom()), Throws.TypeOf<RuleException>());
        command.paymentIds[1] = "fabricated";
        Assert.That(() => DomainRules.Apply(state, 1, command, new FixedRandom()), Throws.TypeOf<RuleException>());
        command.expectedVersion = "0";
        Assert.That(() => DomainRules.Apply(state, 1, command, new FixedRandom()), Throws.TypeOf<RuleException>());
        Assert.That(JsonSerializer.Serialize(state, Json), Is.EqualTo(before));
    }

    [Test]
    public void NewlyAcquiredReactionPaymentAndCombinedLedgerAreRejected()
    {
        EarnPayment();
        OpponentMove();
        var ids = state.seats[1].virtues.Select(token => token.id).ToArray();
        state.snapshot.seats[1].virtues.RemoveAt(0);
        Assert.That(DomainRules.CanRestorePayment(state, 1, ids), Is.False);
        Assert.That(Projection.ForSeat(state, 1).choices.Select(choice => choice.kind), Is.EqualTo(new[] { "pass" }));
        state.snapshot.seats[1].virtues = state.seats[1].virtues.Select(DomainRules.Copy).ToList();
        state.reactionPayments.Add(new ReactionPayment { seat = 1, tokens = new List<TokenState> { DomainRules.Copy(state.seats[1].virtues[0]) } });
        Assert.That(DomainRules.CanRestorePayment(state, 1, ids), Is.False);
    }

    [Test]
    public void DrawPlacementAndAbundanceProgressRoundTripBeforeCompletingAction()
    {
        PlaceSetup();
        string deck = JsonSerializer.Serialize(state.deck, Json);
        Execute(0, "draw");
        Assert.That(state.pending.kind, Is.EqualTo("PlaceDraw"));
        state = RoundTrip(state);
        ResourceType drawn = state.pending.drawnCard.type;
        Execute(0, "place", to: 3);
        Assert.That(state.board.Single(slot => slot.id == 3).type, Is.EqualTo(drawn));
        Assert.That(JsonSerializer.Serialize(state.deck, Json), Is.Not.EqualTo(deck));
        Execute(1, "move", from: 6, to: 5);
        Execute(0, "abundance");
        Assert.That(state.seats[0].revealed, Is.True);
        Execute(0, "abundancePlace", to: 1, type: ResourceType.Fire);
        state = RoundTrip(state);
        Assert.That(state.pending.remaining, Is.EqualTo(1));
        Execute(0, "abundancePlace", to: 8, type: ResourceType.Water);
        Assert.That(state.activeSeat, Is.EqualTo(1));
        Execute(1, "move", from: 5, to: 6);
        Assert.That(Projection.ForSeat(state, 0).choices.Any(choice => choice.kind == "abundance"), Is.False);
    }

    [Test]
    public void RetractionRestoresDeckOrderAndDrawCounter()
    {
        EarnPayment();
        var deck = JsonSerializer.Serialize(state.deck, Json);
        int remaining = state.deckRemaining;
        Execute(0, "draw");
        Execute(0, "place", to: 1);
        Execute(1, "use", payment: state.seats[1].virtues.Select(token => token.id).ToArray());
        Assert.That(JsonSerializer.Serialize(state.deck, Json), Is.EqualTo(deck));
        Assert.That(state.deckRemaining, Is.EqualTo(remaining));
        Assert.That(state.board.Single(slot => slot.id == 1).pieceId, Is.Null);
    }

    [Test]
    public void ProjectionsDoNotIncludeOtherHiddenKingdomVirtuesSnapshotDeckOrPrivatePaymentOptions()
    {
        EarnPayment();
        state.seats[1].virtuesHidden = true;
        OpponentMove();
        ClientView first = Projection.ForSeat(state, 0);
        ClientView second = Projection.ForSeat(state, 1);
        string firstJson = JsonSerializer.Serialize(first, Json);
        Assert.That(first.players[1].kingdom, Is.Null);
        Assert.That(first.players[1].virtues, Is.Empty);
        Assert.That(first.decision, Is.Null);
        Assert.That(first.choices, Is.Empty);
        Assert.That(first.phase, Is.EqualTo("Waiting"));
        Assert.That(firstJson, Does.Not.Contain("Retraction").And.Not.Contain("snapshot").And.Not.Contain("drawnCard").And.Not.Contain("deck"));
        foreach (TokenState token in state.seats[1].virtues)
            Assert.That(firstJson, Does.Not.Contain(token.id));
        Assert.That(second.decision.paymentOptions, Has.Count.EqualTo(2));
        second.board[0].pieceId = "client-mutation";
        Assert.That(state.board[0].pieceId, Is.Not.EqualTo("client-mutation"));
    }

    [Test]
    public void InfiniteKnowledgeRemainsKnownAndCasterRevealedAfterItsActionIsRetracted()
    {
        EarnPayment();
        state.seats[0].kingdom = Definitions.Knowledge;
        state.seats[0].virtues.Add(new TokenState { id = "knowledge-payment", type = VirtueType.Nature });
        state.snapshot = DomainRules.Capture(state);
        Execute(0, "knowledge", payment: new[] { "knowledge-payment" });
        Assert.That(Projection.ForSeat(state, 0).decision.prompt, Does.Contain("Mask of Light"));
        Assert.That(Projection.ForSeat(state, 1).decision, Is.Null);
        state = RoundTrip(state);
        Execute(0, "ack");
        Execute(1, "use", payment: state.seats[1].virtues.Select(token => token.id).ToArray());
        Assert.That(state.seats[0].knownKingdoms, Does.Contain(Definitions.Mask));
        Assert.That(state.seats[0].revealed, Is.True);
        Assert.That(state.seats[0].virtues.Any(token => token.id == "knowledge-payment"), Is.True);
    }

    [Test]
    public void CrossBoardPairForgeAwardsBothRecipientsAndCannotFundUnaffordableUndo()
    {
        PlaceSetup();
        state.board.Single(slot => slot.id == 3).pieceId = "cross-water";
        state.board.Single(slot => slot.id == 3).type = ResourceType.Water;
        state.seats[1].virtues.Add(new TokenState { id = "prior-virtue", type = VirtueType.Security });
        state.snapshot = DomainRules.Capture(state);
        Execute(0, "forge", from: 3, to: 4);
        Assert.That(state.seats[0].virtues.Single().type, Is.EqualTo(VirtueType.Art));
        Assert.That(state.seats[1].virtues.Select(token => token.type), Is.EquivalentTo(new[] { VirtueType.Art, VirtueType.Security }));
        Assert.That(state.pending.kind, Is.EqualTo("Retraction"));
        Assert.That(DomainRules.CanRestorePayment(state, 1, state.seats[1].virtues.Select(token => token.id).ToArray()), Is.False);
    }

    [Test]
    public void WinningWaitsForAfterActionDecision()
    {
        EarnPayment();
        state.seats[0].virtues = Definitions.Goals(Definitions.Egolica).SelectMany(goal =>
            Enumerable.Range(0, goal.Value).Select(_ => new TokenState { id = Guid.NewGuid().ToString(), type = goal.Key })).ToList();
        state.snapshot = DomainRules.Capture(state);
        OpponentMove();
        Assert.That(state.winner, Is.EqualTo(-1));
        Execute(1, "pass");
        Assert.That(state.phase, Is.EqualTo("Finished"));
        Assert.That(state.winner, Is.Zero);
    }

    [Test]
    public void SetupRejectsAdjacentPlacementsAndUnsupportedActions()
    {
        Execute(0, "setupPlace", to: 0, type: ResourceType.Earth);
        string before = JsonSerializer.Serialize(state, Json);
        Assert.That(() => Execute(0, "setupPlace", to: 1, type: ResourceType.Water), Throws.TypeOf<RuleException>());
        Assert.That(() => Execute(1, "setupPlace", to: 4, type: ResourceType.Fire), Throws.TypeOf<RuleException>());
        Assert.That(() => Execute(0, "trade"), Throws.TypeOf<RuleException>());
        Assert.That(JsonSerializer.Serialize(state, Json), Is.EqualTo(before));
    }

    [Test]
    public void DefinitionsMatchKnownAssetRecipesSetupAndGoals()
    {
        Assert.That(Definitions.Setup(0), Is.EqualTo(new[] { ResourceType.Earth, ResourceType.Water, ResourceType.Water, ResourceType.Air, ResourceType.Air }));
        Assert.That(Definitions.Setup(1), Is.EqualTo(new[] { ResourceType.Fire, ResourceType.Fire, ResourceType.Fire, ResourceType.Water, ResourceType.Earth }));
        Assert.That(Definitions.Forge(ResourceType.Fire, ResourceType.Water), Is.EqualTo(VirtueType.Art));
        Assert.That(Definitions.Forge(ResourceType.Earth, ResourceType.Fire), Is.EqualTo(VirtueType.Security));
        Assert.That(Definitions.GoalText(Definitions.Mask), Is.EqualTo("3 Art, 4 Security, 2 Economy"));
        Assert.That(SharedRules.Adjacent(3, 4), Is.True);
        Assert.That(SharedRules.Adjacent(7, 8), Is.False);
        Assert.That(SharedRules.Adjacent(0, 9), Is.False);
    }

    [Test]
    public void NormalCommandsReachAuthoredVictoryWithoutSeededInventory()
    {
        PlaceSetup();
        for (int count = 0; count < 1000 && state.phase != "Finished"; count++)
        {
            int actor = state.pending?.owner ?? state.activeSeat;
            ClientView view = Projection.ForSeat(state, actor);
            LegalChoice choice;
            if (state.pending?.kind == "Retraction")
                choice = view.choices.Single(item => item.kind == "pass");
            else if (state.pending?.kind == "PlaceDraw")
            {
                ResourceType type = state.pending.drawnCard.type;
                choice = view.choices.OrderByDescending(item => state.board.Count(slot =>
                    slot.pieceId != null && slot.type != type && slot.seat == actor &&
                    SharedRules.Adjacent(slot.id, item.to) && Needed(actor, Definitions.Forge(type, slot.type)) > 0)).First();
            }
            else
            {
                int resources = state.board.Count(slot => slot.seat == actor && slot.pieceId != null);
                choice = view.choices.Where(item => item.kind == "forge" && resources > 2)
                    .OrderByDescending(item => Needed(actor, Definitions.Forge(
                        state.board.Single(slot => slot.id == item.from).type,
                        state.board.Single(slot => slot.id == item.to).type))).FirstOrDefault()!;
                if (choice != null && Needed(actor, Definitions.Forge(
                    state.board.Single(slot => slot.id == choice.from).type,
                    state.board.Single(slot => slot.id == choice.to).type)) <= 0 && resources < 14)
                    choice = null!;
                if (choice == null && resources > 2)
                    choice = view.choices.Where(item => item.kind == "move" &&
                        state.board.Single(slot => slot.id == item.to).seat == actor)
                        .FirstOrDefault(item =>
                        {
                            ResourceType type = state.board.Single(slot => slot.id == item.from).type;
                            return state.board.Any(slot => slot.pieceId != null && slot.id != item.from &&
                                slot.seat == actor && slot.type != type && SharedRules.Adjacent(slot.id, item.to) &&
                                Needed(actor, Definitions.Forge(type, slot.type)) > 0);
                        })!;
                choice ??= view.choices.FirstOrDefault(item => item.kind == "draw")!;
                choice ??= view.choices.FirstOrDefault(item => item.kind == "forge")!;
                choice ??= view.choices.First(item => item.kind == "move");
            }
            Execute(actor, choice.kind, choice.from, choice.to,
                choice.resourceType >= 0 ? (ResourceType?)choice.resourceType : null);
        }
        Assert.That(state.phase, Is.EqualTo("Finished"));
        Assert.That(state.draw, Is.False, "The legal command sequence must win, not merely stalemate.");
        Assert.That(SharedRules.MeetsGoals(state.seats[state.winner].virtues.Select(token => token.type),
            Definitions.Goals(state.seats[state.winner].kingdom)), Is.True);
    }

    [Test]
    public void MalformedSavedDecisionFailsExplicitlyInsteadOfResumingByDefault()
    {
        EarnPayment();
        OpponentMove();
        state.pending.continuation = "UnrecognizedFuturePhase";
        RuleException? error = Assert.Throws<RuleException>(() => Projection.ForSeat(state, 1));
        Assert.That(error!.Code, Is.EqualTo("invalid_state"));
    }

    private int Needed(int actor, VirtueType type) =>
        Definitions.Goals(state.seats[actor].kingdom).Where(goal => goal.Key == type).Select(goal => goal.Value)
            .DefaultIfEmpty(0).Max() - state.seats[actor].virtues.Count(token => token.type == type);

    private void EarnPayment()
    {
        PlaceSetup();
        OpponentMove();
        Execute(1, "move", from: 6, to: 5);
        OpponentMove();
        Execute(1, "forge", from: 4, to: 5);
        OpponentMove();
        Execute(1, "move", from: 15, to: 14);
        OpponentMove();
        Execute(1, "forge", from: 13, to: 14);
    }

    private void OpponentMove()
    {
        int from = state.board.Single(slot => slot.id == 24).pieceId != null ? 24 : 25;
        Execute(0, "move", from: from, to: from == 24 ? 25 : 24);
    }

    private void PlaceSetup()
    {
        var first = new[] { (0, ResourceType.Earth), (2, ResourceType.Water), (9, ResourceType.Water), (11, ResourceType.Air), (24, ResourceType.Air) };
        var second = new[] { (4, ResourceType.Fire), (13, ResourceType.Fire), (20, ResourceType.Fire), (6, ResourceType.Water), (15, ResourceType.Earth) };
        foreach (var placement in first) Execute(0, "setupPlace", to: placement.Item1, type: placement.Item2);
        foreach (var placement in second) Execute(1, "setupPlace", to: placement.Item1, type: placement.Item2);
    }

    private static MatchState Started()
    {
        var state = DomainRules.Join(DomainRules.Create(Guid.NewGuid().ToString()), 1, "Guest");
        foreach (int seat in new[] { 0, 1 })
            state = DomainRules.Apply(state, seat, new OnlineCommand
            {
                commandId = Guid.NewGuid().ToString(), expectedVersion = state.version.ToString(), kind = "configure",
                name = seat == 0 ? "Ada" : "Bo", ready = true
            }, new FixedRandom()).state;
        return DomainRules.Apply(state, 0, new OnlineCommand
        {
            commandId = Guid.NewGuid().ToString(), expectedVersion = state.version.ToString(), kind = "start"
        }, new FixedRandom()).state;
    }

    private OnlineCommand Command(string kind) => new()
    {
        commandId = Guid.NewGuid().ToString(), expectedVersion = state.version.ToString(), decisionId = state.pending?.id, kind = kind
    };

    private TransitionResult Execute(int seat, string kind, int from = -1, int to = -1, ResourceType? type = null, string[]? payment = null)
    {
        OnlineCommand command = Command(kind);
        command.from = from; command.to = to; command.resourceType = type.HasValue ? (int)type.Value : -1;
        command.paymentIds = payment ?? Array.Empty<string>();
        TransitionResult result = DomainRules.Apply(state, seat, command, new FixedRandom());
        state = result.state;
        return result;
    }

    private static MatchState RoundTrip(MatchState value) =>
        JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(value, Json), Json)!;
}
