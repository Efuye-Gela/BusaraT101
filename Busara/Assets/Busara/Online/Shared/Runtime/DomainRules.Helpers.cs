using System;
using System.Collections.Generic;
using System.Linq;

namespace Busara.Online
{
    public static partial class DomainRules
    {
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
    }
}
