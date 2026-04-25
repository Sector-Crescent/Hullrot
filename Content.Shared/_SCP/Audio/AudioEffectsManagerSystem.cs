using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Content.Shared.GameTicking;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Shared._Scp.Audio;

/// <summary>
/// Creates, caches, and assigns audio effect auxiliaries referenced by the SCP audio-effects pipeline.
/// </summary>
/// <remarks>
/// Audio presets are represented by entities rather than raw OpenAL handles.
/// This system deduplicates them so multiple sounds that request the same preset can share one auxiliary instead of
/// creating redundant OpenAL state for every source.
/// </remarks>
public sealed class AudioEffectsManagerSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly INetManager _net = default!;

    /// <summary>
    /// Cached auxiliary entity for each preset already materialized by this system.
    /// </summary>
    /// <remarks>
    /// The cache is keyed by preset id so repeated requests for the same reverb/echo profile reuse the same auxiliary.
    /// </remarks>
    private readonly Dictionary<ProtoId<AudioPresetPrototype>, EntityUid> _cachedEffects = new ();

    /// <summary>
    /// Cancellation source for delayed server-side auxiliary assignments.
    /// </summary>
    private CancellationTokenSource _tokenSource = new();

    /// <summary>
    /// Delay used on the server to avoid assigning an auxiliary before the replicated client audio source is ready.
    /// </summary>
    private static readonly TimeSpan RaceConditionWaiting = TimeSpan.FromTicks(10L);

    /// <summary>
    /// Tracks whether creating auxiliaries/effects is currently considered safe in the running environment.
    /// </summary>
    /// <remarks>
    /// Integration tests or missing EFX support may cause effect creation to fail. Once a failure is observed, the
    /// system temporarily stops trying to create new auxiliaries until a later successful attempt resets the flag.
    /// </remarks>
    private bool _isAuxiliariesSafe = true;

    private EntityQuery<AudioComponent> _audioQuery;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => Clear());
        _audioQuery = GetEntityQuery<AudioComponent>();
    }

    /// <summary>
    /// Releases cached state created by this manager.
    /// </summary>
    public override void Shutdown()
    {
        base.Shutdown();

        Clear();
    }

    /// <summary>
    /// Clears the effect cache and cancels any delayed auxiliary assignment still queued by the server.
    /// </summary>
    private void Clear()
    {
        _cachedEffects.Clear();

        _tokenSource.Cancel();
        _tokenSource = new();
    }

    /// <summary>
    /// Attaches the auxiliary for a preset to an audio source, creating the auxiliary on demand if necessary.
    /// </summary>
    /// <param name="sound">The target audio source.</param>
    /// <param name="preset">The preset whose auxiliary should be attached.</param>
    /// <returns><see langword="true"/> if the preset was resolved and an auxiliary assignment was issued.</returns>
    /// <remarks>
    /// Server-side assignment is intentionally delayed by <see cref="RaceConditionWaiting"/>.
    /// Replicated sounds may reach the client before the backing audio source has fully completed startup, and assigning
    /// the auxiliary immediately can bind it to a placeholder source instead of the final live source.
    /// </remarks>
    public bool TryAddEffect(Entity<AudioComponent> sound, ProtoId<AudioPresetPrototype> preset)
    {
        if (!_cachedEffects.TryGetValue(preset, out var effect) && !TryCreateEffect(preset, out effect))
            return false;

        var ev = new AudioGetNewEffectAttemptEvent(preset);
        RaiseLocalEvent(sound, ref ev);

        if (ev.Cancelled)
            return false;

        if (_net.IsServer)
        {
            // Let the replicated client source finish initializing before we assign the auxiliary.
            Timer.Spawn(RaceConditionWaiting, () => SetEffect(sound, effect, preset), _tokenSource.Token);
        }
        else
        {
            SetEffect(sound, effect, preset);
        }

        return true;
    }

    private void SetEffect(Entity<AudioComponent> sound, EntityUid effect, ProtoId<AudioPresetPrototype> preset)
    {
        _audio.SetAuxiliary(sound, sound, effect);

        var ev = new AudioGotNewEffectEvent(preset);
        RaiseLocalEvent(sound, ref ev);
    }

    /// <summary>
    /// Removes a specific preset from a sound if that preset currently owns the auxiliary slot.
    /// </summary>
    /// <param name="sound">The target audio source.</param>
    /// <param name="preset">The preset expected to be attached.</param>
    /// <returns>
    /// <see langword="true"/> when the preset was attached to the sound and the auxiliary slot was cleared.
    /// </returns>
    public bool TryRemoveEffect(Entity<AudioComponent> sound, ProtoId<AudioPresetPrototype> preset)
    {
        if (!_cachedEffects.TryGetValue(preset, out var effect))
            return false;

        if (sound.Comp.Auxiliary != effect)
            return false;

        var ev = new AudioGetNewEffectAttemptEvent(preset);
        RaiseLocalEvent(sound, ref ev);

        if (ev.Cancelled)
            return false;

        RemoveAllEffects(sound.AsNullable());
        return true;
    }

    /// <summary>
    /// Clears the auxiliary slot regardless of which preset is currently attached.
    /// </summary>
    /// <param name="sound">The target audio source.</param>
    public void RemoveAllEffects(Entity<AudioComponent?> sound)
    {
        if (!_audioQuery.Resolve(sound, ref sound.Comp, false))
            return;

        _audio.SetAuxiliary(sound, sound.Comp, null);

        var ev = new AudioGotNewEffectEvent(null);
        RaiseLocalEvent(sound, ref ev);
    }

    /// <summary>
    /// Creates the effect and auxiliary entities for a preset and stores them in the local cache.
    /// </summary>
    /// <param name="preset">The preset to materialize.</param>
    /// <param name="effectStuff">Receives the auxiliary entity created for the preset.</param>
    /// <returns>
    /// <see langword="true"/> if the preset was resolved and cached successfully; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// The method may fail because the preset id is unknown, because auxiliary creation was previously marked unsafe,
    /// or because OpenAL/EFX support is unavailable in the current runtime.
    /// </remarks>
    public bool TryCreateEffect(ProtoId<AudioPresetPrototype> preset, out EntityUid effectStuff)
    {
        effectStuff = default;

        if (!_prototype.TryIndex(preset, out var prototype))
            return false;

        if (!_isAuxiliariesSafe)
            return false;

        (EntityUid Entity, AudioEffectComponent Component)? effect;
        try
        {
            effect = _audio.CreateEffect();
        }
        catch (Exception e)
        {
            Log.Info($"Encountered error {e} while creating audio effect, if you see this log in Integration Test its ok");

            _isAuxiliariesSafe = false;
            return false;
        }

        _isAuxiliariesSafe = true;
        var auxiliary = _audio.CreateAuxiliary();

        _audio.SetEffectPreset(effect.Value.Entity, effect.Value.Component, prototype);
        _audio.SetEffect(auxiliary.Entity, auxiliary.Component, effect.Value.Entity);

        if (!Exists(auxiliary.Entity))
            return false;

        if (!_cachedEffects.TryAdd(preset, auxiliary.Entity))
            return false;

        effectStuff = auxiliary.Entity;

        return true;
    }

    /// <summary>
    /// Determines whether the sound currently points at the auxiliary associated with the given preset.
    /// </summary>
    /// <param name="sound">The audio source to inspect.</param>
    /// <param name="preset">The preset to compare against.</param>
    /// <returns><see langword="true"/> if the sound is routed through the cached auxiliary for the preset.</returns>
    public bool HasEffect(Entity<AudioComponent> sound, ProtoId<AudioPresetPrototype> preset)
    {
        if (!_cachedEffects.TryGetValue(preset, out var effect))
            return false;

        return sound.Comp.Auxiliary == effect;
    }

    /// <summary>
    /// Tries to identify which cached preset, if any, currently owns the sound's auxiliary slot.
    /// </summary>
    /// <param name="sound">The audio source to inspect.</param>
    /// <param name="preset">Receives the matching preset when one is found.</param>
    /// <returns>
    /// <see langword="true"/> if the sound's auxiliary matches a preset cached by this manager.
    /// </returns>
    public bool TryGetEffect(Entity<AudioComponent> sound, [NotNullWhen(true)] out ProtoId<AudioPresetPrototype>? preset)
    {
        preset = null;

        foreach (var (storedPreset, auxUid) in _cachedEffects)
        {
            if (sound.Comp.Auxiliary != auxUid)
                continue;

            preset = storedPreset;
            return true;
        }

        return false;
    }
}

[ByRefEvent]
public record struct AudioGetNewEffectAttemptEvent(ProtoId<AudioPresetPrototype>? Prototype)
{
    public bool Cancelled;
}

[ByRefEvent]
public record struct AudioGotNewEffectEvent(ProtoId<AudioPresetPrototype>? Prototype);
