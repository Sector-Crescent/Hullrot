using Content.Shared.CCVar;
using Content.Shared.Projectiles;
using Content.Shared.Random.Helpers;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Standing;
using Robust.Shared.Physics.Events;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Timing;


namespace Content.Shared.Damage.Components;

public sealed class RequireProjectileTargetSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfgManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RequireProjectileTargetComponent, PreventCollideEvent>(PreventCollide);
        SubscribeLocalEvent<RequireProjectileTargetComponent, StoodEvent>(StandingBulletHit);
        SubscribeLocalEvent<RequireProjectileTargetComponent, DownedEvent>(LayingBulletPass);
    }

    private void PreventCollide(Entity<RequireProjectileTargetComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled || !ent.Comp.Active)
            return;

        var other = args.OtherEntity;

        if (!Exists(ent) || !Exists(other))
            return;

        if (!TryComp(other, out ProjectileComponent? projectile))
            return;

        var targeted = CompOrNull<TargetedProjectileComponent>(other);
        if (targeted?.Target == ent)
            return;

        var shooter = projectile.Shooter;
        if (!shooter.HasValue || shooter.Value == EntityUid.Invalid || !Exists(shooter.Value))
        {
            args.Cancelled = true;
            return;
        }

        if (_container.IsEntityOrParentInContainer(shooter.Value))
        {
            args.Cancelled = true;
            return;
        }

        var hitChance = _cfgManager.GetCVar(CCVars.ProneMobHitChance);

        if (hitChance <= 0 || !HasComp<StandingStateComponent>(ent))
            return;

        var seed = SharedRandomExtensions.HashCodeCombine(new()
        {
            (int) _timing.CurTick.Value,
            GetNetEntity(other).Id
        });

        var rand = new System.Random(seed);

        if (hitChance < 100 && hitChance <= rand.Next(100))
            args.Cancelled = true;
    }

    private void SetActive(Entity<RequireProjectileTargetComponent> ent, bool value)
    {
        if (ent.Comp.Active == value)
            return;

        ent.Comp.Active = value;
        Dirty(ent);
    }

    private void StandingBulletHit(Entity<RequireProjectileTargetComponent> ent, ref StoodEvent args)
    {
        SetActive(ent, false);
    }

    private void LayingBulletPass(Entity<RequireProjectileTargetComponent> ent, ref DownedEvent args)
    {
        SetActive(ent, true);
    }
}
