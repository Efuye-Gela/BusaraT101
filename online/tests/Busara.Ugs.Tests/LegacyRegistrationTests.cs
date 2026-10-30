using Busara.Online;
using NUnit.Framework;

namespace Busara.Ugs.Tests;

public sealed partial class MatchServiceTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task LegacyOnlyAndOverlappingIdentitiesRecoverCreationJoinAndCurrentRoom(bool deterministicGuest)
    {
        string join = Json.Encode(new RoomRequest { commandId = Guid.NewGuid().ToString(), inviteToken = invite });
        var originalJoin = await service.Execute("guest", "join", join, "");
        await Apply("host", "configure");
        var beforeRoom = await StoredRoom();
        var legacy = await SaveLegacyRoom();
        legacy.guests["host"] = clock.Now.AddDays(4);
        var current = deterministicGuest
            ? Json.Decode<GuestDocument>((await store.Read(GuestKey("host")))!.Json) : null;
        if (current is not null)
        {
            current.schemaVersion = 1;
            current.joins["current-only-reservation"] = "current-only-fingerprint";
        }
        await StageLegacyGuest("host", current, legacy);
        await StageLegacyGuest("guest", null, legacy);
        var originalDirectory = await store.Read(MatchService.DirectoryId);
        var originalDeterministic = await store.Read(MatchService.GuestKey("host"));
        var guest = await Worker().Execute("host", "guest", "{}", "");
        Assert.That(guest.status, Is.EqualTo(200));
        Assert.That(Json.Decode<GuestView>(guest.body).expiresAt, Is.EqualTo(legacy.guests["host"].ToString("O")));
        var migrated = Json.Decode<GuestDocument>((await store.Read(GuestKey("host")))!.Json);
        Assert.That(migrated.joins.ContainsKey("current-only-reservation"), Is.EqualTo(deterministicGuest));
        Assert.That(migrated.joins.Keys, Is.SupersetOf(legacy.joins.Keys));
        var creation = legacy.creates[beforeRoom.creationKey];
        var retriedCreation = await Worker().Execute("host", "create",
            Json.Encode(new RoomRequest { commandId = creation.commandId }), "");
        Assert.That(retriedCreation.status, Is.EqualTo(200));
        Assert.That(Json.Decode<RoomResult>(retriedCreation.body).inviteUrl, Is.EqualTo("#invite=" + creation.inviteToken));
        var retriedJoin = await Worker().Execute("guest", "join", join, "");
        Assert.That(retriedJoin.status, Is.EqualTo(originalJoin.status));
        Assert.That(retriedJoin.body, Is.EqualTo(originalJoin.body));
        var afterRoom = await StoredRoom();
        Assert.That(Json.Encode(afterRoom.state), Is.EqualTo(Json.Encode(beforeRoom.state)));
        Assert.That(Json.Encode(afterRoom.events), Is.EqualTo(Json.Encode(beforeRoom.events)));
        Assert.That(Json.Encode(afterRoom.receipts), Is.EqualTo(Json.Encode(beforeRoom.receipts)));
        Assert.That(await store.Read(MatchService.DirectoryId), Is.EqualTo(originalDirectory));
        Assert.That(await store.Read(MatchService.GuestKey("host")), Is.EqualTo(originalDeterministic));
    }

    [Test]
    public async Task LaterLegacyWritesCannotOverwritePublishedGuestExpiryOrLedgers()
    {
        var legacy = await PrepareGuestMigration();
        Assert.That((await Worker().Execute("host", "guest", "{}", "")).status, Is.EqualTo(200));
        var published = await store.Read(GuestKey("host"));
        string registry = (await store.Read(MatchService.RegistrationKey("host")))!.Json;
        await store.CreateCandidate(MatchService.GuestKey("host"),
            Json.Encode(new GuestDocument { expiresAt = clock.Now.AddYears(1) }));
        legacy.guests["host"] = clock.Now.AddYears(1);
        legacy.creates.Clear();
        legacy.joins.Clear();
        await store.CreateCandidate(MatchService.DirectoryId, Json.Encode(legacy));
        var retry = await Worker().Execute("host", "register", "{}", "");
        Assert.That(Json.Decode<GuestView>(retry.body).expiresAt,
            Is.EqualTo(Json.Decode<GuestDocument>(published!.Json).expiresAt.ToString("O")));
        Assert.That(await store.Read(GuestKey("host")), Is.EqualTo(published));
        Assert.That((await store.Read(MatchService.RegistrationKey("host")))!.Json, Is.EqualTo(registry));
    }

    [Test]
    public async Task ExpiredLegacyOnlyGuestCannotObtainANewRegistrationExpiry()
    {
        var legacy = new DirectoryDocument();
        legacy.guests["host"] = clock.Now.AddDays(-2);
        await StageLegacyGuest("host", null, legacy);
        var first = await Worker().Execute("host", "register", "{}", "");
        Assert.That(first.status, Is.EqualTo(401));
        var stored = await store.Read(GuestKey("host"));
        Assert.That(Json.Decode<GuestDocument>(stored!.Json).expiresAt, Is.EqualTo(legacy.guests["host"]));
        clock.Now = clock.Now.AddDays(1);
        Assert.That((await Worker().Execute("host", "register", "{}", "")).body, Is.EqualTo(first.body));
        Assert.That(await store.Read(GuestKey("host")), Is.EqualTo(stored));
    }
}
