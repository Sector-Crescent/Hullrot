using Content.Shared.Traits;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcadis.Limbus;

/// <summary>
/// bait used to be believab- IS THAT THE RED MIST
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EGOManifestationComponent : Component
{
    [DataField, AutoNetworkedField]
    public SoundPathSpecifier? AmbientMusic = null;

    [DataField(required: true), AutoNetworkedField]
    public ProtoId<EGOGearPrototype> Gear = default!;

    [DataField(serverOnly: true)]
    public TraitFunction[] OnManifestFunctions { get; private set; } = Array.Empty<TraitFunction>();

    [DataField(serverOnly: true)]
    public TraitFunction[] OnDemanifestFunctions { get; private set; } = Array.Empty<TraitFunction>();
}

/// <summary>
/// organization. added to EGO manifestor
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ManifestedEGO : Component
{

    [AutoNetworkedField]
    public List<EntityUid> ManifestedItems = new();

    [AutoNetworkedField]
    public EntityUid? AudioStream = null;
}

/// <summary>
/// organization. Added to items manifested from EGO so they can be nuked later
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ManifestedGearComponent : Component;

[Prototype("EGO")]
public sealed partial class EGOGearPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    // All items spawned this way are tagged with a prototype that nukes them after you demanifest.
    // Don't even try.
    [DataField]
    public Dictionary<string, EntProtoId> Equipment = new();
}

