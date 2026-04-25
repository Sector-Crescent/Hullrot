using Robust.Shared.Configuration;

namespace Content.Shared._Scp.ScpCCVars;

[CVarDefs]
public sealed partial class ScpCCVars
{
    /**
     * Шейдеры
     */

    /// <summary>
    /// Выключен ли шейдер зернистости?
    /// </summary>
    public static readonly CVarDef<bool> GrainToggleOverlay =
        CVarDef.Create("shader.grain_toggle_overlay", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Сила шейдера зернистости
    /// </summary>
    public static readonly CVarDef<int> GrainStrength =
        CVarDef.Create("shader.grain_strength", 70, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Выключен ли шейдер SinCity?
    /// </summary>
    public static readonly CVarDef<bool> SinCityToggleOverlay =
        CVarDef.Create("shader.sin_city_toggle_overlay", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Сила шейдера SinCity
    /// </summary>
    public static readonly CVarDef<int> SinCityStrength =
        CVarDef.Create("shader.sin_city_strength", 100, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Размер текстуры размытия у шейдера поля зрения
    /// </summary>
    public static readonly CVarDef<float> FieldOfViewBlurScale =
        CVarDef.Create("shader.field_of_view_blur_scale", 1.0f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Прозрачность наложения поля зрения
    /// </summary>
    public static readonly CVarDef<float> FieldOfViewOpacity =
        CVarDef.Create("shader.field_of_view_opacity", 0.55f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether the blur effect outside the field of view is enabled
    /// </summary>
    public static readonly CVarDef<bool> FieldOfViewBlurEnabled =
        CVarDef.Create("shader.field_of_view_blur_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /*
     * Режим совместимости
     */

    /// <summary>
    /// Будет ли игроку показываться окно с предупреждением о включенном режиме совместимости?
    /// Настраивается, потому что не у всех игроков может ВООБЩЕ работать игра без режима совместимости.
    /// </summary>
    public static readonly CVarDef<bool> CompatibilityModeShowWarning =
        CVarDef.Create("scp.compatibility_mode_show_warning", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Будут ли использоваться пораженные шейдеры, если включен режим совместимости?
    /// В случае, если игрок не может выключить режим совместимости, то лучше дать ему выбор. Использовать шейдеры или нет.
    /// </summary>
    public static readonly CVarDef<bool> CompatibilityModeUseShaders =
        CVarDef.Create("scp.compatibility_mode_use_shaders", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /*
     * Свечение лампочек
     */

    /// <summary>
    /// Будет ли использоваться эффект свечения у лампочек?
    /// Отвечает за главный рубильник для двух опций настройки.
    /// </summary>
    public static readonly CVarDef<bool> LightBloomEnable =
        CVarDef.Create("scp.light_bloom_enable", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Будет ли отображаться конус у эффекта свечения лампочек?
    /// </summary>
    public static readonly CVarDef<bool> LightBloomConeEnable =
        CVarDef.Create("scp.light_bloom_cone_enable", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Определяет прозрачность конуса свечения.
    /// Определяется в процентах от 0% до 100%
    /// </summary>
    public static readonly CVarDef<float> LightBloomConeOpacity =
        CVarDef.Create("scp.light_bloom_cone_opacity", 0.4f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// При включении не рисуется на невидимых для игрока позициях эффект, что увеличивает производительность ТОЛЬКО на слабых видеокартах.
    /// В остальных случаях снижает FPS из-за проверок на видимость. Поэтому это опционально.
    /// </summary>
    public static readonly CVarDef<bool> LightBloomOptimizations =
        CVarDef.Create("scp.light_bloom_optimizations", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Определяет силу эффекта свечения.
    /// Чем выше сила, тем сильнее эффект. Отображается в процентах от 0% до 100%
    /// </summary>
    public static readonly CVarDef<float> LightBloomStrength =
        CVarDef.Create("scp.light_bloom_strength", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);
}
