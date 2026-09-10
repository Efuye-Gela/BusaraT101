using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

public class BusaraFastTestStartTests
{
    private string key;
    private KingdomCatalog catalog;
    private PlayerSetupEntry[] entries;
    private readonly DateTime now = new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);

    [SetUp]
    public void SetUp()
    {
        key = "Busara.FastTestStartTests." + Guid.NewGuid().ToString("N");
        catalog = AssetDatabase.FindAssets("t:KingdomCatalog", new[] { "Assets/Busara" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<KingdomCatalog>(AssetDatabase.GUIDToAssetPath(guid)))
            .FirstOrDefault(asset => asset != null && asset.kingdoms != null && asset.kingdoms.Count >= 2 &&
                asset.kingdoms.All(kingdom => kingdom != null && kingdom.power != null));
        Assert.That(catalog, Is.Not.Null, "The game's saved kingdom catalog is required.");
        entries = new[]
        {
            new PlayerSetupEntry(" First player ", catalog.kingdoms[0], true),
            new PlayerSetupEntry("Second player", catalog.kingdoms[1])
        };
    }

    [TearDown]
    public void TearDown()
    {
        SessionState.EraseString(key);
    }

    [Test]
    public void SessionStateRoundTripRestoresSnapshotWithoutObjectReferences()
    {
        BusaraFastStartRequest request = BusaraFastStartRequest.Create(entries, catalog, 4, now);
        request.phase = "Resources";
        request.Save(key);
        string savedId = request.id;
        entries[0].Name = "Changed after request";
        entries[0].Kingdom = null;
        entries[0].IsBotControlled = false;
        request = null;

        BusaraFastStartRequest restored = BusaraFastStartRequest.Load(key);
        var resolved = restored.Resolve(catalog, 4);
        Assert.That(restored.id, Is.EqualTo(savedId));
        Assert.That(restored.phase, Is.EqualTo("Resources"));
        Assert.That(restored.deadlineTicks, Is.EqualTo(now.AddSeconds(90).Ticks));
        Assert.That(resolved.Select(entry => entry.Name), Is.EqualTo(new[] { "First player", "Second player" }));
        Assert.That(resolved[0].Kingdom, Is.SameAs(catalog.kingdoms[0]));
        Assert.That(resolved[1].Kingdom, Is.SameAs(catalog.kingdoms[1]));
        Assert.That(resolved[0].IsBotControlled, Is.True);
        Assert.That(resolved[1].IsBotControlled, Is.False);
        Assert.That(restored.kingdomGuids[0],
            Is.EqualTo(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(catalog.kingdoms[0]))));
    }

    [Test]
    public void InvalidNamesKingdomsAndCapacityAreRejectedBeforePersistence()
    {
        entries[1].Name = "first player";
        Assert.Throws<InvalidOperationException>(() => BusaraFastStartRequest.Create(entries, catalog, 4, now));
        entries[1].Name = "Second player";
        entries[1].Kingdom = entries[0].Kingdom;
        Assert.Throws<InvalidOperationException>(() => BusaraFastStartRequest.Create(entries, catalog, 4, now));
        entries[1].Kingdom = catalog.kingdoms[1];
        Assert.Throws<InvalidOperationException>(() => BusaraFastStartRequest.Create(entries, catalog, 1, now));
        Assert.That(BusaraFastStartRequest.Load(key), Is.Null);
    }

    [Test]
    public void RestoringRevalidatesAssetsCountAndNamesWithoutFallbacks()
    {
        BusaraFastStartRequest request = BusaraFastStartRequest.Create(entries, catalog, 4, now);
        request.kingdomGuids[0] = "00000000000000000000000000000000";
        Assert.Throws<InvalidOperationException>(() => request.Resolve(catalog, 4));
        request = BusaraFastStartRequest.Create(entries, catalog, 4, now);
        request.names[1] = request.names[0];
        Assert.Throws<InvalidOperationException>(() => request.Resolve(catalog, 4));
        request.names = new[] { "Only one" };
        Assert.Throws<InvalidOperationException>(() => request.Resolve(catalog, 4));
        request = BusaraFastStartRequest.Create(entries, catalog, 4, now);
        Assert.Throws<InvalidOperationException>(() => request.Resolve(catalog, 1));
        request.botControlled = new bool[1];
        Assert.Throws<InvalidOperationException>(() => request.Resolve(catalog, 4));
    }

    [Test]
    public void DeadlineSurvivesPersistenceAndExpiresAtTheBound()
    {
        BusaraFastStartRequest.Create(entries, catalog, 4, now).Save(key);
        BusaraFastStartRequest request = BusaraFastStartRequest.Load(key);
        Assert.That(request.HasExpired(now.AddSeconds(89)), Is.False);
        Assert.That(request.HasExpired(now.AddSeconds(90)), Is.True);
        Assert.That(request.HasExpired(now.AddMinutes(10)), Is.True);
    }
}
