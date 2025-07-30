using Content.Server._Crescent.Hardpoints;
using Content.Shared.Construction;
using Content.Shared.Construction.Components;
using Content.Shared.Popups;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Content.Shared.Popups;

namespace Content.Shared._Crescent.Hardpoints;

/// <summary>
/// This handles...
/// </summary>
public class SharedHardpointSystem : EntitySystem
{
    [Dependency] public readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] public readonly EntityLookupSystem _lookupSystem = default!;
    [Dependency] public readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<HardpointAnchorableOnlyComponent, AnchorAttemptEvent>(OnAnchorTry);
        SubscribeLocalEvent<HardpointAnchorableOnlyComponent, AnchorStateChangedEvent>(OnAnchorChange);
        SubscribeLocalEvent<HardpointAnchorableOnlyComponent, MapInitEvent>(OnMapLoad);
        SubscribeLocalEvent<HardpointComponent, AnchorStateChangedEvent>(OnHardpointAnchor);
        // Removed GridInitializeEvent handling - let the server system handle it
    }

    public void OnMapLoad(EntityUid uid, HardpointAnchorableOnlyComponent comp, ref MapInitEvent args)
    {
        if (Transform(uid).MapUid == null)
            return;
        if (TryAnchorToAnyHardpoint(uid, comp))
            return;
        Logger.Error(
            $"Hardpoint-only weapon had no hardpoint under itself at mapInit. {uid} , {MetaData(uid).EntityName}");
    }
    public void OnAnchorChange(EntityUid uid, HardpointAnchorableOnlyComponent component, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored)
            return;
        if (component.anchoredTo is null)
        {
            // Fuck my chungus life just ignore this error. Auto-generated component states can't transmit entity uids properly , SPCR 2025
            Logger.Error($"SharedHardpointSystem had a anchored entity that wasn't attached to a hardpoint!");
            return;
        }

        var gridUid = Transform(component.anchoredTo.Value).GridUid;
        if (gridUid is null)
            return;
        Deanchor(uid, component.anchoredTo.Value, gridUid.Value, component);
    }

    public void OnHardpointAnchor(EntityUid target, HardpointComponent comp, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored)
            return;
        if (comp.anchoring is null)
            return;
        _transformSystem.Unanchor(comp.anchoring.Value);
    }

    public void Deanchor(EntityUid target, EntityUid anchor, EntityUid grid, HardpointAnchorableOnlyComponent component)
    {
        if (component.anchoredTo is null)
        {
            Logger.Error($"SharedHardpointSystem had a anchored entity that wasn't attached to a hardpoint!");
            return;
        }
        var hardpointComp = Comp<HardpointComponent>(component.anchoredTo.Value);
        var hardpointUid = component.anchoredTo.Value;
        hardpointComp.anchoring = null;
        HardpointCannonDeanchoredEvent arg = new();
        arg.CannonUid = target;
        arg.gridUid = grid;
        RaiseLocalEvent(hardpointUid, arg);
        component.anchoredTo = null;
        Dirty(hardpointUid, hardpointComp);
        //Dirty(arg.CannonUid, component);
    }

    public void OnAnchorTry(EntityUid uid, HardpointAnchorableOnlyComponent component, ref AnchorAttemptEvent args)
    {
        Logger.Info($"[HardpointDebug] OnAnchorTry called for WEAPON entity {uid} (not checking hardpoint limits)");

        // This is for weapons/guns trying to anchor to hardpoints
        // We should NOT check grid hardpoint limits here - only check if there's an available hardpoint

        Logger.Info($"[HardpointDebug] Attempting to anchor weapon {uid} to any available hardpoint...");
        if (TryAnchorToAnyHardpoint(uid, component, args.User))
        {
            Logger.Info($"[HardpointDebug] Successfully found hardpoint for weapon {uid}");
            return;
        }

        Logger.Info($"[HardpointDebug] No available hardpoint found for weapon {uid}, cancelling");
        args.Cancel();
    }

    /// <summary>
    /// Checks if a hardpoint can be installed on the given grid based on limits
    /// </summary>
    private bool CanInstallOnGrid(EntityUid gridUid)
    {
        Logger.Info($"[HardpointDebug] CanInstallOnGrid called for grid {gridUid}");

        // Try to get the server-side limiter system through dependency injection
        if (_entitySystemManager.TryGetEntitySystem<GridHardpointLimiterSystem>(out var limiterSystem))
        {
            Logger.Info($"[HardpointDebug] Got limiter system successfully");
            var result = limiterSystem.CanInstall(gridUid);
            Logger.Info($"[HardpointDebug] Limiter system CanInstall returned: {result}");
            return result;
        }

        Logger.Info($"[HardpointDebug] Limiter system not available (likely client-side), using basic check");
        // Fall back to basic check if the server system isn't available (client-side)
        return DoBasicLimitCheck(gridUid);
    }

    /// <summary>
    /// Basic hardpoint limit check that works on both client and server
    /// </summary>
    private bool DoBasicLimitCheck(EntityUid gridUid)
    {
        var currentCount = GetCurrentHardpointCount(gridUid);

        // Use the same logic as the server system
        // If there are 1 or fewer hardpoints, allow up to 4 total
        // Otherwise, allow original count + 2
        var maxAllowed = currentCount <= 1 ? 4 : currentCount + 2;

        Logger.Info($"[HardpointDebug] Basic limit check - Current: {currentCount}, Max allowed: {maxAllowed}");

        return currentCount < maxAllowed;
    }

    /// <summary>
    /// Counts anchored hardpoints on a grid (shared version)
    /// </summary>
    private int GetCurrentHardpointCount(EntityUid gridUid)
    {
        var count = 0;
        var query = AllEntityQuery<HardpointComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid == gridUid && xform.Anchored)
            {
                count++;
            }
        }
        Logger.Info($"[HardpointDebug] GetCurrentHardpointCount for grid {gridUid}: {count}");
        return count;
    }

    public bool TryAnchorToAnyHardpoint(EntityUid uid, HardpointAnchorableOnlyComponent component, EntityUid? user = null)
    {
        var gridUid = Transform(uid).GridUid;
        if (gridUid is null)
            return false;
        if (!TryComp<MapGridComponent>(gridUid, out var gridComp))
            return false;
        if (!_transformSystem.TryGetGridTilePosition(uid, out var indice, gridComp))
        {
            return false;
        }

        foreach (var entity in _mapSystem.GetAnchoredEntities(new Entity<MapGridComponent>(gridUid.Value, gridComp), indice))
        {
            if (!TryComp<HardpointComponent>(entity, out var hardComp))
                continue;
            if (hardComp.anchoring is not null)
                continue;
            if ((hardComp.CompatibleTypes & component.CompatibleTypes) == 0)
                continue;
            if (hardComp.CompatibleSizes < component.CompatibleSizes)
                continue;
            AnchorEntityToHardpoint(uid, entity, component, hardComp, gridUid.Value, user);
            return true;
        }

        return false;
    }

    public void AnchorEntityToHardpoint(EntityUid target, EntityUid anchor, HardpointAnchorableOnlyComponent targetComp, HardpointComponent hardpoint, EntityUid grid, EntityUid? user = null)
    {
        // Double-check the limit right before anchoring (server-side safety)
        if (!CanInstallOnGrid(grid))
        {
            var currentCount = GetCurrentHardpointCount(grid);
            var maxAllowed = currentCount <= 1 ? 4 : currentCount + 2;

            Logger.Warning($"[HardpointDebug] BLOCKING anchor attempt - limit would be exceeded on grid {grid}");

            // Show popup to the user who tried to anchor, similar to anchorable system
            if (user != null)
            {
                _popup.PopupClient($"Cannot exceed grid limit of {maxAllowed} hardpoints!", anchor, user.Value);
            }
            return;
        }

        Logger.Info($"[HardpointDebug] Anchoring entity {target} to hardpoint {anchor} on grid {grid}");

        hardpoint.anchoring = target;
        targetComp.anchoredTo = anchor;
        _transformSystem.SetLocalRotation(target, Transform(anchor).LocalRotation);
        HardpointCannonAnchoredEvent arg = new();
        arg.cannonUid = target;
        arg.gridUid = grid;
        RaiseLocalEvent(anchor, arg);
        Dirty(anchor, hardpoint);
        //Dirty(target, targetComp);
    }
}
