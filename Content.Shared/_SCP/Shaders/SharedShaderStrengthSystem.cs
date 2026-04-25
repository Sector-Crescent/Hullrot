using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Network;

namespace Content.Shared._Scp.Shaders;

public sealed class SharedShaderStrengthSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;

    /// <summary>
    /// Устанавливает базовую силу шейдера.
    /// </summary>
    /// <param name="ent">Сущность, к которой будет применено значение</param>
    /// <param name="value">Значение, которое будет установлено</param>
    /// <returns>Получилось/Не получилось</returns>
    public bool TrySetBaseStrength<T>(Entity<T?> ent, float value)
        where T : IComponent, IShaderStrength
    {
        // Базовая сила это чисто клиентский параметр
        // Поэтому ее задавать только на клиенте
        if (!_net.IsClient)
            return false;

        if (!Resolve(ent, ref ent.Comp))
            return false;

        ent.Comp.BaseStrength = value;

        return true;
    }

    /// <summary>
    /// Устанавливает базовую силу шейдера.
    /// </summary>
    /// <param name="ent">Сущность, к которой будет применено значение</param>
    /// <param name="value">Значение, которое будет установлено</param>
    /// <param name="component">Возвращаемый компонент с параметрами</param>
    /// <returns>Получилось/Не получилось</returns>
    public bool TrySetBaseStrength<T>(Entity<T?> ent, float value, [NotNullWhen(true)] out T? component)
        where T : Component, IShaderStrength
    {
        component = null;

        // Базовая сила это чисто клиентский параметр
        // Поэтому ее задавать только на клиенте
        if (!_net.IsClient)
            return false;

        if (!Resolve(ent, ref ent.Comp))
            return false;

        ent.Comp.BaseStrength = value;
        component = ent.Comp;

        return true;
    }

    /// <summary>
    /// Устанавливает дополнительную силу шейдера.
    /// </summary>
    /// <param name="ent">Сущность, к которой будет применено значение</param>
    /// <param name="value">Значение, которое будет установлено</param>
    /// <returns>Получилось/Не получилось</returns>
    public bool TrySetAdditionalStrength<T>(Entity<T?> ent, float value)
        where T : IComponent, IShaderStrength
    {
        if (!Resolve(ent, ref ent.Comp))
            return false;

        ent.Comp.AdditionalStrength = value;
        Dirty(ent);

        return true;
    }
}
