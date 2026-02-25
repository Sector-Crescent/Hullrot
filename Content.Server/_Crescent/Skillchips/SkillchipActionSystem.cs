// "eris you need to stop direct pushing to master" sue me it's midnight and i'm not going to keep fucking my sleep schedule by making a new branch every time i need to do a shitchange

using Content.Shared.Containers.ItemSlots;
using Content.Shared.Doors.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Serialization.Manager;

namespace Content.Server._Crescent.Skillchips;

public sealed partial class SkillchipActionSystem : EntitySystem
{

    [Dependency] private readonly EmagSystem _emagSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SkillchipImplantHolderComponent, DoorHackActionEvent>(OnDoorHackAction);
    }

    private void OnDoorHackAction(Entity<SkillchipImplantHolderComponent> skillchip, ref DoorHackActionEvent args)
    {
        if (TryComp<AirlockComponent>(args.Target, out var _))
        {
            _emagSystem.DoEmagEffect(skillchip.Owner, args.Target);
        }
    }
}
