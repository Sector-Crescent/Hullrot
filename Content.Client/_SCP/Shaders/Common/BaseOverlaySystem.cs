using Robust.Client.Graphics;

namespace Content.Client._Scp.Shaders.Common;

/// <summary>
/// Базовая система для быстрого разворачивания шейдеров и оверлеев.
/// Устраняет проблемы копипасты и расхождения API для переключения шейдеров.
/// </summary>
/// <typeparam name="T">Оверлей, которым будет оперировать система.</typeparam>
/// <remarks>
/// Так как песочница не дает создавать оверлей внутри этой системы,
/// то каждая система-наследник обязана создавать оверлей вручную в своем методе инициализации
/// </remarks>
/// <seealso cref="Overlay"/>
/// <seealso cref="OverlayManager"/>
/// <br/> TODO: Поддержка nullability для <see cref="Overlay"/>
/// <br/> TODO: Базовый класс для всех оверлеев, использующих эту систему.
public abstract class BaseOverlaySystem<T> : EntitySystem where T : Overlay
{
    [Dependency] protected readonly IOverlayManager OverlayManager = default!;
    [Dependency] protected readonly CompatibilityModeActiveWarningSystem Compatibility = default!;

    protected T Overlay = default!;
    public bool Enabled = true;
    public bool DisableOnCompatibilityMode = true;

    public bool DisposeOnShutdown = true;

    public override void Shutdown()
    {
        base.Shutdown();

        if (!DisposeOnShutdown)
            return;

        OverlayManager.RemoveOverlay(Overlay);
        Overlay.Dispose();
    }

    #region Public API

    public void ToggleOverlay()
    {
        if (OverlayManager.HasOverlay<T>())
            RemoveOverlay();
        else
            AddOverlay();
    }

    public void ToggleOverlay(bool enable)
    {
        var exists = OverlayManager.HasOverlay<T>();

        if (enable && !exists)
            AddOverlay();
        else if (!enable && exists)
            RemoveOverlay();
    }

    public bool TryAddOverlay()
    {
        if (OverlayManager.HasOverlay<T>())
            return false;

        AddOverlay();
        return true;
    }

    public void AddOverlay()
    {
        if (!Enabled)
            return;

        if (!Compatibility.ShouldUseShaders && DisableOnCompatibilityMode)
            return;

        OverlayManager.AddOverlay(Overlay);
    }

    public bool TryRemoveOverlay()
    {
        if (!OverlayManager.HasOverlay<T>())
            return false;

        RemoveOverlay();
        return true;
    }

    public void RemoveOverlay()
    {
        OverlayManager.RemoveOverlay(Overlay);
    }

    #endregion
}
