using System.Linq;
using System.Numerics;
using Content.Shared._Crescent;
using Content.Shared.Projectiles;
using Robust.Server.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Systems;

public sealed class ProjectilePhasePreventerSystem : EntitySystem
{
    [Dependency] private readonly PhysicsSystem _phys = default!;
    [Dependency] private readonly TransformSystem _trans = default!;
    [Dependency] private readonly ILogManager _logs = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<FixturesComponent> _fixturesQuery;

    private readonly HashSet<Entity<ProjectilePhasePreventComponent, ProjectileComponent>> _projectiles = new();

    private ISawmill _sawmill = default!;

    // xtra forgiveness beyond the projectile's exact movement distance. modify this if we ever raise tps opr have issues with phasing again
    private const float RaycastExtraDistance = 2f;

    // prevents tiny zero-length raycasts
    private const float MinimumTravelDistance = 0.001f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ProjectilePhasePreventComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ProjectilePhasePreventComponent, ComponentShutdown>(OnShutdown);

        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _fixturesQuery = GetEntityQuery<FixturesComponent>();

        _sawmill = _logs.GetSawmill("Phase-Prevention");
    }

    private void OnStartup(EntityUid uid, ProjectilePhasePreventComponent comp, ref ComponentStartup args)
    {
        if (!TryComp<ProjectileComponent>(uid, out var projectile))
        {
            _sawmill.Error($"Tried to initialize ProjectilePhasePreventComponent on entity without ProjectileComponent. Prototype: {MetaData(uid).EntityPrototype?.ID}");
            RemComp<ProjectilePhasePreventComponent>(uid);
            return;
        }

        comp.start = _trans.GetWorldPosition(uid);
        comp.mapId = _trans.GetMapId(uid);

        _projectiles.Add((uid, comp, projectile));
    }

    private void OnShutdown(EntityUid uid, ProjectilePhasePreventComponent comp, ref ComponentShutdown args)
    {
        if (!TryComp<ProjectileComponent>(uid, out var projectile))
            return;

        _projectiles.Remove((uid, comp, projectile));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var (owner, phase, projectile) in _projectiles)
        {
            if (TerminatingOrDeleted(owner))
                continue;

            if (!_physicsQuery.TryGetComponent(owner, out var bulletPhysics))
                continue;

            if (!_fixturesQuery.TryGetComponent(owner, out var bulletFixtures))
                continue;

            if (bulletFixtures.Fixtures.Count == 0)
                continue;

            var currentPos = _trans.GetWorldPosition(owner);
            var currentMap = _trans.GetMapId(owner);

            // Never raycast across maps
            if (currentMap != phase.mapId)
            {
                phase.start = currentPos;
                phase.mapId = currentMap;
                continue;
            }

            var previousPos = phase.start;
            var delta = currentPos - previousPos;
            var distance = delta.Length();

            if (distance <= MinimumTravelDistance)
                continue;

            var direction = delta / distance;
            var perpendicular = new Vector2(-direction.Y, direction.X);
            var rayStarts = new[]
            {
                previousPos,
            };

            var bulletFixturePair = bulletFixtures.Fixtures.First();
            var bulletFixtureKey = bulletFixturePair.Key;

            var ignoredGrid = EntityUid.Invalid;

            if (projectile.Weapon != null &&
                TryComp<TransformComponent>(projectile.Weapon, out var weaponXform) &&
                weaponXform.GridUid != null)
            {
                ignoredGrid = weaponXform.GridUid.Value;
            }

            var hitSomething = false;

            foreach (var rayStart in rayStarts)
            {
                var ray = new CollisionRay(rayStart, direction, phase.relevantBitmasks);

                foreach (var hit in _phys.IntersectRay(
                             currentMap,
                             ray,
                             distance + RaycastExtraDistance,
                             projectile.Weapon,
                             false))
                {
                    var hitEntity = hit.HitEntity;

                    if (hitEntity == owner)
                        continue;

                    if (projectile.IgnoreShooter && projectile.Shooter == hitEntity)
                        continue;

                    if (projectile.IgnoredEntities.Contains(hitEntity))
                        continue;

                    if (!TryComp<TransformComponent>(hitEntity, out var hitXform))
                        continue;

                    if (projectile.IgnoreWeaponGrid &&
                        ignoredGrid != EntityUid.Invalid &&
                        hitXform.GridUid == ignoredGrid)
                    {
                        continue;
                    }

                    if (!_physicsQuery.TryGetComponent(hitEntity, out _))
                        continue;

                    if (!_fixturesQuery.TryGetComponent(hitEntity, out var targetFixtures))
                        continue;

                    if (targetFixtures.Fixtures.Count == 0)
                        continue;

                    var targetFixturePair = targetFixtures.Fixtures.First();

                    var bulletEvent = new HullrotBulletHitEvent
                    {
                        selfEntity = owner,
                        hitEntity = hitEntity,
                        selfFixtureKey = bulletFixtureKey,
                        targetFixture = targetFixturePair.Value,
                        targetFixtureKey = targetFixturePair.Key,
                        selfPhys = bulletPhysics
                    };

                    try
                    {
                        RaiseLocalEvent(owner, ref bulletEvent, true);
                    }
                    catch (Exception e)
                    {
                        _sawmill.Error($"Failed to raise phase-prevent hit event: {e}");
                    }

                    hitSomething = true;
                    break;
                }

                if (hitSomething)
                    break;
            }

            // Update after all raycasts.
            phase.start = currentPos;
            phase.mapId = currentMap;
        }
    }
}
