using Busara.Online;
using NUnit.Framework;

namespace Busara.Ugs.Tests;

public sealed partial class MatchServiceTests
{
    [Test]
    public async Task ConcurrentFirstRegistrationConvergesOnOneImmutableCandidate()
    {
        const string actor = "fresh";
        store.RaceNextTwoReads(MatchService.RegistrationKey(actor));
        var replies = await Task.WhenAll(Worker().Execute(actor, "register", "{}", ""),
            Worker().Execute(actor, "register", "{}", ""));
        Assert.That(replies.Select(reply => reply.status), Is.All.EqualTo(200));
        Assert.That(replies[0].body, Is.EqualTo(replies[1].body));
        Assert.That(store.Conflicts, Is.GreaterThan(0));
        Assert.That(await store.Read(MatchService.GuestKey(actor)), Is.Null, "Never initialize deterministic guest IDs.");
        var candidate = Json.Decode<GuestDocument>((await store.Read(GuestKey(actor)))!.Json);
        Assert.That(candidate.schemaVersion, Is.EqualTo(2));
        Assert.That(candidate.expiresAt.ToString("O"), Is.EqualTo(Json.Decode<GuestView>(replies[0].body).expiresAt));
    }

    [Test]
    public async Task DelayedRegistrationLoserCannotOverwriteWinnersAdvancedLedger()
    {
        const string actor = "delayed";
        var pause = store.PauseNextWrite(MatchService.RegistrationKey(actor));
        var loser = Worker().Execute(actor, "register", "{}", "");
        await pause.Reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            clock.Now = clock.Now.AddHours(1);
            var winner = await Worker().Execute(actor, "register", "{}", "");
            string request = Json.Encode(new RoomRequest { commandId = Guid.NewGuid().ToString() });
            var created = await Worker().Execute(actor, "create", request, "");
            Assert.That(created.status, Is.EqualTo(200));
            var advanced = await store.Read(GuestKey(actor));
            var registration = await store.Read(MatchService.RegistrationKey(actor));
            pause.Resume.SetResult();
            Assert.That((await loser).body, Is.EqualTo(winner.body));
            Assert.That(await store.Read(GuestKey(actor)), Is.EqualTo(advanced));
            Assert.That(await store.Read(MatchService.RegistrationKey(actor)), Is.EqualTo(registration));
            Assert.That((await Worker().Execute(actor, "create", request, "")).body, Is.EqualTo(created.body));
        }
        finally { pause.Resume.TrySetResult(); }
    }

    [Test]
    public async Task LostRegistrationPublicationAckRecoversExactExpiryOnNewWorker()
    {
        const string actor = "lost-ack";
        store.LoseNextAckFor = MatchService.RegistrationKey(actor);
        Assert.ThrowsAsync<IOException>(async () => await Worker().Execute(actor, "register", "{}", ""));
        var registered = await store.Read(MatchService.RegistrationKey(actor));
        var candidate = await store.Read(GuestKey(actor));
        clock.Now = clock.Now.AddDays(5);
        var retry = await Worker().Execute(actor, "register", "{}", "");
        Assert.That(retry.status, Is.EqualTo(200));
        Assert.That(Json.Decode<GuestView>(retry.body).expiresAt,
            Is.EqualTo(Json.Decode<GuestDocument>(candidate!.Json).expiresAt.ToString("O")));
        Assert.That(await store.Read(GuestKey(actor)), Is.EqualTo(candidate));
        Assert.That(await store.Read(MatchService.RegistrationKey(actor)), Is.EqualTo(registered));
    }

    [Test]
    public async Task UncertainCandidateWriteIsAbandonedNotRepublishedOrOverwritten()
    {
        const string actor = "candidate-ack";
        string[] before = store.Ids;
        store.LoseNextCandidateAck = true;
        Assert.ThrowsAsync<IOException>(async () => await Worker().Execute(actor, "register", "{}", ""));
        string orphan = store.Ids.Except(before).Single();
        var original = await store.Read(orphan);
        Assert.That((await Worker().Execute(actor, "register", "{}", "")).status, Is.EqualTo(200));
        Assert.That(GuestKey(actor), Is.Not.EqualTo(orphan));
        Assert.That(await store.Read(orphan), Is.EqualTo(original));
    }

    [Test]
    public async Task MoveHotPathReadsRegistrationAndRoomWithoutGuestLedgerOrLegacyDirectory()
    {
        var command = await Make("host", "configure");
        command.name = "Host";
        string guestId = GuestKey("host");
        int guestReads = store.ReadCount(guestId), legacyReads = store.ReadCount(MatchService.DirectoryId);
        int registryReads = store.ReadCount(MatchService.RegistrationKey("host"));
        int roomReads = store.ReadCount("busara_" + match);
        Assert.That((await Send("host", command)).status, Is.EqualTo(200));
        Assert.That(store.ReadCount(guestId), Is.EqualTo(guestReads));
        Assert.That(store.ReadCount(MatchService.DirectoryId), Is.EqualTo(legacyReads));
        Assert.That(store.ReadCount(MatchService.RegistrationKey("host")), Is.EqualTo(registryReads + 1));
        Assert.That(store.ReadCount("busara_" + match), Is.EqualTo(roomReads + 1));
        await View("host");
        Assert.That(store.ReadCount(guestId), Is.EqualTo(guestReads));
        Assert.That(store.ReadCount(MatchService.DirectoryId), Is.EqualTo(legacyReads));
        Assert.That(store.ReadCount(MatchService.RegistrationKey("host")), Is.EqualTo(registryReads + 2));
    }

    [Test]
    public async Task FullRegistrationShardFailsBeforeInitializingAnyCandidate()
    {
        string key = MatchService.RegistrationKey("full");
        var stored = (await store.Read(key))!;
        var shard = Json.Decode<RegistrationShard>(stored.Json);
        shard.registrations[new string('x', MatchService.MaximumDocumentBytes)] = new GuestRegistration();
        await store.CompareExchange(key, Json.Encode(shard), stored.WriteLock);
        var before = await store.Read(key);
        string[] ids = store.Ids;
        var reply = await Worker().Execute("full", "register", "{}", "");
        Assert.That(reply.status, Is.EqualTo(507));
        Assert.That(await store.Read(key), Is.EqualTo(before));
        Assert.That(store.Ids, Is.EquivalentTo(ids));
    }

    [Test]
    public void ProvisioningContractHasExactly64StableIndependentKeys()
    {
        Assert.That(MatchService.RegistrationShardCount, Is.EqualTo(64));
        Assert.That(MatchService.RegistrationShardKey(0), Is.EqualTo("busara_registration_v1_00"));
        Assert.That(MatchService.RegistrationShardKey(63), Is.EqualTo("busara_registration_v1_63"));
        Assert.Throws<ArgumentOutOfRangeException>(() => MatchService.RegistrationShardKey(64));
        Assert.That(Json.Encode(new RegistrationShard()), Is.EqualTo("{\"schemaVersion\":1,\"registrations\":{}}"));
    }
}
