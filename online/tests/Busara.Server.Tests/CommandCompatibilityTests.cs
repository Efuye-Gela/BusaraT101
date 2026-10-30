using System.Net;
using System.Net.Http.Json;
using Busara.Online;
using Busara.Server;
using Xunit;

namespace Busara.Server.Tests;

[Collection("PostgreSQL")]
public sealed class CommandCompatibilityTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Legacy_canonical_command_receipt_remains_replayable_after_slots_are_added()
    {
        using var host = await fixture.GuestAsync();
        var room = await host.CreateAsync();
        string path = $"/api/rooms/{room.matchId}/commands";
        var legacy = new
        {
            commandId = Guid.NewGuid().ToString("D"), expectedVersion = room.version,
            decisionId = (string?)null, kind = "configure", name = "Host", ready = true,
            from = -1, to = -1, resourceType = -1, paymentIds = Array.Empty<string>()
        };
        using var accepted = await host.SendAsync(HttpMethod.Post, path, legacy);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var receipt = await accepted.Content.ReadFromJsonAsync<CommandReceipt>(Wire.Json);
        string fingerprint = await fixture.ScalarAsync<string>(
            "SELECT fingerprint FROM command_receipts WHERE match_id=@match AND command_id=@command",
            ("match", Guid.Parse(room.matchId)), ("command", legacy.commandId));
        Assert.Equal(ServerSettings.Hash(Wire.Encode(legacy)), fingerprint);
        var retry = Wire.Decode<OnlineCommand>(Wire.Encode(legacy));
        Assert.Empty(retry.slots);
        using var replay = await host.SendAsync(HttpMethod.Post, path, retry);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(Wire.Encode(receipt), Wire.Encode(await replay.Content.ReadFromJsonAsync<CommandReceipt>(Wire.Json)));
        retry.slots = [0];
        using var conflict = await host.SendAsync(HttpMethod.Post, path, retry);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task Legacy_saved_room_stays_v1_while_new_rooms_use_v3()
    {
        using var host = await fixture.GuestAsync();
        var room = await host.CreateAsync();
        string path = $"/api/rooms/{room.matchId}";
        Assert.Equal("busara-online-v3", (await host.GetAsync<ClientView>(path)).ruleset);
        await fixture.ExecuteAsync(
            "UPDATE matches SET state=jsonb_set(state,'{ruleset}','\"busara-online-mvp-v1\"') WHERE id=@id",
            ("id", Guid.Parse(room.matchId)));
        Assert.Equal("busara-online-mvp-v1", (await host.GetAsync<ClientView>(path)).ruleset);
    }
}
