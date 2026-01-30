using Content.Shared._Crescent.Overlays;
using Content.Shared._Crescent.SpaceBiomes;
using Robust.Shared.GameStates;
using Robust.Shared.Network;

namespace Content.Shared._Crescent.HadalCorrosion;

/// <summary>
///     get the FUCK out of the hadal like what are you doing
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HadalCorrosionComponent : Component
{
    [DataField, AutoNetworkedField]
    public float CorrosionLevel = 0.001f;
}

/// <summary>
///     okay maybe you can have a little fog. as a treat
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CorrosionResistanceComponent : Component
{
    [DataField, AutoNetworkedField]
    public float ResistanceMultiplier = 0.5f;
}
