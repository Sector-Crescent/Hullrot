using Content.Shared.Customization.Systems;
using Content.Shared.Mind;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Traits;
using JetBrains.Annotations;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.Traits;

/// <summary>
///     Requires the profile to have all of the specific CharacterFlags on it
/// </summary>
[UsedImplicitly, Serializable, NetSerializable]
public sealed partial class CharacterFlagRequirement : CharacterRequirement
{
    [DataField(required: true)]
    public List<string> CharacterFlags;

    public override bool IsValid(
        JobPrototype job,
        HumanoidCharacterProfile profile,
        Dictionary<string, TimeSpan> playTimes,
        bool whitelisted,
        IPrototype prototype,
        IEntityManager entityManager,
        IPrototypeManager prototypeManager,
        IConfigurationManager configManager,
        out string? reason,
        int depth = 0,
        MindComponent? mind = null
    )
    {
        reason = Loc.GetString(
            "character-flag-requirement",
            ("inverted", Inverted)
        );

        foreach (var flag in CharacterFlags)
        {
            if (!profile.CharacterFlags.Contains(flag))
                return false;
        }

        return true;
    }
}
