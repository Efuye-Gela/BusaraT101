using System.Linq;

namespace Busara.Online
{
    // Full-ruleset activation pipeline mirroring PowerManager: validate, pay, ledger, reveal, Necklace, execute.
    public static partial class DomainRules
    {
        private static bool ApplyFull(MatchState state, int seat, OnlineCommand command, ref string eventKind)
        {
            switch (command.kind)
            {
                case "power":
                case "abundance":
                case "knowledge":
                    ActivateOwnPower(state, seat, command);
                    eventKind = "PowerUsed";
                    return true;
                case "use":
                    UseReaction(state, seat, command);
                    eventKind = "PowerUsed";
                    return true;
                case "pass":
                    PassReaction(state, seat, command);
                    eventKind = "ActionResolved";
                    return true;
                case "ack":
                    Require(state.pending != null && (state.pending.kind == "Knowledge" ||
                        state.pending.kind == "RetractionNotice"), "wrong_phase", "There is no notice to acknowledge.");
                    RequireDecision(state, seat, command, state.pending.kind);
                    CompletePower(state);
                    return true;
                default:
                    return ApplyEffectStep(state, seat, command);
            }
        }

        public static bool CanUse(MatchState state, int seat)
        {
            SeatState player = state.seats[seat];
            KingdomDefinition kingdom = KingdomCatalog.Find(player.kingdom);
            return player.virtues.Count >= kingdom.Cost && (!kingdom.HiddenOnly || !player.revealed);
        }

        // Deliberate online rule: a controller may spend the active player's virtues only when both are visible to them.
        public static bool ControllerSees(MatchState state)
        {
            if (state.controller < 0)
                return false;
            SeatState active = state.seats[state.activeSeat];
            return !active.virtuesHidden && (active.revealed ||
                state.seats[state.controller].knownKingdoms.Contains(active.kingdom));
        }

        public static int VirtueStock(MatchState state, VirtueType type) =>
            System.Math.Max(0, 12 - state.seats.Sum(seat => seat.virtues.Count(token => token.type == type)));

        private static void ActivateOwnPower(MatchState state, int seat, OnlineCommand command)
        {
            RequireAction(state, seat);
            KingdomDefinition kingdom = KingdomCatalog.Find(state.seats[seat].kingdom);
            Require(kingdom.Timing == PowerTiming.OwnTurn && CanUse(state, seat) &&
                (command.kind == "power" || command.kind == kingdom.Power ||
                    (command.kind == "knowledge" && kingdom.Power == KingdomCatalog.Knowledge)),
                "unavailable_power", "That power is not available now. Reaction powers are offered at their legal timing.");
            Require(state.controller < 0 || ControllerSees(state), "unavailable_power",
                "You cannot see this kingdom's power and virtues, so you cannot use it.");
            var use = new PowerUseState
            {
                id = NewId(), power = kingdom.Power, caster = seat, target = 1 - seat, window = PowerWindow.Own,
                virtueType = command.virtueType, count = command.count,
                exchangeIds = (command.exchangeIds ?? new string[0]).ToList()
            };
            ValidatePowerConfig(state, use, command.paymentIds);
            Pay(state, seat, command.paymentIds, kingdom.Cost);
            state.seats[seat].revealed = true;
            state.power = use;
            Commit(state);
        }

