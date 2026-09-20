using Busara.Online;
using NUnit.Framework;

namespace Busara.Ugs.Tests;

public sealed partial class MatchServiceTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void MergeMissingOrExistingGuestRetainsOldExpiryAndAllReservations(bool existing)
    {
        var legacy = new DirectoryDocument();
        legacy.guests["host"] = clock.Now.AddDays(5);
        legacy.creates["old-create"] = new Creation { actor = "host", matchId = "old-room" };
        legacy.creates["other-create"] = new Creation { actor = "other", matchId = "private-room" };
        legacy.joins["irreversible-key"] = "join-fingerprint";
        var current = existing ? new GuestDocument { expiresAt = clock.Now.AddDays(30) } : null;
        if (current is not null)
        {
            current.creates["new-create"] = new Creation { actor = "host", matchId = "new-room" };
            current.joins["new-join"] = "new-fingerprint";
        }
        string before = Json.Encode(current);
        var merged = GuestMigration.Merge("host", current, legacy)!;
        Assert.That(merged.schemaVersion, Is.EqualTo(2));
        Assert.That(merged.expiresAt, Is.EqualTo(legacy.guests["host"]));
        Assert.That(merged.creates.ContainsKey("old-create"), Is.True);
        Assert.That(merged.creates.ContainsKey("other-create"), Is.False);
        Assert.That(merged.joins["irreversible-key"], Is.EqualTo("join-fingerprint"));
        Assert.That(merged.creates.ContainsKey("new-create"), Is.EqualTo(existing));
        Assert.That(merged.joins.ContainsKey("new-join"), Is.EqualTo(existing));
        Assert.That(Json.Encode(current), Is.EqualTo(before), "Merge never mutates its source.");
    }

    [Test]
    public void MergeNeverExtendsEarlierShardedExpiry()
    {
        var current = new GuestDocument { expiresAt = clock.Now.AddDays(-1) };
        var legacy = new DirectoryDocument();
        legacy.guests["host"] = clock.Now.AddDays(30);
        Assert.That(GuestMigration.Merge("host", current, legacy)!.expiresAt, Is.EqualTo(current.expiresAt));
        Assert.That(GuestMigration.Merge("unknown", null, legacy), Is.Null);
    }

    [TestCase("create")]
    [TestCase("join")]
    [TestCase("cross")]
    public async Task ConflictingMigrationFailsExplicitlyWithoutOverwritingEitherLedger(string conflict)
    {
        var stored = (await store.Read(GuestKey("host")))!;
        var current = Json.Decode<GuestDocument>(stored.Json);
        current.schemaVersion = 1;
        var legacy = new DirectoryDocument();
        legacy.guests["host"] = current.expiresAt;
        if (conflict == "create")
        {
            current.creates["conflict"] = new Creation { actor = "host", matchId = "one" };
            legacy.creates["conflict"] = new Creation { actor = "host", matchId = "two" };
        }
        else
        {
            legacy.joins["conflict"] = "original";
            if (conflict == "join") current.joins["conflict"] = "different";
            else current.creates["conflict"] = new Creation { actor = "host" };
        }
        await StageLegacyGuest("host", current, legacy);
        var before = await store.Read(MatchService.GuestKey("host"));
        var shardBefore = await store.Read(MatchService.RegistrationKey("host"));
        var reply = await Worker().Execute("host", "register", "{}", "");
        Assert.That(reply.status, Is.EqualTo(409));
        Assert.That(reply.body, Does.Contain("guest_migration_conflict"));
        Assert.That(await store.Read(MatchService.GuestKey("host")), Is.EqualTo(before));
        Assert.That(await store.Read(MatchService.RegistrationKey("host")), Is.EqualTo(shardBefore));
        Assert.That((await store.Read(MatchService.DirectoryId))!.Json, Is.EqualTo(Json.Encode(legacy)));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ExistingShardImportsLegacyOnlyOnceWithoutRenewingExpiredIdentity(bool expired)
    {
        var stored = (await store.Read(GuestKey("host")))!;
        var current = Json.Decode<GuestDocument>(stored.Json);
        current.schemaVersion = 1;
        var legacy = new DirectoryDocument();
        legacy.guests["host"] = clock.Now.AddDays(expired ? -1 : 3);
        legacy.joins["legacy-reservation"] = "fingerprint";
        await StageLegacyGuest("host", current, legacy);
        var first = await Worker().Execute("host", "guest", "{}", "");
        Assert.That(first.status, Is.EqualTo(expired ? 401 : 200));
        var migrated = Json.Decode<GuestDocument>((await store.Read(GuestKey("host")))!.Json);
        Assert.That(migrated.expiresAt, Is.EqualTo(legacy.guests["host"]));
        Assert.That(migrated.schemaVersion, Is.EqualTo(2));
        Assert.That(migrated.creates.Keys, Is.EquivalentTo(current.creates.Keys));
        Assert.That(migrated.joins["legacy-reservation"], Is.EqualTo("fingerprint"));
        int reads = store.ReadCount(MatchService.DirectoryId);
        clock.Now = clock.Now.AddDays(1);
        Assert.That((await Worker().Execute("host", "register", "{}", "")).body, Is.EqualTo(first.body));
        Assert.That(store.ReadCount(MatchService.DirectoryId), Is.EqualTo(reads));
    }

    private async Task StageLegacyGuest(string actor, GuestDocument? current, DirectoryDocument legacy)
    {
        if (current is not null)
            await store.CreateCandidate(MatchService.GuestKey(actor), Json.Encode(current));
        await store.CreateCandidate(MatchService.DirectoryId, Json.Encode(legacy));
        var stored = (await store.Read(MatchService.RegistrationKey(actor)))!;
        var shard = Json.Decode<RegistrationShard>(stored.Json);
        shard.registrations.Remove(actor);
        await store.CompareExchange(MatchService.RegistrationKey(actor), Json.Encode(shard), stored.WriteLock);
    }
}
