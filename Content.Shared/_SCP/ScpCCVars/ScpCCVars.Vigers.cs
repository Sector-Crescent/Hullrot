using Robust.Shared.Configuration;

namespace Content.Shared._Scp.ScpCCVars;

public sealed partial class ScpCCVars
{
    /*
     * Vigers
     */

    /// <summary>
    /// Автоматически кикает пользователей из списка Vigers после подключения.
    /// </summary>
    public static readonly CVarDef<bool> VigersAutoKick =
        CVarDef.Create("scp.vigers_auto_kick", false, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Автоматически снимает админку у пользователей из списка Vigers после получения прав.
    /// </summary>
    public static readonly CVarDef<bool> VigersAutoDeadmin =
        CVarDef.Create("scp.vigers_auto_deadmin", false, CVar.SERVERONLY | CVar.ARCHIVE);
}
