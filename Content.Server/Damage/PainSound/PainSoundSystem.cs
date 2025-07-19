using Content.Shared.Bed.Sleep;
using Content.Shared.Damage;
using Content.Shared.Damage.PainSound;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Damage.PainSound;

/// <inheritdoc cref="PainSoundComponent"/>
public sealed class PainSoundSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _protMan = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private AudioParams _params = AudioParams.Default.WithMaxDistance(7).WithVolume(5);
    private SoundCollectionPrototype? _painSoundMale;
    private SoundCollectionPrototype? _painSoundFemale;
    private SoundCollectionPrototype? _painSoundOther;

    public override void Initialize()
    {
        base.Initialize();

        _painSoundMale = _protMan.Index<SoundCollectionPrototype>("PainSoundsMale");
        _painSoundFemale = _protMan.Index<SoundCollectionPrototype>("PainSoundsFemale");
        _painSoundOther = _protMan.Index<SoundCollectionPrototype>("PainSoundsOther");

        SubscribeLocalEvent<PainSoundComponent, DamageChangedEvent>(OnDamageChanged, after: new[] { typeof(MobThresholdSystem) });

    }

    private void PlayPainSound(EntityUid uid, PainSoundComponent component)
    {
        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        // disallow if cooldown hasn't ended
        if (component.NextAllowedTime != null &&
            _timing.CurTime < component.NextAllowedTime)
            return;

        // set cooldown & raise event
        component.NextAllowedTime = _timing.CurTime + component.Cooldown;

        HumanoidAppearanceComponent? genderchecker = CompOrNull<HumanoidAppearanceComponent>(uid);

        if (genderchecker == null) //should never happen
            return;

        if (_painSoundMale == null || _painSoundFemale == null || _painSoundOther == null)
            return;

        if (genderchecker.Sex == Sex.Male)
            _audio.PlayPvs(_random.Pick(_painSoundMale.PickFiles).ToString(), uid, _params);
        else if (genderchecker.Sex == Sex.Female)
            _audio.PlayPvs(_random.Pick(_painSoundFemale.PickFiles).ToString(), uid, _params);
        else //(genderchecker.Sex == Sex.Intersex)
            _audio.PlayPvs(_random.Pick(_painSoundOther.PickFiles).ToString(), uid, _params);
    }

    private void OnDamageChanged(EntityUid uid, PainSoundComponent component, DamageChangedEvent args)
    {
        if (args.DamageDelta == null || !args.DamageIncreased || args.DamageDelta.GetTotal() < component.DamageThreshold)
            return;

        if (component.ValidDamageGroups != null)
        {
            var totalApplicableDamage = FixedPoint2.Zero;
            foreach (var (group, value) in args.DamageDelta.GetDamagePerGroup(_protMan))
            {
                if (!component.ValidDamageGroups.Contains(group))
                    continue;

                totalApplicableDamage += value;
            }

            if (totalApplicableDamage < component.DamageThreshold)
                return;
        }

        PlayPainSound(uid, component);
    }
}
