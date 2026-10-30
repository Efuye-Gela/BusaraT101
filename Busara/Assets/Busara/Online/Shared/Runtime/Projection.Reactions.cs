using System.Collections.Generic;
using System.Linq;

namespace Busara.Online
{
    public static partial class Projection
    {
        private static void ReactionChoices(MatchState state, ClientView view, int viewer, string power)
        {
            DecisionView decision = view.decision;
            KingdomDefinition kingdom = KingdomCatalog.ByPower(state.pending.power);
            string opponent = state.seats[1 - viewer].name;
            switch (state.pending.window)
            {
                case PowerWindow.Necklace:
                    decision.prompt = opponent + " used " + KingdomCatalog.ByPower(state.power.power).PowerName +
                        ". Cancel its effect with " + power + "? Their cost stays paid.";
                    break;
                case PowerWindow.Dome:
                    decision.prompt = opponent + " forged a weapon. Use " + power + " to prevent your discard?";
                    break;
                case PowerWindow.TurnStart:
                    decision.prompt = opponent + "'s turn is starting. Use " + power + "?";
                    break;
                default:
                    decision.prompt = opponent + " completed an action. Use " + power + "?";
                    break;
            }
            decision.paymentCost = kingdom.Cost;
            decision.paymentOptions = state.seats[viewer].virtues.Select(DomainRules.Copy).ToList();
            if (decision.paymentOptions.Count >= kingdom.Cost)
                Add(view, "use", "Use " + power + " (pay " + kingdom.Cost + ")", payment: kingdom.Cost);
            Add(view, "pass", "Pass");
        }

        // Only virtues that survive the undo are offered, preserving the unaffordable-restore prohibition.
        private static void RetractionChoices(MatchState state, ClientView view, int viewer)
        {
            DecisionView decision = view.decision;
            int cost = KingdomCatalog.ByPower(KingdomCatalog.Retraction).Cost;
            decision.prompt = "Undo the completed opposing action for " + cost + " virtues, or pass.";
            decision.paymentCost = cost;
            List<TokenState> owned = state.seats[viewer].virtues;
            var payable = new HashSet<string>();
            foreach (List<string> combination in Combinations(owned.Select(token => token.id).ToList(), cost))
                if (DomainRules.CanRestorePayment(state, viewer, combination.ToArray(), cost))
                    payable.UnionWith(combination);
            decision.paymentOptions = owned.Where(token => payable.Contains(token.id)).Select(DomainRules.Copy).ToList();
            if (decision.paymentOptions.Count >= cost)
                Add(view, "use", "Use Retraction (pay " + cost + " virtues)", payment: cost);
            else
                decision.prompt += " No payment survives undoing this action.";
            Add(view, "pass", "Pass");
        }

        private static IEnumerable<List<string>> Combinations(List<string> ids, int size, int start = 0)
        {
            if (size == 0)
            {
                yield return new List<string>();
                yield break;
            }
            for (int index = start; index <= ids.Count - size; index++)
                foreach (List<string> rest in Combinations(ids, size - 1, index + 1))
                {
                    rest.Insert(0, ids[index]);
                    yield return rest;
                }
        }
    }
}
