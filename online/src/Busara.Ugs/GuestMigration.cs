namespace Busara.Ugs;

public static class GuestMigration
{
    public static GuestDocument? Merge(string actor, GuestDocument? current, DirectoryDocument? legacy)
    {
        bool hasLegacy = legacy is not null && legacy.guests.ContainsKey(actor);
        if (current is null && !hasLegacy) return null;
        var merged = current is null ? new GuestDocument { expiresAt = legacy!.guests[actor] }
            : Json.Decode<GuestDocument>(Json.Encode(current));
        if (hasLegacy)
        {
            if (legacy!.guests[actor] < merged.expiresAt) merged.expiresAt = legacy.guests[actor];
            foreach (var (key, creation) in legacy.creates.Where(entry => entry.Value.actor == actor))
            {
                if (merged.joins.ContainsKey(key) ||
                    (merged.creates.TryGetValue(key, out var prior) && Json.Encode(prior) != Json.Encode(creation)))
                    throw new RequestError(409, "guest_migration_conflict");
                merged.creates[key] = creation;
            }
            // Legacy join keys are hashes with no actor field. Conservatively retain all
            // reservations once, rather than guess ownership or reread the global directory.
            foreach (var (key, fingerprint) in legacy.joins)
            {
                if (merged.creates.ContainsKey(key) ||
                    (merged.joins.TryGetValue(key, out var prior) && prior != fingerprint))
                    throw new RequestError(409, "guest_migration_conflict");
                merged.joins[key] = fingerprint;
            }
        }
        merged.schemaVersion = 2;
        return merged;
    }
}
