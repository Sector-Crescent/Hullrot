using System.Threading;
using Content.Server.Database;
using Content.Server.Preferences.Managers;
using Content.Server.GameTicking;
using Content.Shared.Bank.Components;
using Content.Shared.Preferences;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Content.Server.Cargo.Components;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Mind;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.Mind.Components;

namespace Content.Server._Crescent.CharacterFlags;

public sealed partial class CharacterFlagSystem : EntitySystem
{
    [Dependency] private readonly IServerPreferencesManager _prefsManager = default!;
    [Dependency] private readonly IServerDbManager _dbManager = default!;

    private ISawmill _log = default!;

    public override void Initialize()
    {
        base.Initialize();
        _log = Logger.GetSawmill("jerryraisestheyellowflag");
    }

    // this is so fucking evil
    public void SetCharacterFlags(EntityUid playeruid, List<string> flags)
    {
        // get entity netuserid if it has a player attached
        if (!TryComp<MindContainerComponent>(playeruid, out var mindContComp) || !TryComp<MindComponent>(mindContComp.Mind, out var netComp))
            return;

        var user = netComp.Session?.UserId;

        if (user is null)
            return;

        var prefs = _prefsManager.GetPreferences((NetUserId) user);
        var character = prefs.SelectedCharacter;
        var index = prefs.IndexOfCharacter(character);

        if (character is not HumanoidCharacterProfile profile)
        {
            return;
        }

        var newProfile = profile.WithCharacterFlags(flags);

        _prefsManager.SetProfileNoChecks((NetUserId) user, index, newProfile);
        _log.Info($"Character {profile.Name} saved");
    }

    public bool GetCharacterFlags(EntityUid playeruid, [NotNullWhen(true)] out List<string>? flags)
    {
        flags = new List<string>();

        // get entity netuserid if it has a player attached
        if (!TryComp<MindContainerComponent>(playeruid, out var mindContComp) || !TryComp<MindComponent>(mindContComp.Mind, out var netComp))
            return false;

        var user = netComp.Session?.UserId;

        if (user is null)
            return false;

        var prefs = _prefsManager.GetPreferences((NetUserId) user);
        var character = prefs.SelectedCharacter;

        if (character is not HumanoidCharacterProfile profile)
        {
            return false;
        }

        flags = profile.CharacterFlags;
        return true;
    }
}
