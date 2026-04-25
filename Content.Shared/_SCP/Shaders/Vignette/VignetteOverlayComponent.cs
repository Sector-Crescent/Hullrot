using Robust.Shared.GameStates;

namespace Content.Shared._Scp.Shaders.Vignette;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class VignetteOverlayComponent : Component, IShaderStrength
{
    /// <inheritdoc/>
    [ViewVariables]
    public float BaseStrength { get; set; } = 100f;

    /// <inheritdoc/>
    [AutoNetworkedField, ViewVariables]
    public float AdditionalStrength { get; set; }

    /// <inheritdoc/>
    [ViewVariables]
    public float CurrentStrength => BaseStrength + AdditionalStrength;
}
