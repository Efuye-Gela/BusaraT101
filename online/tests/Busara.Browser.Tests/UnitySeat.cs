using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Busara.Online;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Busara.Browser.Tests;

internal sealed class UnitySeat : IAsyncDisposable
{
    internal static readonly JsonSerializerOptions Json = new()
    {
        IncludeFields = true,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private readonly Uri origin;
    private readonly ConcurrentBag<Task> observations = [];
    private readonly ConcurrentQueue<string> violations = [];
    private readonly ConcurrentQueue<string> networkEvidence = [];
    private readonly ConcurrentQueue<OnlineCommand> commands = [];
    private readonly ConcurrentDictionary<IRequest, int> requestGenerations = new();
    private int documentGeneration;
    private ClientView? latest;
    public IBrowserContext Context { get; }
    public IPage Page { get; }
    public int Seat { get; }
    public ClientView View => Volatile.Read(ref latest) ??
        throw new AssertionException("No authorized Unity projection has arrived.");
    public OnlineCommand[] SentCommands => commands.ToArray();

    private UnitySeat(IBrowserContext context, IPage page, Uri origin, int seat)
    {
        Context = context; Page = page; this.origin = origin; Seat = seat;
        page.Response += (_, response) => observations.Add(ObserveAsync(response,
            requestGenerations.TryGetValue(response.Request, out int generation)
                ? generation : Volatile.Read(ref documentGeneration)));
        page.RequestFailed += (_, request) => requestGenerations.TryRemove(request, out int ignored);
        page.Request += (_, request) =>
        {
            if (new Uri(request.Url).AbsolutePath.StartsWith("/api/"))
                requestGenerations[request] = Volatile.Read(ref documentGeneration);
            if (!SameOrigin(request.Url)) violations.Enqueue("An HTTP request escaped the one allowed test origin.");
            if (!request.Url.EndsWith("/commands", StringComparison.Ordinal) || request.Method != "POST") return;
            try
            {
                commands.Enqueue(JsonSerializer.Deserialize<OnlineCommand>(request.PostData!, Json)!);
                if (!request.Headers.TryGetValue("x-csrf-token", out var csrf) || string.IsNullOrWhiteSpace(csrf))
                    violations.Enqueue("A Unity mutation omitted the anti-CSRF header.");
            }
            catch { violations.Enqueue("A Unity command did not match the public command schema."); }
        };
        page.WebSocket += (_, socket) =>
        {
            var address = new Uri(socket.Url);
            if (address.Scheme != "wss" || address.Authority != origin.Authority ||
                !address.AbsolutePath.EndsWith("/events") || address.Query != "")
                violations.Enqueue("A WebSocket escaped the authenticated loopback event endpoint.");
            socket.SocketError += (_, error) =>
            {
                var category = System.Text.RegularExpressions.Regex.Match(error,
                    @"response code: \d{3}|net::[A-Z_]+");
                networkEvidence.Enqueue("websocket error: " +
                    (category.Success ? category.Value : "handshake_or_transport_failure"));
            };
            socket.FrameReceived += (_, frame) =>
            {
                try
                {
                    using var document = JsonDocument.Parse(frame.Text ?? throw new JsonException());
                    var fields = document.RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray();
                    if (!fields.SequenceEqual(new[] { "matchId", "version" }))
                        violations.Enqueue("A WebSocket message contained fields other than match/version invalidation.");
                    networkEvidence.Enqueue("wss invalidation: matchId/version schema verified");
                }
                catch { violations.Enqueue("A WebSocket invalidation was not bounded public JSON."); }
            };
        };
    }

    public static async Task<UnitySeat> OpenAsync(IBrowser browser, Uri origin, int seat)
    {
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1600, Height = 1000 },
            DeviceScaleFactor = 1,
            ServiceWorkers = ServiceWorkerPolicy.Block,
            AcceptDownloads = false
        });
        await context.GrantPermissionsAsync(["clipboard-read", "clipboard-write"],
            new BrowserContextGrantPermissionsOptions { Origin = origin.GetLeftPart(UriPartial.Authority) });
        await context.RouteAsync("**/*", async route =>
        {
            if (Uri.TryCreate(route.Request.Url, UriKind.Absolute, out var target) &&
                target.Scheme == "https" && target.Authority == origin.Authority)
                await route.ContinueAsync();
            else await route.AbortAsync("blockedbyclient");
        });
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(30_000);
        return new UnitySeat(context, page, origin, seat);
    }

    public async Task NavigateAsync(string? invite = null)
    {
        var address = invite == null ? origin.ToString() : invite;
        if (!SameOrigin(address)) throw new AssertionException("Invitation did not target the owned loopback origin.");
        Interlocked.Increment(ref documentGeneration);
        // Suppress the Playwright navigation call log: the invitation fragment is a bearer secret.
        try { await Page.GotoAsync(address, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded }); }
        catch { throw new AssertionException("The owned Unity Web entry did not load (URL withheld)."); }
        await WaitUiAsync();
        await ClickAsync("create-guest");
        await WaitAsync(async () => (await ControlsAsync()).Any(c => c.id == "create-room" && c.enabled),
            "Explicit guest creation did not reach the Unity room entry.");
        Assert.That(new Uri(Page.Url).Fragment.Contains("invite", StringComparison.OrdinalIgnoreCase), Is.False,
            "The invitation must be removed before any screenshot.");
    }

    public async Task<ClientView> WaitViewAsync(Func<ClientView, bool> predicate, string expectation)
    {
        await WaitAsync(async () =>
        {
            var view = Volatile.Read(ref latest);
            if (view == null || !predicate(view)) return false;
            var ui = await UiAsync();
            return ui != null && ui.version == view.version && ui.phase == view.phase;
        }, expectation);
        return View;
    }

    public Task<ClientView> WaitVersionAsync(long version) => WaitViewAsync(
        view => Number(view.version) >= version, "Unity did not render the committed projection revision.");

    public async Task ReloadAsync()
    {
        var expectedSeat = View.seat;
        var match = View.matchId;
        Interlocked.Increment(ref documentGeneration);
        Volatile.Write(ref latest, null);
        await Page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        await WaitUiAsync();
        await WaitViewAsync(view => view.seat == expectedSeat && view.matchId == match,
            "Reload did not resume the same authenticated seat and match.");
    }

    public async Task ClickAsync(string id)
    {
        await Page.BringToFrontAsync();
        UiControl? chosen = null;
        await WaitAsync(async () =>
        {
            chosen = (await ControlsAsync()).SingleOrDefault(c => c.id == id && c.enabled);
            return chosen != null;
        }, "Expected enabled Unity control was absent: " + id);
        await ClickControlAsync(chosen!);
    }

    private async Task ClickControlAsync(UiControl control)
    {
        var canvas = await Page.Locator("#unity-canvas").BoundingBoxAsync();
        Assert.That(canvas, Is.Not.Null, "The acceptance flow requires the real Unity canvas.");
        Assert.That(control.x, Is.InRange(0f, 1f));
        Assert.That(control.y, Is.InRange(0f, 1f));
        Assert.That(control.width, Is.GreaterThan(0));
        Assert.That(control.height, Is.GreaterThan(0));
        Assert.That(control.x + control.width, Is.LessThanOrEqualTo(1.001f));
        Assert.That(control.y + control.height, Is.LessThanOrEqualTo(1.001f));
        await Page.Mouse.ClickAsync(canvas!.X + (control.x + control.width / 2) * canvas.Width,
            canvas.Y + (control.y + control.height / 2) * canvas.Height);
        // Unity processes input in frames; await its next read-only publication before another click.
        await using var previous = await Page.EvaluateHandleAsync("() => window.busaraVisibleUi");
        await using var updated = await Page.WaitForFunctionAsync(
            "previous => !!window.busaraVisibleUi && window.busaraVisibleUi !== previous", previous);
    }

    public async Task SubmitAsync(LegalChoice choice, bool waitForRevision = true)
    {
        long before = Number(View.version);
        if (choice.from >= 0 || choice.to >= 0)
        {
            string group = choice.kind + ":" +
                (choice.kind is "setupPlace" or "abundancePlace" ? choice.resourceType : -1);
            await ClickAsync("mode-" + group);
            if (choice.from >= 0) await ClickAsync("slot-" + choice.from);
            if (choice.to >= 0) await ClickAsync("slot-" + choice.to);
            else await ClickAsync(ChoiceId(choice));
        }
        else await ClickAsync(ChoiceId(choice));
        if (waitForRevision) await WaitVersionAsync(before + 1);
    }

    public Task SubmitAsync(string kind, int from = -1, int to = -1, int resource = -1,
        bool waitForRevision = true)
    {
        var choice = View.choices.Single(c => c.kind == kind && c.from == from &&
            c.to == to && c.resourceType == resource);
        return SubmitAsync(choice, waitForRevision);
    }

    public async Task<string> CopyInvitationAsync()
    {
        await ClickAsync("copy-invite");
        await WaitAsync(async () => (await UiAsync())?.status?.StartsWith("Invitation copied.", StringComparison.Ordinal) == true,
            "The actual Unity clipboard operation did not confirm success.");
        string? invitation = null;
        await WaitAsync(async () =>
        {
            invitation = await Page.EvaluateAsync<string>("() => navigator.clipboard.readText()");
            return invitation != null && SameOrigin(invitation) &&
                new Uri(invitation).Fragment.StartsWith("#invite=", StringComparison.Ordinal);
        }, "The visible Unity Copy invitation control did not copy a private fragment invitation.");
        return invitation!;
    }

    public async Task AssertSecureDistinctCookieAsync(UnitySeat other)
    {
        var own = (await Context.CookiesAsync()).Where(c => c.HttpOnly).ToArray();
        var theirs = (await other.Context.CookiesAsync()).Where(c => c.HttpOnly).ToArray();
        Assert.That(own.Length, Is.EqualTo(1), "Expected exactly one HttpOnly guest cookie.");
        Assert.That(theirs.Length, Is.EqualTo(1), "Expected exactly one HttpOnly guest cookie in the other context.");
        Assert.That(own[0].Secure && theirs[0].Secure, Is.True);
        Assert.That(own[0].SameSite, Is.EqualTo(SameSiteAttribute.Strict));
        Assert.That(theirs[0].SameSite, Is.EqualTo(SameSiteAttribute.Strict));
        Assert.That(own[0].Value == theirs[0].Value, Is.False, "The two independent contexts must not share guest identity.");
    }

    public async Task SaveEvidenceAsync(string directory, string stage)
    {
        await DrainAsync();
        Assert.That(violations.ToArray(), Is.Empty, "Network information-boundary assertions failed.");
        Assert.That(new Uri(Page.Url).Fragment.Contains("invite", StringComparison.OrdinalIgnoreCase), Is.False);
        // Only the canvas is captured. No address bar, clipboard, headers, cookies, token IDs or raw payload files.
        await Page.BringToFrontAsync();
        await Page.Locator("#unity-canvas").ScreenshotAsync(new LocatorScreenshotOptions
        {
            Path = Path.Combine(directory, $"{stage}-seat-{Seat}.png"), Timeout = 10_000
        });
        await File.WriteAllLinesAsync(Path.Combine(directory, $"{stage}-seat-{Seat}-network.txt"),
            networkEvidence.ToArray());
        TestContext.AddTestAttachment(Path.Combine(directory, $"{stage}-seat-{Seat}.png"),
            "Real Unity canvas, seat " + Seat + ", " + stage);
    }

    public async Task AssertNetworkAsync()
    {
        await DrainAsync();
        Assert.That(violations.ToArray(), Is.Empty, "Network information-boundary assertions failed.");
        Assert.That(networkEvidence.Any(line => line.StartsWith("projection:")), Is.True,
            "No real authenticated projection response was inspected.");
        Assert.That(networkEvidence.Any(line => line.StartsWith("wss")), Is.True,
            "No real WSS invalidation payload was inspected. " +
            string.Join("; ", networkEvidence.Where(line => line.StartsWith("websocket error:")).TakeLast(3)));
    }

    public async Task SaveFailureDiagnosticsAsync(string directory)
    {
        await DrainAsync();
        var ui = await UiAsync();
        var view = Volatile.Read(ref latest);
        await File.WriteAllLinesAsync(Path.Combine(directory, $"failure-seat-{Seat}-controls.txt"),
            new[]
            {
                $"surface={ui?.surface}; revision={ui?.version}; phase={ui?.phase}; connection={ui?.connection}",
                $"visible-status={ui?.status}",
                $"observed-revision={view?.version}; observed-phase={view?.phase}; active-seat={view?.activeSeat}"
            }.Concat((ui?.controls ?? []).Select(control => control.id + "; enabled=" + control.enabled)));
        await File.WriteAllLinesAsync(Path.Combine(directory, $"failure-seat-{Seat}-network.txt"),
            networkEvidence.Concat(violations.Select(value => "violation: " + value)));
        await File.WriteAllLinesAsync(Path.Combine(directory, $"failure-seat-{Seat}-commands.txt"),
            commands.Select(command => $"kind={command.kind}; expected-version={command.expectedVersion}; " +
                $"from={command.from}; to={command.to}; resource={command.resourceType}; payment-count={command.paymentIds?.Length ?? 0}"));
    }

    private async Task ObserveAsync(IResponse response, int generation)
    {
        string stage = "route";
        try
        {
            var address = new Uri(response.Url);
            if (!SameOrigin(response.Url) || !address.AbsolutePath.StartsWith("/api/")) return;
            string endpoint = address.AbsolutePath == "/api/guest" ? "guest" :
                address.AbsolutePath == "/api/rooms" ? "create-room" :
                address.AbsolutePath == "/api/rooms/join" ? "join-room" :
                address.AbsolutePath.EndsWith("/commands") ? "command" : "room-view";
            networkEvidence.Enqueue($"http: method={response.Request.Method}; endpoint={endpoint}; status={response.Status}");
            if (!address.AbsolutePath.StartsWith("/api/rooms/")) return;
            if (!response.Ok)
            {
                stage = "rejection";
                using var error = JsonDocument.Parse(await response.TextAsync());
                if (error.RootElement.TryGetProperty("code", out var code))
                    networkEvidence.Enqueue("http rejection code: " + code.GetString());
                return;
            }
            if (response.Request.Method == "GET" && Guid.TryParse(address.Segments.Last(), out _))
            {
                stage = "projection";
                string payload = await response.TextAsync();
                ValidateNoInternalFields(payload);
                var view = JsonSerializer.Deserialize<ClientView>(payload, Json)!;
                if (view.seat != Seat) violations.Enqueue("Server projected a seat not owned by this browser.");
                var opponent = view.players.Single(p => p.seat != view.seat);
                var self = view.players.Single(p => p.seat == view.seat);
                if (!opponent.revealed && self.kingdom != Definitions.KingdomName(Definitions.Knowledge) &&
                    (opponent.kingdom != null || opponent.goal != null))
                    violations.Enqueue("An unrevealed opposing kingdom or goal leaked.");
                if (opponent.setupRemaining.Count != 0 || opponent.virtues.Any(t => t.id != null))
                    violations.Enqueue("Opponent private setup/payment identities leaked.");
                if (!opponent.virtuesVisible && opponent.virtues.Count != 0)
                    violations.Enqueue("Hidden opponent virtues leaked.");
                if (view.decision != null && view.decision.owner != Seat)
                    violations.Enqueue("A decision or payment options were projected to its non-owner.");
                if (view.awaitingOther && (view.decision != null || view.choices.Count != 0))
                    violations.Enqueue("An awaiting-other browser received private legal choices.");
                var previous = Volatile.Read(ref latest);
                if (generation == Volatile.Read(ref documentGeneration) &&
                    (previous == null || Number(view.version) >= Number(previous.version)))
                    Volatile.Write(ref latest, view);
                networkEvidence.Enqueue($"projection: seat={Seat}; revision={view.version}; phase={view.phase}; " +
                    "allowlist/hidden-kingdom/payment-identities/decision-owner verified; body omitted");
            }
            else if (response.Request.Method == "POST" && address.AbsolutePath.EndsWith("/commands"))
            {
                stage = "receipt";
                var payload = await response.TextAsync();
                ValidateNoInternalFields(payload);
                _ = JsonSerializer.Deserialize<CommandReceipt>(payload, Json) ??
                    throw new JsonException();
                networkEvidence.Enqueue("receipt: public command receipt schema verified; body omitted");
            }
        }
        catch (PlaywrightException) when (generation != Volatile.Read(ref documentGeneration) ||
            response.Request.Failure != null)
        {
            networkEvidence.Enqueue("incomplete response: navigation/disposal or observed transport cancellation; body not inspected");
        }
        catch (Exception error)
        {
            violations.Enqueue("A server payload failed the strict public DTO/information-boundary schema (" +
                stage + ", " + error.GetType().Name + ").");
        }
        finally { requestGenerations.TryRemove(response.Request, out _); }
    }

    private static void ValidateNoInternalFields(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        Visit(document.RootElement);
        static void Visit(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
                foreach (var field in element.EnumerateObject())
                {
                    if (new[] { "snapshot", "deck", "deckRemaining", "drawnCard", "reactionPayments",
                        "knownKingdoms", "continuation", "csrfToken", "guestToken", "inviteToken" }
                        .Contains(field.Name, StringComparer.OrdinalIgnoreCase))
                        throw new JsonException("Internal field in public payload.");
                    Visit(field.Value);
                }
            else if (element.ValueKind == JsonValueKind.Array)
                foreach (var item in element.EnumerateArray()) Visit(item);
        }
    }

    private async Task DrainAsync()
    {
        while (observations.TryTake(out var task)) await task;
    }

    private bool SameOrigin(string address) => Uri.TryCreate(address, UriKind.Absolute, out var target) &&
        target.Scheme == "https" && target.Authority == origin.Authority;

    private Task WaitUiAsync() => WaitAsync(async () =>
    {
        var ui = await UiAsync();
        return ui?.surface == "Unity OnlineMVP" && await Page.Locator("#unity-canvas").IsVisibleAsync();
    }, "Real development Unity Web UI did not publish read-only visible control bounds.", 180);

    internal async Task<VisibleUi?> UiAsync()
    {
        var json = await Page.EvaluateAsync<string>("() => JSON.stringify(window.busaraVisibleUi || null)");
        return JsonSerializer.Deserialize<VisibleUi>(json, Json);
    }
    private async Task<List<UiControl>> ControlsAsync() => (await UiAsync())?.controls ?? [];
    public static long Number(string version) => long.Parse(version, CultureInfo.InvariantCulture);
    public static string ChoiceId(LegalChoice choice) =>
        $"choice-{choice.kind}-{choice.from}-{choice.to}-{choice.resourceType}";

    public static async Task WaitAsync(Func<Task<bool>> condition, string failure, int seconds = 30)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(100);
        }
        throw new AssertionException(failure);
    }

    public async ValueTask DisposeAsync()
    {
        Interlocked.Increment(ref documentGeneration);
        await Context.CloseAsync();
        await DrainAsync();
    }

    internal sealed class VisibleUi
    {
        public string? surface { get; set; }
        public string? version { get; set; }
        public string? phase { get; set; }
        public string? status { get; set; }
        public string? connection { get; set; }
        public List<UiControl> controls { get; set; } = [];
    }

    internal sealed class UiControl
    {
        public string id { get; set; } = "";
        public string label { get; set; } = "";
        public float x { get; set; }
        public float y { get; set; }
        public float width { get; set; }
        public float height { get; set; }
        public bool enabled { get; set; }
    }
}
