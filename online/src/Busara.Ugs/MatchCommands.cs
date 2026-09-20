using System.Text.Json;
using Busara.Online;

namespace Busara.Ugs;

public sealed partial class MatchService
{
    private async Task<Reply> Command(string actor, string matchId, string payload, bool withView)
    {
        var request = Json.Decode<OnlineCommand>(payload);
        CommandId(request.commandId);
        if (request.kind is null || request.kind.Length > 64 || request.name?.Length > 80 ||
            request.decisionId?.Length > 100 || request.expectedVersion?.Length > 20 ||
            request.resourceType is < -1 or > 3 || request.from is < -1 or > 31 || request.to is < -1 or > 31 ||
            request.paymentIds is null || request.paymentIds.Length > 12 ||
            request.paymentIds.Any(id => id is null || id.Length > 100))
            throw new RequestError(400, "invalid_command");
        // Both wire formats address the same durable command, including legacy fingerprints.
        string fingerprint = Hash("command\n" + payload);
        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            var guestTask = RequireRegistration(actor);
            var roomTask = Room(matchId);
            await Task.WhenAll(guestTask, roomTask);
            var room = await roomTask;
            int seat = Seat(room.Value, actor);
            var prior = Previous(room.Value, actor, request.commandId, fingerprint);
            if (prior is not null) return CommandReply(prior, room.Value, seat, withView);
            var receipt = new CommandReceipt
            {
                commandId = request.commandId, matchId = matchId, version = Revision(room.Value.state), status = "accepted"
            };
            try
            {
                var transition = DomainRules.Apply(room.Value.state, seat, request, random);
                if (transition.state.version != checked(room.Value.state.version + 1))
                    throw new InvalidOperationException("Invalid transition revision.");
                room.Value.state = transition.state;
                receipt.version = Revision(transition.state);
                room.Value.events.Add(new HistoryEvent
                {
                    version = receipt.version, kind = transition.eventKind, actionId = transition.actionId
                });
            }
            catch (RuleException error)
            {
                receipt.status = "rejected";
                receipt.code = error.Code;
                receipt.message = "Command rejected. Refresh and check available choices.";
            }
            var reply = new Reply { status = receipt.status == "accepted" ? 200 : 409, body = Json.Encode(receipt) };
            Retain(room.Value, request.commandId, new Receipt { actor = actor, fingerprint = fingerprint, reply = reply });
            // Capacity is checked before the CAS; receipts and history are never evicted.
            if (await store.CompareExchange(RoomId(matchId), Encode(room.Value), room.Lock))
                return CommandReply(reply, room.Value, seat, withView);
        }
        throw new RequestError(503, "storage_busy");
    }

    private static Reply CommandReply(Reply original, RoomDocument room, int seat, bool withView)
    {
        string body = CompactReceipt(original.body);
        if (withView)
            body = Json.Encode(new CommandResult
            {
                receipt = DecodeStored<CommandReceipt>(body),
                view = Projection.ForSeat(room.state, seat)
            });
        return new Reply { status = original.status, body = body };
    }

    private static string CompactReceipt(string body)
    {
        using var document = JsonDocument.Parse(body);
        // Earlier schema-2 writers persisted stale projections alongside command receipts.
        return document.RootElement.TryGetProperty("receipt", out var receipt)
            ? Json.Encode(DecodeStored<CommandReceipt>(receipt.GetRawText())) : body;
    }

    private static void NormalizeReceipts(RoomDocument room)
    {
        foreach (var receipt in room.receipts.Values)
            receipt.reply.body = CompactReceipt(receipt.reply.body);
    }
}
