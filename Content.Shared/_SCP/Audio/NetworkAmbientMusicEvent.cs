using Content.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Scp.Audio;

[Serializable, NetSerializable]
public sealed class NetworkAmbientMusicEvent(ProtoId<AmbientMusicPrototype> prototype) : EntityEventArgs
{
    public ProtoId<AmbientMusicPrototype> Prototype { get; } = prototype;
}

[Serializable, NetSerializable]
public sealed class NetworkAmbientMusicEventStop : EntityEventArgs;
