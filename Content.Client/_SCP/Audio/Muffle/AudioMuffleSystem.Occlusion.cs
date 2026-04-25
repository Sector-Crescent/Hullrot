using System.Numerics;
using System.Collections.Concurrent;
using Content.Shared._Scp.Proximity;
using Content.Shared._Scp.ScpCCVars;
using Content.Shared.Physics;
using Content.Shared.Tag;
using Content.Shared.Wall;
using Robust.Shared;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Scp.Audio.Muffle;

public sealed partial class AudioMuffleSystem
{
    [Dependency] private readonly Robust.Client.Physics.PhysicsSystem _physics = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    /// <summary>
    /// Maximum distance that the custom occlusion ray is allowed to travel.
    /// </summary>
    private float _maxRayLength;

    /// <summary>
    /// Flat occlusion contribution added for each solid blocker intersected by the ray.
    /// </summary>
    private float _solidBaseOcclusion;

    /// <summary>
    /// Additional occlusion contribution applied per meter of penetration through solid blockers.
    /// </summary>
    private float _solidOcclusionPerMeter;

    /// <summary>
    /// Flat occlusion contribution added for each transparent blocker intersected by the ray.
    /// </summary>
    private float _transparentBaseOcclusion;

    /// <summary>
    /// Additional occlusion contribution applied per meter of penetration through transparent blockers.
    /// </summary>
    private float _transparentOcclusionPerMeter;

    /// <summary>
    /// Pool of scratch sets used to de-duplicate ray hits without allocating every update.
    /// </summary>
    private readonly ConcurrentBag<HashSet<EntityUid>> _seenPool = [];

    /// <summary>
    /// Tags that identify blockers which should attenuate like transparent materials instead of solid walls.
    /// </summary>
    private static readonly HashSet<ProtoId<TagPrototype>> TransparentOccluderTags =
    [
        "Window",
        "GlassAirlock",
        "Windoor",
        "Directional",
        "SecureWindoor",
        "SecurePlasmaWindoor",
        "SecureUraniumWindoor",
        "ScpMuffleCountsThisAsTransparent",
    ];

    private static readonly HashSet<ProtoId<TagPrototype>> NoneOccluderTags =
    [
        "ScpMuffleCountsThisAsNone",
    ];

    /// <summary>
    /// Cached query for collision and solidity classification.
    /// </summary>
    private EntityQuery<PhysicsComponent> _physicsQuery;

    /// <summary>
    /// Cached query for resolving the grid that owns a wall-mounted source.
    /// </summary>
    private EntityQuery<MapGridComponent> _gridQuery;

    /// <summary>
    /// Cached query used to detect wall-mounted sources with custom audio-occlusion behavior.
    /// </summary>
    private EntityQuery<WallMountComponent> _wallMountQuery;

    /// <summary>
    /// Initializes the custom occlusion override and binds its tuning cvars.
    /// </summary>
    private void InitializeOcclusion()
    {
        _audio.GetOcclusionOverride += Override;

        Subs.CVar(_cfg, CVars.NetPvsPriorityRange, OnPvsPriorityRangeChanged, true);
        Subs.CVar(_cfg, CVars.AudioRaycastLength, OnRaycastLengthChanged, true);
        Subs.CVar(_cfg, ScpCCVars.AudioMufflingSolidBaseOcclusion, value => _solidBaseOcclusion = value, true);
        Subs.CVar(_cfg, ScpCCVars.AudioMufflingSolidOcclusionPerMeter, value => _solidOcclusionPerMeter = value, true);
        Subs.CVar(_cfg, ScpCCVars.AudioMufflingTransparentBaseOcclusion, value => _transparentBaseOcclusion = value, true);
        Subs.CVar(_cfg, ScpCCVars.AudioMufflingTransparentOcclusionPerMeter, value => _transparentOcclusionPerMeter = value, true);

        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _wallMountQuery = GetEntityQuery<WallMountComponent>();
    }

    /// <summary>
    /// Unregisters the custom occlusion override.
    /// </summary>
    private void ShutdownOcclusion()
    {
        _audio.GetOcclusionOverride -= Override;
    }

