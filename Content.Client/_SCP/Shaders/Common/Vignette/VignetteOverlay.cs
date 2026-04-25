using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Scp.Shaders.Common.Vignette;

public sealed class VignetteOverlay : Overlay
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;

    private readonly EntityQuery<EyeComponent> _eyeQuery;

    private readonly ShaderInstance _shader;
    private static readonly ProtoId<ShaderPrototype> ShaderProtoId = "Vignette";

    public float CurrentStrength;

    public VignetteOverlay()
    {
        IoCManager.InjectDependencies(this);

        _eyeQuery = _entManager.GetEntityQuery<EyeComponent>();

        _shader = _prototype.Index(ShaderProtoId).InstanceUnique();
    }

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public override bool RequestScreenTexture => true;

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (CurrentStrength == 0)
            return false;

        var playerEntity = _player.LocalEntity;

        if (!_eyeQuery.TryGetComponent(playerEntity, out var eyeComp) || args.Viewport.Eye != eyeComp.Eye)
            return false;

        return true;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture is null)
            return;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("vignette_color", Color.Black.WithAlpha(0.9f));
        _shader.SetParameter("effect_overall_strength", CurrentStrength);

        args.WorldHandle.UseShader(_shader);
        args.WorldHandle.DrawRect(args.WorldBounds, Color.White);
        args.WorldHandle.UseShader(null);
    }

    protected override void DisposeBehavior()
    {
        base.DisposeBehavior();

        _shader.Dispose();
    }
}
