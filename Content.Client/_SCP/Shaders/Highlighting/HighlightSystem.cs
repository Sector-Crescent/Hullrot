using Content.Shared._Scp.Shaders.Highlighting;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Scp.Shaders.Highlighting;

/// <summary>
/// Система, регулирующая добавление шейдера подсвечивания.
/// </summary>
public sealed class HighlightSystem : SharedHighlightSystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;

    private static readonly ProtoId<ShaderPrototype> ShaderProtoId = "HighlightWave";

    /// <summary>
    /// Шейдер подсвечивания.
    /// Будет накладываться на текстуры используя внутренние методы спрайта.
    /// </summary>
    private ShaderInstance _shader = default!;

    public override void Initialize()
    {
        base.Initialize();

        _shader = _prototype.Index(ShaderProtoId).InstanceUnique();

        SubscribeLocalEvent<HighlightedComponent, HighLightStartEvent>(OnHighlightStarted);
        SubscribeLocalEvent<SpriteComponent, HighLightEndEvent>(OnHighlightEnded);

        SubscribeNetworkEvent<HighLightStartEvent>(OnNetworkHighlightStarted);
        SubscribeNetworkEvent<HighLightEndEvent>(OnNetworkHighlightEnded);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _shader.Dispose();
    }

    private void OnHighlightStarted(Entity<HighlightedComponent> ent, ref HighLightStartEvent args)
    {
        StartHighlight(ent.AsNullable());
    }

    private void OnHighlightEnded(Entity<SpriteComponent> ent, ref HighLightEndEvent args)
    {
        EndHighlight(ent.AsNullable());
    }

    private void OnNetworkHighlightStarted(HighLightStartEvent args)
    {
        var uid = GetEntity(args.Entity);
        if (!uid.HasValue)
            return;

        StartHighlight(uid.Value);
    }

    private void OnNetworkHighlightEnded(HighLightEndEvent args)
    {
        var uid = GetEntity(args.Entity);
        if (!uid.HasValue)
            return;

        EndHighlight(uid.Value);
    }

    private void StartHighlight(Entity<HighlightedComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        if (ent.Comp.Recipient.HasValue && _player.LocalEntity != ent.Comp.Recipient)
            return;

        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (sprite.PostShader != _shader)
            sprite.PostShader = _shader;
    }

    private void EndHighlight(Entity<SpriteComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        if (ent.Comp.PostShader != _shader)
            return;

        ent.Comp.PostShader = null;
    }
}
