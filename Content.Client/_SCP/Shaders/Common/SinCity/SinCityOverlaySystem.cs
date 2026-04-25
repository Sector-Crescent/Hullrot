using Content.Shared._Scp.ScpCCVars;
using Content.Shared._Scp.Shaders;
using Content.Shared._Scp.Shaders.SinCity;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Client._Scp.Shaders.Common.SinCity;

public sealed class SinCityOverlaySystem : ComponentOverlaySystem<SinCityOverlay, SinCityOverlayComponent>
{
    [Dependency] private readonly SharedShaderStrengthSystem _shaderStrength = default!;
    [Dependency] private readonly CompatibilityModeActiveWarningSystem _compatibility = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    public override void Initialize()
    {
        base.Initialize();

        Overlay = new SinCityOverlay();

        SubscribeLocalEvent<SinCityOverlayComponent, AfterAutoHandleStateEvent>(OnAdditionalStrengthChanged);

        _cfg.OnValueChanged(ScpCCVars.SinCityToggleOverlay, ToggleSinCityOverlay);
        _cfg.OnValueChanged(ScpCCVars.SinCityStrength, SetBaseStrength);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _cfg.UnsubValueChanged(ScpCCVars.SinCityToggleOverlay, ToggleSinCityOverlay);
        _cfg.UnsubValueChanged(ScpCCVars.SinCityStrength, SetBaseStrength);
    }

    protected override void OnPlayerAttached(Entity<SinCityOverlayComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        base.OnPlayerAttached(ent, ref args);

        ToggleSinCityOverlay(_cfg.GetCVar(ScpCCVars.SinCityToggleOverlay));
        SetBaseStrength(_cfg.GetCVar(ScpCCVars.SinCityStrength));
    }

    private void OnAdditionalStrengthChanged(Entity<SinCityOverlayComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_player.LocalEntity != ent)
            return;

        Overlay.CurrentStrength = ent.Comp.CurrentStrength;
    }

    private void ToggleSinCityOverlay(bool option)
    {
        if (_compatibility.IsCompatibilityModeEnabled && !_compatibility.CompabilityUseShaders)
            return;

        Enabled = option;
        ToggleOverlay(option);
    }

    private void SetBaseStrength(int value)
    {
        var player = _player.LocalEntity;

        if (!player.HasValue)
            return;

        if (!_shaderStrength.TrySetBaseStrength<SinCityOverlayComponent>(player.Value, value, out var component))
            return;

        Overlay.CurrentStrength = component.CurrentStrength;
    }
}
