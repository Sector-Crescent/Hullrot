using Content.Shared._Scp.Shaders;
using Content.Shared._Scp.Shaders.Vignette;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client._Scp.Shaders.Common.Vignette;

public sealed class VignetteOverlaySystem : ComponentOverlaySystem<VignetteOverlay, VignetteOverlayComponent>
{
    [Dependency] private readonly SharedShaderStrengthSystem _shaderStrength = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    public override void Initialize()
    {
        base.Initialize();

        DisableOnCompatibilityMode = false;
        Overlay = new VignetteOverlay();

        SubscribeLocalEvent<VignetteOverlayComponent, AfterAutoHandleStateEvent>(OnAdditionalStrengthChanged);
    }

    private void OnAdditionalStrengthChanged(Entity<VignetteOverlayComponent> ent,
        ref AfterAutoHandleStateEvent args)
    {
        if (_player.LocalEntity != ent)
            return;

        Overlay.CurrentStrength = ent.Comp.CurrentStrength;
    }

    protected override void OnPlayerAttached(Entity<VignetteOverlayComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        base.OnPlayerAttached(ent, ref args);

        SetBaseStrength(ent.Comp.BaseStrength);
    }

    private void SetBaseStrength(float value)
    {
        var player = _player.LocalEntity;

        if (!player.HasValue)
            return;

        if (!_shaderStrength.TrySetBaseStrength<VignetteOverlayComponent>(player.Value, value, out var component))
            return;

        Overlay.CurrentStrength = component.CurrentStrength;
    }
}
