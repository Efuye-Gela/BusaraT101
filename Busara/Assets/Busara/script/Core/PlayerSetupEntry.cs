using System;
using System.Collections.Generic;

[Serializable]
public class PlayerSetupEntry
{
    public string Name;
    public Kingdom Kingdom;
    public bool IsBotControlled;

    public PlayerSetupEntry(string name, Kingdom kingdom = null, bool isBotControlled = false)
    {
        Name = name;
        Kingdom = kingdom;
        IsBotControlled = isBotControlled;
    }
}

public static class PlayerSetupRules
{
    public const int MinimumPlayers = 2;
    public const int MaximumNameLength = 32;

    public static bool Validate(IReadOnlyList<PlayerSetupEntry> entries, KingdomCatalog catalog,
        int capacity, out string error)
    {
        if (capacity < MinimumPlayers)
        {
            error = "This scene needs at least two configured player boards.";
            return false;
        }
        if (catalog == null || catalog.kingdoms == null || catalog.kingdoms.Count == 0 ||
            catalog.kingdoms.Exists(kingdom => kingdom == null || kingdom.power == null) ||
            new HashSet<Kingdom>(catalog.kingdoms).Count != catalog.kingdoms.Count)
        {
            error = "The kingdom catalog is missing or incomplete.";
            return false;
        }
        if (entries == null || entries.Count < MinimumPlayers || entries.Count > capacity)
        {
            error = $"Add between {MinimumPlayers} and {capacity} players.";
            return false;
        }
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var kingdoms = new HashSet<Kingdom>();
        for (int i = 0; i < entries.Count; i++)
        {
            PlayerSetupEntry entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.Name) ||
                entry.Name.Trim().Length > MaximumNameLength)
            {
                error = $"Player {i + 1} needs a name of 1-{MaximumNameLength} characters.";
                return false;
            }
            if (!names.Add(entry.Name.Trim()))
            {
                error = "Give each player a different name.";
                return false;
            }
            if (entry.Kingdom == null || !catalog.kingdoms.Contains(entry.Kingdom))
            {
                error = $"Choose a kingdom for {entry.Name.Trim()}.";
                return false;
            }
            if (!kingdoms.Add(entry.Kingdom))
            {
                error = "Each kingdom can belong to only one player.";
                return false;
            }
        }
        error = string.Empty;
        return true;
    }
}
