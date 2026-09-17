using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Busara.Server;

public sealed class ServerSettings
{
    public string ConnectionString { get; }
    public Uri Origin { get; }
    private readonly byte[] key;
    public string KeyFingerprint => Hash(Convert.ToBase64String(key));

    public ServerSettings(IConfiguration configuration)
    {
        if (configuration.GetSection("Kestrel:Endpoints").Exists())
            throw new InvalidOperationException("Custom Kestrel endpoints are disabled; this slice binds loopback HTTPS only.");
        ConnectionString = configuration.GetConnectionString("Busara") ?? configuration["BUSARA_DATABASE"]
            ?? throw new InvalidOperationException("ConnectionStrings__Busara is required.");
        var urls = configuration["urls"] ?? configuration["ASPNETCORE_URLS"];
        if (!Uri.TryCreate(configuration["BUSARA_ORIGIN"] ?? urls, UriKind.Absolute, out var origin) ||
            origin.Scheme != "https" || !origin.IsLoopback || origin.AbsolutePath != "/" ||
            origin.Query.Length != 0 || origin.Fragment.Length != 0 || origin.UserInfo.Length != 0)
            throw new InvalidOperationException("BUSARA_ORIGIN must be a loopback HTTPS origin.");
        if (!string.IsNullOrEmpty(urls) &&
            (!Uri.TryCreate(urls, UriKind.Absolute, out var configuredUrl) || configuredUrl != origin))
            throw new InvalidOperationException("ASPNETCORE_URLS must be one loopback HTTPS URL matching BUSARA_ORIGIN.");
        Origin = origin;
        try { key = Convert.FromBase64String(configuration["BUSARA_SECRET_KEY"] ?? ""); }
        catch (FormatException) { throw new InvalidOperationException("BUSARA_SECRET_KEY must be base64."); }
        if (key.Length != 32)
            throw new InvalidOperationException("BUSARA_SECRET_KEY must contain 32 random bytes.");
    }

    public string Secret(string purpose, string value) =>
        Base64Url(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(purpose + "\n" + value)));
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public static string NewToken() => Base64Url(RandomNumberGenerator.GetBytes(32));
    public static bool Equal(string expected, string supplied) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied));
}

public static class Wire
{
    public static readonly JsonSerializerOptions Json = new()
    {
        IncludeFields = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32
    };
    public static string Encode<T>(T value) => JsonSerializer.Serialize(value, Json);
    public static T Decode<T>(string value) =>
        JsonSerializer.Deserialize<T>(value, Json) ?? throw new InvalidOperationException("Invalid persisted data.");
}

public sealed class ApiException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public sealed record Guest(Guid Id, DateTimeOffset ExpiresAt);
public sealed record CreateRoomRequest(string CommandId);
public sealed record JoinRoomRequest(string CommandId, string InviteToken);
public sealed record StoredReceipt(int HttpStatus, Busara.Online.CommandReceipt Receipt);
public sealed class SecureGameRandom : Busara.Online.IGameRandom
{
    public int Next(int exclusiveMaximum) => RandomNumberGenerator.GetInt32(exclusiveMaximum);
}

internal sealed class FixedZeroGameRandom : Busara.Online.IGameRandom
{
    public int Next(int exclusiveMaximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exclusiveMaximum);
        return 0;
    }
}
