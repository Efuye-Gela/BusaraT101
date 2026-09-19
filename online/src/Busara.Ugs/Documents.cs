using Busara.Online;

namespace Busara.Ugs;

public sealed class DirectoryDocument
{
    public int schemaVersion = 1;
    public Dictionary<string, DateTimeOffset> guests = new();
    public Dictionary<string, Creation> creates = new();
    public Dictionary<string, string> joins = new();
}

public sealed class Creation
{
    public string actor = "";
    public string commandId = "";
    public string matchId = "";
    public string inviteToken = "";
}

public sealed class Receipt
{
    public string actor = "";
    public string fingerprint = "";
    public Reply reply = new();
}

public sealed class HistoryEvent
{
    public string version = "";
    public string kind = "";
    public string? actionId;
}

public sealed class RoomDocument
{
    public int schemaVersion = 1;
    public string creationKey = "";
    public string[] members = new[] { "", "" };
    public string inviteHash = "";
    public DateTimeOffset inviteExpiresAt;
    public MatchState state = new();
    public Dictionary<string, Receipt> receipts = new();
    public List<HistoryEvent> events = new();
}

public sealed class Reply
{
    public int status { get; set; }
    public string body { get; set; } = "";
    public static Reply Ok<T>(T body) => new() { status = 200, body = Json.Encode(body) };
    public static Reply Error(int status, string code) =>
        new() { status = status, body = Json.Encode(new { code, message = "Request not applied. Check setup, identity or available choices before retrying." }) };
}

public sealed class RequestError(int status, string code) : Exception(code)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public sealed class RoomRequest
{
    public string commandId = "";
    public string? inviteToken;
}
