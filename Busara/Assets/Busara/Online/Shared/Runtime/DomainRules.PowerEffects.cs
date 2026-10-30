using System.Collections.Generic;
using System.Linq;

namespace Busara.Online
{
    // Full-ruleset effects mirroring the offline PowerEffect classes and PowerEffects helpers.
    public static partial class DomainRules
    {
        private static void ExecutePower(MatchState state)
        {
            PowerUseState use = state.power;
            state.pending = null;
            state.phase = "Action";
            string name = KingdomCatalog.ByPower(use.power).PowerName;
            if (use.cancelled)
            {
                AddNotice(state, name + " was cancelled by King's Necklace. Its activation cost remains paid.");
                CompletePower(state);
                return;
            }
            SeatState caster = state.seats[use.caster], target = state.seats[use.target];
            switch (use.power)
            {
                case KingdomCatalog.Abundance:
                    use.remaining = state.seats.Count;
                    AddStep(state);
                    return;
                case KingdomCatalog.Blessing:
                    use.copies = state.board.Where(slot => slot.seat == use.caster && slot.pieceId != null)
                        .Select(slot => slot.type).ToList();
                    use.remaining = use.copies.Count;
                    AddStep(state);
                    return;
                case KingdomCatalog.Rain:
                    use.rainSeat = 0;
                    StartRainSeat(state);
                    return;
                case KingdomCatalog.Knowledge:
                    if (!caster.knownKingdoms.Contains(target.kingdom))
                        caster.knownKingdoms.Add(target.kingdom);
                    DecidePower(state, use.caster, "Knowledge");
                    return;
                case KingdomCatalog.Invisibility when HasPiece(state, use.target) && HasEmpty(state, use.caster):
                    DecidePower(state, use.caster, "Steal");
                    return;
                case KingdomCatalog.Witchcraft:
                    DecidePower(state, use.caster, "Rearrange");
                    return;
                case KingdomCatalog.Retraction:
                    RestoreAction(state);
                    state.extraActions = 0;
                    state.retracted = true;
                    DecidePower(state, use.caster, "RetractionNotice");
                    return;
            }
            ExecuteImmediate(state, use, caster, target, name);
            CompletePower(state);
        }

        private static void ExecuteImmediate(MatchState state, PowerUseState use, SeatState caster, SeatState target,
            string name)
        {
            switch (use.power)
            {
                case KingdomCatalog.Magic:
                    state.extraActions += 2;
                    AddNotice(state, "Magic grants two additional actions this turn.");
                    break;
                case KingdomCatalog.IdentitySurfing:
                    string kingdom = caster.kingdom;
                    bool revealed = caster.revealed;
                    caster.kingdom = target.kingdom;
                    caster.revealed = target.revealed;
                    target.kingdom = kingdom;
                    target.revealed = revealed;
                    AddNotice(state, "Identity Surfing exchanged both kingdom cards.");
                    break;
                case KingdomCatalog.Imagination:
                    TokenState taken = target.virtues.FirstOrDefault(token => (int)token.type == use.virtueType);
                    // Unlike the offline quirk, a vanished virtue is never created from nothing.
                    if (taken == null)
                        AddNotice(state, "Imagination found no " + (VirtueType)use.virtueType + " virtue to take.");
                    else
                    {
                        target.virtues.Remove(taken);
                        caster.virtues.Add(taken);
                    }
                    break;
                case KingdomCatalog.Transform:
                    Require(use.exchangeIds.All(id => caster.virtues.Any(token => token.id == id)),
                        "invalid_state", "The saved exchange virtues are no longer owned.");
                    caster.virtues.RemoveAll(token => use.exchangeIds.Contains(token.id));
                    foreach (string _ in use.exchangeIds)
                        caster.virtues.Add(new TokenState { id = NewId(), type = (VirtueType)use.virtueType });
                    break;
                case KingdomCatalog.Invisibility:
                    AddNotice(state, "Invisibility found nothing to steal.");
                    break;
                case KingdomCatalog.Manipulation:
                    state.controller = use.caster;
                    AddNotice(state, caster.name + " chooses " + target.name + "'s action. Trading is not allowed.");
                    break;
                case KingdomCatalog.Time:
                    state.extraTurns.Add(use.caster);
                    AddNotice(state, caster.name + " will take an extra turn after this turn.");
                    break;
                case KingdomCatalog.Dome:
                    AddNotice(state, "Celestial Dome prevented the weapon discard.");
                    break;
                default:
                    throw new RuleException("invalid_state", "The saved power is unsupported: " + name + ".");
            }
        }

