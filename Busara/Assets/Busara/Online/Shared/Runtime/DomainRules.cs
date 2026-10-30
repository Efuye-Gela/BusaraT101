using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Busara.Online
{
    public static partial class DomainRules
    {
        public static TransitionResult Apply(MatchState current, int seat, OnlineCommand command, IGameRandom random)
        {
            ValidateState(current);
            Require(seat >= 0 && seat < 2 && current.seats[seat].joined, "forbidden", "You do not own a seat in this match.");
            Require(command != null && Guid.TryParse(command.commandId, out _), "invalid_command", "A unique command ID is required.");
            Require(command.expectedVersion == current.version.ToString(CultureInfo.InvariantCulture),
                "stale_version", "The match changed. Refresh the board and choose again.");
            Require(random != null, "configuration_error", "The server randomness provider is unavailable.");
            Require(command.paymentIds != null, "invalid_payment", "Provide the selected payment token IDs.");
            Require(current.phase != "Finished", "match_finished", "This match has finished.");
            MatchState state = Clone(current);
            state.notice = null;
            string actionId = state.snapshot?.actionId;
            string eventKind = "CommandApplied";
            if (Definitions.IsFull(state.ruleset) && state.phase != "Lobby")
                seat = MapControlledSeat(state, seat, command.kind);
            if (!Definitions.IsFull(state.ruleset) || !ApplyFull(state, seat, command, ref eventKind))
                ApplyOrdinary(state, seat, command, random, ref eventKind);
            if (!current.retracted && state.retracted)
                eventKind = "ActionRetracted";
            state.version = checked(current.version + 1);
            return new TransitionResult { state = state, eventKind = eventKind, actionId = actionId };
        }

        private static void ApplyOrdinary(MatchState state, int seat, OnlineCommand command, IGameRandom random,
            ref string eventKind)
        {
            switch (command.kind)
            {
                case "configure":
                    Require(state.phase == "Lobby", "wrong_phase", "Player setup is closed.");
                    string name = ValidName(command.name);
                    Require(!state.seats.Any(player => player.seat != seat && player.joined &&
                        string.Equals(player.name, name, StringComparison.OrdinalIgnoreCase)),
                        "invalid_name", "Players need different names.");
                    state.seats[seat].name = name;
                    state.seats[seat].ready = command.ready;
                    break;
                case "start":
                    Require(state.phase == "Lobby" && seat == 0 && state.seats.All(player => player.joined && player.ready),
                        "not_ready", "The host can start after both players are ready.");
                    Start(state, random);
                    eventKind = "MatchStarted";
                    break;
                case "setupPlace":
                    RequireSetup(state, seat);
                    ResourceType type = Resource(command.resourceType);
                    Require(state.seats[seat].setupRemaining.Contains(type), "invalid_resource", "That setup resource is not available.");
                    SlotState setupDestination = OwnedEmpty(state, seat, command.to);
                    Require(SetupSpace(state, seat, setupDestination.id, -1), "invalid_setup", "Setup resources cannot be adjacent on your board.");
                    Put(setupDestination, type);
                    state.seats[seat].setupRemaining.Remove(type);
                    FinishSetupPlacement(state);
                    break;
                case "setupMove":
                    RequireSetup(state, seat);
                    SlotState setupSource = OwnedPiece(state, seat, command.from);
                    SlotState setupTarget = OwnedEmpty(state, seat, command.to);
                    Require(SetupSpace(state, seat, setupTarget.id, setupSource.id),
                        "invalid_setup", "Setup resources cannot be adjacent on your board.");
                    Move(setupSource, setupTarget);
                    break;
                case "draw":
                    RequireAction(state, seat);
                    Require(state.board.Any(slot => slot.seat == seat && slot.pieceId == null),
                        "no_space", "Make space on your board before drawing.");
                    CardState card = state.deck[0];
                    state.deck.RemoveAt(0);
                    state.deck.Add(card);
                    if (state.deckRemaining == 0)
                    {
                        Shuffle(state.deck, random);
                        state.deckRemaining = state.deck.Count;
                    }
                    else
                        state.deckRemaining--;
                    Decide(state, seat, "PlaceDraw", "AfterAction");
                    state.pending.drawnCard = Copy(card);
                    break;
                case "place":
                    RequireDecision(state, seat, command, "PlaceDraw");
                    Put(OwnedEmpty(state, seat, command.to), state.pending.drawnCard.type);
                    AfterAction(state);
                    break;
                case "move":
                    RequireAction(state, seat);
                    SlotState source = OwnedPiece(state, seat, command.from);
                    SlotState destination = Empty(state, command.to);
                    Require(SharedRules.Adjacent(source.id, destination.id), "invalid_move", "Choose an adjacent empty participating space.");
                    Move(source, destination);
                    AfterAction(state);
                    break;
                case "forge":
                    ForgePair(state, seat, command.from, command.to);
                    break;
                case "forgeChain":
                    RequireExpanded(state);
                    Forge(state, seat, command.slots);
                    break;
                case "weapon":
                    Weapon(state, seat, command.slots);
                    break;
                case "weaponDiscard":
                    WeaponDiscard(state, seat, command);
                    break;
                case "trade":
                    Trade(state, seat, command);
                    break;
                case "tradeAccept":
                case "tradeReject":
                    TradeResponse(state, seat, command, command.kind == "tradeAccept");
                    break;
                case "tradeComplete":
                    TradeComplete(state, seat, command);
                    break;
                case "tradeCancel":
                    TradeCancel(state, seat, command);
                    break;
                case "abundance":
                    RequireAction(state, seat);
                    Require(state.seats[seat].kingdom == Definitions.Egolica && !state.seats[seat].revealed,
                        "unavailable_power", "That power is not available.");
                    Pay(state, seat, command.paymentIds, 0);
                    state.seats[seat].revealed = true;
                    Decide(state, seat, "Abundance", "AfterAction");
                    state.pending.remaining = 2;
                    ContinueAbundance(state);
                    break;
                case "abundancePlace":
                    RequireDecision(state, seat, command, "Abundance");
                    ResourceType addition = Resource(command.resourceType);
                    Require(ResourceStock(state, addition) > 0, "no_stock", "That resource has no remaining power stock.");
                    Put(OwnedEmpty(state, seat, command.to), addition);
                    state.pending.remaining--;
                    ContinueAbundance(state);
                    break;
                case "knowledge":
                    RequireAction(state, seat);
                    Require(state.seats[seat].kingdom == Definitions.Knowledge, "unavailable_power", "That power is not available.");
                    Pay(state, seat, command.paymentIds, 1);
                    state.seats[seat].revealed = true;
                    string known = state.seats[1 - seat].kingdom;
                    if (!state.seats[seat].knownKingdoms.Contains(known))
                        state.seats[seat].knownKingdoms.Add(known);
                    Decide(state, seat, "Knowledge", "AfterAction");
                    break;
                case "use":
                    RequireDecision(state, seat, command, "Retraction");
                    Require(CanRestorePayment(state, seat, command.paymentIds), "invalid_payment",
                        "Choose two owned virtues that remain payable after undoing the action.");
                    List<TokenState> payment = Pay(state, seat, command.paymentIds, 2);
                    state.reactionPayments.Add(new ReactionPayment { seat = seat, tokens = payment });
                    state.seats[seat].revealed = true;
                    RestoreAction(state);
                    Decide(state, seat, "RetractionNotice", "TurnEnd");
                    eventKind = "ActionRetracted";
                    break;
                case "pass":
                    RequireDecision(state, seat, command, "Retraction");
                    FinishTurn(state);
                    eventKind = "ActionResolved";
                    break;
                case "ack":
                    Require(state.pending != null && (state.pending.kind == "Knowledge" ||
                        state.pending.kind == "RetractionNotice" || state.pending.kind == "AbundanceNotice"),
                        "wrong_phase", "There is no notice to acknowledge.");
                    RequireDecision(state, seat, command, state.pending.kind);
                    if (state.pending.continuation == "TurnEnd")
                        FinishTurn(state);
                    else if (state.pending.continuation == "AfterAction")
                        AfterAction(state);
                    else
                        throw new RuleException("invalid_state", "The saved continuation is unsupported.");
                    break;
                default:
                    throw new RuleException("unsupported_command", "That action is not supported in this online match.");
            }
        }
    }
}
