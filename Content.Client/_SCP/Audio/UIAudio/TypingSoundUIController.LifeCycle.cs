using Content.Shared._Scp.ScpCCVars;
using Content.Shared.CCVar;
using Robust.Client.State;
using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Client._Scp.Audio.UIAudio;

public sealed partial class TypingSoundUIController
{
    private ConfigurationMultiSubscriptionBuilder? _cfgSub;

    private bool _enabled;
    private bool _chatSubmitSoundEnabled;
    private float _interfaceVolume;
    private bool _interfaceAudioEnabled;
    private string _typingSound = ScpCCVars.TypingSound.DefaultValue;
    private string _pasteSound = ScpCCVars.TypingPasteSound.DefaultValue;
    private string _submitSound = ScpCCVars.TypingSubmitSound.DefaultValue;
    private string _deleteSound = ScpCCVars.TypingDeleteSound.DefaultValue;
    private float _typingVolume = ScpCCVars.TypingSoundVolume.DefaultValue;
    private float _typingVariance = ScpCCVars.TypingSoundVariance.DefaultValue;

    private bool _active;

    public void OnStateEntered(State state)
    {
        Activate();
    }

    public void OnStateExited(State state)
    {
        Deactivate();
    }

    private void Activate()
    {
        if (_active)
            return;

        _active = true;
        _cfgSub = _cfg.SubscribeMultiple()
            .OnValueChanged(ScpCCVars.TypingSoundEnabled, SetEnabled, true)
            .OnValueChanged(ScpCCVars.TypingChatSubmitSoundEnabled, SetChatSubmitSound, true)
            .OnValueChanged(ScpCCVars.TypingSound, SetTypingSound, true)
            .OnValueChanged(ScpCCVars.TypingPasteSound, SetPasteSound, true)
            .OnValueChanged(ScpCCVars.TypingSubmitSound, SetSubmitSound, true)
            .OnValueChanged(ScpCCVars.TypingDeleteSound, SetDeleteSound, true)
            .OnValueChanged(ScpCCVars.TypingSoundVolume, SetTypingVolume, true)
            .OnValueChanged(ScpCCVars.TypingSoundVariance, SetTypingVariance, true)
            .OnValueChanged(CCVars.InterfaceVolume, OnInterfaceVolumeChanged, true)
            .OnValueChanged(CVars.InterfaceAudio, OnInterfaceAudioChanged, true);

        foreach (var root in UIManager.AllRoots)
        {
            TrackSubtree(root);
        }

        UIManager.OnPostDrawUIRoot += OnPostDrawRoot;
    }

    private void SetEnabled(bool value)
    {
        _enabled = value;
    }

    private void SetTypingSound(string value)
    {
        _typingSound = value;
    }

    private void SetPasteSound(string value)
    {
        _pasteSound = value;
    }

    private void SetSubmitSound(string value)
    {
        _submitSound = value;
    }

    private void SetChatSubmitSound(bool value)
    {
        _chatSubmitSoundEnabled = value;
    }

    private void SetDeleteSound(string value)
    {
        _deleteSound = value;
    }

    private void SetTypingVolume(float value)
    {
        _typingVolume = SanitizeVolume(value);
        UpdateSourceGain();
    }

    private void SetTypingVariance(float value)
    {
        _typingVariance = SanitizeVariance(value);
    }

    private void OnInterfaceVolumeChanged(float value)
    {
        _interfaceVolume = value;
        UpdateSourceGain();
    }

    private void OnInterfaceAudioChanged(bool value)
    {
        _interfaceAudioEnabled = value;
    }

    private void Deactivate()
    {
        if (!_active)
            return;

        _active = false;
        UIManager.OnPostDrawUIRoot -= OnPostDrawRoot;
        _cfgSub?.Dispose();
        _cfgSub = null;

        foreach (var (lineEdit, tracking) in _trackedLineEdits)
        {
            lineEdit.OnKeyBindDown -= tracking.KeyBindDown;
            lineEdit.OnTextChanged -= tracking.TextChanged;
            lineEdit.OnTextTyped -= tracking.TextTyped;
            lineEdit.OnTextRemoved -= tracking.TextRemoved;
            lineEdit.OnTextEntered -= tracking.TextEntered;
        }
        _trackedLineEdits.Clear();

        foreach (var (textEdit, tracking) in _trackedTextEdits)
        {
            textEdit.OnKeyBindDown -= tracking.KeyBindDown;
            textEdit.OnTextChanged -= tracking.TextChanged;
        }
        _trackedTextEdits.Clear();

        foreach (var (control, tracking) in _trackedControls)
        {
            control.OnChildAdded -= tracking.ChildAdded;
            control.OnChildRemoved -= tracking.ChildRemoved;
        }
        _trackedControls.Clear();

        foreach (var source in _sourceCache.Values)
        {
            source.Dispose();
        }
        _sourceCache.Clear();
    }
}
