using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Client._Scp.Audio;

/// <summary>
/// Stores the desired and currently applied auxiliary effect for a client audio source.
/// </summary>
[RegisterComponent]
public sealed partial class AudioEffectStateComponent : Component
{
    /// <summary>
    /// The regular effect that should be present when no higher-priority override is active.
    /// </summary>
    [ViewVariables]
    public ProtoId<AudioPresetPrototype>? BasePreset;

    /// <summary>
    /// A temporary dominant effect that overrides <see cref="BasePreset"/>, such as muffling.
    /// </summary>
    [ViewVariables]
    public ProtoId<AudioPresetPrototype>? OverridePreset;

    /// <summary>
    /// The preset currently attached to the underlying audio source.
    /// </summary>
    [ViewVariables]
    public ProtoId<AudioPresetPrototype>? AppliedPreset;

    /// <summary>
    /// Whether the desired and applied state still need to be reconciled.
    /// </summary>
    [ViewVariables]
    public bool NeedsReconcile = true;
}
