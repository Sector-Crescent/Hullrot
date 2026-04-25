using Robust.Client.Graphics;
using Robust.Shared.Player;

namespace Content.Client._Scp.Shaders.Common;

/// <summary>
/// Абстрактная прослойка для <see cref="BaseOverlaySystem"/>, добавляющая оверлей <see cref="Overlay"/>
/// при заходе игрока в любое тело. И удаляющая после
/// </summary>
/// <typeparam name="T">Оверлей, который будет использован системой</typeparam>
/// <seealso cref="BaseOverlaySystem"/>
public abstract class CommonOverlaySystem<T> : BaseOverlaySystem<T> where T : Overlay
{
    public override void Initialize()
    {
        base.Initialize();

        // Мир если бы сендбокса не существовало
        // Overlay = new T();

        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    protected virtual void OnPlayerAttached(LocalPlayerAttachedEvent args)
    {
        TryAddOverlay();
    }

    protected virtual void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        TryRemoveOverlay();
    }
}
