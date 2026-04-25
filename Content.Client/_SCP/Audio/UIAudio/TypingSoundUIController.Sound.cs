using Content.Shared._Scp.ScpCCVars;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Sources;
using Robust.Shared.Random;

namespace Content.Client._Scp.Audio.UIAudio;

public sealed partial class TypingSoundUIController
{
    public void PlayGameChatSubmit(Control control)
    {
        Play(control, TypingSoundKind.ChatSubmit, requireKeyboardFocus: false);
    }

    private void Play(Control control, TypingSoundKind kind, bool requireKeyboardFocus = true)
    {
        if (!_enabled || !_interfaceAudioEnabled)
            return;

        if (!CanPlay(control, requireKeyboardFocus))
            return;

        var specifier = GetSpecifier(kind);
        if (string.IsNullOrWhiteSpace(specifier))
            return;

        var path = ResolveSoundPath(specifier);
        if (string.IsNullOrEmpty(path))
            return;

        var source = GetOrCreateSource(path);
        if (source == null)
            return;

        PrepareSourceForPlayback(source, kind);
        source.Restart();
    }

    private bool CanPlay(Control control, bool requireKeyboardFocus)
    {
        if (control.Disposed)
            return false;

        if (requireKeyboardFocus && !control.HasKeyboardFocus())
            return false;

        return control switch
        {
            LineEdit lineEdit => lineEdit.Editable,
            TextEdit textEdit => textEdit.Editable,
            _ => false,
        };
    }

    private string GetSpecifier(TypingSoundKind kind)
    {
        return kind switch
        {
            TypingSoundKind.Paste => GetPasteSpecifier(),
            TypingSoundKind.Submit => GetSubmitSpecifier(),
            TypingSoundKind.ChatSubmit => GetChatSubmitSpecifier(),
            TypingSoundKind.Delete => string.IsNullOrWhiteSpace(_deleteSound) ? _typingSound : _deleteSound,
            _ => _typingSound,
        };
    }

    private string GetPasteSpecifier()
    {
        return string.IsNullOrWhiteSpace(_pasteSound) ? _typingSound : _pasteSound;
    }

    private string GetSubmitSpecifier()
    {
        return string.IsNullOrWhiteSpace(_submitSound) ? GetPasteSpecifier() : _submitSound;
    }

    private string GetChatSubmitSpecifier()
    {
        return _chatSubmitSoundEnabled ? GetSubmitSpecifier() : string.Empty;
    }

    private string ResolveSoundPath(string specifier)
    {
        if (string.IsNullOrWhiteSpace(specifier))
            return string.Empty;

        if (specifier.StartsWith('/'))
        {
            if (_resource.TryGetResource(specifier, out AudioResource? _))
                return specifier;

            WarnInvalidSpecifier($"path::{specifier}");
            return string.Empty;
        }

        if (!_prototype.HasIndex<SoundCollectionPrototype>(specifier))
        {
            WarnInvalidSpecifier($"collection::{specifier}");
            return string.Empty;
        }

        var collection = _prototype.Index<SoundCollectionPrototype>(specifier);
        if (collection.PickFiles.Count == 0)
        {
            WarnInvalidSpecifier($"collection::{specifier}");
            return string.Empty;
        }

        return collection.PickFiles[_random.Next(collection.PickFiles.Count)].ToString();
    }

    private IAudioSource? GetOrCreateSource(string path)
    {
        if (_sourceCache.TryGetValue(path, out var source))
            return source;

        if (!_resource.TryGetResource(path, out AudioResource? resource))
        {
            WarnInvalidSpecifier($"path::{path}");
            return null;
        }

        source = _audio.CreateAudioSource(resource);
        if (source == null)
            return null;

        source.Global = true;
        source.Gain = GetGain();
        _sourceCache[path] = source;
        return source;
    }

    private void PrepareSourceForPlayback(IAudioSource source, TypingSoundKind kind)
    {
        source.Global = true;
        source.Gain = GetGain();
        source.Pitch = GetPitch(kind);
    }

    private void UpdateSourceGain()
    {
        var gain = GetGain();
        foreach (var source in _sourceCache.Values)
        {
            source.Gain = gain;
        }
    }

    private float GetGain()
    {
        return MathF.Max(0f, BaseGain * _interfaceVolume * _typingVolume);
    }

    private float GetPitch(TypingSoundKind kind)
    {
        _ = kind;

        if (_typingVariance <= 0f)
            return 1f;

        var pitch = (float) _random.NextGaussian(1, _typingVariance);
        if (!float.IsFinite(pitch))
            return 1f;

        return MathF.Max(0.01f, pitch);
    }

    private static float SanitizeVolume(float value)
    {
        if (!float.IsFinite(value))
            return ScpCCVars.TypingSoundVolume.DefaultValue;

        return MathF.Max(0f, value);
    }

    private static float SanitizeVariance(float value)
    {
        if (!float.IsFinite(value))
            return 0f;

        return MathF.Max(0f, value);
    }

    private void WarnInvalidSpecifier(string key)
    {
        if (!_invalidSoundWarnings.Add(key))
            return;

        Log.Warning($"Invalid UI typing sound specifier '{key}'.");
    }
}
