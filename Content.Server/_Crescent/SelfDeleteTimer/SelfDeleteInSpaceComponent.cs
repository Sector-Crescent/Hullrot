using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Server._Crescent.HullrotSelfDeleteTimer;

[RegisterComponent]
public sealed partial class SelfDeleteInSpaceComponent : Component
{
    [DataField("retryDelay", customTypeSerializer: typeof(TimespanSerializer))]
    public TimeSpan RetryDelay = TimeSpan.FromMinutes(1);
}
