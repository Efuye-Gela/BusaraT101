using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.Services.CloudSave.Model;

namespace Busara.Ugs.Tests;

public sealed class CloudSaveStorageTests
{
    private const string WriteLock = "0123456789abcdef0123456789abcdef";
    private const string Document = "{\"schemaVersion\":1,\"expiresAt\":\"2026-10-01T00:00:00+00:00\"," +
        "\"creates\":{\"existing-request\":{\"actor\":\"existing-player\",\"commandId\":\"original-command\"," +
        "\"matchId\":\"existing-match\",\"inviteToken\":\"private-test-value\"}},\"joins\":{\"prior-join\":\"fingerprint\"}}";

    private static Item ResponseItem(object? value, string writeLock = WriteLock)
    {
        string response = JsonConvert.SerializeObject(new
        {
            key = "document", value, writeLock,
            created = new { date = "2026-09-19T00:00:00Z" },
            modified = new { date = "2026-09-19T00:00:00Z" }
        });
        return JsonConvert.DeserializeObject<Item>(response)!;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SdkResponseAcceptsDashboardObjectAndModuleStringWithoutLosingData(bool dashboardObject)
    {
        object value = dashboardObject ? JObject.Parse(Document) : Document;
        var item = ResponseItem(value);
        var stored = CloudSaveStore.DecodeDocument(item);
        Assert.That(stored.WriteLock, Is.EqualTo(WriteLock));
        Assert.That(JToken.DeepEquals(JObject.Parse(stored.Json), JObject.Parse(Document)), Is.True);
        var guest = Json.Decode<GuestDocument>(stored.Json);
        Assert.That(guest.expiresAt, Is.EqualTo(DateTimeOffset.Parse("2026-10-01T00:00:00+00:00")));
        Assert.That(guest.creates["existing-request"].matchId, Is.EqualTo("existing-match"));
        Assert.That(guest.joins["prior-join"], Is.EqualTo("fingerprint"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void JsonElementObjectAndStringPreserveContents(bool asString)
    {
        using var parsed = JsonDocument.Parse(asString ? JsonConvert.SerializeObject(Document) : Document);
        var item = ResponseItem(Document);
        item.Value = parsed.RootElement;
        var stored = CloudSaveStore.DecodeDocument(item);
        Assert.That(JToken.DeepEquals(JObject.Parse(stored.Json), JObject.Parse(Document)), Is.True);
        Assert.That(stored.WriteLock, Is.EqualTo(WriteLock));
    }

    [Test]
    public void WrappedStringPreservesContents()
    {
        var item = ResponseItem(Document);
        item.Value = new JValue(Document);
        Assert.That(CloudSaveStore.DecodeDocument(item).Json, Is.EqualTo(Document));
    }

    [TestCase("[]")]
    [TestCase("42")]
    [TestCase("true")]
    [TestCase("null")]
    public void UnsupportedValuesFailWithoutEchoingContents(string json)
    {
        var item = ResponseItem(Document);
        item.Value = JToken.Parse(json);
        var error = Assert.Throws<InvalidOperationException>(() => CloudSaveStore.DecodeDocument(item));
        Assert.That(error!.Message, Is.EqualTo("Invalid Cloud Save document value type."));
    }

    [Test]
    public void MissingLockDoesNotPermitUnconditionalUpdate()
    {
        var item = ResponseItem(JObject.Parse(Document), "");
        Assert.Throws<InvalidOperationException>(() => CloudSaveStore.DecodeDocument(item));
    }

    [Test]
    public void ObjectSupportDoesNotRelaxDocumentSchemaValidation()
    {
        var stored = CloudSaveStore.DecodeDocument(ResponseItem(JObject.Parse("{\"schemaVersion\":1,\"unexpected\":true}")));
        Assert.Throws<System.Text.Json.JsonException>(() => Json.Decode<GuestDocument>(stored.Json));
    }
}