        private static bool ApplyEffectStep(MatchState state, int seat, OnlineCommand command)
        {
            switch (command.kind)
            {
                case "addResource":
                    RequireDecision(state, seat, command, "AddResource");
                    ResourceType type = Resource(command.resourceType);
                    Require(AddableTypes(state).Contains(type), "no_stock", "That resource cannot be added now.");
                    Put(OwnedEmpty(state, seat, command.to), type);
                    state.power.copies.Remove(type);
                    state.power.remaining--;
                    AddStep(state);
                    return true;
                case "removeResource":
                    RequireDecision(state, seat, command, "RemoveResource");
                    OwnedPiece(state, seat, command.to).pieceId = null;
                    state.power.remaining--;
                    RemoveStep(state);
                    return true;
                case "witchMove":
                    RequireDecision(state, seat, command, "Rearrange");
                    SlotState source = OwnedPiece(state, seat, command.from);
                    SlotState destination = Slot(state, command.to);
                    Require(destination.seat == seat && destination.id != source.id,
                        "invalid_move", "Choose another space within your kingdom.");
                    SwapOrMove(source, destination);
                    DecidePower(state, seat, "Rearrange");
                    return true;
                case "witchFinish":
                    RequireDecision(state, seat, command, "Rearrange");
                    CompletePower(state);
                    return true;
                case "steal":
                    RequireDecision(state, seat, command, "Steal");
                    Move(OwnedPiece(state, state.power.target, command.from), OwnedEmpty(state, seat, command.to));
                    state.seats[seat].virtuesHidden = true;
                    CompletePower(state);
                    return true;
                default:
                    return false;
            }
        }

        public static IEnumerable<ResourceType> AddableTypes(MatchState state)
        {
            IEnumerable<ResourceType> types = state.power.power == KingdomCatalog.Blessing
                ? state.power.copies.Distinct() : Enumerable.Range(0, 4).Select(type => (ResourceType)type);
            return types.Where(type => ResourceStock(state, type) > 0).ToList();
        }

        public static int EffectSeat(PowerUseState use) => use.power == KingdomCatalog.Rain ? use.rainSeat : use.caster;

        private static void AddStep(MatchState state)
        {
            int seat = EffectSeat(state.power);
            if (state.power.remaining == 0)
                NextEffect(state);
            else if (!HasEmpty(state, seat) || !AddableTypes(state).Any())
            {
                AddNotice(state, state.seats[seat].name + ": no more resources can be added. The rest of the effect is skipped.");
                NextEffect(state);
            }
            else
                DecidePower(state, seat, "AddResource");
        }

        private static void RemoveStep(MatchState state)
        {
            int seat = EffectSeat(state.power);
            if (state.power.remaining == 0)
                NextEffect(state);
            else if (!HasPiece(state, seat))
            {
                AddNotice(state, state.seats[seat].name + ": no resources remain to remove.");
                NextEffect(state);
            }
            else
                DecidePower(state, seat, "RemoveResource");
        }

        private static void StartRainSeat(MatchState state)
        {
            PowerUseState use = state.power;
            if (use.rainSeat >= state.seats.Count)
            {
                CompletePower(state);
                return;
            }
            use.remaining = System.Math.Abs(use.count);
            if (use.count > 0)
                AddStep(state);
            else
                RemoveStep(state);
        }

        private static void NextEffect(MatchState state)
        {
            if (state.power.power != KingdomCatalog.Rain)
            {
                CompletePower(state);
                return;
            }
            state.power.rainSeat++;
            StartRainSeat(state);
        }

        private static void DecidePower(MatchState state, int owner, string kind)
        {
            Decide(state, owner, kind, "Power");
            state.pending.power = state.power.power;
            state.pending.window = state.power.window;
            state.pending.remaining = state.power.remaining;
        }

        private static void SwapOrMove(SlotState source, SlotState destination)
        {
            string piece = destination.pieceId;
            ResourceType type = destination.type;
            destination.pieceId = source.pieceId;
            destination.type = source.type;
            source.pieceId = piece;
            source.type = type;
        }

        private static bool HasPiece(MatchState state, int seat) =>
            state.board.Any(slot => slot.seat == seat && slot.pieceId != null);

        private static bool HasEmpty(MatchState state, int seat) =>
            state.board.Any(slot => slot.seat == seat && slot.pieceId == null);
    }
}
