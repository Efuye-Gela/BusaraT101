using System.Text.Json;
using Busara.Online;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Busara.Browser.Tests;

[TestFixture]
[NonParallelizable]
public sealed class TwoBrowserUnityTests
{
    private OwnedServer server = null!;
    private IPlaywright playwright = null!;
    private IBrowser browser = null!;
    private UnitySeat[] seats = [];
    private readonly List<string> milestones = [];

    [OneTimeSetUp]
    public async Task StartRealBackendAndChromium()
    {
        server = new OwnedServer();
        try
        {
            await server.StartAsync(migrate: true);
            playwright = await Playwright.CreateAsync();
            browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = Environment.GetEnvironmentVariable("BUSARA_BROWSER_HEADED") != "1",
                Args = ["--enable-webgl", "--use-angle=swiftshader", "--enable-unsafe-swiftshader"]
            });
        }
        catch
        {
            if (playwright != null) playwright.Dispose();
            await server.DisposeAsync();
            throw;
        }
    }

    [SetUp]
    public async Task NewIndependentBrowserContexts()
    {
        milestones.Clear();
        seats = [await UnitySeat.OpenAsync(browser, server.Origin, 0),
            await UnitySeat.OpenAsync(browser, server.Origin, 1)];
    }

    [Test]
    [Order(1)]
    public async Task EarnedRetractionSurvivesReloadRestartAndLostAckThenNormalPlayWins()
    {
        await CreateJoinReadyAndSetUpAsync();
        await EarnRetractionPaymentAsync();
        var beforeBoard = Board(seats[0].View);
        string[] exactPayment = Self(1).virtues.Select(t => t.id).ToArray();
        Assert.That(Self(1).virtues.Select(t => t.type),
            Is.EquivalentTo(new[] { VirtueType.Art, VirtueType.Security }));
        await ExecuteAsync(0, "move", 24, 25);
        var pending = seats[1].View.decision;
        Assert.That(pending, Is.Not.Null);
        Assert.That(pending.kind, Is.EqualTo("Retraction"));
        Assert.That(pending.owner, Is.EqualTo(1));
        Assert.That(seats[1].View.activeSeat, Is.Zero, "This must be an off-turn owner decision.");
        Assert.That(pending.paymentCost, Is.EqualTo(2));
        Assert.That(pending.paymentOptions.Select(t => t.id).Order().SequenceEqual(exactPayment.Order()), Is.True,
            "The owner must be offered precisely the two earned token identities.");
        Assert.That(seats[0].View.decision, Is.Null, "The non-owner must not receive the private decision.");
        string decisionId = pending.id;
        string prompt = pending.prompt;
        string pendingVersion = seats[1].View.version;
        string matchId = seats[1].View.matchId;
        string pendingProjection = JsonSerializer.Serialize(seats[1].View, UnitySeat.Json);
        var cookiesBefore = await GuestCookieValuesAsync();
        await CaptureAsync("earned-off-turn-decision");

        await seats[1].ReloadAsync();
        AssertResumedDecision(decisionId, prompt, pendingVersion, matchId);
        Assert.That(JsonSerializer.Serialize(seats[1].View, UnitySeat.Json) == pendingProjection, Is.True,
            "Reload must restore the complete authorized projection, including payment eligibility.");
        milestones.Add("reacting-browser-reloaded: same seat/match/version/decision/prompt");

        await server.StopAsync();
        await UnitySeat.WaitAsync(async () =>
        {
            var ui = await seats[1].UiAsync();
            return ui?.connection?.Contains("Reconnecting", StringComparison.OrdinalIgnoreCase) == true;
        }, "The actual Unity client did not surface backend disconnection.");
        await server.StartAsync();
        // Reload both real clients, preserving cookies, to force authenticated durable rejoin after process death.
        await Task.WhenAll(seats.Select(seat => seat.ReloadAsync()));
        AssertResumedDecision(decisionId, prompt, pendingVersion, matchId);
        Assert.That(JsonSerializer.Serialize(seats[1].View, UnitySeat.Json) == pendingProjection, Is.True,
            "Backend restart must restore the complete authorized projection and exact payment options.");
        Assert.That((await GuestCookieValuesAsync()).SequenceEqual(cookiesBefore), Is.True,
            "Reload/restart must not rotate or replace the fixed-lifetime guest identities.");
        Assert.That(seats[0].View.seat, Is.Zero);
        Assert.That(seats[0].View.matchId, Is.EqualTo(matchId));
        Assert.That(seats[0].View.decision, Is.Null);
        milestones.Add("owned-backend-process-restarted: same PostgreSQL/cookies/pending decision");
        await CaptureAsync("decision-after-backend-restart");

        var lostAck = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool dropped = false;
        await seats[1].Page.RouteAsync("**/api/rooms/*/commands", async route =>
        {
            OnlineCommand command = JsonSerializer.Deserialize<OnlineCommand>(route.Request.PostData!, UnitySeat.Json)!;
            if (command.kind != "use" || dropped)
            {
                await route.ContinueAsync();
                return;
            }
            dropped = true;
            try
            {
                // Deliver the actual Unity click's request and commit it, then lose only its HTTP ACK.
                var receiptResponse = await route.FetchAsync(new RouteFetchOptions { MaxRedirects = 0 });
                if (!receiptResponse.Ok) throw new InvalidOperationException();
                var receipt = JsonSerializer.Deserialize<CommandReceipt>(
                    await receiptResponse.TextAsync(), UnitySeat.Json)!;
                await receiptResponse.DisposeAsync();
                await route.AbortAsync("connectionreset");
                lostAck.TrySetResult(UnitySeat.Number(receipt.version));
            }
            catch
            {
                lostAck.TrySetException(new AssertionException("Could not simulate a committed-but-lost Unity command ACK."));
                await route.AbortAsync();
            }
        });
        var use = seats[1].View.choices.Single(c => c.kind == "use");
        await seats[1].ClickAsync(UnitySeat.ChoiceId(use));
        for (int index = 0; index < pending.paymentOptions.Count; index++)
            if (exactPayment.Contains(pending.paymentOptions[index].id))
                await seats[1].ClickAsync("payment-" + index);
        await seats[1].ClickAsync("confirm-payment");
        long useVersion = await lostAck.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.That(useVersion, Is.EqualTo(UnitySeat.Number(pendingVersion) + 1));
        await seats[1].ClickAsync("retry-pending");
        await WaitBothAsync(useVersion);
        await UnitySeat.WaitAsync(() => Task.FromResult(seats[1].SentCommands.Count(c => c.kind == "use") >= 2),
            "Unity did not retry the identical persisted outbox command.");
        var attempts = seats[1].SentCommands.Where(c => c.kind == "use").ToArray();
        Assert.That(attempts.Select(c => c.commandId).Distinct().Count(), Is.EqualTo(1));
        Assert.That(attempts.Select(c => JsonSerializer.Serialize(c, UnitySeat.Json)).Distinct().Count(), Is.EqualTo(1),
            "Lost ACK retry must preserve the complete command, expected version, decision and exact payment.");
        Assert.That(attempts[0].decisionId, Is.EqualTo(decisionId));
        Assert.That(attempts[0].paymentIds.Order().SequenceEqual(exactPayment.Order()), Is.True,
            "The submitted payment must contain the exact selected earned tokens.");
        await seats[1].WaitViewAsync(v => v.decision?.kind == "RetractionNotice",
            "Use did not render its durable Retraction acknowledgement.");
        await seats[1].Page.UnrouteAsync("**/api/rooms/*/commands");
        Assert.That(Board(seats[0].View), Is.EqualTo(beforeBoard), "Use must restore the whole public board.");
        Assert.That(Self(1).virtues, Is.Empty, "Exactly the two normally earned payment tokens must remain spent.");
        Assert.That(Self(1).revealed, Is.True);
        Assert.That(seats[0].View.players[1].kingdom, Is.EqualTo(Definitions.KingdomName(Definitions.Mask)));
        Assert.That(seats[1].View.version, Is.EqualTo(useVersion.ToString()));
        milestones.Add("Use: exact earned payment/rollback/reveal/new revision; lost ACK identical outbox retry");
        await CaptureAsync("use-after-lost-ack");

        await ExecuteAsync(1, "ack");
        Assert.That(seats[1].View.activeSeat, Is.EqualTo(1), "Retraction grants no replacement action to seat zero.");
        Assert.That(seats[1].View.phase, Is.EqualTo("Action"));
        await ContinueNormalPlayToVictoryAsync();
        await CaptureAsync("normal-play-victory");
        foreach (var seat in seats) await seat.AssertNetworkAsync();
    }

    [Test]
    [Order(2)]
    public async Task PassKeepsEarnedPaymentKingdomHiddenAndOpposingActionCommitted()
    {
        await CreateJoinReadyAndSetUpAsync();
        await EarnRetractionPaymentAsync();
        string[] exactPayment = Self(1).virtues.Select(t => t.id).ToArray();
        await ExecuteAsync(0, "move", 24, 25);
        Assert.That(seats[1].View.decision?.kind, Is.EqualTo("Retraction"));
        string board = Board(seats[1].View);
        long pendingVersion = UnitySeat.Number(seats[1].View.version);
        await ExecuteAsync(1, "pass");
        Assert.That(Board(seats[1].View), Is.EqualTo(board));
        Assert.That(Self(1).virtues.Select(t => t.id).Order().SequenceEqual(exactPayment.Order()), Is.True,
            "Pass must preserve the exact earned token identities.");
        Assert.That(Self(1).revealed, Is.False);
        Assert.That(seats[0].View.players[1].kingdom, Is.Null);
        Assert.That(seats[1].View.activeSeat, Is.EqualTo(1));
        Assert.That(UnitySeat.Number(seats[1].View.version), Is.EqualTo(pendingVersion + 1));
        Assert.That(seats[1].View.decision, Is.Null);
        milestones.Add("Pass: committed board retained; exact payment unchanged; no reveal; next seat");
        await CaptureAsync("pass-without-payment-or-reveal");
        foreach (var seat in seats) await seat.AssertNetworkAsync();
    }

    private async Task CreateJoinReadyAndSetUpAsync()
    {
        await seats[0].NavigateAsync();
        await seats[0].ClickAsync("create-room");
        await seats[0].WaitViewAsync(v => v.phase == "Lobby" && v.seat == 0,
            "Unity Create private room did not enter the host lobby.");
        string invitation = await seats[0].CopyInvitationAsync();
        await seats[1].NavigateAsync(invitation);
        invitation = "";
        await seats[1].ClickAsync("join-room");
        await seats[1].WaitViewAsync(v => v.phase == "Lobby" && v.seat == 1,
            "Unity Accept private invitation did not claim only the empty seat.");
        await WaitBothAsync(UnitySeat.Number(seats[1].View.version));
        await seats[0].AssertSecureDistinctCookieAsync(seats[1]);
        Assert.That(seats[0].View.matchId, Is.EqualTo(seats[1].View.matchId));
        Assert.That(seats[0].View.players.All(p => !p.ready), Is.True);
        long before = UnitySeat.Number(seats[0].View.version);
        await seats[0].ClickAsync("configure");
        await WaitBothAsync(before + 1);
        Assert.That(seats[0].View.players[0].ready, Is.True);
        Assert.That(seats[0].View.players[1].ready, Is.False,
            "Host readiness must not configure the other seat.");
        before = UnitySeat.Number(seats[1].View.version);
        await seats[1].ClickAsync("configure");
        await WaitBothAsync(before + 1);
        Assert.That(seats[0].View.players.All(p => p.ready), Is.True);
        before = UnitySeat.Number(seats[0].View.version);
        await seats[0].ClickAsync("start");
        await WaitBothAsync(before + 1);
        var kingdomNames = new[] { Definitions.Egolica, Definitions.Mask, Definitions.Knowledge }
            .Select(Definitions.KingdomName).ToArray();
        Assert.That(new[] { Self(0).kingdom, Self(1).kingdom }.Distinct().Count(), Is.EqualTo(2));
        Assert.That(Self(0).kingdom, Is.AnyOf(kingdomNames));
        Assert.That(Self(1).kingdom, Is.AnyOf(kingdomNames));
        Assert.That(Self(0).kingdom, Is.EqualTo(Definitions.KingdomName(Definitions.Egolica)),
            "The Testing-only deterministic random injection is not active; never force a production deal.");
        Assert.That(Self(1).kingdom, Is.EqualTo(Definitions.KingdomName(Definitions.Mask)));
        Assert.That(seats[0].View.players[1].kingdom, Is.Null);
        Assert.That(seats[1].View.players[0].kingdom, Is.Null);
        Assert.That(Self(0).virtues, Is.Empty);
        Assert.That(Self(1).virtues, Is.Empty);
        Assert.That(Self(0).setupRemaining, Is.EqualTo(Definitions.Setup(0)));
        await CaptureAsync("distinct-private-kingdoms");
        foreach (var (slot, type) in new[] { (0, ResourceType.Earth), (2, ResourceType.Water),
            (9, ResourceType.Water), (11, ResourceType.Air), (24, ResourceType.Air) })
            await ExecuteAsync(0, "setupPlace", to: slot, resource: (int)type);
        foreach (var (slot, type) in new[] { (4, ResourceType.Fire), (13, ResourceType.Fire),
            (20, ResourceType.Fire), (6, ResourceType.Water), (15, ResourceType.Earth) })
            await ExecuteAsync(1, "setupPlace", to: slot, resource: (int)type);
        Assert.That(seats[0].View.phase, Is.EqualTo("Action"));
        Assert.That(seats[0].View.board.Count(slot => slot.pieceId != null), Is.EqualTo(10));
        milestones.Add("two isolated browser guests; actual Unity create/copy/join/own-readiness/start/native setup");
    }

    private async Task EarnRetractionPaymentAsync()
    {
        await ExecuteAsync(0, "move", 24, 25);
        await ExecuteAsync(1, "move", 6, 5);
        await ExecuteAsync(0, "move", 25, 24);
        await ExecuteAsync(1, "forge", 4, 5);
        await ExecuteAsync(0, "move", 24, 25);
        await ExecuteAsync(1, "move", 15, 14);
        await ExecuteAsync(0, "move", 25, 24);
        await ExecuteAsync(1, "forge", 13, 14);
        Assert.That(Self(1).virtues.Select(t => t.type),
            Is.EquivalentTo(new[] { VirtueType.Art, VirtueType.Security }));
        Assert.That(seats[1].View.board.Single(s => s.id == 20).pieceId, Is.Not.Null);
        milestones.Add("Art and Security earned through authored setup and four ordinary turns; no seeding");
    }

    private async Task ExecuteAsync(int actor, string kind, int from = -1, int to = -1, int resource = -1)
    {
        await seats[actor].SubmitAsync(kind, from, to, resource);
        await WaitBothAsync(UnitySeat.Number(seats[actor].View.version));
    }

    private Task WaitBothAsync(long version) => Task.WhenAll(seats.Select(s => s.WaitVersionAsync(version)));
    private PlayerView Self(int seat) => seats[seat].View.players.Single(p => p.seat == seat);
    private static string Board(ClientView view) => JsonSerializer.Serialize(view.board, UnitySeat.Json);

    private void AssertResumedDecision(string decision, string prompt, string version, string match)
    {
        var view = seats[1].View;
        Assert.That(view.seat, Is.EqualTo(1));
        Assert.That(view.matchId, Is.EqualTo(match));
        Assert.That(view.version, Is.EqualTo(version));
        Assert.That(view.decision?.id, Is.EqualTo(decision));
        Assert.That(view.decision?.prompt, Is.EqualTo(prompt));
        Assert.That(view.decision?.kind, Is.EqualTo("Retraction"));
        Assert.That(view.decision?.owner, Is.EqualTo(1));
    }

    private async Task<string[]> GuestCookieValuesAsync()
    {
        var values = new List<string>();
        foreach (var seat in seats)
            values.Add((await seat.Context.CookiesAsync()).Single(c => c.HttpOnly).Value);
        return values.ToArray();
    }

    private async Task ContinueNormalPlayToVictoryAsync()
    {
        var counts = new Dictionary<string, int>();
        int count = 0;
        for (; count < 1000 && seats[0].View.phase != "Finished"; count++)
        {
            // Choose exclusively from the two browsers' authorized projections. Never read MatchState,
            // server storage, a future deck card, or a hidden opposing kingdom.
            int actor = seats.FirstOrDefault(s => s.View.decision != null)?.Seat ?? seats[0].View.activeSeat;
            ClientView view = seats[actor].View;
            LegalChoice choice = ChooseVisibleNormalAction(view);
            counts[choice.kind] = counts.GetValueOrDefault(choice.kind) + 1;
            await seats[actor].SubmitAsync(choice);
            await WaitBothAsync(UnitySeat.Number(seats[actor].View.version));
        }
        Assert.That(seats[0].View.phase, Is.EqualTo("Finished"),
            "A bounded sequence of real Unity draw/place/move/forge clicks must reach authored victory.");
        Assert.That(seats[0].View.draw, Is.False, "A stalemate is not a win acceptance pass.");
        Assert.That(seats[0].View.winner, Is.InRange(0, 1));
        Assert.That(seats[1].View.winner, Is.EqualTo(seats[0].View.winner));
        int winner = seats[0].View.winner;
        Assert.That(SharedRules.MeetsGoals(Self(winner).virtues.Select(t => t.type), Goals(Self(winner))), Is.True);
        Assert.That(counts.GetValueOrDefault("draw"), Is.GreaterThan(0));
        Assert.That(counts.GetValueOrDefault("place"), Is.GreaterThan(0));
        Assert.That(counts.GetValueOrDefault("forge"), Is.GreaterThan(0));
        milestones.Add($"native victory: {count} additional actual Unity commands; " +
            string.Join(", ", counts.OrderBy(pair => pair.Key).Select(pair => pair.Key + "=" + pair.Value)));
    }

    private static LegalChoice ChooseVisibleNormalAction(ClientView view)
    {
        int actor = view.seat;
        var self = view.players.Single(player => player.seat == actor);
        int Needed(VirtueType type) => Goals(self).Where(goal => goal.Key == type)
            .Select(goal => goal.Value).DefaultIfEmpty(0).Max() - self.virtues.Count(token => token.type == type);
        VirtueType Result(LegalChoice choice) => Definitions.Forge(
            view.board.Single(slot => slot.id == choice.from).type,
            view.board.Single(slot => slot.id == choice.to).type);
        if (view.decision?.kind == "Retraction") return view.choices.Single(item => item.kind == "pass");
        if (view.decision?.kind == "PlaceDraw")
        {
            var drawn = Enum.GetValues<ResourceType>().Single(type =>
                view.decision.prompt.Contains("drawn " + type + " ", StringComparison.Ordinal));
            return view.choices.OrderByDescending(item => view.board.Count(slot =>
                slot.pieceId != null && slot.type != drawn && slot.seat == actor &&
                SharedRules.Adjacent(slot.id, item.to) && Needed(Definitions.Forge(drawn, slot.type)) > 0)).First();
        }
        if (view.decision != null) return view.choices.Single(item => item.kind == "ack");
        int resources = view.board.Count(slot => slot.seat == actor && slot.pieceId != null);
        var choice = view.choices.Where(item => item.kind == "forge" && resources > 2)
            .OrderByDescending(item => Needed(Result(item))).FirstOrDefault();
        if (choice != null && Needed(Result(choice)) <= 0 && resources < 14) choice = null;
        if (choice == null && resources > 2)
            choice = view.choices.Where(item => item.kind == "move" &&
                view.board.Single(slot => slot.id == item.to).seat == actor)
                .FirstOrDefault(item =>
                {
                    ResourceType type = view.board.Single(slot => slot.id == item.from).type;
                    return view.board.Any(slot => slot.pieceId != null && slot.id != item.from &&
                        slot.seat == actor && slot.type != type && SharedRules.Adjacent(slot.id, item.to) &&
                        Needed(Definitions.Forge(type, slot.type)) > 0);
                });
        return choice ?? view.choices.FirstOrDefault(item => item.kind == "draw") ??
            view.choices.FirstOrDefault(item => item.kind == "forge") ??
            view.choices.First(item => item.kind == "move");
    }

    private static KeyValuePair<VirtueType, int>[] Goals(PlayerView self) =>
        Definitions.Goals(new[] { Definitions.Egolica, Definitions.Mask, Definitions.Knowledge }
            .Single(id => Definitions.KingdomName(id) == self.kingdom));

    private async Task CaptureAsync(string stage)
    {
        string directory = Path.Combine(server.Artifacts, TestContext.CurrentContext.Test.Name);
        Directory.CreateDirectory(directory);
        foreach (var seat in seats) await seat.SaveEvidenceAsync(directory, stage);
        await File.WriteAllLinesAsync(Path.Combine(directory, "milestones.txt"), milestones);
    }

    [TearDown]
    public async Task CloseOnlyOwnedContexts()
    {
        if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
        {
            string directory = Path.Combine(server.Artifacts, TestContext.CurrentContext.Test.Name);
            Directory.CreateDirectory(directory);
            foreach (var seat in seats)
            {
                try
                {
                    if (new Uri(seat.Page.Url).Fragment.Contains("invite", StringComparison.OrdinalIgnoreCase)) continue;
                    await seat.Page.Locator("#unity-canvas").ScreenshotAsync(new LocatorScreenshotOptions
                    {
                        Path = Path.Combine(directory, $"failure-seat-{seat.Seat}.png"), Timeout = 5000
                    });
                    await seat.SaveFailureDiagnosticsAsync(directory);
                }
                catch (Exception error)
                {
                    TestContext.Error.WriteLine("Failure evidence capture failed: " + error.GetType().Name);
                }
            }
        }
        foreach (var seat in seats) await seat.DisposeAsync();
        seats = [];
    }

    [OneTimeTearDown]
    public async Task StopOnlyOwnedProcesses()
    {
        if (browser != null) await browser.CloseAsync();
        if (playwright != null) playwright.Dispose();
        if (server != null) await server.DisposeAsync();
    }
}
