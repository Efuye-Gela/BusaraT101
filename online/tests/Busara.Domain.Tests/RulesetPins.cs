using Busara.Online;

namespace Busara.Domain.Tests;

internal static class RulesetPins
{
    public static MatchState Ordinary(MatchState state)
    {
        state.ruleset = Definitions.OrdinaryRuleset;
        return state;
    }
}
