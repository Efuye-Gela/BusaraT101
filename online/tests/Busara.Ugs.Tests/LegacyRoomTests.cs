using System.Text.Json.Nodes;
using Busara.Online;
using NUnit.Framework;

namespace Busara.Ugs.Tests;

public sealed partial class MatchServiceTests
{
    private async Task<DirectoryDocument> SaveLegacyRoom()
    {
        var stored = (await store.Read("busara_" + match))!;
        var room = JsonNode.Parse(stored.Json)!;
        room["schemaVersion"] = 1;
        room.AsObject().Remove("published");
        room.AsObject().Remove("receiptOrder");
        await store.CompareExchange("busara_" + match, room.ToJsonString(), stored.WriteLock);
        var legacy = new DirectoryDocument();
        foreach (string actor in new[] { "host", "guest", "outsider" })
        {
            var guest = Json.Decode<GuestDocument>((await store.Read(GuestKey(actor)))!.Json);
            legacy.guests[actor] = guest.expiresAt;
            foreach (var (key, value) in guest.creates) legacy.creates[key] = value;
            foreach (var (key, value) in guest.joins) legacy.joins[key] = value;
        }
        await store.CreateCandidate(MatchService.DirectoryId, Json.Encode(legacy));
        return legacy;
    }

    [Test]
    public async Task LegacyRoomPreservesMembershipInviteReceiptsHistoryAndCreationRetry()
    {
        await Join();
        await Apply("host", "configure");
        var before = await StoredRoom();
        var legacy = await SaveLegacyRoom();
        var response = await Worker().Execute("host", "view", "{}", match);
        Assert.That(response.status, Is.EqualTo(200));
        var after = await StoredRoom();
        Assert.That(after.schemaVersion, Is.EqualTo(2));
        Assert.That(after.published, Is.True);
        Assert.That(Json.Encode(after.state), Is.EqualTo(Json.Encode(before.state)));
        Assert.That(after.members, Is.EqualTo(before.members));
        Assert.That(after.inviteHash, Is.EqualTo(before.inviteHash));
        Assert.That(after.inviteExpiresAt, Is.EqualTo(before.inviteExpiresAt));
        Assert.That(Json.Encode(after.events), Is.EqualTo(Json.Encode(before.events)));
        Assert.That(Json.Encode(after.receipts), Is.EqualTo(Json.Encode(before.receipts)));
        int reads = store.ReadCount(MatchService.DirectoryId);
        Assert.That((await Worker().Execute("guest", "view", "{}", match)).status, Is.EqualTo(200));
        var creation = legacy.creates[before.creationKey];
        var retry = await Worker().Execute("host", "create",
            Json.Encode(new RoomRequest { commandId = creation.commandId }), "");
        Assert.That(Json.Decode<RoomResult>(retry.body).inviteUrl, Is.EqualTo("#invite=" + creation.inviteToken));
        Assert.That(store.ReadCount(MatchService.DirectoryId), Is.EqualTo(reads));
        Assert.That((await Worker().Execute("outsider", "view", "{}", match)).status, Is.EqualTo(404));
    }

    [Test]
    public async Task LegacyOrphanIsNeverPublishedEvenWithARealCreationKey()
    {
        await SaveLegacyRoom();
        var room = await StoredRoom();
        string orphan = Guid.NewGuid().ToString();
        room.state.id = orphan;
        await store.CreateCandidate("busara_" + orphan, Json.Encode(room));
        var before = await store.Read("busara_" + orphan);
        Assert.That((await Worker().Execute("host", "view", "{}", orphan)).status, Is.EqualTo(404));
        Assert.That(await store.Read("busara_" + orphan), Is.EqualTo(before));
    }
}
