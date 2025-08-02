using Content.Shared.DeviceLinking;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;


namespace Content.Shared._Crescent.SpaceArtillery;


/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class CannonComponent : Component
{
    public List<EntityUid> loadedEntities = new List<EntityUid>();
    // how many shells and powder packages we can load
    [DataField]
    public int LoadingLimit = 2;
    // the minimum speed a projectile must get on launch to actually spawn,
    // if not reached it will bloowmp on the floor infront, unfired.
    [DataField]
    public float MinimumSpeed = 45;
    // maximum speed the cannon can handle , if you go above this it will blow the cannon
    [DataField]
    public float MaximumSpeed = 450;

    [DataField("spaceArtilleryFirePort", customTypeSerializer: typeof(PrototypeIdSerializer<SinkPortPrototype>))]
    public string SpaceArtilleryFirePort = "SpaceArtilleryFire";
}
