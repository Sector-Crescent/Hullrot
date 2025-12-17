using Content.Shared.CombatMode;
using Content.Shared.Random.Rules;

namespace Content.Shared.Random;
public sealed partial class InCombatModeRule : RulesRule
{
    public override bool Check(EntityManager entManager, EntityUid uid)
    {
        if (entManager.TryGetComponent<CombatModeComponent>(uid, out var combatModeComponent) &&
            combatModeComponent.IsInCombatMode)
        {
            return true;
        }

        return false;
    }
}
