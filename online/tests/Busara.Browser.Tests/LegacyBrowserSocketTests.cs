using NUnit.Framework;

namespace Busara.Browser.Tests;

public sealed partial class TwoBrowserUnityTests
{
    [Test]
    public async Task LegacyBrowserReceivesAuthenticatedWebSocketInvalidation()
    {
        await seats[0].NavigateAsync();
        await seats[0].ClickAsync("create-room");
        await seats[0].WaitViewAsync(view => view.phase == "Lobby", "Host lobby did not load.");
        await Task.Delay(1500);
        await seats[0].AssertNetworkAsync();
    }
}
