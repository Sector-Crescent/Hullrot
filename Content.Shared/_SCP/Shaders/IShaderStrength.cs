namespace Content.Shared._Scp.Shaders;

public interface IShaderStrength
{
    /// <summary>
    /// Базовая сила шейдера.
    /// Она определяет то, какой силы будет шейдер при стандартных условиях.
    /// Может быть настроена клиентом на свое усмотрение.
    /// </summary>
    /// <remarks>
    /// КЛИЕНТСКИЙ ПАРАМЕТР -> НЕ МЕНЯТЬ С СЕРВЕРА!!
    /// </remarks>
    public float BaseStrength { get; set; }

    /// <summary>
    /// Дополнительная сила шейдера.
    /// Складывается с <see cref="BaseStrength"/> в <see cref="CurrentStrength"/>.
    /// Не может быть настроена клиентом, настраивается различными системами извне в качестве негативного эффекта.
    /// </summary>
    public float AdditionalStrength { get; set; }

    /// <summary>
    /// Текущая сила шейдера.
    /// Складывается из <see cref="BaseStrength"/> и <see cref="AdditionalStrength"/>.
    /// Определяет реальную силу шейдера, учитывая все параметры.
    /// </summary>
    public float CurrentStrength { get; }
}
