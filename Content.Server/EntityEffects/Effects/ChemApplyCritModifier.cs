using Robust.Shared.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization;
using Content.Shared.FixedPoint;
using Content.Shared.EntityEffects;
using Content.Shared.Traits.Assorted.Components;
using Content.Shared.Traits.Assorted.Systems;

using Robust.Shared.Log;
using Content.Shared.Popups;

namespace Content.Server.EntityEffects.Effects;

public sealed partial class ChemApplyCritModifier : EntityEffect
{
    [DataField("value")] public float Value = 0f;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager _, IEntitySystemManager __)
    {
        if (Value == 0f)
            return null;

        return Loc.GetString("reagent-effect-guidebook-crit-modifier",
            ("deltasign", Value >= 0f ? 1 : -1),
            ("amount", Math.Abs((int) Value)));
    }

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args is not EntityEffectReagentArgs reagentArgs)
            return;

        if (reagentArgs.Source is null || reagentArgs.Reagent is null)
        {
            Value = 0f;
            return;
        }

        var entMan = args.EntityManager;
        var uid = args.TargetEntity;

        var comp = entMan.EnsureComponent<CritModifierComponent>(uid);
        var solution = reagentArgs.Source;
        var reagentId = reagentArgs.Reagent.ID;

        var totalBefore = solution.GetTotalPrototypeQuantity(reagentId);
        var removedThisTick = reagentArgs.Quantity;
        var totalAfter = totalBefore - removedThisTick;

        var shouldBeActive = totalAfter > FixedPoint2.New(1f);

        var desired = shouldBeActive ? Value : 0f;
        var delta = desired - comp.ChemActive;
        if (delta == 0f)
            return;

        comp.ChemActive = desired;
        entMan.Dirty(uid, comp);

        entMan.EventBus.RaiseLocalEvent(uid, new CritModifierChangedEvent());
    }
}
