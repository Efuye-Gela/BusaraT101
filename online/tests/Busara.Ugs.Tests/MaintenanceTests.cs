#if BUSARA_MAINTENANCE
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Busara.Ugs.Tests;

public sealed class MaintenanceTests
{
    [Test]
    public async Task MaintenanceBoundaryRejectsBeforeTouchingContextOrStorage()
    {
        var module = new Module(NullLogger<Module>.Instance);
        var reply = await module.Execute(null!, null!, "register", "{}", "");
        Assert.That(reply.status, Is.EqualTo(503));
        Assert.That(reply.body, Does.Contain("storage_upgrade_in_progress"));
    }
}
#endif
