using System.Diagnostics.CodeAnalysis;
using Content.Server.Preferences.Managers;
using Content.Shared._Crescent.PersistentItems;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Preferences;
using Robust.Shared.Network;

namespace Content.Server._Crescent.PersistentItems;

/// <summary>
/// :)
/// </summary>
public sealed class PersistentItemStorageSystem : SharedPersistentItemStorageSystem
{
    [Dependency] private readonly IServerPreferencesManager _prefsManager = default!;

    // this is so fucking evil
    public void SetCharacterStoredItems(EntityUid playeruid, List<PersistentItemProfile> storedItems)
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

        var newProfile = profile.WithItemStorage(storedItems);

        _prefsManager.SetProfileNoChecks((NetUserId) user, index, newProfile);
    }

    public bool GetCharacterStoredItems(EntityUid playeruid, [NotNullWhen(true)] out List<PersistentItemProfile>? storedItems)
    {
        storedItems = new List<PersistentItemProfile>();

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

        storedItems = profile.ItemStorage;
        return true;
    }
}
