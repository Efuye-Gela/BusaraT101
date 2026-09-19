using System.Security.Cryptography;
using Busara.Online;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Apis.Extensions;
using Unity.Services.CloudCode.Core;

namespace Busara.Ugs;

public sealed class ModuleConfig : ICloudCodeSetup
{
    public void Setup(ICloudCodeConfig config) => config.AddGameApiClient();
}

public sealed class SecureRandom : IGameRandom
{
    public int Next(int exclusiveMaximum) => RandomNumberGenerator.GetInt32(exclusiveMaximum);
}

public sealed class Module(ILogger<Module> logger)
{
    [CloudCodeFunction("Execute")]
    public async Task<Reply> Execute(IExecutionContext context, IGameApiClient api,
        string operation, string payload, string matchId)
    {
        try
        {
            return await new MatchService(new CloudSaveStore(api, context), new SecureRandom(), TimeProvider.System)
                .Execute(context.PlayerId, operation, payload, matchId);
        }
        catch (Exception error) when (error is Unity.Services.CloudCode.Shared.ApiException ||
                                      error is InvalidOperationException || error is System.Text.Json.JsonException ||
                                      error is HttpRequestException || error is TaskCanceledException)
        {
            // Upstream errors may embed private Cloud Save values. Never log their message/body.
            logger.LogError("UGS request failed ({ErrorType}).", error.GetType().Name);
            return Reply.Error(503, "storage_unavailable");
        }
    }
}
