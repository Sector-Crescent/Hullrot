using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcadis.Limbus;

/// <summary>
/// "Erasing You, Erasing Me"
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ArayashikiComponent : Component
{
    [DataField, AutoNetworkedField]
    public float ErasurePerHit = 0.075f;

    [DataField, AutoNetworkedField]
    public float TextErasedPercentage = 0.05f;

    [DataField, AutoNetworkedField]
    public bool EraseUser = true;
}

// the slow buildup
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BeingErasedComponent : Component
{
    [DataField, AutoNetworkedField]
    public float ErasureLevel = 0f;
};

// its over
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BeingIncineratedComponent : Component
{
    [DataField, AutoNetworkedField]
    public float ErasedIntensity = 0f;
};

[Serializable, NetSerializable]

public enum ErasureVisuals : byte
{
    BeingErased,
}
