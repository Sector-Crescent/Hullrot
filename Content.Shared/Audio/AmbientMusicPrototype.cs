using Content.Shared.Random;
using Content.Shared.Random.Rules;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Audio;

/// <summary>
/// Attaches a rules prototype to sound files to play ambience.
/// </summary>
[Prototype("ambientMusic")]
public sealed partial class AmbientMusicPrototype : IPrototype
{
    /// <summary>
    /// Decides if this music will play on top of other music or not.
    /// NOTE!!! THIS ISN'T DONE YET!!!
    /// BIOMES: 1;
    /// SHIP AMBIENT: 2;
    /// COMBAT MODE: 3;
    /// FUCKED BIOMES/HADAL: 4;
    /// ADMIN: 5+;
    /// </summary>
    [DataField(required: false)]
    public int Priority = 1;
    [IdDataField] public string ID { get; private set; } = string.Empty;

    /// <summary>
    /// Can we interrupt this ambience for a better prototype if possible?
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("interruptable")]
    public bool Interruptable = false;

    /// <summary>
    /// Do we fade-in. Useful for songs.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("fadeIn")]
    public bool FadeIn;

    [ViewVariables(VVAccess.ReadWrite), DataField("sound", required: true)]
    public SoundSpecifier Sound = default!;

    [ViewVariables(VVAccess.ReadWrite), DataField("rules", required: false, customTypeSerializer:typeof(PrototypeIdSerializer<RulesPrototype>))]
    public string Rules = string.Empty;
}
