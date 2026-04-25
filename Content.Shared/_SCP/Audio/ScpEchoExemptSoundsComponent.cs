using System.Collections.Generic;
using Robust.Shared.GameStates;

namespace Content.Shared._Scp.Audio;

/// <summary>
/// Declares which authored sounds emitted from an entity should stay dry in the SCP echo pipeline.
/// </summary>
/// <remarks>
/// The actual audio entities are transient and may begin playback immediately after spawn.
/// Storing the exemption on the stable source entity lets the client resolve the rule through the audio child's
/// parent transform without introducing a race with effect assignment.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ScpEchoExemptSoundsComponent : Component
{
    /// <summary>
    /// Exact audio file paths that must not receive the SCP environmental echo when spawned from this entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<string> ExemptPaths = [];
}
