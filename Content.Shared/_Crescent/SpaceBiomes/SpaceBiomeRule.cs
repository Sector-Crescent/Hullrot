using Content.Shared.Access;
using Content.Shared.Maps;
using Content.Shared.Roles;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;
using Content.Shared.Random.Rules;
using Content.Shared._Crescent.SpaceBiomes;

namespace Content.Shared.Random;

public sealed partial class InSpaceBiomeRule : RulesRule
{
    [DataField]
    public string Biome;

    public override bool Check(EntityManager entManager, EntityUid uid)
    {
        if (entManager.TryGetComponent<SpaceBiomeTrackerComponent>(uid, out var tracker) && tracker.Biome == Biome)
        {
            return true;
        }

        return false;
    }
}
