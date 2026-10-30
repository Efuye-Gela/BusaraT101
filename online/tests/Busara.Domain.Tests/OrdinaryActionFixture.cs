using System.Text.Json;
using System.Text.Json.Serialization;
using Busara.Online;
using NUnit.Framework;
using static Busara.Domain.Tests.RulesetPins;

namespace Busara.Domain.Tests;

public sealed partial class OrdinaryActionTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        IncludeFields = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private sealed class FixedRandom : IGameRandom { public int Next(int maximum) => 0; }
    private MatchState state = null!;
    private static string Encode<T>(T value) => JsonSerializer.Serialize(value, Json);
    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(Encode(value), Json)!;

    [SetUp]
    public void Setup()
    {
        state = DomainRules.Join(Ordinary(DomainRules.Create("ordinary-test")), 1, "Guest");
        state.seats[0].kingdom = Definitions.Egolica;
        state.seats[1].kingdom = Definitions.Mask;
        state.deck = Enumerable.Range(0, 24).Select(index =>
            new CardState { id = "card-" + index, type = (ResourceType)(index % 4) }).ToList();
        state.deckRemaining = 24;
        state.phase = "Action";
        Board((0, ResourceType.Water), (4, ResourceType.Fire), (16, ResourceType.Earth), (20, ResourceType.Earth));
    }

    private void Board(params (int id, ResourceType type)[] pieces)
    {
        foreach (SlotState slot in state.board) slot.pieceId = null;
        foreach (var piece in pieces)
        {
            state.board[piece.id].pieceId = "piece-" + piece.id;
            state.board[piece.id].type = piece.type;
        }
        Snapshot();
    }

    private void Snapshot() => state.snapshot = DomainRules.Capture(state);
    private void Payment()
    {
        state.seats[1].virtues.AddRange(new[]
        {
            new TokenState { id = "paid-art", type = VirtueType.Art },
            new TokenState { id = "paid-security", type = VirtueType.Security },
            new TokenState { id = "unpaid-nature", type = VirtueType.Nature }
        });
        Snapshot();
    }

    private OnlineCommand Command(string kind, int from = -1, int to = -1, int type = -1, int[]? slots = null) => new()
    {
        kind = kind, commandId = Guid.NewGuid().ToString(), expectedVersion = state.version.ToString(),
        decisionId = state.pending?.id, from = from, to = to, resourceType = type, slots = slots ?? Array.Empty<int>()
    };

    private void Apply(int seat, string kind, int from = -1, int to = -1, int type = -1, int[]? slots = null) =>
        Apply(seat, Command(kind, from, to, type, slots));

    private void Apply(int seat, OnlineCommand command)
    {
        state = RoundTrip(DomainRules.Apply(state, seat, RoundTrip(command), new FixedRandom()).state);
        DomainRules.ValidateState(state);
    }

    private void Reject(int seat, OnlineCommand command, string? code = null)
    {
        string before = Encode(state);
        var error = Assert.Throws<RuleException>(() => DomainRules.Apply(state, seat, command, new FixedRandom()));
        if (code != null) Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(Encode(state), Is.EqualTo(before));
    }

    private IEnumerable<string> Choices(int seat) => Projection.ForSeat(state, seat).choices.Select(choice => choice.kind);
}
