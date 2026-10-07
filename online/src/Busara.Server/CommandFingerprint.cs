using System.Text;
using System.Text.Json;
using Busara.Online;

namespace Busara.Server;

internal static class CommandFingerprint
{
    public static string For(OnlineCommand command)
    {
        using var document = JsonDocument.Parse(Wire.Encode(command));
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var field in document.RootElement.EnumerateObject())
            {
                // Empty v2 selection must retain the canonical bytes used by durable v1 receipts.
                if (field.Name == nameof(OnlineCommand.slots) && command.slots.Length == 0) continue;
                // Default v3 power fields likewise keep v1/v2 fingerprints stable.
                if (field.Name == nameof(OnlineCommand.count) && command.count == 0) continue;
                if (field.Name == nameof(OnlineCommand.virtueType) && command.virtueType == -1) continue;
                if (field.Name == nameof(OnlineCommand.exchangeIds) && (command.exchangeIds?.Length ?? 0) == 0) continue;
                field.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        return ServerSettings.Hash(Encoding.UTF8.GetString(buffer.ToArray()));
    }
}