        private static void ValidatePowerConfig(MatchState state, PowerUseState use, string[] payment)
        {
            bool choosesVirtue = use.power == KingdomCatalog.Imagination || use.power == KingdomCatalog.Transform;
            Require(choosesVirtue ? use.virtueType >= 0 && use.virtueType < 6 : use.virtueType == -1,
                "invalid_power", "Choose a supported virtue type for this power only.");
            Require(use.power == KingdomCatalog.Rain ? use.count != 0 && System.Math.Abs(use.count) <= 3 : use.count == 0,
                "invalid_power", "Rain must add or remove between one and three resources.");
            Require(use.power == KingdomCatalog.Transform || use.exchangeIds.Count == 0,
                "invalid_power", "Only Transform exchanges virtues.");
            SeatState caster = state.seats[use.caster];
            if (use.power == KingdomCatalog.Imagination && !state.seats[use.target].virtuesHidden)
                Require(state.seats[use.target].virtues.Any(token => (int)token.type == use.virtueType),
                    "invalid_power", "The chosen player does not have that virtue.");
            if (use.power == KingdomCatalog.Invisibility)
                Require(HasPiece(state, use.target) && HasEmpty(state, use.caster), "invalid_power",
                    "Stealing requires an empty space on your board and a resource on the target's board.");
            if (use.power != KingdomCatalog.Transform)
                return;
            Require(use.exchangeIds.Distinct().Count() == use.exchangeIds.Count && !use.exchangeIds.Intersect(payment).Any() &&
                use.exchangeIds.All(id => caster.virtues.Any(token => token.id == id)),
                "invalid_power", "Choose exchange virtues separately from the activation cost.");
            var spending = caster.virtues.Where(token => payment.Contains(token.id) || use.exchangeIds.Contains(token.id));
            Require(VirtueStock(state, (VirtueType)use.virtueType) +
                spending.Count(token => (int)token.type == use.virtueType) >= use.exchangeIds.Count,
                "invalid_power", "The stockpile cannot supply that many virtues of the chosen type.");
        }

        private static bool OfferReaction(MatchState state, int seat, string window)
        {
            string power = KingdomCatalog.PowerOf(state.seats[seat].kingdom);
            bool timing =
                window == PowerWindow.TurnStart ? power == KingdomCatalog.Manipulation || power == KingdomCatalog.Time :
                window == PowerWindow.AfterAction ? power == KingdomCatalog.Time ||
                    (power == KingdomCatalog.Retraction && !state.retracted && state.snapshot != null) :
                window == PowerWindow.Dome ? power == KingdomCatalog.Dome : power == KingdomCatalog.Necklace;
            if (!timing || !CanUse(state, seat))
                return false;
            Decide(state, seat, power == KingdomCatalog.Retraction ? "Retraction" : "Reaction", "Window");
            state.pending.power = power;
            state.pending.window = window;
            return true;
        }

        private static void Commit(MatchState state)
        {
            if (!OfferReaction(state, 1 - state.power.caster, PowerWindow.Necklace))
                ExecutePower(state);
        }

        private static void UseReaction(MatchState state, int seat, OnlineCommand command)
        {
            RequireReaction(state, seat, command);
            string window = state.pending.window;
            KingdomDefinition kingdom = KingdomCatalog.Find(state.seats[seat].kingdom);
            Require(kingdom.Power == state.pending.power && CanUse(state, seat),
                "unavailable_power", "That power is not available now.");
            if (kingdom.Power == KingdomCatalog.Retraction)
                Require(CanRestorePayment(state, seat, command.paymentIds, kingdom.Cost), "invalid_payment",
                    "Choose two owned virtues that remain payable after undoing the action.");
            var paid = Pay(state, seat, command.paymentIds, kingdom.Cost);
            if (state.snapshot != null)
                state.reactionPayments.Add(new ReactionPayment { seat = seat, tokens = paid });
            state.seats[seat].revealed = true;
            state.pending = null;
            if (window == PowerWindow.Necklace)
            {
                state.power.cancelled = true;
                ExecutePower(state);
                return;
            }
            state.power = new PowerUseState
            {
                id = NewId(), power = kingdom.Power, caster = seat, target = 1 - seat, window = window
            };
            Commit(state);
        }

        private static void PassReaction(MatchState state, int seat, OnlineCommand command)
        {
            RequireReaction(state, seat, command);
            string window = state.pending.window;
            state.pending = null;
            state.phase = "Action";
            switch (window)
            {
                case PowerWindow.Necklace: ExecutePower(state); break;
                case PowerWindow.TurnStart: StartAction(state); break;
                case PowerWindow.AfterAction: EndAction(state); break;
                case PowerWindow.Dome: Decide(state, seat, "WeaponDiscard", "AfterAction"); break;
                default: throw new RuleException("invalid_state", "The saved reaction window is unsupported.");
            }
        }

        private static void RequireReaction(MatchState state, int seat, OnlineCommand command)
        {
            Require(state.pending != null && (state.pending.kind == "Reaction" || state.pending.kind == "Retraction"),
                "stale_decision", "This choice is not yours or is no longer pending.");
            RequireDecision(state, seat, command, state.pending.kind);
        }
    }
}
