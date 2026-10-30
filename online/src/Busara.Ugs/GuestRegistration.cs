using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Busara.Ugs;

public sealed partial class MatchService
{
    public const int RegistrationShardCount = 64;
    private const string CandidateGuestPrefix = "busara_guest_candidate_";

    public static string RegistrationShardKey(int index)
    {
        if (index is < 0 or >= RegistrationShardCount) throw new ArgumentOutOfRangeException(nameof(index));
        return "busara_registration_v1_" + index.ToString("D2", CultureInfo.InvariantCulture);
    }

    public static string RegistrationKey(string actor) =>
        RegistrationShardKey(SHA256.HashData(Encoding.UTF8.GetBytes(actor))[0] % RegistrationShardCount);

    private async Task<GuestRegistration?> Registration(string actor, bool allowNew)
    {
        string shardKey = RegistrationKey(actor);
        GuestRegistration? candidate = null;
        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            var stored = await store.Read(shardKey);
            if (stored is null) throw new RequestError(503, "registration_not_initialized");
            var shard = DecodeStored<RegistrationShard>(stored.Json);
            if (shard.schemaVersion != 1 || shard.registrations is null)
                throw new InvalidOperationException("Invalid registration shard.");
            if (shard.registrations.TryGetValue(actor, out var published))
            {
                ValidateRegistration(published);
                return published;
            }
            if (candidate is null)
            {
                var guest = await InitialGuest(actor, allowNew);
                if (guest is null) return null;
                candidate = new GuestRegistration
                {
                    guestDocumentId = CandidateGuestPrefix + Guid.NewGuid().ToString("D"),
                    expiresAt = guest.expiresAt
                };
                shard.registrations.Add(actor, candidate);
                string shardJson = Encode(shard);
                string guestJson = Encode(guest);
                // Unlocked writes are restricted to this invocation's fresh random ID.
                // If the write's outcome is uncertain, abandon it: never retry that ID.
                await store.CreateCandidate(candidate.guestDocumentId, guestJson);
                if (await store.CompareExchange(shardKey, shardJson, stored.WriteLock)) return candidate;
            }
            else
            {
                shard.registrations.Add(actor, candidate);
                if (await store.CompareExchange(shardKey, Encode(shard), stored.WriteLock)) return candidate;
            }
        }
        throw new RequestError(503, "storage_busy");
    }

    private async Task<GuestDocument?> InitialGuest(string actor, bool allowNew)
    {
        var currentTask = store.Read(GuestKey(actor));
        var legacyTask = ReadLegacyDirectory();
        await Task.WhenAll(currentTask, legacyTask);
        var stored = await currentTask;
        var current = stored is null ? null : DecodeStored<GuestDocument>(stored.Json);
        if (current is not null && (current.schemaVersion is not (1 or 2) ||
                                   current.creates is null || current.joins is null))
            throw new InvalidOperationException("Invalid legacy guest document.");
        var merged = GuestMigration.Merge(actor, current, await legacyTask);
        return merged ?? (allowNew ? new GuestDocument { schemaVersion = 2, expiresAt = Now.AddDays(30) } : null);
    }

    private static void ValidateRegistration(GuestRegistration registration)
    {
        if (registration is null || registration.guestDocumentId is null ||
            !registration.guestDocumentId.StartsWith(CandidateGuestPrefix, StringComparison.Ordinal) ||
            !Guid.TryParseExact(registration.guestDocumentId[CandidateGuestPrefix.Length..], "D", out _) ||
            registration.expiresAt == default)
            throw new InvalidOperationException("Invalid guest registration.");
    }
}
