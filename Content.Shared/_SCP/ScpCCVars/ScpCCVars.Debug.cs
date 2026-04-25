using Robust.Shared.Configuration;

namespace Content.Shared._Scp.ScpCCVars;

public sealed partial class ScpCCVars
{
    /*
     * Безопасное время
     */

    /// <summary>
    /// Будут ли осуществляться проверки на безопасное время?
    /// </summary>
    public static readonly CVarDef<bool> SafeTimeEnabled =
        CVarDef.Create("scp.safe_time_enabled", true, CVar.SERVER | CVar.REPLICATED);
}
