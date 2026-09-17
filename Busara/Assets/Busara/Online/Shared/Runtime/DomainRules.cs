using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Busara.Online
{
    public static class DomainRules
    {
        public static MatchState Create(string matchId)
        {
            Require(!string.IsNullOrWhiteSpace(matchId), "invalid_match", "A match ID is required.");
            var state = new MatchState { id = matchId, version = 1 };
            for (int seat = 0; seat < 2; seat++)
                state.seats.Add(new SeatState { seat = seat, joined = seat == 0, name = seat == 0 ? "Host" : "Guest" });
            for (int id = 0; id < 32; id++)
                state.board.Add(new SlotState { id = id, seat = id % 8 < 4 ? 0 : 1 });
            return state;
        }

        public static MatchState Join(MatchState current, int seat, string name)
        {
            ValidateState(current);
            Require(current.phase == "Lobby" && seat == 1 && !current.seats[1].joined,
                "room_unavailable", "This invitation cannot claim a seat.");
            MatchState state = Clone(current);
            state.seats[seat].joined = true;
            state.seats[seat].name = ValidName(name);
            state.version++;
            return state;
        }

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
            string actionId = state.snapshot?.actionId;
            string eventKind = "CommandApplied";
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
                    RequireAction(state, seat);
                    SlotState first = OwnedPiece(state, seat, command.from);
                    SlotState second = Piece(state, command.to);
                    Require(first.id != second.id && SharedRules.Adjacent(first.id, second.id),
                        "invalid_forge", "Choose an ordered pair starting with your resource and an adjacent resource.");
                    VirtueType virtue = Definitions.Forge(first.type, second.type);
                    foreach (int recipient in new[] { first.seat, second.seat }.Distinct())
                        state.seats[recipient].virtues.Add(new TokenState { id = NewId(), type = virtue });
                    first.pieceId = null;
                    second.pieceId = null;
                    AfterAction(state);
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
                    throw new RuleException("unsupported_command", "That action is not supported in Online MVP.");
            }
            state.version = checked(current.version + 1);
            return new TransitionResult { state = state, eventKind = eventKind, actionId = actionId };
        }

        public static bool CanRestorePayment(MatchState state, int seat, string[] ids)
        {
            if (state.snapshot == null || ids == null || ids.Length != 2 || ids.Distinct().Count() != ids.Length ||
                ids.Any(id => !state.seats[seat].virtues.Any(token => token.id == id)))
                return false;
            var ledger = state.reactionPayments.Select(payment => new ReactionPayment
            {
                seat = payment.seat, tokens = payment.tokens.Select(Copy).ToList()
            }).ToList();
            ledger.Add(new ReactionPayment
            {
                seat = seat, tokens = ids.Select(id => Copy(state.seats[seat].virtues.Single(token => token.id == id))).ToList()
            });
            foreach (var group in ledger.GroupBy(payment => payment.seat))
            {
                var selected = group.SelectMany(payment => payment.tokens).ToList();
                if (selected.Select(token => token.id).Distinct().Count() != selected.Count ||
                    selected.Any(token => !state.snapshot.seats[group.Key].virtues.Any(original =>
                        original.id == token.id && original.type == token.type)))
                    return false;
            }
            return true;
        }

        public static int ResourceStock(MatchState state, ResourceType type) =>
            Math.Max(0, 20 - state.board.Count(slot => slot.pieceId != null && slot.type == type));

        public static bool SetupSpace(MatchState state, int seat, int destination, int ignoredSource) =>
            !state.board.Any(slot => slot.seat == seat && slot.pieceId != null && slot.id != ignoredSource &&
                SharedRules.Adjacent(slot.id, destination));

        private static void Start(MatchState state, IGameRandom random)
        {
            var kingdoms = new List<string> { Definitions.Egolica, Definitions.Mask, Definitions.Knowledge };
            Shuffle(kingdoms, random);
            for (int seat = 0; seat < 2; seat++)
            {
                state.seats[seat].kingdom = kingdoms[seat];
                state.seats[seat].setupRemaining = Definitions.Setup(seat).ToList();
            }
            for (int type = 0; type < 4; type++)
                for (int copy = 0; copy < 6; copy++)
                    state.deck.Add(new CardState { id = "resource-" + type + "-" + copy, type = (ResourceType)type });
            Shuffle(state.deck, random);
            state.deckRemaining = state.deck.Count;
            state.phase = "Setup";
            state.activeSeat = 0;
        }

        private static void FinishSetupPlacement(MatchState state)
        {
            if (state.seats[state.activeSeat].setupRemaining.Count != 0)
                return;
            state.activeSeat = 1 - state.activeSeat;
            if (state.seats.All(seat => seat.setupRemaining.Count == 0))
                BeginTurn(state);
        }

        private static void ContinueAbundance(MatchState state)
        {
            if (state.pending.remaining == 0)
                AfterAction(state);
            else if (!state.board.Any(slot => slot.seat == state.activeSeat && slot.pieceId == null) ||
                !Enumerable.Range(0, 4).Any(type => ResourceStock(state, (ResourceType)type) > 0))
                Decide(state, state.activeSeat, "AbundanceNotice", "AfterAction");
        }

        private static void AfterAction(MatchState state)
        {
            state.pending = null;
            int reactor = 1 - state.activeSeat;
            if (state.snapshot != null && state.seats[reactor].kingdom == Definitions.Mask &&
                state.seats[reactor].virtues.Count >= 2)
                Decide(state, reactor, "Retraction", "TurnEnd");
            else
                FinishTurn(state);
        }

        private static void FinishTurn(MatchState state)
        {
            state.pending = null;
            state.snapshot = null;
            state.reactionPayments.Clear();
            if (Win(state))
                return;
            state.activeSeat = 1 - state.activeSeat;
            BeginTurn(state);
        }

        private static bool Win(MatchState state)
        {
            foreach (int seat in new[] { state.activeSeat, 1 - state.activeSeat })
                if (SharedRules.MeetsGoals(state.seats[seat].virtues.Select(token => token.type),
                    Definitions.Goals(state.seats[seat].kingdom)))
                {
                    state.phase = "Finished";
                    state.winner = seat;
                    return true;
                }
            return false;
        }

        private static void BeginTurn(MatchState state)
        {
            // This is the existing empty-board game rule, not a disconnect timeout.
            while (!state.board.Any(slot => slot.seat == state.activeSeat && slot.pieceId != null))
            {
                state.consecutiveEmptySkips++;
                if (state.consecutiveEmptySkips > 2)
                {
                    state.phase = "Finished";
                    state.draw = true;
                    return;
                }
                if (Win(state))
                    return;
                state.activeSeat = 1 - state.activeSeat;
            }
            state.consecutiveEmptySkips = 0;
            state.phase = "Action";
            state.snapshot = Capture(state);
        }

        private static List<TokenState> Pay(MatchState state, int seat, string[] ids, int cost)
        {
            Require(ids.Length == cost && ids.Distinct().Count() == cost &&
                ids.All(id => state.seats[seat].virtues.Any(token => token.id == id)),
                "invalid_payment", "Select the exact number of distinct owned virtue tokens.");
            var paid = ids.Select(id => Copy(state.seats[seat].virtues.Single(token => token.id == id))).ToList();
            state.seats[seat].virtues.RemoveAll(token => ids.Contains(token.id));
            return paid;
        }

        private static void RestoreAction(MatchState state)
        {
            var revealed = new HashSet<string>(state.seats.Where(seat => seat.revealed).Select(seat => seat.kingdom));
            var knowledge = state.seats.Select(seat => new List<string>(seat.knownKingdoms)).ToList();
            state.board = state.snapshot.board.Select(Copy).ToList();
            state.deck = state.snapshot.deck.Select(Copy).ToList();
            state.deckRemaining = state.snapshot.deckRemaining;
            state.seats = state.snapshot.seats.Select(Copy).ToList();
            foreach (SeatState seat in state.seats)
            {
                seat.revealed |= revealed.Contains(seat.kingdom);
                seat.knownKingdoms = seat.knownKingdoms.Union(knowledge[seat.seat]).ToList();
            }
            foreach (ReactionPayment payment in state.reactionPayments)
                foreach (TokenState token in payment.tokens)
                    Require(state.seats[payment.seat].virtues.RemoveAll(item => item.id == token.id) == 1,
                        "invalid_state", "The saved reaction payment could not be reapplied.");
        }

        private static void Decide(MatchState state, int owner, string kind, string continuation)
        {
            state.phase = "Decision";
            state.pending = new PendingDecision { id = NewId(), owner = owner, kind = kind, continuation = continuation };
        }

        private static void RequireAction(MatchState state, int seat) =>
            Require(state.phase == "Action" && state.pending == null && state.activeSeat == seat,
                "wrong_phase", "Wait for your ordinary action.");

        private static void RequireSetup(MatchState state, int seat) =>
            Require(state.phase == "Setup" && state.activeSeat == seat && state.seats[seat].setupRemaining.Count > 0,
                "wrong_phase", "Wait for your resource setup turn.");

        private static void RequireDecision(MatchState state, int seat, OnlineCommand command, string kind) =>
            Require(state.phase == "Decision" && state.pending != null && state.pending.owner == seat &&
                state.pending.kind == kind && state.pending.id == command.decisionId,
                "stale_decision", "This choice is not yours or is no longer pending.");

        private static SlotState Slot(MatchState state, int id)
        {
            SlotState slot = state.board.SingleOrDefault(item => item.id == id);
            Require(slot != null, "invalid_slot", "Choose a participating board space.");
            return slot;
        }

        private static SlotState Empty(MatchState state, int id)
        {
            SlotState slot = Slot(state, id);
            Require(slot.pieceId == null, "occupied_slot", "Choose an empty space.");
            return slot;
        }

        private static SlotState OwnedEmpty(MatchState state, int seat, int id)
        {
            SlotState slot = Empty(state, id);
            Require(slot.seat == seat, "wrong_owner", "Choose a space on your board.");
            return slot;
        }

        private static SlotState Piece(MatchState state, int id)
        {
            SlotState slot = Slot(state, id);
            Require(slot.pieceId != null, "missing_resource", "Choose a resource on the board.");
            return slot;
        }

        private static SlotState OwnedPiece(MatchState state, int seat, int id)
        {
            SlotState slot = Piece(state, id);
            Require(slot.seat == seat, "wrong_owner", "Start with a resource on your board.");
            return slot;
        }

        private static ResourceType Resource(int value)
        {
            Require(value >= 0 && value < 4, "invalid_resource", "Choose a supported resource.");
            return (ResourceType)value;
        }

        private static string ValidName(string name)
        {
            string value = name?.Trim();
            Require(value != null && value.Length >= 1 && value.Length <= 32 &&
                !value.Any(char.IsControl), "invalid_name", "Use a name of 1-32 characters without control characters.");
            return value;
        }

        private static void Move(SlotState source, SlotState destination)
        {
            destination.pieceId = source.pieceId;
            destination.type = source.type;
            source.pieceId = null;
        }

        private static void Put(SlotState slot, ResourceType type) { slot.pieceId = NewId(); slot.type = type; }
        private static string NewId() => Guid.NewGuid().ToString("N");
        private static void Require(bool condition, string code, string message)
        {
            if (!condition) throw new RuleException(code, message);
        }

        private static void Shuffle<T>(List<T> items, IGameRandom random)
        {
            for (int i = 0; i < items.Count; i++)
            {
                int offset = random.Next(items.Count - i);
                Require(offset >= 0 && offset < items.Count - i, "configuration_error", "The random provider returned an invalid choice.");
                int j = i + offset;
                T value = items[i]; items[i] = items[j]; items[j] = value;
            }
        }

        public static void ValidateState(MatchState state)
        {
            Require(state != null && state.schemaVersion == 1 && state.ruleset == "busara-online-mvp-v1",
                "unsupported_state", "This saved match uses an unsupported state or ruleset version.");
            ValidateWorld(state.seats, state.board);
            Require(!string.IsNullOrWhiteSpace(state.id) && state.version >= 1 && state.activeSeat >= 0 &&
                state.activeSeat < 2 && state.winner >= -1 && state.winner <= 1,
                "invalid_state", "The saved match identity or turn is inconsistent.");
            Require(new[] { "Lobby", "Setup", "Action", "Decision", "Finished" }.Contains(state.phase),
                "invalid_state", "The saved phase is unsupported.");
            Require((state.phase == "Decision") == (state.pending != null),
                "invalid_state", "The saved decision state is inconsistent.");
            Require(state.reactionPayments != null && state.reactionPayments.All(payment =>
                payment != null && payment.seat >= 0 && payment.seat < 2 && payment.tokens != null &&
                payment.tokens.All(ValidToken)), "invalid_state", "The saved payment ledger is inconsistent.");
            if (state.phase != "Lobby")
            {
                Require(state.deck != null && state.deck.Count == 24 && state.seats.All(seat =>
                    new[] { Definitions.Egolica, Definitions.Mask, Definitions.Knowledge }.Contains(seat.kingdom)) &&
                    state.seats.Select(seat => seat.kingdom).Distinct().Count() == 2,
                    "invalid_state", "The saved definitions or deck are inconsistent.");
                Require(state.deck.All(card => card != null && !string.IsNullOrWhiteSpace(card.id) &&
                    (int)card.type >= 0 && (int)card.type < 4) &&
                    state.deck.Select(card => card.id).Distinct().Count() == 24 &&
                    state.deckRemaining >= 0 && state.deckRemaining <= 24,
                    "invalid_state", "The saved deck order or cursor is inconsistent.");
            }
            if (state.pending != null)
            {
                Require(!string.IsNullOrWhiteSpace(state.pending.id) && state.pending.owner >= 0 &&
                    state.pending.owner < 2 && new[] { "PlaceDraw", "Abundance", "Retraction", "Knowledge",
                        "RetractionNotice", "AbundanceNotice" }.Contains(state.pending.kind),
                    "invalid_state", "The saved decision is unsupported.");
                Require(state.pending.continuation == (state.pending.kind == "Retraction" ||
                    state.pending.kind == "RetractionNotice" ? "TurnEnd" : "AfterAction"),
                    "invalid_state", "The saved continuation is unsupported.");
                Require(state.pending.owner == (state.pending.kind == "Retraction" ||
                    state.pending.kind == "RetractionNotice" ? 1 - state.activeSeat : state.activeSeat),
                    "invalid_state", "The saved decision owner is inconsistent.");
                if (state.pending.kind == "PlaceDraw")
                    Require(state.pending.drawnCard != null && state.deck.Any(card =>
                        card.id == state.pending.drawnCard.id && card.type == state.pending.drawnCard.type),
                        "invalid_state", "The saved drawn card is inconsistent.");
                if (state.pending.kind == "Abundance")
                    Require(state.pending.remaining >= 1 && state.pending.remaining <= 2,
                        "invalid_state", "The saved resource continuation is inconsistent.");
            }
            if (state.phase == "Action" || state.phase == "Decision")
            {
                Require(state.snapshot != null && !string.IsNullOrWhiteSpace(state.snapshot.actionId),
                    "invalid_state", "The saved action snapshot is missing.");
                ValidateWorld(state.snapshot.seats, state.snapshot.board);
                Require(state.snapshot.deck != null && state.snapshot.deck.Count == 24 &&
                    state.snapshot.deck.All(card => card != null) && state.snapshot.deckRemaining >= 0 &&
                    state.snapshot.deckRemaining <= 24, "invalid_state", "The saved snapshot deck is inconsistent.");
            }
        }

        private static void ValidateWorld(List<SeatState> seats, List<SlotState> board)
        {
            Require(seats != null && seats.Count == 2 && seats[0]?.seat == 0 && seats[1]?.seat == 1 &&
                seats.All(seat => seat.virtues != null && seat.knownKingdoms != null && seat.setupRemaining != null &&
                    seat.virtues.All(ValidToken)), "invalid_state", "The saved players or inventories are inconsistent.");
            var tokens = seats.SelectMany(seat => seat.virtues).ToList();
            Require(tokens.Select(token => token.id).Distinct().Count() == tokens.Count,
                "invalid_state", "A saved virtue token has multiple owners.");
            Require(board != null && board.Count == 32 && board.All(slot => slot != null &&
                slot.id >= 0 && slot.id < 32 && slot.seat == (slot.id % 8 < 4 ? 0 : 1) &&
                (int)slot.type >= 0 && (int)slot.type < 4 &&
                (slot.pieceId == null || !string.IsNullOrWhiteSpace(slot.pieceId))) &&
                board.Select(slot => slot.id).Distinct().Count() == 32,
                "invalid_state", "The saved board geometry is inconsistent.");
            var pieces = board.Where(slot => slot.pieceId != null).Select(slot => slot.pieceId).ToList();
            Require(pieces.Distinct().Count() == pieces.Count, "invalid_state", "A saved resource occupies multiple spaces.");
        }

        private static bool ValidToken(TokenState token) =>
            token != null && !string.IsNullOrWhiteSpace(token.id) && (int)token.type >= 0 && (int)token.type < 6;

        public static ActionSnapshot Capture(MatchState state) => new ActionSnapshot
        {
            actionId = NewId(), seats = state.seats.Select(Copy).ToList(), board = state.board.Select(Copy).ToList(),
            deck = state.deck.Select(Copy).ToList(), deckRemaining = state.deckRemaining
        };

        public static MatchState Clone(MatchState state) => new MatchState
        {
            schemaVersion = state.schemaVersion, ruleset = state.ruleset, id = state.id, version = state.version,
            phase = state.phase, activeSeat = state.activeSeat, winner = state.winner, draw = state.draw,
            consecutiveEmptySkips = state.consecutiveEmptySkips, seats = state.seats.Select(Copy).ToList(),
            board = state.board.Select(Copy).ToList(), deck = state.deck.Select(Copy).ToList(),
            deckRemaining = state.deckRemaining,
            pending = state.pending == null ? null : new PendingDecision
            {
                id = state.pending.id, kind = state.pending.kind, owner = state.pending.owner,
                continuation = state.pending.continuation, remaining = state.pending.remaining,
                drawnCard = state.pending.drawnCard == null ? null : Copy(state.pending.drawnCard)
            },
            snapshot = state.snapshot == null ? null : new ActionSnapshot
            {
                actionId = state.snapshot.actionId, seats = state.snapshot.seats.Select(Copy).ToList(),
                board = state.snapshot.board.Select(Copy).ToList(), deck = state.snapshot.deck.Select(Copy).ToList(),
                deckRemaining = state.snapshot.deckRemaining
            },
            reactionPayments = state.reactionPayments.Select(payment => new ReactionPayment
            {
                seat = payment.seat, tokens = payment.tokens.Select(Copy).ToList()
            }).ToList()
        };

        public static SlotState Copy(SlotState slot) => new SlotState
        {
            id = slot.id, seat = slot.seat, pieceId = slot.pieceId, type = slot.type
        };
        public static TokenState Copy(TokenState token) => new TokenState { id = token.id, type = token.type };
        public static CardState Copy(CardState card) => new CardState { id = card.id, type = card.type };
        private static SeatState Copy(SeatState seat) => new SeatState
        {
            seat = seat.seat, name = seat.name, joined = seat.joined, ready = seat.ready, kingdom = seat.kingdom,
            revealed = seat.revealed, virtuesHidden = seat.virtuesHidden, virtues = seat.virtues.Select(Copy).ToList(),
            knownKingdoms = new List<string>(seat.knownKingdoms), setupRemaining = new List<ResourceType>(seat.setupRemaining)
        };
    }
}
