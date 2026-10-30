using Busara.Online;
using Npgsql;

namespace Busara.Server;

public sealed class Authentication(Database database, ServerSettings settings)
{
    public const string CookieName = "__Host-busara";

    public void CheckOrigin(HttpContext context)
    {
        var origin = context.Request.Headers.Origin.ToString();
        if (!string.Equals(origin, settings.Origin.GetLeftPart(UriPartial.Authority), StringComparison.Ordinal) ||
            (context.Request.Headers.TryGetValue("Sec-Fetch-Site", out var site) && site != "same-origin"))
            throw new ApiException(403, "origin_rejected", "A same-origin request is required.");
    }

    public async Task<Guest> RequireAsync(HttpContext context, bool mutation = false)
    {
        if (mutation) CheckOrigin(context);
        if (!context.Request.Cookies.TryGetValue(CookieName, out var token) || token.Length != 43)
            throw new ApiException(401, "guest_required", "A valid guest session is required.");
        await using var connection = await database.Source.OpenConnectionAsync(context.RequestAborted);
        await using var command = new NpgsqlCommand("SELECT id,expires_at FROM guests WHERE token_hash=@hash", connection);
        command.Parameters.AddWithValue("hash", ServerSettings.Hash(token));
        await using var reader = await command.ExecuteReaderAsync(context.RequestAborted);
        if (!await reader.ReadAsync(context.RequestAborted))
            throw new ApiException(401, "guest_invalid", "This guest session is invalid.");
        var guest = new Guest(reader.GetGuid(0), reader.GetFieldValue<DateTimeOffset>(1));
        if (guest.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new ApiException(401, "guest_expired", "This guest session has expired. Seat recovery is unavailable.");
        if (mutation && !ServerSettings.Equal(Csrf(guest), context.Request.Headers["X-CSRF-Token"].ToString()))
            throw new ApiException(403, "csrf_rejected", "A valid CSRF token is required.");
        return guest;
    }

    public string Csrf(Guest guest) => settings.Secret("csrf-v1", guest.Id.ToString("D"));
    public GuestView View(Guest guest) => new()
    {
        guestId = guest.Id.ToString("D"), csrfToken = Csrf(guest), expiresAt = guest.ExpiresAt.ToString("O")
    };

    public async Task<GuestView> CreateAsync(HttpContext context)
    {
        CheckOrigin(context);
        if (context.Request.Cookies.ContainsKey(CookieName))
        {
            await RequireAsync(context);
            throw new ApiException(409, "guest_exists", "A guest session already exists.");
        }
        var token = ServerSettings.NewToken();
        var expiry = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeMilliseconds());
        var guest = new Guest(Guid.NewGuid(), expiry);
        await using var command = database.Source.CreateCommand(
            "INSERT INTO guests(id,token_hash,expires_at) VALUES(@id,@hash,@expiry)");
        command.Parameters.AddWithValue("id", guest.Id);
        command.Parameters.AddWithValue("hash", ServerSettings.Hash(token));
        command.Parameters.AddWithValue("expiry", guest.ExpiresAt);
        await command.ExecuteNonQueryAsync(context.RequestAborted);
        context.Response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict,
            Path = "/", Expires = guest.ExpiresAt, IsEssential = true
        });
        return View(guest);
    }
}
