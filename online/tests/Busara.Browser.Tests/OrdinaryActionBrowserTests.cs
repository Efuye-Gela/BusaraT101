using Busara.Online;
using System.Text.Json;
using NUnit.Framework;

namespace Busara.Browser.Tests;

public sealed partial class TwoBrowserUnityTests
{
    [Test]
    public async Task TradeWeaponAndChainUseRealControlsAndRestorePendingDecisions()
    {
        await CreateJoinReadyAndSetUpAsync(capture: false);
        foreach (var seat in seats) await seat.Page.EvaluateAsync("() => busaraLatency.enable()");
        Assert.That(seats[0].View.ruleset, Is.EqualTo("busara-online-v3"));
        string initialBoard = Board(seats[0].View);
        await ExecuteAsync(0, "trade", from: 0, resource: (int)ResourceType.Fire);
        Assert.That(seats[1].View.decision?.kind, Is.EqualTo("TradeResponse"));
        Assert.That(seats[0].View.choices, Is.Empty);
        await ExecuteAsync(1, "tradeReject");
        await ExecuteAsync(0, "tradeCancel");
        Assert.That(seats[0].View.activeSeat, Is.Zero);
        Assert.That(Board(seats[0].View), Is.EqualTo(initialBoard));

        await ExecuteAsync(0, "trade", from: 2, resource: (int)ResourceType.Fire);
        string decision = seats[1].View.decision.id;
        string version = seats[1].View.version;
        await seats[1].ReloadAsync();
        await seats[1].Page.EvaluateAsync("() => busaraLatency.enable()");
        Assert.That(seats[1].View.decision?.id, Is.EqualTo(decision));
        Assert.That(seats[1].View.version, Is.EqualTo(version));
        await ExecuteAsync(1, "tradeAccept");
        Assert.That(seats[0].View.decision?.kind, Is.EqualTo("TradeSelect"));
        Assert.That(seats[0].View.choices.Count(c => c.kind == "tradeComplete"), Is.EqualTo(3));
        await ExecuteAsync(0, "tradeComplete", to: 13);
        Assert.That(seats[0].View.board.Single(s => s.id == 2).type, Is.EqualTo(ResourceType.Fire));
        Assert.That(seats[0].View.board.Single(s => s.id == 13).type, Is.EqualTo(ResourceType.Water));
        Assert.That(seats[1].View.activeSeat, Is.EqualTo(1));

        await ExecuteAsync(1, "move", 20, 12);
        await ExecuteAsync(0, "move", 2, 3);
        await SubmitSelectionAsync(1, "weapon", [4, 3, 12]);
        Assert.That(seats[0].View.decision?.kind, Is.EqualTo("WeaponDiscard"));
        Assert.That(seats[0].View.board.Where(s => new[] { 4, 3, 12 }.Contains(s.id))
            .All(s => s.pieceId == null), Is.True);
        decision = seats[0].View.decision.id;
        version = seats[0].View.version;
        await server.RestartAsync();
        await seats[0].ReloadAsync();
        await seats[0].Page.EvaluateAsync("() => busaraLatency.enable()");
        Assert.That(seats[0].View.decision?.id, Is.EqualTo(decision));
        Assert.That(seats[0].View.version, Is.EqualTo(version));
        await ExecuteAsync(0, "weaponDiscard", to: 24);
        Assert.That(seats[0].View.board.Single(s => s.id == 24).pieceId, Is.Null);
        Assert.That(seats[0].View.activeSeat, Is.Zero);

        await ExecuteAsync(0, "move", 9, 8);
        await ExecuteAsync(1, "move", 15, 14);
        await ExecuteAsync(0, "move", 11, 10);
        await ExecuteAsync(1, "move", 14, 15);
        await ExecuteAsync(0, "move", 10, 9);
        await ExecuteAsync(1, "move", 15, 14);
        await SubmitSelectionAsync(0, "forgeChain", [0, 8, 9]);
        Assert.That(Self(0).virtues.Select(token => token.type),
            Is.EquivalentTo(new[] { VirtueType.Nature, VirtueType.Economy }));
        Assert.That(seats[0].View.board.Where(s => new[] { 0, 8, 9 }.Contains(s.id))
            .All(s => s.pieceId == null), Is.True);
        foreach (var seat in seats) await seat.AssertNetworkAsync();
        await seats[0].Page.BringToFrontAsync();
        await seats[0].Page.WaitForFunctionAsync(
            "() => busaraLatency.report().samples.some(s => s.start_to_frame_boundary_ms !== null)",
            null, new() { Timeout = 5000 });
        string timing = await seats[0].Page.EvaluateAsync<string>("() => JSON.stringify(busaraLatency.report())");
        using var report = JsonDocument.Parse(timing);
        Assert.That(report.RootElement.GetProperty("retained").GetInt32(), Is.GreaterThanOrEqualTo(5));
        Assert.That(report.RootElement.GetProperty("samples").EnumerateArray()
            .All(sample => sample.GetProperty("outcome").GetString() == "accepted"), Is.True);
        Assert.That(timing, Does.Not.Contain(seats[0].View.matchId));
        foreach (var token in Self(0).virtues) Assert.That(timing, Does.Not.Contain(token.id));
        await File.WriteAllTextAsync(Path.Combine(server.Artifacts, "ordinary-local-latency.json"), timing);
        TestContext.Progress.WriteLine("Local loopback move latency diagnostics: " +
            report.RootElement.GetProperty("summary").GetRawText());
        TestContext.Progress.WriteLine("Real Unity controls: trade decline/accept/exact swap; reload restored offer; " +
            "weapon consumed connected cross-board triple; backend restart restored defender discard; " +
            "three-resource ordered chain awarded two authored virtues.");
    }

    private async Task SubmitSelectionAsync(int actor, string kind, int[] slots)
    {
        var seat = seats[actor];
        long version = UnitySeat.Number(seat.View.version);
        await seat.ClickAsync(UnitySeat.ChoiceId(seat.View.choices.Single(choice => choice.kind == kind)));
        foreach (int slot in slots) await seat.ClickAsync("slot-" + slot);
        if (kind == "forgeChain")
        {
            string directory = Path.Combine(server.Artifacts, TestContext.CurrentContext.Test.Name);
            Directory.CreateDirectory(directory);
            await seat.SaveEvidenceAsync(directory, "ordered-chain-confirmation");
        }
        await seat.ClickAsync("confirm-resource-selection");
        await WaitBothAsync(version + 1);
        Assert.That(seat.SentCommands.Last().slots, Is.EqualTo(slots));
    }
}
