using Content.Server.Shuttles.Systems;
using Content.Shared.Shuttles.Components;
using Robust.Shared.GameStates;

namespace Content.Server.Shuttles.Components;

/// <summary>
///  Makes heat generation, be proportional to speed of ship.
/// </summary>
[RegisterComponent, Access(typeof(ShuttleSystem))]
public sealed partial class IFFCloakSpeedComponent : Component
{
    [DataField("speedMultiplier")]
    public float SpeedMultiplier = 0.15f;
}
