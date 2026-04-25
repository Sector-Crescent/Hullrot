using Content.Shared._Scp.ScpCCVars;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Client._Scp.Audio.Echo;

/// <summary>
/// Система, накладывающая эффект эхо каждому неглобальному звуку.
/// Эффект может быть отключен игроком в настройках.
/// </summary>
public sealed class EchoEffectSystem : EntitySystem
{
    [Dependency] private readonly AudioEffectStateSystem _effectState = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private static readonly ProtoId<AudioPresetPrototype> StandardEchoEffectPreset = "Bathroom";
    private static readonly ProtoId<AudioPresetPrototype> StrongEchoEffectPreset = "SewerPipe";

    private bool _isClientSideEnabled;
    private bool _strongPresetPreferred;

    private EntityQuery<AudioComponent> _audioQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AudioEffectedComponent, ComponentStartup>(OnEffectedAudioStartup, after: [typeof(SharedAudioSystem)]);

        Subs.CVar(_cfg, ScpCCVars.EchoEnabled, OnEnabledToggled, true);
        Subs.CVar(_cfg, ScpCCVars.EchoStrongPresetPreferred, OnPreferredPresetToggled, true);

        _audioQuery = GetEntityQuery<AudioComponent>();
    }

    private void OnEffectedAudioStartup(Entity<AudioEffectedComponent> ent, ref ComponentStartup args)
    {
        if (!_isClientSideEnabled)
            return;

        if (!_audioQuery.TryComp(ent.Owner, out var audio))
            return;

        TryApplyEcho((ent.Owner, audio));
    }

    /// <summary>
    /// Пытается применить эхо к данному звуку.
    /// </summary>
    /// <param name="sound">Звук, к которому будет применен эффект.</param>
    /// <param name="preset">Пресет, если нужно выставить какой-то особенный.</param>
    /// <returns>Удалось ли обновить желаемое состояние эффекта.</returns>
    public bool TryApplyEcho(Entity<AudioComponent> sound, ProtoId<AudioPresetPrototype>? preset = null)
    {
        if (TerminatingOrDeleted(sound))
            return false;

        var clientPreferredPreset = _strongPresetPreferred ? StrongEchoEffectPreset : StandardEchoEffectPreset;
        var targetPreset = preset ?? clientPreferredPreset;

        return _effectState.SetBaseEffect(sound, targetPreset);
    }

    /// <summary>
    /// Пытается убрать эффект эхо у выбранного звука.
    /// </summary>
    public bool TryRemoveEcho(Entity<AudioComponent> sound)
    {
        if (TerminatingOrDeleted(sound))
            return false;

        return _effectState.SetBaseEffect(sound, null);
    }

    private void OnEnabledToggled(bool enabled)
    {
        _isClientSideEnabled = enabled;

        if (enabled)
            ApplyEchoToAll();
        else
            RevertChanges();
    }

    private void OnPreferredPresetToggled(bool useStrong)
    {
        _strongPresetPreferred = useStrong;
        if (!_isClientSideEnabled)
            return;

        var newPreferredPreset = useStrong ? StrongEchoEffectPreset : StandardEchoEffectPreset;
        TogglePreset(newPreferredPreset);
    }

    /// <summary>
    /// Убирает эхо у всех затронутых звуков.
    /// Вызывается при выключении эффекта эха игроком.
    /// </summary>
    private void RevertChanges()
    {
        var query = AllEntityQuery<AudioEffectedComponent, AudioComponent>();

        while (query.MoveNext(out var uid, out _, out var audio))
        {
            TryRemoveEcho((uid, audio));
        }
    }

    private void TogglePreset(ProtoId<AudioPresetPrototype> newPreferredPreset)
    {
        var query = AllEntityQuery<AudioEffectedComponent, AudioComponent>();

        while (query.MoveNext(out var uid, out _, out var audio))
        {
            TryApplyEcho((uid, audio), newPreferredPreset);
        }
    }

    private void ApplyEchoToAll()
    {
        var query = AllEntityQuery<AudioEffectedComponent, AudioComponent>();

        while (query.MoveNext(out var uid, out _, out var audio))
        {
            TryApplyEcho((uid, audio));
        }
    }
}
