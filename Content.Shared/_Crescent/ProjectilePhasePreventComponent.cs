using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;

namespace Content.Shared._Crescent;

[ByRefEvent]
public sealed class HullrotBulletHitEvent : EntityEventArgs
{
    public EntityUid selfEntity;
    public EntityUid hitEntity;
    public Fixture targetFixture = default!;
    public Fixture selfFixture = default!;
    public PhysicsComponent selfPhys = default!;
    public string selfFixtureKey = string.Empty;
    public string targetFixtureKey = string.Empty;
}
