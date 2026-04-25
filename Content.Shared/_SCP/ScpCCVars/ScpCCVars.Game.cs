using Robust.Shared.Configuration;

namespace Content.Shared._Scp.ScpCCVars;

public sealed partial class ScpCCVars
{
    /**
     * Авто-открытие меню персонажа при спавне
     */

    /// <summary>
    /// Будет ли автоматически открываться меню персонажа, если игрок первый раз на должности?
    /// Устанавливается на клиенте игроком
    /// </summary>
    public static readonly CVarDef<bool> AutoOpenCharacterMenuClientSideEnabled =
        CVarDef.Create("scp.auto_open_character_menu", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Включено ли автоматическое открытие меню персонажа при заходе на должность впервые?
    /// Устанавливается на сервере, определяет ли вообще это включено.
    /// Игроки могут отключить у себя это в настройках.
    /// </summary>
    public static readonly CVarDef<bool> AutoOpenCharacterMenuServerSideEnabled =
        CVarDef.Create("scp.server_auto_open_character_menu", true, CVar.SERVERONLY | CVar.ARCHIVE);

    /*
     * Сохранение мусора между раундами
     */

    /// <summary>
    /// Включено ли сохранение мусора между раундами?
    /// </summary>
    public static readonly CVarDef<bool> MetaGarbageEnableSaving =
        CVarDef.Create("scp.meta_garbage_enable_saving", true, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Включен ли спавн сохраненного между раундами мусора?
    /// </summary>
    public static readonly CVarDef<bool> MetaGarbageEnableSpawning =
        CVarDef.Create("scp.meta_garbage_enable_spawning", true, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Будет ли мусор спавниться при загрузке станции без запуска геймрула?
    /// </summary>
    public static readonly CVarDef<bool> MetaGarbageEnableSpawningWithoutRule =
        CVarDef.Create("scp.meta_garbage_enable_spawning_without_rule", false, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether the automatic ghost role system is enabled for disconnected SCP players.
    /// </summary>
    public static readonly CVarDef<bool> AutoGhostRoleEnabled =
        CVarDef.Create("scp.auto_ghost_role_enabled", true, CVar.SERVERONLY | CVar.ARCHIVE);
}
