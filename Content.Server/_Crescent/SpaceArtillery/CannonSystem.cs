using System.Linq;
using Content.Server._Crescent.SpaceArtillery;
using Content.Server.DeviceLinking.Events;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._Crescent.SpaceArtillery;
using Content.Shared._Lavaland.Weapons.Ranged.Events;
using Content.Shared.Interaction;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Map;


namespace Content.Server._Crescent;


/// <summary>
/// This handles...
/// </summary>
public sealed class CannonSystem : EntitySystem
{
    [Dependency] private readonly GunSystem _guns = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<CannonComponent, InteractEvent>(onInteract);
        SubscribeLocalEvent<CannonComponent, SignalReceivedEvent>(OnSignalReceived);
        // for updating pellets from fragmented ammo to have proper armor pen and speed!
        SubscribeLocalEvent<CannonComponent, ProjectileShotEvent>(checkPellets);

    }

    public void OnSignalReceived(Entity<CannonComponent> owner,ref SignalReceivedEvent args)
    {
        if (!TryComp(owner, out GunComponent? gunComp))
            return;
        if (args.Port == owner.Comp.SpaceArtilleryFirePort)
            TryFire((owner.Owner, owner.Comp,gunComp));
    }

    public void TryFire(Entity<CannonComponent, GunComponent> cannon)
    {
        if (!HasPropellant(cannon) || !HasAmmoLast(cannon))
            return;
        List<Entity<CannonAmmoComponent>> cannonAmmo = new List<Entity<CannonAmmoComponent>>();
        List<EntityUid> cannonPropellants = new List<EntityUid>();
        float combinedSpeed = 0f;
        foreach (EntityUid entity in cannon.Comp1.loadedEntities)
        {
            if(TryComp<CannonAmmoComponent>(entity, out var ammo))
                cannonAmmo.Add((entity, ammo));
            if (TryComp<CannonPropellantComponent>(entity, out var propellant))
            {
                cannonPropellants.Add(entity);
                combinedSpeed += propellant.SpeedAdd;
            }
        }
        float baseSpeed = combinedSpeed / cannonAmmo.Count;
        foreach (Entity<CannonAmmoComponent> ammo in cannonAmmo)
        {
            float ammoSpeed = baseSpeed * ammo.Comp.SpeedMultiplier;
            if (ammoSpeed < ammo.Comp.MinSpeed)
            {
                _throwing.TryThrow();
                continue;
            }

            if (ammoSpeed > ammo.Comp.MaxSpeed)
            {
                EntityUid projectileUid = EntityManager.SpawnEntity(ammo.Comp.fragmentationPrototype.ToString(), MapCoordinates.Nullspace, null);
                _guns.Shoot(cannon.Owner, cannon.Comp2, projectileUid, _transform.GetMoverCoordinates(cannon.Owner),_transform.GetMoverCoordinates(cannon.Owner),out var _, null, false );
            }
        }
    }

    public bool TryLoadAmmo(Entity<CannonComponent> cannon, Entity<CannonAmmoComponent> cannonAmmo, EntityUid user)
    {
        if (!HasPropellant(cannon))
            return false;
        foreach (EntityUid entity in cannon.Comp.loadedEntities)
        {
            if (!TryComp<CannonAmmoComponent>(entity, out var ammo))
                continue;
            if (ammo.canStack == false)
                return false;
        }

        return true;
    }

    public bool TryLoadPropellant(Entity<CannonComponent> cannon, Entity<CannonPropellantComponent> cannonPropellant, EntityUid user)
    {
        if (HasAmmoLast(cannon))
            return false;
        return false;
    }

    public bool HasPropellant(Entity<CannonComponent> cannon)
    {
        foreach (EntityUid loaded in cannon.Comp.loadedEntities)
        {
            if (!TryComp<CannonPropellantComponent>(loaded, out var cannonPropellant))
                continue;
            return true;
        }

        return false;
    }

    public bool HasAmmoLast(Entity<CannonComponent> cannon)
    {
        if (TryComp<CannonAmmoComponent>(cannon.Comp.loadedEntities.Last(), out var cannonAmmo))
            return true;
        return false;

    }

    public void onInteract(Entity<CannonComponent> cannon, ref InteractEvent args)
    {
        if (args.Handled)
            return;
        if (TryComp<CannonAmmoComponent>(args.Used, out var cannonAmmo) && TryLoadAmmo(cannon, (args.Used, cannonAmmo), args.User))
            args.Handled = true;
        else if (TryComp<CannonPropellantComponent>(args.Used, out var cannonPropellant) && TryLoadPropellant(cannon, (args.Used, cannonPropellant), args.User))
            args.Handled = true;
    }

}
