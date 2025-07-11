using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Set;

namespace Content.Shared.Damage.PainSound;

/// <summary>
/// This is used for making a pain sound whenever a player character takes enough damage.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PainSoundComponent : Component
{

    /// <summary>
    ///     The amount of total damage between <see cref="ValidDamageGroups"/> that needs to be taken before
    ///     a force say occurs.
    /// </summary>
    [DataField]
    public FixedPoint2 DamageThreshold = FixedPoint2.New(5);

    /// <summary>
    ///     A list of damage group types that are considered when checking <see cref="DamageThreshold"/>.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<DamageGroupPrototype>>? ValidDamageGroups = new()
    {
        "Brute",
        "Burn",
    };

    /// <summary>
    ///     The time enforced between pain sounds to avoid spam.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(1.0);

    public TimeSpan? NextAllowedTime = null;
}
