using Content.Server._Crescent.Hullmods;
using Content.Server.DeviceLinking.Events;
using Content.Server.DeviceLinking.Systems;
using Content.Server.Factory.Components;
using Content.Server.PointCannons;
using Content.Shared._Crescent.Hardpoints;
using Content.Shared.Construction.Components;
using Content.Shared.PointCannons;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Timing;

namespace Content.Server._Crescent.Hardpoint;

/// <summary>
/// This handles...
/// </summary>
public sealed class HardpointSystem : SharedHardpointSystem
{
    [Dependency] private readonly PointCannonSystem _cannonSystem = default!;
    [Dependency] private readonly DeviceLinkSystem _signalSystem = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<HardpointComponent, HardpointCannonDeanchoredEvent>(OnCannonDeanchor);
        SubscribeLocalEvent<HardpointFixedMountComponent, SignalReceivedEvent>(OnSignalReceived);
    }
    private void OnSignalReceived(EntityUid uid, HardpointFixedMountComponent component, ref SignalReceivedEvent args)
    {
        if (!TryComp<HardpointComponent>(uid, out var hard))
            return;
        if (hard.anchoring is null)
            return;
        if (!TryComp<GunComponent>(hard.anchoring.Value, out var gun))
            return;

        var gridUid = Transform(uid).GridUid;
        if (gridUid != null && HasComp<PacifistShipHullmodComponent>(gridUid))
        {
                return;
        }

        if (args.Port == component.Trigger)
            _gun.AttemptShoot(hard.anchoring.Value, gun);
        var autoShoot = EnsureComp<AutoShootGunComponent>(hard.anchoring.Value);
        if (args.Port == component.Toggle)
            _gun.SetEnabled(hard.anchoring.Value, autoShoot, !autoShoot.Enabled);
    }

    public void OnCannonDeanchor(EntityUid uid, HardpointComponent comp, ref HardpointCannonDeanchoredEvent args)
    {
        // This is just for turret-cannons!
        if (!TryComp<PointCannonComponent>(args.CannonUid, out var compx))
            return;
        _cannonSystem.UnlinkCannon(args.CannonUid);
    }

}
