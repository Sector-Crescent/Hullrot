using Content.Shared.PointCannons;
using Robust.Shared.Timing;

namespace Content.Server.PointCannons;

[RegisterComponent]
public sealed partial class TargetingConsoleComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public Dictionary<string, List<EntityUid>> CannonGroups = new() { { "all", new() } };
    public HashSet<string> ActiveGroups = new();
    public string CurrentGroupName = string.Empty;
    //public List<EntityUid> CurrentGroup => CannonGroups[CurrentGroupName];
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public List<EntityUid> CurrentGroup = new();

    public bool RegenerateCannons = true;
    public TargetingConsoleBoundUserInterfaceState? PrevState;
}