    /// <summary>
    /// Calculates content-side occlusion by summing contributions from every blocker intersected by the ray.
    /// </summary>
    /// <param name="listener">Current world coordinates of the listener.</param>
    /// <param name="delta">Vector from listener to source.</param>
    /// <param name="distance">Distance from listener to source.</param>
    /// <param name="ignoredEnt">
    /// Entity that should be ignored by the ray, typically the source parent already handled by the engine.
    /// </param>
    /// <returns>The total SCP occlusion value accumulated along the ray.</returns>
    /// <remarks>
    /// Unlike the engine fallback, this override can distinguish solid and transparent blockers and can selectively
    /// ignore same-tile wall-mount geometry when the listener is on the intended audible side of the mount.
    /// </remarks>
    private float Override(MapCoordinates listener, Vector2 delta, float distance, EntityUid? ignoredEnt = null)
    {
        if (distance <= 0.1f)
            return 0f;

        var rayLength = MathF.Min(distance, _maxRayLength);
        var ray = new CollisionRay(listener.Position, delta / distance, _audio.OcclusionCollisionMask);

        var seen = RentSeenBuffer();

        try
        {
            if (ignoredEnt != null)
                seen.Add(ignoredEnt.Value);

            AddWallMountOcclusionExemptions(listener.Position, ignoredEnt, seen);
            var occlusion = 0f;

            foreach (var hit in _physics.IntersectRayWithPredicate(
                         listener.MapId,
                         ray,
                         seen,
                         static (uid, seenState) => !seenState.Add(uid),
                         rayLength,
                         returnOnFirstHit: false))
            {
                var blockerType = ClassifyBlocker(hit.HitEntity);
                if (blockerType == LineOfSightBlockerLevel.None)
                    continue;

                var penetration = GetPenetrationDistance(hit.HitEntity, ray);
                if (penetration <= 0f)
                    continue;

                occlusion += blockerType switch
                {
                    LineOfSightBlockerLevel.Solid =>
                        _solidBaseOcclusion + penetration * _solidOcclusionPerMeter,

                    LineOfSightBlockerLevel.Transparent =>
                        _transparentBaseOcclusion + penetration * _transparentOcclusionPerMeter,

                    _ => 0f,
                };
            }

            return occlusion;
        }
        finally
        {
            ReturnSeenBuffer(seen);
        }
    }

