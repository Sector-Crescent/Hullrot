using Content.Shared._Scp.ScpCCVars;
using Content.Shared.Silicons.StationAi;
using Robust.Client.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Scp.Audio.Muffle;

public sealed partial class AudioMuffleSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly AudioEffectStateSystem _effectState = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private static readonly ProtoId<AudioPresetPrototype> MufflingEffectPreset = "ScpBehindWalls";

    private bool _isClientSideEnabled;
    private float _occlusionGainFalloff;
    private float _silentOcclusionThreshold;
    private float _minAudibleGainFactor;
    private float _muffleEffectApplyOcclusionThreshold;
    private float _muffleEffectClearOcclusionThreshold;

    private EntityQuery<StationAiHeldComponent> _aiQuery;
    private EntityQuery<AudioMuffledComponent> _audioMuffledQuery;

    #region CCvar events

    public override void Initialize()
    {
        base.Initialize();

        UpdatesOutsidePrediction = true;
        UpdatesAfter.Add(typeof(AudioSystem));

        Subs.CVar(_cfg, ScpCCVars.AudioMufflingEnabled, OnToggled, true);
        Subs.CVar(_cfg, ScpCCVars.AudioMufflingOcclusionGainFalloff, value => _occlusionGainFalloff = value, true);
        Subs.CVar(_cfg, ScpCCVars.AudioMufflingSilentOcclusionThreshold, value => _silentOcclusionThreshold = value, true);
        Subs.CVar(_cfg, ScpCCVars.AudioMufflingMinAudibleGainFactor, value => _minAudibleGainFactor = value, true);
        Subs.CVar(_cfg, ScpCCVars.AudioMufflingEffectApplyOcclusionThreshold, value => _muffleEffectApplyOcclusionThreshold = value, true);
        Subs.CVar(_cfg, ScpCCVars.AudioMufflingEffectClearOcclusionThreshold, value => _muffleEffectClearOcclusionThreshold = value, true);

        _aiQuery = GetEntityQuery<StationAiHeldComponent>();
        _audioMuffledQuery = GetEntityQuery<AudioMuffledComponent>();
        InitializeOcclusion();
    }

    public override void Shutdown()
    {
        base.Shutdown();

        ShutdownOcclusion();
    }

    #endregion

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!_isClientSideEnabled)
            return;

        IterateAudios();
    }

    /// <summary>
    /// Iterates over active audio entities and applies content-side muffling state.
    /// </summary>
    /// <remarks>
    /// This runs as a post-pass over audio because the engine audio API does not expose a cleaner content hook for
    /// effect management, and positional data becomes valid later than component creation and playback startup.
    /// </remarks>
    private void IterateAudios()
    {
        // Station AI should not have positional audio muffled away.
        if (_aiQuery.HasComp(_player.LocalEntity))
            return;

        var query = EntityQueryEnumerator<AudioEffectedComponent, AudioComponent>();
        while (query.MoveNext(out var sound, out _, out var audioComp))
        {
            if (!audioComp.Loaded || !audioComp.Started)
                continue;

            ApplyOcclusionGain(audioComp);
            UpdateMuffleEffect((sound, audioComp));
        }
    }

    /// <summary>
    /// Applies or removes the muffling effect preset based on the current occlusion value.
    /// </summary>
    private void UpdateMuffleEffect(Entity<AudioComponent> ent)
    {
        if (ent.Comp.Gain <= 0f)
        {
            TryUnMuffleSound(ent);
            return;
        }

        if (ent.Comp.Occlusion >= _silentOcclusionThreshold)
        {
            TryUnMuffleSound(ent);
            return;
        }

        var threshold = _audioMuffledQuery.HasComp(ent)
            ? _muffleEffectClearOcclusionThreshold
            : _muffleEffectApplyOcclusionThreshold;

        if (ent.Comp.Occlusion >= threshold)
            TryMuffleSound(ent);
        else
            TryUnMuffleSound(ent);
    }

    /// <summary>
    /// Applies additional gain attenuation derived from occlusion without ever restoring gain above the engine result.
    /// </summary>
    private void ApplyOcclusionGain(AudioComponent audioComp)
    {
        var occlusion = audioComp.Occlusion;
        var gainFactor = GetGainFactor(occlusion);

        var targetGain = SharedAudioSystem.VolumeToGain(audioComp.Params.Volume) * gainFactor;

        // AudioSystem may already have muted this source for distance/map/nullspace.
        // Only ever attenuate further, never restore gain above the engine's current value.
        if (audioComp.Gain > targetGain)
            audioComp.Gain = targetGain;
    }

    /// <summary>
    /// Tries to apply the muffling effect to a sound.
    /// </summary>
    public bool TryMuffleSound(Entity<AudioComponent> ent)
    {
        if (_audioMuffledQuery.HasComp(ent))
            return false;

        AddComp<AudioMuffledComponent>(ent);
        return _effectState.SetOverrideEffect(ent, MufflingEffectPreset);
    }

    /// <summary>
    /// Tries to remove the muffling effect from a sound.
    /// </summary>
    public bool TryUnMuffleSound(Entity<AudioComponent> ent, AudioMuffledComponent? muffledComponent = null)
    {
        if (!_audioMuffledQuery.Resolve(ent.Owner, ref muffledComponent, false))
            return false;

        _effectState.SetOverrideEffect(ent, null);
        RemComp<AudioMuffledComponent>(ent);

        return true;
    }

    /// <summary>
    /// Handles runtime toggling of the client-side audio muffling feature.
    /// </summary>
    private void OnToggled(bool enabled)
    {
        _isClientSideEnabled = enabled;

        if (!enabled)
            RevertChanges();
    }

    /// <summary>
    /// Restores all sounds that still have the muffling marker component.
    /// Used when the player disables the client-side muffling feature.
    /// </summary>
    private void RevertChanges()
    {
        var query = AllEntityQuery<AudioMuffledComponent, AudioComponent>();
        while (query.MoveNext(out var uid, out var muffled, out var audio))
        {
            TryUnMuffleSound((uid, audio), muffled);
        }
    }

    private float GetGainFactor(float occlusion)
    {
        float gainFactor;
        if (occlusion <= 0f)
        {
            gainFactor = 1f;
        }
        else if (occlusion >= _silentOcclusionThreshold)
        {
            gainFactor = 0f;
        }
        else
        {
            gainFactor = MathF.Exp(-occlusion * _occlusionGainFalloff);

            if (gainFactor < _minAudibleGainFactor)
                gainFactor = 0f;
        }

        return gainFactor;
    }

    public bool IsSilencedByMuffle(Entity<AudioComponent> ent)
    {
        return GetGainFactor(ent.Comp.Occlusion) <= 0f;
    }
}
