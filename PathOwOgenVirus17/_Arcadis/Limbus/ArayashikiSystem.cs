using Content.Shared.Mind.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Arcadis.Limbus;

public sealed partial class SharedArayashikiSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _phys = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearanceSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BeingIncineratedComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<BeingIncineratedComponent, ComponentRemove>(OnComponentShutdown);
    }

    public void OnComponentStartup(EntityUid ent, BeingIncineratedComponent comp, ComponentStartup args)
    {
        if (TryComp<PhysicsComponent>(ent, out var phys))
        {
            _phys.SetCanCollide(ent, false, true);
        }
        EnsureComp<AppearanceComponent>(ent);

        _appearanceSystem.SetData(ent, ErasureVisuals.BeingErased, true);
    }
    public void OnComponentShutdown(EntityUid ent, BeingIncineratedComponent comp, ComponentRemove args)
    {
        if (!Terminating(ent))
            _appearanceSystem.SetData(ent, ErasureVisuals.BeingErased, false);
    }

    public override void Update(float frameTime)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        var enumerator = EntityQueryEnumerator<BeingIncineratedComponent>();
        while (enumerator.MoveNext(out var ent, out var comp))
        {
            comp.ErasedIntensity += frameTime;
        }
    }
}
