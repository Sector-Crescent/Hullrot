using System.Linq;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Client.Graphics;
using Content.Shared._Arcadis.Limbus;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.Client._Arcadis.Limbus;

public sealed class ConceptIncinerationVisualizerSystem : VisualizerSystem<BeingIncineratedComponent>
{
    [Dependency] private readonly SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    private ISawmill _sawmill = default!;
    private readonly static string ShaderName = "Erasure";
    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("ego");
    }

    public override void Update(float frameTime)
    {
        var enumerator = EntityQueryEnumerator<BeingIncineratedComponent, SpriteComponent>();
        while (enumerator.MoveNext(out var ent, out var comp, out var comp2))
        {
            for (var i = 0; i < comp2.AllLayers.Count(); ++i)
            {
                if (comp2.TryGetLayer(i, out var layer) && layer.Shader != null && layer.ShaderPrototype == "Erasure")
                {
                    layer.Shader.SetParameter("fade_level", comp.ErasedIntensity);
                }
            }
        }
    }

    protected override void OnAppearanceChange(EntityUid uid, BeingIncineratedComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!_appearanceSystem.TryGetData(uid, ErasureVisuals.BeingErased, out bool beingErased) || !beingErased)
        {
            for (var i = 0; i < args.Sprite.AllLayers.Count(); ++i)
                if (args.Sprite.TryGetLayer(i, out var layer) && layer.ShaderPrototype == "Erasure")
                {
                    args.Sprite.LayerSetShader(i, null, null);
                }

            return;
        }

        for (var i = 0; i < args.Sprite.AllLayers.Count(); ++i)
        {
            if (args.Sprite.TryGetLayer(i, out var layer) && layer.ShaderPrototype != "Erasure")
            {
                var shader = _protoMan.Index<ShaderPrototype>(ShaderName).InstanceUnique();
                shader.SetParameter("fade_level", component.ErasedIntensity);
                args.Sprite.LayerSetShader(i, shader);
                layer.ShaderPrototype = _protoMan.Index<ShaderPrototype>(ShaderName);
            }
        }
    }
}
