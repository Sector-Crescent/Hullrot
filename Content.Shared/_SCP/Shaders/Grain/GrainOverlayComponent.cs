using Content.Shared._Scp.Helpers;
using Robust.Shared.GameStates;

namespace Content.Shared._Scp.Shaders.Grain;

/// <summary>
/// Компонент, отвечающий за параметры шейдера зернистости.
/// Наличие компонента необходимо для работы шейдера.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class GrainOverlayComponent : Component, IShaderStrength
{
    /// <summary>
    /// Максимальные и минимальные значения базовой силы шейдера зернистости.
    /// Эти пороги используются для настроек клиента и позволяет выбрать доступный диапазон
    /// </summary>
    public static readonly MinMaxExtended BaseStrengthLimit = new (200, 400);

    /// <inheritdoc/>
    [ViewVariables]
    private float _baseStrength;

    [ViewVariables]
    public float BaseStrength
    {
        get => _baseStrength;
        set => _baseStrength = Math.Clamp(value, BaseStrengthLimit.Min, BaseStrengthLimit.Max);
    }

    /// <inheritdoc/>
    [AutoNetworkedField, ViewVariables]
    public float AdditionalStrength { get; set; }

    /// <inheritdoc/>
    [ViewVariables]
    public float CurrentStrength => BaseStrength + AdditionalStrength;
}
