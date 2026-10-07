using System.Linq;

namespace Busara.Online
{
    public static partial class DomainRules
    {
        private static readonly string[] Windows =
            { PowerWindow.Own, PowerWindow.TurnStart, PowerWindow.AfterAction, PowerWindow.Dome };

        private static void ValidatePowerState(MatchState state)
        {
            Require(state.extraTurns != null && state.extraTurns.All(seat => seat == 0 || seat == 1) &&
                state.extraActions >= 0 && state.controller >= -1 && state.controller <= 1 &&
                state.resumeSeat >= -1 && state.resumeSeat <= 1,
                "invalid_state", "The saved turn control state is inconsistent.");
            if (!Definitions.IsFull(state.ruleset))
            {
                Require(state.power == null && state.controller == -1 && state.extraActions == 0 &&
                    state.extraTurns.Count == 0 && state.resumeSeat == -1 && !state.retracted,
                    "invalid_state", "The saved match uses powers outside its ruleset.");
                return;
            }
            Require(state.controller == -1 || state.controller == 1 - state.activeSeat,
                "invalid_state", "The saved turn controller is inconsistent.");
            PowerUseState use = state.power;
            if (use == null)
                return;
            Require(state.pending != null && !string.IsNullOrWhiteSpace(use.id) &&
                KingdomCatalog.ByPower(use.power) != null && (use.caster == 0 || use.caster == 1) &&
                use.target == 1 - use.caster && Windows.Contains(use.window) && use.exchangeIds != null &&
                use.copies != null && use.rainSeat >= 0 && use.rainSeat <= 2 && use.remaining >= 0,
                "invalid_state", "The saved power resolution is inconsistent.");
        }

        private static void ValidateFullDecision(MatchState state)
        {
            PendingDecision pending = state.pending;
            int active = state.activeSeat;
            Require(!string.IsNullOrWhiteSpace(pending.id) && pending.kind != null && pending.owner >= 0 && pending.owner < 2,
                "invalid_state", "The saved decision is unsupported.");
            bool trade = pending.kind.StartsWith("Trade");
            Require((pending.kind == "PlaceDraw") == (pending.drawnCard != null) &&
                trade == (pending.offerFrom >= 0) && trade == (pending.offerResourceType >= 0) &&
                (pending.kind == "AddResource" || pending.kind == "RemoveResource" || pending.remaining == 0) &&
                ((pending.power == null) == (pending.continuation == "AfterAction" || trade)),
                "invalid_state", "The saved decision payload is inconsistent.");
            switch (pending.kind)
            {
                case "PlaceDraw":
                    Expect(pending, active, "AfterAction", state.power == null);
                    Require(pending.drawnCard != null && state.deck.Any(card =>
                        card.id == pending.drawnCard.id && card.type == pending.drawnCard.type),
                        "invalid_state", "The saved drawn card is inconsistent.");
                    break;
                case "WeaponDiscard":
                    Expect(pending, 1 - active, "AfterAction", state.power == null && HasPiece(state, pending.owner));
                    break;
                case "TradeResponse":
                case "TradeSelect":
                case "TradeDeclined":
                    Expect(pending, pending.kind == "TradeResponse" ? 1 - active : active,
                        pending.kind == "TradeResponse" ? "TradeSelect" : pending.kind == "TradeSelect" ? "AfterAction" : "Action",
                        state.power == null && state.controller < 0);
                    ValidateOffer(state);
                    break;
                case "Reaction":
                case "Retraction":
                    ValidateReaction(state);
                    break;
                case "AddResource":
                case "RemoveResource":
                    Expect(pending, state.power == null ? -1 : EffectSeat(state.power), "Power",
                        pending.power == state.power?.power && pending.remaining == state.power.remaining &&
                        pending.remaining > 0);
                    break;
                case "Rearrange":
                case "Steal":
                case "Knowledge":
                case "RetractionNotice":
                    Expect(pending, state.power?.caster ?? -1, "Power", pending.power == state.power?.power);
                    break;
                default:
                    throw new RuleException("invalid_state", "The saved decision is unsupported.");
            }
        }

        private static void ValidateReaction(MatchState state)
        {
            PendingDecision pending = state.pending;
            bool necklace = pending.window == PowerWindow.Necklace;
            int owner = necklace ? (state.power == null ? -1 : 1 - state.power.caster) : 1 - state.activeSeat;
            Expect(pending, owner, "Window", (necklace || (pending.window != PowerWindow.Own &&
                Windows.Contains(pending.window))) && necklace == (state.power != null) &&
                pending.power == KingdomCatalog.PowerOf(state.seats[pending.owner].kingdom) &&
                (pending.kind == "Retraction") == (pending.power == KingdomCatalog.Retraction));
        }

        private static void Expect(PendingDecision pending, int owner, string continuation, bool consistent) =>
            Require(pending.owner == owner && pending.continuation == continuation && consistent,
                "invalid_state", "The saved decision owner or continuation is inconsistent.");
    }
}
