using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;


namespace Content.Server._Crescent.SpaceArtillery;


/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class CannonAmmoComponent : Component
{
    // a total load above this speed will cause the ammo to fragment
    [DataField]
    public float MaxSpeed = 400;

    [DataField]
    // below this the projectile will just plop
    public float MinSpeed = 100;

    [DataField]
    // If we can have multiple of the same type loaded or be loaded in combination with other ammunitions.
    // If set to true , you wnt be able to load any more ammo into the cannon , not even ammo that allows it.
    public bool canStack = false;

    [DataField]
    // Multiplier applied to the final speed. heavy projectiles would have sub 1.
    public float SpeedMultiplier = 1f;
    // Extra Penetration added by bullet speed.
    [DataField]
    public float SpeedToPenetrationRatio = 0.8f;

    [DataField]
    // prototype projectile definition.  To spawn when the max speed is above and the shell fragments
    public PrototypeIdSerializer<EntityPrototype> fragmentationPrototype;
}
