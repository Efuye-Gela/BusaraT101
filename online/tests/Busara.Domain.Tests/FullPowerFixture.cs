using System.Text.Json;
using System.Text.Json.Serialization;
using Busara.Online;
using NUnit.Framework;

namespace Busara.Domain.Tests;

public sealed partial class FullPowerTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        IncludeFields = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private sealed class FixedRandom : IGameRandom { public int Next(int maximum) => 0; }
    private MatchState state = null!;
    private static string Encode<T>(T value) => JsonSerializer.Serialize(value, Json);
    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(Encode(value), Json)!;

    private void Setup(string first, string second, params (int id, ResourceType type)[] pieces)
    {
        state = DomainRules.Join(DomainRules.Create("full-" + Guid.NewGuid().ToString("N")), 1, "Bo");
        Assert.That(state.ruleset, Is.EqualTo(Definitions.FullRuleset));
        state.seats[0].name = "Ada";
        state.seats[0].kingdom = first;
        state.seats[1].kingdom = second;
        state.deck = Enumerable.Range(0, 24).Select(index =>
            new CardState { id = "card-" + index, type = (ResourceType)(index % 4) }).ToList();
        state.deckRemaining = 24;
        state.phase = "Action";
        foreach (SlotState slot in state.board) slot.pieceId = null;
        foreach (var piece in pieces)
        {
            state.board[piece.id].pieceId = "piece-" + piece.id;
            state.board[piece.id].type = piece.type;
        }
        state.snapshot = DomainRules.Capture(state);
        DomainRules.ValidateState(state);
    }

    private void Give(int seat, params VirtueType[] types)
    {
        foreach (VirtueType type in types)
            state.seats[seat].virtues.Add(new TokenState { id = "s" + seat + "-" + Guid.NewGuid().ToString("N")[..8], type = type });
        state.snapshot = DomainRules.Capture(state);
    }

    private string[] Ids(int seat, int count) => state.seats[seat].virtues.Take(count).Select(token => token.id).ToArray();

    private OnlineCommand Command(string kind, int from = -1, int to = -1, int type = -1, string[]? payment = null,
        int count = 0, int virtue = -1, string[]? exchange = null, int[]? slots = null) => new()
    {
        kind = kind, commandId = Guid.NewGuid().ToString(), expectedVersion = state.version.ToString(),
        decisionId = state.pending?.id, from = from, to = to, resourceType = type,
        paymentIds = payment ?? Array.Empty<string>(), count = count, virtueType = virtue,
        exchangeIds = exchange ?? Array.Empty<string>(), slots = slots ?? Array.Empty<int>()
    };

    private void Apply(int seat, string kind, int from = -1, int to = -1, int type = -1, string[]? payment = null,
        int count = 0, int virtue = -1, string[]? exchange = null, int[]? slots = null) =>
        Apply(seat, Command(kind, from, to, type, payment, count, virtue, exchange, slots));

    private void Apply(int seat, OnlineCommand command)
    {
        state = RoundTrip(DomainRules.Apply(state, seat, RoundTrip(command), new FixedRandom()).state);
        DomainRules.ValidateState(state);
        foreach (int viewer in new[] { 0, 1 })
            AssertNoLeak(Projection.ForSeat(state, viewer), viewer);
    }

    private void Reject(int seat, OnlineCommand command, string? code = null)
    {
        string before = Encode(state);
        var error = Assert.Throws<RuleException>(() => DomainRules.Apply(state, seat, command, new FixedRandom()));
        if (code != null) Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(Encode(state), Is.EqualTo(before));
    }

    private ClientView View(int seat) => Projection.ForSeat(state, seat);
    private IEnumerable<string> Choices(int seat) => View(seat).choices.Select(choice => choice.kind);

    private void AssertNoLeak(ClientView view, int viewer)
    {
        string json = Encode(view);
        foreach (CardState card in state.deck) Assert.That(json, Does.Not.Contain(card.id));
        if (state.snapshot != null) Assert.That(json, Does.Not.Contain(state.snapshot.actionId));
        SeatState other = state.seats[1 - viewer];
        bool known = other.revealed || state.seats[viewer].knownKingdoms.Contains(other.kingdom) ||
            (view.decision?.kind == "Knowledge");
        if (!known) Assert.That(view.players[1 - viewer].kingdom, Is.Null);
        if (!known) Assert.That(view.players[1 - viewer].powerName, Is.Null);
        if (other.virtuesHidden) Assert.That(view.players[1 - viewer].virtues, Is.Empty);
        bool controlling = viewer == state.controller && other.seat == state.activeSeat && DomainRules.ControllerSees(state);
        if (!controlling)
            foreach (TokenState token in other.virtues) Assert.That(json, Does.Not.Contain(token.id));
    }
}
