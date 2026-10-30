using Busara.Online;
using NUnit.Framework;

namespace Busara.Ugs.Tests;

// Rooms for two to four seats: a short room code or the invitation link claims the next empty seat, the host
// fills seats with bots, and bots play at once after each human command.
public sealed partial class MatchServiceTests
{
    private async Task<RoomResult> CreateRoom(string actor, int seats)
    {
        var reply = await service.Execute(actor, "create",
            Json.Encode(new RoomRequest { commandId = Guid.NewGuid().ToString(), seats = seats }), "");
        Assert.That(reply.status, Is.EqualTo(200), reply.body);
        return Json.Decode<RoomResult>(reply.body);
    }

    private Task<Reply> JoinWith(string actor, string? code = null, string? token = null) =>
        service.Execute(actor, "join", Json.Encode(new RoomRequest
        {
            commandId = Guid.NewGuid().ToString(), code = code, inviteToken = token
        }), "");

    [Test]
    public async Task ACodeOrTheLinkClaimsTheNextEmptySeatUntilTheRoomIsFull()
    {
        foreach (string actor in new[] { "third", "fourth", "fifth" })
            Assert.That((await service.Execute(actor, "register", "{}", "")).status, Is.EqualTo(200));
        var room = await CreateRoom("host", 4);
        match = room.matchId;
        Assert.That(room.code, Has.Length.EqualTo(8));
        string spaced = room.code[..4].ToLowerInvariant() + "-" + room.code[4..];
        Assert.That((await JoinWith("guest", code: spaced)).status, Is.EqualTo(200), "codes ignore case and dashes");
        Assert.That((await JoinWith("third", token: room.inviteUrl["#invite=".Length..])).status, Is.EqualTo(200));
        Assert.That((await JoinWith("fourth", code: room.code)).status, Is.EqualTo(200));
        Assert.That((await JoinWith("fifth", code: room.code)).status, Is.EqualTo(409), "a full room takes nobody else");
        Assert.That((await JoinWith("fifth", code: "AAAAAAAA")).status, Is.EqualTo(404));
        Assert.That((await JoinWith("fifth", code: "short")).status, Is.EqualTo(404));
        var stored = await StoredRoom();
        Assert.That(stored.members, Is.EqualTo(new[] { "host", "guest", "third", "fourth" }));
        Assert.That(stored.codeHash, Is.Not.Empty);
        Assert.That((await View("guest")).roomCode, Is.EqualTo(room.code), "members can share the code from the lobby");
        Assert.That((await View("fourth")).seat, Is.EqualTo(3));
        Assert.That((await service.Execute("host", "create", Json.Encode(new RoomRequest
        {
            commandId = Guid.NewGuid().ToString(), seats = 5
        }), "")).status, Is.EqualTo(400));
    }

    [Test]
    public async Task BotsFillSeatsAndPlayRightAfterTheHumanMoves()
    {
        var room = await CreateRoom("host", 3);
        match = room.matchId;
        foreach (int seat in new[] { 1, 2 })
        {
            var add = await Make("host", "addBot");
            add.target = seat;
            Assert.That((await Send("host", add)).status, Is.EqualTo(200));
        }
        Assert.That((await JoinWith("guest", code: room.code)).status, Is.EqualTo(409), "bot seats are not open");
        await Apply("host", "configure");
        await Apply("host", "start");
        var view = await View("host");
        Assert.That(view.players.Count(player => player.bot), Is.EqualTo(2));
        Assert.That(view.phase, Is.EqualTo("Setup"));
        foreach (var (type, space) in view.players[0].setupRemaining.ToList().Zip(new[] { 0, 2, 9, 11, 16 }))
            await Apply("host", "setupPlace", to: space, type: (int)type);
        view = await View("host");
        Assert.That(view.phase, Is.EqualTo("Action"), "both bots set up in the same request as the host's last placement");
        Assert.That(view.activeSeat, Is.EqualTo(0));
        Assert.That(view.board.Count(slot => slot.pieceId != null), Is.EqualTo(15));
        await Apply("host", "draw");
        await Apply("host", "place", to: (await View("host")).choices.First(choice => choice.kind == "place").to);
        view = await View("host");
        Assert.That(view.activeSeat, Is.EqualTo(0), "both bots took their turns and play is back with the host");
        var history = (await StoredRoom()).events.Select(item => long.Parse(item.version)).ToList();
        Assert.That(history, Is.Ordered, "every bot move is recorded in order");
    }
}

public sealed partial class MatchServiceTests
{
    [Test]
    public async Task EmptyFieldsFromUnityJsonMeanAbsent()
    {
        // JsonUtility writes "" for unset strings and 0 for unset ints.
        var created = await service.Execute("host", "create",
            "{\"commandId\":\"" + Guid.NewGuid() + "\",\"inviteToken\":\"\",\"code\":\"\",\"seats\":3}", "");
        Assert.That(created.status, Is.EqualTo(200), created.body);
        var room = Json.Decode<RoomResult>(created.body);
        var joined = await service.Execute("guest", "join",
            "{\"commandId\":\"" + Guid.NewGuid() + "\",\"inviteToken\":\"\",\"code\":\"" + room.code + "\",\"seats\":0}", "");
        Assert.That(joined.status, Is.EqualTo(200), joined.body);
    }
}
