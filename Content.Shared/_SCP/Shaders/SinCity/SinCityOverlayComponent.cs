using Content.Shared._Scp.Helpers;
using Robust.Shared.GameStates;

namespace Content.Shared._Scp.Shaders.SinCity;

/// <summary>
/// Компонент-маркер, отвечающий за шейдер "тусклости".
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class SinCityOverlayComponent : Component, IShaderStrength
{
    /// <summary>
    /// Базовая сила шейдера, которая соответствует его текущему стандартному виду.
    /// </summary>
    public const float DefaultBaseStrength = 100f;

    /// <summary>
    /// Максимальные и минимальные значения базовой силы шейдера
    /// Эти пороги используются для настроек клиента и позволяет выбрать доступный диапазон
    /// </summary>
    public static readonly MinMaxExtended BaseStrengthLimit = new (50, 150);

    /// <inheritdoc/>
    [ViewVariables]
    public float BaseStrength
    {
        get;
        set => field = Math.Clamp(value, BaseStrengthLimit.Min, BaseStrengthLimit.Max);
    } = DefaultBaseStrength;

    /// <inheritdoc/>
    [AutoNetworkedField, ViewVariables]
    public float AdditionalStrength { get; set; }

    /// <inheritdoc/>
    [ViewVariables]
    public float CurrentStrength => BaseStrength + AdditionalStrength;
}
