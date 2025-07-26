using Content.Shared._Crescent.Hardpoints;
using Content.Shared.Construction.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Log;
using Robust.Shared.IoC;
using Robust.Shared.Timing;
using Robust.Shared.Map.Components;

namespace Content.Server._Crescent.Hardpoints;

public sealed class GridHardpointLimiterSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly IGameTiming _timing = default!; // Fixed: Use SharedTransformSystem

    public override void Initialize()
    {
        SubscribeLocalEvent<HardpointCannonAnchoredEvent>(OnCannonAnchored);
        SubscribeLocalEvent<HardpointCannonDeanchoredEvent>(OnCannonDeanchored);

        // Instead of GridInitializeEvent, let's use MapInitEvent on grids
        SubscribeLocalEvent<MapGridComponent, MapInitEvent>(OnGridMapInit);

        // IMPORTANT: Listen for hardpoint anchoring attempts
        SubscribeLocalEvent<HardpointComponent, AnchorAttemptEvent>(OnHardpointAnchorAttempt);
        SubscribeLocalEvent<HardpointComponent, UnanchorAttemptEvent>(OnHardpointUnanchorAttempt);

        // CRITICAL: Final check right before anchoring completes
        SubscribeLocalEvent<HardpointComponent, BeforeAnchoredEvent>(OnHardpointBeforeAnchored);
    }

    private void OnGridMapInit(EntityUid uid, MapGridComponent component, MapInitEvent args)
    {
        // Add a small delay to ensure all entities are loaded
        Timer.Spawn(200, () => InitializeGridLimits(uid));
    }

    private void OnHardpointAnchorAttempt(EntityUid uid, HardpointComponent component, AnchorAttemptEvent args)
    {
        Logger.Info($"[HardpointLimiter] OnHardpointAnchorAttempt called for hardpoint {uid}");

        var gridUid = Transform(uid).GridUid;
        if (gridUid == null)
        {
            Logger.Warning($"[HardpointLimiter] Hardpoint {uid} has no grid, allowing anchor");
            return;
        }

        Logger.Info($"[HardpointLimiter] Checking if hardpoint {uid} can be anchored to grid {gridUid}");

        if (!CanInstall(gridUid.Value))
        {
            Logger.Warning($"[HardpointLimiter] BLOCKING hardpoint {uid} anchor - limit exceeded on grid {gridUid}");
            args.Cancel();
            return;
        }

        Logger.Info($"[HardpointLimiter] Allowing hardpoint {uid} to anchor to grid {gridUid}");
    }

    private void OnHardpointBeforeAnchored(EntityUid uid, HardpointComponent component, BeforeAnchoredEvent args)
    {
        Logger.Info($"[HardpointLimiter] OnHardpointBeforeAnchored called for hardpoint {uid} (FINAL CHECK)");

        var gridUid = Transform(uid).GridUid;
        if (gridUid == null)
        {
            Logger.Warning($"[HardpointLimiter] Hardpoint {uid} has no grid during final check, allowing anchor");
            return;
        }

        Logger.Info($"[HardpointLimiter] FINAL CHECK: Can hardpoint {uid} be anchored to grid {gridUid}?");

        if (!CanInstall(gridUid.Value))
        {
            Logger.Warning($"[HardpointLimiter] FINAL CHECK FAILED: BLOCKING hardpoint {uid} anchor - limit exceeded on grid {gridUid}");

            // We can't cancel BeforeAnchoredEvent, so we need to unanchor it immediately
            // This is a bit hacky but necessary since BeforeAnchoredEvent isn't cancellable
            Timer.Spawn(1, () => {
                if (Exists(uid) && Transform(uid).Anchored)
                {
                    Logger.Warning($"[HardpointLimiter] Force-unanchoring hardpoint {uid} due to limit exceeded");
                    _xform.Unanchor(uid);
                }
            });
            return;
        }

        Logger.Info($"[HardpointLimiter] FINAL CHECK PASSED: Allowing hardpoint {uid} to complete anchoring to grid {gridUid}");
    }

    private void OnHardpointUnanchorAttempt(EntityUid uid, HardpointComponent component, UnanchorAttemptEvent args)
    {
        Logger.Info($"[HardpointLimiter] OnHardpointUnanchorAttempt called for hardpoint {uid} - always allowing unanchor");
        // Always allow unanchoring - we want to let people remove hardpoints
    }

    private void InitializeGridLimits(EntityUid grid)
    {
        if (!Exists(grid))
            return;

        // Create a simple server-side component for tracking
        if (!TryComp<ServerGridHardpointTrackerComponent>(grid, out var limiter))
        {
            limiter = EntityManager.AddComponent<ServerGridHardpointTrackerComponent>(grid);
        }


        var found = 0;
        var query = EntityQueryEnumerator<HardpointComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out _, out var xform))
        {
            // Only count anchored hardpoints on this specific grid
            if (xform.GridUid == grid && xform.Anchored)
                found++;
        }

        limiter.MaxHardpoints = found <= 1 ? 4 : found + 2;
        limiter.CurrentHardpoints = found;

        var gridName = TryComp<MetaDataComponent>(grid, out var meta) ? meta.EntityName : "Unknown";
        Logger.Info($"[HardpointLimiter] Grid {grid} ({gridName}) initialized with {found} hardpoints (max = {limiter.MaxHardpoints})");
    }

    private void OnCannonAnchored(HardpointCannonAnchoredEvent ev)
    {
        Logger.Info($"[HardpointLimiter] OnCannonAnchored called for cannon {ev.cannonUid} on grid {ev.gridUid}");

        if (!TryComp<ServerGridHardpointTrackerComponent>(ev.gridUid, out var comp))
        {
            Logger.Warning($"[HardpointLimiter] No tracker component found for grid {ev.gridUid}!");
            return;
        }

        // Recalculate current count to be sure
        var actualCount = GetCurrentHardpointCount(ev.gridUid);
        comp.CurrentHardpoints = actualCount;

        Logger.Info($"[HardpointLimiter] Grid {ev.gridUid} - Current: {comp.CurrentHardpoints}, Max: {comp.MaxHardpoints}");

        if (comp.CurrentHardpoints > comp.MaxHardpoints)
        {
            Logger.Warning($"[HardpointLimiter] Hardpoint limit exceeded on grid {ev.gridUid}! Current: {comp.CurrentHardpoints}, Max: {comp.MaxHardpoints}");
        }
    }

    private void OnCannonDeanchored(HardpointCannonDeanchoredEvent ev)
    {
        Logger.Info($"[HardpointLimiter] OnCannonDeanchored called for cannon {ev.CannonUid} on grid {ev.gridUid}");

        if (!TryComp<ServerGridHardpointTrackerComponent>(ev.gridUid, out var comp))
        {
            Logger.Warning($"[HardpointLimiter] No tracker component found for grid {ev.gridUid}!");
            return;
        }

        // Recalculate current count to be sure
        var actualCount = GetCurrentHardpointCount(ev.gridUid);
        comp.CurrentHardpoints = actualCount;

        Logger.Info($"[HardpointLimiter] Grid {ev.gridUid} after deanchor - Current: {comp.CurrentHardpoints}, Max: {comp.MaxHardpoints}");
    }

    public bool CanInstall(EntityUid gridUid)
    {
        Logger.Info($"[HardpointLimiter] CanInstall called for grid {gridUid}");

        if (!TryComp<ServerGridHardpointTrackerComponent>(gridUid, out var comp))
        {
            Logger.Warning($"[HardpointLimiter] No tracker component found for grid {gridUid}, allowing installation");
            return true;
        }

        var currentCount = GetCurrentHardpointCount(gridUid);
        Logger.Info($"[HardpointLimiter] Grid {gridUid} - Current count: {currentCount}, Component current: {comp.CurrentHardpoints}, Max: {comp.MaxHardpoints}");

        // Update the component with actual count
        comp.CurrentHardpoints = currentCount;

        var canInstall = currentCount < comp.MaxHardpoints;
        Logger.Info($"[HardpointLimiter] CanInstall result: {canInstall} (current: {currentCount} < max: {comp.MaxHardpoints})");

        return canInstall;
    }

    /// <summary>
    /// Counts the number of anchored hardpoints on a grid
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
        Logger.Info($"[HardpointLimiter] GetCurrentHardpointCount for grid {gridUid}: {count}");
        return count;
    }
}

/// <summary>
/// Server-side component for tracking hardpoint limits on grids
/// </summary>
[RegisterComponent]
public sealed partial class ServerGridHardpointTrackerComponent : Component
{
    [DataField("max")]
    public int MaxHardpoints = 4;

    [DataField("current")]
    public int CurrentHardpoints = 0;
}
