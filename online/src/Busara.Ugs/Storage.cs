using System.Net;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;

namespace Busara.Ugs;

public sealed record StoredDocument(string Json, string WriteLock);

public interface IPrivateStore
{
    Task<StoredDocument?> Read(string customId);
    Task<bool> CompareExchange(string customId, string json, string writeLock);
    Task CreateCandidate(string customId, string json);
}

public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        IncludeFields = true,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };

    public static string Encode<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Decode<T>(string value) =>
        JsonSerializer.Deserialize<T>(value, Options) ?? throw new JsonException("Missing document.");
}

public sealed class CloudSaveStore(IGameApiClient api, IExecutionContext context) : IPrivateStore
{
    public const string Key = "document";

    public async Task<StoredDocument?> Read(string customId)
    {
        try
        {
            var result = await api.CloudSaveData.GetPrivateCustomItemsAsync(
                context, context.ServiceToken, context.ProjectId, customId, new List<string> { Key });
            var item = result.Data.Results.SingleOrDefault(value => value.Key == Key);
            if (item is null) return null;
            return DecodeDocument(item);
        }
        catch (Unity.Services.CloudCode.Shared.ApiException error) when (error.Response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public static StoredDocument DecodeDocument(Item item)
    {
        if (string.IsNullOrWhiteSpace(item.WriteLock))
            throw new InvalidOperationException("Cloud Save returned no write lock.");
        // Dashboard-created objects and module-written strings carry the same
        // document schema. Preserve their contents and lock; never reinitialize.
        string json = item.Value switch
        {
            string text => text,
            JObject value => value.ToString(Newtonsoft.Json.Formatting.None),
            JValue { Type: JTokenType.String, Value: string text } => text,
            JsonElement { ValueKind: JsonValueKind.Object } value => value.GetRawText(),
            JsonElement { ValueKind: JsonValueKind.String } value => value.GetString()!,
            _ => throw new InvalidOperationException("Invalid Cloud Save document value type.")
        };
        return new StoredDocument(json, item.WriteLock);
    }

    public async Task<bool> CompareExchange(string customId, string json, string writeLock)
    {
        if (string.IsNullOrWhiteSpace(writeLock)) throw new ArgumentException("A write lock is required.");
        try
        {
            await api.CloudSaveData.SetPrivateCustomItemAsync(context, context.ServiceToken,
                context.ProjectId, customId, new SetItemBody(Key, json, writeLock));
            return true;
        }
        catch (Unity.Services.CloudCode.Shared.ApiException error) when (error.Response.StatusCode == HttpStatusCode.Conflict)
        {
            return false;
        }
    }

    public async Task CreateCandidate(string customId, string json)
    {
        // Only fresh random candidate IDs reach this method. Never retry an unlocked
        // initialization over a known room: it could overwrite an advanced match.
        await api.CloudSaveData.SetPrivateCustomItemAsync(context, context.ServiceToken,
            context.ProjectId, customId, new SetItemBody(Key, json));
    }
}