    /// <summary>
    /// Adds same-tile anchored occluders to the ignore set for eligible wall-mount sources, but only when the
    /// listener is on the permitted side of the mount.
    /// </summary>
    /// <param name="listenerPosition">World position of the listener.</param>
    /// <param name="source">Tracked source entity that may be a wall mount.</param>
    /// <param name="seen">Current ignore/de-duplication set used by the raycast.</param>
    private void AddWallMountOcclusionExemptions(Vector2 listenerPosition, EntityUid? source, HashSet<EntityUid> seen)
    {
        if (source == null || !_wallMountQuery.TryComp(source.Value, out var wallMount))
            return;

        var xform = Transform(source.Value);

        if (!wallMount.IgnoreAudioOcclusion)
            return;

        if (!xform.GridUid.HasValue|| !_gridQuery.TryComp(xform.GridUid, out var grid))
            return;

        if (!ShouldIgnoreWallMountOcclusion(listenerPosition, xform, wallMount))
            return;

        var tileIndices = _map.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);
        seen.UnionWith(_map.GetAnchoredEntities(xform.GridUid.Value, grid, tileIndices));
    }

    /// <summary>
    /// Uses the wall-mount interaction arc to determine whether same-tile blockers should be ignored for audio.
    /// </summary>
    /// <param name="listenerPosition">World-space listener position.</param>
    /// <param name="xform">Transform of the wall-mounted source.</param>
    /// <param name="wallMount">Wall-mount metadata describing direction and interaction arc.</param>
    /// <returns>
    /// <see langword="true"/> when the listener is on an allowed side of the mount and same-tile occluders should be
    /// ignored.
    /// </returns>
    private bool ShouldIgnoreWallMountOcclusion(
        Vector2 listenerPosition,
        TransformComponent xform,
        WallMountComponent wallMount)
    {
        if (wallMount.Arc >= Math.Tau)
            return true;

        var (worldPosition, worldRotation) = _transform.GetWorldPositionRotation(xform);
        var angle = Angle.FromWorldVec(listenerPosition - worldPosition);
        var angleDelta = (wallMount.Direction + worldRotation - angle).Reduced().FlipPositive();

        return angleDelta < wallMount.Arc / 2 || Math.Tau - angleDelta < wallMount.Arc / 2;
    }

    /// <summary>
    /// Rents a per-call scratch set used to de-duplicate ray hits by entity.
    /// </summary>
    /// <returns>A cleared set ready to be used for a single occlusion query.</returns>
    private HashSet<EntityUid> RentSeenBuffer()
    {
        if (_seenPool.TryTake(out var seen))
            return seen;

        return new HashSet<EntityUid>(64);
    }

    /// <summary>
    /// Clears and returns a scratch set to the local pool.
    /// </summary>
    /// <param name="seen">The scratch set to recycle.</param>
    private void ReturnSeenBuffer(HashSet<EntityUid> seen)
    {
        seen.Clear();
        _seenPool.Add(seen);
    }

    /// <summary>
    /// Classifies a ray hit as a solid blocker, transparent blocker, or non-blocker.
    /// </summary>
    /// <param name="uid">The hit entity to classify.</param>
    /// <returns>The blocker category that should contribute to SCP occlusion accumulation.</returns>
    private LineOfSightBlockerLevel ClassifyBlocker(EntityUid uid)
    {
        if (!_physicsQuery.TryComp(uid, out var physics) || !physics.CanCollide || !physics.Hard)
            return LineOfSightBlockerLevel.None;

        var layer = (CollisionGroup) physics.CollisionLayer;

        if (layer.HasFlag(CollisionGroup.Opaque))
            return LineOfSightBlockerLevel.Solid;

        if (_tag.HasAnyTag(uid, TransparentOccluderTags))
            return LineOfSightBlockerLevel.Transparent;

        if (_tag.HasAnyTag(uid, NoneOccluderTags))
            return LineOfSightBlockerLevel.None;

        if (layer.HasFlag(CollisionGroup.Impassable) || layer.HasFlag(CollisionGroup.InteractImpassable))
            return LineOfSightBlockerLevel.Solid;

        return LineOfSightBlockerLevel.None;
    }

    /// <summary>
    /// Approximates the distance traveled by the ray inside the entity's hard AABB.
    /// </summary>
    /// <param name="uid">The blocker whose penetration distance should be measured.</param>
    /// <param name="ray">The ray currently being cast from listener to source.</param>
    /// <returns>The approximate distance traveled inside the blocker, or zero when no stable measurement is possible.</returns>
    private float GetPenetrationDistance(EntityUid uid, CollisionRay ray)
    {
        var aabb = _physics.GetHardAABB(uid);
        if (aabb.Size.LengthSquared() <= 0f)
            return 0f;

        var worldRay = (Ray) ray;

        if (!worldRay.Intersects(aabb, out _, out var entryPoint))
            return 0f;

        var reverseOrigin = entryPoint + ray.Direction * aabb.Size.Length() * 2f;

        if (!new Ray(reverseOrigin, -ray.Direction).Intersects(aabb, out _, out var exitPoint))
            return 0f;

        return (entryPoint - exitPoint).Length();
    }

    private void OnPvsPriorityRangeChanged(float value)
    {
        var newRayCastRange = MathF.Ceiling((value / 2f) + 2);
        _cfg.SetCVar(CVars.AudioRaycastLength, newRayCastRange);
    }

    /// <summary>
    /// Updates the maximum occlusion ray length from the engine audio cvar.
    /// </summary>
    /// <param name="value">New maximum ray length in world units.</param>
    private void OnRaycastLengthChanged(float value)
    {
        _maxRayLength = value;
    }
}
