namespace Content.Server._Crescent.SpaceArtillery;


/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class CannonPropellantComponent : Component
{
    [DataField]
    public float SpeedAdd = 5;
}
