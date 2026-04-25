using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Scp.Shaders.Common.Grain;

/// <summary>
/// Шейдер зернистости с заданной силой <see cref="CurrentStrength"/>
/// </summary>
public sealed class GrainOverlay : Overlay
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;

    private readonly EntityQuery<EyeComponent> _eyeQuery;

    private readonly ShaderInstance _shader;
    private static readonly ProtoId<ShaderPrototype> ShaderProtoId = "Grain";

    /// <summary>
    /// Текущая сила шейдера
    /// </summary>
    [ViewVariables]
    public float CurrentStrength;

    public GrainOverlay()
    {
        IoCManager.InjectDependencies(this);

        _eyeQuery = _entManager.GetEntityQuery<EyeComponent>();

        _shader = _prototype.Index(ShaderProtoId).InstanceUnique();
    }

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public override bool RequestScreenTexture => true;

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (CurrentStrength == 0f)
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
        _shader.SetParameter("strength", CurrentStrength);

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
