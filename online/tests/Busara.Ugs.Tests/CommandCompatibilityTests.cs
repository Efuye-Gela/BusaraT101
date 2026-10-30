using Busara.Online;
using NUnit.Framework;

namespace Busara.Ugs.Tests;

public sealed partial class MatchServiceTests
{
    [TestCase("command", "commandWithView")]
    [TestCase("commandWithView", "command")]
    public async Task CrossFormatRetriesPreserveReceiptAndReturnCurrentActorProjection(string first, string retry)
    {
        await Join();
        var command = await Make("host", "configure");
        command.name = "Host"; command.ready = true;
        string payload = Json.Encode(command);
        var initial = await service.Execute("host", first, payload, match);
        var receipt = first == "command" ? Json.Decode<CommandReceipt>(initial.body)
            : Json.Decode<CommandResult>(initial.body).receipt;
        await Apply("guest", "configure");
        var before = await StoredRoom();
        var repeated = await Worker().Execute("host", retry, payload, match);
        Assert.That(repeated.status, Is.EqualTo(initial.status));
        if (retry == "command")
            Assert.That(repeated.body, Is.EqualTo(Json.Encode(receipt)));
        else
        {
            var result = Json.Decode<CommandResult>(repeated.body);
            Assert.That(Json.Encode(result.receipt), Is.EqualTo(Json.Encode(receipt)));
            Assert.That(Json.Encode(result.view), Is.EqualTo(Json.Encode(await View("host"))));
            Assert.That(result.view.version, Is.Not.EqualTo(result.receipt.version));
        }
        Assert.That(before.receipts[command.commandId].reply.body, Is.EqualTo(Json.Encode(receipt)));
        Assert.That(Json.Encode(await StoredRoom()), Is.EqualTo(Json.Encode(before)));
    }

    [TestCase("command")]
    [TestCase("commandWithView")]
    public async Task OldWrappedReceiptsAreNormalizedWithoutReplayingOrLeakingTheirProjection(string operation)
    {
        await Join();
        var command = await Make("host", "configure");
        command.name = "Host"; command.ready = true;
        var original = await Send("host", command);
        var stored = (await store.Read("busara_" + match))!;
        var room = Json.Decode<RoomDocument>(stored.Json);
        room.receipts[command.commandId].reply.body = Json.Encode(new CommandResult
        {
            receipt = Json.Decode<CommandReceipt>(original.body),
            // Deliberately wrong old projection: response must recompute authorization.
            view = await View("guest")
        });
        await store.CompareExchange("busara_" + match, Json.Encode(room), stored.WriteLock);
        await Apply("guest", "configure");
        var reply = await Worker().Execute("host", operation, Json.Encode(command), match);
        if (operation == "command") Assert.That(reply.body, Is.EqualTo(original.body));
        else
        {
            var result = Json.Decode<CommandResult>(reply.body);
            Assert.That(Json.Encode(result.receipt), Is.EqualTo(original.body));
            Assert.That(Json.Encode(result.view), Is.EqualTo(Json.Encode(await View("host"))));
        }
        Assert.That((await StoredRoom()).receipts[command.commandId].reply.body, Is.EqualTo(original.body));
        Assert.That((await StoredRoom()).events, Has.Count.EqualTo(4));
    }

    [Test]
    public async Task RejectedReceiptAlsoSurvivesCrossFormatRetryAndOtherSeatProgress()
    {
        await Join();
        var command = await Make("guest", "start");
        var first = await Send("guest", command);
        Assert.That(first.status, Is.EqualTo(409));
        await Apply("host", "configure");
        var repeated = await Worker().Execute("guest", "commandWithView", Json.Encode(command), match);
        Assert.That(repeated.status, Is.EqualTo(409));
        var result = Json.Decode<CommandResult>(repeated.body);
        Assert.That(Json.Encode(result.receipt), Is.EqualTo(first.body));
        Assert.That(Json.Encode(result.view), Is.EqualTo(Json.Encode(await View("guest"))));
    }
}
