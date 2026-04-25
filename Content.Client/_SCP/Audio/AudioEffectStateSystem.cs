using Content.Client._Scp.Audio.Muffle;
using Content.Shared._Scp.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Scp.Audio;

/// <summary>
/// Reconciles client-side audio effects from desired state rather than one-shot effect application attempts.
/// </summary>
public sealed class AudioEffectStateSystem : EntitySystem
{
    [Dependency] private readonly AudioEffectsManagerSystem _effectsManager = default!;
    [Dependency] private readonly AudioMuffleSystem _muffle = default!;

    private EntityQuery<AudioEffectStateComponent> _audioStateQuery;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesOutsidePrediction = true;
        UpdatesAfter.Add(typeof(AudioMuffleSystem));

        SubscribeLocalEvent<AudioComponent, AudioGotNewEffectEvent>(OnEffectChanged);

        _audioStateQuery = GetEntityQuery<AudioEffectStateComponent>();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<AudioEffectStateComponent, AudioComponent>();
        while (query.MoveNext(out var uid, out var state, out var audio))
        {
            // Desired preset state does not change when muffle makes a source fully inaudible.
            // Keep polling sources that still have a live effect so we can tear it down immediately.
            var needsSilencePolling = state.AppliedPreset != null && _muffle.IsSilencedByMuffle((uid, audio));
            if (!state.NeedsReconcile && !needsSilencePolling)
                continue;

            Reconcile((uid, audio), state);
        }
    }

    public bool SetBaseEffect(Entity<AudioComponent> ent, ProtoId<AudioPresetPrototype>? preset)
    {
        var state = EnsureComp<AudioEffectStateComponent>(ent);
        if (state.BasePreset == preset)
            return false;

        state.BasePreset = preset;
        state.NeedsReconcile = true;
        Reconcile(ent);
        return true;
    }

    public bool SetOverrideEffect(Entity<AudioComponent> ent, ProtoId<AudioPresetPrototype>? preset)
    {
        var state = EnsureComp<AudioEffectStateComponent>(ent);
        if (state.OverridePreset == preset)
            return false;

        state.OverridePreset = preset;
        state.NeedsReconcile = true;
        Reconcile(ent);
        return true;
    }

    private void OnEffectChanged(Entity<AudioComponent> ent, ref AudioGotNewEffectEvent args)
    {
        if (!_audioStateQuery.TryComp(ent, out var state))
            return;

        state.AppliedPreset = args.Prototype;
        state.NeedsReconcile = GetTargetPreset(state) != state.AppliedPreset;
    }

    private void Reconcile(Entity<AudioComponent> ent, AudioEffectStateComponent? state = null)
    {
        if (!_audioStateQuery.Resolve(ent, ref state, false))
            return;

        // Fully silenced sources must not keep feeding a stale auxiliary effect.
        if (_muffle.IsSilencedByMuffle(ent))
        {
            if (state.AppliedPreset != null)
                _effectsManager.RemoveAllEffects(ent.AsNullable());

            return;
        }

        var targetPreset = GetTargetPreset(state);

        // Removing a stale effect must always win over readiness checks so state cannot get stuck while muted.
        if (state.AppliedPreset != targetPreset || targetPreset == null)
            _effectsManager.RemoveAllEffects(ent.AsNullable());

        if (targetPreset == null)
        {
            state.NeedsReconcile = false;
            return;
        }

        if (state.AppliedPreset != targetPreset)
            _effectsManager.TryAddEffect(ent, targetPreset.Value);

        state.NeedsReconcile = state.AppliedPreset != targetPreset;
    }

    private static ProtoId<AudioPresetPrototype>? GetTargetPreset(AudioEffectStateComponent state)
    {
        return state.OverridePreset ?? state.BasePreset;
    }
}
