using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Content.Shared._Crescent.Overlays;
using Robust.Shared.Player;

namespace Content.Client._Crescent.Overlays;

/// <summary>
///     A simple overlay that applies a static texture to the screen.
/// </summary>
public sealed class StaticOverlay : Overlay
{
    [Dependency] private readonly ISharedPlayerManager _playerMan = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] IEntityManager _entityManager = default!;

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    private readonly ShaderInstance _shader;

    /// <summary>
    ///     The color to tint the screen to as RGB on a scale of 0-1.
    /// </summary>
    public Color ScreenColor = new(0, 0, 0);
    /// <summary>
    ///     The percent to tint the screen by on a scale of 0-1.
    /// </summary>
    public float AdditionLevel = 0.1f;

    public StaticOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototype.Index<ShaderPrototype>("ScreenStatic").InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent(_playerManager.LocalEntity, out EyeComponent? eyeComp))
            return false;

        if (args.Viewport.Eye != eyeComp.Eye)
            return false;

        return AdditionLevel > 0;
    }
    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture is null)
            return;

        var handle = args.WorldHandle;
        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("addition_level", AdditionLevel);
        _shader.SetParameter("rgb_color", ScreenColor);
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}

/// <summary>
///     System to handle drug related overlays.
/// </summary>
public sealed class StaticOverlaySystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    private StaticOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StaticOverlayComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<StaticOverlayComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<StaticOverlayComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<StaticOverlayComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        _overlay = new();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!TryComp<StaticOverlayComponent>(_player.LocalEntity, out var component))
            return;

        _overlay.ScreenColor = component.StaticColor;
        _overlay.AdditionLevel = component.AdditionLevel;
    }

    private void OnPlayerAttached(EntityUid uid, StaticOverlayComponent component, LocalPlayerAttachedEvent args)
    {

        _overlay.ScreenColor = component.StaticColor;
        _overlay.AdditionLevel = component.AdditionLevel;
        _overlayMan.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(EntityUid uid, StaticOverlayComponent component, LocalPlayerDetachedEvent args)
    {
        _overlay.ScreenColor = Color.Black;
        _overlay.AdditionLevel = 0f;
        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnInit(EntityUid uid, StaticOverlayComponent component, ComponentInit args)
    {
        if (_player.LocalEntity == uid)
        {
            _overlay.ScreenColor = component.StaticColor;
            _overlay.AdditionLevel = component.AdditionLevel;
            _overlayMan.AddOverlay(_overlay);
        }

    }

    private void OnShutdown(EntityUid uid, StaticOverlayComponent component, ComponentShutdown args)
    {
        if (_player.LocalEntity == uid)
        {
            _overlay.ScreenColor = Color.Black;
            _overlay.AdditionLevel = 0f;
            _overlayMan.RemoveOverlay(_overlay);
        }
    }
}
