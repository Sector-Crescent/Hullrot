using Content.Shared._Scp.Shaders.SinCity;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Scp.Shaders.Common.SinCity;

public sealed class SinCityOverlay : Overlay
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;

    private const string SettingsScaleParameter = "settings_scale";
    private readonly EntityQuery<EyeComponent> _eyeQuery;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    private readonly ShaderInstance _shader;
    private static readonly ProtoId<ShaderPrototype> ShaderProtoId = "Scp096Static";

    public float CurrentStrength = SinCityOverlayComponent.DefaultBaseStrength;

    public SinCityOverlay()
    {
        IoCManager.InjectDependencies(this);

        _eyeQuery = _entManager.GetEntityQuery<EyeComponent>();
        _shader = _prototype.Index(ShaderProtoId).InstanceUnique();

        ZIndex = 20;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (CurrentStrength <= 0f)
            return false;

        var playerEntity = _player.LocalEntity;

        if (!_eyeQuery.TryGetComponent(playerEntity, out var eyeComp) || args.Viewport.Eye != eyeComp.Eye)
            return false;

        return true;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter(SettingsScaleParameter, Math.Max(0f, CurrentStrength / SinCityOverlayComponent.DefaultBaseStrength));

        var handle = args.WorldHandle;
        var viewport = args.WorldBounds;

        handle.UseShader(_shader);
        handle.DrawRect(viewport, Color.White);
        handle.UseShader(null);
    }

    protected override void DisposeBehavior()
    {
        base.DisposeBehavior();

        _shader.Dispose();
    }
}
