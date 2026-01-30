using Content.Shared._Crescent.Overlays;
using Content.Shared._Crescent.SpaceBiomes;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Content.Server.Polymorph;
using Content.Shared._Crescent.HadalCorrosion;
using Content.Server.Polymorph.Systems;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Collections;

namespace Content.Server._Crescent.HadalCorrosion;

public sealed class HadalCorrosionSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;

    [Dependency] private readonly ISerializationManager _serialization = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer)
            return;

        var query = EntityManager.EntityQueryEnumerator<HadalCorrosionComponent, SpaceBiomeTrackerComponent>();
        var entList = new ValueList<(EntityUid, HadalCorrosionComponent, SpaceBiomeTrackerComponent)>();
        while (query.MoveNext(out var ent, out var hadalCorrosion, out var biomeTracker))
        {
            entList.Add((ent, hadalCorrosion, biomeTracker));
        }
        foreach (var (ent, hadalCorrosion, biomeTracker) in entList)
        {
            var inHadal = biomeTracker.Biome == "default";

            if (!TryComp<CorrosionResistanceComponent>(ent, out var resistance))
                resistance = new CorrosionResistanceComponent { ResistanceMultiplier = 1f };

            var corrosionAmount = resistance.ResistanceMultiplier * 0.0002f + hadalCorrosion.CorrosionLevel;
            if (!inHadal)
                corrosionAmount = corrosionAmount * -5;

            if (EntityManager.TryGetComponent<MetaDataComponent>(ent, out var meta) && meta.EntityPrototype?.ID == "MobFleshGolemCorroded")
                corrosionAmount = corrosionAmount * -10f;

            hadalCorrosion.CorrosionLevel = Math.Min(1f, Math.Max(0f, corrosionAmount));

            var staticomp = EnsureComp<StaticOverlayComponent>(ent);
            staticomp.AdditionLevel = hadalCorrosion.CorrosionLevel;

            if (hadalCorrosion.CorrosionLevel >= 1f)
                _polymorph.PolymorphEntity(ent, "HadalCorrosion");

            Dirty(ent, hadalCorrosion);
            Dirty(ent, staticomp);
        }
    }
}
