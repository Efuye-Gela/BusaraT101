using Busara.Online;

namespace Busara.Ugs;

public sealed partial class MatchService
{
    // Read-only compatibility key. New guest documents always have random candidate IDs.
    public static string GuestKey(string actor) => "busara_guest_" + actor;

    private async Task<GuestRegistration> RequireRegistration(string actor)
    {
        var registration = await Registration(actor, false);
        if (registration is null || registration.expiresAt <= Now)
            throw new RequestError(401, "guest_expired");
        return registration;
    }

    private async Task<(GuestDocument Doc, string Lock, string Key)> RequireGuest(string actor)
    {
        var registration = await RequireRegistration(actor);
        var stored = await store.Read(registration.guestDocumentId);
        if (stored is null) throw new InvalidOperationException("Published guest document is missing.");
        var doc = DecodeStored<GuestDocument>(stored.Json);
        if (doc.schemaVersion != 2 || doc.creates is null || doc.joins is null ||
            doc.expiresAt != registration.expiresAt)
            throw new InvalidOperationException("Invalid published guest document.");
        return (doc, stored.WriteLock, registration.guestDocumentId);
    }

    private async Task<Reply> Guest(string actor, bool register)
    {
        var registration = await Registration(actor, register);
        if (registration is null) throw new RequestError(401, "guest_unregistered");
        if (registration.expiresAt <= Now) throw new RequestError(401, "guest_expired");
        return Reply.Ok(new GuestView
        {
            guestId = actor, expiresAt = registration.expiresAt.ToString("O"), csrfToken = "ugs-bearer"
        });
    }
}
