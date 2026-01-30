using Robust.Shared.GameStates;

namespace Content.Shared._Crescent.Overlays;

/// <summary>
///     Causes static to overlay the viewport with a specific color.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StaticOverlayComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color StaticColor = new Color(58, 0, 0);

    [DataField, AutoNetworkedField]
    public float AdditionLevel = 0.5f;
}
