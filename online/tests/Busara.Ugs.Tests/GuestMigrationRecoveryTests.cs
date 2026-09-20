using Busara.Online;
using NUnit.Framework;

namespace Busara.Ugs.Tests;

public sealed partial class MatchServiceTests
{
    private async Task<DirectoryDocument> PrepareGuestMigration()
    {
        var stored = (await store.Read(GuestKey("host")))!;
        var current = Json.Decode<GuestDocument>(stored.Json);
        current.schemaVersion = 1;
        var legacy = new DirectoryDocument();
        legacy.guests["host"] = clock.Now.AddDays(3);
        legacy.joins["legacy-reservation"] = "fingerprint";
        await StageLegacyGuest("host", current, legacy);
        return legacy;
    }

    [Test]
    public async Task ConcurrentMigrationUsesExistingLockAndPreservesLedgers()
    {
        var legacy = await PrepareGuestMigration();
        var before = Json.Decode<GuestDocument>((await store.Read(MatchService.GuestKey("host")))!.Json);
        store.RaceNextTwoReads(MatchService.RegistrationKey("host"));
        var replies = await Task.WhenAll(Worker().Execute("host", "guest", "{}", ""),
            Worker().Execute("host", "register", "{}", ""));
        Assert.That(replies.Select(reply => reply.status), Is.All.EqualTo(200));
        Assert.That(replies[0].body, Is.EqualTo(replies[1].body));
        Assert.That(store.Conflicts, Is.GreaterThan(0));
        var migrated = Json.Decode<GuestDocument>((await store.Read(GuestKey("host")))!.Json);
        Assert.That(migrated.expiresAt, Is.EqualTo(legacy.guests["host"]));
        Assert.That(Json.Encode(migrated.creates), Is.EqualTo(Json.Encode(before.creates)));
        Assert.That(migrated.joins["legacy-reservation"], Is.EqualTo("fingerprint"));
    }

    [Test]
    public async Task MigrationLostAckRecoversOnNewWorkerWithoutRenewingExpiry()
    {
        var legacy = await PrepareGuestMigration();
        store.LoseNextAckFor = MatchService.RegistrationKey("host");
        Assert.ThrowsAsync<IOException>(async () => await Worker().Execute("host", "register", "{}", ""));
        var afterCommit = await store.Read(GuestKey("host"));
        int reads = store.ReadCount(MatchService.DirectoryId);
        clock.Now = clock.Now.AddDays(1);
        var retry = await Worker().Execute("host", "register", "{}", "");
        Assert.That(retry.status, Is.EqualTo(200));
        Assert.That(Json.Decode<GuestView>(retry.body).expiresAt, Is.EqualTo(legacy.guests["host"].ToString("O")));
        Assert.That(await store.Read(GuestKey("host")), Is.EqualTo(afterCommit));
        Assert.That(store.ReadCount(MatchService.DirectoryId), Is.EqualTo(reads));
    }

    [Test]
    public async Task MigrationCapacityFailsBeforeChangingOriginalShardOrRoom()
    {
        var legacy = await PrepareGuestMigration();
        legacy.joins[new string('x', MatchService.MaximumDocumentBytes)] = "fingerprint";
        await store.CreateCandidate(MatchService.DirectoryId, Json.Encode(legacy));
        var beforeGuest = await store.Read(MatchService.GuestKey("host"));
        var beforeShard = await store.Read(MatchService.RegistrationKey("host"));
        var beforeRoom = await store.Read("busara_" + match);
        var reply = await Worker().Execute("host", "register", "{}", "");
        Assert.That(reply.status, Is.EqualTo(507));
        Assert.That(reply.body, Does.Contain("storage_capacity_reached"));
        Assert.That(await store.Read(MatchService.GuestKey("host")), Is.EqualTo(beforeGuest));
        Assert.That(await store.Read(MatchService.RegistrationKey("host")), Is.EqualTo(beforeShard));
        Assert.That(await store.Read("busara_" + match), Is.EqualTo(beforeRoom));
    }
}
